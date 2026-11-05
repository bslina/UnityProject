using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // One screen for the whole match setup: player names and category. A match is always 3 rounds.
    public sealed class SetupPanel : MonoBehaviour
    {
        const string Name1Key = "soundcopy.player1";
        const string Name2Key = "soundcopy.player2";

        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_InputField player1Input;
        [SerializeField] private TMP_InputField player2Input;
        [SerializeField] private Button[] categoryButtons = new Button[3];
        [SerializeField] private Image[] categoryGraphics = new Image[3];
        [SerializeField] private TextMeshProUGUI[] categoryLabels = new TextMeshProUGUI[6];
        [SerializeField] private GameObject[] categoryTicks = new GameObject[3];
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        static readonly ChallengeCategory[] CategoryOptions = { ChallengeCategory.International, ChallengeCategory.Tunisian, ChallengeCategory.Mixed };
        static readonly Color[] CategoryColors = { new Color(0.53f, 0.84f, 1f), new Color(1f, 0.68f, 0.63f), SoundCopyTheme.Pink };
        static readonly Color Unselected = new Color(0.17f, 0.11f, 0.21f);

        ChallengeCategory category = ChallengeCategory.International;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                var value = CategoryOptions[i];
                if (categoryButtons[i] != null) categoryButtons[i].onClick.AddListener(() => { category = value; Refresh(); });
            }
            startButton?.onClick.AddListener(StartMatch);
            backButton?.onClick.AddListener(() => gameManager?.OnSetupBack());
        }

        public void Open(string name1, string name2, ChallengeCategory currentCategory)
        {
            category = currentCategory;
            if (player1Input != null) player1Input.text = PlayerPrefs.GetString(Name1Key, name1 == "Player 1" ? "" : name1);
            if (player2Input != null) player2Input.text = PlayerPrefs.GetString(Name2Key, name2 == "Player 2" ? "" : name2);
            Refresh();
        }

        void StartMatch()
        {
            string n1 = player1Input != null ? player1Input.text.Trim() : "";
            string n2 = player2Input != null ? player2Input.text.Trim() : "";
            PlayerPrefs.SetString(Name1Key, n1);
            PlayerPrefs.SetString(Name2Key, n2);
            PlayerPrefs.Save();
            gameManager?.OnSetupConfirmed(n1, n2, category);
        }

        void Refresh()
        {
            for (int i = 0; i < categoryGraphics.Length; i++)
            {
                bool on = CategoryOptions[i] == category;
                if (categoryGraphics[i] != null) categoryGraphics[i].color = on ? CategoryColors[i] : Unselected;
                SetColor(categoryLabels, i * 2, on ? SoundCopyTheme.Ink : SoundCopyTheme.Cream);
                SetColor(categoryLabels, i * 2 + 1, on ? SoundCopyTheme.Ink : new Color(1f, 1f, 1f, 0.6f));
                if (categoryTicks[i] != null) categoryTicks[i].SetActive(on);
            }
        }

        static void SetColor(TextMeshProUGUI[] texts, int index, Color color)
        {
            if (texts != null && index < texts.Length && texts[index] != null) texts[index].color = color;
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            backButton = MobileUI.TopBar(page, "NEW MATCH", true, SoundCopyTheme.Cream, out _, out _);

            // Left column: players + how a match works.
            SectionLabel(page, "PLAYERS", 0f, 0.5f, 165f);
            player1Input = NameRow(page, 1, 215f);
            player2Input = NameRow(page, 2, 350f);

            var info = MobileUI.ColRow(page, "MatchInfo", 0f, 0.5f, 510f, 230f);
            MobileUI.Panel(info.gameObject);
            var rounds = MobileUI.Text(info, "Rounds", "3", 150f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, true);
            rounds.rectTransform.anchorMin = new Vector2(0f, 0.24f);
            rounds.rectTransform.anchorMax = new Vector2(0.28f, 1f);
            var roundsLabel = MobileUI.Text(info, "RoundsLabel", "ROUNDS", 30f, SoundCopyTheme.Cream, TextAlignmentOptions.Bottom);
            roundsLabel.rectTransform.anchorMax = new Vector2(0.28f, 0.3f);
            roundsLabel.rectTransform.offsetMin = new Vector2(0f, 18f);
            var rule = MobileUI.Text(info, "Rule", "Before each round, <color=#FFF1B8>spin the phone</color>:\nit picks who goes first, and a fast spin makes it a x2 round!", 36f, SoundCopyTheme.Cream, TextAlignmentOptions.MidlineLeft);
            rule.rectTransform.anchorMin = new Vector2(0.28f, 0f);
            rule.rectTransform.offsetMin = new Vector2(10f, 0f);
            rule.rectTransform.offsetMax = new Vector2(-30f, 0f);
            rule.textWrappingMode = TextWrappingModes.Normal;

            // Right column: category.
            SectionLabel(page, "CATEGORY", 0.5f, 1f, 165f);
            string[] titles = { "INTERNATIONAL", "TUNISIAN", "MIXED" };
            string[] descriptions = { "Bruh, Nokia, airhorn… the classics", "Famous Tunisian lines and sounds", "A bit of both: surprise!" };
            for (int i = 0; i < 3; i++)
            {
                var row = MobileUI.ColRow(page, "Category" + titles[i], 0.5f, 1f, 215f + i * 150f, 135f);
                categoryGraphics[i] = MobileUI.Card(row.gameObject, Unselected, 36f, true, true);
                categoryButtons[i] = row.gameObject.AddComponent<Button>();
                categoryButtons[i].targetGraphic = categoryGraphics[i];
                var title = MobileUI.Text(row, "Title", titles[i], 50f, SoundCopyTheme.Cream, TextAlignmentOptions.BottomLeft, true);
                title.rectTransform.anchorMin = new Vector2(0f, 0.45f);
                title.rectTransform.offsetMin = new Vector2(36f, 0f);
                title.rectTransform.offsetMax = new Vector2(-140f, -10f);
                var desc = MobileUI.Text(row, "Description", descriptions[i], 32f, SoundCopyTheme.Cream, TextAlignmentOptions.TopLeft);
                desc.rectTransform.anchorMax = new Vector2(1f, 0.45f);
                desc.rectTransform.offsetMin = new Vector2(36f, 0f);
                desc.rectTransform.offsetMax = new Vector2(-140f, 0f);
                categoryLabels[i * 2] = title;
                categoryLabels[i * 2 + 1] = desc;

                var tick = UIFactory.CreateRect("Selected", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
                var tickRt = (RectTransform)tick.transform;
                tickRt.pivot = new Vector2(1f, 0.5f);
                tickRt.anchoredPosition = new Vector2(-28f, 0f);
                tickRt.sizeDelta = new Vector2(88f, 88f);
                UIFactory.AddRounded(tick, SoundCopyTheme.Ink, 44f);
                MobileUI.Text(tick.transform, "Label", "OK", 36f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
                categoryTicks[i] = tick;
            }

            var start = MobileUI.BottomRight(page, "StartAction");
            startButton = MobileUI.BigButton(start, "StartButton", "START THE MATCH  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
            Refresh();
        }

        static void SectionLabel(RectTransform page, string text, float xMin, float xMax, float top)
        {
            var row = MobileUI.ColRow(page, "Label" + text, xMin, xMax, top, 46f);
            MobileUI.Text(row, "Text", text, 34f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.MidlineLeft);
        }

        static TMP_InputField NameRow(RectTransform page, int player, float top)
        {
            var row = MobileUI.ColRow(page, "Player" + player, 0f, 0.5f, top, 120f);
            MobileUI.Badge(row, "Badge", "P" + player, MobileUI.PlayerColor(player), 116f, 48f);

            var field = UIFactory.CreateRect("NameField", row, Vector2.zero, Vector2.one, new Vector2(140f, 0f), Vector2.zero);
            var bg = UIFactory.AddRounded(field, SoundCopyTheme.Cream, 34f, true);
            UIFactory.AddNativeOutline(field, MobileUI.PlayerColor(player), 5f);
            var input = field.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 14;
            var area = UIFactory.CreateRect("TextArea", field.transform, Vector2.zero, Vector2.one, new Vector2(34f, 8f), new Vector2(-34f, -8f));
            area.AddComponent<RectMask2D>();
            var placeholder = MobileUI.Text(area.transform, "Placeholder", "Player " + player + " name", 42f, SoundCopyTheme.MutedGray, TextAlignmentOptions.MidlineLeft);
            var text = MobileUI.Text(area.transform, "Text", "", 48f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft);
            input.textViewport = (RectTransform)area.transform;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.caretColor = SoundCopyTheme.Pink;
            input.caretWidth = 4;
            input.selectionColor = new Color(SoundCopyTheme.Lime.r, SoundCopyTheme.Lime.g, SoundCopyTheme.Lime.b, 0.5f);
            if (MobileUI.BodyFont != null) input.fontAsset = MobileUI.BodyFont;
            input.pointSize = 48f;
            return input;
        }
    }
}
