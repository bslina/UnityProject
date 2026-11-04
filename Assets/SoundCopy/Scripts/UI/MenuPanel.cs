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
        private void Start() => Bind();
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
        }

        private void Unbind()
        {
            playButton?.onClick.RemoveListener(Play);
            launchLabelButton?.onClick.RemoveListener(Play);
            rulesButton?.onClick.RemoveListener(ShowRules);
            closeRulesButton?.onClick.RemoveListener(HideRules);
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
            BuildRulesOverlay();
            ApplyFonts();
        }

        private void BuildPosterBackground()
        {
            GameObject background = UIFactory.CreateRect("BattlePosterBackground", transform, new Vector2(-0.035f, -0.01f), new Vector2(1.035f, 1.01f), Vector2.zero, Vector2.zero);
            Image image = background.AddComponent<Image>();
            image.sprite = posterBackground;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private void BuildLogo()
        {
            GameObject logo = UIFactory.CreateRect("Logo", transform, new Vector2(0.29f, 0.895f), new Vector2(0.71f, 0.975f), Vector2.zero, Vector2.zero);
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
            TextMeshProUGUI copy = UIFactory.CreateText("HeadlineCopy", transform, "COPIE", 142f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            copy.rectTransform.anchorMin = new Vector2(0.10f, 0.745f);
            copy.rectTransform.anchorMax = new Vector2(0.90f, 0.875f);
            copy.rectTransform.offsetMin = Vector2.zero;
            copy.rectTransform.offsetMax = Vector2.zero;
            copy.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 4f);

            GameObject bannerShadow = UIFactory.CreateRect("HeadlineBannerShadow", transform, new Vector2(0.15f, 0.655f), new Vector2(0.85f, 0.76f), new Vector2(9f, -11f), new Vector2(9f, -11f));
            UIFactory.AddRounded(bannerShadow, SoundCopyTheme.Ink, 2f);
            bannerShadow.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            GameObject banner = UIFactory.CreateRect("HeadlineBanner", transform, new Vector2(0.15f, 0.655f), new Vector2(0.85f, 0.76f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(banner, SoundCopyTheme.Cream, 2f);
            banner.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            UIFactory.CreateText("HeadlineSound", banner.transform, "CE SON", 112f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);

            TextMeshProUGUI subtitle = UIFactory.CreateText("Subtitle", transform, "Deux voix. Un point.", 35f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            subtitle.rectTransform.anchorMin = new Vector2(0.18f, 0.605f);
            subtitle.rectTransform.anchorMax = new Vector2(0.82f, 0.655f);
            subtitle.rectTransform.offsetMin = Vector2.zero;
            subtitle.rectTransform.offsetMax = Vector2.zero;
            subtitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
        }

        private void BuildPlayerLetters()
        {
            TextMeshProUGUI firstShadow = CreatePosterLetter("J1Shadow", "J1", new Vector2(-0.10f, 0.26f), new Vector2(0.42f, 0.59f), SoundCopyTheme.Lime);
            firstShadow.rectTransform.anchoredPosition = new Vector2(10f, -12f);
            firstPlayerLetter = CreatePosterLetter("J1", "J1", new Vector2(-0.10f, 0.26f), new Vector2(0.42f, 0.59f), SoundCopyTheme.DarkPlum).rectTransform;
            TextMeshProUGUI secondShadow = CreatePosterLetter("J2Shadow", "J2", new Vector2(0.60f, 0.19f), new Vector2(1.12f, 0.52f), SoundCopyTheme.Coral);
            secondShadow.rectTransform.anchoredPosition = new Vector2(10f, -12f);
            secondPlayerLetter = CreatePosterLetter("J2", "J2", new Vector2(0.60f, 0.19f), new Vector2(1.12f, 0.52f), SoundCopyTheme.DarkPlum).rectTransform;
        }

        private TextMeshProUGUI CreatePosterLetter(string name, string value, Vector2 min, Vector2 max, Color color)
        {
            TextMeshProUGUI text = UIFactory.CreateText(name, transform, value, 210f, color, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.anchorMin = min;
            text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, name.Contains("J1") ? -9f : 8f);
            return text;
        }

        private void BuildPlayAction()
        {
            GameObject shadow = UIFactory.CreateRect("PlayShadow", transform, new Vector2(0.355f, 0.405f), new Vector2(0.645f, 0.545f), new Vector2(9f, -13f), new Vector2(9f, -13f));
            UIFactory.AddRounded(shadow, SoundCopyTheme.Ink, 300f);
            GameObject play = UIFactory.CreateRect("PlayButton", transform, new Vector2(0.355f, 0.405f), new Vector2(0.645f, 0.545f), Vector2.zero, Vector2.zero);
            playPulse = play.GetComponent<RectTransform>();
            RoundedRectGraphic graphic = UIFactory.AddRounded(play, SoundCopyTheme.Cream, 300f, true);
            UIFactory.AddNativeOutline(play, SoundCopyTheme.Ink, 5f);
            playButton = play.AddComponent<Button>();
            playButton.targetGraphic = graphic;
            TextMeshProUGUI triangle = UIFactory.CreateText("PlayIcon", play.transform, ">", 88f, SoundCopyTheme.DarkPlum, TextAlignmentOptions.Center, FontStyles.Bold);
            triangle.rectTransform.anchoredPosition = new Vector2(8f, 0f);

            GameObject launch = UIFactory.CreateRect("LaunchAction", transform, new Vector2(0.14f, 0.105f), new Vector2(0.86f, 0.18f), Vector2.zero, Vector2.zero);
            launchPulse = launch.GetComponent<RectTransform>();
            TextMeshProUGUI label = UIFactory.CreateText("LaunchLabel", launch.transform, "LANCER LE DUEL", 64f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            launchLabelButton = launch.AddComponent<Button>();
            launchLabelButton.targetGraphic = label;
        }

        private void BuildFooter()
        {
            GameObject rules = UIFactory.CreateRect("RulesButton", transform, new Vector2(0.055f, 0.025f), new Vector2(0.36f, 0.075f), Vector2.zero, Vector2.zero);
            RoundedRectGraphic rulesGraphic = UIFactory.AddRounded(rules, new Color(0f, 0f, 0f, 0f), 28f, true);
            UIFactory.AddNativeOutline(rules, SoundCopyTheme.Cream, 2f);
            rulesButton = rules.AddComponent<Button>();
            rulesButton.targetGraphic = rulesGraphic;
            UIFactory.CreateText("Label", rules.transform, "LES RÈGLES", 31f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI offline = UIFactory.CreateText("Offline", transform, "HORS LIGNE  ·  2 JOUEURS", 23f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            offline.rectTransform.anchorMin = new Vector2(0.48f, 0.025f);
            offline.rectTransform.anchorMax = new Vector2(0.95f, 0.075f);
            offline.rectTransform.offsetMin = Vector2.zero;
            offline.rectTransform.offsetMax = Vector2.zero;
        }

        private void BuildRulesOverlay()
        {
            rulesOverlay = UIFactory.CreateRect("RulesOverlay", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(rulesOverlay, new Color(0.035f, 0.01f, 0.05f, 0.96f), 0f);

            GameObject cardShadow = UIFactory.CreateRect("RulesShadow", rulesOverlay.transform, new Vector2(0.055f, 0.105f), new Vector2(0.945f, 0.895f), new Vector2(0f, -14f), new Vector2(0f, -14f));
            UIFactory.AddRounded(cardShadow, SoundCopyTheme.Ink, 52f);
            GameObject card = UIFactory.CreateRect("RulesCard", rulesOverlay.transform, new Vector2(0.055f, 0.105f), new Vector2(0.945f, 0.895f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(card, SoundCopyTheme.Cream, 52f);
            UIFactory.AddNativeOutline(card, SoundCopyTheme.Ink, 5f);

            GameObject kicker = UIFactory.CreateRect("Kicker", card.transform, new Vector2(.08f, .88f), new Vector2(.43f, .94f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(kicker, SoundCopyTheme.DarkPlum, 22f);
            UIFactory.CreateText("KickerText", kicker.transform, "MODE DUEL · 2 JOUEURS", 24f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, FontStyles.Bold);

            TextMeshProUGUI title = UIFactory.CreateText("RulesTitle", card.transform, "COMMENT\nJOUER ?", 88f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.lineSpacing = -14f;
            title.rectTransform.anchorMin = new Vector2(.08f, .72f);
            title.rectTransform.anchorMax = new Vector2(.92f, .88f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI intro = UIFactory.CreateText("RulesIntro", card.transform, "Un son. Deux imitations. L’IA donne le point.", 29f, SoundCopyTheme.MutedGray, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            intro.rectTransform.anchorMin = new Vector2(.08f, .665f);
            intro.rectTransform.anchorMax = new Vector2(.92f, .72f);
            intro.rectTransform.offsetMin = Vector2.zero;
            intro.rectTransform.offsetMax = Vector2.zero;

            BuildRuleRow(card.transform, 1, "ÉCOUTE", "Regarde la vidéo et mémorise le rythme.", .535f, SoundCopyTheme.Lime);
            BuildRuleRow(card.transform, 2, "IMITE", "J1 puis J2 reproduisent exactement le son.", .405f, SoundCopyTheme.Coral);
            BuildRuleRow(card.transform, 3, "ANALYSE", "L’IA compare les deux voix au son original.", .275f, SoundCopyTheme.Lime);
            BuildRuleRow(card.transform, 4, "POINT", "La meilleure imitation gagne la manche.", .145f, SoundCopyTheme.Coral);

            GameObject close = UIFactory.CreateRect("Close", card.transform, new Vector2(.09f, .035f), new Vector2(.91f, .115f), Vector2.zero, Vector2.zero);
            closeRulesButton = UIFactory.CreateButton("CloseButton", close.transform, "C’EST PARTI  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 38f, out _);
            rulesOverlay.SetActive(false);
        }

        private static void BuildRuleRow(Transform parent, int index, string heading, string description, float y, Color accent)
        {
            GameObject row = UIFactory.CreateRect("Rule" + index, parent, new Vector2(.08f, y), new Vector2(.92f, y + .105f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(row, new Color(.965f, .94f, .89f), 24f);
            UIFactory.AddNativeOutline(row, new Color(.25f, .20f, .24f, .28f), 2f);
            GameObject number = UIFactory.CreateRect("Number", row.transform, new Vector2(.025f, .15f), new Vector2(.16f, .85f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(number, accent, 50f);
            UIFactory.AddNativeOutline(number, SoundCopyTheme.Ink, 2f);
            UIFactory.CreateText("Value", number.transform, index.ToString("00"), 32f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI headingText = UIFactory.CreateText("Heading", row.transform, heading, 35f, SoundCopyTheme.Ink, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            headingText.rectTransform.anchorMin = new Vector2(.20f, .45f);
            headingText.rectTransform.anchorMax = new Vector2(.95f, .88f);
            headingText.rectTransform.offsetMin = Vector2.zero;
            headingText.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI body = UIFactory.CreateText("Body", row.transform, description, 24f, SoundCopyTheme.MutedGray, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            body.rectTransform.anchorMin = new Vector2(.20f, .08f);
            body.rectTransform.anchorMax = new Vector2(.95f, .50f);
            body.rectTransform.offsetMin = Vector2.zero;
            body.rectTransform.offsetMax = Vector2.zero;
        }

        private void ApplyFonts()
        {
            foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                bool display = text.name.StartsWith("Headline") || text.name == "LaunchLabel" || text.name.StartsWith("J1") || text.name.StartsWith("J2") || text.name == "RulesTitle";
                if (display && displayFont != null) text.font = displayFont;
                else if (bodyFont != null) text.font = bodyFont;
            }
        }

        private void Play() => gameManager?.OnPlayButton();
        private void ShowRules() { rulesOverlay?.SetActive(true); rulesOverlay?.transform.SetAsLastSibling(); }
        private void HideRules() => rulesOverlay?.SetActive(false);
        private void ClearChildren() { for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject); }
    }
}
