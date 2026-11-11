using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Full-screen "pass the phone to X" between the two players, in that player's colour.
    public sealed class HandOverPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI badgeText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private GameObject jokerBadge;
        [SerializeField] private Button readyButton;
        private int player = 1;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake() => readyButton?.onClick.AddListener(() => gameManager?.OnHandOverReady(player));

        // doubleRound: the spin made this a x2 round (the badge says so).
        public void Prepare(int playerIndex, string playerName, bool doubleRound)
        {
            player = playerIndex;
            if (background != null) background.color = MobileUI.PlayerColor(playerIndex);
            if (badgeText != null) badgeText.text = "P" + playerIndex;
            if (nameText != null) nameText.text = playerName.ToUpperInvariant();
            if (jokerBadge != null)
            {
                jokerBadge.SetActive(doubleRound);
                var label = jokerBadge.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "x2 ROUND";
            }
            Haptics.Pulse(80, 150);
        }

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.Lime, false);
            background = page.Find("Background").GetComponent<Image>();

            // Left: a phone held sideways, with the player badge on its screen.
            var phone = MobileUI.TopLeft(page, "PhoneIcon", 110f, 300f, 600f, 340f);
            MobileUI.Card(phone.gameObject, SoundCopyTheme.Ink, 56f, false);
            var screen = UIFactory.CreateRect("Screen", phone, Vector2.zero, Vector2.one, new Vector2(70f, 24f), new Vector2(-70f, -24f));
            UIFactory.AddRounded(screen, SoundCopyTheme.Cream, 28f);
            badgeText = MobileUI.Text(screen.transform, "Badge", "P1", 150f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            var speaker = UIFactory.CreateRect("Speaker", phone, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            ((RectTransform)speaker.transform).sizeDelta = new Vector2(14f, 90f);
            ((RectTransform)speaker.transform).anchoredPosition = new Vector2(34f, 0f);
            UIFactory.AddRounded(speaker, SoundCopyTheme.Cream, 7f);

            // Right: who plays now.
            var side = UIFactory.CreateRect("Side", page, Vector2.zero, Vector2.one, new Vector2(800f, 0f), Vector2.zero);
            var label = MobileUI.Row(side.transform, "Label", 190f, 80f, 0f);
            MobileUI.Text(label, "Text", "PASS THE PHONE TO", 54f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft, true);
            var name = MobileUI.Row(side.transform, "Name", 280f, 220f, 0f);
            nameText = MobileUI.Text(name, "Text", "PLAYER", 170f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft, true);
            nameText.rectTransform.offsetMax = new Vector2(-MobileUI.Side, 0f);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 70f;
            nameText.fontSizeMax = 170f;

            var joker = MobileUI.TopLeft(side.transform, "JokerBadge", 0f, 520f, 560f, 90f);
            MobileUI.Card(joker.gameObject, SoundCopyTheme.Ink, 45f, false);
            MobileUI.Text(joker, "Text", "x2 ROUND", 44f, SoundCopyTheme.PaleBonus, TextAlignmentOptions.Center, true);
            jokerBadge = joker.gameObject;
            jokerBadge.SetActive(false);

            var tip = MobileUI.Row(side.transform, "Tip", 640f, 110f, 0f);
            var tipText = MobileUI.Text(tip, "Text", "Everyone else: quiet during the imitation!\nHold the mic close to your mouth.", 36f, SoundCopyTheme.Ink, TextAlignmentOptions.TopLeft);
            tipText.rectTransform.offsetMax = new Vector2(-MobileUI.Side, 0f);
            tipText.textWrappingMode = TextWrappingModes.Normal;

            var ready = MobileUI.BottomRight(page, "ReadyAction");
            readyButton = MobileUI.BigButton(ready, "ReadyButton", "I'M READY  >", SoundCopyTheme.Ink, SoundCopyTheme.Cream, 56f);
        }
    }
}
