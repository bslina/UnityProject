using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Result of one round: who won, each player's AI score (animated bars) and points (x2 after a fast spin).
    public sealed class VerdictPanel : MonoBehaviour
    {
        static readonly Color CardIdle = new Color(0.17f, 0.11f, 0.21f);

        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI headlineText;
        [SerializeField] private RectTransform[] cards = new RectTransform[2];
        [SerializeField] private Image[] cardGraphics = new Image[2];
        [SerializeField] private TextMeshProUGUI[] nameTexts = new TextMeshProUGUI[2];
        [SerializeField] private TextMeshProUGUI[] percentTexts = new TextMeshProUGUI[2];
        [SerializeField] private RectTransform[] barFills = new RectTransform[2];
        [SerializeField] private Image[] barGraphics = new Image[2];
        [SerializeField] private GameObject[] tags = new GameObject[2];
        [SerializeField] private TextMeshProUGUI[] tagTexts = new TextMeshProUGUI[2];
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button continueButton;
        [SerializeField] private TextMeshProUGUI continueLabel;

        RoundRecord record;
        float animTime;
        readonly float[] targets = new float[2];

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake() => continueButton?.onClick.AddListener(() => gameManager?.OnVerdictContinue());

        public void Show(RoundRecord round)
        {
            record = round;
            animTime = 0f;
            if (gameManager == null || round == null) return;

            if (titleText != null) titleText.text = "VERDICT  ·  ROUND " + round.roundNumber;
            if (headlineText != null)
            {
                headlineText.text = round.winner == 0 ? "DRAW!" : gameManager.PlayerName(round.winner).ToUpperInvariant() + " WINS!";
                headlineText.color = round.winner == 0 ? SoundCopyTheme.Cream : MobileUI.PlayerColor(round.winner);
            }

            for (int i = 0; i < 2; i++)
            {
                int player = i + 1;
                bool won = round.winner == player;
                bool quiet = player == 1 ? round.player1TooQuiet : round.player2TooQuiet;
                targets[i] = player == 1 ? round.player1Percent : round.player2Percent;

                Color ink = won ? SoundCopyTheme.Ink : SoundCopyTheme.Cream;
                if (cardGraphics[i] != null) cardGraphics[i].color = won ? MobileUI.PlayerColor(player) : CardIdle;
                if (nameTexts[i] != null) { nameTexts[i].text = gameManager.PlayerName(player); nameTexts[i].color = ink; }
                if (percentTexts[i] != null) percentTexts[i].color = ink;
                if (barGraphics[i] != null) barGraphics[i].color = won ? SoundCopyTheme.Ink : MobileUI.PlayerColor(player);

                string tag = quiet ? "TOO QUIET"
                    : won ? (round.doubled ? $"+{round.points}  x2 ROUND" : $"+{round.points}")
                    : "";
                if (tags[i] != null) tags[i].SetActive(tag.Length > 0);
                if (tagTexts[i] != null) tagTexts[i].text = tag;
            }

            if (scoreText != null)
                scoreText.text = $"{gameManager.Player1Name}  {gameManager.Player1Score} – {gameManager.Player2Score}  {gameManager.Player2Name}";
            if (continueLabel != null) continueLabel.text = gameManager.IsLastRound ? "SEE RESULTS  >" : "NEXT ROUND  >";
            Apply(0f);
            if (round.winner != 0) Haptics.Pulse(150, 200);
            GameUI.GameAudio.Play(round.winner != 0 ? "point" : "spin_result");
        }

        private void Update()
        {
            if (record == null || animTime > 2f) return;
            animTime += Time.unscaledDeltaTime;
            Apply(animTime);
        }

        void Apply(float time)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 1.2f));
            for (int i = 0; i < 2; i++)
            {
                float value = targets[i] * t;
                if (percentTexts[i] != null) percentTexts[i].text = Mathf.RoundToInt(value) + "%";
                if (barFills[i] != null) barFills[i].anchorMax = new Vector2(Mathf.Clamp01(value / 100f), 1f);
                if (cards[i] != null)
                {
                    bool won = record != null && record.winner == i + 1;
                    float pop = won && time > 1.2f ? 1f + 0.04f * Mathf.Sin(Mathf.Clamp01((time - 1.2f) / 0.4f) * Mathf.PI) : 1f;
                    cards[i].localScale = new Vector3(pop, pop, 1f);
                }
            }
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            MobileUI.TopBar(page, "VERDICT", false, SoundCopyTheme.Cream, out titleText, out _);

            var headline = MobileUI.Row(page, "Headline", 150f, 140f);
            headlineText = MobileUI.Text(headline, "Text", "DRAW!", 110f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
            headlineText.enableAutoSizing = true;
            headlineText.fontSizeMin = 56f;
            headlineText.fontSizeMax = 110f;

            // Two player cards side by side.
            var cardsRow = MobileUI.Row(page, "Cards", 320f, 470f);
            for (int i = 0; i < 2; i++)
            {
                int player = i + 1;
                var card = MobileUI.Cell(cardsRow, "Player" + player, i, 2, 56f);
                cards[i] = card;
                cardGraphics[i] = MobileUI.Card(card.gameObject, CardIdle, 44f);
                UIFactory.AddNativeOutline(card.gameObject, MobileUI.PlayerColor(player), 5f);

                var badge = MobileUI.Badge(card, "Badge", "P" + player, MobileUI.PlayerColor(player), 100f, 44f);
                var badgeRt = (RectTransform)badge.transform;
                badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0f, 1f);
                badgeRt.pivot = new Vector2(0f, 1f);
                badgeRt.anchoredPosition = new Vector2(36f, -36f);

                nameTexts[i] = MobileUI.Text(card, "Name", "Player", 56f, SoundCopyTheme.Cream, TextAlignmentOptions.MidlineLeft, true);
                nameTexts[i].rectTransform.anchorMin = new Vector2(0f, 1f);
                nameTexts[i].rectTransform.anchorMax = new Vector2(1f, 1f);
                nameTexts[i].rectTransform.pivot = new Vector2(0.5f, 1f);
                nameTexts[i].rectTransform.offsetMin = new Vector2(160f, -136f);
                nameTexts[i].rectTransform.offsetMax = new Vector2(-36f, -36f);
                nameTexts[i].enableAutoSizing = true;
                nameTexts[i].fontSizeMin = 34f;
                nameTexts[i].fontSizeMax = 56f;

                percentTexts[i] = MobileUI.Text(card, "Percent", "0%", 150f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
                percentTexts[i].rectTransform.anchorMin = new Vector2(0f, 0.37f);
                percentTexts[i].rectTransform.anchorMax = new Vector2(1f, 0.72f);

                var track = UIFactory.CreateRect("Bar", card, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
                var trackRt = (RectTransform)track.transform;
                trackRt.pivot = new Vector2(0.5f, 0f);
                trackRt.offsetMin = new Vector2(36f, 36f);
                trackRt.offsetMax = new Vector2(-36f, 80f);
                if (SoundCopySkin.BarTrack != null) UIFactory.AddSprite(track, SoundCopySkin.BarTrack, Color.white);
                else UIFactory.AddRounded(track, new Color(0f, 0f, 0f, 0.25f), 22f);
                var fill = UIFactory.CreateRect("Fill", track.transform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                barGraphics[i] = UIFactory.AddRounded(fill, MobileUI.PlayerColor(player), 22f);
                barFills[i] = (RectTransform)fill.transform;

                var tag = UIFactory.CreateRect("Tag", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
                var tagRt = (RectTransform)tag.transform;
                tagRt.pivot = new Vector2(0.5f, 0f);
                tagRt.anchoredPosition = new Vector2(0f, 100f);
                tagRt.sizeDelta = new Vector2(420f, 70f);
                UIFactory.AddRounded(tag, SoundCopyTheme.PaleBonus, 35f);
                UIFactory.AddNativeOutline(tag, SoundCopyTheme.Ink, 3f);
                tagTexts[i] = MobileUI.Text(tag.transform, "Text", "+1", 38f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
                tags[i] = tag;
            }

            // Bottom: running score (left) and continue (right).
            var score = UIFactory.CreateRect("Score", page, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            var scoreRt = (RectTransform)score.transform;
            scoreRt.pivot = new Vector2(0.5f, 0f);
            scoreRt.offsetMin = new Vector2(MobileUI.Side, MobileUI.BottomMargin);
            scoreRt.offsetMax = new Vector2(-(MobileUI.Side + MobileUI.ButtonWidth + 40f), MobileUI.BottomMargin + MobileUI.ButtonHeight);
            scoreText = MobileUI.Text(score.transform, "Text", "P1  0 – 0  P2", 72f, SoundCopyTheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 40f;
            scoreText.fontSizeMax = 72f;

            var next = MobileUI.BottomRight(page, "ContinueAction");
            continueButton = MobileUI.BigButton(next, "ContinueButton", "NEXT ROUND  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink, out continueLabel);
        }
    }
}
