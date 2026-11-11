using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameUI
{
    // Pocket Jungle is a 2-player party game:
    //  - Players: the duo playing now, typed once in the Players scene (remembered between launches),
    //    plus the recent duos for one-tap picking. Every minigame reads them (MiniGameBridge.PlayerName).
    //  - Solo minigames (Registry: Two Player unticked, e.g. Egg Run) are played as 2 turns:
    //    player 1, the TurnHandover scene ("pass the phone"), player 2, then the result screen.
    //  - 2-player minigames report the winner with MiniGameBridge.ReportDuel.
    public partial class GameFlow
    {
        [Header("Players")]
        [SerializeField] string playersScene = "Players";
        [SerializeField] string turnHandoverScene = "TurnHandover";
        [Tooltip("How many recent duos the Players scene offers.")]
        [SerializeField] int recentDuosKept = 6;

        const string Name1Key = "players.name1", Name2Key = "players.name2", RecentKey = "players.recent";

        readonly string[] players = { "Player 1", "Player 2" };
        bool playersLoaded;

        public string PlayerName(int player)
        {
            LoadPlayers();
            return players[Mathf.Clamp(player, 1, 2) - 1];
        }

        void LoadPlayers()
        {
            if (playersLoaded) return;
            playersLoaded = true;
            players[0] = PlayerPrefs.GetString(Name1Key, "Player 1");
            players[1] = PlayerPrefs.GetString(Name2Key, "Player 2");
        }

        public void SetPlayers(string name1, string name2)
        {
            LoadPlayers();
            players[0] = string.IsNullOrWhiteSpace(name1) ? "Player 1" : name1.Trim();
            players[1] = string.IsNullOrWhiteSpace(name2) ? "Player 2" : name2.Trim();
            PlayerPrefs.SetString(Name1Key, players[0]);
            PlayerPrefs.SetString(Name2Key, players[1]);

            // Recent duos, newest first, the same pair only once (whatever the order).
            var duos = RecentDuos();
            duos.RemoveAll(d => SamePair(d.Item1, d.Item2, players[0], players[1]));
            duos.Insert(0, (players[0], players[1]));
            if (duos.Count > recentDuosKept) duos.RemoveRange(recentDuosKept, duos.Count - recentDuosKept);
            var sb = new System.Text.StringBuilder();
            foreach (var d in duos) sb.Append(d.Item1).Append('|').Append(d.Item2).Append('\n');
            PlayerPrefs.SetString(RecentKey, sb.ToString());
            PlayerPrefs.Save();
        }

        public List<(string, string)> RecentDuos()
        {
            var list = new List<(string, string)>();
            foreach (var line in PlayerPrefs.GetString(RecentKey, "").Split('\n'))
            {
                var parts = line.Split('|');
                if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0) list.Add((parts[0], parts[1]));
            }
            return list;
        }

        static bool SamePair(string a, string b, string c, string d) =>
            (string.Equals(a, c, System.StringComparison.OrdinalIgnoreCase) && string.Equals(b, d, System.StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(a, d, System.StringComparison.OrdinalIgnoreCase) && string.Equals(b, c, System.StringComparison.OrdinalIgnoreCase));

        // Main menu PLAY / Levels "change players": the "Who's playing?" scene.
        public void OpenPlayers()
        {
            Match = null;
            LoadScene(playersScene);
        }

        // ---------- solo minigames as 2 turns ----------

        int soloTurn;
        readonly bool[] soloWon = new bool[2];
        readonly int[] soloScores = new int[2];
        readonly int[] soloStars = new int[2];
        readonly string[] soloDetails = new string[2];

        // Whose turn it is in a solo minigame: 0 = player 1, 1 = player 2.
        public int SoloTurn => InMatch ? Match.SoloTurn : soloTurn;
        // What player 1 did on their turn (shown on the hand-over screen).
        public string FirstTurnDetail => InMatch ? Match.SoloDetails[0] : soloDetails[0];

        void ResetSoloTurns() => soloTurn = 0;

        // Ranks a turn: any win beats any loss, then the higher score.
        static long TurnRank(bool won, int score) => (won ? 1_000_000L : 0L) + score;

        void ReportSoloTurnFree(bool won, string detail, int stars, int score, float delay)
        {
            int turn = soloTurn;
            soloWon[turn] = won;
            soloScores[turn] = score;
            soloStars[turn] = won ? stars : 0;
            soloDetails[turn] = won ? detail : "Lost: " + detail;

            if (turn == 0)
            {
                soloTurn = 1;
                ShowSceneAfter(delay, turnHandoverScene);
                return;
            }

            long r1 = TurnRank(soloWon[0], soloScores[0]), r2 = TurnRank(soloWon[1], soloScores[1]);
            int winner = r1 > r2 ? 1 : r2 > r1 ? 2 : 0;
            string detailLine = PlayerName(1) + ": " + soloDetails[0] + "\n" + PlayerName(2) + ": " + soloDetails[1];
            SetDuelResult(winner, soloScores[0], soloScores[1], detailLine, "",
                          winner > 0 ? soloStars[winner - 1] : Mathf.Max(soloStars[0], soloStars[1]),
                          winner > 0 ? soloScores[winner - 1] : Mathf.Max(soloScores[0], soloScores[1]));
            soloTurn = 0;
            ShowSceneAfter(delay, WinScene);
        }

        // Hand-over screen "I'm ready": the same minigame again, for player 2.
        public void PlaySecondTurn()
        {
            var game = Registry != null ? Registry.Get(LastGameIndex) : null;
            if (game != null) LoadScene(game.sceneName);
            else LoadLevelMenu();
        }

        // ---------- 2-player minigames (outside a Random Match) ----------

        public void ReportDuelResult(int winner, int score1, int score2, string detail, int stars, int score, float delay)
        {
            if (ResultShown || IsLoading) return;
            string scoreLine = PlayerName(1) + "  " + score1 + "  -  " + score2 + "  " + PlayerName(2);
            SetDuelResult(Mathf.Clamp(winner, 0, 2), score1, score2, detail, scoreLine, stars, score);
            ShowSceneAfter(delay, WinScene);
        }

        void SetDuelResult(int winner, int score1, int score2, string detail, string scoreLine, int stars, int score)
        {
            var game = CurrentGame;
            stars = Mathf.Clamp(stars, 0, 3);
            bool newBest = game != null && SaveData.RecordWin(game.id, stars, score);
            LastResult = new GameResult
            {
                Won = true, Detail = detail, Stars = stars, Score = score, NewBest = newBest, GameIndex = CurrentIndex,
                IsDuel = true, Winner = winner, Score1 = score1, Score2 = score2,
                Player1 = PlayerName(1), Player2 = PlayerName(2), ScoreLine = scoreLine,
            };
        }

        void ShowSceneAfter(float delay, string sceneName)
        {
            ResultShown = true;
            if (overlay != null) overlay.Refresh();
            StartCoroutine(ShowSceneRoutine(delay, sceneName));
        }

        IEnumerator ShowSceneRoutine(float delay, string sceneName)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (IsPaused) Resume();
            Time.timeScale = 0f; // freeze the minigame during the fade
            LoadScene(sceneName);
        }
    }
}
