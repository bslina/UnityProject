using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Egg Run > Add "Tap me!" Label
// Adds a small pulsing "Tap me!" text at the top right of the start panel character (CharacterGuide).
// It's a child of the character, so tapping the text also makes the character react.
// Afterwards it's a normal UI Text: move it, change the words, font, size or colour freely.
public static class EggRunTapMeLabel
{
    [MenuItem("Tools/Egg Run/Add \"Tap me!\" Label")]
    static void Add()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/PocketJungle/MiniGames/EggRun/Scenes/EggRun.unity", OpenSceneMode.Single);

        var guide = Object.FindAnyObjectByType<StartGuide>(FindObjectsInactive.Include);
        Transform character = null;
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
            if (t.name == "CharacterGuide") { character = t; break; }
        if (character == null)
        {
            EditorUtility.DisplayDialog("Egg Run", "No CharacterGuide in the start panel. Run Tools > Egg Run > Set Up Character first.", "OK");
            return;
        }

        var existing = character.Find("TapMe");
        GameObject label = existing != null ? existing.gameObject : null;
        if (label == null)
        {
            label = new GameObject("TapMe", typeof(RectTransform), typeof(Text), typeof(Outline));
            Undo.RegisterCreatedObjectUndo(label, "Tap me label");
            label.transform.SetParent(character, false);

            var rt = (RectTransform)label.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);      // top-right corner of the character
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-30f, -110f);
            rt.sizeDelta = new Vector2(260f, 80f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -8f);

            var text = label.GetComponent<Text>();
            var guideText = guide != null ? new SerializedObject(guide).FindProperty("bubbleText").objectReferenceValue as Text : null;
            text.font = guideText != null && guideText.font != null ? guideText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "Tap me!";
            text.fontSize = 48;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = true;   // tapping the words counts as tapping the character

            var outline = label.GetComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.13f, 0.08f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        if (guide != null)
        {
            var so = new SerializedObject(guide);
            so.FindProperty("tapMeLabel").objectReferenceValue = label.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.activeGameObject = label;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Egg Run",
            "\"Tap me!\" added at the top right of the character (CharacterGuide > TapMe, selected now).\n" +
            "Move it with the Rect Tool (T); change the words, font or colour in its Text component.", "OK");
    }
}
