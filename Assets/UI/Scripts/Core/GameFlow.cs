using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GameUI
{
    // What the last minigame reported, read by the Win / Lose scenes.
    public class GameResult
    {
        public bool Won;
        public string Detail;
        public int Stars;
        public int Score;
        public bool NewBest;
        public int GameIndex;

        // 2-player result (every minigame is a duel): who won and both scores.
        public bool IsDuel;
        public int Winner;            // 1, 2, or 0 = draw
        public int Score1, Score2;
        public string Player1, Player2;
        public string ScoreLine;      // e.g. "Lina  3  -  1  Sam" (empty when the detail already says it)
    }

    // The "big game" brain. Lives on the GameUI prefab (Assets/UI/Resources/GameUI.prefab),
    // which spawns itself before the first scene loads and survives every scene change.
    //
    // Every menu is its own scene (Assets/UI/Scenes):
    //   MainMenu, LevelSelect, WinMenu, LoseMenu  -> replace the current scene
    //   PauseMenu, SettingsMenu                   -> opened ON TOP of the current scene (additive),
    //                                                so a paused minigame keeps its state
    public partial class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [Tooltip("Pause button shown over minigames.")]
        [SerializeField] GameOverlayUI overlay;
        [SerializeField] AudioManager audioManager;
        [Tooltip("Black full-screen image used to fade between scenes.")]
        [SerializeField] CanvasGroup fader;
        [SerializeField] float fadeDuration = 0.25f;

        public MiniGameRegistry Registry => MiniGameRegistry.Instance;
        public int CurrentIndex { get; private set; } = -1;
        public MiniGameInfo CurrentGame => Registry != null ? Registry.Get(CurrentIndex) : null;
        public bool InMiniGame => CurrentIndex >= 0;
        public bool IsPaused { get; private set; }
        public bool IsLoading { get; private set; }
        public bool ResultShown { get; private set; }
        public AudioManager Audio => audioManager;

        // The minigame played last (still set while the Win / Lose scene is showing).
        public int LastGameIndex { get; private set; } = -1;
        // Filled when a minigame calls MiniGameBridge.Win / Lose. Null if no game was played yet.
        public GameResult LastResult { get; private set; }

        string MenuName(string name, string fallback) => string.IsNullOrEmpty(name) ? fallback : name;
        string MainMenuScene => MenuName(Registry != null ? Registry.MainMenuScene : null, "MainMenu");
        string LevelSelectScene => MenuName(Registry != null ? Registry.LevelSelectScene : null, "LevelSelect");
        string SettingsScene => MenuName(Registry != null ? Registry.SettingsScene : null, "SettingsMenu");
        string PauseScene => MenuName(Registry != null ? Registry.PauseScene : null, "PauseMenu");
        string WinScene => MenuName(Registry != null ? Registry.WinScene : null, "WinMenu");
        string LoseScene => MenuName(Registry != null ? Registry.LoseScene : null, "LoseMenu");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var prefab = Resources.Load<GameObject>("GameUI");
            if (prefab == null)
            {
                Debug.LogWarning("GameUI prefab not found in Resources. Run Tools > Game UI > Build Menus.");
                return;
            }
            Instantiate(prefab).name = "GameUI";
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateAudioListener();

            Screen.orientation = ScreenOrientation.LandscapeLeft;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (fader != null)
            {
                fader.alpha = 0f;
                fader.blocksRaycasts = false;
            }
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        // With "Enter Play Mode Options > Reload Scene" turned off (this project), Unity doesn't fire
        // sceneLoaded for the scene that is already open when Play is pressed: set it up here instead.
        bool sceneSetUp;

        void Start()
        {
            if (Instance == this && !sceneSetUp) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneSetUp = true;
            StartCoroutine(AddClickSounds());
            EnsureAudioListener();
            if (mode != LoadSceneMode.Single) return; // pause / settings popups don't change the game state

            ResultShown = false;
            SetPaused(false);
            CurrentIndex = Registry != null ? Registry.IndexOfScene(scene.name) : -1;
            if (InMiniGame) LastGameIndex = CurrentIndex;
            EnsureEventSystem();

            ApplyOrientation(CurrentGame);
            if (overlay != null)
            {
                overlay.OnSceneChanged(InMiniGame);
                if (CurrentGame != null && CurrentGame.hidePauseButton) overlay.SetPauseButtonVisible(false);
            }
            if (audioManager != null) audioManager.PlayMusicFor(CurrentGame);
        }

        // Every button in the game clicks: buttons without a ButtonClickSound get one. Scanned again a bit
        // later because some minigames (Photo Challenge) build their buttons after the scene has loaded.
        IEnumerator AddClickSounds()
        {
            foreach (float wait in new[] { 0f, 0.3f, 1.5f })
            {
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                else yield return null;
                foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (button.GetComponent<ButtonClickSound>() == null) button.gameObject.AddComponent<ButtonClickSound>();
            }
        }

        void LateUpdate()
        {
            // The listener sits where the game's camera is (matters for any 3D sound).
            if (listener == null) return;
            Camera cam = Camera.main;
            if (cam != null) listener.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);

            // A minigame may create its own camera + listener later on: keep only ours.
            listenerScanTimer -= Time.unscaledDeltaTime;
            if (listenerScanTimer <= 0f)
            {
                listenerScanTimer = 1f;
                EnsureAudioListener();
            }
        }

        void Update()
        {
            // Android back button arrives as Escape.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                OnBackPressed();
        }

        void OnBackPressed()
        {
            if (IsLoading) return;

            if (IsOpenOnTop(SettingsScene)) { CloseSettings(); return; }
            if (IsPaused) { Resume(); return; }
            if (InMiniGame)
            {
                if (!ResultShown) Pause();
                return;
            }

            // Menu scenes: main menu quits, every other menu goes back to the main menu.
            if (SceneManager.GetActiveScene().name == MainMenuScene) Quit();
            else LoadMainMenu();
        }

        // ---------- Navigation ----------

        public void PlayCampaign()
        {
            if (Registry == null) return;
            int index = Registry.CampaignStart();
            if (index >= 0) PlayGame(index);
            else Debug.LogWarning("No playable minigame in the registry.");
        }

        public void PlayGame(int index)
        {
            var game = Registry != null ? Registry.Get(index) : null;
            if (game == null || !game.IsPlayable)
            {
                PlayCampaign();
                return;
            }
            ResetSoloTurns();   // a fresh game starts with player 1's turn
            LoadScene(game.sceneName);
        }

        // From a minigame (pause menu) or from the Win / Lose scene.
        public void Restart()
        {
            if (InMiniGame) LoadScene(SceneManager.GetActiveScene().name);
            else PlayGame(LastGameIndex);
        }

        public bool HasNextGame => Registry != null && LastGameIndex >= 0 && Registry.NextPlayable(LastGameIndex) >= 0;

        public void NextGame()
        {
            int next = Registry != null && LastGameIndex >= 0 ? Registry.NextPlayable(LastGameIndex) : -1;
            if (next >= 0) PlayGame(next);
            else LoadMainMenu();
        }

        public void LoadMainMenu() { Match = null; LoadScene(MainMenuScene); }

        public void LoadLevelMenu() { Match = null; LoadScene(LevelSelectScene); }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void LoadScene(string sceneName)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            yield return Fade(1f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
                Debug.LogError($"Scene '{sceneName}' can't be loaded. Run Tools > Game UI > Sync Build Scene List.");
            else
                while (!op.isDone) yield return null;

            yield return Fade(0f);
            IsLoading = false;
        }

        IEnumerator Fade(float target)
        {
            if (fader == null) yield break;
            fader.blocksRaycasts = true;
            float start = fader.alpha;
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                fader.alpha = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }
            fader.alpha = target;
            fader.blocksRaycasts = target > 0f;
        }

        // ---------- Popup scenes (loaded on top) ----------

        static bool IsLoaded(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

        // True when the scene is open as a popup over another scene (not played on its own).
        static bool IsOpenOnTop(string sceneName) => IsLoaded(sceneName) && SceneManager.sceneCount > 1;

        static void OpenOnTop(string sceneName)
        {
            if (SceneManager.GetSceneByName(sceneName).IsValid()) return; // already open or opening
            if (SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive) == null)
                Debug.LogError($"Scene '{sceneName}' can't be loaded. Run Tools > Game UI > Sync Build Scene List.");
        }

        static void CloseOnTop(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded && SceneManager.sceneCount > 1) SceneManager.UnloadSceneAsync(scene);
        }

        public void OpenSettings()
        {
            if (IsLoading) return;
            OpenOnTop(SettingsScene);
        }

        public void CloseSettings()
        {
            // Played on its own (opened directly in the Editor): there is nothing under it.
            if (!IsOpenOnTop(SettingsScene)) LoadMainMenu();
            else CloseOnTop(SettingsScene);
        }

        // ---------- Pause ----------

        public void Pause()
        {
            if (!InMiniGame || IsPaused || ResultShown || IsLoading) return;
            SetPaused(true);
            OpenOnTop(PauseScene);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            CloseOnTop(SettingsScene);
            CloseOnTop(PauseScene);
            SetPaused(false);
        }

        void SetPaused(bool paused)
        {
            bool changed = IsPaused != paused;
            IsPaused = paused;
            Time.timeScale = paused || ResultShown ? 0f : 1f;
            AudioListener.pause = paused;
            if (overlay != null) overlay.Refresh();
            if (changed) MiniGameBridge.RaisePauseChanged(paused);
        }

        public void SetPauseButtonVisible(bool visible)
        {
            if (overlay != null) overlay.SetPauseButtonVisible(visible);
        }

        // ---------- Results ----------

        public void ReportResult(bool won, string detail, int stars, int score, float delay)
        {
            if (ResultShown || IsLoading) return;
            if (CurrentGame != null && !CurrentGame.twoPlayer)
            {
                // Solo minigame = one player's turn (2 turns, then the result).
                if (InMatch) ReportSoloTurn(won, detail, score, delay);
                else ReportSoloTurnFree(won, detail, stars, score, delay);
                return;
            }
            ResultShown = true;
            if (overlay != null) overlay.Refresh();

            var game = CurrentGame;
            stars = Mathf.Clamp(stars, 0, 3);
            bool newBest = false;
            if (won && game != null) newBest = SaveData.RecordWin(game.id, stars, score);

            LastResult = new GameResult
            {
                Won = won, Detail = detail, Stars = won ? stars : 0, Score = score,
                NewBest = newBest, GameIndex = CurrentIndex,
            };
            StartCoroutine(ShowResultAfter(delay, won));
        }

        IEnumerator ShowResultAfter(float delay, bool won)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (IsPaused) Resume();
            Time.timeScale = 0f; // freeze the minigame during the fade
            LoadScene(won ? WinScene : LoseScene);
        }

        // ---------- Helpers ----------

        // Menus are landscape; each minigame says how it is held (MiniGameRegistry > Orientation).
        static void ApplyOrientation(MiniGameInfo game)
        {
            Screen.orientation = game != null && game.orientation == GameOrientation.Portrait
                ? ScreenOrientation.Portrait
                : ScreenOrientation.LandscapeLeft;
        }

        // Unity plays no sound without exactly ONE AudioListener. Scenes come and go (and some have
        // none), so GameUI keeps its own permanent listener that follows the camera, and switches the
        // scenes' own listeners off: never zero (no "no audio listeners" warning), never two.
        AudioListener listener;
        float listenerScanTimer;

        void CreateAudioListener()
        {
            var go = new GameObject("AudioListener (GameUI)");
            go.transform.SetParent(transform, false);
            listener = go.AddComponent<AudioListener>();
        }

        void EnsureAudioListener()
        {
            if (listener == null) CreateAudioListener();
            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (other != listener && other.enabled) other.enabled = false;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Debug.Log("GameUI: no EventSystem in this scene, created one.");
            }
            GiveUIModulesOwnActions();
        }

        // Every menu scene has its own EventSystem, and by default they all share ONE input actions
        // asset. When a scene change destroys one module and enables the next while a finger is still
        // on the screen, the Input System is left with stale listeners on that shared asset
        // ("Map index out of range in ProcessControlStateChange" + NullReferenceException, mostly in
        // the Device Simulator). A private copy per module keeps their states apart.
        static readonly List<(InputSystemUIInputModule module, InputActionAsset copy)> uiActionCopies =
            new List<(InputSystemUIInputModule, InputActionAsset)>();

        static void GiveUIModulesOwnActions()
        {
            // Free the copies of modules that went away with their scene.
            for (int i = uiActionCopies.Count - 1; i >= 0; i--)
            {
                if (uiActionCopies[i].module != null) continue;
                if (uiActionCopies[i].copy != null) Destroy(uiActionCopies[i].copy);
                uiActionCopies.RemoveAt(i);
            }

            foreach (var module in FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None))
            {
                if (module.actionsAsset == null || uiActionCopies.Exists(c => c.module == module)) continue;
                var copy = Instantiate(module.actionsAsset);
                copy.name = module.actionsAsset.name + " (" + module.gameObject.scene.name + ")";
                module.actionsAsset = copy;   // the module re-binds its point/click/scroll actions to the copy
                uiActionCopies.Add((module, copy));
            }
        }
    }
}
