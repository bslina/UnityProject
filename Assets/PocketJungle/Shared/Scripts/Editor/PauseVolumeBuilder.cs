using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    // Tools > Game UI > Add Volume Sliders to Pause Menu
    // Copies the Music and Sound effects rows of YOUR SettingsMenu scene (same look) into the Pause
    // menu's Window, under the buttons, and connects them to PauseMenuUI. Run it again = nothing added twice.
    public static class PauseVolumeBuilder
    {
        const string PausePath = "Assets/PocketJungle/Shared/Scenes/PauseMenu.unity";
        const string SettingsPath = "Assets/PocketJungle/Shared/Scenes/SettingsMenu.unity";

        [MenuItem("Tools/Game UI/Add Volume Sliders to Pause Menu", priority = 13)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string startScene = SceneManager.GetActiveScene().path;

            var pause = EditorSceneManager.OpenScene(PausePath, OpenSceneMode.Single);
            var settings = EditorSceneManager.OpenScene(SettingsPath, OpenSceneMode.Additive);

            var ui = Object.FindAnyObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
            string report;
            if (ui == null) report = "No PauseMenuUI in the Pause menu.";
            else
            {
                var so = new SerializedObject(ui);
                Transform window = FindIn(pause, "Window");
                if (window == null) report = "No Window object in the Pause menu.";
                else
                {
                    report = "";
                    report += AddRow(so, "musicSlider", FindIn(settings, "MusicRow"), window, "MusicRow");
                    report += AddRow(so, "sfxSlider", FindIn(settings, "SoundeffectsRow"), window, "SoundeffectsRow");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(pause);
                    EditorSceneManager.SaveScene(pause);
                }
            }

            EditorSceneManager.CloseScene(settings, true);   // the Settings scene is not changed
            if (!string.IsNullOrEmpty(startScene) && startScene != PausePath) EditorSceneManager.OpenScene(startScene);
            EditorUtility.DisplayDialog("Pause menu", report, "OK");
        }

        static string AddRow(SerializedObject so, string field, Transform source, Transform window, string name)
        {
            var prop = so.FindProperty(field);
            if (prop.objectReferenceValue != null) return name + ": already in the Pause menu.\n";
            if (source == null) return name + ": not found in the Settings menu.\n";

            var row = Object.Instantiate(source.gameObject, window);
            row.name = name;
            row.SetActive(true);
            row.transform.SetAsLastSibling();

            // Fit the Pause window's width (it's narrower than the Settings window).
            var layout = window.GetComponent<VerticalLayoutGroup>();
            float inner = ((RectTransform)window).sizeDelta.x - (layout != null ? layout.padding.left + layout.padding.right : 40);
            var element = row.GetComponent<LayoutElement>();
            float rowHeight = 70f;
            if (element != null)
            {
                if (element.preferredWidth > inner) element.preferredWidth = inner;
                if (element.preferredHeight > 0f) rowHeight = element.preferredHeight;
            }
            // Make the window taller for the new row.
            var wrt = (RectTransform)window;
            wrt.sizeDelta += new Vector2(0f, rowHeight + (layout != null ? layout.spacing : 20f));

            var slider = row.GetComponentInChildren<Slider>(true);
            prop.objectReferenceValue = slider;
            return name + ": added under the Pause buttons.\n";
        }

        static Transform FindIn(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
