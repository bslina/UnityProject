using System.Collections;
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
    }

    // The "big game" brain. Lives on the GameUI prefab (Assets/UI/Resources/GameUI.prefab),
    // which spawns itself before the first scene loads and survives every scene change.
    //
    // Every menu is its own scene (Assets/UI/Scenes):
    //   MainMenu, LevelSelect, WinMenu, LoseMenu  -> replace the current scene
    //   PauseMenu, SettingsMenu                   -> opened ON TOP of the current scene (additive),
    //                                                so a paused minigame keeps its state
    public class GameFlow : MonoBehaviour
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

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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

        public void LoadMainMenu() => LoadScene(MainMenuScene);

        public void LoadLevelMenu() => LoadScene(LevelSelectScene);

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

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("GameUI: no EventSystem in this scene, created one.");
        }
    }
}
