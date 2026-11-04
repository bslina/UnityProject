using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // The always-on-top canvas of the GameUI prefab: the pause button shown over every minigame.
    // The pause / settings / win / lose screens themselves are scenes in Assets/UI/Scenes.
    public class GameOverlayUI : MonoBehaviour
    {
        [SerializeField] Button pauseButton;

        bool pauseButtonWanted = true;
        bool inMiniGame;

        void Awake()
        {
            pauseButton.onClick.AddListener(() => GameFlow.Instance.Pause());
            Refresh();
        }

        public void OnSceneChanged(bool isMiniGame)
        {
            inMiniGame = isMiniGame;
            pauseButtonWanted = true;
            Refresh();
        }

        public void SetPauseButtonVisible(bool visible)
        {
            pauseButtonWanted = visible;
            Refresh();
        }

        public void Refresh()
        {
            if (pauseButton == null) return;
            var flow = GameFlow.Instance;
            bool busy = flow != null && (flow.IsPaused || flow.ResultShown);
            pauseButton.gameObject.SetActive(inMiniGame && pauseButtonWanted && !busy);
        }
    }
}
