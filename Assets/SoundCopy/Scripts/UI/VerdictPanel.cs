using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Result of one round: who won, each player's AI score (animated bars), points and jokers.
    public sealed class VerdictPanel : MonoBehaviour
    {
        static readonly Color CardIdle = new Color(0.17f, 0.11f, 0.21f);

        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI headlineText;
        [SerializeField] private RectTransform[] cards = new RectTransform[2];
        [SerializeField] private RoundedRectGraphic[] cardGraphics = new RoundedRectGraphic[2];
        [SerializeField] private TextMeshProUGUI[] nameTexts = new TextMeshProUGUI[2];
        [SerializeField] private TextMeshProUGUI[] percentTexts = new TextMeshProUGUI[2];
        [SerializeField] private RectTransform[] barFills = new RectTransform[2];
        [SerializeField] private RoundedRectGraphic[] barGraphics = new RoundedRectGraphic[2];
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

            if (titleText != null) titleText.text = "VERDICT  ·  MANCHE " + round.roundNumber;
            if (headlineText != null)
            {
                headlineText.text = round.winner == 0 ? "ÉGALITÉ !" : gameManager.PlayerName(round.winner).ToUpperInvariant() + " GAGNE !";
                headlineText.color = round.winner == 0 ? SoundCopyTheme.Cream : MobileUI.PlayerColor(round.winner);
            }

            for (int i = 0; i < 2; i++)
            {
                int player = i + 1;
                bool won = round.winner == player;
                bool quiet = player == 1 ? round.player1TooQuiet : round.player2TooQuiet;
                bool joker = player == 1 ? round.player1Joker : round.player2Joker;
                targets[i] = player == 1 ? round.player1Percent : round.player2Percent;

                Color ink = won ? SoundCopyTheme.Ink : SoundCopyTheme.Cream;
                if (cardGraphics[i] != null) cardGraphics[i].color = won ? MobileUI.PlayerColor(player) : CardIdle;
                if (nameTexts[i] != null) { nameTexts[i].text = gameManager.PlayerName(player); nameTexts[i].color = ink; }
                if (percentTexts[i] != null) percentTexts[i].color = ink;
                if (barGraphics[i] != null) barGraphics[i].color = won ? SoundCopyTheme.Ink : MobileUI.PlayerColor(player);

                string tag = quiet ? "TROP SILENCIEUX"
                    : won ? (joker ? $"+{round.points}  JOKER x2" : $"+{round.points}")
                    : joker ? "JOKER PERDU" : "";
                if (tags[i] != null) tags[i].SetActive(tag.Length > 0);
                if (tagTexts[i] != null) tagTexts[i].text = tag;
            }

            if (scoreText != null)
                scoreText.text = $"{gameManager.Player1Name}  {gameManager.Player1Score} – {gameManager.Player2Score}  {gameManager.Player2Name}";
            if (continueLabel != null) continueLabel.text = gameManager.IsLastRound ? "VOIR LES RÉSULTATS  >" : "MANCHE SUIVANTE  >";
            Apply(0f);
            if (round.winner != 0) Haptics.Pulse(150, 200);
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

            var headline = MobileUI.Row(page, "Headline", 200f, 190f);
            headlineText = MobileUI.Text(headline, "Text", "ÉGALITÉ !", 100f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
            headlineText.enableAutoSizing = true;
            headlineText.fontSizeMin = 56f;
            headlineText.fontSizeMax = 100f;

            for (int i = 0; i < 2; i++)
            {
                int player = i + 1;
                var card = MobileUI.Row(page, "Player" + player, 430f + i * 370f, 330f);
                cards[i] = card;
                cardGraphics[i] = MobileUI.Card(card.gameObject, CardIdle, 44f);
                UIFactory.AddNativeOutline(card.gameObject, MobileUI.PlayerColor(player), 5f);

                MobileUI.Badge(card, "Badge", "J" + player, MobileUI.PlayerColor(player), 110f, 46f).GetComponent<RectTransform>().anchoredPosition = new Vector2(36f, 40f);
                nameTexts[i] = MobileUI.Text(card, "Name", "Joueur", 56f, SoundCopyTheme.Cream, TextAlignmentOptions.MidlineLeft, true);
                nameTexts[i].rectTransform.anchorMin = new Vector2(0f, 0.42f);
                nameTexts[i].rectTransform.anchorMax = new Vector2(0.62f, 1f);
                nameTexts[i].rectTransform.offsetMin = new Vector2(170f, 0f);
                nameTexts[i].enableAutoSizing = true;
                nameTexts[i].fontSizeMin = 36f;
                nameTexts[i].fontSizeMax = 56f;

                percentTexts[i] = MobileUI.Text(card, "Percent", "0%", 130f, SoundCopyTheme.Cream, TextAlignmentOptions.MidlineRight, true);
                percentTexts[i].rectTransform.anchorMin = new Vector2(0.55f, 0.32f);
                percentTexts[i].rectTransform.offsetMax = new Vector2(-40f, -10f);

                var track = UIFactory.CreateRect("Bar", card, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
                var trackRt = (RectTransform)track.transform;
                trackRt.pivot = new Vector2(0.5f, 0f);
                trackRt.offsetMin = new Vector2(40f, 40f);
                trackRt.offsetMax = new Vector2(-40f, 84f);
                UIFactory.AddRounded(track, new Color(0f, 0f, 0f, 0.25f), 22f);
                var fill = UIFactory.CreateRect("Fill", track.transform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                barGraphics[i] = UIFactory.AddRounded(fill, MobileUI.PlayerColor(player), 22f);
                barFills[i] = (RectTransform)fill.transform;

                var tag = UIFactory.CreateRect("Tag", card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                var tagRt = (RectTransform)tag.transform;
                tagRt.pivot = new Vector2(0f, 0.5f);
                tagRt.anchoredPosition = new Vector2(170f, -20f);
                tagRt.sizeDelta = new Vector2(400f, 70f);
                UIFactory.AddRounded(tag, SoundCopyTheme.PaleBonus, 35f);
                UIFactory.AddNativeOutline(tag, SoundCopyTheme.Ink, 3f);
                tagTexts[i] = MobileUI.Text(tag.transform, "Text", "+1", 38f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
                tags[i] = tag;
            }

            var score = MobileUI.Row(page, "Score", 1180f, 130f);
            scoreText = MobileUI.Text(score, "Text", "J1  0 – 0  J2", 72f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 40f;
            scoreText.fontSizeMax = 72f;

            var next = MobileUI.BottomRow(page, "ContinueAction", MobileUI.BottomMargin, MobileUI.ButtonHeight);
            continueButton = MobileUI.BigButton(next, "ContinueButton", "MANCHE SUIVANTE  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink, out continueLabel);
        }
    }
}
