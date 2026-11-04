using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class MatchSetupPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button oneButton;
        [SerializeField] private Button threeButton;
        [SerializeField] private Button fiveButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button backButton;
        [SerializeField] private RoundedRectGraphic oneGraphic;
        [SerializeField] private RoundedRectGraphic threeGraphic;
        [SerializeField] private RoundedRectGraphic fiveGraphic;
        [SerializeField] private int selectedRounds = 3;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            oneButton?.onClick.AddListener(SelectOne);
            threeButton?.onClick.AddListener(SelectThree);
            fiveButton?.onClick.AddListener(SelectFive);
            continueButton?.onClick.AddListener(Continue);
            backButton?.onClick.AddListener(Back);
            UpdateSelection();
        }

        private void OnDestroy()
        {
            oneButton?.onClick.RemoveListener(SelectOne);
            threeButton?.onClick.RemoveListener(SelectThree);
            fiveButton?.onClick.RemoveListener(SelectFive);
            continueButton?.onClick.RemoveListener(Continue);
            backButton?.onClick.RemoveListener(Back);
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            GameObject shell = ScreenScaffold.Build(transform, "ÉTAPE  02 / 03", "Combien de rounds ?", "Court et intense, ou long et chaotique ?");
            GameObject cards = UIFactory.CreateRect("RoundCards", shell.transform, new Vector2(0.075f, 0.43f), new Vector2(0.925f, 0.64f), Vector2.zero, Vector2.zero);
            oneButton = BuildCard(cards.transform, "OneRound", new Vector2(0f, 0f), new Vector2(0.3f, 1f), "1", "RAPIDE", out oneGraphic);
            threeButton = BuildCard(cards.transform, "ThreeRounds", new Vector2(0.35f, 0f), new Vector2(0.65f, 1f), "3", "NORMAL", out threeGraphic);
            fiveButton = BuildCard(cards.transform, "FiveRounds", new Vector2(0.7f, 0f), new Vector2(1f, 1f), "5", "CHAOS", out fiveGraphic);

            GameObject action = UIFactory.CreateRect("ContinueAction", shell.transform, new Vector2(0.075f, 0.30f), new Vector2(0.925f, 0.375f), Vector2.zero, Vector2.zero);
            continueButton = UIFactory.CreateButton("ContinueButton", action.transform, "Choisir une catégorie", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 34f, out _);
            GameObject back = UIFactory.CreateRect("BackAction", shell.transform, new Vector2(0.075f, 0.205f), new Vector2(0.43f, 0.26f), Vector2.zero, Vector2.zero);
            backButton = UIFactory.CreateButton("BackButton", back.transform, "<  Retour", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 28f, out _, false);
            UpdateSelection();
        }

        private Button BuildCard(Transform parent, string name, Vector2 min, Vector2 max, string number, string caption, out RoundedRectGraphic graphic)
        {
            GameObject card = UIFactory.CreateRect(name, parent, min, max, Vector2.zero, Vector2.zero);
            graphic = UIFactory.AddRounded(card, SoundCopyTheme.DarkPlum, 38f, true);
            UIFactory.AddNativeOutline(card, SoundCopyTheme.Ink, 3f);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = graphic;
            TextMeshProUGUI value = UIFactory.CreateText("Number", card.transform, number, 76f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            value.rectTransform.anchorMin = new Vector2(0f, 0.34f);
            value.rectTransform.anchorMax = new Vector2(1f, 0.9f);
            value.rectTransform.offsetMin = Vector2.zero;
            value.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI text = UIFactory.CreateText("Caption", card.transform, caption, 25f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);
            text.characterSpacing = 6f;
            text.rectTransform.anchorMin = new Vector2(0f, 0.12f);
            text.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private void SelectOne() { selectedRounds = 1; UpdateSelection(); }
        private void SelectThree() { selectedRounds = 3; UpdateSelection(); }
        private void SelectFive() { selectedRounds = 5; UpdateSelection(); }
        private void Continue() => gameManager?.OnMatchSetupContinue(selectedRounds);
        private void Back() => gameManager?.OnMatchSetupBack();

        private void UpdateSelection()
        {
            if (oneGraphic != null) oneGraphic.color = selectedRounds == 1 ? SoundCopyTheme.Lime : SoundCopyTheme.DarkPlum;
            if (threeGraphic != null) threeGraphic.color = selectedRounds == 3 ? SoundCopyTheme.Lime : SoundCopyTheme.DarkPlum;
            if (fiveGraphic != null) fiveGraphic.color = selectedRounds == 5 ? SoundCopyTheme.Lime : SoundCopyTheme.DarkPlum;
        }
    }
}
