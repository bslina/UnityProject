using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GameUI
{
    // The "big game" brain. Lives on the GameUI prefab (Assets/UI/Resources/GameUI.prefab),
    // which spawns itself before the first scene loads and survives every scene change.
    // So you can press Play in ANY scene (MainMenu, EggRun, a colleague's scene) and the
    // pause button, settings and win/lose screens are there.
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

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

        // Set before going back to the main menu so it opens straight on the level menu.
        public static bool OpenLevelsOnMenuLoad;

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
            if (mode != LoadSceneMode.Single) return;

            ResultShown = false;
            SetPaused(false);
            CurrentIndex = Registry != null ? Registry.IndexOfScene(scene.name) : -1;
            EnsureEventSystem();

            if (overlay != null) overlay.OnSceneChanged(InMiniGame);
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
            if (IsLoading || overlay == null) return;
            if (overlay.CloseTopPopup()) return;

            if (InMiniGame && !ResultShown) Pause();
            else if (!InMiniGame) MainMenuUI.HandleBack();
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
            if (game == null || !game.IsPlayable) return;
            LoadScene(game.sceneName);
        }

        public void Restart() => LoadScene(SceneManager.GetActiveScene().name);

        public bool HasNextGame => Registry != null && InMiniGame && Registry.NextPlayable(CurrentIndex) >= 0;

        public void NextGame()
        {
            int next = Registry != null ? Registry.NextPlayable(CurrentIndex) : -1;
            if (next >= 0) PlayGame(next);
            else LoadMainMenu();
        }

        public void LoadMainMenu()
        {
            OpenLevelsOnMenuLoad = false;
            LoadScene(Registry != null ? Registry.MainMenuScene : "MainMenu");
        }

        public void LoadLevelMenu()
        {
            OpenLevelsOnMenuLoad = true;
            LoadScene(Registry != null ? Registry.MainMenuScene : "MainMenu");
        }

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

            if (overlay != null) overlay.CloseAll();
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"Scene '{sceneName}' can't be loaded. Add it to File > Build Profiles > Scene List.");
            }
            else
            {
                while (!op.isDone) yield return null;
            }

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

        // ---------- Pause ----------

        public void Pause()
        {
            if (!InMiniGame || IsPaused || ResultShown || IsLoading) return;
            SetPaused(true);
            if (overlay != null) overlay.ShowPause();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            SetPaused(false);
            if (overlay != null) overlay.HidePause();
        }

        void SetPaused(bool paused)
        {
            bool changed = IsPaused != paused;
            IsPaused = paused;
            Time.timeScale = paused || ResultShown ? 0f : 1f;
            AudioListener.pause = paused;
            if (changed) MiniGameBridge.RaisePauseChanged(paused);
        }

        public void SetPauseButtonVisible(bool visible)
        {
            if (overlay != null) overlay.SetPauseButtonVisible(visible);
        }

        public void OpenSettings()
        {
            if (overlay != null) overlay.ShowSettings();
        }

        // ---------- Results ----------

        public void ReportResult(bool won, string detail, int stars, int score, float delay)
        {
            if (ResultShown || IsLoading) return;
            ResultShown = true;

            var game = CurrentGame;
            bool newBest = false;
            if (won && game != null) newBest = SaveData.RecordWin(game.id, Mathf.Clamp(stars, 0, 3), score);

            StartCoroutine(ShowResultAfter(delay, won, detail, stars, score, newBest));
        }

        IEnumerator ShowResultAfter(float delay, bool won, string detail, int stars, int score, bool newBest)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (IsPaused) SetPaused(false);
            Time.timeScale = 0f;

            if (audioManager != null) audioManager.PlayResult(won);
            if (overlay != null) overlay.ShowResult(won, detail, stars, score, newBest, HasNextGame);
        }

        // ---------- Helpers ----------

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("GameUI: no EventSystem in this scene, created one.");
        }
    }
}
