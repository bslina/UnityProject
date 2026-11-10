using System;
using UnityEngine;
namespace DuoRoad
{

// The ONE entry point the 5-game app will use to collect this mini-game's score.
//   ScoreBridge.Report("RoadDuo", finalScore);
// The main app can listen to OnScoreReported to add scores together.
public static class ScoreBridge
{
    public static event Action<string, int> OnScoreReported;

    public static int LastScore { get; private set; }

    public static void Report(string gameName, int score)
    {
        LastScore = score;

        int best = PlayerPrefs.GetInt("best_" + gameName, 0);
        if (score > best)
        {
            PlayerPrefs.SetInt("best_" + gameName, score);
            PlayerPrefs.Save();
        }

        Debug.Log("[ScoreBridge] " + gameName + " score = " + score);
        OnScoreReported?.Invoke(gameName, score);
    }

    public static int Best(string gameName)
    {
        return PlayerPrefs.GetInt("best_" + gameName, 0);
    }
}
}
