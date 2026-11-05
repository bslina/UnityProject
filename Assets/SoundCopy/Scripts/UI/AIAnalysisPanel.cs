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
            statusText.text = "Getting the analysis ready…";
            if (errorActions != null) errorActions.SetActive(false);
            analysisRoutine = StartCoroutine(RunAnalysis());
        }

        private IEnumerator RunAnalysis()
        {
            if (gameManager == null || gameManager.AIJudge == null)
            {
                ShowError("The AI judge isn't set up.\nRun SoundCopy > Build Scenes again.");
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
                ShowError(result != null ? result.Error : "AI result unavailable.");
                yield break;
            }

            statusText.text = "Verdict ready!";
            yield return new WaitForSecondsRealtime(0.6f);
            analysisRoutine = null;
            gameManager.RegisterRoundResult(result);
        }

        private void ShowError(string message)
        {
            statusText.text = "AI error\n" + message;
            if (errorActions != null) errorActions.SetActive(true);
            analysisRoutine = null;
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            MobileUI.TopBar(page, "THE AI IS LISTENING…", false, SoundCopyTheme.Cream, out _, out _);

            // Left: AI badge.
            var badge = MobileUI.TopLeft(page, "AIBadge", 150f, 300f, 340f, 340f);
            MobileUI.Card(badge.gameObject, SoundCopyTheme.Lime, 170f);
            MobileUI.Text(badge, "Label", "AI", 170f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);

            // Right: scanner, status, explanation.
            var side = UIFactory.CreateRect("Side", page, Vector2.zero, Vector2.one, new Vector2(600f, 0f), new Vector2(-MobileUI.Side, 0f));
            var card = MobileUI.Row(side.transform, "Scanner", 180f, 400f, 0f);
            MobileUI.Card(card.gameObject, new Color(0.12f, 0.07f, 0.15f), 44f);
            UIFactory.AddNativeOutline(card.gameObject, SoundCopyTheme.Cream, 3f);
            var wave = UIFactory.CreateRect("Waveform", card, Vector2.zero, Vector2.one, new Vector2(40f, 44f), new Vector2(-40f, -44f));
            scannerBars = new RectTransform[31];
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

            var status = MobileUI.Row(side.transform, "Status", 610f, 120f, 0f);
            statusText = MobileUI.Text(status, "Text", "Getting the analysis ready…", 46f, SoundCopyTheme.Cream, TextAlignmentOptions.TopLeft);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            var info = MobileUI.Row(side.transform, "Info", 740f, 90f, 0f);
            var infoText = MobileUI.Text(info, "Text", "Both imitations are compared with the original sound, right on the phone.", 32f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.TopLeft);
            infoText.textWrappingMode = TextWrappingModes.Normal;

            var actions = MobileUI.BottomRight(page, "ErrorActions", 900f);
            errorActions = actions.gameObject;
            retryButton = MobileUI.BigButton(MobileUI.Cell(actions, "Retry", 0, 2, 30f), "RetryButton", "TRY AGAIN", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 44f);
            redoButton = MobileUI.BigButton(MobileUI.Cell(actions, "Redo", 1, 2, 30f), "RedoButton", "REDO", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 44f);
            errorActions.SetActive(false);
        }
    }
}
