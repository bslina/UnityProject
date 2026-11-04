using SoundCopy.UI;
using SoundCopy.Audio;
using SoundCopy.AI;
using System.IO;
using UnityEngine;

namespace SoundCopy
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameState currentState = GameState.Menu;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject namesPanel;
        [SerializeField] private GameObject matchSetupPanel;
        [SerializeField] private GameObject categoryPanel;
        [SerializeField] private GameObject roundPanel;
        [SerializeField] private GameObject videoPanel;
        [SerializeField] private GameObject recordingPanel;
        [SerializeField] private GameObject aiAnalysisPanel;
        [SerializeField] private AudioClip player1Recording;
        [SerializeField] private AudioClip player2Recording;
        [SerializeField] private string player1WavPath;
        [SerializeField] private string player2WavPath;
        [SerializeField] private OnDeviceAIJudge aiJudge;
        [SerializeField] private PauseOverlay pauseOverlay;
        [SerializeField] private string currentReferenceId;
        [SerializeField] private float currentChallengeDuration = 4f;
        [SerializeField] private int player1Score;
        [SerializeField] private int player2Score;

        [SerializeField] private string player1Name = "Joueur 1";
        [SerializeField] private string player2Name = "Joueur 2";
        [SerializeField] private int roundCount = 3;
        [SerializeField] private int currentRound = 1;
        [SerializeField] private ChallengeCategory category = ChallengeCategory.International;

        public GameState CurrentState => currentState;
        public string Player1Name => string.IsNullOrWhiteSpace(player1Name) ? "Joueur 1" : player1Name;
        public string Player2Name => string.IsNullOrWhiteSpace(player2Name) ? "Joueur 2" : player2Name;
        public int RoundCount => roundCount;
        public int CurrentRound => currentRound;
        public ChallengeCategory Category => category;
        public string CategoryLabel => category == ChallengeCategory.Tunisian ? "TOUNSI" : category == ChallengeCategory.Mixed ? "MIXTE" : "INTERNATIONAL";
        public AudioClip Player1Recording => player1Recording;
        public AudioClip Player2Recording => player2Recording;
        public string CurrentReferenceId => currentReferenceId;
        public float CurrentChallengeDuration => currentChallengeDuration;
        public OnDeviceAIJudge AIJudge => aiJudge;
        public int Player1Score => player1Score;
        public int Player2Score => player2Score;

        public void ConfigureAI(OnDeviceAIJudge judge) => aiJudge = judge;
        public void ConfigurePause(PauseOverlay overlay) => pauseOverlay = overlay;

        public void ConfigurePanels(GameObject menu, GameObject names, GameObject matchSetup, GameObject categories, GameObject round, GameObject video, GameObject recording, GameObject analysis)
        {
            menuPanel = menu;
            namesPanel = names;
            matchSetupPanel = matchSetup;
            categoryPanel = categories;
            roundPanel = round;
            videoPanel = video;
            recordingPanel = recording;
            aiAnalysisPanel = analysis;
        }

        private void Start() => Show(GameState.Menu, menuPanel);
        public void OnPlayButton() => Show(GameState.Names, namesPanel);
        public void OnNamesBack() => Show(GameState.Menu, menuPanel);

        public void OnSelectionContinue(string firstName, string secondName, int selectedRounds)
        {
            player1Name = string.IsNullOrWhiteSpace(firstName) ? "Joueur 1" : firstName.Trim();
            player2Name = string.IsNullOrWhiteSpace(secondName) ? "Joueur 2" : secondName.Trim();
            roundCount = selectedRounds == 1 || selectedRounds == 3 || selectedRounds == 5 ? selectedRounds : 3;
            CategoryPanel panel = categoryPanel != null ? categoryPanel.GetComponent<CategoryPanel>() : null;
            panel?.SetMatchInfo(Player1Name, Player2Name, roundCount);
            Show(GameState.Category, categoryPanel);
        }

        public void OnNamesContinue(string firstName, string secondName)
        {
            player1Name = string.IsNullOrWhiteSpace(firstName) ? "Joueur 1" : firstName.Trim();
            player2Name = string.IsNullOrWhiteSpace(secondName) ? "Joueur 2" : secondName.Trim();
            Show(GameState.MatchSetup, matchSetupPanel);
        }

        public void OnMatchSetupBack() => Show(GameState.Names, namesPanel);

        public void OnMatchSetupContinue(int selectedRounds)
        {
            roundCount = selectedRounds == 1 || selectedRounds == 3 || selectedRounds == 5 ? selectedRounds : 3;
            CategoryPanel panel = categoryPanel != null ? categoryPanel.GetComponent<CategoryPanel>() : null;
            panel?.SetPlayerNames(Player1Name, Player2Name);
            Show(GameState.Category, categoryPanel);
        }

        public void OnCategoryBack() => Show(GameState.Names, namesPanel);

        public void OnCategorySelected(ChallengeCategory selectedCategory)
        {
            category = selectedCategory;
            currentRound = 1;
            player1Score = 0;
            player2Score = 0;
            RoundPanel panel = roundPanel != null ? roundPanel.GetComponent<RoundPanel>() : null;
            panel?.Refresh();
            Show(GameState.Round, roundPanel);
        }

        public void OnRoundBack() => Show(GameState.Category, categoryPanel);

        public void OnWatchVideo()
        {
            Show(GameState.Video, videoPanel);
            VideoPanel panel = videoPanel != null ? videoPanel.GetComponent<VideoPanel>() : null;
            panel?.PlayForCategory(category);
            currentReferenceId = panel != null ? panel.CurrentClipId : string.Empty;
            currentChallengeDuration = panel != null ? Mathf.Max(1f, panel.CurrentClipDuration) : 4f;
        }

        public void OnVideoBack()
        {
            VideoPanel panel = videoPanel != null ? videoPanel.GetComponent<VideoPanel>() : null;
            Show(GameState.Round, roundPanel);
        }

        public void OnVideoContinue()
        {
            Show(GameState.RecordingPlayer1, recordingPanel);
            RecordingPanel panel = recordingPanel != null ? recordingPanel.GetComponent<RecordingPanel>() : null;
            panel?.Prepare(1, Player1Name);
        }

        public bool StoreRecording(int playerIndex, AudioClip clip)
        {
            string fileName = playerIndex == 1 ? "player1.wav" : "player2.wav";
            string directory;
#if UNITY_EDITOR
            // Keep large test WAV files beside the project (normally on D:) instead
            // of filling Windows' LocalLow folder on the system drive.
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            directory = Path.Combine(projectRoot != null ? projectRoot.FullName : Application.dataPath, "SoundCopyRecordings");
#else
            directory = Path.Combine(Application.persistentDataPath, "SoundCopy");
#endif
            string path = Path.Combine(directory, fileName);
            try
            {
                WavUtility.Save(clip, path);
                if (playerIndex == 1) { player1Recording = clip; player1WavPath = path; }
                else { player2Recording = clip; player2WavPath = path; }
                Debug.Log("Sound Copy: WAV saved at " + path);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Sound Copy: unable to save WAV at " + path + " — " + exception.Message);
                return false;
            }
        }

        public void OnRecordingContinue(int playerIndex)
        {
            RecordingPanel panel = recordingPanel != null ? recordingPanel.GetComponent<RecordingPanel>() : null;
            if (playerIndex == 1)
            {
                currentState = GameState.RecordingPlayer2;
                panel?.Prepare(2, Player2Name);
            }
            else
            {
                Show(GameState.AIAnalysis, aiAnalysisPanel);
                AIAnalysisPanel analysis = aiAnalysisPanel != null ? aiAnalysisPanel.GetComponent<AIAnalysisPanel>() : null;
                analysis?.BeginAnalysis();
                Debug.Log($"Sound Copy: recordings ready for AI — {player1WavPath} | {player2WavPath}");
            }
        }

        public void RegisterAIResult(int winner)
        {
            if (winner == 1) player1Score++;
            else if (winner == 2) player2Score++;
        }

        public void OnAnalysisContinue()
        {
            if (currentRound < roundCount)
            {
                currentRound++;
                RoundPanel panel = roundPanel != null ? roundPanel.GetComponent<RoundPanel>() : null;
                panel?.Refresh();
                Show(GameState.Round, roundPanel);
            }
            else
            {
                currentState = GameState.Results;
                Show(GameState.Menu, menuPanel);
            }
        }

        public void OnRecordingBack() => Show(GameState.Video, videoPanel);
        public void OnAnalysisBack()
        {
            Show(GameState.RecordingPlayer2, recordingPanel);
            RecordingPanel panel = recordingPanel != null ? recordingPanel.GetComponent<RecordingPanel>() : null;
            panel?.Prepare(2, Player2Name);
        }

        public void OnPauseOpened()
        {
            VideoPanel panel = videoPanel != null ? videoPanel.GetComponent<VideoPanel>() : null;
            panel?.PauseForOverlay();
        }

        public void OnPauseClosed()
        {
            VideoPanel panel = videoPanel != null ? videoPanel.GetComponent<VideoPanel>() : null;
            panel?.ResumeAfterOverlay();
        }

        public void RestartCurrentMatch()
        {
            player1Score = 0;
            player2Score = 0;
            currentRound = 1;
            RoundPanel panel = roundPanel != null ? roundPanel.GetComponent<RoundPanel>() : null;
            panel?.Refresh();
            Show(GameState.Round, roundPanel);
        }

        public void QuitCurrentMatch()
        {
            player1Score = 0;
            player2Score = 0;
            currentRound = 1;
            Show(GameState.Menu, menuPanel);
        }

        private void Show(GameState state, GameObject target)
        {
            currentState = state;
            SetVisible(menuPanel, target);
            SetVisible(namesPanel, target);
            SetVisible(matchSetupPanel, target);
            SetVisible(categoryPanel, target);
            SetVisible(roundPanel, target);
            SetVisible(videoPanel, target);
            SetVisible(recordingPanel, target);
            SetVisible(aiAnalysisPanel, target);
            pauseOverlay?.SetAvailable(state == GameState.Round || state == GameState.Video);
        }

        private static void SetVisible(GameObject panel, GameObject target)
        {
            if (panel != null) panel.SetActive(panel == target);
        }
    }
}
