using System.Collections;
using SoundCopy.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class AIAnalysisPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button backButton;
        [SerializeField] private Button continueButton;
        private Coroutine analysisRoutine;
        private bool resultRegistered;
        private RectTransform[] scannerBars;
        private RectTransform scannerSweep;
        private float scannerTime;

        public void Initialize(GameManager manager) => gameManager = manager;
        private void Awake()
        {
            backButton?.onClick.AddListener(Back);
            continueButton?.onClick.AddListener(Continue);
        }
        private void OnDestroy()
        {
            backButton?.onClick.RemoveListener(Back);
            continueButton?.onClick.RemoveListener(Continue);
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            GameObject shell = ScreenScaffold.Build(transform, "ANALYSE IA", "L’IA vous écoute", "YAMNet compare le son original aux deux imitations.");
            GameObject card = UIFactory.CreateRect("AnalysisCard", shell.transform, new Vector2(0.075f, 0.38f), new Vector2(0.925f, 0.67f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(card, SoundCopyTheme.DarkPlum, 44f);
            UIFactory.AddNativeOutline(card, SoundCopyTheme.Ink, 3f);

            GameObject badge = UIFactory.CreateRect("AIBadge", card.transform, new Vector2(0.40f, 0.68f), new Vector2(0.60f, 0.91f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(badge, SoundCopyTheme.Lime, 50f);
            UIFactory.AddNativeOutline(badge, SoundCopyTheme.Ink, 3f);
            UIFactory.CreateText("AILabel", badge.transform, "AI", 35f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject waveform = UIFactory.CreateRect("ScannerWaveform", card.transform, new Vector2(0.07f, 0.16f), new Vector2(0.93f, 0.64f), Vector2.zero, Vector2.zero);
            scannerBars = new RectTransform[23];
            for (int i = 0; i < scannerBars.Length; i++)
            {
                float width = 1f / scannerBars.Length;
                float x = i * width + width * 0.20f;
                GameObject bar = UIFactory.CreateRect("ScanBar" + i, waveform.transform, new Vector2(x, 0.16f), new Vector2(x + width * 0.55f, 0.84f), Vector2.zero, Vector2.zero);
                UIFactory.AddRounded(bar, i % 5 == 0 ? SoundCopyTheme.Coral : SoundCopyTheme.Lime, 8f);
                scannerBars[i] = bar.GetComponent<RectTransform>();
            }
            GameObject sweep = UIFactory.CreateRect("ScannerSweep", waveform.transform, new Vector2(0f, 0.05f), new Vector2(0.012f, 0.95f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(sweep, SoundCopyTheme.Cream, 6f);
            scannerSweep = sweep.GetComponent<RectTransform>();

            statusText = UIFactory.CreateText("Status", shell.transform, "Préparation de l’analyse…", 28f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            statusText.rectTransform.anchorMin = new Vector2(0.075f, 0.29f);
            statusText.rectTransform.anchorMax = new Vector2(0.925f, 0.37f);
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;

            scoreText = UIFactory.CreateText("Scores", shell.transform, string.Empty, 34f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            scoreText.rectTransform.anchorMin = new Vector2(0.075f, 0.20f);
            scoreText.rectTransform.anchorMax = new Vector2(0.925f, 0.29f);
            scoreText.rectTransform.offsetMin = Vector2.zero;
            scoreText.rectTransform.offsetMax = Vector2.zero;

            GameObject back = UIFactory.CreateRect("BackAction", shell.transform, new Vector2(0.075f, 0.105f), new Vector2(0.39f, 0.165f), Vector2.zero, Vector2.zero);
            backButton = UIFactory.CreateButton("BackButton", back.transform, "< Retour", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 25f, out _, false);
            GameObject next = UIFactory.CreateRect("ContinueAction", shell.transform, new Vector2(0.42f, 0.105f), new Vector2(0.925f, 0.165f), Vector2.zero, Vector2.zero);
            continueButton = UIFactory.CreateButton("ContinueButton", next.transform, "Continuer", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 25f, out _);
            continueButton.interactable = false;
        }

        private void Update()
        {
            if (analysisRoutine == null || scannerBars == null) return;
            scannerTime += Time.unscaledDeltaTime;
            for (int i = 0; i < scannerBars.Length; i++)
            {
                float pulse = 0.30f + 0.70f * Mathf.Abs(Mathf.Sin(scannerTime * 4.2f + i * 0.73f));
                scannerBars[i].localScale = new Vector3(1f, pulse, 1f);
            }
            if (scannerSweep != null)
            {
                float x = Mathf.PingPong(scannerTime * 0.72f, 1f);
                scannerSweep.anchorMin = new Vector2(x, 0.05f);
                scannerSweep.anchorMax = new Vector2(Mathf.Min(1f, x + 0.012f), 0.95f);
                scannerSweep.offsetMin = Vector2.zero;
                scannerSweep.offsetMax = Vector2.zero;
            }
        }

        public void BeginAnalysis()
        {
            if (analysisRoutine != null) StopCoroutine(analysisRoutine);
            resultRegistered = false;
            scoreText.text = string.Empty;
            statusText.text = "Préparation de l’analyse…";
            continueButton.interactable = false;
            backButton.interactable = false;
            analysisRoutine = StartCoroutine(RunAnalysis());
        }

        private IEnumerator RunAnalysis()
        {
            if (gameManager == null || gameManager.AIJudge == null)
            {
                ShowError("Le juge IA n’est pas configuré. Relance SoundCopy > Build Scenes.");
                yield break;
            }
            AIJudgeResult result = null;
            yield return gameManager.AIJudge.Judge(
                gameManager.CurrentReferenceId,
                gameManager.Player1Recording,
                gameManager.Player2Recording,
                message => statusText.text = message,
                value => result = value);

            if (result == null || !result.Success)
            {
                ShowError(result != null ? result.Error : "Résultat IA indisponible.");
                yield break;
            }

            if (!resultRegistered)
            {
                gameManager.RegisterAIResult(result.Winner);
                resultRegistered = true;
            }
            string firstLabel = result.Player1TooQuiet ? "audio trop faible" : result.Player1Score.ToString("0") + "%";
            string secondLabel = result.Player2TooQuiet ? "audio trop faible" : result.Player2Score.ToString("0") + "%";
            scoreText.text = gameManager.Player1Name + "  " + firstLabel + "\n" + gameManager.Player2Name + "  " + secondLabel;
            if (result.Player1TooQuiet && result.Player2TooQuiet) statusText.text = "AUCUNE IMITATION DÉTECTÉE";
            else if (result.Player1TooQuiet) statusText.text = gameManager.Player2Name + " gagne — " + gameManager.Player1Name + " est trop silencieux";
            else if (result.Player2TooQuiet) statusText.text = gameManager.Player1Name + " gagne — " + gameManager.Player2Name + " est trop silencieux";
            else statusText.text = result.Winner == 0 ? "ÉGALITÉ !" : result.Winner == 1 ? gameManager.Player1Name + " gagne la manche !" : gameManager.Player2Name + " gagne la manche !";
            continueButton.GetComponentInChildren<TextMeshProUGUI>().text = gameManager.CurrentRound < gameManager.RoundCount ? "Manche suivante" : "Terminer";
            continueButton.interactable = true;
            backButton.interactable = false;
            analysisRoutine = null;
        }

        private void ShowError(string message)
        {
            statusText.text = "Erreur IA\n" + message;
            backButton.interactable = true;
            continueButton.interactable = false;
            analysisRoutine = null;
        }

        private void Continue() => gameManager?.OnAnalysisContinue();
        private void Back() => gameManager?.OnAnalysisBack();
    }
}
