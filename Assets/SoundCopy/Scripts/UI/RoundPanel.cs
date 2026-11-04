using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Start of a round: scoreboard, jokers and the "listen" button.
    // Joker: tap your joker card, then shake the phone N times. If you win this round, your points count double.
    public sealed class RoundPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI[] nameTexts = new TextMeshProUGUI[2];
        [SerializeField] private TextMeshProUGUI[] pointTexts = new TextMeshProUGUI[2];
        [SerializeField] private Button[] jokerButtons = new Button[2];
        [SerializeField] private RoundedRectGraphic[] jokerGraphics = new RoundedRectGraphic[2];
        [SerializeField] private TextMeshProUGUI[] jokerTitles = new TextMeshProUGUI[2];
        [SerializeField] private TextMeshProUGUI[] jokerStatus = new TextMeshProUGUI[2];
        [SerializeField] private RoundedRectGraphic[] jokerPips = new RoundedRectGraphic[6];
        [SerializeField] private Button watchButton;

        static readonly Color CardIdle = new Color(0.17f, 0.11f, 0.21f);

        int listeningPlayer;   // 0 = nobody is shaking for a joker
        int shakeCount;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            for (int i = 0; i < 2; i++)
            {
                int player = i + 1;
                if (jokerButtons[i] != null) jokerButtons[i].onClick.AddListener(() => ToggleJoker(player));
            }
            watchButton?.onClick.AddListener(() => gameManager?.OnWatchVideo());
        }

        private void OnEnable()
        {
            listeningPlayer = 0;
            if (gameManager != null && gameManager.Shake != null) gameManager.Shake.Shaken += OnShake;
            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager != null && gameManager.Shake != null) gameManager.Shake.Shaken -= OnShake;
            listeningPlayer = 0;
        }

        public void Refresh()
        {
            if (gameManager == null) return;
            if (titleText != null) titleText.text = $"MANCHE {gameManager.CurrentRound} / {gameManager.RoundCount}";
            if (categoryText != null) categoryText.text = gameManager.CategoryLabel;
            for (int i = 0; i < 2; i++)
            {
                int player = i + 1;
                if (nameTexts[i] != null) nameTexts[i].text = gameManager.PlayerName(player);
                if (pointTexts[i] != null) pointTexts[i].text = gameManager.Points(player).ToString();
                if (jokerTitles[i] != null) jokerTitles[i].text = "JOKER  " + gameManager.PlayerName(player);
                RefreshJoker(player);
            }
        }

        void RefreshJoker(int player)
        {
            int i = player - 1;
            bool active = gameManager.JokerActive(player);
            bool used = gameManager.JokerUsed(player);
            bool listening = listeningPlayer == player;

            string status = active ? "ACTIF : POINTS x2 !"
                : used ? "Déjà utilisé"
                : listening ? $"SECOUE LE TÉLÉPHONE !  {shakeCount}/{gameManager.ShakesForJoker}"
                : "Touche ici puis secoue";
            if (jokerStatus[i] != null)
            {
                jokerStatus[i].text = status;
                jokerStatus[i].color = active || listening ? SoundCopyTheme.Ink : new Color(1f, 1f, 1f, used ? 0.4f : 0.75f);
            }
            if (jokerTitles[i] != null) jokerTitles[i].color = active || listening ? SoundCopyTheme.Ink : SoundCopyTheme.Cream;
            if (jokerGraphics[i] != null)
                jokerGraphics[i].color = active ? MobileUI.PlayerColor(player) : listening ? SoundCopyTheme.PaleBonus : CardIdle;
            if (jokerButtons[i] != null) jokerButtons[i].interactable = !used;

            for (int p = 0; p < 3; p++)
            {
                var pip = jokerPips[i * 3 + p];
                if (pip == null) continue;
                pip.gameObject.SetActive(listening || active);
                pip.color = active || p < shakeCount && listening ? SoundCopyTheme.Ink : new Color(0f, 0f, 0f, 0.2f);
            }
        }

        void ToggleJoker(int player)
        {
            if (gameManager == null || gameManager.JokerUsed(player)) return;
            int previous = listeningPlayer;
            listeningPlayer = listeningPlayer == player ? 0 : player;
            shakeCount = 0;
            if (previous != 0) RefreshJoker(previous);
            RefreshJoker(player);
        }

        void OnShake()
        {
            if (listeningPlayer == 0 || gameManager == null) return;
            shakeCount++;
            Haptics.Pulse(60, 160);
            int player = listeningPlayer;
            if (shakeCount >= gameManager.ShakesForJoker)
            {
                listeningPlayer = 0;
                shakeCount = 0;
                gameManager.ArmJoker(player);
            }
            RefreshJoker(player);
        }

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            MobileUI.TopBar(page, "MANCHE 1 / 3", false, SoundCopyTheme.Cream, out titleText, out _);

            var pillRow = MobileUI.Row(page, "Category", 190f, 76f, 290f);
            MobileUI.Card(pillRow.gameObject, SoundCopyTheme.Cream, 38f);
            categoryText = MobileUI.Text(pillRow, "Label", "INTERNATIONAL", 38f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);

            var board = MobileUI.Row(page, "Scoreboard", 310f, 360f);
            for (int i = 0; i < 2; i++)
            {
                var cell = MobileUI.Cell(board, "Player" + (i + 1), i, 2, 32f);
                MobileUI.Card(cell.gameObject, MobileUI.PlayerColor(i + 1), 44f);
                nameTexts[i] = MobileUI.Text(cell, "Name", "Joueur", 50f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
                nameTexts[i].rectTransform.anchorMin = new Vector2(0f, 0.72f);
                nameTexts[i].rectTransform.offsetMin = new Vector2(20f, 0f);
                nameTexts[i].rectTransform.offsetMax = new Vector2(-20f, -16f);
                nameTexts[i].enableAutoSizing = true;
                nameTexts[i].fontSizeMin = 34f;
                nameTexts[i].fontSizeMax = 50f;
                pointTexts[i] = MobileUI.Text(cell, "Points", "0", 170f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
                pointTexts[i].rectTransform.anchorMin = new Vector2(0f, 0.18f);
                pointTexts[i].rectTransform.anchorMax = new Vector2(1f, 0.74f);
                var label = MobileUI.Text(cell, "Label", "POINTS", 32f, SoundCopyTheme.Ink, TextAlignmentOptions.Center);
                label.rectTransform.anchorMax = new Vector2(1f, 0.2f);
                label.rectTransform.offsetMin = new Vector2(0f, 14f);
            }

            var jokerLabel = MobileUI.Row(page, "JokerLabel", 720f, 56f);
            MobileUI.Text(jokerLabel, "Text", "JOKERS  ·  POINTS x2", 38f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.MidlineLeft);
            var jokers = MobileUI.Row(page, "Jokers", 790f, 300f);
            for (int i = 0; i < 2; i++)
            {
                var cell = MobileUI.Cell(jokers, "Joker" + (i + 1), i, 2, 32f);
                jokerGraphics[i] = MobileUI.Card(cell.gameObject, CardIdle, 40f, true, true);
                UIFactory.AddNativeOutline(cell.gameObject, MobileUI.PlayerColor(i + 1), 5f);
                jokerButtons[i] = cell.gameObject.AddComponent<Button>();
                jokerButtons[i].targetGraphic = jokerGraphics[i];

                jokerTitles[i] = MobileUI.Text(cell, "Title", "JOKER", 44f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
                jokerTitles[i].rectTransform.anchorMin = new Vector2(0f, 0.66f);
                jokerTitles[i].rectTransform.offsetMin = new Vector2(16f, 0f);
                jokerTitles[i].rectTransform.offsetMax = new Vector2(-16f, -18f);
                jokerTitles[i].enableAutoSizing = true;
                jokerTitles[i].fontSizeMin = 30f;
                jokerTitles[i].fontSizeMax = 44f;

                jokerStatus[i] = MobileUI.Text(cell, "Status", "Touche ici puis secoue", 36f, SoundCopyTheme.Cream, TextAlignmentOptions.Center);
                jokerStatus[i].rectTransform.anchorMin = new Vector2(0f, 0.30f);
                jokerStatus[i].rectTransform.anchorMax = new Vector2(1f, 0.66f);
                jokerStatus[i].rectTransform.offsetMin = new Vector2(20f, 0f);
                jokerStatus[i].rectTransform.offsetMax = new Vector2(-20f, 0f);
                jokerStatus[i].textWrappingMode = TextWrappingModes.Normal;

                for (int p = 0; p < 3; p++)
                {
                    var pip = UIFactory.CreateRect("Pip" + p, cell, new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.14f), Vector2.zero, Vector2.zero);
                    var rt = (RectTransform)pip.transform;
                    rt.sizeDelta = new Vector2(44f, 44f);
                    rt.anchoredPosition = new Vector2((p - 1) * 70f, 0f);
                    jokerPips[i * 3 + p] = UIFactory.AddRounded(pip, new Color(0f, 0f, 0f, 0.2f), 22f);
                    pip.SetActive(false);
                }
            }

            var hint = MobileUI.Row(page, "JokerHint", 1120f, 130f);
            var hintText = MobileUI.Text(hint, "Text", "Un joker par joueur et par partie.\nS'il gagne la manche, ses points comptent double.", 34f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            hintText.textWrappingMode = TextWrappingModes.Normal;

            var watch = MobileUI.BottomRow(page, "WatchAction", MobileUI.BottomMargin, MobileUI.ButtonHeight);
            watchButton = MobileUI.BigButton(watch, "WatchButton", "ÉCOUTER LE SON  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
        }
    }
}
