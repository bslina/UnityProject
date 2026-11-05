using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // End of the match: winner, final score and a round-by-round table of AI scores.
    // "Continuer" hands over to the big game's Win screen; "Revanche" replays with the same settings.
    public sealed class ResultsPanel : MonoBehaviour
    {
        const int MaxRounds = 5;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private Image[] trophyParts = new Image[4];
        [SerializeField] private TextMeshProUGUI kickerText;
        [SerializeField] private TextMeshProUGUI winnerText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI[] headerTexts = new TextMeshProUGUI[2];
        [SerializeField] private GameObject[] rows = new GameObject[MaxRounds];
        [SerializeField] private TextMeshProUGUI[] rowTexts = new TextMeshProUGUI[MaxRounds * 3];
        [SerializeField] private TextMeshProUGUI[] averageTexts = new TextMeshProUGUI[2];
        [SerializeField] private Button continueButton;
        [SerializeField] private Button rematchButton;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            continueButton?.onClick.AddListener(() => gameManager?.OnResultsContinue());
            rematchButton?.onClick.AddListener(() => gameManager?.OnRematch());
        }

        public void Show()
        {
            if (gameManager == null) return;
            int winner = gameManager.MatchWinner;
            Color color = winner == 0 ? SoundCopyTheme.Cream : MobileUI.PlayerColor(winner);
            foreach (var part in trophyParts) if (part != null) part.color = color;

            if (kickerText != null) kickerText.text = winner == 0 ? "DRAW" : "WINNER";
            if (winnerText != null)
            {
                winnerText.text = winner == 0 ? "PERFECT DRAW" : gameManager.PlayerName(winner).ToUpperInvariant();
                winnerText.color = color;
            }
            if (scoreText != null)
                scoreText.text = $"{gameManager.Player1Name}  {gameManager.Player1Score} – {gameManager.Player2Score}  {gameManager.Player2Name}";
            if (headerTexts[0] != null) headerTexts[0].text = gameManager.Player1Name;
            if (headerTexts[1] != null) headerTexts[1].text = gameManager.Player2Name;

            var history = gameManager.History;
            for (int i = 0; i < MaxRounds; i++)
            {
                bool used = i < history.Count;
                if (rows[i] != null) rows[i].SetActive(used);
                if (!used) continue;
                var r = history[i];
                SetRow(i, 0, "R" + r.roundNumber + (r.doubled ? " x2" : ""), SoundCopyTheme.Cream);
                SetRow(i, 1, Cell(r.player1Percent, r.player1TooQuiet), r.winner == 1 ? SoundCopyTheme.Lime : new Color(1f, 1f, 1f, 0.55f));
                SetRow(i, 2, Cell(r.player2Percent, r.player2TooQuiet), r.winner == 2 ? SoundCopyTheme.Coral : new Color(1f, 1f, 1f, 0.55f));
            }
            if (averageTexts[0] != null) averageTexts[0].text = Mathf.RoundToInt(gameManager.AverageAccuracy(1)) + "%";
            if (averageTexts[1] != null) averageTexts[1].text = Mathf.RoundToInt(gameManager.AverageAccuracy(2)) + "%";

            Haptics.Pulse(300, 255);
        }

        static string Cell(float percent, bool quiet) => quiet ? "—" : Mathf.RoundToInt(percent) + "%";

        void SetRow(int row, int column, string value, Color color)
        {
            var text = rowTexts[row * 3 + column];
            if (text == null) return;
            text.text = value;
            text.color = color;
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            MobileUI.TopBar(page, "MATCH OVER", false, SoundCopyTheme.Cream, out _, out _);

            // Left column: trophy, winner, final score.
            var trophy = MobileUI.ColRow(page, "Trophy", 0f, 0.42f, 160f, 250f);
            var trophyBox = UIFactory.CreateRect("Icon", trophy, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            ((RectTransform)trophyBox.transform).sizeDelta = new Vector2(250f, 0f);
            if (SoundCopySkin.IconTrophy != null)
            {
                trophyParts[0] = UIFactory.AddSprite(trophyBox, SoundCopySkin.IconTrophy, SoundCopyTheme.Lime);
                trophyParts[0].preserveAspect = true;
            }
            else
            {
                trophyParts[0] = UIFactory.AddCircle(trophyBox, SoundCopyTheme.Lime);
                MobileUI.Text(trophyBox.transform, "Number", "1", 120f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            }
            trophyParts[1] = trophyParts[2] = trophyParts[3] = null;

            var kicker = MobileUI.ColRow(page, "Kicker", 0f, 0.42f, 430f, 56f);
            kickerText = MobileUI.Text(kicker, "Text", "WINNER", 40f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
            var winner = MobileUI.ColRow(page, "Winner", 0f, 0.42f, 485f, 150f);
            winnerText = MobileUI.Text(winner, "Text", "PLAYER", 130f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, true);
            winnerText.enableAutoSizing = true;
            winnerText.fontSizeMin = 56f;
            winnerText.fontSizeMax = 130f;
            var score = MobileUI.ColRow(page, "Score", 0f, 0.42f, 650f, 100f);
            scoreText = MobileUI.Text(score, "Text", "P1  0 – 0  P2", 64f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 38f;
            scoreText.fontSizeMax = 64f;

            // Right column: round-by-round table.
            const float rowHeight = 86f;
            var table = MobileUI.ColRow(page, "Table", 0.42f, 1f, 160f, rowHeight * (MaxRounds + 2) + 24f);
            MobileUI.Panel(table.gameObject);
            TableRow(table, "Header", 0, rowHeight, out var headerCells);
            headerCells[0].text = "";
            headerTexts[0] = headerCells[1];
            headerTexts[1] = headerCells[2];
            headerTexts[0].color = SoundCopyTheme.Lime;
            headerTexts[1].color = SoundCopyTheme.Coral;
            for (int i = 0; i < MaxRounds; i++)
            {
                rows[i] = TableRow(table, "Round" + (i + 1), i + 1, rowHeight, out var cells).gameObject;
                for (int c = 0; c < 3; c++) rowTexts[i * 3 + c] = cells[c];
            }
            TableRow(table, "Average", MaxRounds + 1, rowHeight, out var avgCells);
            avgCells[0].text = "AVG";
            averageTexts[0] = avgCells[1];
            averageTexts[1] = avgCells[2];
            foreach (var c in avgCells) c.color = SoundCopyTheme.PaleBonus;

            // Bottom: rematch (left of) continue (bottom-right).
            var rematch = MobileUI.BottomRight(page, "RematchAction", 420f, MobileUI.ButtonHeight, MobileUI.Side + MobileUI.ButtonWidth + 30f);
            rematchButton = MobileUI.BigButton(rematch, "RematchButton", "REMATCH", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 46f);
            var next = MobileUI.BottomRight(page, "ContinueAction");
            continueButton = MobileUI.BigButton(next, "ContinueButton", "CONTINUE  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
        }

        static RectTransform TableRow(RectTransform table, string name, int index, float height, out TextMeshProUGUI[] cells)
        {
            var row = MobileUI.Row(table, name, 12f + index * height, height, 30f);
            cells = new TextMeshProUGUI[3];
            float[] widths = { 0.2f, 0.4f, 0.4f };
            float x = 0f;
            for (int c = 0; c < 3; c++)
            {
                var cell = UIFactory.CreateRect("Cell" + c, row, new Vector2(x, 0f), new Vector2(x + widths[c], 1f), Vector2.zero, Vector2.zero);
                x += widths[c];
                cells[c] = MobileUI.Text(cell.transform, "Text", "", 42f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, c == 0 || index == 0);
                cells[c].enableAutoSizing = true;
                cells[c].fontSizeMin = 28f;
                cells[c].fontSizeMax = 42f;
            }
            return row;
        }
    }
}
