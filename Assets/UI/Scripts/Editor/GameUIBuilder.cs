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
    // Builds the whole menu system from code so the hierarchy is clean and identical for everyone:
    //   Tools > Game UI > Build Menus            creates whatever is missing (safe to run any time)
    //   Tools > Game UI > Rebuild Menus          overwrites the prefabs + MainMenu scene (loses manual edits!)
    //   Tools > Game UI > Sync Build Scene List  puts MainMenu first + every registry scene in Build Profiles
    public static class GameUIBuilder
    {
        const string Root = "Assets/UI";
        const string ResourcesDir = Root + "/Resources";
        const string PrefabsDir = Root + "/Prefabs";
        const string ScenesDir = Root + "/Scenes";
        const string ThemePath = ResourcesDir + "/UITheme.asset";
        const string RegistryPath = ResourcesDir + "/MiniGameRegistry.asset";
        const string GameUIPrefabPath = ResourcesDir + "/GameUI.prefab";
        const string LevelButtonPath = PrefabsDir + "/LevelButton.prefab";
        const string MainMenuScenePath = ScenesDir + "/MainMenu.unity";

        static Font font;
        static Sprite roundedSprite, backgroundSprite, knobSprite, checkSprite;

        // ---------------------------------------------------------------- menu items

        [MenuItem("Tools/Game UI/Build Menus (create missing)", priority = 0)]
        public static void BuildMissing() => Build(false);

        [MenuItem("Tools/Game UI/Rebuild Menus (overwrite)", priority = 1)]
        static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Rebuild menus",
                    "This overwrites GameUI.prefab, LevelButton.prefab and the MainMenu scene.\n" +
                    "Manual changes to them will be lost. The Theme and Registry are kept.", "Rebuild", "Cancel"))
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

            if (File.Exists(MainMenuScenePath)) Add(MainMenuScenePath);

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

        [MenuItem("Tools/Game UI/Select Theme (skins)", priority = 40)]
        static void SelectTheme() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);

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

            CreateThemeIfMissing();
            CreateRegistryIfMissing();

            if (overwrite || !File.Exists(LevelButtonPath)) BuildLevelButtonPrefab();
            if (overwrite || !File.Exists(GameUIPrefabPath)) BuildGameUIPrefab();
            if (overwrite || !File.Exists(MainMenuScenePath)) BuildMainMenuScene();

            AssetDatabase.SaveAssets();
            SyncBuildScenes();
            Debug.Log("Game UI: menus built. Skin them in Assets/UI/Resources/UITheme.asset.");
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

        static void CreateThemeIfMissing()
        {
            if (File.Exists(ThemePath)) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UITheme>(), ThemePath);
        }

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
            AddGame("minigame_2", "Mini Game 2", "Coming soon", "", true);
            AddGame("minigame_3", "Mini Game 3", "Coming soon", "", true);
            AddGame("minigame_4", "Mini Game 4", "Coming soon", "", true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- LevelButton prefab

        static void BuildLevelButtonPrefab()
        {
            var root = UI("LevelButton", null);
            Size(root, 380, 520);
            Img(root, ImageRole.Card, roundedSprite);
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

            var number = Txt(root.transform, "Number", "1", 170, TextRole.Title);
            Place(number.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(300, 240));

            var title = Txt(root.transform, "Name", "Game Name", 46, TextRole.Title);
            Place(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -280), new Vector2(350, 70));

            var description = Txt(root.transform, "Description", "Short description", 28, TextRole.Body);
            Place(description.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -350), new Vector2(340, 80));

            var starsRow = HStack(root.transform, "Stars", 10);
            Place(starsRow, new Vector2(0.5f, 0f), new Vector2(0, 25), new Vector2(300, 80));
            var stars = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var star = UI("Star" + (i + 1), starsRow.transform);
                Layout(star, 70, 70);
                stars[i] = Img(star, ImageRole.StarEmpty, knobSprite, false).GetComponent<ThemedImage>();
            }

            var locked = UI("LockedOverlay", root.transform);
            Stretch(locked);
            Img(locked, ImageRole.Dim, roundedSprite, false);
            var lockText = Txt(locked.transform, "LockedLabel", "Locked", 52, TextRole.Light);
            Place(lockText.gameObject, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 80));
            var lockIcon = UI("LockIcon", locked.transform);
            Place(lockIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            IconImage(lockIcon, ImageRole.LockIcon, lockText.gameObject);

            var soon = UI("ComingSoonOverlay", root.transform);
            Stretch(soon);
            Img(soon, ImageRole.Dim, roundedSprite, false);
            var soonText = Txt(soon.transform, "Label", "Coming soon", 52, TextRole.Light);
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

        // ---------------------------------------------------------------- GameUI prefab (persistent overlay)

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

            // --- Overlay canvas: pause button + popups
            var canvas = MakeCanvas(root.transform, "OverlayCanvas", 100);
            var overlay = canvas.AddComponent<GameOverlayUI>();

            var safe = UI("SafeArea", canvas.transform);
            Stretch(safe);
            safe.AddComponent<SafeArea>();
            var pause = Btn(safe.transform, "PauseButton", "II", ImageRole.IconButton, new Vector2(130, 130), 60);
            Place(pause.gameObject, new Vector2(1f, 1f), new Vector2(-30, -30), new Vector2(130, 130));
            AddIcon(pause, ImageRole.PauseIcon);

            // Pause menu
            var pauseMenu = Popup<PauseMenuUI>(canvas.transform, "PauseMenu", new Vector2(760, 880), "Paused", out var pauseWindow);
            var resume = Btn(pauseWindow, "ResumeButton", "Resume", ImageRole.PrimaryButton, new Vector2(560, 120), 54);
            var restart = Btn(pauseWindow, "RestartButton", "Restart", ImageRole.SecondaryButton, new Vector2(560, 100), 46);
            var pauseSettings = Btn(pauseWindow, "SettingsButton", "Settings", ImageRole.SecondaryButton, new Vector2(560, 100), 46);
            var pauseLevels = Btn(pauseWindow, "LevelsButton", "Levels", ImageRole.SecondaryButton, new Vector2(560, 100), 46);
            var pauseMain = Btn(pauseWindow, "MainMenuButton", "Main Menu", ImageRole.SecondaryButton, new Vector2(560, 100), 46);
            Set(pauseMenu, "resumeButton", resume);
            Set(pauseMenu, "restartButton", restart);
            Set(pauseMenu, "settingsButton", pauseSettings);
            Set(pauseMenu, "levelsButton", pauseLevels);
            Set(pauseMenu, "mainMenuButton", pauseMain);

            // Result (win / lose)
            var result = Popup<ResultUI>(canvas.transform, "ResultPanel", new Vector2(1060, 900), null, out var resultWindow);
            var header = UI("HeaderIcon", resultWindow);
            Layout(header, 170, 170);
            var headerImg = Img(header, ImageRole.WinIcon, knobSprite, false);
            headerImg.preserveAspect = true;
            var resultTitle = Txt(resultWindow, "Title", "You made it!", 96, TextRole.Title);
            Layout(resultTitle.gameObject, 900, 120);
            var detail = Txt(resultWindow, "Detail", "Details", 50, TextRole.Body);
            Layout(detail.gameObject, 900, 70);
            var starsRow = HStack(resultWindow, "Stars", 20);
            Layout(starsRow, 420, 120);
            var stars = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var star = UI("Star" + (i + 1), starsRow.transform);
                Layout(star, 110, 110);
                stars[i] = Img(star, ImageRole.StarFull, knobSprite, false).GetComponent<ThemedImage>();
            }
            var score = Txt(resultWindow, "Score", "Score 0", 42, TextRole.Body);
            Layout(score.gameObject, 900, 56);
            var buttonRow = HStack(resultWindow, "Buttons", 20);
            Layout(buttonRow, 920, 120);
            var resMain = Btn(buttonRow.transform, "MainMenuButton", "Menu", ImageRole.SecondaryButton, new Vector2(200, 110), 40);
            var resLevels = Btn(buttonRow.transform, "LevelsButton", "Levels", ImageRole.SecondaryButton, new Vector2(200, 110), 40);
            var resRetry = Btn(buttonRow.transform, "RetryButton", "Retry", ImageRole.PrimaryButton, new Vector2(220, 110), 44);
            var resNext = Btn(buttonRow.transform, "NextButton", "Next", ImageRole.PrimaryButton, new Vector2(220, 110), 44);
            Set(result, "headerIcon", header.GetComponent<ThemedImage>());
            Set(result, "titleText", resultTitle);
            Set(result, "detailText", detail);
            Set(result, "scoreText", score);
            Set(result, "starsRow", starsRow);
            SetArray(result, "stars", stars);
            Set(result, "nextButton", resNext);
            Set(result, "retryButton", resRetry);
            Set(result, "levelsButton", resLevels);
            Set(result, "mainMenuButton", resMain);

            // Settings (also opened from the main menu)
            var settings = Popup<SettingsUI>(canvas.transform, "SettingsPanel", new Vector2(1120, 990), "Settings", out var settingsWindow);
            var musicSlider = SliderRow(settingsWindow, "Music");
            var sfxSlider = SliderRow(settingsWindow, "Sound effects");
            var vibration = ToggleRow(settingsWindow, "Vibration");
            var invert = ToggleRow(settingsWindow, "Invert tilt");
            var sensitivity = SliderRow(settingsWindow, "Tilt sensitivity");
            var debug = ToggleRow(settingsWindow, "Show debug info");
            var settingsButtons = HStack(settingsWindow, "Buttons", 30);
            Layout(settingsButtons, 960, 110);
            var reset = Btn(settingsButtons.transform, "ResetProgressButton", "Reset progress", ImageRole.SecondaryButton, new Vector2(440, 100), 40);
            var close = Btn(settingsButtons.transform, "CloseButton", "Close", ImageRole.PrimaryButton, new Vector2(320, 100), 48);
            var version = Txt(settingsWindow, "Version", "v1.0", 28, TextRole.Body);
            Layout(version.gameObject, 960, 36);
            Set(settings, "musicSlider", musicSlider);
            Set(settings, "sfxSlider", sfxSlider);
            Set(settings, "vibrationToggle", vibration);
            Set(settings, "invertTiltToggle", invert);
            Set(settings, "tiltSensitivitySlider", sensitivity);
            Set(settings, "debugToggle", debug);
            Set(settings, "resetProgressButton", reset);
            Set(settings, "resetProgressLabel", reset.GetComponentInChildren<Text>());
            Set(settings, "closeButton", close);
            Set(settings, "versionText", version);

            Set(overlay, "pauseButton", pause);
            Set(overlay, "pauseMenu", pauseMenu);
            Set(overlay, "settings", settings);
            Set(overlay, "result", result);
            pauseMenu.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            pause.gameObject.SetActive(false);

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

        // ---------------------------------------------------------------- MainMenu scene

        static void BuildMainMenuScene()
        {
            var cardPrefab = AssetDatabase.LoadAssetAtPath<LevelButton>(LevelButtonPath);
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var camera = cam.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = theme != null ? theme.backgroundColor : Color.gray;
            camera.orthographic = true;
            cam.AddComponent<AudioListener>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvas = MakeCanvas(null, "MenuCanvas", 0);
            var menu = canvas.AddComponent<MainMenuUI>();

            var background = UI("Background", canvas.transform);
            Stretch(background);
            Img(background, ImageRole.MenuBackground, null, false);

            // --- Main panel
            var main = UI("MainPanel", canvas.transform);
            Stretch(main);
            var mainPanel = main.AddComponent<UIPanel>();

            var gameTitle = Txt(main.transform, "GameTitle", "GAME TITLE", 140, TextRole.Title);
            Place(gameTitle.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1400, 260));
            var logo = UI("Logo", main.transform);
            Place(logo, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1000, 320));
            var logoImage = IconImage(logo, ImageRole.Logo, gameTitle.gameObject);
            logoImage.preserveAspect = true;

            var buttons = VStack(main.transform, "Buttons", 28);
            Place(buttons, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(600, 560));
            var play = Btn(buttons.transform, "PlayButton", "PLAY", ImageRole.PrimaryButton, new Vector2(560, 140), 70);
            var levels = Btn(buttons.transform, "LevelsButton", "Levels", ImageRole.SecondaryButton, new Vector2(520, 110), 50);
            var settings = Btn(buttons.transform, "SettingsButton", "Settings", ImageRole.SecondaryButton, new Vector2(520, 110), 50);
            var quit = Btn(buttons.transform, "QuitButton", "Quit", ImageRole.SecondaryButton, new Vector2(520, 110), 50);

            // --- Level select panel
            var levelsGo = UI("LevelSelectPanel", canvas.transform);
            Stretch(levelsGo);
            var levelSelect = levelsGo.AddComponent<LevelSelectUI>();

            var levelsTitle = Txt(levelsGo.transform, "Title", "Choose a game", 90, TextRole.Title);
            Place(levelsTitle.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1200, 130));

            var safe = UI("SafeArea", levelsGo.transform);
            Stretch(safe);
            safe.AddComponent<SafeArea>();
            var back = Btn(safe.transform, "BackButton", "<", ImageRole.IconButton, new Vector2(130, 130), 70);
            Place(back.gameObject, new Vector2(0f, 1f), new Vector2(30, -30), new Vector2(130, 130));
            AddIcon(back, ImageRole.BackIcon);

            var scroll = UI("CardScroll", levelsGo.transform);
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

            Set(levelSelect, "cardPrefab", cardPrefab);
            Set(levelSelect, "cardContainer", content.transform);
            Set(levelSelect, "backButton", back);
            levelsGo.SetActive(false);

            Set(menu, "mainPanel", mainPanel);
            Set(menu, "levelSelect", levelSelect);
            Set(menu, "playButton", play);
            Set(menu, "levelsButton", levels);
            Set(menu, "settingsButton", settings);
            Set(menu, "quitButton", quit);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        // ---------------------------------------------------------------- composite widgets

        static T Popup<T>(Transform parent, string name, Vector2 size, string title, out Transform window) where T : UIPanel
        {
            var root = UI(name, parent);
            Stretch(root);
            var panel = root.AddComponent<T>();

            var dim = UI("Dim", root.transform);
            Stretch(dim);
            Img(dim, ImageRole.Dim, null); // blocks taps on what's behind

            var win = UI("Window", root.transform);
            Place(win, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Img(win, ImageRole.Panel, roundedSprite);
            var layout = win.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 50, 50);
            layout.spacing = 18;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            if (title != null)
            {
                var t = Txt(win.transform, "Title", title, 84, TextRole.Title);
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
            Img(bg, ImageRole.SliderBackground, backgroundSprite);

            var fillArea = UI("Fill Area", go.transform);
            Stretch(fillArea);
            var faRt = (RectTransform)fillArea.transform;
            faRt.anchorMin = new Vector2(0, 0.3f);
            faRt.anchorMax = new Vector2(1, 0.7f);
            faRt.offsetMin = new Vector2(5, 0);
            faRt.offsetMax = new Vector2(-15, 0);
            var fill = UI("Fill", fillArea.transform);
            ((RectTransform)fill.transform).sizeDelta = new Vector2(10, 0);
            Img(fill, ImageRole.SliderFill, roundedSprite, false);

            var handleArea = UI("Handle Slide Area", go.transform);
            Stretch(handleArea);
            var haRt = (RectTransform)handleArea.transform;
            haRt.offsetMin = new Vector2(20, 0);
            haRt.offsetMax = new Vector2(-20, 0);
            var handle = UI("Handle", handleArea.transform);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(56, 0);
            var handleImg = Img(handle, ImageRole.SliderHandle, knobSprite);

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
            var bgImg = Img(bg, ImageRole.ToggleBackground, roundedSprite);

            var check = UI("Checkmark", bg.transform);
            Stretch(check, 8);
            var checkImg = Img(check, ImageRole.ToggleCheckmark, checkSprite, false);

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;
            return toggle;
        }

        static GameObject Row(Transform parent, string name, string label)
        {
            var row = HStack(parent, name, 30);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            Layout(row, 960, 76);
            var text = Txt(row.transform, "Label", label, 46, TextRole.Body, TextAnchor.MiddleLeft);
            Layout(text.gameObject, 430, 76);
            return row;
        }

        // Icon inside a button; the button's text label shows until the theme has an icon sprite.
        static void AddIcon(Button button, ImageRole role)
        {
            var icon = UI("Icon", button.transform);
            Stretch(icon, 22);
            var label = button.transform.Find("Label");
            IconImage(icon, role, label != null ? label.gameObject : null).preserveAspect = true;
        }

        static Image IconImage(GameObject go, ImageRole role, GameObject fallback)
        {
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            var themed = go.AddComponent<ThemedImage>();
            Set(themed, "role", role);
            Set(themed, "hideIfNoSprite", true);
            if (fallback != null) Set(themed, "fallback", fallback);
            themed.Apply();
            return image;
        }

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

        static Image Img(GameObject go, ImageRole role, Sprite defaultSprite, bool raycast = true)
        {
            var image = go.AddComponent<Image>();
            image.sprite = defaultSprite;
            image.type = defaultSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = raycast;
            if (role != ImageRole.None)
            {
                var themed = go.AddComponent<ThemedImage>();
                Set(themed, "role", role);
                themed.Apply();
            }
            return image;
        }

        static Text Txt(Transform parent, string name, string content, int size, TextRole role, TextAnchor align = TextAnchor.MiddleCenter)
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
            var themed = go.AddComponent<ThemedText>();
            Set(themed, "role", role);
            themed.Apply();
            return text;
        }

        static Button Btn(Transform parent, string name, string label, ImageRole role, Vector2 size, int fontSize)
        {
            var go = UI(name, parent);
            Size(go, size.x, size.y);
            Layout(go, size.x, size.y);
            var image = Img(go, role, roundedSprite);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            SetDisabledTint(button);
            go.AddComponent<ButtonClickSound>();

            var text = Txt(go.transform, "Label", label, fontSize, role == ImageRole.IconButton ? TextRole.Icon : TextRole.Button);
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
