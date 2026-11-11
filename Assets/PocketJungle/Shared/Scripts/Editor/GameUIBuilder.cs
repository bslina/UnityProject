using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools
{
    // Builds the whole menu system from code so the hierarchy is clean and identical for everyone.
    // One scene per menu in Assets/PocketJungle/Shared/Scenes: MainMenu, LevelSelect, SettingsMenu, PauseMenu, WinMenu, LoseMenu.
    //   Tools > Game UI > Build Menus            creates whatever is missing (safe to run any time)
    //   Tools > Game UI > Rebuild Menus          overwrites the prefabs + menu scenes (loses manual edits!)
    //   Tools > Game UI > Sync Build Scene List  puts the menu scenes + every registry scene in Build Profiles
    public static class GameUIBuilder
    {
        const string Root = "Assets/PocketJungle/Shared";
        const string ResourcesDir = Root + "/Resources";
        const string PrefabsDir = Root + "/Prefabs";
        const string ScenesDir = Root + "/Scenes";
        const string RegistryPath = ResourcesDir + "/MiniGameRegistry.asset";
        const string GameUIPrefabPath = ResourcesDir + "/GameUI.prefab";
        const string LevelButtonPath = PrefabsDir + "/LevelButton.prefab";

        // Scene names (must match the names in MiniGameRegistry.asset).
        const string MainMenuScene = "MainMenu";
        const string LevelSelectScene = "LevelSelect";
        const string SettingsScene = "SettingsMenu";
        const string PauseScene = "PauseMenu";
        const string WinScene = "WinMenu";
        const string LoseScene = "LoseMenu";
        static readonly string[] MenuScenes = { MainMenuScene, LevelSelectScene, SettingsScene, PauseScene, WinScene, LoseScene };

        static string ScenePath(string sceneName) => ScenesDir + "/" + sceneName + ".unity";

        static Font font;
        static Sprite roundedSprite, backgroundSprite, knobSprite, checkSprite;

        // ---------------------------------------------------------------- menu items

        [MenuItem("Tools/Game UI/Build Menus (create missing)", priority = 0)]
        public static void BuildMissing() => Build(false);

        [MenuItem("Tools/Game UI/Rebuild Menus (overwrite)", priority = 1)]
        static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Rebuild menus",
                    "This overwrites GameUI.prefab, LevelButton.prefab and the 6 menu scenes in Assets/PocketJungle/Shared/Scenes.\n" +
                    "Manual changes to them will be lost. The Registry is kept.", "Rebuild", "Cancel"))
                return;
            Build(true);
        }

        [MenuItem("Tools/Game UI/Sync Build Scene List", priority = 20)]
        public static void SyncBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            void Add(string path)
            {
                if (string.IsNullOrEmpty(path) || scenes.Exists(s => s.path == path)) return;
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (var menu in MenuScenes)
                if (File.Exists(ScenePath(menu))) Add(ScenePath(menu)); // MainMenu first = the game's start scene

            var registry = AssetDatabase.LoadAssetAtPath<MiniGameRegistry>(RegistryPath);
            if (registry != null)
            {
                foreach (var game in registry.Games)
                {
                    if (string.IsNullOrEmpty(game.sceneName)) continue;
                    string path = FindScene(game.sceneName);
                    if (path != null) Add(path);
                    else Debug.LogWarning($"Game UI: scene '{game.sceneName}' for '{game.displayName}' was not found in the project.");
                }
            }

            // Keep any other scene that was already in the list, after ours.
            foreach (var existing in EditorBuildSettings.scenes) Add(existing.path);

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Game UI: Build scene list = " + string.Join(", ", scenes.ConvertAll(s => Path.GetFileNameWithoutExtension(s.path))));
        }

        [MenuItem("Tools/Game UI/Select MiniGame Registry (level list)", priority = 41)]
        static void SelectRegistry() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<MiniGameRegistry>(RegistryPath);

        [MenuItem("Tools/Game UI/Clear Saved Progress and Settings", priority = 60)]
        static void ClearSaves()
        {
            if (EditorUtility.DisplayDialog("Clear saves", "Delete all PlayerPrefs (progress + settings) for this project?", "Delete", "Cancel"))
                PlayerPrefs.DeleteAll();
        }

        // ---------------------------------------------------------------- build

        public static void Build(bool overwrite)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            LoadDefaults();
            EnsureFolder(ResourcesDir);
            EnsureFolder(PrefabsDir);
            EnsureFolder(ScenesDir);

            CreateRegistryIfMissing();

            // Files made by the first version (every menu inside MainMenu + GameUI.prefab) are replaced.
            bool oldLayout = FileContains(GameUIPrefabPath, "PauseMenu") || FileContains(ScenePath(MainMenuScene), "LevelSelectPanel");
            if (oldLayout) Debug.Log("Game UI: replacing the old single-scene menus with one scene per menu.");

            if (overwrite || !File.Exists(LevelButtonPath)) BuildLevelButtonPrefab();
            if (overwrite || oldLayout || !File.Exists(GameUIPrefabPath)) BuildGameUIPrefab();

            void BuildScene(string sceneName, Action build)
            {
                if (overwrite || !File.Exists(ScenePath(sceneName)) || (oldLayout && sceneName == MainMenuScene)) build();
            }
            BuildScene(LevelSelectScene, BuildLevelSelectScene);
            BuildScene(SettingsScene, BuildSettingsScene);
            BuildScene(PauseScene, BuildPauseScene);
            BuildScene(WinScene, () => BuildResultScene(true));
            BuildScene(LoseScene, () => BuildResultScene(false));
            BuildScene(MainMenuScene, BuildMainMenuScene); // last, so it is the scene left open

            AssetDatabase.SaveAssets();
            SyncBuildScenes();
            Debug.Log("Game UI: menus built. Style each element directly in the scenes and prefabs.");
        }

        static void LoadDefaults()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            backgroundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            checkSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static bool FileContains(string path, string text) => File.Exists(path) && File.ReadAllText(path).Contains(text);

        static string FindScene(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName) return path;
            }
            return null;
        }

        // ---------------------------------------------------------------- data assets

        static void CreateRegistryIfMissing()
        {
            if (File.Exists(RegistryPath)) return;
            var registry = ScriptableObject.CreateInstance<MiniGameRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);

            var so = new SerializedObject(registry);
            var games = so.FindProperty("games");
            void AddGame(string id, string name, string description, string scene, bool comingSoon)
            {
                games.arraySize++;
                var g = games.GetArrayElementAtIndex(games.arraySize - 1);
                g.FindPropertyRelative("id").stringValue = id;
                g.FindPropertyRelative("displayName").stringValue = name;
                g.FindPropertyRelative("description").stringValue = description;
                g.FindPropertyRelative("sceneName").stringValue = scene;
                g.FindPropertyRelative("comingSoon").boolValue = comingSoon;
            }
            AddGame("egg_run", "Egg Run", "Balance the egg on the spoon and reach the flag!", "EggRun", false);
            AddGame("sound_copy", "Sound Copy", "One sound, two imitations: the AI picks the winner!", "Main", false);
            AddGame("photo_challenge", "Photo Challenge", "Find the object and take its picture!", "MainScene", false);
            AddGame("minigame_4", "Mini Game 4", "Coming soon", "", true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- LevelButton prefab

        static void BuildLevelButtonPrefab()
        {
            var root = UI("LevelButton", null);
            Size(root, 380, 520);
            Img(root, Look.Card, roundedSprite);
            var button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            SetDisabledTint(button);
            root.AddComponent<ButtonClickSound>();
            Layout(root, 380, 520);
            var card = root.AddComponent<LevelButton>();

            var icon = UI("Icon", root.transform);
            Place(icon, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(300, 240));
            var iconImage = icon.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var number = Txt(root.transform, "Number", "1", 170, TextLook.Title);
            Place(number.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(300, 240));

            var title = Txt(root.transform, "Name", "Game Name", 46, TextLook.Title);
            Place(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -280), new Vector2(350, 70));

            var description = Txt(root.transform, "Description", "Short description", 28, TextLook.Body);
            Place(description.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -350), new Vector2(340, 80));

            var starsRow = HStack(root.transform, "Stars", 10);
            Place(starsRow, new Vector2(0.5f, 0f), new Vector2(0, 25), new Vector2(300, 80));
            var stars = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var star = UI("Star" + (i + 1), starsRow.transform);
                Layout(star, 70, 70);
                stars[i] = Img(star, Look.StarEmpty, knobSprite, false);
            }

            var locked = UI("LockedOverlay", root.transform);
            Stretch(locked);
            Img(locked, Look.Dim, roundedSprite, false);
            var lockText = Txt(locked.transform, "LockedLabel", "Locked", 52, TextLook.Light);
            Place(lockText.gameObject, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 80));
            var lockIcon = UI("LockIcon", locked.transform);
            Place(lockIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            IconImage(lockIcon, Look.LockIcon, lockText.gameObject);

            var soon = UI("ComingSoonOverlay", root.transform);
            Stretch(soon);
            Img(soon, Look.Dim, roundedSprite, false);
            var soonText = Txt(soon.transform, "Label", "Coming soon", 52, TextLook.Light);
            Place(soonText.gameObject, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 80));
            soon.SetActive(false);
            locked.SetActive(false);

            Set(card, "button", button);
            Set(card, "numberText", number);
            Set(card, "nameText", title);
            Set(card, "descriptionText", description);
            Set(card, "icon", iconImage);
            SetArray(card, "stars", stars);
            Set(card, "starsRow", starsRow);
            Set(card, "lockedOverlay", locked);
            Set(card, "comingSoonOverlay", soon);
            icon.SetActive(false);

            SavePrefab(root, LevelButtonPath);
        }

        // ---------------------------------------------------------------- GameUI prefab (persistent: pause button + fader)

        static void BuildGameUIPrefab()
        {
            var root = new GameObject("GameUI");
            var flow = root.AddComponent<GameFlow>();
            var audio = root.AddComponent<AudioManager>();

            var music = new GameObject("MusicSource").AddComponent<AudioSource>();
            music.transform.SetParent(root.transform, false);
            music.loop = true;
            music.playOnAwake = false;
            var sfx = new GameObject("SfxSource").AddComponent<AudioSource>();
            sfx.transform.SetParent(root.transform, false);
            sfx.playOnAwake = false;
            Set(audio, "musicSource", music);
            Set(audio, "sfxSource", sfx);

            // --- Overlay canvas: the pause button shown over minigames
            var canvas = MakeCanvas(root.transform, "OverlayCanvas", 100);
            var overlay = canvas.AddComponent<GameOverlayUI>();
            var safe = UI("SafeArea", canvas.transform);
            Stretch(safe);
            safe.AddComponent<SafeArea>();
            var pause = Btn(safe.transform, "PauseButton", "II", Look.IconButton, new Vector2(130, 130), 60);
            Place(pause.gameObject, new Vector2(1f, 1f), new Vector2(-30, -30), new Vector2(130, 130));
            AddIcon(pause, Look.PauseIcon);
            pause.gameObject.SetActive(false);
            Set(overlay, "pauseButton", pause);

            // --- Fader canvas: black screen between scenes, above everything
            var faderCanvas = MakeCanvas(root.transform, "FaderCanvas", 1000);
            var fader = UI("Fader", faderCanvas.transform);
            Stretch(fader);
            var faderImage = fader.AddComponent<Image>();
            faderImage.color = Color.black;
            var faderGroup = fader.AddComponent<CanvasGroup>();
            faderGroup.alpha = 0f;
            faderGroup.blocksRaycasts = false;

            Set(flow, "overlay", overlay);
            Set(flow, "audioManager", audio);
            Set(flow, "fader", faderGroup);

            SavePrefab(root, GameUIPrefabPath);
        }

        // ---------------------------------------------------------------- scenes

        // Full-screen menu scene: camera + EventSystem + canvas with the themed background.
        static Scene NewMenuScene(string canvasName, out GameObject canvas)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var camera = cam.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorOf(Look.MenuBackground);
            camera.orthographic = true;
            cam.AddComponent<AudioListener>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            canvas = MakeCanvas(null, canvasName, 0);
            var background = UI("Background", canvas.transform);
            Stretch(background);
            Img(background, Look.MenuBackground, null, false);
            return scene;
        }

        // Popup scene opened ON TOP of another scene (pause, settings): just a canvas, no camera
        // or EventSystem (the scene underneath has them).
        static Scene NewPopupScene(string canvasName, int sortingOrder, out GameObject canvas)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            canvas = MakeCanvas(null, canvasName, sortingOrder);
            return scene;
        }

        // Layout groups only help to place things while building: they are removed before saving,
        // so every element can be moved freely by hand afterwards.
        static void SaveScene(Scene scene, string sceneName)
        {
            foreach (var root in scene.GetRootGameObjects()) UILayoutUnlocker.Unlock(root);
            EditorSceneManager.SaveScene(scene, ScenesDir + "/" + sceneName + ".unity");
        }

        static void BuildMainMenuScene()
        {
            var scene = NewMenuScene("MainMenuCanvas", out var canvas);
            var menu = canvas.AddComponent<MainMenuUI>();

            var safe = UI("SafeArea", canvas.transform);
            Stretch(safe);
            safe.AddComponent<SafeArea>();

            var gameTitle = Txt(safe.transform, "GameTitle", "GAME TITLE", 140, TextLook.Title);
            Place(gameTitle.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1400, 260));
            var logo = UI("Logo", safe.transform);
            Place(logo, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1000, 320));
            IconImage(logo, Look.Logo, gameTitle.gameObject).preserveAspect = true;

            var buttons = VStack(safe.transform, "Buttons", 28);
            Place(buttons, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(600, 560));
            var play = Btn(buttons.transform, "PlayButton", "PLAY", Look.PrimaryButton, new Vector2(560, 140), 70);
            var settings = Btn(buttons.transform, "SettingsButton", "Settings", Look.SecondaryButton, new Vector2(520, 110), 50);
            var quit = Btn(buttons.transform, "QuitButton", "Quit", Look.SecondaryButton, new Vector2(520, 110), 50);

            Set(menu, "playButton", play);
            Set(menu, "settingsButton", settings);
            Set(menu, "quitButton", quit);

            SaveScene(scene, MainMenuScene);
        }

        static void BuildLevelSelectScene()
        {
            var cardPrefab = AssetDatabase.LoadAssetAtPath<LevelButton>(LevelButtonPath);
            var scene = NewMenuScene("LevelSelectCanvas", out var canvas);
            var levelSelect = canvas.AddComponent<LevelSelectUI>();

            var levelsTitle = Txt(canvas.transform, "Title", "Choose a game", 90, TextLook.Title);
            Place(levelsTitle.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1200, 130));

            var scroll = UI("CardScroll", canvas.transform);
            Stretch(scroll);
            var scrollRt = (RectTransform)scroll.transform;
            scrollRt.offsetMin = new Vector2(60, 80);
            scrollRt.offsetMax = new Vector2(-60, -220);
            var scrollHit = scroll.AddComponent<Image>();
            scrollHit.color = Color.clear; // invisible, but lets you drag the list
            scroll.AddComponent<RectMask2D>();
            var scrollRect = scroll.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            var content = UI("Cards", scroll.transform);
            Place(content, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 560));
            var row = content.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 50;
            row.padding = new RectOffset(20, 20, 20, 20);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = scrollRt;
            scrollRect.content = (RectTransform)content.transform;

            // Corner buttons last so they draw above the cards.
            var safe = UI("SafeArea", canvas.transform);
            Stretch(safe);
            safe.AddComponent<SafeArea>();
            var back = Btn(safe.transform, "BackButton", "<", Look.IconButton, new Vector2(130, 130), 70);
            Place(back.gameObject, new Vector2(0f, 1f), new Vector2(30, -30), new Vector2(130, 130));
            AddIcon(back, Look.BackIcon);
            var settings = Btn(safe.transform, "SettingsButton", "Set", Look.IconButton, new Vector2(130, 130), 40);
            Place(settings.gameObject, new Vector2(1f, 1f), new Vector2(-30, -30), new Vector2(130, 130));
            AddIcon(settings, Look.SettingsIcon);

            Set(levelSelect, "cardPrefab", cardPrefab);
            Set(levelSelect, "cardContainer", content.transform);
            Set(levelSelect, "backButton", back);
            Set(levelSelect, "settingsButton", settings);

            SaveScene(scene, LevelSelectScene);
        }

        static void BuildSettingsScene()
        {
            var scene = NewPopupScene("SettingsCanvas", 300, out var canvas);
            var settings = Popup<SettingsUI>(canvas.transform, "SettingsPanel", new Vector2(1120, 1010), "Settings", true, out var window);

            var musicSlider = SliderRow(window, "Music");
            var sfxSlider = SliderRow(window, "Sound effects");
            var vibration = ToggleRow(window, "Vibration");
            var invert = ToggleRow(window, "Invert tilt");
            var sensitivity = SliderRow(window, "Tilt sensitivity");
            var sunEvents = ToggleRow(window, "Sun events (Egg Run)");
            var debug = ToggleRow(window, "Show debug info");
            var buttons = HStack(window, "Buttons", 30);
            Layout(buttons, 960, 110);
            var reset = Btn(buttons.transform, "ResetProgressButton", "Reset progress", Look.SecondaryButton, new Vector2(440, 100), 40);
            var close = Btn(buttons.transform, "CloseButton", "Close", Look.PrimaryButton, new Vector2(320, 100), 48);
            var version = Txt(window, "Version", "v1.0", 28, TextLook.Body);
            Layout(version.gameObject, 960, 36);

            Set(settings, "musicSlider", musicSlider);
            Set(settings, "sfxSlider", sfxSlider);
            Set(settings, "vibrationToggle", vibration);
            Set(settings, "invertTiltToggle", invert);
            Set(settings, "tiltSensitivitySlider", sensitivity);
            Set(settings, "sunEventsToggle", sunEvents);
            Set(settings, "debugToggle", debug);
            Set(settings, "resetProgressButton", reset);
            Set(settings, "resetProgressLabel", reset.GetComponentInChildren<Text>());
            Set(settings, "closeButton", close);
            Set(settings, "versionText", version);

            SaveScene(scene, SettingsScene);
        }

        static void BuildPauseScene()
        {
            var scene = NewPopupScene("PauseCanvas", 200, out var canvas);
            var pauseMenu = Popup<PauseMenuUI>(canvas.transform, "PausePanel", new Vector2(760, 880), "Paused", true, out var window);

            var resume = Btn(window, "ResumeButton", "Resume", Look.PrimaryButton, new Vector2(560, 120), 54);
            var restart = Btn(window, "RestartButton", "Restart", Look.SecondaryButton, new Vector2(560, 100), 46);
            var settings = Btn(window, "SettingsButton", "Settings", Look.SecondaryButton, new Vector2(560, 100), 46);
            var levels = Btn(window, "LevelsButton", "Levels", Look.SecondaryButton, new Vector2(560, 100), 46);
            var main = Btn(window, "MainMenuButton", "Main Menu", Look.SecondaryButton, new Vector2(560, 100), 46);

            Set(pauseMenu, "resumeButton", resume);
            Set(pauseMenu, "restartButton", restart);
            Set(pauseMenu, "settingsButton", settings);
            Set(pauseMenu, "levelsButton", levels);
            Set(pauseMenu, "mainMenuButton", main);

            SaveScene(scene, PauseScene);
        }

        static void BuildResultScene(bool won)
        {
            var scene = NewMenuScene(won ? "WinCanvas" : "LoseCanvas", out var canvas);
            var result = Popup<ResultUI>(canvas.transform, won ? "WinPanel" : "LosePanel",
                new Vector2(1060, won ? 940 : 760), null, false, out var window);
            Set(result, "isWinScreen", won);

            var header = UI("HeaderIcon", window);
            Layout(header, 170, 170);
            Img(header, won ? Look.WinIcon : Look.LoseIcon, knobSprite, false).preserveAspect = true;

            var gameName = Txt(window, "GameName", "Game Name", 40, TextLook.Body);
            Layout(gameName.gameObject, 900, 50);
            var title = Txt(window, "Title", won ? "You made it!" : "Oops!", 96, TextLook.Title);
            Layout(title.gameObject, 900, 120);
            var detail = Txt(window, "Detail", won ? "Well done" : "Try again", 50, TextLook.Body);
            Layout(detail.gameObject, 900, 70);

            Button next = null;
            if (won)
            {
                var starsRow = HStack(window, "Stars", 20);
                Layout(starsRow, 420, 120);
                var stars = new Object[3];
                for (int i = 0; i < 3; i++)
                {
                    var star = UI("Star" + (i + 1), starsRow.transform);
                    Layout(star, 110, 110);
                    stars[i] = Img(star, Look.StarFull, knobSprite, false);
                }
                SetArray(result, "stars", stars);

                var score = Txt(window, "Score", "Score 0", 42, TextLook.Body);
                Layout(score.gameObject, 900, 56);
                Set(result, "scoreText", score);
            }

            var buttonRow = HStack(window, "Buttons", 20);
            Layout(buttonRow, 920, 120);
            var main = Btn(buttonRow.transform, "MainMenuButton", "Menu", Look.SecondaryButton, new Vector2(200, 110), 40);
            var levels = Btn(buttonRow.transform, "LevelsButton", "Levels", Look.SecondaryButton, new Vector2(200, 110), 40);
            var retry = Btn(buttonRow.transform, "RetryButton", "Retry", won ? Look.SecondaryButton : Look.PrimaryButton, new Vector2(220, 110), 44);
            if (won) next = Btn(buttonRow.transform, "NextButton", "Next", Look.PrimaryButton, new Vector2(220, 110), 44);

            Set(result, "gameNameText", gameName);
            Set(result, "detailText", detail);
            Set(result, "retryButton", retry);
            Set(result, "levelsButton", levels);
            Set(result, "mainMenuButton", main);
            if (next != null) Set(result, "nextButton", next);

            SaveScene(scene, won ? WinScene : LoseScene);
        }

        // ---------------------------------------------------------------- composite widgets

        static T Popup<T>(Transform parent, string name, Vector2 size, string title, bool dim, out Transform window) where T : UIPanel
        {
            var root = UI(name, parent);
            Stretch(root);
            var panel = root.AddComponent<T>();

            if (dim)
            {
                var dimGo = UI("Dim", root.transform);
                Stretch(dimGo);
                Img(dimGo, Look.Dim, null); // darkens and blocks taps on the scene underneath
            }

            var win = UI("Window", root.transform);
            Place(win, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Img(win, Look.Panel, roundedSprite);
            win.AddComponent<FitToScreen>(); // shrinks the window on short/wide screens
            var layout = win.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 50, 50);
            layout.spacing = 18;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            if (title != null)
            {
                var t = Txt(win.transform, "Title", title, 84, TextLook.Title);
                Layout(t.gameObject, size.x - 120, 100);
            }

            window = win.transform;
            return panel;
        }

        static Slider SliderRow(Transform parent, string label)
        {
            var row = Row(parent, label.Replace(" ", "") + "Row", label);

            var go = UI("Slider", row.transform);
            Layout(go, 480, 56);
            var slider = go.AddComponent<Slider>();

            var bg = UI("Background", go.transform);
            Stretch(bg);
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = new Vector2(0, 0.3f);
            bgRt.anchorMax = new Vector2(1, 0.7f);
            Img(bg, Look.SliderBackground, backgroundSprite);

            var fillArea = UI("Fill Area", go.transform);
            Stretch(fillArea);
            var faRt = (RectTransform)fillArea.transform;
            faRt.anchorMin = new Vector2(0, 0.3f);
            faRt.anchorMax = new Vector2(1, 0.7f);
            faRt.offsetMin = new Vector2(5, 0);
            faRt.offsetMax = new Vector2(-15, 0);
            var fill = UI("Fill", fillArea.transform);
            ((RectTransform)fill.transform).sizeDelta = new Vector2(10, 0);
            Img(fill, Look.SliderFill, roundedSprite, false);

            var handleArea = UI("Handle Slide Area", go.transform);
            Stretch(handleArea);
            var haRt = (RectTransform)handleArea.transform;
            haRt.offsetMin = new Vector2(20, 0);
            haRt.offsetMax = new Vector2(-20, 0);
            var handle = UI("Handle", handleArea.transform);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(56, 0);
            var handleImg = Img(handle, Look.SliderHandle, knobSprite);

            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.value = 0.8f;
            return slider;
        }

        static Toggle ToggleRow(Transform parent, string label)
        {
            var row = Row(parent, label.Replace(" ", "") + "Row", label);

            var go = UI("Toggle", row.transform);
            Layout(go, 80, 80);
            var toggle = go.AddComponent<Toggle>();

            var bg = UI("Background", go.transform);
            Stretch(bg);
            var bgImg = Img(bg, Look.ToggleBackground, roundedSprite);

            var check = UI("Checkmark", bg.transform);
            Stretch(check, 8);
            var checkImg = Img(check, Look.ToggleCheckmark, checkSprite, false);

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;
            return toggle;
        }

        static GameObject Row(Transform parent, string name, string label)
        {
            var row = HStack(parent, name, 30);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            Layout(row, 960, 68);
            var text = Txt(row.transform, "Label", label, 44, TextLook.Body, TextAnchor.MiddleLeft);
            Layout(text.gameObject, 430, 68);
            return row;
        }

        // Empty icon Image inside a button, switched off: give it a sprite, tick its Image on and
        // hide the button's text label if you want an icon instead of text.
        static void AddIcon(Button button, Look role)
        {
            var icon = UI("Icon", button.transform);
            Stretch(icon, 22);
            IconImage(icon, role, null).preserveAspect = true;
        }

        // Image left empty and disabled (logo, lock icon...): turn it on once you give it a sprite.
        static Image IconImage(GameObject go, Look role, GameObject fallback)
        {
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = ColorOf(role);
            image.enabled = false;
            return image;
        }

        // ---------------------------------------------------------------- default look

        enum Look
        {
            None,
            MenuBackground, Logo, Panel, Card, Dim,
            PrimaryButton, SecondaryButton, IconButton,
            PauseIcon, BackIcon, SettingsIcon, LockIcon,
            StarFull, StarEmpty, WinIcon, LoseIcon,
            SliderBackground, SliderFill, SliderHandle,
            ToggleBackground, ToggleCheckmark,
        }

        enum TextLook { Body, Title, Button, Light, Icon }

        static readonly Color Ink = new Color(0.25f, 0.2f, 0.15f);
        static readonly Color Orange = new Color(1f, 0.67f, 0.18f);

        static Color ColorOf(Look look)
        {
            switch (look)
            {
                case Look.MenuBackground: return new Color(0.36f, 0.68f, 0.93f);
                case Look.Panel: return new Color(1f, 0.97f, 0.88f);
                case Look.Dim: return new Color(0f, 0f, 0f, 0.6f);
                case Look.PrimaryButton: return Orange;
                case Look.SecondaryButton: return new Color(0.42f, 0.75f, 0.42f);
                case Look.IconButton: return new Color(1f, 1f, 1f, 0.9f);
                case Look.PauseIcon:
                case Look.BackIcon:
                case Look.SettingsIcon:
                case Look.LockIcon: return Ink;
                case Look.StarFull: return new Color(1f, 0.82f, 0.1f);
                case Look.StarEmpty: return new Color(0.75f, 0.72f, 0.66f);
                case Look.WinIcon: return new Color(0.36f, 0.75f, 0.36f);
                case Look.LoseIcon: return new Color(0.9f, 0.35f, 0.3f);
                case Look.SliderBackground:
                case Look.ToggleBackground: return new Color(0.82f, 0.76f, 0.66f);
                case Look.SliderFill:
                case Look.ToggleCheckmark: return Orange;
                default: return Color.white;
            }
        }

        static Color TextColorOf(TextLook look) =>
            look == TextLook.Button || look == TextLook.Light ? Color.white : Ink;

        // ---------------------------------------------------------------- primitives

        static GameObject UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject MakeCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = UI(name, parent);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static Image Img(GameObject go, Look role, Sprite defaultSprite, bool raycast = true)
        {
            var image = go.AddComponent<Image>();
            image.sprite = defaultSprite;
            image.type = defaultSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = raycast;
            image.color = ColorOf(role);
            return image;
        }

        static Text Txt(Transform parent, string name, string content, int size, TextLook role, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = UI(name, parent);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = TextColorOf(role);
            return text;
        }

        static Button Btn(Transform parent, string name, string label, Look role, Vector2 size, int fontSize)
        {
            var go = UI(name, parent);
            Size(go, size.x, size.y);
            Layout(go, size.x, size.y);
            var image = Img(go, role, roundedSprite);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            SetDisabledTint(button);
            go.AddComponent<ButtonClickSound>();

            var text = Txt(go.transform, "Label", label, fontSize, role == Look.IconButton ? TextLook.Icon : TextLook.Button);
            Stretch(text.gameObject, 8);
            return button;
        }

        static void SetDisabledTint(Selectable s)
        {
            var colors = s.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.8f);
            s.colors = colors;
        }

        static GameObject VStack(Transform parent, string name, float spacing)
        {
            var go = UI(name, parent);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            return go;
        }

        static GameObject HStack(Transform parent, string name, float spacing)
        {
            var go = UI(name, parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return go;
        }

        static void Layout(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
        }

        static void Size(GameObject go, float width, float height) => ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);

        static void Stretch(GameObject go, float inset = 0f)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // Anchors and pivots at the same point, so "pos" is an offset from that corner/edge.
        static void Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void SavePrefab(GameObject root, string path)
        {
            UILayoutUnlocker.Unlock(root, keepRootLayoutElement: true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"Game UI builder: {target.GetType().Name}.{field} not found");
                return;
            }
            switch (value)
            {
                case Object o: prop.objectReferenceValue = o; break;
                case Enum e: prop.intValue = Convert.ToInt32(e); break;
                case bool b: prop.boolValue = b; break;
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case string s: prop.stringValue = s; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
