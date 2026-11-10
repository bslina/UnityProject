using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GameUI;

// Tools > Egg Run > Set Up Character (keeps your edits)
// Wires the Mixamo character (Assets/EggRun/Character) into Egg Run:
//  1. Animation import settings (which clips loop, character stays in place).
//  2. EggRunCharacter.controller: Speed blend Idle <-> Walking, Win -> Victory, Lose -> Defeated,
//     plus an "Arms" layer (left arm only) that waves at the bird on Shoo.
//  3. Player in EggRun.unity: controller on the Animator, root motion off, PlayerAnimator, hand on the spoon.
//  4. Start panel: the character waves at you and gives the instructions in a speech bubble.
//  5. Win / Lose screens: the character cheers you on (Excited).
// Safe to run again: it never deletes or moves anything you placed; existing objects are only re-linked.
public static partial class EggRunCharacterSetup
{
    const string Folder = "Assets/EggRun/Character";
    const string ControllerPath = Folder + "/EggRunCharacter.controller";
    const string ArmsMaskPath = Folder + "/EggRunArmsMask.mask";
    const string WaveControllerPath = Folder + "/EggRunWave.controller";
    const string CheerControllerPath = Folder + "/EggRunCheer.controller";
    const string GuidePrefabPath = Folder + "/EggRunGuide.prefab";
    const string CheerPrefabPath = Folder + "/EggRunCheer.prefab";
    const string ModelPath = Folder + "/Breathing Idle.fbx";
    const string PortraitMaterialPath = Folder + "/Ch09_portrait.mat";   // the one imported "with skin"

    const string EggRunScene = "Assets/Scenes/EggRun.unity";
    const string WinScene = "Assets/UI/Scenes/WinMenu.unity";
    const string LoseScene = "Assets/UI/Scenes/LoseMenu.unity";
    const string RegistryPath = "Assets/UI/Resources/MiniGameRegistry.asset";
    const string GuiSheet = "Assets/2D Casual UI/Sprite/GUI.png";

    // Mixamo's "Waving" waves the right hand; mirrored, the left hand waves and the right keeps the spoon.
    const bool MirrorShoo = true;

    const string WinLine = "We did it! You're a natural!";
    const string LoseLine = "It's okay! I'm sure we can do it next time. Try again?";

    [MenuItem("Tools/Egg Run/Set Up Character (keeps your edits)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            EditorUtility.DisplayDialog("Egg Run", "Can't find " + ModelPath, "OK");
            return;
        }

        SetupImports();
        var controller = BuildGameController();
        var guidePrefab = BuildGuidePrefab();
        var cheerPrefab = BuildPortraitPrefab(CheerControllerPath, "Excited", CheerPrefabPath);

        string report = "";
        report += SetupEggRunScene(controller, guidePrefab);
        report += SetupResultScene(WinScene, true, cheerPrefab);
        report += SetupResultScene(LoseScene, false, cheerPrefab);
        report += SetupRegistry(cheerPrefab);

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Egg Run] Character set up.\n" + report);
        EditorUtility.DisplayDialog("Egg Run", "Character set up!\n\n" + report +
            "\nMove the new objects (CharacterGuide, GuideBubble, ResultCharacter) wherever you like.", "OK");
    }

    // ---------------------------------------------------------------- 1. import settings

    static void SetupImports()
    {
        SetClip("Breathing Idle", loop: true, inPlace: true);
        SetClip("Walking", loop: true, inPlace: false);   // root motion is off, so it walks in place anyway
        SetClip("Victory Idle", loop: true, inPlace: true);
        SetClip("Defeat Idle", loop: true, inPlace: true);
        SetClip("Excited", loop: true, inPlace: true);
        SetClip("Waving Gesture", loop: true, inPlace: true);
        SetClip("Waving", loop: false, inPlace: true);
    }

    static void SetClip(string file, bool loop, bool inPlace)
    {
        string path = Folder + "/" + file + ".fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) { Debug.LogWarning("[Egg Run] Missing " + path); return; }

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = false;
        foreach (var c in clips)
        {
            if (c.loopTime != loop || c.loopPose != loop || !c.lockRootRotation || !c.lockRootHeightY ||
                c.lockRootPositionXZ != inPlace) changed = true;
            c.loopTime = loop;
            c.loopPose = loop;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;  // never turns away
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;     // never sinks / floats
            c.lockRootPositionXZ = inPlace; c.keepOriginalPositionXZ = true;
        }
        if (!changed && importer.clipAnimations.Length > 0) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static AnimationClip Clip(string file)
    {
        string path = Folder + "/" + file + ".fbx";
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) Debug.LogWarning("[Egg Run] No animation found in " + path);
        return clip;
    }

    // ---------------------------------------------------------------- 2. controllers

    static AnimatorController BuildGameController()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Start from a clean controller (it only ever holds what this tool puts in it).
        while (ctrl.layers.Length > 1) ctrl.RemoveLayer(ctrl.layers.Length - 1);
        if (ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer");
        var sm = ctrl.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
        foreach (var p in ctrl.parameters.ToArray()) ctrl.RemoveParameter(p);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Win", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Lose", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Shoo", AnimatorControllerParameterType.Trigger);

        var layers = ctrl.layers;
        layers[0].iKPass = true;              // lets SpoonHandIK keep the hand on the spoon
        ctrl.layers = layers;

        // Base layer: Locomotion blend tree + Victory / Defeated from Any State.
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip("Breathing Idle"), 0f);
        tree.AddChild(Clip("Walking"), 1f);
        sm.defaultState = loco;
        loco.writeDefaultValues = true;

        AddEndState(sm, "Victory", Clip("Victory Idle"), "Win", new Vector3(300, 80));
        AddEndState(sm, "Defeated", Clip("Defeat Idle"), "Lose", new Vector3(300, 160));

        // Arms layer: only the left arm, only while shooing (PlayerAnimator fades the weight in/out).
        ctrl.AddLayer("Arms");
        layers = ctrl.layers;
        layers[1].avatarMask = BuildArmsMask();
        layers[1].defaultWeight = 0f;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        ctrl.layers = layers;

        var arms = ctrl.layers[1].stateMachine;
        var empty = arms.AddState("Empty", new Vector3(300, 0));
        arms.defaultState = empty;
        var shoo = arms.AddState("Shoo", new Vector3(300, 100));
        shoo.motion = Clip("Waving");
        shoo.mirror = MirrorShoo;
        shoo.speed = 1.4f;                    // a quick, annoyed wave
        var toShoo = arms.AddAnyStateTransition(shoo);
        toShoo.AddCondition(AnimatorConditionMode.If, 0f, "Shoo");
        toShoo.hasExitTime = false;
        toShoo.duration = 0.1f;
        toShoo.canTransitionToSelf = true;    // shake again = wave again
        var back = shoo.AddTransition(empty);
        back.hasExitTime = true;
        back.exitTime = 0.9f;
        back.duration = 0.2f;

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void AddEndState(AnimatorStateMachine sm, string name, AnimationClip clip, string trigger, Vector3 pos)
    {
        var state = sm.AddState(name, pos);
        state.motion = clip;
        var t = sm.AddAnyStateTransition(state);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.duration = 0.3f;
        t.canTransitionToSelf = false;
    }

    static AvatarMask BuildArmsMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ArmsMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, ArmsMaskPath);
        }
        bool leftWaves = MirrorShoo;
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var part = (AvatarMaskBodyPart)i;
            bool on = leftWaves
                ? part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.LeftFingers
                : part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.RightFingers;
            mask.SetHumanoidBodyPartActive(part, on);
        }
        EditorUtility.SetDirty(mask);
        return mask;
    }

    static GameObject BuildPortraitPrefab(string controllerPath, string clipFile, string prefabPath)
    {
        var clip = Clip(clipFile);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
        else if (ctrl.layers.Length > 0 && ctrl.layers[0].stateMachine.defaultState != null)
            ctrl.layers[0].stateMachine.defaultState.motion = clip;

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
        {
            // Keep any change made to the prefab; only fill what is missing.
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var a = contents.GetComponentInChildren<Animator>();
            if (a != null && a.runtimeAnimatorController == null) a.runtimeAnimatorController = ctrl;
            UsePortraitMaterial(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        UsePortraitMaterial(instance);
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    // Portraits use a copy of the body material with emission switched on, so the phone build keeps
    // the emission shader variant (CharacterPortrait's "Self Light" needs it).
    static void UsePortraitMaterial(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].name == "Ch09_portrait") continue;
                mats[i] = PortraitMaterial(mats[i]);
            }
            r.sharedMaterials = mats;
        }
    }

    static Material PortraitMaterial(Material source)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(PortraitMaterialPath);
        if (mat != null) return mat;
        mat = new Material(source) { name = "Ch09_portrait" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_BaseMap") && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", mat.GetTexture("_BaseMap"));
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.white * 0.45f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(mat, PortraitMaterialPath);
        return mat;
    }

    // ---------------------------------------------------------------- 3 + 4. Egg Run scene

    static string SetupEggRunScene(AnimatorController controller, GameObject guidePrefab)
    {
        var scene = EditorSceneManager.OpenScene(EggRunScene, OpenSceneMode.Single);
        string report = "";

        var walker = Object.FindAnyObjectByType<PlayerWalker>(FindObjectsInactive.Include);
        if (walker == null) return "EggRun: no Player (PlayerWalker) found!\n";

        Animator animator = walker.GetComponentsInChildren<Animator>(true)
            .FirstOrDefault(a => a.gameObject.activeInHierarchy && a.isHuman)
            ?? walker.GetComponentsInChildren<Animator>(true).FirstOrDefault();
        if (animator == null) report += "EggRun: no character Animator under Player (drag Breathing Idle under Player first).\n";
        else
        {
            Undo.RecordObject(animator, "Egg Run character");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var ik = animator.GetComponent<SpoonHandIK>() ?? animator.gameObject.AddComponent<SpoonHandIK>();
            var spoon = Object.FindAnyObjectByType<SpoonController>(FindObjectsInactive.Include);
            if (spoon != null)
            {
                var so = new SerializedObject(ik);
                var target = so.FindProperty("target");
                if (target.objectReferenceValue == null)
                {
                    var handle = spoon.transform.Find("Handle");
                    target.objectReferenceValue = handle != null ? handle : spoon.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            report += "EggRun: controller on \"" + animator.name + "\", root motion off, hand on the spoon.\n";
        }

        var pa = walker.GetComponent<PlayerAnimator>() ?? walker.gameObject.AddComponent<PlayerAnimator>();
        var paSo = new SerializedObject(pa);
        paSo.FindProperty("walker").objectReferenceValue = walker;
        if (animator != null) paSo.FindProperty("animator").objectReferenceValue = animator;
        paSo.ApplyModifiedPropertiesWithoutUndo();
        report += "EggRun: PlayerAnimator on Player.\n";

        report += AddStartGuide(guidePrefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return report;
    }

    static string AddStartGuide(GameObject guidePrefab)
    {
        var panel = FindByName("StartPanel");
        if (panel == null) return "EggRun: no StartPanel found, guide not added.\n";

        Transform info = panel.transform.Find("Info");
        Text infoText = info != null ? info.GetComponent<Text>() : null;
        Font font = infoText != null ? infoText.font : null;

        // The waving character.
        var character = panel.transform.Find("CharacterGuide");
        if (character == null)
        {
            var go = new GameObject("CharacterGuide", typeof(RectTransform), typeof(RawImage), typeof(CharacterPortrait));
            go.transform.SetParent(panel.transform, false);
            Place(go, new Vector2(-720f, -40f), new Vector2(420f, 640f));
            go.GetComponent<RawImage>().raycastTarget = false;
            character = go.transform;
        }
        var portrait = character.GetComponent<CharacterPortrait>() ?? character.gameObject.AddComponent<CharacterPortrait>();
        portrait.characterPrefab = guidePrefab;
        EditorUtility.SetDirty(portrait);

        // The speech bubble with the tips (replaces the Info text, which is hidden, not deleted).
        if (panel.transform.Find("GuideBubble") == null && panel.transform.Find("Card/GuideBubble") == null)
        {
            var bubble = new GameObject("GuideBubble", typeof(RectTransform), typeof(Image), typeof(Button), typeof(StartGuide));
            bubble.transform.SetParent(panel.transform, false);
            Place(bubble, new Vector2(110f, 10f), new Vector2(1100f, 240f));
            var image = bubble.GetComponent<Image>();
            image.sprite = GuiSprite("GUI_28");
            image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (image.sprite == null) image.color = new Color(0.2f, 0.45f, 0.2f, 0.9f);

            var text = NewText("Text", bubble.transform, font, 46, TextAnchor.MiddleCenter);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(60f, 50f); trt.offsetMax = new Vector2(-60f, -30f);
            text.text = "Hi! Help me carry this egg to the flag!";

            var hint = NewText("TapHint", bubble.transform, font, 28, TextAnchor.LowerRight);
            var hrt = hint.rectTransform;
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(40f, 18f); hrt.offsetMax = new Vector2(-50f, -18f);
            hint.text = "tap for next tip  >>";
            hint.color = new Color(1f, 1f, 1f, 0.75f);

            var guide = bubble.GetComponent<StartGuide>();
            var so = new SerializedObject(guide);
            so.FindProperty("bubbleText").objectReferenceValue = text;
            so.FindProperty("tapHint").objectReferenceValue = hint.gameObject;
            so.FindProperty("nextButton").objectReferenceValue = bubble.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            if (info != null) info.gameObject.SetActive(false);
            return "EggRun: waving guide + speech bubble added to StartPanel (old Info text hidden).\n";
        }
        return "EggRun: guide already there, re-linked.\n";
    }

    // ---------------------------------------------------------------- 5. Win / Lose screens

    static string SetupResultScene(string path, bool win, GameObject cheerPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return path + " not found.\n";
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        string label = win ? "WinMenu" : "LoseMenu";

        var existing = Object.FindAnyObjectByType<ResultCharacter>(FindObjectsInactive.Include);
        if (existing != null)
        {
            var so = new SerializedObject(existing);
            so.FindProperty("editorPreview").objectReferenceValue = cheerPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            return label + ": character already there, re-linked.\n";
        }

        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.isRootCanvas);
        if (canvas == null) return label + ": no Canvas found.\n";

        var resultUI = Object.FindAnyObjectByType<ResultUI>(FindObjectsInactive.Include);
        Font font = null;
        if (resultUI != null)
        {
            var detail = new SerializedObject(resultUI).FindProperty("detailText").objectReferenceValue as Text;
            if (detail != null) font = detail.font;
        }
        if (font == null)
        {
            var anyText = Object.FindAnyObjectByType<Text>(FindObjectsInactive.Include);
            if (anyText != null) font = anyText.font;
        }

        var root = new GameObject("ResultCharacter", typeof(RectTransform), typeof(ResultCharacter));
        root.transform.SetParent(canvas.transform, false);
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0f, 0f);    // bottom-left corner
        rrt.anchoredPosition = new Vector2(30f, 0f);
        rrt.sizeDelta = new Vector2(760f, 760f);
        root.transform.SetAsLastSibling();

        var character = new GameObject("Character", typeof(RectTransform), typeof(RawImage), typeof(CharacterPortrait));
        character.transform.SetParent(root.transform, false);
        var crt = (RectTransform)character.transform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 0f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(400f, 620f);
        character.GetComponent<RawImage>().raycastTarget = false;
        var portrait = character.GetComponent<CharacterPortrait>();
        portrait.characterPrefab = null;     // ResultCharacter fills it from the registry

        var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        bubble.transform.SetParent(root.transform, false);
        var brt = (RectTransform)bubble.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0f);
        brt.anchoredPosition = new Vector2(300f, 520f);
        brt.sizeDelta = new Vector2(460f, 200f);
        var image = bubble.GetComponent<Image>();
        image.sprite = GuiSprite("GUI_28");
        image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        if (image.sprite == null) image.color = new Color(0.2f, 0.45f, 0.2f, 0.9f);

        var text = NewText("Line", bubble.transform, font, 34, TextAnchor.MiddleCenter);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(36f, 30f); trt.offsetMax = new Vector2(-36f, -26f);
        text.text = win ? WinLine : LoseLine;

        var rc = root.GetComponent<ResultCharacter>();
        var rso = new SerializedObject(rc);
        rso.FindProperty("isWinScreen").boolValue = win;
        rso.FindProperty("portrait").objectReferenceValue = portrait;
        rso.FindProperty("lineText").objectReferenceValue = text;
        rso.FindProperty("root").objectReferenceValue = root;
        rso.FindProperty("editorPreview").objectReferenceValue = cheerPrefab;
        rso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return label + ": cheering character + speech bubble added (bottom-left).\n";
    }

    static string SetupRegistry(GameObject cheerPrefab)
    {
        var registry = AssetDatabase.LoadAssetAtPath<MiniGameRegistry>(RegistryPath);
        if (registry == null) return "Registry not found at " + RegistryPath + ".\n";
        var so = new SerializedObject(registry);
        var games = so.FindProperty("games");
        for (int i = 0; i < games.arraySize; i++)
        {
            var g = games.GetArrayElementAtIndex(i);
            string id = g.FindPropertyRelative("id").stringValue;
            string scene = g.FindPropertyRelative("sceneName").stringValue;
            if (id != "egg_run" && scene != "EggRun") continue;

            g.FindPropertyRelative("resultCharacter").objectReferenceValue = cheerPrefab;
            var winLine = g.FindPropertyRelative("winLine");
            var loseLine = g.FindPropertyRelative("loseLine");
            if (string.IsNullOrEmpty(winLine.stringValue)) winLine.stringValue = WinLine;
            if (string.IsNullOrEmpty(loseLine.stringValue)) loseLine.stringValue = LoseLine;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "Registry: Egg Run shows the character on Win / Lose.\n";
        }
        return "Registry: no Egg Run entry found.\n";
    }

    // ---------------------------------------------------------------- helpers

    static GameObject FindByName(string name)
    {
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name == name && t.gameObject.scene.IsValid())
            .Select(t => t.gameObject).FirstOrDefault();
    }

    static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static Text NewText(string name, Transform parent, Font font, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, size / 2);
        text.resizeTextMaxSize = size;
        text.raycastTarget = false;
        go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.45f);
        return text;
    }

    static Sprite GuiSprite(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(GuiSheet).OfType<Sprite>().FirstOrDefault(s => s.name == name);
    }
}
