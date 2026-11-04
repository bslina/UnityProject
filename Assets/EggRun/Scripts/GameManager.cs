using GameUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Round flow: Ready -> Playing -> Won or Lost, plus the countdown timer.
// When the shared Game UI is running (always, unless its prefab is missing), the win/lose
// screen, pause and retry come from it (MiniGameBridge); the old EndPanel is the fallback.
public class GameManager : MonoBehaviour
{
    enum State { Ready, Playing, Won, Lost }

    [SerializeField] float timeLimit = 60f;

    [Header("Scene")]
    [SerializeField] PlayerWalker player;
    [SerializeField] SpoonController spoon;
    [SerializeField] Egg egg;
    [SerializeField] Transform finishLine;
    [Tooltip("Sun events + heat meter. Added automatically if left empty.")]
    [SerializeField] SunGlare sunGlare;

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

    [Header("Stars on win (seconds left needed)")]
    [SerializeField] float threeStarTime = 30f;
    [SerializeField] float twoStarTime = 15f;

    State state;
    float timeLeft;

    void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        startButton.onClick.AddListener(StartRound);
        retryButton.onClick.AddListener(Retry);
        egg.Dropped += () => EndRound(false, "The egg fell!");

        if (sunGlare == null) sunGlare = GetComponent<SunGlare>();
        if (sunGlare == null) sunGlare = gameObject.AddComponent<SunGlare>();
        sunGlare.Cooked += () =>
        {
            egg.Freeze(true); // a cooked egg stays in the spoon
            EndRound(false, sunGlare.CookedText);
        };
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
        debugText.gameObject.SetActive(ShowDebug);
        UpdateTimerText();
        sunGlare.Prepare();
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
        sunGlare.Begin(egg, () => timeLeft);
    }

    void Update()
    {
        debugText.gameObject.SetActive(ShowDebug);
        if (ShowDebug && TiltInput.Instance != null)
            debugText.text = $"Tilt {TiltInput.Instance.Angle:0.0}°  ({TiltInput.Instance.Source})"
                + (ShakeDetector.Instance != null ? $"\nShake {ShakeDetector.Instance.Strength:0.0} g" : "")
                + (LightCoverDetector.Instance != null ? "\n" + LightCoverDetector.Instance.DebugLine() : "")
                + $"\nSun {sunGlare.PhaseName}  heat {sunGlare.Heat01:P0}";

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
        sunGlare.Stop();

        if (MiniGameBridge.IsAvailable)
        {
            if (won)
            {
                int stars = timeLeft >= threeStarTime ? 3 : timeLeft >= twoStarTime ? 2 : 1;
                MiniGameBridge.Win(detail, stars, Mathf.RoundToInt(timeLeft * 10f));
            }
            else
            {
                MiniGameBridge.Lose(detail);
            }
            return;
        }

        endPanel.SetActive(true);
        endTitle.text = won ? "You made it!" : "Oops!";
        endDetail.text = detail;
    }

    void UpdateTimerText()
    {
        timerText.text = Mathf.Max(0f, timeLeft).ToString("0.0");
    }

    // The Inspector switch AND the player's "Show debug info" setting must both be on.
    bool ShowDebug => showDebug && GameSettings.ShowDebug;

    void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
