using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // One screen for the whole match setup: player names, number of rounds and category.
    public sealed class SetupPanel : MonoBehaviour
    {
        const string Name1Key = "soundcopy.player1";
        const string Name2Key = "soundcopy.player2";

        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_InputField player1Input;
        [SerializeField] private TMP_InputField player2Input;
        [SerializeField] private Button[] roundButtons = new Button[3];
        [SerializeField] private RoundedRectGraphic[] roundGraphics = new RoundedRectGraphic[3];
        [SerializeField] private TextMeshProUGUI[] roundLabels = new TextMeshProUGUI[6];
        [SerializeField] private Button[] categoryButtons = new Button[3];
        [SerializeField] private RoundedRectGraphic[] categoryGraphics = new RoundedRectGraphic[3];
        [SerializeField] private TextMeshProUGUI[] categoryLabels = new TextMeshProUGUI[6];
        [SerializeField] private GameObject[] categoryTicks = new GameObject[3];
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        static readonly int[] RoundOptions = { 1, 3, 5 };
        static readonly ChallengeCategory[] CategoryOptions = { ChallengeCategory.International, ChallengeCategory.Tunisian, ChallengeCategory.Mixed };
        static readonly Color[] CategoryColors = { new Color(0.53f, 0.84f, 1f), new Color(1f, 0.68f, 0.63f), SoundCopyTheme.Pink };
        static readonly Color Unselected = new Color(0.17f, 0.11f, 0.21f);

        int rounds = 3;
        ChallengeCategory category = ChallengeCategory.International;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            for (int i = 0; i < roundButtons.Length; i++)
            {
                int value = RoundOptions[i];
                if (roundButtons[i] != null) roundButtons[i].onClick.AddListener(() => { rounds = value; Refresh(); });
            }
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                var value = CategoryOptions[i];
                if (categoryButtons[i] != null) categoryButtons[i].onClick.AddListener(() => { category = value; Refresh(); });
            }
            startButton?.onClick.AddListener(StartMatch);
            backButton?.onClick.AddListener(() => gameManager?.OnSetupBack());
        }

        public void Open(string name1, string name2, int currentRounds, ChallengeCategory currentCategory)
        {
            rounds = currentRounds;
            category = currentCategory;
            if (player1Input != null) player1Input.text = PlayerPrefs.GetString(Name1Key, name1 == "Joueur 1" ? "" : name1);
            if (player2Input != null) player2Input.text = PlayerPrefs.GetString(Name2Key, name2 == "Joueur 2" ? "" : name2);
            Refresh();
        }

        void StartMatch()
        {
            string n1 = player1Input != null ? player1Input.text.Trim() : "";
            string n2 = player2Input != null ? player2Input.text.Trim() : "";
            PlayerPrefs.SetString(Name1Key, n1);
            PlayerPrefs.SetString(Name2Key, n2);
            PlayerPrefs.Save();
            gameManager?.OnSetupConfirmed(n1, n2, rounds, category);
        }

        void Refresh()
        {
            for (int i = 0; i < roundGraphics.Length; i++)
            {
                bool on = RoundOptions[i] == rounds;
                if (roundGraphics[i] != null) roundGraphics[i].color = on ? SoundCopyTheme.Lime : Unselected;
                SetColor(roundLabels, i * 2, on ? SoundCopyTheme.Ink : SoundCopyTheme.Cream);
                SetColor(roundLabels, i * 2 + 1, on ? SoundCopyTheme.Ink : new Color(1f, 1f, 1f, 0.6f));
            }
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
            backButton = MobileUI.TopBar(page, "NOUVELLE PARTIE", true, SoundCopyTheme.Cream, out _, out _);

            SectionLabel(page, "JOUEURS", 200f);
            player1Input = NameRow(page, 1, 255f);
            player2Input = NameRow(page, 2, 420f);

            SectionLabel(page, "MANCHES", 600f);
            var roundsRow = MobileUI.Row(page, "Rounds", 655f, 190f);
            string[] captions = { "RAPIDE", "NORMAL", "CHAOS" };
            for (int i = 0; i < 3; i++)
            {
                var cell = MobileUI.Cell(roundsRow, "Rounds" + RoundOptions[i], i, 3);
                roundGraphics[i] = MobileUI.Card(cell.gameObject, Unselected, 36f, true, true);
                roundButtons[i] = cell.gameObject.AddComponent<Button>();
                roundButtons[i].targetGraphic = roundGraphics[i];
                var number = MobileUI.Text(cell, "Number", RoundOptions[i].ToString(), 96f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
                number.rectTransform.anchorMin = new Vector2(0f, 0.32f);
                var caption = MobileUI.Text(cell, "Caption", captions[i], 34f, SoundCopyTheme.Cream, TextAlignmentOptions.Center);
                caption.rectTransform.anchorMax = new Vector2(1f, 0.36f);
                caption.rectTransform.offsetMin = new Vector2(0f, 14f);
                roundLabels[i * 2] = number;
                roundLabels[i * 2 + 1] = caption;
            }

            SectionLabel(page, "CATÉGORIE", 885f);
            string[] titles = { "INTERNATIONAL", "TOUNSI", "MIXTE" };
            string[] descriptions = { "Bruh, Nokia, airhorn… les classiques", "Répliques et sons cultes du pays", "Un peu des deux : surprise !" };
            for (int i = 0; i < 3; i++)
            {
                var row = MobileUI.Row(page, "Category" + titles[i], 940f + i * 175f, 155f);
                categoryGraphics[i] = MobileUI.Card(row.gameObject, Unselected, 36f, true, true);
                categoryButtons[i] = row.gameObject.AddComponent<Button>();
                categoryButtons[i].targetGraphic = categoryGraphics[i];
                var title = MobileUI.Text(row, "Title", titles[i], 52f, SoundCopyTheme.Cream, TextAlignmentOptions.BottomLeft, true);
                title.rectTransform.anchorMin = new Vector2(0f, 0.45f);
                title.rectTransform.offsetMin = new Vector2(40f, 0f);
                title.rectTransform.offsetMax = new Vector2(-150f, -12f);
                var desc = MobileUI.Text(row, "Description", descriptions[i], 34f, SoundCopyTheme.Cream, TextAlignmentOptions.TopLeft);
                desc.rectTransform.anchorMax = new Vector2(1f, 0.45f);
                desc.rectTransform.offsetMin = new Vector2(40f, 0f);
                desc.rectTransform.offsetMax = new Vector2(-150f, 0f);
                categoryLabels[i * 2] = title;
                categoryLabels[i * 2 + 1] = desc;

                var tick = UIFactory.CreateRect("Selected", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
                var tickRt = (RectTransform)tick.transform;
                tickRt.pivot = new Vector2(1f, 0.5f);
                tickRt.anchoredPosition = new Vector2(-30f, 0f);
                tickRt.sizeDelta = new Vector2(96f, 96f);
                UIFactory.AddRounded(tick, SoundCopyTheme.Ink, 48f);
                MobileUI.Text(tick.transform, "Label", "OK", 38f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
                categoryTicks[i] = tick;
            }

            var start = MobileUI.BottomRow(page, "StartAction", MobileUI.BottomMargin, MobileUI.ButtonHeight);
            startButton = MobileUI.BigButton(start, "StartButton", "LANCER LA PARTIE  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
            Refresh();
        }

        static void SectionLabel(RectTransform page, string text, float top)
        {
            var row = MobileUI.Row(page, "Label" + text, top, 50f);
            MobileUI.Text(row, "Text", text, 36f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.MidlineLeft);
        }

        static TMP_InputField NameRow(RectTransform page, int player, float top)
        {
            var row = MobileUI.Row(page, "Player" + player, top, 145f);
            MobileUI.Badge(row, "Badge", "J" + player, MobileUI.PlayerColor(player), 130f, 52f);

            var field = UIFactory.CreateRect("NameField", row, Vector2.zero, Vector2.one, new Vector2(160f, 0f), Vector2.zero);
            var bg = UIFactory.AddRounded(field, SoundCopyTheme.Cream, 36f, true);
            UIFactory.AddNativeOutline(field, MobileUI.PlayerColor(player), 5f);
            var input = field.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 14;
            var area = UIFactory.CreateRect("TextArea", field.transform, Vector2.zero, Vector2.one, new Vector2(36f, 10f), new Vector2(-36f, -10f));
            area.AddComponent<RectMask2D>();
            var placeholder = MobileUI.Text(area.transform, "Placeholder", "Prénom du joueur " + player, 44f, SoundCopyTheme.MutedGray, TextAlignmentOptions.MidlineLeft);
            var text = MobileUI.Text(area.transform, "Text", "", 50f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft);
            input.textViewport = (RectTransform)area.transform;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.caretColor = SoundCopyTheme.Pink;
            input.caretWidth = 4;
            input.selectionColor = new Color(SoundCopyTheme.Lime.r, SoundCopyTheme.Lime.g, SoundCopyTheme.Lime.b, 0.5f);
            if (MobileUI.BodyFont != null) input.fontAsset = MobileUI.BodyFont;
            input.pointSize = 50f;
            return input;
        }
    }
}
