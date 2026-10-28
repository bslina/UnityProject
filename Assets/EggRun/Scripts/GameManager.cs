using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Round flow: Ready -> Playing -> Won or Lost, plus the countdown timer.
public class GameManager : MonoBehaviour
{
    enum State { Ready, Playing, Won, Lost }

    [SerializeField] float timeLimit = 60f;

    [Header("Scene")]
    [SerializeField] PlayerWalker player;
    [SerializeField] SpoonController spoon;
    [SerializeField] Egg egg;
    [SerializeField] Transform finishLine;

    [Header("UI")]
    [SerializeField] Text timerText;
    [SerializeField] GameObject walkButton;
    [SerializeField] GameObject startPanel;
    [SerializeField] Button startButton;
    [SerializeField] GameObject endPanel;
    [SerializeField] Text endTitle;
    [SerializeField] Text endDetail;
    [SerializeField] Button retryButton;
    [SerializeField] Text debugText;
    [SerializeField] bool showDebug = true;

    State state;
    float timeLeft;

    void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        startButton.onClick.AddListener(StartRound);
        retryButton.onClick.AddListener(Retry);
        egg.Dropped += () => EndRound(false, "The egg fell!");
    }

    void Start()
    {
        state = State.Ready;
        timeLeft = timeLimit;

        player.CanWalk = false;
        spoon.FollowTilt = false;
        egg.Freeze(true);

        startPanel.SetActive(true);
        endPanel.SetActive(false);
        walkButton.SetActive(false);
        debugText.gameObject.SetActive(showDebug);
        UpdateTimerText();
    }

    void StartRound()
    {
        if (TiltInput.Instance != null) TiltInput.Instance.Calibrate();

        spoon.FollowTilt = true;
        egg.Freeze(false);
        player.CanWalk = true;

        startPanel.SetActive(false);
        walkButton.SetActive(true);
        state = State.Playing;
    }

    void Update()
    {
        if (showDebug && TiltInput.Instance != null)
            debugText.text = $"Tilt {TiltInput.Instance.Angle:0.0}°  ({TiltInput.Instance.Source})";

        if (state != State.Playing) return;

        timeLeft -= Time.deltaTime;
        UpdateTimerText();

        if (player.transform.position.x >= finishLine.position.x)
            EndRound(true, $"{timeLeft:0.0} s left");
        else if (timeLeft <= 0f)
            EndRound(false, "Time's up!");
    }

    void EndRound(bool won, string detail)
    {
        if (state != State.Playing) return;
        state = won ? State.Won : State.Lost;

        player.CanWalk = false;
        walkButton.SetActive(false);
        endPanel.SetActive(true);
        endTitle.text = won ? "You made it!" : "Oops!";
        endDetail.text = detail;
    }

    void UpdateTimerText()
    {
        timerText.text = Mathf.Max(0f, timeLeft).ToString("0.0");
    }

    void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
