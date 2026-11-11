using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SoundCopy.UI;

namespace SoundCopy.EditorTools
{
    // Tools > Sound Copy > Pocket Jungle Screens (keeps your edits)
    // Adapts Sound Copy's screens (Main.unity) to Pocket Jungle. Nothing is deleted: removed things are
    // only switched OFF (tick them back on to restore them), new things are added next to your objects.
    //  - every screen: no BackButton (the shared pause menu is the way out)
    //  - Setup: no players / names (typed in the Players scene) and no "3 rounds" box; the category
    //    column is centred and its label becomes the subtitle "Pick a category..."
    //  - Spin: a wheel with the 2 players' names (turns with the phone, or with SPIN THE WHEEL) and a
    //    pointer, instead of the phone drawing; the title asks "WHO GOES FIRST?"
    //  - Recording: a real microphone drawing instead of the 4 shapes
    // Run it again = nothing is added twice.
    public static class SoundCopyPocketJungleScreens
    {
        const string ScenePath = "Assets/PocketJungle/MiniGames/SoundCopy/Scenes/Main.unity";
        const string ArtDir = "Assets/PocketJungle/MiniGames/SoundCopy/Art/PocketJungle";

        static readonly Color Ink = new Color(0.17f, 0.12f, 0.16f);

        [MenuItem("Tools/Sound Copy/Pocket Jungle Screens (keeps your edits)")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string report = "";

            report += HideBackButtons(scene);
            report += Setup(scene);
            report += Spin(scene);
            report += Microphone(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Sound Copy", report + "\nSwitched-off objects are still there: tick them on to bring them back.", "OK");
        }

        // ---------------------------------------------------------------- every screen

        static string HideBackButtons(Scene scene)
        {
            int n = 0;
            foreach (var t in All(scene).Where(t => t.name == "BackButton"))
                if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); n++; }
            return "Back buttons switched off: " + n + ".\n";
        }

        // ---------------------------------------------------------------- setup

        static string Setup(Scene scene)
        {
            var panel = All(scene).Select(t => t.GetComponent<SetupPanel>()).FirstOrDefault(p => p != null);
            if (panel == null) return "Setup: not found.\n";
            var page = panel.transform.Find("Page");
            if (page == null) return "Setup: no Page.\n";

            foreach (var name in new[] { "LabelJOUEURS", "Player1", "Player2", "MatchInfo" })
            {
                var t = page.Find(name);
                if (t != null) t.gameObject.SetActive(false);
            }
            // Centre the category column (it was the right half, next to the players).
            foreach (Transform child in page)
            {
                if (!(child.name.StartsWith("Category") || child.name.StartsWith("LabelCAT"))) continue;
                var rt = (RectTransform)child;
                if (rt.anchorMin.x >= 0.49f && rt.anchorMax.x >= 0.99f)
                {
                    rt.anchorMin = new Vector2(0.2f, rt.anchorMin.y);
                    rt.anchorMax = new Vector2(0.8f, rt.anchorMax.y);
                }
                if (child.name.StartsWith("LabelCAT"))
                {
                    var label = child.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null)
                    {
                        label.text = "Pick a category of sounds, then start the match!";
                        label.alignment = TextAlignmentOptions.Center;
                        label.textWrappingMode = TextWrappingModes.Normal;
                    }
                }
            }
            return "Setup: players, names and the rounds box switched off; category centred with a subtitle.\n";
        }

        // ---------------------------------------------------------------- spin wheel

        static string Spin(Scene scene)
        {
            var panel = All(scene).Select(t => t.GetComponent<SpinPanel>()).FirstOrDefault(p => p != null);
            if (panel == null) return "Spin: not found.\n";
            var page = panel.transform.Find("Page");
            if (page == null) return "Spin: no Page.\n";

            var title = page.Find("TopBar/Title");
            var titleText = title != null ? title.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (titleText != null) titleText.text = "WHO GOES FIRST?";

            var so = new SerializedObject(panel);
            if (so.FindProperty("wheel").objectReferenceValue != null) return "Spin: the wheel is already there (title updated).\n";

            var phone = page.Find("PhoneIcon") as RectTransform;
            foreach (var name in new[] { "PhoneIcon", "TopSide", "BottomSide" })
            {
                var t = page.Find(name);
                if (t != null) t.gameObject.SetActive(false);
            }

            Sprite disc = Disc(), half = HalfDisc(), pointer = Pointer();
            TMP_FontAsset font = titleText != null ? titleText.font : null;

            // Area: a square where the phone drawing was.
            var area = NewRect("WheelArea", page);
            if (phone != null)
            {
                var corners = new Vector3[4];
                phone.GetWorldCorners(corners);
                Vector3 a = page.InverseTransformPoint(corners[0]), b = page.InverseTransformPoint(corners[2]);
                float size = Mathf.Min(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
                area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
                area.pivot = new Vector2(0.5f, 0.5f);
                var pageRect = ((RectTransform)page).rect;
                area.anchoredPosition = (Vector2)((a + b) * 0.5f) - pageRect.center;
                area.sizeDelta = Vector2.one * Mathf.Max(200f, size);
            }
            else
            {
                area.anchorMin = area.anchorMax = new Vector2(0.27f, 0.45f);
                area.sizeDelta = new Vector2(560f, 560f);
            }
            area.SetSiblingIndex(phone != null ? phone.GetSiblingIndex() : 1);

            // The wheel itself (turns).
            var wheel = NewRect("Wheel", area);
            Stretch(wheel, 0.06f);
            Img(NewRect("Rim", wheel), disc, Ink, 0f);
            var half2 = Img(NewRect("Half2", wheel), half, MobileUI.PlayerColor(2), 0.035f);   // top = player 2
            var h1 = NewRect("Half1", wheel);
            var half1 = Img(h1, half, MobileUI.PlayerColor(1), 0.035f);                       // bottom = player 1
            h1.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var divider = NewRect("Divider", wheel);
            divider.anchorMin = new Vector2(0.035f, 0.49f); divider.anchorMax = new Vector2(0.965f, 0.51f);
            divider.offsetMin = divider.offsetMax = Vector2.zero;
            divider.gameObject.AddComponent<Image>().color = Ink;
            var name2 = Label(wheel, "Name2", "PLAYER 2", font, new Vector2(0.14f, 0.56f), new Vector2(0.86f, 0.86f));
            var name1 = Label(wheel, "Name1", "PLAYER 1", font, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.44f));
            var hub = NewRect("Hub", wheel);
            hub.anchorMin = new Vector2(0.42f, 0.42f); hub.anchorMax = new Vector2(0.58f, 0.58f);
            hub.offsetMin = hub.offsetMax = Vector2.zero;
            var hubImg = hub.gameObject.AddComponent<Image>(); hubImg.sprite = disc; hubImg.color = Ink; hubImg.raycastTarget = false;

            // The pointer (doesn't turn): at the top, pointing down at the winner's half.
            var tip = NewRect("Pointer", area);
            tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 1f);
            tip.pivot = new Vector2(0.5f, 1f);
            tip.anchoredPosition = new Vector2(0f, 6f);
            tip.sizeDelta = new Vector2(90f, 80f);
            var tipImg = tip.gameObject.AddComponent<Image>(); tipImg.sprite = pointer; tipImg.color = Color.white; tipImg.raycastTarget = false;
            var shadow = tip.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0f, 0f, 0f, 0.5f); shadow.effectDistance = new Vector2(3f, -4f);

            so.FindProperty("wheel").objectReferenceValue = wheel;
            so.FindProperty("wheelHalf1").objectReferenceValue = half1;
            so.FindProperty("wheelHalf2").objectReferenceValue = half2;
            so.FindProperty("wheelName1").objectReferenceValue = name1;
            so.FindProperty("wheelName2").objectReferenceValue = name2;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "Spin: wheel with the players' names added (WheelArea), phone drawing switched off, title \"WHO GOES FIRST?\".\n";
        }

        // ---------------------------------------------------------------- microphone

        static string Microphone(Scene scene)
        {
            var panel = All(scene).Select(t => t.GetComponent<RecordingPanel>()).FirstOrDefault(p => p != null);
            if (panel == null) return "Recording: not found.\n";
            var icon = All(scene).FirstOrDefault(t => t.name == "MicIcon" && t.IsChildOf(panel.transform));
            if (icon == null) return "Recording: no MicIcon.\n";
            if (icon.Find("Microphone") != null) return "Recording: the microphone drawing is already there.\n";

            Color color = Ink;
            var head = icon.Find("Head");
            if (head != null && head.GetComponent<Graphic>() != null) color = head.GetComponent<Graphic>().color;
            foreach (var name in new[] { "Head", "Cradle", "Stem", "Foot" })
            {
                var t = icon.Find(name);
                if (t != null) t.gameObject.SetActive(false);
            }
            var mic = NewRect("Microphone", icon);
            mic.anchorMin = new Vector2(0.28f, 0.14f); mic.anchorMax = new Vector2(0.72f, 0.86f);
            mic.offsetMin = mic.offsetMax = Vector2.zero;
            var img = mic.gameObject.AddComponent<Image>();
            img.sprite = MicrophoneSprite();
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return "Recording: new microphone drawing (MicIcon > Microphone), old shapes switched off.\n";
        }

        // ---------------------------------------------------------------- helpers

        static System.Collections.Generic.IEnumerable<Transform> All(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.one * inset; rt.anchorMax = Vector2.one * (1f - inset);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, float inset)
        {
            Stretch(rt, inset);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Label(RectTransform parent, string name, string text, TMP_FontAsset font, Vector2 min, Vector2 max)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.color = Ink;
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 24f; t.fontSizeMax = 80f;
            t.fontStyle = FontStyles.Bold;
            t.raycastTarget = false;
            return t;
        }

        // ---- generated art (white shapes, tinted by the Image colour)

        static Sprite Disc() => Shape("WheelDisc", 256, 256, (x, y) => 0.5f - Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)));

        static Sprite HalfDisc() => Shape("WheelHalf", 256, 256, (x, y) =>
            Mathf.Min(0.5f - Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)), y - 0.5f));

        static Sprite Pointer() => Shape("WheelPointer", 128, 112, (x, y) =>
        {
            // triangle pointing down: tip at (0.5, 0.04), base at the top
            float left = (x - 0.5f) + (y - 0.04f) * 0.55f, right = (0.5f - x) + (y - 0.04f) * 0.55f;
            return Mathf.Min(Mathf.Min(left, right) * 0.85f, 0.96f - y);
        });

        // A studio microphone: grille head with slots, U-shaped holder, stem and base.
        static Sprite MicrophoneSprite() => Shape("Microphone", 256, 384, (x, y) =>
        {
            Vector2 p = new Vector2(x, y * 1.5f);                 // work in a 1 x 1.5 box
            float head = Capsule(p, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 1.22f), 0.2f);
            // grille slots (cut out of the head)
            float slots = 1f;
            for (int i = 0; i < 4; i++)
            {
                float sy = 0.92f + i * 0.085f;
                slots = Mathf.Min(slots, Mathf.Max(Mathf.Abs(p.y - sy) - 0.012f, Mathf.Abs(p.x - 0.5f) - 0.12f));
            }
            head = Mathf.Min(head, slots);
            // holder: lower half of a ring around the head + its two arms
            float ringDist = Mathf.Abs(Vector2.Distance(p, new Vector2(0.5f, 0.86f)) - 0.27f);
            float ring = p.y <= 0.86f ? 0.03f - ringDist : -1f;
            float armL = Box(p, new Vector2(0.23f, 0.86f), new Vector2(0.03f, 0.07f));
            float armR = Box(p, new Vector2(0.77f, 0.86f), new Vector2(0.03f, 0.07f));
            float stem = Box(p, new Vector2(0.5f, 0.45f), new Vector2(0.032f, 0.14f));
            float baseBar = Box(p, new Vector2(0.5f, 0.29f), new Vector2(0.2f, 0.035f));
            return Mathf.Max(Mathf.Max(head, ring), Mathf.Max(Mathf.Max(armL, armR), Mathf.Max(stem, baseBar)));
        });

        // signed "inside" distances (positive inside), in the shape's normalised units
        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return r - Vector2.Distance(p, a + ab * t);
        }

        static float Box(Vector2 p, Vector2 center, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - center.x) - half.x, Mathf.Abs(p.y - center.y) - half.y);
            float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            return -(outside + Mathf.Min(Mathf.Max(d.x, d.y), 0f));
        }

        static Sprite Shape(string name, int w, int h, System.Func<float, float, float> inside)
        {
            string path = ArtDir + "/" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(ArtDir);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float px = 1f / w;   // one pixel, for smooth (anti-aliased) edges
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = inside((x + 0.5f) / w, (y + 0.5f) / h);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(d / px + 0.5f)));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
