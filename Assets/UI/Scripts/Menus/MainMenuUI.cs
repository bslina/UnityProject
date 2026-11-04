using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // Main menu scene: title screen + level menu.
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] UIPanel mainPanel;
        [SerializeField] LevelSelectUI levelSelect;

        [Header("Buttons")]
        [SerializeField] Button playButton;
        [SerializeField] Button levelsButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        static MainMenuUI current;

        void Awake()
        {
            current = this;
            playButton.onClick.AddListener(() => GameFlow.Instance.PlayCampaign());
            levelsButton.onClick.AddListener(ShowLevels);
            settingsButton.onClick.AddListener(() => GameFlow.Instance.OpenSettings());
            quitButton.onClick.AddListener(() => GameFlow.Instance.Quit());
            levelSelect.BackPressed += ShowMain;

#if UNITY_IOS
            quitButton.gameObject.SetActive(false); // iOS apps don't quit themselves
#endif
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        void Start()
        {
            if (GameFlow.OpenLevelsOnMenuLoad)
            {
                GameFlow.OpenLevelsOnMenuLoad = false;
                mainPanel.HideImmediate();
                levelSelect.Show();
            }
            else
            {
                levelSelect.HideImmediate();
                mainPanel.Show();
            }
        }

        public void ShowLevels()
        {
            mainPanel.Hide();
            levelSelect.Show();
        }

        public void ShowMain()
        {
            levelSelect.Hide();
            mainPanel.Show();
        }

        // Android back on the menu: level menu -> main, main -> quit.
        public static void HandleBack()
        {
            if (current == null) return;
            if (current.levelSelect.IsOpen) current.ShowMain();
            else GameFlow.Instance.Quit();
        }
    }
}
