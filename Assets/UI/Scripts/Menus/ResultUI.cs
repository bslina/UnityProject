using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // Win / lose popup shown at the end of every minigame.
    public class ResultUI : UIPanel
    {
        [SerializeField] ThemedImage headerIcon;
        [SerializeField] Text titleText;
        [SerializeField] Text detailText;
        [SerializeField] Text scoreText;
        [SerializeField] GameObject starsRow;
        [SerializeField] ThemedImage[] stars;

        [Header("Buttons")]
        [SerializeField] Button nextButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button levelsButton;
        [SerializeField] Button mainMenuButton;

        [Header("Texts")]
        [SerializeField] string winTitle = "You made it!";
        [SerializeField] string loseTitle = "Oops!";
        [SerializeField] string lastGameNextLabel = "Finish";

        Text nextLabel;
        string nextLabelDefault;

        protected override void Awake()
        {
            base.Awake();
            nextButton.onClick.AddListener(() => GameFlow.Instance.NextGame());
            retryButton.onClick.AddListener(() => GameFlow.Instance.Restart());
            levelsButton.onClick.AddListener(() => GameFlow.Instance.LoadLevelMenu());
            mainMenuButton.onClick.AddListener(() => GameFlow.Instance.LoadMainMenu());
            nextLabel = nextButton.GetComponentInChildren<Text>(true);
            if (nextLabel != null) nextLabelDefault = nextLabel.text;
        }

        public void Show(bool won, string detail, int starCount, int score, bool newBest, bool hasNext)
        {
            gameObject.SetActive(true); // runs Awake the first time

            if (headerIcon != null) headerIcon.SetRole(won ? ImageRole.WinIcon : ImageRole.LoseIcon);
            if (titleText != null) titleText.text = won ? winTitle : loseTitle;
            if (detailText != null) detailText.text = detail;

            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(won && score > 0);
                scoreText.text = newBest ? $"Score {score}  (new best!)" : $"Score {score}";
            }

            if (starsRow != null) starsRow.SetActive(won);
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null) stars[i].SetRole(i < starCount ? ImageRole.StarFull : ImageRole.StarEmpty);

            // Winning shows Next (or Finish on the last game); losing hides it so Retry stands out.
            nextButton.gameObject.SetActive(won);
            if (nextLabel != null) nextLabel.text = hasNext ? nextLabelDefault : lastGameNextLabel;

            Show();
        }
    }
}
