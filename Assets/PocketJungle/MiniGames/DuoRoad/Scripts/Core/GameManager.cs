using UnityEngine;
namespace DuoRoad
{

// Game flow for a RoadDuo match: 5 rounds, then a results screen.
//
//   Round 1: Player 1 drives (wheel + pedals), Player 2 shoots (stick, FIRE, BOMB).
//   Round 2: Player 1 alone  - tilt the phone to steer, pedals + gunner side as usual.
//   Round 3: Player 2 alone  - same controls as round 2.
//   Round 4: Player 1 alone  - VOICE round: tilt to steer, gas is automatic, say BRAKE / SHOOT / BOMB.
//   Round 5: Player 2 alone  - VOICE round, same as round 4.
//   Each round ends when the car dies. After round 5: results (both scores + winner).
//
// Scoring (kept per player):
//   Round 1  -> Player 1 gets the driving points (distance + zone bonuses),
//               Player 2 gets the shooting points (kills, popped bombs, big-bomb traffic kills).
//   Rounds 2 and 4 -> everything goes to Player 1.   Rounds 3 and 5 -> everything goes to Player 2.
//
// States: Intro -> Playing <-> Paused -> RoundOver -> Intro (next round) ... -> Results -> Intro (round 1)
public class GameManager : MonoBehaviour
{
    public const string GameName = "RoadDuo";
    public const int RoundCount = 5;

    public enum State { Intro, Playing, Paused, RoundOver, Results }

    public CarController car;
    public PlayerHealth health;
    public ObstacleSpawner spawner;
    public EnemySpawner enemies;
    public GunController gun;
    public PickupSpawner pickups;
    public PlayerAbilities abilities;
    public ZoneManager zones;
    public TouchInput touch;

    public int killScore = 100;

    public State Current { get; private set; }

    // Pocket Jungle: the players' names (Players scene); "Player 1/2" when played on its own.
    public static string Name(int player)
    {
        return GameUI.MiniGameBridge.IsAvailable ? GameUI.MiniGameBridge.PlayerName(player) : "Player " + player;
    }

    // Pocket Jungle Random Match: a short version with only the two solo tilt rounds
    // (round 2 = Player 1, round 3 = Player 2), the fairest head-to-head.
    public static int FirstRound { get { return GameUI.MiniGameBridge.InMatch ? 1 : 0; } }
    public static int LastRound { get { return GameUI.MiniGameBridge.InMatch ? 2 : RoundCount - 1; } }
    public static int RoundsInMatch { get { return LastRound - FirstRound + 1; } }
    // 1-based number of the round as shown on screen (1/2 in a Random Match, 1/5 otherwise)
    public int RoundShown { get { return Round - FirstRound + 1; } }

    // 0..4
    public int Round { get; private set; }
    public int RoundNumber { get { return Round + 1; } }

    // Round 1 is the duo round; rounds 2-5 are solo (tilt steering); rounds 4 and 5 are played by voice
    public bool IsDuoRound { get { return Round == 0; } }
    public bool IsVoiceRound { get { return IsVoiceRoundIndex(Round); } }
    public int SoloPlayer { get { return SoloPlayerOf(Round); } }

    // Helpers that work for any round index (0-based), e.g. to announce the NEXT round
    public static bool IsVoiceRoundIndex(int round) { return round >= 3; }
    public static int SoloPlayerOf(int round) { return round == 1 || round == 3 ? 1 : 2; }

    public int Kills { get; private set; }

    // Live score of the round being played (duo round: team score)
    public int Score { get { return DistanceScore + driverBonus + gunnerBonus; } }
    public int DistanceScore { get { return car != null ? Mathf.RoundToInt(car.Distance) : 0; } }

    // What the last finished round gave to each player (for the Round Over screen)
    public int LastRoundScore { get; private set; }
    public int LastRoundDriver { get; private set; }
    public int LastRoundGunner { get; private set; }

    // Match scores
    public int Round1Driver { get; private set; }   // Player 1
    public int Round1Gunner { get; private set; }   // Player 2
    public int Round2Score { get; private set; }    // Player 1
    public int Round3Score { get; private set; }    // Player 2
    public int Round4Score { get; private set; }    // Player 1, voice
    public int Round5Score { get; private set; }    // Player 2, voice
    public int P1Total { get { return Round1Driver + Round2Score + Round4Score; } }
    public int P2Total { get { return Round1Gunner + Round3Score + Round5Score; } }
    public int MatchTotal { get { return P1Total + P2Total; } }

    // 1 = Player 1, 2 = Player 2, 0 = draw
    public int Winner { get { return P1Total > P2Total ? 1 : (P2Total > P1Total ? 2 : 0); } }

    public int BestScore { get { return ScoreBridge.Best(GameName); } }

    // Taps are ignored for a moment after a screen appears (no accidental skipping)
    public bool CanAdvance
    {
        get
        {
            float wait = Current == State.Intro ? 0.4f : 1f;
            return Time.unscaledTime - stateTime > wait;
        }
    }

    // Seconds since the current screen / state started (real time, also counts while paused)
    public float StateAge { get { return Time.unscaledTime - stateTime; } }

    public string RoundLabel
    {
        get
        {
            if (IsDuoRound) return Name(1) + " drives · " + Name(2) + " shoots";
            if (IsVoiceRound) return Name(SoloPlayer) + " solo · voice";
            return Name(SoloPlayer) + " solo";
        }
    }

    // Short on-screen messages ("+30 HP", "BIG BOMB!") shown by the HUD
    public event System.Action<string> Message;

    // Fired once when the last round ends: (player 1 total, player 2 total)
    public event System.Action<int, int> MatchFinished;

    int driverBonus, gunnerBonus;
    float stateTime;
    bool started;

    void Start()
    {
        Time.timeScale = 1f;
        health.Died += OnDied;
        // Pocket Jungle: the shared pause menu replaces Duo Road's own pause screen.
        GameUI.MiniGameBridge.PauseChanged += OnSharedPause;
    }

    void OnSharedPause(bool paused)
    {
        if (paused) Pause();      // only while a round is played (freezes the controls too)
        else Resume();
    }

    void OnDestroy()
    {
        GameUI.MiniGameBridge.PauseChanged -= OnSharedPause;
        Time.timeScale = 1f;
    }

    // Phone call / app switched away: pause so the player is not killed in the background
    void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        if (InBigGame && GameUI.GameFlow.Instance != null) GameUI.GameFlow.Instance.Pause();   // shared pause menu
        else Pause();
    }

    public void Say(string text)
    {
        if (Message != null) Message(text);
    }

    // ------------------------------------------------------------------ score

    // Extra points (popping a bomb, destroying a traffic car...). forDriver = driving points.
    public void AddScore(int points, bool forDriver = false)
    {
        if (Current != State.Playing) return;
        if (forDriver) driverBonus += points;
        else gunnerBonus += points;
    }

    // Same thing, under the name the enemy scripts use
    public void AddBonus(int points)
    {
        AddScore(points);
    }

    public void RegisterKill(int points)
    {
        if (Current != State.Playing) return;
        Kills++;
        gunnerBonus += points;
    }

    // Called by EnemySpawner every time an enemy car is destroyed
    public void AddKill()
    {
        RegisterKill(killScore);
    }

    // ------------------------------------------------------------------ flow

    void Update()
    {
        // Pocket Jungle: no music during the voice rounds, so the microphone hears the player.
        GameUI.MiniGameBridge.SetMusicPaused(IsVoiceRound && (Current == State.Intro || Current == State.Playing || Current == State.Paused));

        // First frame: every other script has started, show the round 1 intro
        if (!started)
        {
            started = true;
            EnterIntro(FirstRound);
            return;
        }

        switch (Current)
        {
            case State.Intro:
                if (CanAdvance && ScreenTap.Any()) StartRound();
                break;

            case State.RoundOver:
                if (CanAdvance && ScreenTap.Any()) EnterIntro(Round + 1);
                break;

            case State.Results:
                if (CanAdvance && ScreenTap.Any())
                {
                    if (InBigGame) ReportToBigGame();   // Pocket Jungle: shared Win screen
                    else RestartMatch();
                }
                break;

            case State.Playing:
                if (!InBigGame && PausePressed()) Pause();     // in Pocket Jungle, Escape opens the shared pause
                break;

            case State.Paused:
                if (!InBigGame && PausePressed()) Resume();
                break;
        }
    }

    // Editor convenience: Escape or P
    static bool PausePressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame);
    }

    void SetState(State s)
    {
        Current = s;
        stateTime = Time.unscaledTime;
        // Pocket Jungle: the shared pause button only while a round is being played.
        if (InBigGame) GameUI.MiniGameBridge.SetPauseButtonVisible(s == State.Playing || s == State.Paused);
    }

    // Round screen: the world is reset and parked, controls are off, the HUD shows the instructions.
    void EnterIntro(int round)
    {
        Round = round;

        Time.timeScale = 1f;
        ResetWorld(false);          // keep the current sky; the new road look comes when the round starts
        SetPlayActive(false);
        car.Park();

        // Round 1 = wheel on screen, rounds 2-5 = tilt (no wheel). Rounds 4 and 5 also switch to voice
        // (no buttons, microphone on; it is ready while the intro screen is shown).
        if (touch != null)
        {
            touch.SetSteerMode(round == 0 ? TouchInput.SteerMode.Wheel : TouchInput.SteerMode.Tilt);
            touch.SetVoiceMode(IsVoiceRound);
        }

        SetState(State.Intro);
    }

    void StartRound()
    {
        ResetWorld(true);
        driverBonus = 0;
        gunnerBonus = 0;
        Kills = 0;

        Time.timeScale = 1f;
        SetPlayActive(true);

        // The way the phone is held right now is "straight ahead"
        if (touch != null) touch.Recalibrate();

        SetState(State.Playing);
    }

    void OnDied()
    {
        if (Current != State.Playing) return;

        // The car rolls to a stop, no more input, no more spawning
        SetPlayActive(false);
        if (touch != null) touch.SetVoiceListening(false);   // microphone off until the next voice round
        RecordRound();

        if (Round >= LastRound) EnterResults();
        else SetState(State.RoundOver);
    }

    void RecordRound()
    {
        LastRoundDriver = DistanceScore + driverBonus;   // driving points
        LastRoundGunner = gunnerBonus;                   // shooting points
        LastRoundScore = LastRoundDriver + LastRoundGunner;

        if (Round == 0)
        {
            Round1Driver = LastRoundDriver;   // Player 1
            Round1Gunner = LastRoundGunner;   // Player 2
        }
        else if (Round == 1) Round2Score = LastRoundScore;   // Player 1, solo
        else if (Round == 2) Round3Score = LastRoundScore;   // Player 2, solo
        else if (Round == 3) Round4Score = LastRoundScore;   // Player 1, voice
        else Round5Score = LastRoundScore;                   // Player 2, voice
    }

    void EnterResults()
    {
        SetState(State.Results);

        ScoreBridge.Report(GameName, MatchTotal);
        if (MatchFinished != null) MatchFinished(P1Total, P2Total);
    }

    // ------------------------------------------------------------------ Pocket Jungle

    // True when Duo Road runs inside the Pocket Jungle app (shared menus, Win screen, level menu).
    public static bool InBigGame { get { return GameUI.MiniGameBridge.IsAvailable; } }

    // Stars on the Win screen / level card from the match total (both players together).
    public int twoStarTotal = 1500;
    public int threeStarTotal = 4000;

    bool reported;

    void ReportToBigGame()
    {
        if (reported) return;
        reported = true;
        string verdict = Winner == 0 ? "Draw!" : Name(Winner) + " wins!";
        int stars = MatchTotal >= threeStarTotal ? 3 : MatchTotal >= twoStarTotal ? 2 : 1;
        // Win screen normally; in a Random Match it counts the round for the winner.
        GameUI.MiniGameBridge.ReportDuel(Winner, P1Total, P2Total, verdict + "  P1 " + P1Total + " - P2 " + P2Total, stars, MatchTotal, 0.2f);
    }

    // Pause menu "Back to levels".
    public void QuitToLevels()
    {
        Time.timeScale = 1f;
        if (touch != null) touch.SetVoiceListening(false);
        if (GameUI.GameFlow.Instance != null) GameUI.GameFlow.Instance.LoadLevelMenu();
    }

    // ------------------------------------------------------------------ pause

    public void Pause()
    {
        if (Current != State.Playing) return;

        Time.timeScale = 0f;                       // freezes road, cars, enemies, bombs, rain, bullets
        if (touch != null) touch.inputEnabled = false;
        SetState(State.Paused);
    }

    public void Resume()
    {
        if (Current != State.Paused) return;

        Time.timeScale = 1f;
        if (touch != null)
        {
            touch.inputEnabled = true;
            touch.Recalibrate();                   // the phone may have moved while paused
        }
        SetState(State.Playing);
    }

    // Back to round 1 with both scores at zero
    public void RestartMatch()
    {
        Round1Driver = 0;
        Round1Gunner = 0;
        Round2Score = 0;
        Round3Score = 0;
        Round4Score = 0;
        Round5Score = 0;
        LastRoundScore = 0;
        LastRoundDriver = 0;
        LastRoundGunner = 0;

        EnterIntro(FirstRound);
    }

    // ------------------------------------------------------------------ helpers

    // Turns the controls and the spawners on or off (the car still rolls to a stop when off)
    void SetPlayActive(bool on)
    {
        car.controlsEnabled = on;
        gun.controlsEnabled = on;
        abilities.controlsEnabled = on;
        spawner.spawning = on;
        enemies.spawning = on;
        pickups.spawning = on;
        if (touch != null) touch.inputEnabled = on;
    }

    // Fresh road: full health, car in the middle, nothing on the road
    void ResetWorld(bool newZone)
    {
        health.ResetHealth();
        car.ResetCar();
        if (newZone && zones != null) zones.ResetZones();
        gun.ResetGun();
        abilities.ResetAbilities();
        spawner.Clear();
        enemies.Clear();
        pickups.Clear();
    }
}
}
