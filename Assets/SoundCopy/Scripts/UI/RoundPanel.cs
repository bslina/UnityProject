using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class RoundPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI playersText;
        [SerializeField] private Button watchButton;
        [SerializeField] private Button backButton;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            watchButton?.onClick.AddListener(Watch);
            backButton?.onClick.AddListener(Back);
        }

        private void OnEnable() => Refresh();

        private void OnDestroy()
        {
            watchButton?.onClick.RemoveListener(Watch);
            backButton?.onClick.RemoveListener(Back);
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            GameObject shell = ScreenScaffold.Build(transform, "PRÊTS ?", "Le round commence", "Écoute bien. Après, il faudra tout reproduire.");

            GameObject status = UIFactory.CreateRect("RoundStatus", shell.transform, new Vector2(0.075f, 0.58f), new Vector2(0.925f, 0.69f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(status, SoundCopyTheme.DarkPlum, 34f);
            roundText = CreateStatusText(status.transform, "Round", new Vector2(0.04f, 0f), new Vector2(0.34f, 1f), TextAlignmentOptions.MidlineLeft, 31f, SoundCopyTheme.Lime);
            categoryText = CreateStatusText(status.transform, "Category", new Vector2(0.36f, 0f), new Vector2(0.96f, 1f), TextAlignmentOptions.MidlineRight, 24f, SoundCopyTheme.Cream);

            GameObject duel = UIFactory.CreateRect("DuelCard", shell.transform, new Vector2(0.075f, 0.36f), new Vector2(0.925f, 0.55f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(duel, SoundCopyTheme.PaleBonus, 40f);
            UIFactory.AddNativeOutline(duel, SoundCopyTheme.Ink, 3f);
            playersText = UIFactory.CreateText("Players", duel.transform, "Joueur 1   VS   Joueur 2", 40f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI hint = UIFactory.CreateText("Hint", duel.transform, "UN SON  ·  DEUX IMITATIONS  ·  UN VERDICT IA", 18f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);
            hint.characterSpacing = 4f;
            hint.rectTransform.anchorMin = new Vector2(0.05f, 0.08f);
            hint.rectTransform.anchorMax = new Vector2(0.95f, 0.34f);
            hint.rectTransform.offsetMin = Vector2.zero;
            hint.rectTransform.offsetMax = Vector2.zero;
            playersText.rectTransform.anchorMin = new Vector2(0.05f, 0.31f);
            playersText.rectTransform.anchorMax = new Vector2(0.95f, 0.88f);
            playersText.rectTransform.offsetMin = Vector2.zero;
            playersText.rectTransform.offsetMax = Vector2.zero;

            GameObject action = UIFactory.CreateRect("WatchAction", shell.transform, new Vector2(0.075f, 0.235f), new Vector2(0.925f, 0.32f), Vector2.zero, Vector2.zero);
            watchButton = UIFactory.CreateButton("WatchButton", action.transform, "Écouter le son", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 34f, out _);
            GameObject back = UIFactory.CreateRect("BackAction", shell.transform, new Vector2(0.075f, 0.14f), new Vector2(0.43f, 0.195f), Vector2.zero, Vector2.zero);
            backButton = UIFactory.CreateButton("BackButton", back.transform, "<  Retour", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 28f, out _, false);
            Refresh();
        }

        private TextMeshProUGUI CreateStatusText(Transform parent, string name, Vector2 min, Vector2 max, TextAlignmentOptions alignment, float size, Color color)
        {
            TextMeshProUGUI text = UIFactory.CreateText(name, parent, string.Empty, size, color, alignment, FontStyles.Bold);
            text.rectTransform.anchorMin = min;
            text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        public void Refresh()
        {
            if (gameManager == null) return;
            if (roundText != null) roundText.text = $"ROUND {gameManager.CurrentRound} / {gameManager.RoundCount}";
            if (categoryText != null) categoryText.text = gameManager.CategoryLabel;
            if (playersText != null) playersText.text = $"{gameManager.Player1Name}   VS   {gameManager.Player2Name}";
        }

        private void Watch() => gameManager?.OnWatchVideo();
        private void Back() => gameManager?.OnRoundBack();
    }
}
