using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    // Tools > Game UI > Build Players + Turns (scenes + wiring)
    //  - Assets/UI/Scenes/Players.unity       "Who's playing?": the 2 names (+ recent duos), after PLAY
    //  - Assets/UI/Scenes/TurnHandover.unity  "Pass the phone to Sam!" between the 2 turns of a solo game
    //  - LevelSelect: "Lina  vs  Sam" under the title + a PLAYERS button (change the names)
    //  - WinMenu: its Title text shows the winner ("LINA WINS!")
    //  - both scenes added to the build list
    // Styled like your main menu. Existing scenes/objects are kept as they are (your edits stay).
    public static partial class RandomMatchBuilder
    {
        const string PlayersPath = "Assets/UI/Scenes/Players.unity";
        const string HandoverPath = "Assets/UI/Scenes/TurnHandover.unity";
        const string WinMenuPath = "Assets/UI/Scenes/WinMenu.unity";

        [MenuItem("Tools/Game UI/Build Players + Turns (scenes + wiring)", priority = 11)]
        public static void BuildPlayers()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string startScene = SceneManager.GetActiveScene().path;
            string report = "";

            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            ReadStyle();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayersPath) == null) { BuildPlayersScene(); report += "Players scene created.\n"; }
            else report += "Players scene already exists: kept.\n";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HandoverPath) == null) { BuildHandoverScene(); report += "TurnHandover scene created.\n"; }
            else report += "TurnHandover scene already exists: kept.\n";

            report += WireLevels();
            report += WireWinMenu();

            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in new[] { PlayersPath, HandoverPath })
                if (!scenes.Any(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
            EditorUtility.DisplayDialog("Players + Turns", report + "\nEvery new element is a normal UI object: move and restyle it freely.", "OK");
        }

        // ---------------------------------------------------------------- scenes

        static Transform NewStyledScene(string canvasName)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.32f, 0.2f);
            cam.orthographic = true;
            camGO.AddComponent<AudioListener>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGO.transform;

            var bg = UI("Background", root);
            Stretch(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = backgroundSprite;
            bgImg.color = backgroundSprite != null ? backgroundColor : new Color(0.2f, 0.4f, 0.25f);
            bgImg.raycastTarget = false;
            var dim = UI("Dim", root);
            Stretch(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.35f);
            dimImg.raycastTarget = false;
            return root;
        }

        static void BuildPlayersScene()
        {
            var root = NewStyledScene("PlayersCanvas");
            var ui = root.gameObject.AddComponent<PlayersUI>();

            Title(root, "WHO'S PLAYING?", 390);
            Label(root, "Subtitle", "Every game is played by these 2 players", 44, Color.white, new Vector2(0, 285), new Vector2(1500, 80));
            Label(root, "Player1Label", "PLAYER 1", 54, P1Color, new Vector2(-440, 170), new Vector2(640, 80));
            var in1 = Input(root, "Player1Name", "Name of player 1", new Vector2(-440, 60));
            Label(root, "Player2Label", "PLAYER 2", 54, P2Color, new Vector2(440, 170), new Vector2(640, 80));
            var in2 = Input(root, "Player2Name", "Name of player 2", new Vector2(440, 60));
            var swap = Btn(root, "SwapButton", "<>", new Vector2(0, 60), new Vector2(130, 110), 50);

            Label(root, "RecentLabel", "Played before:", 40, new Color(1f, 1f, 1f, 0.85f), new Vector2(0, -75), new Vector2(800, 60));
            var recent = new Button[4];
            for (int i = 0; i < 4; i++)
                recent[i] = Btn(root, "RecentDuo" + (i + 1), "Lina & Sam", new Vector2(-645 + 430 * i, -165), new Vector2(400, 100), 38);

            var play = Btn(root, "PlayButton", "LET'S PLAY!", new Vector2(0, -360), new Vector2(680, 160), 66);
            var back = Btn(root, "BackButton", "Back", new Vector2(-760, 420), new Vector2(280, 110), 46);

            var so = new SerializedObject(ui);
            so.FindProperty("player1Input").objectReferenceValue = in1;
            so.FindProperty("player2Input").objectReferenceValue = in2;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("swapButton").objectReferenceValue = swap;
            var arr = so.FindProperty("recentDuoButtons");
            arr.arraySize = recent.Length;
            for (int i = 0; i < recent.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = recent[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), PlayersPath);
        }

        static void BuildHandoverScene()
        {
            var root = NewStyledScene("TurnHandoverCanvas");
            var ui = root.gameObject.AddComponent<TurnHandoverUI>();

            var title = Title(root, "EGG RUN: LINA'S TURN IS OVER!", 360);
            var detail = Label(root, "Detail", "Lina: 23.4 s left", 60, Color.white, new Vector2(0, 200), new Vector2(1600, 120));
            var card = UI("PassCard", root);
            Place(card, new Vector2(0, -20), new Vector2(1300, 280));
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = panelSprite;
            img.type = panelSprite != null && panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (panelSprite == null) img.color = new Color(0.2f, 0.45f, 0.2f, 0.95f);
            var pass = Label(card, "Pass", "Pass the phone to Sam!\nBeat Lina's score.", 66, Color.white, Vector2.zero, new Vector2(1200, 240));
            var ready = Btn(root, "ReadyButton", "SAM, I'M READY!", new Vector2(0, -330), new Vector2(760, 160), 60);
            var quit = Btn(root, "QuitButton", "Quit", new Vector2(-760, 420), new Vector2(280, 110), 46);

            var so = new SerializedObject(ui);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("detailText").objectReferenceValue = detail;
            so.FindProperty("passText").objectReferenceValue = pass;
            so.FindProperty("readyButton").objectReferenceValue = ready;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("passCard").objectReferenceValue = card.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            AddHandoverCharacter();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), HandoverPath);
        }

        // ---------------------------------------------------------------- existing scenes

        static string WireLevels()
        {
            var scene = EditorSceneManager.OpenScene(LevelSelectPath, OpenSceneMode.Single);
            var ui = Object.FindAnyObjectByType<LevelSelectUI>(FindObjectsInactive.Include);
            if (ui == null) return "LevelSelect: no LevelSelectUI found.\n";
            var so = new SerializedObject(ui);
            var safe = Find("SafeArea");
            Transform parent = safe != null ? safe.transform : ui.transform;
            string report = "";

            var textProp = so.FindProperty("playersText");
            if (textProp.objectReferenceValue == null)
            {
                var t = Label(parent, "PlayersText", "Lina  vs  Sam", 50, labelColor, Vector2.zero, new Vector2(1000, 70));
                var rt = (RectTransform)t.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -145f);
                textProp.objectReferenceValue = t;
                report += "LevelSelect: \"Lina vs Sam\" added under the title.\n";
            }
            var buttonProp = so.FindProperty("changePlayersButton");
            if (buttonProp.objectReferenceValue == null)
            {
                var b = Btn(parent, "ChangePlayersButton", "PLAYERS", Vector2.zero, new Vector2(300, 110), 42);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-180f, -30f);     // left of the settings button
                buttonProp.objectReferenceValue = b;
                report += "LevelSelect: PLAYERS button added (top right).\n";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return report == "" ? "LevelSelect: already wired.\n" : report;
        }

        static string WireWinMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WinMenuPath) == null) return "";
            var scene = EditorSceneManager.OpenScene(WinMenuPath, OpenSceneMode.Single);
            var ui = Object.FindAnyObjectByType<ResultUI>(FindObjectsInactive.Include);
            if (ui == null) return "WinMenu: no ResultUI found.\n";
            var so = new SerializedObject(ui);
            var prop = so.FindProperty("titleText");
            if (prop.objectReferenceValue != null) return "WinMenu: already wired.\n";
            var title = Find("Title");
            var text = title != null ? title.GetComponent<Text>() : null;
            if (text == null) return "WinMenu: no Title text found; drag one into ResultUI > Title Text.\n";
            prop.objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "WinMenu: its Title now shows the winner (\"LINA WINS!\").\n";
        }
    }
}
