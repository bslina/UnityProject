using UnityEngine;

namespace GameUI
{
    // Progress per minigame (completed, best stars, best score), saved in PlayerPrefs.
    public static class SaveData
    {
        const string Prefix = "progress.";

        public static bool IsCompleted(string gameId) => PlayerPrefs.GetInt(Prefix + gameId + ".done", 0) == 1;
        public static int GetStars(string gameId) => PlayerPrefs.GetInt(Prefix + gameId + ".stars", 0);
        public static int GetBestScore(string gameId) => PlayerPrefs.GetInt(Prefix + gameId + ".score", 0);

        // Keeps the best result. Returns true if this was a new best score.
        public static bool RecordWin(string gameId, int stars, int score)
        {
            PlayerPrefs.SetInt(Prefix + gameId + ".done", 1);
            if (stars > GetStars(gameId)) PlayerPrefs.SetInt(Prefix + gameId + ".stars", stars);

            bool best = score > GetBestScore(gameId);
            if (best) PlayerPrefs.SetInt(Prefix + gameId + ".score", score);

            PlayerPrefs.Save();
            return best;
        }

        public static void ResetAll(MiniGameRegistry registry)
        {
            if (registry == null) return;
            foreach (var game in registry.Games)
            {
                PlayerPrefs.DeleteKey(Prefix + game.id + ".done");
                PlayerPrefs.DeleteKey(Prefix + game.id + ".stars");
                PlayerPrefs.DeleteKey(Prefix + game.id + ".score");
            }
            PlayerPrefs.Save();
        }
    }
}
