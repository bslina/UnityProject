using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    // Tools > Game UI > Add Character to Turn Hand-over
    // Adds to TurnHandover.unity: the minigame's character (cheering) on the left and a speech bubble
    // "Pass the phone to Sam!" on its right, like Egg Run's start panel. The plain Pass card stays for
    // minigames without a character. Existing objects are kept (run it again = only re-links).
    public static partial class RandomMatchBuilder
    {
        const string BubbleSpritePath = "Assets/EggRun/Character/UI/SpeechBubble.png";
        const string BubbleTailPath = "Assets/EggRun/Character/UI/SpeechBubbleTail.png";
        static readonly Color BubbleInk = new Color(0.22f, 0.15f, 0.10f);

        [MenuItem("Tools/Game UI/Add Character to Turn Hand-over", priority = 12)]
        public static void AddHandoverCharacterMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HandoverPath) == null)
            {
                EditorUtility.DisplayDialog("Turn hand-over", "No TurnHandover scene yet: run Tools > Game UI > Build Players + Turns first.", "OK");
                return;
            }
            string startScene = SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            ReadStyle();
            var scene = EditorSceneManager.OpenScene(HandoverPath, OpenSceneMode.Single);
            string report = AddHandoverCharacter();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(startScene) && startScene != HandoverPath) EditorSceneManager.OpenScene(startScene);
            EditorUtility.DisplayDialog("Turn hand-over", report, "OK");
        }

        // Works on the open TurnHandover scene.
        static string AddHandoverCharacter()
        {
            var ui = Object.FindAnyObjectByType<TurnHandoverUI>(FindObjectsInactive.Include);
            if (ui == null) return "No TurnHandoverUI in the scene.";
            var so = new SerializedObject(ui);
            var root = ui.transform;

            if (so.FindProperty("passCard").objectReferenceValue == null)
            {
                var card = Find("PassCard");
                if (card != null) so.FindProperty("passCard").objectReferenceValue = card;
            }
            if (so.FindProperty("characterRoot").objectReferenceValue != null)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                return "The character is already there (re-linked).";
            }

            var group = UI("Character", root);
            Stretch(group);
            group.SetSiblingIndex(Mathf.Min(root.childCount - 1, 2));   // above the background, under the texts/buttons

            // The cheering character (the minigame's Result Character, e.g. Egg Run's Excited).
            var portraitRT = UI("Portrait", group);
            Place(portraitRT, new Vector2(-580f, -110f), new Vector2(560f, 820f));
            var raw = portraitRT.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var portrait = portraitRT.gameObject.AddComponent<CharacterPortrait>();

            // Speech bubble growing out of its tail, next to the character's head.
            var bubble = UI("Bubble", group);
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = new Vector2(0f, 0.5f);
            bubble.anchoredPosition = new Vector2(-250f, 10f);
            bubble.sizeDelta = new Vector2(900f, 300f);
            var img = bubble.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BubbleSpritePath) ?? panelSprite;
            img.type = img.sprite != null && img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;

            var tailSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BubbleTailPath);
            if (tailSprite != null)
            {
                var tail = UI("Tail", bubble);
                tail.anchorMin = tail.anchorMax = new Vector2(0f, 0.5f);
                tail.pivot = new Vector2(1f, 0.5f);
                tail.anchoredPosition = new Vector2(16f, -30f);
                tail.sizeDelta = new Vector2(130f, 110f);
                var tailImg = tail.gameObject.AddComponent<Image>();
                tailImg.sprite = tailSprite;
                tailImg.raycastTarget = false;
            }

            var text = Label(bubble, "Text", "Pass the phone to Sam!\nBeat Lina's score.", 56,
                             tailSprite != null ? BubbleInk : Color.white, Vector2.zero, new Vector2(800f, 240f));
            var shadow = text.GetComponent<Shadow>();
            if (shadow != null && tailSprite != null) shadow.enabled = false;   // dark text on a white bubble

            so.FindProperty("characterRoot").objectReferenceValue = group.gameObject;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("bubble").objectReferenceValue = bubble;
            so.FindProperty("bubbleText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            return "Character + speech bubble added to TurnHandover (Character > Portrait / Bubble).\n" +
                   "It shows the minigame's Result Character (Egg Run: cheering) saying \"Pass the phone to ...!\".\n" +
                   "Move Portrait and Bubble wherever you like; the plain Pass card is used for games without a character.";
        }
    }
}
