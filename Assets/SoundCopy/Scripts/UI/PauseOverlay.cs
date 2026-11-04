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
            pbRt.anchoredPosition = new Vector2(-32f, -34f);
            pbRt.sizeDelta = new Vector2(130f, 130f);
            var pbGraphic = UIFactory.AddRounded(pauseButtonObject, SoundCopyTheme.Cream, 65f, true);
            UIFactory.AddNativeOutline(pauseButtonObject, SoundCopyTheme.Ink, 4f);
            pauseButton = pauseButtonObject.AddComponent<Button>();
            pauseButton.targetGraphic = pbGraphic;
            for (int i = 0; i < 2; i++)
            {
                var bar = UIFactory.CreateRect("Bar" + i, pauseButtonObject.transform, new Vector2(i == 0 ? 0.33f : 0.55f, 0.3f), new Vector2(i == 0 ? 0.45f : 0.67f, 0.7f), Vector2.zero, Vector2.zero);
                UIFactory.AddRounded(bar, SoundCopyTheme.Ink, 6f);
            }

            modal = UIFactory.CreateRect("PauseModal", transform, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
            UIFactory.AddRounded(modal, new Color(0.04f, 0.02f, 0.06f, 0.85f), 0f, true);
            var content = UIFactory.CreateRect("Content", modal.transform, Vector2.zero, Vector2.one, new Vector2(200f, 200f), new Vector2(-200f, -200f));

            var card = MobileUI.Fill(content.transform, "Card", 380f, 300f, 48f);
            MobileUI.Card(card.gameObject, SoundCopyTheme.Cream, 56f);
            var title = MobileUI.Row(card, "Title", 60f, 130f);
            MobileUI.Text(title, "Text", "PAUSE", 120f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            var sub = MobileUI.Row(card, "Subtitle", 200f, 60f);
            MobileUI.Text(sub, "Text", "Le micro est coupé, les scores sont gardés.", 36f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center);
            var score = MobileUI.Row(card, "Score", 290f, 110f);
            MobileUI.Card(score.gameObject, new Color(0.95f, 0.92f, 0.86f), 30f, false);
            scoreText = MobileUI.Text(score, "Text", "J1  0 – 0  J2", 52f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 34f;
            scoreText.fontSizeMax = 52f;

            resumeButton = MobileUI.BigButton(MobileUI.Row(card, "Resume", 450f, 170f), "ResumeButton", "REPRENDRE  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
            restartButton = MobileUI.BigButton(MobileUI.Row(card, "Restart", 660f, 140f), "RestartButton", "RECOMMENCER LE MATCH", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 44f);
            quitButton = MobileUI.BigButton(MobileUI.Row(card, "Quit", 840f, 140f), "QuitButton", "QUITTER LE MATCH", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 44f);
            exitButton = MobileUI.BigButton(MobileUI.Row(card, "Exit", 1020f, 140f), "ExitButton", "MENU DES JEUX", SoundCopyTheme.Ink, SoundCopyTheme.Cream, 44f);
            modal.SetActive(false);
        }
    }
}
