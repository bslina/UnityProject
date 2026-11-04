using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // Pause popup shown over a minigame.
    public class PauseMenuUI : UIPanel
    {
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button levelsButton;
        [SerializeField] Button mainMenuButton;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(() => GameFlow.Instance.Resume());
            restartButton.onClick.AddListener(() => GameFlow.Instance.Restart());
            settingsButton.onClick.AddListener(() => GameFlow.Instance.OpenSettings());
            levelsButton.onClick.AddListener(() => GameFlow.Instance.LoadLevelMenu());
            mainMenuButton.onClick.AddListener(() => GameFlow.Instance.LoadMainMenu());
        }
    }
}
