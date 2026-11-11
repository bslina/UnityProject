using System;
using UnityEngine;

namespace GameUI
{
    // The only thing a minigame needs to talk to the shared menus.
    //
    //   using GameUI;
    //   MiniGameBridge.Win("12.4 s left", stars: 3, score: 124);
    //   MiniGameBridge.Lose("The egg fell!");
    //
    // The shared result screen, pause button, settings and scene loading are handled for you.
    public static class MiniGameBridge
    {
        // True when the shared UI is running (always, unless its prefab is missing from Resources).
        public static bool IsAvailable => GameFlow.Instance != null;

        public static bool IsPaused => GameFlow.Instance != null && GameFlow.Instance.IsPaused;

        // Raised with true when the game is paused and false when it resumes.
        // Time.timeScale is already set to 0 while paused; use this for anything not driven by Time.deltaTime.
        public static event Action<bool> PauseChanged;

        // stars: 0-3 shown on the result screen and level cards. score: any number, best one is saved.
        // delay: seconds (real time) before the result screen opens, so the last moment of play is visible.
        public static void Win(string detail = "", int stars = 3, int score = 0, float delay = 0.75f)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.ReportResult(true, detail, stars, score, delay);
        }

        // score: optional, only used in a Random Match to rank two losing turns of a solo game
        // (e.g. how far the player got).
        public static void Lose(string detail = "", float delay = 1f, int score = 0)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.ReportResult(false, detail, 0, score, delay);
        }

        // ---------- Random Match (2 players, 3 random games) ----------

        // True while this minigame is played as a round of a Random Match: play a SHORT version.
        public static bool InMatch => GameFlow.Instance != null && GameFlow.Instance.InMatch;

        // The names of the 2 players, typed in the Players scene ("Player 1" / "Player 2" when unknown).
        // Use them in your minigame instead of "Player 1" / "Player 2".
        public static string PlayerName(int player)
        {
            var flow = GameFlow.Instance;
            int i = Mathf.Clamp(player, 1, 2) - 1;
            if (flow == null) return "Player " + (i + 1);
            var match = flow.Match;
            if (match != null && !string.IsNullOrWhiteSpace(match.Names[i])) return match.Names[i];
            return flow.PlayerName(i + 1);
        }

        // Solo minigames (played as 2 turns): whose turn it is (1 = first player, 2 = second player).
        public static int TurnPlayer => GameFlow.Instance != null ? GameFlow.Instance.SoloTurn + 1 : 1;

        // End of a 2-player minigame. winner: 1 or 2, 0 = draw.
        // In a Random Match it counts the round; otherwise it opens the Win screen like Win(detail, stars, score).
        public static void ReportDuel(int winner, int player1Score, int player2Score, string detail,
                                      int stars = 3, int score = -1, float delay = 0.75f)
        {
            var flow = GameFlow.Instance;
            if (flow == null) return;
            if (flow.InMatch) flow.ReportDuel(winner, player1Score, player2Score, detail, delay);
            else flow.ReportDuelResult(winner, player1Score, player2Score, detail, stars,
                                       score >= 0 ? score : Math.Max(player1Score, player2Score), delay);
        }

        // Hide the pause button e.g. during a minigame's own intro panel.
        public static void SetPauseButtonVisible(bool visible)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.SetPauseButtonVisible(visible);
        }

        // Silence the minigame's music for a moment (e.g. while recording with the microphone).
        public static void SetMusicPaused(bool paused)
        {
            if (GameFlow.Instance != null && GameFlow.Instance.Audio != null) GameFlow.Instance.Audio.SetMusicPaused(paused);
        }

        // Play another music track from Assets/PocketJungle/Shared/Resources/Audio (e.g. a start screen tune).
        // Empty = back to this minigame's normal music.
        public static void PlayMusic(string name)
        {
            if (GameFlow.Instance != null && GameFlow.Instance.Audio != null) GameFlow.Instance.Audio.PlayMusic(name);
        }

        // Turn this minigame's music down (e.g. 0.5) so its sound effects stand out. Resets on scene change.
        public static void SetMusicVolume(float scale)
        {
            if (GameFlow.Instance != null && GameFlow.Instance.Audio != null) GameFlow.Instance.Audio.SetMusicScale(scale);
        }

        internal static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
    }
}
