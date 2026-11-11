using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Start of a round: scoreboard, then the button that goes to "spin the phone" (and then the video).
    // The old joker cards (if still in the scene) are hidden; delete them from the scene whenever you like.
    public sealed class RoundPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI[] nameTexts = new TextMeshProUGUI[2];
        [SerializeField] private TextMeshProUGUI[] pointTexts = new TextMeshProUGUI[2];
        [Tooltip("Old joker cards: hidden at runtime (the joker was replaced by spinning the phone).")]
        [SerializeField] private Button[] jokerButtons = new Button[2];
        [SerializeField] private Button watchButton;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            watchButton?.onClick.AddListener(() => gameManager?.OnWatchVideo());
            HideOldJokerUI();
        }

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            if (gameManager == null) return;
            if (titleText != null) titleText.text = gameManager.RoundCount <= 1 ? "THE ROUND" : $"ROUND {gameManager.CurrentRound} / {gameManager.RoundCount}";
            if (categoryText != null) categoryText.text = gameManager.CategoryLabel;
            for (int i = 0; i < 2; i++)
            {
                int player = i + 1;
                if (nameTexts[i] != null) nameTexts[i].text = gameManager.PlayerName(player);
                if (pointTexts[i] != null) pointTexts[i].text = gameManager.Points(player).ToString();
            }
        }

        void HideOldJokerUI()
        {
            foreach (var button in jokerButtons)
            {
                if (button == null) continue;
                button.gameObject.SetActive(false);
                if (button.transform.parent != null && button.transform.parent.name == "Jokers")
                    button.transform.parent.gameObject.SetActive(false);
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "JokerLabel" || t.name == "JokerHint" || t.name == "Jokers") t.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            MobileUI.TopBar(page, "ROUND 1 / 3", false, SoundCopyTheme.Cream, out titleText, out _);

            // Left column: category + scoreboard.
            var pill = MobileUI.ColRow(page, "Category", 0f, 0.5f, 170f, 70f);
            MobileUI.Card(pill.gameObject, SoundCopyTheme.Cream, 35f);
            categoryText = MobileUI.Text(pill, "Label", "INTERNATIONAL", 36f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);

            var board = MobileUI.ColRow(page, "Scoreboard", 0f, 0.5f, 270f, 420f);
            for (int i = 0; i < 2; i++)
            {
                var cell = MobileUI.Cell(board, "Player" + (i + 1), i, 2, 32f);
                MobileUI.Card(cell.gameObject, MobileUI.PlayerColor(i + 1), 44f);
                nameTexts[i] = MobileUI.Text(cell, "Name", "Player", 48f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
                nameTexts[i].rectTransform.anchorMin = new Vector2(0f, 0.74f);
                nameTexts[i].rectTransform.offsetMin = new Vector2(20f, 0f);
                nameTexts[i].rectTransform.offsetMax = new Vector2(-20f, -14f);
                nameTexts[i].enableAutoSizing = true;
                nameTexts[i].fontSizeMin = 32f;
                nameTexts[i].fontSizeMax = 48f;
                pointTexts[i] = MobileUI.Text(cell, "Points", "0", 190f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
                pointTexts[i].rectTransform.anchorMin = new Vector2(0f, 0.18f);
                pointTexts[i].rectTransform.anchorMax = new Vector2(1f, 0.76f);
                var label = MobileUI.Text(cell, "Label", "POINTS", 32f, SoundCopyTheme.Ink, TextAlignmentOptions.Center);
                label.rectTransform.anchorMax = new Vector2(1f, 0.2f);
                label.rectTransform.offsetMin = new Vector2(0f, 12f);
            }

            // Right column: what happens next (spin the phone).
            var spinLabel = MobileUI.ColRow(page, "SpinLabel", 0.5f, 1f, 170f, 70f);
            MobileUI.Text(spinLabel, "Text", "NEXT: SPIN THE PHONE", 38f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.MidlineLeft);
            var spinHint = MobileUI.ColRow(page, "SpinHint", 0.5f, 1f, 270f, 300f);
            var spinHintText = MobileUI.Text(spinHint, "Text",
                "Put the phone flat on the table between you and spin it.\nWhere its top points picks who goes first.\nSpin fast for a <color=#FFF1B8>x2 round</color>!",
                38f, SoundCopyTheme.Cream, TextAlignmentOptions.TopLeft);
            spinHintText.textWrappingMode = TextWrappingModes.Normal;

            var watch = MobileUI.BottomRight(page, "WatchAction");
            watchButton = MobileUI.BigButton(watch, "WatchButton", "SPIN THE PHONE  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
        }
    }
}
