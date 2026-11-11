using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // WinMenu and LoseMenu scenes. Reads what the last minigame reported (GameFlow.LastResult).
    // Every slot is optional: the LoseMenu scene simply has no stars, score or Next button.
    public class ResultUI : UIPanel
    {
        [Tooltip("Ticked in the WinMenu scene, unticked in the LoseMenu scene (picks the win/lose sound).")]
        [SerializeField] bool isWinScreen = true;

        [Tooltip("Headline: \"LINA WINS!\" / \"DRAW!\" (every minigame is a 2-player duel).")]
        [SerializeField] Text titleText;
        [SerializeField] Text detailText;
        [SerializeField] Text scoreText;
        [Tooltip("Shows which minigame was just played.")]
        [SerializeField] Text gameNameText;
        [SerializeField] Image[] stars;
        [SerializeField] StarStyle starStyle = new StarStyle();

        [Header("Buttons")]
        [SerializeField] Button nextButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button levelsButton;
        [SerializeField] Button mainMenuButton;

        [Header("Texts")]
        [SerializeField] string lastGameNextLabel = "Finish";

        protected override void Awake()
        {
            base.Awake();
            if (nextButton != null) nextButton.onClick.AddListener(() => GameFlow.Instance.NextGame());
            if (retryButton != null) retryButton.onClick.AddListener(() => GameFlow.Instance.Restart());
            if (levelsButton != null) levelsButton.onClick.AddListener(() => GameFlow.Instance.LoadLevelMenu());
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => GameFlow.Instance.LoadMainMenu());
        }

        void Start()
        {
            var flow = GameFlow.Instance;
            var result = flow != null ? flow.LastResult : null;

            // Opened directly in the Editor (no game played yet): keep the texts typed in the scene.
            if (result != null)
            {
                if (detailText != null) detailText.text = result.Detail;

                if (result.IsDuel)
                {
                    string winner = result.Winner == 1 ? result.Player1 : result.Winner == 2 ? result.Player2 : null;
                    if (titleText != null) titleText.text = winner == null ? "DRAW!" : winner.ToUpperInvariant() + " WINS!";
                    if (scoreText != null)
                    {
                        scoreText.gameObject.SetActive(!string.IsNullOrEmpty(result.ScoreLine));
                        scoreText.text = result.ScoreLine;
                    }
                }
                else if (scoreText != null)
                {
                    scoreText.gameObject.SetActive(result.Score > 0);
                    scoreText.text = result.NewBest ? $"Score {result.Score}  (new best!)" : $"Score {result.Score}";
                }

                for (int i = 0; i < stars.Length; i++)
                    starStyle.Apply(stars[i], i < result.Stars);

                var game = flow.Registry != null ? flow.Registry.Get(result.GameIndex) : null;
                if (gameNameText != null) gameNameText.text = game != null ? game.displayName : "";
            }

            if (nextButton != null && flow != null && !flow.HasNextGame)
            {
                var label = nextButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = lastGameNextLabel;
            }

            if (flow != null && flow.Audio != null) flow.Audio.PlayResult(isWinScreen);
            Show();
        }
    }
}
