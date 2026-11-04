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
        [SerializeField] private RoundedRectGraphic[] trophyParts = new RoundedRectGraphic[4];
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

            if (kickerText != null) kickerText.text = winner == 0 ? "MATCH NUL" : "VAINQUEUR";
            if (winnerText != null)
            {
                winnerText.text = winner == 0 ? "ÉGALITÉ PARFAITE" : gameManager.PlayerName(winner).ToUpperInvariant();
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
                SetRow(i, 0, "M" + r.roundNumber, SoundCopyTheme.Cream);
                SetRow(i, 1, Cell(r.player1Percent, r.player1TooQuiet, r.player1Joker), r.winner == 1 ? SoundCopyTheme.Lime : new Color(1f, 1f, 1f, 0.55f));
                SetRow(i, 2, Cell(r.player2Percent, r.player2TooQuiet, r.player2Joker), r.winner == 2 ? SoundCopyTheme.Coral : new Color(1f, 1f, 1f, 0.55f));
            }
            if (averageTexts[0] != null) averageTexts[0].text = Mathf.RoundToInt(gameManager.AverageAccuracy(1)) + "%";
            if (averageTexts[1] != null) averageTexts[1].text = Mathf.RoundToInt(gameManager.AverageAccuracy(2)) + "%";

            Haptics.Pulse(300, 255);
        }

        static string Cell(float percent, bool quiet, bool joker) =>
            (quiet ? "—" : Mathf.RoundToInt(percent) + "%") + (joker ? "  x2" : "");

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
            MobileUI.TopBar(page, "FIN DU MATCH", false, SoundCopyTheme.Cream, out _, out _);

            // Trophy: cup, stem and base, tinted with the winner's colour.
            var trophy = MobileUI.Row(page, "Trophy", 190f, 330f, 370f);
            var cup = UIFactory.CreateRect("Cup", trophy, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 1f), Vector2.zero, Vector2.zero);
            trophyParts[0] = MobileUI.Card(cup, SoundCopyTheme.Lime, 90f);
            var stem = UIFactory.CreateRect("Stem", trophy, new Vector2(0.42f, 0.18f), new Vector2(0.58f, 0.46f), Vector2.zero, Vector2.zero);
            trophyParts[1] = MobileUI.Card(stem, SoundCopyTheme.Lime, 10f);
            var stand = UIFactory.CreateRect("Stand", trophy, new Vector2(0.22f, 0f), new Vector2(0.78f, 0.18f), Vector2.zero, Vector2.zero);
            trophyParts[2] = MobileUI.Card(stand, SoundCopyTheme.Lime, 16f);
            var star = UIFactory.CreateRect("Shine", cup.transform, new Vector2(0.2f, 0.55f), new Vector2(0.32f, 0.85f), Vector2.zero, Vector2.zero);
            trophyParts[3] = null;
            UIFactory.AddRounded(star, new Color(1f, 1f, 1f, 0.6f), 20f);
            MobileUI.Text(cup.transform, "Number", "1", 120f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);

            var kicker = MobileUI.Row(page, "Kicker", 560f, 60f);
            kickerText = MobileUI.Text(kicker, "Text", "VAINQUEUR", 42f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
            var winner = MobileUI.Row(page, "Winner", 620f, 170f);
            winnerText = MobileUI.Text(winner, "Text", "JOUEUR", 140f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, true);
            winnerText.enableAutoSizing = true;
            winnerText.fontSizeMin = 60f;
            winnerText.fontSizeMax = 140f;
            var score = MobileUI.Row(page, "Score", 800f, 110f);
            scoreText = MobileUI.Text(score, "Text", "J1  0 – 0  J2", 66f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 40f;
            scoreText.fontSizeMax = 66f;

            // Round-by-round table.
            var table = MobileUI.Row(page, "Table", 950f, 72f * (MaxRounds + 2) + 30f);
            MobileUI.Card(table.gameObject, new Color(0.12f, 0.07f, 0.15f), 36f);
            var header = TableRow(table, "Header", 0, out var headerCells);
            headerCells[0].text = "";
            headerTexts[0] = headerCells[1];
            headerTexts[1] = headerCells[2];
            headerTexts[0].color = SoundCopyTheme.Lime;
            headerTexts[1].color = SoundCopyTheme.Coral;
            for (int i = 0; i < MaxRounds; i++)
            {
                rows[i] = TableRow(table, "Round" + (i + 1), i + 1, out var cells).gameObject;
                for (int c = 0; c < 3; c++) rowTexts[i * 3 + c] = cells[c];
            }
            var avg = TableRow(table, "Average", MaxRounds + 1, out var avgCells);
            avgCells[0].text = "MOY.";
            averageTexts[0] = avgCells[1];
            averageTexts[1] = avgCells[2];
            foreach (var c in avgCells) c.color = SoundCopyTheme.PaleBonus;

            var rematch = MobileUI.BottomRow(page, "RematchAction", MobileUI.BottomMargin + MobileUI.ButtonHeight + 30f, 140f);
            rematchButton = MobileUI.BigButton(rematch, "RematchButton", "REVANCHE", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 46f);
            var next = MobileUI.BottomRow(page, "ContinueAction", MobileUI.BottomMargin, MobileUI.ButtonHeight);
            continueButton = MobileUI.BigButton(next, "ContinueButton", "CONTINUER  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
        }

        static RectTransform TableRow(RectTransform table, string name, int index, out TextMeshProUGUI[] cells)
        {
            var row = MobileUI.Row(table, name, 15f + index * 72f, 72f, 30f);
            cells = new TextMeshProUGUI[3];
            float[] widths = { 0.22f, 0.39f, 0.39f };
            float x = 0f;
            for (int c = 0; c < 3; c++)
            {
                var cell = UIFactory.CreateRect("Cell" + c, row, new Vector2(x, 0f), new Vector2(x + widths[c], 1f), Vector2.zero, Vector2.zero);
                x += widths[c];
                cells[c] = MobileUI.Text(cell.transform, "Text", "", 40f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, c == 0 || index == 0);
                cells[c].enableAutoSizing = true;
                cells[c].fontSizeMin = 28f;
                cells[c].fontSizeMax = 40f;
            }
            return row;
        }
    }
}
