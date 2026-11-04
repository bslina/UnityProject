using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class PauseOverlay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject pauseButtonObject;
        [SerializeField] private GameObject modal;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TextMeshProUGUI scoreText;
        private bool paused;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            pauseButton?.onClick.AddListener(Open);
            resumeButton?.onClick.AddListener(Close);
            restartButton?.onClick.AddListener(Restart);
            quitButton?.onClick.AddListener(Quit);
        }

        private void OnDestroy()
        {
            pauseButton?.onClick.RemoveListener(Open);
            resumeButton?.onClick.RemoveListener(Close);
            restartButton?.onClick.RemoveListener(Restart);
            quitButton?.onClick.RemoveListener(Quit);
            Time.timeScale = 1f;
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            pauseButtonObject = UIFactory.CreateRect("PauseButton", transform, new Vector2(0.82f, 0.91f), new Vector2(0.93f, 0.965f), Vector2.zero, Vector2.zero);
            pauseButton = UIFactory.CreateButton("Button", pauseButtonObject.transform, "II", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 28f, out _, false);

            modal = UIFactory.CreateRect("PauseModal", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(modal, new Color(0.07f, 0.03f, 0.09f, 0.78f), 0f);
            GameObject card = UIFactory.CreateRect("Card", modal.transform, new Vector2(0.045f, 0.13f), new Vector2(0.955f, 0.64f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(card, SoundCopyTheme.Cream, 52f);
            UIFactory.AddNativeOutline(card, SoundCopyTheme.Ink, 5f);
            Shadow cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = SoundCopyTheme.Ink;
            cardShadow.effectDistance = new Vector2(0f, -12f);

            GameObject icon = UIFactory.CreateRect("Icon", card.transform, new Vector2(0.06f, 0.82f), new Vector2(0.17f, 0.95f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(icon, SoundCopyTheme.Lime, 24f);
            UIFactory.AddNativeOutline(icon, SoundCopyTheme.Ink, 2f);
            UIFactory.CreateText("Label", icon.transform, "II", 27f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI badge = UIFactory.CreateText("Badge", card.transform, "MATCH EN PAUSE", 16f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);
            badge.characterSpacing = 4f;
            badge.rectTransform.anchorMin = new Vector2(0.60f, 0.84f);
            badge.rectTransform.anchorMax = new Vector2(0.94f, 0.94f);
            badge.rectTransform.offsetMin = Vector2.zero;
            badge.rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI title = UIFactory.CreateText("Title", card.transform, "PAUSE <color=#FF665F>STRATÉGIQUE.</color>", 52f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.62f);
            title.rectTransform.anchorMax = new Vector2(0.96f, 0.80f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI sub = UIFactory.CreateText("Subtitle", card.transform, "Le micro est coupé.\nLes scores restent au frais.", 22f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Normal);
            sub.rectTransform.anchorMin = new Vector2(0.06f, 0.50f);
            sub.rectTransform.anchorMax = new Vector2(0.92f, 0.61f);
            sub.rectTransform.offsetMin = Vector2.zero;
            sub.rectTransform.offsetMax = Vector2.zero;

            GameObject score = UIFactory.CreateRect("Score", card.transform, new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.49f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(score, new Color(0.97f, 0.95f, 0.91f), 24f);
            UIFactory.AddNativeOutline(score, new Color(0.6f, 0.57f, 0.55f, 0.4f), 2f);
            GameObject j1 = UIFactory.CreateRect("J1", score.transform, new Vector2(0.015f, 0.12f), new Vector2(0.13f, 0.88f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(j1, SoundCopyTheme.Lime, 50f);
            UIFactory.AddNativeOutline(j1, SoundCopyTheme.Ink, 2f);
            UIFactory.CreateText("Label", j1.transform, "J1", 21f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            GameObject j2 = UIFactory.CreateRect("J2", score.transform, new Vector2(0.87f, 0.12f), new Vector2(0.985f, 0.88f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(j2, SoundCopyTheme.Coral, 50f);
            UIFactory.AddNativeOutline(j2, SoundCopyTheme.Ink, 2f);
            UIFactory.CreateText("Label", j2.transform, "J2", 21f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            scoreText = UIFactory.CreateText("Text", score.transform, "Joueur 1   0     —     0   Joueur 2", 22f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            scoreText.rectTransform.anchorMin = new Vector2(0.13f, 0f);
            scoreText.rectTransform.anchorMax = new Vector2(0.87f, 1f);
            scoreText.rectTransform.offsetMin = Vector2.zero;
            scoreText.rectTransform.offsetMax = Vector2.zero;

            GameObject resume = UIFactory.CreateRect("Resume", card.transform, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.34f), Vector2.zero, Vector2.zero);
            resumeButton = UIFactory.CreateButton("Button", resume.transform, ">  REPRENDRE LE MATCH", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 27f, out _);
            GameObject restart = UIFactory.CreateRect("Restart", card.transform, new Vector2(0.06f, 0.07f), new Vector2(0.48f, 0.18f), Vector2.zero, Vector2.zero);
            restartButton = UIFactory.CreateButton("Button", restart.transform, "RECOMMENCER", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 21f, out _, false);
            GameObject quit = UIFactory.CreateRect("Quit", card.transform, new Vector2(0.52f, 0.07f), new Vector2(0.94f, 0.18f), Vector2.zero, Vector2.zero);
            quitButton = UIFactory.CreateButton("Button", quit.transform, "QUITTER", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 21f, out _, false);
            modal.SetActive(false);
        }

        public void SetAvailable(bool available)
        {
            if (pauseButtonObject != null) pauseButtonObject.SetActive(available);
            if (!available && paused) Close();
        }

        private void Open()
        {
            if (modal == null || gameManager == null) return;
            paused = true;
            scoreText.text = gameManager.Player1Name + "   " + gameManager.Player1Score + "     —     " + gameManager.Player2Score + "   " + gameManager.Player2Name;
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
    }
}
