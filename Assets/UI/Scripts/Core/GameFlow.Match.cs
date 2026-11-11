using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameUI
{
    // One finished round of a Random Match.
    public class MatchRound
    {
        public int GameIndex;
        public int Winner;          // 1, 2, or 0 = draw
        public int Score1, Score2;
        public string Detail;
    }

    // A Random Match: 2 players, 3 random minigames in a row, 1 point per round won.
    // Tie after 3 rounds = a tiebreaker round with a game not played yet (if there is one left).
    public class RandomMatchState
    {
        public const int Rounds = 3;

        public readonly string[] Names = { "Player 1", "Player 2" };
        public readonly List<int> Games = new List<int>();     // registry indices, in play order
        public readonly int[] Points = new int[2];
        public readonly List<MatchRound> Results = new List<MatchRound>();
        public int Round;                 // index in Games of the round being played next
        public bool Drawn;                // the draw (slot machine) has been shown
        public bool Finished;

        // Solo games (e.g. Egg Run): each player plays one turn.
        public int SoloTurn;              // 0 = first player's turn, 1 = second player's turn
        public readonly int[] SoloScores = new int[2];
        public readonly bool[] SoloWon = new bool[2];
        public readonly string[] SoloDetails = new string[2];

        public int CurrentGame => Round < Games.Count ? Games[Round] : -1;
        public bool IsTiebreaker => Round >= Rounds;
        public int Winner => Points[0] > Points[1] ? 1 : Points[1] > Points[0] ? 2 : 0;
        public string Name(int player) => Names[Mathf.Clamp(player, 1, 2) - 1];
    }

    public partial class GameFlow
    {
        [Header("Random Match")]
        [SerializeField] string randomMatchScene = "RandomMatch";

        // Null when no Random Match is being played.
        public RandomMatchState Match { get; private set; }
        public bool InMatch => Match != null && !Match.Finished;

        // Levels button: starts a Random Match with the current players and shows the draw.
        public void OpenRandomMatch()
        {
            StartRandomMatch(PlayerName(1), PlayerName(2));
            LoadScene(randomMatchScene);
        }

        // Starts a new match and draws 3 different random games (the Random Match screen shows the draw).
        public void StartRandomMatch(string player1, string player2)
        {
            Match = new RandomMatchState();
            Match.Names[0] = string.IsNullOrWhiteSpace(player1) ? "Player 1" : player1.Trim();
            Match.Names[1] = string.IsNullOrWhiteSpace(player2) ? "Player 2" : player2.Trim();
            var pool = MatchCandidates();
            Shuffle(pool);
            for (int i = 0; i < Mathf.Min(RandomMatchState.Rounds, pool.Count); i++) Match.Games.Add(pool[i]);
        }

        // Plays the current round (or the second player's turn of a solo game).
        public void PlayMatchRound()
        {
            if (Match == null || Match.CurrentGame < 0) return;
            var game = Registry.Get(Match.CurrentGame);
            if (game != null) LoadScene(game.sceneName);
        }

        List<int> MatchCandidates()
        {
            var list = new List<int>();
            if (Registry == null) return list;
            for (int i = 0; i < Registry.Count; i++)
            {
                var g = Registry.Get(i);
                if (g != null && g.IsPlayable && g.inRandomMatch) list.Add(i);
            }
            return list;
        }

        static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---------- results from the minigames ----------

        // A 2-player minigame finished (MiniGameBridge.ReportDuel).
        public void ReportDuel(int winner, int score1, int score2, string detail, float delay)
        {
            if (!InMatch || ResultShown || IsLoading) return;
            FinishRound(new MatchRound
            {
                GameIndex = Match.CurrentGame, Winner = Mathf.Clamp(winner, 0, 2),
                Score1 = score1, Score2 = score2, Detail = detail,
            }, delay);
        }

        // One player's turn of a solo minigame finished (its Win / Lose call).
        void ReportSoloTurn(bool won, string detail, int score, float delay)
        {
            int turn = Match.SoloTurn;
            Match.SoloScores[turn] = score;
            Match.SoloWon[turn] = won;
            Match.SoloDetails[turn] = (won ? "" : "Lost: ") + detail;

            if (turn == 0)
            {
                Match.SoloTurn = 1;                 // the Random Match screen asks to pass the phone
                ShowMatchScreenAfter(delay);
                return;
            }

            int s1 = Match.SoloScores[0], s2 = Match.SoloScores[1];
            long r1 = TurnRank(Match.SoloWon[0], s1), r2 = TurnRank(Match.SoloWon[1], s2);
            FinishRound(new MatchRound
            {
                GameIndex = Match.CurrentGame, Winner = r1 > r2 ? 1 : r2 > r1 ? 2 : 0, Score1 = s1, Score2 = s2,
                Detail = Match.Name(1) + ": " + Match.SoloDetails[0] + "   •   " + Match.Name(2) + ": " + Match.SoloDetails[1],
            }, delay);
        }

        void FinishRound(MatchRound round, float delay)
        {
            Match.Results.Add(round);
            if (round.Winner > 0) Match.Points[round.Winner - 1]++;
            Match.Round++;
            Match.SoloTurn = 0;

            if (Match.Round >= Match.Games.Count)
            {
                // Tie: one more round with a game not played yet. No game left = the match is a draw.
                if (Match.Points[0] == Match.Points[1])
                {
                    var unused = MatchCandidates();
                    unused.RemoveAll(i => Match.Games.Contains(i));
                    if (unused.Count > 0) Match.Games.Add(unused[Random.Range(0, unused.Count)]);
                    else Match.Finished = true;
                }
                else Match.Finished = true;
            }
            ShowMatchScreenAfter(delay);
        }

        void ShowMatchScreenAfter(float delay)
        {
            ResultShown = true;
            if (overlay != null) overlay.Refresh();
            StartCoroutine(MatchScreenRoutine(delay));
        }

        IEnumerator MatchScreenRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (IsPaused) Resume();
            Time.timeScale = 0f; // freeze the minigame during the fade
            LoadScene(randomMatchScene);
        }
    }
}
