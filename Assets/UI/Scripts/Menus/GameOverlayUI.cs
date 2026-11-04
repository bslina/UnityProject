using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // The always-on-top canvas of the GameUI prefab: pause button + pause, settings and result popups.
    public class GameOverlayUI : MonoBehaviour
    {
        [SerializeField] Button pauseButton;
        [SerializeField] PauseMenuUI pauseMenu;
        [SerializeField] SettingsUI settings;
        [SerializeField] ResultUI result;

        bool pauseButtonWanted = true;
        bool inMiniGame;

        void Awake()
        {
            pauseButton.onClick.AddListener(() => GameFlow.Instance.Pause());
            CloseAll();
        }

        public void OnSceneChanged(bool isMiniGame)
        {
            inMiniGame = isMiniGame;
            pauseButtonWanted = true;
            CloseAll();
        }

        public void SetPauseButtonVisible(bool visible)
        {
            pauseButtonWanted = visible;
            RefreshPauseButton();
        }

        void RefreshPauseButton()
        {
            bool resultOpen = result != null && result.IsOpen;
            bool pauseOpen = pauseMenu != null && pauseMenu.IsOpen;
            pauseButton.gameObject.SetActive(inMiniGame && pauseButtonWanted && !resultOpen && !pauseOpen);
        }

        public void ShowPause()
        {
            pauseMenu.Show();
            RefreshPauseButton();
        }

        public void HidePause()
        {
            if (settings.IsOpen) settings.Hide();
            pauseMenu.HideImmediate();
            RefreshPauseButton();
        }

        public void ShowSettings() => settings.Show();

        public void ShowResult(bool won, string detail, int stars, int score, bool newBest, bool hasNext)
        {
            pauseMenu.HideImmediate();
            settings.HideImmediate();
            result.Show(won, detail, stars, score, newBest, hasNext);
            RefreshPauseButton();
        }

        // Android back: closes settings first, then the pause menu. Returns true if it closed something.
        public bool CloseTopPopup()
        {
            if (settings.IsOpen) { settings.Hide(); return true; }
            if (pauseMenu.IsOpen) { GameFlow.Instance.Resume(); return true; }
            return result.IsOpen; // the result screen stays until a button is pressed
        }

        public void CloseAll()
        {
            pauseMenu.HideImmediate();
            settings.HideImmediate();
            result.HideImmediate();
            RefreshPauseButton();
        }
    }
}
