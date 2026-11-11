using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI
{
    // PauseMenu scene: opened on top of the paused minigame.
    public class PauseMenuUI : UIPanel
    {
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button levelsButton;
        [SerializeField] Button mainMenuButton;

        [Header("Volume (same as the Settings menu)")]
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;

        protected override void Awake()
        {
            base.Awake();
            if (resumeButton != null) resumeButton.onClick.AddListener(() => GameFlow.Instance.Resume());
            if (restartButton != null) restartButton.onClick.AddListener(() => GameFlow.Instance.Restart());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => GameFlow.Instance.OpenSettings());
            if (levelsButton != null) levelsButton.onClick.AddListener(() => GameFlow.Instance.LoadLevelMenu());
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => GameFlow.Instance.LoadMainMenu());
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => GameSettings.MusicVolume = v);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => GameSettings.SfxVolume = v);
        }

        void Start()
        {
            // The game was resumed while this scene was still loading: close again.
            var flow = GameFlow.Instance;
            if (flow != null && !flow.IsPaused && SceneManager.sceneCount > 1)
            {
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }
            // Saved volumes, without firing the change callbacks.
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            Show();
        }
    }
}
