using System.Collections.Generic;
using System.IO;
using GameUI;
using SoundCopy.AI;
using SoundCopy.Audio;
using SoundCopy.UI;
using UnityEngine;

namespace SoundCopy
{
    // Match flow (one round):
    //   Menu -> Setup (names, category) -> Round -> SPIN THE PHONE -> Video -> HandOver(first) -> Recording(first)
    //   -> HandOver(second) -> Recording(second) -> AI analysis -> Verdict -> next Round ... -> Results
    // Spin: the phone is spun flat on the table (gyroscope). Where its top points when it stops picks who
    // imitates first; a fast spin turns the round into a x2 round. Recording starts by raising the phone.
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameState currentState = GameState.Menu;

        [Header("Panels")]
        [SerializeField] private MenuPanel menuPanel;
        [SerializeField] private SetupPanel setupPanel;
        [SerializeField] private RoundPanel roundPanel;
        [SerializeField] private VideoPanel videoPanel;
        [SerializeField] private HandOverPanel handOverPanel;
        [SerializeField] private RecordingPanel recordingPanel;
        [SerializeField] private AIAnalysisPanel aiAnalysisPanel;
        [SerializeField] private VerdictPanel verdictPanel;
        [SerializeField] private ResultsPanel resultsPanel;
        [Tooltip("Spin-the-phone screen (added by SoundCopy > Add Spin Screen). Without it, P1 always starts.")]
        [SerializeField] private SpinPanel spinPanel;
        [SerializeField] private PauseOverlay pauseOverlay;

        [Header("Services")]
        [SerializeField] private OnDeviceAIJudge aiJudge;
        [SerializeField] private ShakeDetector shakeDetector;

        [Header("Rules")]
        [Tooltip("Points for winning a round. A x2 round (fast spin) doubles them.")]
        [SerializeField] private int pointsPerRound = 1;

        [Header("Match (runtime)")]
        [SerializeField] private string player1Name = "Player 1";
        [SerializeField] private string player2Name = "Player 2";
        [SerializeField] private int roundCount = 3;
        [SerializeField] private int currentRound = 1;
        [SerializeField] private ChallengeCategory category = ChallengeCategory.International;
        [SerializeField] private int player1Points;
        [SerializeField] private int player2Points;
        [SerializeField] private List<RoundRecord> history = new List<RoundRecord>();

        [Header("Round (runtime)")]
        [SerializeField] private string currentReferenceId;
        [SerializeField] private float currentChallengeDuration = 4f;
        [SerializeField] private AudioClip player1Recording;
        [SerializeField] private AudioClip player2Recording;
        [SerializeField] private bool player1RecordedThisRound;
        [SerializeField] private bool player2RecordedThisRound;
        [SerializeField] private int firstPlayer = 1;
        [SerializeField] private bool doubleRound;

        public GameState CurrentState => currentState;
        public string Player1Name => string.IsNullOrWhiteSpace(player1Name) ? "Player 1" : player1Name;
        public string Player2Name => string.IsNullOrWhiteSpace(player2Name) ? "Player 2" : player2Name;
        public string PlayerName(int player) => player == 1 ? Player1Name : Player2Name;
        public int RoundCount => roundCount;
        public int CurrentRound => currentRound;
        public bool IsLastRound => currentRound >= roundCount;
        public int RoundsLeft => roundCount - currentRound + 1;
        public ChallengeCategory Category => category;
        public string CategoryLabel => category == ChallengeCategory.Tunisian ? "TUNISIAN" : category == ChallengeCategory.Mixed ? "MIXED" : "INTERNATIONAL";
        public AudioClip Player1Recording => player1Recording;
        public AudioClip Player2Recording => player2Recording;
        public string CurrentReferenceId => currentReferenceId;
        public float CurrentChallengeDuration => currentChallengeDuration;
        public OnDeviceAIJudge AIJudge => aiJudge;
        public int FirstPlayer => firstPlayer;
        public bool DoubleRound => doubleRound;
        public bool Recorded(int player) => player == 1 ? player1RecordedThisRound : player2RecordedThisRound;
        public int Player1Score => player1Points;
        public int Player2Score => player2Points;
        public int Points(int player) => player == 1 ? player1Points : player2Points;
        public IReadOnlyList<RoundRecord> History => history;
        public RoundRecord LastRound => history.Count > 0 ? history[history.Count - 1] : null;


        // Average AI accuracy of a player over the match (0-100).
        public float AverageAccuracy(int player)
        {
            if (history.Count == 0) return 0f;
            float sum = 0f;
            foreach (var r in history) sum += player == 1 ? r.player1Percent : r.player2Percent;
            return sum / history.Count;
        }

        // 0 = draw, 1 or 2 = match winner (points first, accuracy breaks a tie).
        public int MatchWinner
        {
            get
            {
                if (player1Points != player2Points) return player1Points > player2Points ? 1 : 2;
                float a = AverageAccuracy(1), b = AverageAccuracy(2);
                if (Mathf.Abs(a - b) < 1f) return 0;
                return a > b ? 1 : 2;
            }
        }

        // ---------------------------------------------------------------- wiring (scene builder)

        public void Configure(MenuPanel menu, SetupPanel setup, RoundPanel round, VideoPanel video, HandOverPanel handOver,
            RecordingPanel recording, AIAnalysisPanel analysis, VerdictPanel verdict, ResultsPanel results,
            PauseOverlay pause, OnDeviceAIJudge judge, ShakeDetector shake)
        {
            menuPanel = menu;
            setupPanel = setup;
            roundPanel = round;
            videoPanel = video;
            handOverPanel = handOver;
            recordingPanel = recording;
            aiAnalysisPanel = analysis;
            verdictPanel = verdict;
            resultsPanel = results;
            pauseOverlay = pause;
            aiJudge = judge;
            shakeDetector = shake;
        }

        public void ConfigureSpin(SpinPanel spin) => spinPanel = spin;

        private void Start()
        {
            // Pocket Jungle: the shared pause menu replaces Sound Copy's own (the video pauses with it).
            MiniGameBridge.PauseChanged += OnSharedPause;
            PhoneMotion.Warm();
            UseSharedNames();
            Show(GameState.Menu);
        }

        // ---------------------------------------------------------------- menu + setup

        public void OnPlayButton()
        {
            UseSharedNames();
            setupPanel.Open(Player1Name, Player2Name, category);
            Show(GameState.Setup);
        }

        // Pocket Jungle: the 2 players typed in the Players scene (or the Random Match's players).
        private void UseSharedNames()
        {
            if (!MiniGameBridge.IsAvailable) return;
            player1Name = MiniGameBridge.PlayerName(1);
            player2Name = MiniGameBridge.PlayerName(2);
        }

        public void OnSetupBack() => Show(GameState.Menu);

        public void OnSetupConfirmed(string firstName, string secondName, ChallengeCategory selectedCategory)
        {
            player1Name = string.IsNullOrWhiteSpace(firstName) ? "Player 1" : firstName.Trim();
            player2Name = string.IsNullOrWhiteSpace(secondName) ? "Player 2" : secondName.Trim();
            category = selectedCategory;
            // Pocket Jungle: a name edited here becomes the players' name everywhere (other games, results).
            if (MiniGameBridge.IsAvailable && GameFlow.Instance != null && !MiniGameBridge.InMatch &&
                (player1Name != MiniGameBridge.PlayerName(1) || player2Name != MiniGameBridge.PlayerName(2)))
                GameFlow.Instance.SetPlayers(player1Name, player2Name);
            StartMatch();
        }

        // Leaves the minigame: back to the big game's level menu (or Sound Copy's own menu when played alone).
        public void ExitToGame()
        {
            if (MiniGameBridge.IsAvailable && GameFlow.Instance != null) GameFlow.Instance.LoadLevelMenu();
            else Show(GameState.Menu);
        }

        public bool InsideBigGame => MiniGameBridge.IsAvailable;

        // ---------------------------------------------------------------- match

        private void StartMatch()
        {
            // A match is one round (spin -> video -> both imitations -> AI verdict -> results).
            roundCount = 1;
            currentRound = 1;
            player1Points = player2Points = 0;
            history.Clear();
            BeginRound();
        }

        private void BeginRound()
        {
            player1RecordedThisRound = player2RecordedThisRound = false;
            firstPlayer = 1;
            doubleRound = false;
            player1Recording = player2Recording = null;
            // No "round" screen any more: straight to the spin wheel (who goes first), then the video.
            OnWatchVideo();
        }

        // Spin the phone first (if the spin screen exists), then the video.
        public void OnWatchVideo()
        {
            if (spinPanel != null)
            {
                spinPanel.Begin();
                Show(GameState.Spin);
                return;
            }
            OnSpinDone(1, false);
        }

        public void OnSpinBack() => Show(GameState.Setup);

        // From the spin screen: who goes first and whether this is a x2 round.
        public void OnSpinDone(int first, bool doubled)
        {
            firstPlayer = first == 2 ? 2 : 1;
            doubleRound = doubled;
            Show(GameState.Video);
            videoPanel.PlayForCategory(category);
            currentReferenceId = videoPanel.CurrentClipId;
            currentChallengeDuration = Mathf.Max(1f, videoPanel.CurrentClipDuration);
        }

        public void OnVideoBack() => Show(spinPanel != null ? GameState.Spin : GameState.Setup);

        int SecondPlayer => 3 - firstPlayer;

        // Next player who still has to imitate this round.
        int NextToRecord => !Recorded(firstPlayer) ? firstPlayer : SecondPlayer;

        public void OnVideoContinue() => ShowHandOver(NextToRecord);

        private void ShowHandOver(int player)
        {
            handOverPanel.Prepare(player, PlayerName(player), doubleRound);
            Show(GameState.HandOver);
        }

        public void OnHandOverReady(int player)
        {
            recordingPanel.Prepare(player, PlayerName(player));
            Show(GameState.Recording);
        }

        public void OnRecordingBack() => Show(GameState.Video);

        public bool StoreRecording(int playerIndex, AudioClip clip)
        {
            string fileName = playerIndex == 1 ? "player1.wav" : "player2.wav";
            string directory;
#if UNITY_EDITOR
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            directory = Path.Combine(projectRoot != null ? projectRoot.FullName : Application.dataPath, "SoundCopyRecordings");
#else
            directory = Path.Combine(Application.persistentDataPath, "SoundCopy");
#endif
            string path = Path.Combine(directory, fileName);
            if (playerIndex == 1) player1Recording = clip; else player2Recording = clip;
            try
            {
                WavUtility.Save(clip, path); // kept for debugging the AI; the game itself uses the clip in memory
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Sound Copy: WAV not saved at " + path + " (" + exception.Message + ")");
            }
            return clip != null;
        }

        public void OnRecordingDone(int player)
        {
            if (player == 1) player1RecordedThisRound = true;
            else player2RecordedThisRound = true;
            if (!Recorded(1) || !Recorded(2))
            {
                ShowHandOver(NextToRecord);
                return;
            }
            Show(GameState.AIAnalysis);
            aiAnalysisPanel.BeginAnalysis();
        }

        // "Redo" after an AI error: the second player records again.
        public void OnAnalysisBack()
        {
            if (SecondPlayer == 1) player1RecordedThisRound = false; else player2RecordedThisRound = false;
            ShowHandOver(SecondPlayer);
        }

        // Called by the analysis screen with the AI result.
        public void RegisterRoundResult(AIJudgeResult result)
        {
            int winner = result.Winner;
            int points = winner == 0 ? 0 : pointsPerRound * (doubleRound ? 2 : 1);
            if (winner == 1) player1Points += points;
            else if (winner == 2) player2Points += points;

            history.Add(new RoundRecord
            {
                roundNumber = currentRound,
                clipId = currentReferenceId,
                player1Percent = result.Player1Score,
                player2Percent = result.Player2Score,
                player1TooQuiet = result.Player1TooQuiet,
                player2TooQuiet = result.Player2TooQuiet,
                winner = winner,
                firstPlayer = firstPlayer,
                doubled = doubleRound,
                points = points,
            });

            verdictPanel.Show(LastRound);
            Show(GameState.Verdict);
        }

        public void OnVerdictContinue()
        {
            if (!IsLastRound)
            {
                currentRound++;
                BeginRound();
                return;
            }
            // Pocket Jungle: one round, so the verdict leads straight to the shared result screen.
            if (MiniGameBridge.IsAvailable) { OnResultsContinue(); return; }
            resultsPanel.Show();
            Show(GameState.Results);
        }

        public void OnRematch() => StartMatch();

        // End of match: hand over to the big game's Win screen (stars from the winner's accuracy).
        public void OnResultsContinue()
        {
            if (!MiniGameBridge.IsAvailable)
            {
                Show(GameState.Menu);
                return;
            }
            int winner = MatchWinner;
            string detail = winner == 0
                ? $"Draw {player1Points} – {player2Points}"
                : $"{PlayerName(winner)} wins {Points(winner)} – {Points(3 - winner)}";
            float accuracy = winner == 0 ? Mathf.Max(AverageAccuracy(1), AverageAccuracy(2)) : AverageAccuracy(winner);
            int stars = accuracy >= 70f ? 3 : accuracy >= 45f ? 2 : 1;
            // Win screen normally; in a Random Match it counts the round for the winner.
            MiniGameBridge.ReportDuel(winner, player1Points, player2Points, detail, stars, Mathf.RoundToInt(accuracy), 0.2f);
        }

        // ---------------------------------------------------------------- pause

        private void OnDestroy() => MiniGameBridge.PauseChanged -= OnSharedPause;

        private void OnSharedPause(bool paused)
        {
            if (paused) OnPauseOpened();
            else OnPauseClosed();
            if (paused && recordingPanel != null) recordingPanel.CancelTakeForPause();
        }

        public void OnPauseOpened() => videoPanel.PauseForOverlay();
        public void OnPauseClosed() => videoPanel.ResumeAfterOverlay();
        public void RestartCurrentMatch() => StartMatch();
        public void QuitCurrentMatch() => Show(GameState.Menu);

        // ---------------------------------------------------------------- screens

        private void Show(GameState state)
        {
            currentState = state;
            SetVisible(menuPanel, state == GameState.Menu);
            SetVisible(setupPanel, state == GameState.Setup);
            SetVisible(roundPanel, state == GameState.Round);
            SetVisible(videoPanel, state == GameState.Video);
            SetVisible(handOverPanel, state == GameState.HandOver);
            SetVisible(recordingPanel, state == GameState.Recording);
            SetVisible(aiAnalysisPanel, state == GameState.AIAnalysis);
            SetVisible(verdictPanel, state == GameState.Verdict);
            SetVisible(resultsPanel, state == GameState.Results);
            SetVisible(spinPanel, state == GameState.Spin);
            // Music off while the meme video plays and while someone records (the mic would pick it up).
            MiniGameBridge.SetMusicPaused(state == GameState.Video || state == GameState.HandOver
                                          || state == GameState.Recording || state == GameState.AIAnalysis);
            // Pause is always available, on every screen (a take being recorded is cancelled by it,
            // see RecordingPanel; the AI verdict waits for the resume).
            bool canPause = true;
            if (MiniGameBridge.IsAvailable)
            {
                if (pauseOverlay != null) pauseOverlay.SetAvailable(false);   // Sound Copy's own pause: not used
                MiniGameBridge.SetPauseButtonVisible(canPause);              // Pocket Jungle's shared pause
            }
            else if (pauseOverlay != null) pauseOverlay.SetAvailable(canPause);
        }

        private static void SetVisible(Component panel, bool visible)
        {
            if (panel != null) panel.gameObject.SetActive(visible);
        }
    }
}
