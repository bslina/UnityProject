using System.Collections;
using SoundCopy.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Runs the on-device AI judge with an animated scanner, then opens the verdict.
    public sealed class AIAnalysisPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject errorActions;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button redoButton;
        [SerializeField] private RectTransform[] scannerBars;
        [SerializeField] private RectTransform scannerSweep;
        private Coroutine analysisRoutine;
        private float scannerTime;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            retryButton?.onClick.AddListener(BeginAnalysis);
            redoButton?.onClick.AddListener(() => gameManager?.OnAnalysisBack());
        }

        private void Update()
        {
            if (analysisRoutine == null || scannerBars == null) return;
            scannerTime += Time.unscaledDeltaTime;
            for (int i = 0; i < scannerBars.Length; i++)
            {
                if (scannerBars[i] == null) continue;
                float pulse = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(scannerTime * 4.2f + i * 0.73f));
                scannerBars[i].localScale = new Vector3(1f, pulse, 1f);
            }
            if (scannerSweep != null)
            {
                float x = Mathf.PingPong(scannerTime * 0.72f, 1f);
                scannerSweep.anchorMin = new Vector2(x, 0.04f);
                scannerSweep.anchorMax = new Vector2(Mathf.Min(1f, x + 0.012f), 0.96f);
            }
        }

        public void BeginAnalysis()
        {
            if (analysisRoutine != null) StopCoroutine(analysisRoutine);
            statusText.text = "Préparation de l'analyse…";
            if (errorActions != null) errorActions.SetActive(false);
            analysisRoutine = StartCoroutine(RunAnalysis());
        }

        private IEnumerator RunAnalysis()
        {
            if (gameManager == null || gameManager.AIJudge == null)
            {
                ShowError("Le juge IA n'est pas configuré.\nRelance SoundCopy > Build Scenes.");
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

            statusText.text = "Verdict prêt !";
            yield return new WaitForSecondsRealtime(0.6f);
            analysisRoutine = null;
            gameManager.RegisterRoundResult(result);
        }

        private void ShowError(string message)
        {
            statusText.text = "Erreur IA\n" + message;
            if (errorActions != null) errorActions.SetActive(true);
            analysisRoutine = null;
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            MobileUI.TopBar(page, "L'IA ÉCOUTE…", false, SoundCopyTheme.Cream, out _, out _);

            var badge = MobileUI.Row(page, "AIBadge", 260f, 220f, 430f);
            MobileUI.Card(badge.gameObject, SoundCopyTheme.Lime, 110f);
            MobileUI.Text(badge, "Label", "IA", 120f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);

            var card = MobileUI.Row(page, "Scanner", 560f, 520f);
            MobileUI.Card(card.gameObject, new Color(0.12f, 0.07f, 0.15f), 44f);
            UIFactory.AddNativeOutline(card.gameObject, SoundCopyTheme.Cream, 3f);
            var wave = UIFactory.CreateRect("Waveform", card, Vector2.zero, Vector2.one, new Vector2(40f, 50f), new Vector2(-40f, -50f));
            scannerBars = new RectTransform[23];
            for (int i = 0; i < scannerBars.Length; i++)
            {
                float w = 1f / scannerBars.Length;
                var bar = UIFactory.CreateRect("ScanBar" + i, wave.transform, new Vector2(i * w + w * 0.2f, 0.08f), new Vector2(i * w + w * 0.75f, 0.92f), Vector2.zero, Vector2.zero);
                UIFactory.AddRounded(bar, i % 5 == 0 ? SoundCopyTheme.Coral : SoundCopyTheme.Lime, 8f);
                scannerBars[i] = (RectTransform)bar.transform;
            }
            var sweep = UIFactory.CreateRect("ScannerSweep", wave.transform, new Vector2(0f, 0.04f), new Vector2(0.012f, 0.96f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(sweep, SoundCopyTheme.Cream, 6f);
            scannerSweep = (RectTransform)sweep.transform;

            var status = MobileUI.Row(page, "Status", 1130f, 200f);
            statusText = MobileUI.Text(status, "Text", "Préparation de l'analyse…", 48f, SoundCopyTheme.Cream, TextAlignmentOptions.Center);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            var info = MobileUI.Row(page, "Info", 1340f, 130f);
            var infoText = MobileUI.Text(info, "Text", "Les deux imitations sont comparées au son original,\ndirectement sur le téléphone.", 34f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
            infoText.textWrappingMode = TextWrappingModes.Normal;

            var actions = MobileUI.BottomRow(page, "ErrorActions", MobileUI.BottomMargin, MobileUI.ButtonHeight);
            errorActions = actions.gameObject;
            retryButton = MobileUI.BigButton(MobileUI.Cell(actions, "Retry", 0, 2, 30f), "RetryButton", "RÉESSAYER", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 46f);
            redoButton = MobileUI.BigButton(MobileUI.Cell(actions, "Redo", 1, 2, 30f), "RedoButton", "REFAIRE", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 46f);
            errorActions.SetActive(false);
        }
    }
}
