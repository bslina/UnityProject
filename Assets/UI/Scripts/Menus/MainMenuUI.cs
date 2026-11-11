using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // MainMenu scene: title screen. Play opens the level menu, where the player picks a minigame.
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] Button playButton;
        [Tooltip("Opens the Random Match (2 players, 3 random games).")]
        [SerializeField] Button randomMatchButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        void Awake()
        {
            // PLAY: "Who's playing?" (the 2 names), then the level menu.
            if (playButton != null) playButton.onClick.AddListener(() => GameFlow.Instance.OpenPlayers());
            if (randomMatchButton != null) randomMatchButton.onClick.AddListener(() => GameFlow.Instance.OpenRandomMatch());
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
