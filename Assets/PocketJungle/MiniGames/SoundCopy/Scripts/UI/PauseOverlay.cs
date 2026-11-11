using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Pause button (top-right) and pause window: resume, restart, quit the match, back to the big game.
    public sealed class PauseOverlay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject pauseButtonObject;
        [SerializeField] private GameObject modal;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private TextMeshProUGUI scoreText;
        private bool paused;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            pauseButton?.onClick.AddListener(Open);
            resumeButton?.onClick.AddListener(Close);
            restartButton?.onClick.AddListener(Restart);
            quitButton?.onClick.AddListener(Quit);
            exitButton?.onClick.AddListener(Exit);
        }

        private void OnDestroy() => Time.timeScale = 1f;

        public void SetAvailable(bool available)
        {
            if (pauseButtonObject != null) pauseButtonObject.SetActive(available);
            if (!available && paused) Close();
        }

        private void Open()
        {
            if (modal == null || gameManager == null) return;
            paused = true;
            scoreText.text = $"{gameManager.Player1Name}  {gameManager.Player1Score} – {gameManager.Player2Score}  {gameManager.Player2Name}";
            if (exitButton != null) exitButton.gameObject.SetActive(gameManager.InsideBigGame);
            modal.SetActive(true);
            modal.transform.SetAsLastSibling();
            pauseButtonObject.SetActive(false);
            Time.timeScale = 0f;
            gameManager.OnPauseOpened();
        }

        private void Close()
        {
            if (!paused) return;
            paused = false;
            Time.timeScale = 1f;
            modal.SetActive(false);
            pauseButtonObject.SetActive(true);
            gameManager.OnPauseClosed();
        }

        private void Restart()
        {
            paused = false;
            Time.timeScale = 1f;
            modal.SetActive(false);
            gameManager.RestartCurrentMatch();
        }

        private void Quit()
        {
            paused = false;
            Time.timeScale = 1f;
            modal.SetActive(false);
            gameManager.QuitCurrentMatch();
        }

        private void Exit()
        {
            paused = false;
            Time.timeScale = 1f;
            modal.SetActive(false);
            gameManager.ExitToGame();
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);

            // Pause button, in the top bar's right corner (screens leave that spot free).
            pauseButtonObject = UIFactory.CreateRect("PauseButton", transform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var pbRt = (RectTransform)pauseButtonObject.transform;
            pbRt.pivot = new Vector2(1f, 1f);
            pbRt.anchoredPosition = new Vector2(-32f, -24f);
            pbRt.sizeDelta = new Vector2(116f, 116f);
            pauseButton = MobileUI.RoundButton(pauseButtonObject.transform, "Button", SoundCopySkin.IconPause, "II");

            modal = UIFactory.CreateRect("PauseModal", transform, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
            UIFactory.AddRounded(modal, new Color(0.04f, 0.02f, 0.06f, 0.85f), 0f, true);

            // Centred landscape window: resume on top, three choices in a row below.
            var card = UIFactory.CreateRect("Card", modal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var cardRt = (RectTransform)card.transform;
            cardRt.sizeDelta = new Vector2(1500f, 820f);
            card.AddComponent<GameUI.FitToScreen>();
            MobileUI.Panel(card, true);

            var title = MobileUI.Row(card.transform, "Title", 40f, 130f);
            MobileUI.Text(title, "Text", "PAUSE", 120f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            var sub = MobileUI.Row(card.transform, "Subtitle", 175f, 56f);
            MobileUI.Text(sub, "Text", "The mic is off, scores are kept.", 36f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center);
            var score = MobileUI.Row(card.transform, "Score", 250f, 110f, 200f);
            MobileUI.Card(score.gameObject, new Color(0.95f, 0.92f, 0.86f), 30f, false);
            scoreText = MobileUI.Text(score, "Text", "P1  0 – 0  P2", 54f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 34f;
            scoreText.fontSizeMax = 54f;

            resumeButton = MobileUI.BigButton(MobileUI.Row(card.transform, "Resume", 400f, 160f, 200f), "ResumeButton", "RESUME  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
            var choices = MobileUI.Row(card.transform, "Choices", 610f, 140f);
            restartButton = MobileUI.BigButton(MobileUI.Cell(choices, "Restart", 0, 3, 30f), "RestartButton", "RESTART", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 42f);
            quitButton = MobileUI.BigButton(MobileUI.Cell(choices, "Quit", 1, 3, 30f), "QuitButton", "QUIT MATCH", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 42f);
            exitButton = MobileUI.BigButton(MobileUI.Cell(choices, "Exit", 2, 3, 30f), "ExitButton", "GAMES MENU", SoundCopyTheme.Ink, SoundCopyTheme.Cream, 42f);
            modal.SetActive(false);
        }
    }
}
