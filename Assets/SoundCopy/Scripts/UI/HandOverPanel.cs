using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Full-screen "pass the phone to X" between the two players, in that player's colour.
    public sealed class HandOverPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private RoundedRectGraphic background;
        [SerializeField] private TextMeshProUGUI badgeText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private GameObject jokerBadge;
        [SerializeField] private Button readyButton;
        private int player = 1;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake() => readyButton?.onClick.AddListener(() => gameManager?.OnHandOverReady(player));

        public void Prepare(int playerIndex, string playerName, bool jokerActive)
        {
            player = playerIndex;
            if (background != null) background.color = MobileUI.PlayerColor(playerIndex);
            if (badgeText != null) badgeText.text = "J" + playerIndex;
            if (nameText != null) nameText.text = playerName.ToUpperInvariant();
            if (jokerBadge != null) jokerBadge.SetActive(jokerActive);
            Haptics.Pulse(80, 150);
        }

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.Lime, false);
            background = page.Find("Background").GetComponent<RoundedRectGraphic>();

            // Phone icon with the player badge on its screen.
            var phone = MobileUI.Row(page, "PhoneIcon", 230f, 520f, 370f);
            MobileUI.Card(phone.gameObject, SoundCopyTheme.Ink, 56f, false);
            var screen = UIFactory.CreateRect("Screen", phone, Vector2.zero, Vector2.one, new Vector2(22f, 60f), new Vector2(-22f, -60f));
            UIFactory.AddRounded(screen, SoundCopyTheme.Cream, 30f);
            badgeText = MobileUI.Text(screen.transform, "Badge", "J1", 130f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            var speaker = UIFactory.CreateRect("Speaker", phone, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            ((RectTransform)speaker.transform).sizeDelta = new Vector2(90f, 14f);
            ((RectTransform)speaker.transform).anchoredPosition = new Vector2(0f, -30f);
            UIFactory.AddRounded(speaker, SoundCopyTheme.Cream, 7f);

            var label = MobileUI.Row(page, "Label", 820f, 80f);
            MobileUI.Text(label, "Text", "PASSE LE TÉLÉPHONE À", 54f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            var name = MobileUI.Row(page, "Name", 910f, 220f);
            nameText = MobileUI.Text(name, "Text", "JOUEUR", 150f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 70f;
            nameText.fontSizeMax = 150f;

            var joker = MobileUI.Row(page, "JokerBadge", 1160f, 100f, 250f);
            MobileUI.Card(joker.gameObject, SoundCopyTheme.Ink, 50f, false);
            MobileUI.Text(joker, "Text", "JOKER ACTIF  ·  x2", 46f, SoundCopyTheme.PaleBonus, TextAlignmentOptions.Center, true);
            jokerBadge = joker.gameObject;
            jokerBadge.SetActive(false);

            var tip = MobileUI.Row(page, "Tip", 1300f, 120f);
            var tipText = MobileUI.Text(tip, "Text", "Les autres : silence pendant l'imitation !\nTiens le micro près de ta bouche.", 38f, SoundCopyTheme.Ink, TextAlignmentOptions.Center);
            tipText.textWrappingMode = TextWrappingModes.Normal;

            var ready = MobileUI.BottomRow(page, "ReadyAction", MobileUI.BottomMargin, MobileUI.ButtonHeight + 10f);
            readyButton = MobileUI.BigButton(ready, "ReadyButton", "JE SUIS PRÊT  >", SoundCopyTheme.Ink, SoundCopyTheme.Cream, 58f);
        }
    }
}
