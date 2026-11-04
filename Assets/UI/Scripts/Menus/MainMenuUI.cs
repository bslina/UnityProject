using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // MainMenu scene: title screen. Play opens the level menu, where the player picks a minigame.
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] Button playButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(() => GameFlow.Instance.LoadLevelMenu());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => GameFlow.Instance.OpenSettings());
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(() => GameFlow.Instance.Quit());
#if UNITY_IOS
                quitButton.gameObject.SetActive(false); // iOS apps don't quit themselves
#endif
            }
        }
    }
}
