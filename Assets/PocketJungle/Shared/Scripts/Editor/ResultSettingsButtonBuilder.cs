using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    // Tools > Game UI > Add Settings Button to Win + Lose
    // Copies the round Settings (gear) button of YOUR LevelSelect scene into the top-right corner of the
    // WinMenu and LoseMenu scenes and connects it to ResultUI. Only ADDS that button: nothing else in
    // those scenes is changed. A scene that already has a SettingsButton is left alone.
    public static class ResultSettingsButtonBuilder
    {
        const string Scenes = "Assets/PocketJungle/Shared/Scenes/";

        [MenuItem("Tools/Game UI/Add Settings Button to Win + Lose", priority = 14)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string startScene = SceneManager.GetActiveScene().path;
            string report = "";

            foreach (var name in new[] { "WinMenu", "LoseMenu" })
            {
                string path = Scenes + name + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { report += name + ": not found.\n"; continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var levels = EditorSceneManager.OpenScene(Scenes + "LevelSelect.unity", OpenSceneMode.Additive);
                report += name + ": " + AddButton(scene, levels) + "\n";
                EditorSceneManager.CloseScene(levels, true);   // LevelSelect is not changed
            }

            if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
            EditorUtility.DisplayDialog("Win + Lose", report, "OK");
        }

        static string AddButton(Scene scene, Scene levels)
        {
            var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ResultUI>(true)).FirstOrDefault();
            if (ui == null) return "no ResultUI, nothing added.";
            var so = new SerializedObject(ui);
            var prop = so.FindProperty("settingsButton");

            var existing = Find(scene, "SettingsButton");
            if (existing != null)
            {
                if (prop.objectReferenceValue == null && existing.GetComponent<Button>() != null)
                {
                    prop.objectReferenceValue = existing.GetComponent<Button>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.SaveScene(scene);
                    return "already has a SettingsButton (connected it).";
                }
                return "already has a SettingsButton (left as it is).";
            }

            var source = Find(levels, "SettingsButton");
            var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(c => c.isRootCanvas);
            if (canvas == null) return "no Canvas, nothing added.";

            GameObject button;
            if (source != null)
            {
                button = Object.Instantiate(source.gameObject, canvas.transform);
                var element = button.GetComponent<LayoutElement>();
                if (element != null) Object.DestroyImmediate(element);   // was for the Levels layout only
            }
            else
            {
                button = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
                button.transform.SetParent(canvas.transform, false);
                ((RectTransform)button.transform).sizeDelta = new Vector2(130f, 130f);
            }
            button.name = "SettingsButton";
            button.SetActive(true);
            button.transform.SetAsLastSibling();   // drawn on top
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);   // top-right corner
            rt.anchoredPosition = new Vector2(-40f, -40f);

            var b = button.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            prop.objectReferenceValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return source != null ? "Settings button added (top right, copied from Levels)." : "Settings button added (top right).";
        }

        static Transform Find(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
    }
}
