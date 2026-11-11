using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    // Tools > Game UI > Gear + Pause Icons (white, no label)
    // The round Settings buttons (Levels, Players, Win, Lose) get the 2D Casual UI gear (GUI_43) and the
    // in-game Pause button (GameUI prefab) gets its pause icon (GUI_34): fully white and opaque, label hidden.
    // Only the icon and the label of those buttons are touched (the button's own art and position stay).
    public static class IconButtonsBuilder
    {
        const string Sheet = "Assets/ThirdParty/2D Casual UI/Sprite/GUI.png";
        const string Scenes = "Assets/PocketJungle/Shared/Scenes/";
        const string GameUIPrefab = "Assets/PocketJungle/Shared/Resources/GameUI.prefab";
        static readonly string[] SettingsScenes = { "LevelSelect", "Players", "WinMenu", "LoseMenu" };

        [MenuItem("Tools/Game UI/Gear + Pause Icons (white, no label)", priority = 15)]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToArray();
            Sprite gear = sprites.FirstOrDefault(s => s.name == "GUI_43");
            Sprite pause = sprites.FirstOrDefault(s => s.name == "GUI_34");
            if (gear == null || pause == null)
            {
                EditorUtility.DisplayDialog("Icons", "GUI_43 (gear) or GUI_34 (pause) not found in " + Sheet, "OK");
                return;
            }
            string startScene = SceneManager.GetActiveScene().path;
            string report = "";

            foreach (var name in SettingsScenes)
            {
                string path = Scenes + name + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;
                foreach (var b in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)))
                    if (b.name == "SettingsButton" && IsRoundButton(b) && SetIcon(b, gear)) n++;
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                report += name + ": " + n + " settings button(s) with the gear.\n";
            }

            var root = PrefabUtility.LoadPrefabContents(GameUIPrefab);
            int p = 0;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
                if (b.name == "PauseButton" && SetIcon(b, pause)) p++;
            PrefabUtility.SaveAsPrefabAsset(root, GameUIPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            report += "GameUI (in-game overlay): " + p + " pause button(s) with the pause icon.\n";

            if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
            EditorUtility.DisplayDialog("Icons", report, "OK");
        }

        // The small square icon buttons (not the wide text buttons of a menu list).
        static bool IsRoundButton(Button b)
        {
            var rt = (RectTransform)b.transform;
            Vector2 size = rt.rect.size;
            return b.transform.Find("Icon") != null || (size.x > 0f && Mathf.Abs(size.x - size.y) < 20f);
        }

        static bool SetIcon(Button b, Sprite sprite)
        {
            var iconT = b.transform.Find("Icon");
            if (iconT == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(b.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.2f, 0.2f); rt.anchorMax = new Vector2(0.8f, 0.8f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                iconT = go.transform;
            }
            iconT.gameObject.SetActive(true);
            var icon = iconT.GetComponent<Image>() ?? iconT.gameObject.AddComponent<Image>();
            icon.enabled = true;
            icon.sprite = sprite;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.color = Color.white;          // fully white, fully opaque
            icon.raycastTarget = false;

            // No label.
            foreach (var t in b.GetComponentsInChildren<Text>(true))
                if (t.transform != iconT) t.gameObject.SetActive(false);
            EditorUtility.SetDirty(b.gameObject);
            return true;
        }
    }
}
