using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GameUI;

// Tools > Egg Run > Build Start Panel (character left, card right)
// Turns the Egg Run StartPanel into an opaque intro screen:
//   jungle backdrop | big waving character on a soft spotlight (tap it: it jumps for joy)
//   framed card on the right: title, tips bubble (pops in), progress dots, START.
// First run builds the layout; later runs only re-link (never move what you placed).
public static partial class EggRunCharacterSetup
{
    const string BackdropPath = "Assets/PocketJungle/Shared/Art/background/Serene forest scene with a peaceful atmosphere, trees, and soft light rays shining through the leaves_ Cartoon landscape illustration.jpg";
    const string GlowTexturePath = Folder + "/UI/SoftGlow.png";
    const string ShadowTexturePath = Folder + "/UI/SoftShadow.png";

    // Guide portrait: waves (Waving Gesture); "React" trigger = jumps for joy (Excited) once.
    static GameObject BuildGuidePrefab()
    {
        var prefab = BuildPortraitPrefab(WaveControllerPath, "Waving Gesture", GuidePrefabPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(WaveControllerPath);
        if (ctrl == null) return prefab;
        if (!ctrl.parameters.Any(p => p.name == "React"))
            ctrl.AddParameter("React", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var wave = sm.defaultState;
        if (wave != null && !sm.states.Any(s => s.state.name == "React"))
        {
            var react = sm.AddState("React", new Vector3(300, 120));
            react.motion = Clip("Excited");
            var go = wave.AddTransition(react);
            go.AddCondition(AnimatorConditionMode.If, 0f, "React");
            go.hasExitTime = false;
            go.duration = 0.15f;
            var back = react.AddTransition(wave);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.25f;
        }
        EditorUtility.SetDirty(ctrl);
        return prefab;
    }

    [MenuItem("Tools/Egg Run/Build Start Panel (character left, card right)")]
    public static void BuildStartPanelLayout()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            EditorUtility.DisplayDialog("Egg Run", "Can't find " + ModelPath, "OK");
            return;
        }
        SetupImports();
        var guidePrefab = BuildGuidePrefab();

        var scene = EditorSceneManager.OpenScene(EggRunScene, OpenSceneMode.Single);
        string report = AddStartGuide(guidePrefab);
        report += LayoutStartPanel();
        report += StyleSpeechBubble() + "\n";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!string.IsNullOrEmpty(startScene) && startScene != EggRunScene) EditorSceneManager.OpenScene(startScene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Egg Run] " + report);
        EditorUtility.DisplayDialog("Egg Run", report + "\nEverything is a normal UI object: move, resize or restyle it freely.", "OK");
    }

    static string LayoutStartPanel()
    {
        var panelGO = FindByName("StartPanel");
        if (panelGO == null) return "No StartPanel found.\n";
        var panel = panelGO.transform;
        var character = panel.Find("CharacterGuide");
        Transform card = panel.Find("Card");
        bool firstTime = card == null;
        Transform bubble = panel.Find("GuideBubble") ?? (card != null ? card.Find("GuideBubble") : null);

        if (firstTime)
        {
            // Opaque: nothing of the level shows through any more.
            var panelImage = panelGO.GetComponent<Image>();
            if (panelImage != null)
            {
                var c = panelImage.color; c.a = 1f; panelImage.color = c;
                EditorUtility.SetDirty(panelImage);
            }

            // Backdrop picture filling the screen (keeps its proportions, crops the edges).
            var backdrop = NewUI("Backdrop", panel);
            backdrop.SetSiblingIndex(0);
            Stretch(backdrop);
            var bgImage = backdrop.gameObject.AddComponent<Image>();
            bgImage.sprite = AssetDatabase.LoadAllAssetsAtPath(BackdropPath).OfType<Sprite>().FirstOrDefault();
            bgImage.raycastTarget = false;
            if (bgImage.sprite != null)
            {
                var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bgImage.sprite.rect.width / bgImage.sprite.rect.height;
            }
            else bgImage.color = new Color(0.36f, 0.68f, 0.45f);

            // Soft spotlight behind the character + a shadow under its feet.
            var glow = NewUI("Glow", panel);
            glow.SetSiblingIndex(1);
            var glowImage = glow.gameObject.AddComponent<RawImage>();
            glowImage.texture = SoftTexture(GlowTexturePath);
            glowImage.color = new Color(1f, 0.95f, 0.7f, 0.85f);
            glowImage.raycastTarget = false;
            PlaceLeft(glow, new Vector2(400f, -40f), new Vector2(820f, 820f));

            var shadow = NewUI("FloorShadow", panel);
            shadow.SetSiblingIndex(2);
            var shadowImage = shadow.gameObject.AddComponent<RawImage>();
            shadowImage.texture = SoftTexture(ShadowTexturePath);
            shadowImage.color = new Color(0f, 0f, 0f, 0.45f);
            shadowImage.raycastTarget = false;
            PlaceLeft(shadow, new Vector2(400f, -455f), new Vector2(440f, 90f));

            // Big character on the left.
            if (character != null)
            {
                character.SetSiblingIndex(3);
                PlaceLeft((RectTransform)character, new Vector2(400f, -40f), new Vector2(640f, 920f));
                var so = new SerializedObject(character.GetComponent<CharacterPortrait>());
                so.FindProperty("framing").floatValue = 1f;
                so.FindProperty("turnAngle").floatValue = -20f;    // turned towards the card
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Framed card on the right with the title, the tips and START.
            card = NewUI("Card", panel);
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = GuiSprite("GUI_28");
            cardImage.type = cardImage.sprite != null && cardImage.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (cardImage.sprite == null) cardImage.color = new Color(0.2f, 0.45f, 0.2f);
            var crt = (RectTransform)card;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(-540f, 0f);
            crt.sizeDelta = new Vector2(980f, 880f);

            MoveInto(panel.Find("Title"), card, new Vector2(0f, 300f), new Vector2(900f, 150f));
            if (bubble != null)
            {
                MoveInto(bubble, card, new Vector2(0f, 40f), new Vector2(860f, 330f));
                var img = bubble.GetComponent<Image>();
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                img.type = Image.Type.Sliced;
                img.color = new Color(0f, 0f, 0f, 0.28f);
            }
            var dots = NewUI("TipDots", card);
            dots.anchoredPosition = new Vector2(0f, -165f);
            dots.sizeDelta = new Vector2(600f, 40f);
            MoveInto(panel.Find("StartButton"), card, new Vector2(0f, -300f), Vector2.zero);
        }

        // (Re-)link the guide every time.
        if (bubble != null)
        {
            var guide = bubble.GetComponent<StartGuide>();
            var so = new SerializedObject(guide);
            so.FindProperty("popTarget").objectReferenceValue = bubble;
            var dots = card != null ? card.Find("TipDots") : null;
            if (dots != null) so.FindProperty("dotsParent").objectReferenceValue = dots;
            if (so.FindProperty("dotSprite").objectReferenceValue == null)
                so.FindProperty("dotSprite").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (character != null)
            {
                var button = character.GetComponent<Button>() ?? character.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                character.GetComponent<RawImage>().raycastTarget = true;
                so.FindProperty("characterButton").objectReferenceValue = button;
                so.FindProperty("portrait").objectReferenceValue = character.GetComponent<CharacterPortrait>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return firstTime
            ? "Start panel: opaque jungle backdrop, big character on the left (tap it!), card on the right with the title, tips, dots and START.\n"
            : "Start panel: layout already built, only re-linked (nothing moved).\n";
    }

    // White disc fading to transparent at the edge (tinted by the RawImage colour).
    static Texture2D SoftTexture(string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);                 // smooth falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void PlaceLeft(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // Moves one of your objects into the card; keeps its look, only sets where it sits.
    static void MoveInto(Transform child, Transform parent, Vector2 position, Vector2 size)
    {
        if (child == null) return;
        var rt = (RectTransform)child;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        if (size != Vector2.zero) rt.sizeDelta = size;
        rt.SetAsLastSibling();
    }
}
