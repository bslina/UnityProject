using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class CategoryPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button internationalButton;
        [SerializeField] private Button tunisianButton;
        [SerializeField] private Button mixedButton;
        [SerializeField] private Button backButton;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            internationalButton?.onClick.AddListener(International);
            tunisianButton?.onClick.AddListener(Tunisian);
            mixedButton?.onClick.AddListener(Mixed);
            backButton?.onClick.AddListener(Back);
        }

        private void OnDestroy()
        {
            internationalButton?.onClick.RemoveListener(International);
            tunisianButton?.onClick.RemoveListener(Tunisian);
            mixedButton?.onClick.RemoveListener(Mixed);
            backButton?.onClick.RemoveListener(Back);
        }

        public void SetPlayerNames(string first, string second) => SetMatchInfo(first, second, gameManager != null ? gameManager.RoundCount : 3);

        public void SetMatchInfo(string first, string second, int rounds)
        {
            if (summaryText != null) summaryText.text = first + "  VS  " + second + "     ·     " + rounds + " ROUND" + (rounds > 1 ? "S" : string.Empty);
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            GameObject shell = ScreenScaffold.Build(transform, "ÉTAPE  2 / 2", "Choisis ton chaos.", "Toute la partie piochera dans la famille sélectionnée.");

            GameObject cards = UIFactory.CreateRect("CategoryCards", shell.transform, new Vector2(0.075f, 0.245f), new Vector2(0.925f, 0.67f), Vector2.zero, Vector2.zero);
            internationalButton = BuildCategory(cards.transform, "International", new Vector2(0f, 0.69f), new Vector2(1f, 1f), new Color(0.53f, 0.84f, 1f), "MONDE", "INTERNATIONAL", "Airhorn, bruh, Nokia et classiques sans traduction.", "BRUH  ·  NOKIA  ·  MEOW");
            tunisianButton = BuildCategory(cards.transform, "Tunisian", new Vector2(0f, 0.345f), new Vector2(1f, 0.655f), new Color(1f, 0.68f, 0.63f), "MADE IN TUNISIA", "TN TOUNSI", "Répliques, stade, taxi et sons cultes du pays.", "3ASLEMA  ·  CHBIK  ·  BRIK");
            mixedButton = BuildCategory(cards.transform, "Mixed", new Vector2(0f, 0f), new Vector2(1f, 0.31f), SoundCopyTheme.DarkPlum, "MODE CHAOS", "MIXTE", "International et Tounsi dans la même partie.", "AUCUN RÉPIT  ·  SURPRISE");

            GameObject summary = UIFactory.CreateRect("MatchSummary", shell.transform, new Vector2(0.075f, 0.16f), new Vector2(0.925f, 0.22f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(summary, new Color(0.97f, 0.95f, 0.91f), 24f);
            UIFactory.AddNativeOutline(summary, new Color(0.6f, 0.57f, 0.55f, 0.4f), 2f);
            summaryText = UIFactory.CreateText("Summary", summary.transform, "Joueur 1  VS  Joueur 2     ·     3 ROUNDS", 18f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject back = UIFactory.CreateRect("BackAction", shell.transform, new Vector2(0.075f, 0.075f), new Vector2(0.38f, 0.13f), Vector2.zero, Vector2.zero);
            backButton = UIFactory.CreateButton("BackButton", back.transform, "< RETOUR", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 23f, out _, false);
        }

        private Button BuildCategory(Transform parent, string name, Vector2 min, Vector2 max, Color color, string overline, string titleValue, string description, string preview)
        {
            bool dark = color == SoundCopyTheme.DarkPlum;
            Color ink = dark ? SoundCopyTheme.Cream : SoundCopyTheme.Ink;
            Color muted = dark ? new Color(0.78f, 0.74f, 0.77f) : SoundCopyTheme.MutedGray;
            GameObject card = UIFactory.CreateRect(name, parent, min, max, Vector2.zero, Vector2.zero);
            RoundedRectGraphic graphic = UIFactory.AddRounded(card, color, 34f, true);
            UIFactory.AddNativeOutline(card, SoundCopyTheme.Ink, 3f);
            Shadow shadow = card.AddComponent<Shadow>();
            shadow.effectColor = SoundCopyTheme.Ink;
            shadow.effectDistance = new Vector2(0f, -6f);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = graphic;

            TextMeshProUGUI top = UIFactory.CreateText("Overline", card.transform, overline, 14f, muted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            top.characterSpacing = 4f;
            top.rectTransform.anchorMin = new Vector2(0.055f, 0.72f);
            top.rectTransform.anchorMax = new Vector2(0.72f, 0.92f);
            top.rectTransform.offsetMin = Vector2.zero;
            top.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI title = UIFactory.CreateText("Title", card.transform, titleValue, 34f, ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.rectTransform.anchorMin = new Vector2(0.055f, 0.43f);
            title.rectTransform.anchorMax = new Vector2(0.82f, 0.73f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI sub = UIFactory.CreateText("Description", card.transform, description, 17f, muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            sub.rectTransform.anchorMin = new Vector2(0.055f, 0.20f);
            sub.rectTransform.anchorMax = new Vector2(0.82f, 0.47f);
            sub.rectTransform.offsetMin = Vector2.zero;
            sub.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI examples = UIFactory.CreateText("Preview", card.transform, preview, 13f, muted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            examples.characterSpacing = 2f;
            examples.rectTransform.anchorMin = new Vector2(0.055f, 0.03f);
            examples.rectTransform.anchorMax = new Vector2(0.80f, 0.21f);
            examples.rectTransform.offsetMin = Vector2.zero;
            examples.rectTransform.offsetMax = Vector2.zero;
            GameObject arrow = UIFactory.CreateRect("ArrowBadge", card.transform, new Vector2(0.86f, 0.56f), new Vector2(0.96f, 0.87f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(arrow, dark ? SoundCopyTheme.Lime : SoundCopyTheme.Ink, 50f);
            UIFactory.CreateText("Arrow", arrow.transform, ">", 26f, dark ? SoundCopyTheme.Ink : SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            return button;
        }

        private void International() => gameManager?.OnCategorySelected(ChallengeCategory.International);
        private void Tunisian() => gameManager?.OnCategorySelected(ChallengeCategory.Tunisian);
        private void Mixed() => gameManager?.OnCategorySelected(ChallengeCategory.Mixed);
        private void Back() => gameManager?.OnCategoryBack();
    }
}
