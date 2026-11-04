using System.Collections.Generic;
using System.IO;
using GameUI;
using SoundCopy.AI;
using SoundCopy.Audio;
using SoundCopy.UI;
using UnityEngine;

namespace SoundCopy
{
    // Match flow:
    //   Menu -> Setup (names, rounds, category) -> Round -> Video -> HandOver(P1) -> Recording(P1)
    //   -> HandOver(P2) -> Recording(P2) -> AI analysis -> Verdict -> next Round ... -> Results
    // Jokers: once per match, each player can shake the phone on the Round screen to double
    // the points of that round if they win it.
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
        [SerializeField] private PauseOverlay pauseOverlay;

        [Header("Services")]
        [SerializeField] private OnDeviceAIJudge aiJudge;
        [SerializeField] private ShakeDetector shakeDetector;

        [Header("Rules")]
        [Tooltip("Points for winning a round. A joker doubles them.")]
        [SerializeField] private int pointsPerRound = 1;
        [Tooltip("Shakes needed to arm a joker.")]
        [SerializeField] private int shakesForJoker = 3;

        [Header("Match (runtime)")]
        [SerializeField] private string player1Name = "Joueur 1";
        [SerializeField] private string player2Name = "Joueur 2";
        [SerializeField] private int roundCount = 3;
        [SerializeField] private int currentRound = 1;
        [SerializeField] private ChallengeCategory category = ChallengeCategory.International;
        [SerializeField] private int player1Points;
        [SerializeField] private int player2Points;
        [SerializeField] private bool player1JokerUsed;
        [SerializeField] private bool player2JokerUsed;
        [SerializeField] private bool player1JokerThisRound;
        [SerializeField] private bool player2JokerThisRound;
        [SerializeField] private List<RoundRecord> history = new List<RoundRecord>();

        [Header("Round (runtime)")]
        [SerializeField] private string currentReferenceId;
        [SerializeField] private float currentChallengeDuration = 4f;
        [SerializeField] private AudioClip player1Recording;
        [SerializeField] private AudioClip player2Recording;
        [SerializeField] private bool player1RecordedThisRound;

        public GameState CurrentState => currentState;
        public string Player1Name => string.IsNullOrWhiteSpace(player1Name) ? "Joueur 1" : player1Name;
        public string Player2Name => string.IsNullOrWhiteSpace(player2Name) ? "Joueur 2" : player2Name;
        public string PlayerName(int player) => player == 1 ? Player1Name : Player2Name;
        public int RoundCount => roundCount;
        public int CurrentRound => currentRound;
        public bool IsLastRound => currentRound >= roundCount;
        public ChallengeCategory Category => category;
        public string CategoryLabel => category == ChallengeCategory.Tunisian ? "TOUNSI" : category == ChallengeCategory.Mixed ? "MIXTE" : "INTERNATIONAL";
        public AudioClip Player1Recording => player1Recording;
        public AudioClip Player2Recording => player2Recording;
        public string CurrentReferenceId => currentReferenceId;
        public float CurrentChallengeDuration => currentChallengeDuration;
        public OnDeviceAIJudge AIJudge => aiJudge;
        public ShakeDetector Shake => shakeDetector;
        public int ShakesForJoker => Mathf.Max(1, shakesForJoker);
        public int Player1Score => player1Points;
        public int Player2Score => player2Points;
        public int Points(int player) => player == 1 ? player1Points : player2Points;
        public IReadOnlyList<RoundRecord> History => history;
        public RoundRecord LastRound => history.Count > 0 ? history[history.Count - 1] : null;

        public bool JokerUsed(int player) => player == 1 ? player1JokerUsed : player2JokerUsed;
        public bool JokerActive(int player) => player == 1 ? player1JokerThisRound : player2JokerThisRound;

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

        private void Start() => Show(GameState.Menu);

        // ---------------------------------------------------------------- menu + setup

        public void OnPlayButton()
        {
            setupPanel.Open(Player1Name, Player2Name, roundCount, category);
            Show(GameState.Setup);
        }

        public void OnSetupBack() => Show(GameState.Menu);

        public void OnSetupConfirmed(string firstName, string secondName, int rounds, ChallengeCategory selectedCategory)
        {
            player1Name = string.IsNullOrWhiteSpace(firstName) ? "Joueur 1" : firstName.Trim();
            player2Name = string.IsNullOrWhiteSpace(secondName) ? "Joueur 2" : secondName.Trim();
            roundCount = rounds == 1 || rounds == 3 || rounds == 5 ? rounds : 3;
            category = selectedCategory;
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
            currentRound = 1;
            player1Points = player2Points = 0;
            player1JokerUsed = player2JokerUsed = false;
            history.Clear();
            BeginRound();
        }

        private void BeginRound()
        {
            player1JokerThisRound = player2JokerThisRound = false;
            player1RecordedThisRound = false;
            player1Recording = player2Recording = null;
            roundPanel.Refresh();
            Show(GameState.Round);
        }

        // Called by the Round screen once the player has shaken the phone enough.
        public bool ArmJoker(int player)
        {
            if (JokerUsed(player)) return false;
            if (player == 1) { player1JokerUsed = true; player1JokerThisRound = true; }
            else { player2JokerUsed = true; player2JokerThisRound = true; }
            Haptics.Pulse(200);
            return true;
        }

        public void OnWatchVideo()
        {
            Show(GameState.Video);
            videoPanel.PlayForCategory(category);
            currentReferenceId = videoPanel.CurrentClipId;
            currentChallengeDuration = Mathf.Max(1f, videoPanel.CurrentClipDuration);
        }

        public void OnVideoBack() => Show(GameState.Round);

        public void OnVideoContinue() => ShowHandOver(player1RecordedThisRound ? 2 : 1);

        private void ShowHandOver(int player)
        {
            handOverPanel.Prepare(player, PlayerName(player), JokerActive(player));
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
            if (player == 1)
            {
                player1RecordedThisRound = true;
                ShowHandOver(2);
                return;
            }
            Show(GameState.AIAnalysis);
            aiAnalysisPanel.BeginAnalysis();
        }

        public void OnAnalysisBack() => ShowHandOver(2);

        // Called by the analysis screen with the AI result.
        public void RegisterRoundResult(AIJudgeResult result)
        {
            int winner = result.Winner;
            bool joker = winner == 1 ? player1JokerThisRound : winner == 2 && player2JokerThisRound;
            int points = winner == 0 ? 0 : pointsPerRound * (joker ? 2 : 1);
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
                player1Joker = player1JokerThisRound,
                player2Joker = player2JokerThisRound,
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
                ? $"Égalité {player1Points} – {player2Points}"
                : $"{PlayerName(winner)} gagne {Points(winner)} – {Points(3 - winner)}";
            float accuracy = winner == 0 ? Mathf.Max(AverageAccuracy(1), AverageAccuracy(2)) : AverageAccuracy(winner);
            int stars = accuracy >= 70f ? 3 : accuracy >= 45f ? 2 : 1;
            MiniGameBridge.Win(detail, stars, Mathf.RoundToInt(accuracy), 0.2f);
        }

        // ---------------------------------------------------------------- pause

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
            if (pauseOverlay != null)
                pauseOverlay.SetAvailable(state == GameState.Round || state == GameState.Video || state == GameState.HandOver || state == GameState.Verdict);
        }

        private static void SetVisible(Component panel, bool visible)
        {
            if (panel != null) panel.gameObject.SetActive(visible);
        }
    }
}
