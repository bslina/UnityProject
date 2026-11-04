using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUI
{
    [Serializable]
    public class MiniGameInfo
    {
        [Tooltip("Unique key used for saving progress. Never change it once players have saves.")]
        public string id = "new_game";
        public string displayName = "New Game";
        [TextArea(2, 4)] public string description;
        [Tooltip("Exact scene name (without .unity). The scene must be in Build Profiles > Scene List.")]
        public string sceneName;
        [Tooltip("Card picture in the level menu (optional).")]
        public Sprite icon;
        [Tooltip("Music while this minigame runs (optional; leave empty for silence).")]
        public AudioClip music;
        [Tooltip("Tick while the minigame isn't integrated yet: shows a 'Coming soon' card.")]
        public bool comingSoon;

        public bool IsPlayable => !comingSoon && !string.IsNullOrEmpty(sceneName);
    }

    // The list of minigames that make up the big game, in play order.
    // Lives in Assets/UI/Resources/MiniGameRegistry.asset; add a colleague's game by adding an entry.
    [CreateAssetMenu(menuName = "Game UI/MiniGame Registry", fileName = "MiniGameRegistry")]
    public class MiniGameRegistry : ScriptableObject
    {
        [SerializeField] string mainMenuScene = "MainMenu";
        [Tooltip("If on, a minigame is locked until the one before it has been won.")]
        [SerializeField] bool lockUntilPreviousWon = true;
        [SerializeField] List<MiniGameInfo> games = new List<MiniGameInfo>();

        public string MainMenuScene => mainMenuScene;
        public IReadOnlyList<MiniGameInfo> Games => games;
        public int Count => games.Count;

        static MiniGameRegistry instance;
        public static MiniGameRegistry Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<MiniGameRegistry>("MiniGameRegistry");
                return instance;
            }
        }

        public MiniGameInfo Get(int index) => index >= 0 && index < games.Count ? games[index] : null;

        public int IndexOfScene(string sceneName)
        {
            for (int i = 0; i < games.Count; i++)
                if (games[i].IsPlayable && games[i].sceneName == sceneName) return i;
            return -1;
        }

        public bool IsUnlocked(int index)
        {
            if (index <= 0 || !lockUntilPreviousWon) return true;
            // Walk back to the previous real game, skipping "coming soon" slots.
            for (int i = index - 1; i >= 0; i--)
                if (games[i].IsPlayable) return SaveData.IsCompleted(games[i].id);
            return true;
        }

        // Next playable game after this one, or -1 if this was the last.
        public int NextPlayable(int index)
        {
            for (int i = index + 1; i < games.Count; i++)
                if (games[i].IsPlayable) return i;
            return -1;
        }

        // Where "Play" starts: the first unlocked game not yet won, else the first game.
        public int CampaignStart()
        {
            int first = -1;
            for (int i = 0; i < games.Count; i++)
            {
                if (!games[i].IsPlayable) continue;
                if (first < 0) first = i;
                if (IsUnlocked(i) && !SaveData.IsCompleted(games[i].id)) return i;
            }
            return first;
        }
    }
}
