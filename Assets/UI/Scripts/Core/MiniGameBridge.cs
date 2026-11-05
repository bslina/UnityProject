using System;

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

        public static void Lose(string detail = "", float delay = 1f)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.ReportResult(false, detail, 0, 0, delay);
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

        internal static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
    }
}
