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
    // Tools > Game UI > Build Random Match (scene + main menu button)
    //  - Assets/PocketJungle/Shared/Scenes/RandomMatch.unity: names, the draw, the scoreboard between rounds, the final screen.
    //    Styled like YOUR main menu (button art, fonts, colours and background are copied from it).
    //  - A RANDOM MATCH button at the bottom right of the LevelSelect scene (styled like your PLAY button).
    //    (An older one in the MainMenu is removed.)
    //  - The scene is added to the build list.
    // Safe to run again: an existing RandomMatch scene / button is kept as it is (your edits stay).
    public static partial class RandomMatchBuilder
    {
        const string MainMenuPath = "Assets/PocketJungle/Shared/Scenes/MainMenu.unity";
        const string LevelSelectPath = "Assets/PocketJungle/Shared/Scenes/LevelSelect.unity";
        const string ScenePath = "Assets/PocketJungle/Shared/Scenes/RandomMatch.unity";
        const string GuiSheet = "Assets/ThirdParty/2D Casual UI/Sprite/GUI.png";

        static readonly Color P1Color = new Color(1f, 0.67f, 0.18f);
        static readonly Color P2Color = new Color(0.35f, 0.72f, 1f);

        // Look copied from the main menu.
        static Sprite buttonSprite, backgroundSprite, panelSprite, trophySprite;
        static Image.Type buttonType = Image.Type.Sliced;
        static Color buttonColor = Color.white, labelColor = Color.white, titleColor = Color.white, backgroundColor = Color.white;
        static Font font, titleFont;

        [MenuItem("Tools/Game UI/Build Random Match (scene + main menu button)", priority = 10)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string startScene = SceneManager.GetActiveScene().path;
            string report = "";

            // 1. Main menu: read its look (and remove the button an older version put there).
            var menu = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            ReadStyle();
            report += RemoveMenuButton(menu);

            // 1b. Levels: the RANDOM MATCH button, bottom right.
            var levels = EditorSceneManager.OpenScene(LevelSelectPath, OpenSceneMode.Single);
            report += AddLevelsButton(levels);

            // 2. The Random Match scene.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildScene();
                report += "RandomMatch scene created (Assets/PocketJungle/Shared/Scenes/RandomMatch.unity).\n";
            }
            else report += "RandomMatch scene already exists: kept as it is.\n";

            // 3. Build list.
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                report += "Added to the build scene list.\n";
            }

            if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
            EditorUtility.DisplayDialog("Random Match", report +
                "\nOpen RandomMatch.unity to restyle it: SetupPanel, DrawPanel, BoardPanel and FinalPanel " +
                "(turn each on to edit it; the game shows the right one by itself).", "OK");
        }

        // ---------------------------------------------------------------- main menu

        static void ReadStyle()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleFont = font;
            var sheet = AssetDatabase.LoadAllAssetsAtPath(GuiSheet).OfType<Sprite>().ToArray();
            panelSprite = sheet.FirstOrDefault(s => s.name == "GUI_28");
            trophySprite = sheet.FirstOrDefault(s => s.name == "GUI_62");

            var play = Find("PlayButton");
            if (play != null)
            {
                var img = play.GetComponent<Image>();
                if (img != null) { buttonSprite = img.sprite; buttonType = img.type; buttonColor = img.color; }
                var label = play.GetComponentInChildren<Text>(true);
                if (label != null) { if (label.font != null) font = label.font; labelColor = label.color; }
            }
            var title = Find("GameTitle");
            if (title != null && title.GetComponent<Text>() is Text t) { if (t.font != null) titleFont = t.font; titleColor = t.color; }
            else titleFont = font;
            var bg = Find("Background");
            if (bg != null && bg.GetComponent<Image>() is Image b) { backgroundSprite = b.sprite; backgroundColor = b.color; }
        }

        static string RemoveMenuButton(Scene menu)
        {
            var ui = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            var old = Find("RandomMatchButton");
            if (ui == null || old == null) return "";
            var so = new SerializedObject(ui);
            so.FindProperty("randomMatchButton").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(old);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            return "MainMenu: RANDOM MATCH button removed (it's on the Levels screen now).\n";
        }

        static string AddLevelsButton(Scene levels)
        {
            var ui = Object.FindAnyObjectByType<LevelSelectUI>(FindObjectsInactive.Include);
            if (ui == null) return "LevelSelect: no LevelSelectUI found, button not added.\n";
            var so = new SerializedObject(ui);
            var prop = so.FindProperty("randomMatchButton");
            if (prop.objectReferenceValue != null) return "LevelSelect: RANDOM MATCH button already there.\n";

            var safe = Find("SafeArea");
            Transform parent = safe != null ? safe.transform : ui.transform;
            var button = Btn(parent, "RandomMatchButton", "RANDOM MATCH", Vector2.zero, new Vector2(480, 130), 50);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);    // bottom-right corner
            rt.anchoredPosition = new Vector2(-40f, 30f);
            prop.objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(levels);
            EditorSceneManager.SaveScene(levels);
            return "LevelSelect: RANDOM MATCH button added at the bottom right.\n";
        }

        static string AddMenuButton(Scene menu)
        {
            var ui = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            if (ui == null) return "MainMenu: no MainMenuUI found, button not added.\n";
            var so = new SerializedObject(ui);
            var prop = so.FindProperty("randomMatchButton");
            if (prop.objectReferenceValue != null) return "MainMenu: RANDOM MATCH button already there.\n";

            var play = Find("PlayButton");
            if (play == null) return "MainMenu: no PlayButton to copy, button not added.\n";

            var copy = Object.Instantiate(play, play.transform.parent);
            copy.name = "RandomMatchButton";
            copy.transform.SetSiblingIndex(play.transform.GetSiblingIndex() + 1);
            var label = copy.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "RANDOM MATCH";
                label.fontSize = Mathf.RoundToInt(label.fontSize * 0.75f);
                label.resizeTextForBestFit = true;
                label.resizeTextMaxSize = label.fontSize;
                label.resizeTextMinSize = Mathf.Max(10, label.fontSize / 2);
            }
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();   // no listeners copied from PLAY
            prop.objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            return "MainMenu: RANDOM MATCH button added under PLAY (a copy of your PLAY button).\n";
        }

        static GameObject Find(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                  .Where(t => t.name == name).Select(t => t.gameObject).FirstOrDefault();

        // ---------------------------------------------------------------- scene

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.32f, 0.2f);
            cam.orthographic = true;
            camGO.AddComponent<AudioListener>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("RandomMatchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var ui = canvasGO.AddComponent<RandomMatchUI>();
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

            // ---- Setup
            var setup = Panel("SetupPanel", root);
            Title(setup, "RANDOM MATCH", 380);
            Label(setup, "Subtitle", "2 players  •  3 random games  •  win a game = 1 point", 44, Color.white, new Vector2(0, 270), new Vector2(1500, 80));
            Label(setup, "Player1Label", "PLAYER 1", 54, P1Color, new Vector2(-420, 130), new Vector2(640, 80));
            var in1 = Input(setup, "Player1Name", "Name of player 1", new Vector2(-420, 20));
            Label(setup, "Player2Label", "PLAYER 2", 54, P2Color, new Vector2(420, 130), new Vector2(640, 80));
            var in2 = Input(setup, "Player2Name", "Name of player 2", new Vector2(420, 20));
            var draw = Btn(setup, "DrawButton", "DRAW 3 GAMES!", new Vector2(0, -230), new Vector2(680, 160), 64);
            var back = Btn(setup, "BackButton", "Back", new Vector2(-760, 420), new Vector2(280, 110), 46);

            // ---- Draw
            var drawPanel = Panel("DrawPanel", root);
            Title(drawPanel, "YOUR 3 GAMES", 380);
            var slots = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = UI("RoundCard" + (i + 1), drawPanel);
                Place(card, new Vector2(-560 + 560 * i, 20), new Vector2(500, 430));
                var img = card.gameObject.AddComponent<Image>();
                img.sprite = panelSprite;
                img.type = panelSprite != null && panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                if (panelSprite == null) img.color = new Color(0.2f, 0.45f, 0.2f, 0.95f);
                Label(card, "Round", "ROUND " + (i + 1), 50, labelColor, new Vector2(0, 130), new Vector2(440, 80));
                slots[i] = Label(card, "GameName", "?", 66, Color.white, new Vector2(0, -20), new Vector2(440, 200));
            }
            var start = Btn(drawPanel, "StartButton", "PLAY ROUND 1", new Vector2(0, -380), new Vector2(640, 150), 60);

            // ---- Board
            var board = Panel("BoardPanel", root);
            var boardTitle = Title(board, "ROUND 1: GAME", 400);
            var result = Label(board, "Result", "Player wins the round!", 56, Color.white, new Vector2(0, 270), new Vector2(1600, 140));
            var p1Name = Label(board, "Player1Name", "Player 1", 64, P1Color, new Vector2(-560, 90), new Vector2(560, 100));
            var p1Pts = Label(board, "Player1Points", "0", 170, Color.white, new Vector2(-170, 90), new Vector2(240, 200));
            Label(board, "Dash", "-", 140, Color.white, new Vector2(0, 90), new Vector2(120, 200));
            var p2Pts = Label(board, "Player2Points", "0", 170, Color.white, new Vector2(170, 90), new Vector2(240, 200));
            var p2Name = Label(board, "Player2Name", "Player 2", 64, P2Color, new Vector2(560, 90), new Vector2(560, 100));
            var nextCard = UI("NextCard", board);
            Place(nextCard, new Vector2(0, -130), new Vector2(1400, 220));
            var nextImg = nextCard.gameObject.AddComponent<Image>();
            nextImg.sprite = panelSprite;
            nextImg.type = panelSprite != null && panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (panelSprite == null) nextImg.color = new Color(0.2f, 0.45f, 0.2f, 0.95f);
            var next = Label(nextCard, "Next", "Next: ROUND 2 - Game", 46, Color.white, Vector2.zero, new Vector2(1300, 190));
            var history = Label(board, "History", "Round 1: Game - Player", 34, new Color(1f, 1f, 1f, 0.85f), new Vector2(760, -330), new Vector2(560, 200));
            history.alignment = TextAnchor.UpperRight;
            var play = Btn(board, "PlayButton", "PLAY ROUND 2", new Vector2(0, -390), new Vector2(720, 150), 58);
            var quit = Btn(board, "QuitButton", "Quit match", new Vector2(-760, 420), new Vector2(300, 110), 42);

            // ---- Final
            var final = Panel("FinalPanel", root);
            var trophy = UI("Trophy", final);
            Place(trophy, new Vector2(0, 290), new Vector2(280, 280));
            var trophyImg = trophy.gameObject.AddComponent<Image>();
            trophyImg.sprite = trophySprite;
            trophyImg.preserveAspect = true;
            trophyImg.raycastTarget = false;
            if (trophySprite == null) trophyImg.color = new Color(1f, 0.8f, 0.2f);
            var winner = Title(final, "PLAYER WINS THE MATCH!", 80);
            var finalScore = Label(final, "FinalScore", "Player 1  2 - 1  Player 2", 64, Color.white, new Vector2(0, -50), new Vector2(1600, 100));
            var recap = Label(final, "Recap", "Round 1: Game - Player", 38, new Color(1f, 1f, 1f, 0.9f), new Vector2(0, -200), new Vector2(1400, 180));
            var rematch = Btn(final, "RematchButton", "REMATCH", new Vector2(-330, -390), new Vector2(560, 150), 58);
            var menuBtn = Btn(final, "MenuButton", "Main menu", new Vector2(330, -390), new Vector2(560, 150), 52);

            drawPanel.gameObject.SetActive(false);
            board.gameObject.SetActive(false);
            final.gameObject.SetActive(false);

            var so = new SerializedObject(ui);
            void Set(string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
            Set("setupPanel", setup.gameObject); Set("drawPanel", drawPanel.gameObject);
            Set("boardPanel", board.gameObject); Set("finalPanel", final.gameObject);
            Set("player1Input", in1); Set("player2Input", in2); Set("drawButton", draw); Set("backButton", back);
            var slotsProp = so.FindProperty("slotTexts");
            slotsProp.arraySize = 3;
            for (int i = 0; i < 3; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            Set("startButton", start);
            Set("boardTitle", boardTitle); Set("resultText", result);
            Set("player1NameText", p1Name); Set("player2NameText", p2Name);
            Set("player1PointsText", p1Pts); Set("player2PointsText", p2Pts);
            Set("nextText", next); Set("historyText", history); Set("playButton", play); Set("quitButton", quit);
            Set("winnerText", winner); Set("finalScoreText", finalScore); Set("recapText", recap);
            Set("rematchButton", rematch); Set("menuButton", menuBtn);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---------------------------------------------------------------- helpers

        static RectTransform UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Panel(string name, Transform parent)
        {
            var p = UI(name, parent);
            Stretch(p);
            return p;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Text Label(Transform parent, string name, string text, int size, Color color, Vector2 pos, Vector2 box)
        {
            var rt = UI(name, parent);
            Place(rt, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = size;
            t.resizeTextMinSize = Mathf.Max(12, size / 2);
            t.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        static Text Title(Transform parent, string text, float y)
        {
            var t = Label(parent, "Title", text, 110, titleColor, new Vector2(0, y), new Vector2(1700, 160));
            t.font = titleFont;
            return t;
        }

        static Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, int fontSize)
        {
            var rt = UI(name, parent);
            Place(rt, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = buttonSprite;
            img.type = buttonSprite != null ? buttonType : Image.Type.Simple;
            img.color = buttonSprite != null ? buttonColor : new Color(0.3f, 0.7f, 0.3f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var text = Label(rt, "Label", label, fontSize, labelColor, Vector2.zero, size - new Vector2(40, 20));
            Stretch((RectTransform)text.transform);
            ((RectTransform)text.transform).offsetMin = new Vector2(20, 10);
            ((RectTransform)text.transform).offsetMax = new Vector2(-20, -10);
            return button;
        }

        static InputField Input(Transform parent, string name, string placeholder, Vector2 pos)
        {
            var res = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            };
            var go = DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, pos, new Vector2(640, 130));
            var field = go.GetComponent<InputField>();
            field.characterLimit = 12;
            foreach (var t in go.GetComponentsInChildren<Text>(true))
            {
                t.font = font;
                t.fontSize = 56;
                t.alignment = TextAnchor.MiddleCenter;
            }
            var ph = field.placeholder as Text;
            if (ph != null) ph.text = placeholder;
            if (field.textComponent != null) field.textComponent.color = new Color(0.2f, 0.15f, 0.1f);
            return field;
        }
    }
}
