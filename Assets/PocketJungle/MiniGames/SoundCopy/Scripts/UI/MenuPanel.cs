using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class MenuPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Sprite posterBackground;
        [SerializeField] private TMP_FontAsset displayFont;
        [SerializeField] private TMP_FontAsset bodyFont;
        [SerializeField] private Button playButton;
        [SerializeField] private Button rulesButton;
        [SerializeField] private Button launchLabelButton;
        [SerializeField] private Button closeRulesButton;
        [Tooltip("Back to the big game's level menu; only shown when Sound Copy runs inside the big game.")]
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject rulesOverlay;
        [SerializeField] private RectTransform playPulse;
        [SerializeField] private RectTransform launchPulse;
        [SerializeField] private RectTransform firstPlayerLetter;
        [SerializeField] private RectTransform secondPlayerLetter;
        private float animationTime;

        public void Initialize(GameManager manager, TMP_FontAsset display = null, TMP_FontAsset body = null, Sprite background = null)
        {
            gameManager = manager;
            displayFont = display;
            bodyFont = body;
            posterBackground = background;
        }

        private void Awake() => Bind();
        private void Start()
        {
            Bind();
            // Inside Pocket Jungle the pause menu is the way out (no exit button on the screens).
            if (exitButton != null) exitButton.gameObject.SetActive(false);
        }
        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (rulesOverlay != null && rulesOverlay.activeSelf) return;
            animationTime += Time.unscaledDeltaTime;
            float playScale = 1f + Mathf.Sin(animationTime * 3.2f) * 0.035f;
            if (playPulse != null) playPulse.localScale = new Vector3(playScale, playScale, 1f);
            float labelScale = 1f + Mathf.Sin(animationTime * 2.1f + 1.1f) * 0.012f;
            if (launchPulse != null) launchPulse.localScale = new Vector3(labelScale, labelScale, 1f);
            if (firstPlayerLetter != null)
                firstPlayerLetter.localRotation = Quaternion.Euler(0f, 0f, -9f + Mathf.Sin(animationTime * 1.4f) * 1.4f);
            if (secondPlayerLetter != null)
                secondPlayerLetter.localRotation = Quaternion.Euler(0f, 0f, 8f - Mathf.Sin(animationTime * 1.4f) * 1.4f);
        }

        private void Bind()
        {
            Unbind();
            playButton?.onClick.AddListener(Play);
            launchLabelButton?.onClick.AddListener(Play);
            rulesButton?.onClick.AddListener(ShowRules);
            closeRulesButton?.onClick.AddListener(HideRules);
            exitButton?.onClick.AddListener(Exit);
        }

        private void Unbind()
        {
            playButton?.onClick.RemoveListener(Play);
            launchLabelButton?.onClick.RemoveListener(Play);
            rulesButton?.onClick.RemoveListener(ShowRules);
            closeRulesButton?.onClick.RemoveListener(HideRules);
            exitButton?.onClick.RemoveListener(Exit);
        }

        public void BuildUI()
        {
            ClearChildren();
            BuildPosterBackground();
            BuildLogo();
            BuildHeadline();
            BuildPlayerLetters();
            BuildPlayAction();
            BuildFooter();
            BuildExitButton();
            BuildRulesOverlay();
            ApplyFonts();
        }

        // Landscape "battle poster": headline on the left, play button in the centre, giant J1 / J2 on the right.
        private void BuildPosterBackground()
        {
            GameObject background = UIFactory.CreateRect("BattlePosterBackground", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image image = background.AddComponent<Image>();
            image.sprite = posterBackground;
            image.color = Color.white;
            image.raycastTarget = false;
            // The poster art is portrait: cover the whole landscape screen without stretching it.
            AspectRatioFitter fitter = background.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = posterBackground != null ? posterBackground.rect.width / posterBackground.rect.height : 0.45f;

            GameObject shade = UIFactory.CreateRect("Shade", transform, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
            UIFactory.AddRounded(shade, new Color(0.10f, 0.06f, 0.13f, 0.35f), 0f);
        }

        private void BuildLogo()
        {
            GameObject logo = UIFactory.CreateRect("Logo", transform, new Vector2(0.10f, 0.855f), new Vector2(0.36f, 0.965f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI text = UIFactory.CreateText("LogoText", logo.transform, "SOUND\nCOPY", 48f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            text.lineSpacing = -20f;
            GameObject leftWave = UIFactory.CreateRect("LeftWave", logo.transform, new Vector2(0f, 0.15f), new Vector2(0.24f, 0.85f), Vector2.zero, Vector2.zero);
            GameObject rightWave = UIFactory.CreateRect("RightWave", logo.transform, new Vector2(0.76f, 0.15f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero);
            BuildMiniWave(leftWave.transform);
            BuildMiniWave(rightWave.transform);
        }

        private static void BuildMiniWave(Transform parent)
        {
            float[] heights = { .25f, .55f, .90f, .55f, .25f };
            for (int i = 0; i < heights.Length; i++)
            {
                float width = 1f / heights.Length;
                float x = i * width + width * .34f;
                GameObject bar = UIFactory.CreateRect("Bar" + i, parent, new Vector2(x, .5f - heights[i] * .38f), new Vector2(x + width * .30f, .5f + heights[i] * .38f), Vector2.zero, Vector2.zero);
                UIFactory.AddRounded(bar, SoundCopyTheme.Cream, 5f);
            }
        }

        private void BuildHeadline()
        {
            TextMeshProUGUI copy = UIFactory.CreateText("HeadlineCopy", transform, "COPY", 190f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            SetAnchors(copy.rectTransform, new Vector2(0.03f, 0.58f), new Vector2(0.47f, 0.84f));
            copy.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 4f);

            GameObject bannerShadow = UIFactory.CreateRect("HeadlineBannerShadow", transform, new Vector2(0.06f, 0.40f), new Vector2(0.44f, 0.58f), new Vector2(9f, -11f), new Vector2(9f, -11f));
            UIFactory.AddRounded(bannerShadow, SoundCopyTheme.Ink, 2f);
            bannerShadow.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            GameObject banner = UIFactory.CreateRect("HeadlineBanner", transform, new Vector2(0.06f, 0.40f), new Vector2(0.44f, 0.58f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(banner, SoundCopyTheme.Cream, 2f);
            banner.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            UIFactory.CreateText("HeadlineSound", banner.transform, "THIS SOUND", 140f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);

            TextMeshProUGUI subtitle = UIFactory.CreateText("Subtitle", transform, "Two voices. One point.", 46f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            SetAnchors(subtitle.rectTransform, new Vector2(0.06f, 0.29f), new Vector2(0.44f, 0.38f));
            subtitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
        }

        private void BuildPlayerLetters()
        {
            TextMeshProUGUI firstShadow = CreatePosterLetter("J1Shadow", "P1", new Vector2(0.70f, 0.46f), new Vector2(0.90f, 0.96f), SoundCopyTheme.Lime);
            firstShadow.rectTransform.anchoredPosition = new Vector2(10f, -12f);
            firstPlayerLetter = CreatePosterLetter("P1", "P1", new Vector2(0.70f, 0.46f), new Vector2(0.90f, 0.96f), SoundCopyTheme.DarkPlum).rectTransform;
            TextMeshProUGUI secondShadow = CreatePosterLetter("J2Shadow", "P2", new Vector2(0.80f, 0.10f), new Vector2(1.00f, 0.60f), SoundCopyTheme.Coral);
            secondShadow.rectTransform.anchoredPosition = new Vector2(10f, -12f);
            secondPlayerLetter = CreatePosterLetter("P2", "P2", new Vector2(0.80f, 0.10f), new Vector2(1.00f, 0.60f), SoundCopyTheme.DarkPlum).rectTransform;
        }

        private TextMeshProUGUI CreatePosterLetter(string name, string value, Vector2 min, Vector2 max, Color color)
        {
            TextMeshProUGUI text = UIFactory.CreateText(name, transform, value, 300f, color, TextAlignmentOptions.Center, FontStyles.Bold);
            SetAnchors(text.rectTransform, min, max);
            text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, name.Contains("P1") ? -9f : 8f);
            return text;
        }

        private void BuildPlayAction()
        {
            const float size = 330f;
            Vector2 centre = new Vector2(0.585f, 0.54f);
            GameObject shadow = UIFactory.CreateRect("PlayShadow", transform, centre, centre, Vector2.zero, Vector2.zero);
            ((RectTransform)shadow.transform).sizeDelta = new Vector2(size, size);
            ((RectTransform)shadow.transform).anchoredPosition = new Vector2(10f, -14f);
            UIFactory.AddRounded(shadow, SoundCopyTheme.Ink, size * 0.5f);
            GameObject play = UIFactory.CreateRect("PlayButton", transform, centre, centre, Vector2.zero, Vector2.zero);
            ((RectTransform)play.transform).sizeDelta = new Vector2(size, size);
            playPulse = play.GetComponent<RectTransform>();
            Image graphic = UIFactory.AddRounded(play, SoundCopyTheme.Cream, size * 0.5f, true);
            UIFactory.AddNativeOutline(play, SoundCopyTheme.Ink, 5f);
            playButton = play.AddComponent<Button>();
            playButton.targetGraphic = graphic;
            TextMeshProUGUI triangle = UIFactory.CreateText("PlayIcon", play.transform, ">", 150f, SoundCopyTheme.DarkPlum, TextAlignmentOptions.Center, FontStyles.Bold);
            triangle.rectTransform.anchoredPosition = new Vector2(10f, 0f);

            GameObject launch = UIFactory.CreateRect("LaunchAction", transform, new Vector2(0.45f, 0.14f), new Vector2(0.72f, 0.27f), Vector2.zero, Vector2.zero);
            launchPulse = launch.GetComponent<RectTransform>();
            TextMeshProUGUI label = UIFactory.CreateText("LaunchLabel", launch.transform, "START THE DUEL", 78f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            launchLabelButton = launch.AddComponent<Button>();
            launchLabelButton.targetGraphic = label;
        }

        private void BuildFooter()
        {
            GameObject rules = UIFactory.CreateRect("RulesButton", transform, new Vector2(0.035f, 0.04f), new Vector2(0.22f, 0.14f), Vector2.zero, Vector2.zero);
            Image rulesGraphic = UIFactory.AddRounded(rules, new Color(0f, 0f, 0f, 0.35f), 50f, true);
            UIFactory.AddNativeOutline(rules, SoundCopyTheme.Cream, 3f);
            rulesButton = rules.AddComponent<Button>();
            rulesButton.targetGraphic = rulesGraphic;
            UIFactory.CreateText("Label", rules.transform, "RULES", 40f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI offline = UIFactory.CreateText("Offline", transform, "OFFLINE  ·  2 PLAYERS", 30f, SoundCopyTheme.Cream, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            SetAnchors(offline.rectTransform, new Vector2(0.62f, 0.03f), new Vector2(0.97f, 0.09f));
        }

        private void BuildExitButton()
        {
            GameObject exit = UIFactory.CreateRect("ExitButton", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            RectTransform rt = exit.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(32f, -24f);
            rt.sizeDelta = new Vector2(116f, 116f);
            exitButton = MobileUI.RoundButton(exit.transform, "Button", SoundCopySkin.IconBack, "<", true);
        }

        private void BuildRulesOverlay()
        {
            rulesOverlay = UIFactory.CreateRect("RulesOverlay", transform, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
            UIFactory.AddRounded(rulesOverlay, new Color(0.035f, 0.01f, 0.05f, 0.96f), 0f, true);
            GameObject area = UIFactory.CreateRect("Area", rulesOverlay.transform, Vector2.zero, Vector2.one, new Vector2(200f, 200f), new Vector2(-200f, -200f));

            GameObject cardShadow = UIFactory.CreateRect("RulesShadow", area.transform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.94f), new Vector2(0f, -14f), new Vector2(0f, -14f));
            UIFactory.AddRounded(cardShadow, SoundCopyTheme.Ink, 52f);
            GameObject card = UIFactory.CreateRect("RulesCard", area.transform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.94f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(card, SoundCopyTheme.Cream, 52f);
            UIFactory.AddNativeOutline(card, SoundCopyTheme.Ink, 5f);

            GameObject kicker = UIFactory.CreateRect("Kicker", card.transform, new Vector2(.04f, .86f), new Vector2(.30f, .94f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(kicker, SoundCopyTheme.DarkPlum, 30f);
            UIFactory.CreateText("KickerText", kicker.transform, "DUEL MODE · 2 PLAYERS", 30f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, FontStyles.Bold);

            TextMeshProUGUI title = UIFactory.CreateText("RulesTitle", card.transform, "HOW TO PLAY?", 96f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            SetAnchors(title.rectTransform, new Vector2(.04f, .70f), new Vector2(.96f, .86f));
            TextMeshProUGUI intro = UIFactory.CreateText("RulesIntro", card.transform, "One sound. Two imitations. The AI gives the point.", 36f, SoundCopyTheme.MutedGray, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            SetAnchors(intro.rectTransform, new Vector2(.04f, .62f), new Vector2(.96f, .70f));

            BuildRuleRow(card.transform, 1, "LISTEN", "Watch the video and remember the rhythm.", new Vector2(.04f, .38f), new Vector2(.49f, .59f), SoundCopyTheme.Lime);
            BuildRuleRow(card.transform, 2, "COPY IT", "P1 then P2 copy the sound into the mic.", new Vector2(.51f, .38f), new Vector2(.96f, .59f), SoundCopyTheme.Coral);
            BuildRuleRow(card.transform, 3, "ANALYSIS", "The AI compares both voices with the original sound.", new Vector2(.04f, .15f), new Vector2(.49f, .36f), SoundCopyTheme.Lime);
            BuildRuleRow(card.transform, 4, "POINT", "Best one wins. Spin fast for a x2 round!", new Vector2(.51f, .15f), new Vector2(.96f, .36f), SoundCopyTheme.Coral);

            GameObject close = UIFactory.CreateRect("Close", card.transform, new Vector2(.62f, .025f), new Vector2(.96f, .13f), Vector2.zero, Vector2.zero);
            closeRulesButton = UIFactory.CreateButton("CloseButton", close.transform, "LET'S GO  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 40f, out TextMeshProUGUI closeLabel, false);
            closeLabel.fontSize = 46f;
            rulesOverlay.SetActive(false);
        }

        private static void BuildRuleRow(Transform parent, int index, string heading, string description, Vector2 min, Vector2 max, Color accent)
        {
            GameObject row = UIFactory.CreateRect("Rule" + index, parent, min, max, Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(row, new Color(.965f, .94f, .89f), 28f);
            UIFactory.AddNativeOutline(row, new Color(.25f, .20f, .24f, .28f), 2f);
            GameObject number = UIFactory.CreateRect("Number", row.transform, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, Vector2.zero);
            RectTransform numberRt = number.GetComponent<RectTransform>();
            numberRt.pivot = new Vector2(0f, .5f);
            numberRt.anchoredPosition = new Vector2(24f, 0f);
            numberRt.sizeDelta = new Vector2(110f, 110f);
            UIFactory.AddRounded(number, accent, 55f);
            UIFactory.AddNativeOutline(number, SoundCopyTheme.Ink, 2f);
            UIFactory.CreateText("Value", number.transform, index.ToString("00"), 44f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI headingText = UIFactory.CreateText("Heading", row.transform, heading, 46f, SoundCopyTheme.Ink, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            headingText.rectTransform.anchorMin = new Vector2(0f, .50f);
            headingText.rectTransform.anchorMax = new Vector2(1f, .92f);
            headingText.rectTransform.offsetMin = new Vector2(160f, 0f);
            headingText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            TextMeshProUGUI body = UIFactory.CreateText("Body", row.transform, description, 32f, SoundCopyTheme.MutedGray, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            body.rectTransform.anchorMin = new Vector2(0f, .06f);
            body.rectTransform.anchorMax = new Vector2(1f, .50f);
            body.rectTransform.offsetMin = new Vector2(160f, 0f);
            body.rectTransform.offsetMax = new Vector2(-20f, 0f);
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void ApplyFonts()
        {
            foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                bool display = text.name.StartsWith("Headline") || text.name == "LaunchLabel" || text.name.StartsWith("P1") || text.name.StartsWith("P2") || text.name == "RulesTitle";
                if (display && displayFont != null) text.font = displayFont;
                else if (bodyFont != null) text.font = bodyFont;
            }
        }

        private void Play() => gameManager?.OnPlayButton();
        private void Exit() => gameManager?.ExitToGame();
        private void ShowRules() { rulesOverlay?.SetActive(true); rulesOverlay?.transform.SetAsLastSibling(); }
        private void HideRules() => rulesOverlay?.SetActive(false);
        private void ClearChildren() { for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject); }
    }
}
