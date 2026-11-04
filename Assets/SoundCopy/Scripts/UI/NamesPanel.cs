using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class NamesPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_InputField player1Input;
        [SerializeField] private TMP_InputField player2Input;
        [SerializeField] private Button oneButton;
        [SerializeField] private Button threeButton;
        [SerializeField] private Button fiveButton;
        [SerializeField] private RoundedRectGraphic oneGraphic;
        [SerializeField] private RoundedRectGraphic threeGraphic;
        [SerializeField] private RoundedRectGraphic fiveGraphic;
        [SerializeField] private TextMeshProUGUI oneNumber;
        [SerializeField] private TextMeshProUGUI threeNumber;
        [SerializeField] private TextMeshProUGUI fiveNumber;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button backButton;
        [SerializeField] private int selectedRounds = 3;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            continueButton?.onClick.AddListener(Continue);
            backButton?.onClick.AddListener(Back);
            oneButton?.onClick.AddListener(SelectOne);
            threeButton?.onClick.AddListener(SelectThree);
            fiveButton?.onClick.AddListener(SelectFive);
            UpdateRoundSelection();
        }

        private void OnDestroy()
        {
            continueButton?.onClick.RemoveListener(Continue);
            backButton?.onClick.RemoveListener(Back);
            oneButton?.onClick.RemoveListener(SelectOne);
            threeButton?.onClick.RemoveListener(SelectThree);
            fiveButton?.onClick.RemoveListener(SelectFive);
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            GameObject shell = ScreenScaffold.Build(transform, "ÉTAPE  1 / 2", "Entrez en scène.", "Deux pseudos. Un seul téléphone. Le micro va souffrir.");

            GameObject cards = UIFactory.CreateRect("PlayerCards", shell.transform, new Vector2(0.075f, 0.43f), new Vector2(0.925f, 0.66f), Vector2.zero, Vector2.zero);
            player1Input = BuildPlayerCard(cards.transform, true, "J1", "JOUEUR 1  ·  OUVRE LE BAL", "Pseudo du joueur 1");
            player2Input = BuildPlayerCard(cards.transform, false, "J2", "JOUEUR 2  ·  RÉPOND", "Pseudo du joueur 2");

            TextMeshProUGUI roundLabel = UIFactory.CreateText("RoundLabel", shell.transform, "DURÉE DU MATCH", 19f, SoundCopyTheme.MutedGray, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            roundLabel.characterSpacing = 5f;
            roundLabel.rectTransform.anchorMin = new Vector2(0.075f, 0.365f);
            roundLabel.rectTransform.anchorMax = new Vector2(0.925f, 0.415f);
            roundLabel.rectTransform.offsetMin = Vector2.zero;
            roundLabel.rectTransform.offsetMax = Vector2.zero;

            GameObject rounds = UIFactory.CreateRect("RoundSelector", shell.transform, new Vector2(0.075f, 0.255f), new Vector2(0.925f, 0.36f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(rounds, new Color(0.93f, 0.91f, 0.87f), 30f);
            UIFactory.AddNativeOutline(rounds, new Color(0.6f, 0.57f, 0.55f, 0.45f), 2f);
            oneButton = BuildRoundChoice(rounds.transform, "One", new Vector2(0.015f, 0.08f), new Vector2(0.325f, 0.92f), "1", out oneGraphic, out oneNumber);
            threeButton = BuildRoundChoice(rounds.transform, "Three", new Vector2(0.345f, 0.08f), new Vector2(0.655f, 0.92f), "3", out threeGraphic, out threeNumber);
            fiveButton = BuildRoundChoice(rounds.transform, "Five", new Vector2(0.675f, 0.08f), new Vector2(0.985f, 0.92f), "5", out fiveGraphic, out fiveNumber);

            GameObject note = UIFactory.CreateRect("LocalNote", shell.transform, new Vector2(0.075f, 0.185f), new Vector2(0.925f, 0.24f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(note, new Color(0.91f, 1f, 0.70f), 24f);
            UIFactory.CreateText("Text", note.transform, "⚡  L’IA vote automatiquement après chaque duel.", 20f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject action = UIFactory.CreateRect("ContinueAction", shell.transform, new Vector2(0.075f, 0.09f), new Vector2(0.925f, 0.16f), Vector2.zero, Vector2.zero);
            continueButton = UIFactory.CreateButton("ContinueButton", action.transform, "CHOISIR LES SONS  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 31f, out _);
            GameObject back = UIFactory.CreateRect("BackAction", shell.transform, new Vector2(0.075f, 0.025f), new Vector2(0.28f, 0.075f), Vector2.zero, Vector2.zero);
            backButton = UIFactory.CreateButton("BackButton", back.transform, "< RETOUR", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 21f, out _, false);
            UpdateRoundSelection();
        }

        private TMP_InputField BuildPlayerCard(Transform parent, bool first, string initials, string label, string placeholder)
        {
            float y0 = first ? 0.54f : 0f;
            float y1 = first ? 1f : 0.46f;
            GameObject card = UIFactory.CreateRect(first ? "Player1Card" : "Player2Card", parent, new Vector2(0f, y0), new Vector2(1f, y1), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(card, first ? SoundCopyTheme.Lime : SoundCopyTheme.Coral, 30f);
            UIFactory.AddNativeOutline(card, SoundCopyTheme.Ink, 3f);
            Shadow shadow = card.AddComponent<Shadow>();
            shadow.effectColor = SoundCopyTheme.Ink;
            shadow.effectDistance = new Vector2(7f, -8f);
            shadow.useGraphicAlpha = true;
            GameObject badge = UIFactory.CreateRect("Badge", card.transform, new Vector2(0.04f, 0.18f), new Vector2(0.17f, 0.82f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(badge, first ? SoundCopyTheme.Lime : SoundCopyTheme.Coral, 60f);
            UIFactory.AddNativeOutline(badge, SoundCopyTheme.Ink, 3f);
            UIFactory.CreateText("Initials", badge.transform, initials, 27f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI title = UIFactory.CreateText("Label", card.transform, label, 17f, SoundCopyTheme.Ink, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            title.characterSpacing = 3f;
            title.rectTransform.anchorMin = new Vector2(0.21f, 0.56f);
            title.rectTransform.anchorMax = new Vector2(0.94f, 0.82f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;
            return UIFactory.CreateInputField("NameInput", card.transform, placeholder, new Vector2(0.20f, 0.12f), new Vector2(0.94f, 0.57f));
        }

        private Button BuildRoundChoice(Transform parent, string name, Vector2 min, Vector2 max, string number, out RoundedRectGraphic graphic, out TextMeshProUGUI numberText)
        {
            GameObject card = UIFactory.CreateRect(name, parent, min, max, Vector2.zero, Vector2.zero);
            graphic = UIFactory.AddRounded(card, Color.clear, 24f, true);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = graphic;
            numberText = UIFactory.CreateText("Number", card.transform, number, 42f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);
            numberText.rectTransform.anchorMin = new Vector2(0f, 0.20f);
            numberText.rectTransform.anchorMax = new Vector2(1f, 0.90f);
            numberText.rectTransform.offsetMin = Vector2.zero;
            numberText.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI caption = UIFactory.CreateText("Caption", card.transform, "ROUNDS", 14f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);
            caption.characterSpacing = 4f;
            caption.rectTransform.anchorMin = new Vector2(0f, 0.02f);
            caption.rectTransform.anchorMax = new Vector2(1f, 0.30f);
            caption.rectTransform.offsetMin = Vector2.zero;
            caption.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private void SelectOne() { selectedRounds = 1; UpdateRoundSelection(); }
        private void SelectThree() { selectedRounds = 3; UpdateRoundSelection(); }
        private void SelectFive() { selectedRounds = 5; UpdateRoundSelection(); }

        private void UpdateRoundSelection()
        {
            SetRoundStyle(oneGraphic, oneNumber, selectedRounds == 1);
            SetRoundStyle(threeGraphic, threeNumber, selectedRounds == 3);
            SetRoundStyle(fiveGraphic, fiveNumber, selectedRounds == 5);
        }

        private static void SetRoundStyle(RoundedRectGraphic graphic, TextMeshProUGUI number, bool selected)
        {
            if (graphic != null) graphic.color = selected ? SoundCopyTheme.Lime : Color.clear;
            if (number != null) number.color = SoundCopyTheme.Ink;
        }

        private void Continue() => gameManager?.OnSelectionContinue(player1Input?.text, player2Input?.text, selectedRounds);
        private void Back() => gameManager?.OnNamesBack();
    }
}
