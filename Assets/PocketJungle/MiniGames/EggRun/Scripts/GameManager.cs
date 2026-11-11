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

    [Header("Music (files in Assets/PocketJungle/Shared/Resources/Audio)")]
    [Tooltip("Played while the start panel (how to play) is shown.")]
    [SerializeField] string startPanelMusic = "music_egg_run_intro";
    [Tooltip("Played once START is pressed. Empty = the normal Egg Run music (music_egg_run).")]
    [SerializeField] string gameplayMusic = "";
    [Tooltip("Music level while playing (1 = normal). Lower = footsteps and effects are easier to hear.")]
    [SerializeField, Range(0f, 1f)] float gameplayMusicVolume = 0.45f;

    [Header("Stars on win (seconds left needed)")]
    [SerializeField] float threeStarTime = 30f;
    [SerializeField] float twoStarTime = 15f;

    State state;
    float timeLeft;
    float startX;

    void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        startButton.onClick.AddListener(StartRound);
        retryButton.onClick.AddListener(Retry);
        egg.Dropped += () => EndRound(false, "The egg fell!");

        if (sunGlare == null) sunGlare = GetComponent<SunGlare>();
        if (sunGlare == null) sunGlare = gameObject.AddComponent<SunGlare>();
        if (player.GetComponent<Footsteps>() == null) player.gameObject.AddComponent<Footsteps>();

        sunGlare.Cooked += () =>
        {
            GameAudio.Play("egg_cooked");   // quick sharp oven "ting!"
            egg.Freeze(true); // a cooked egg stays in the spoon
            EndRound(false, sunGlare.CookedText);
        };
    }

    void Start()
    {
        state = State.Ready;
        timeLeft = timeLimit;
        startX = player.transform.position.x;

        player.CanWalk = false;
        spoon.FollowTilt = false;
        egg.Freeze(true);

        startPanel.SetActive(true);
        MiniGameBridge.PlayMusic(startPanelMusic);
        if (MiniGameBridge.IsAvailable)
        {
            // Pocket Jungle plays Egg Run as 2 turns: the start panel's title says whose turn it is.
            foreach (var t in startPanel.GetComponentsInChildren<Text>(true))
                if (t.name == "Title") { t.text = MiniGameBridge.PlayerName(MiniGameBridge.TurnPlayer) + "'s turn!"; break; }
        }
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
        MiniGameBridge.PlayMusic(gameplayMusic);
        MiniGameBridge.SetMusicVolume(gameplayMusicVolume);
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
        if (PlayerAnimator.Instance != null) PlayerAnimator.Instance.ShowResult(won);

        if (MiniGameBridge.IsAvailable)
        {
            // Each player plays one turn; the shared menus compare them: any finish beats any fall,
            // two finishes = more time left (score), two falls = who got further (score = share of the track).
            if (won)
            {
                int stars = timeLeft >= threeStarTime ? 3 : timeLeft >= twoStarTime ? 2 : 1;
                MiniGameBridge.Win(detail, stars, Mathf.RoundToInt(timeLeft * 10f), 2f); // time to see the victory dance
            }
            else
            {
                float track = Mathf.Max(0.01f, finishLine.position.x - startX);
                float walked = Mathf.Clamp01((player.transform.position.x - startX) / track);
                MiniGameBridge.Lose(detail + " (" + Mathf.RoundToInt(walked * 100f) + "% of the way)", 2f, Mathf.RoundToInt(walked * 999f));
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
