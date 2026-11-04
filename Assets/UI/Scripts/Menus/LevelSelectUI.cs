using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // LevelSelect scene: one card per entry in the MiniGame Registry (Assets/UI/Resources/MiniGameRegistry.asset).
    public class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] LevelButton cardPrefab;
        [Tooltip("Parent of the cards (has a layout group).")]
        [SerializeField] Transform cardContainer;
        [SerializeField] Button backButton;
        [Tooltip("Optional: opens the settings on top of this menu.")]
        [SerializeField] Button settingsButton;

        static readonly List<LevelSelectUI> open = new List<LevelSelectUI>();
        readonly List<LevelButton> cards = new List<LevelButton>();

        void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => GameFlow.Instance.LoadMainMenu());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => GameFlow.Instance.OpenSettings());
        }

        void OnEnable()
        {
            open.Add(this);
            Refresh();
        }

        void OnDisable() => open.Remove(this);

        // Called after "Reset progress" in the settings so the stars disappear.
        public static void RefreshAll()
        {
            foreach (var menu in open) menu.Refresh();
        }

        public void Refresh()
        {
            var registry = MiniGameRegistry.Instance;
            if (registry == null || cardPrefab == null || cardContainer == null) return;

            // Remove cards left in the container (e.g. editor previews), then reuse/create one per game.
            for (int i = cardContainer.childCount - 1; i >= 0; i--)
            {
                var child = cardContainer.GetChild(i).GetComponent<LevelButton>();
                if (child == null || !cards.Contains(child)) Destroy(cardContainer.GetChild(i).gameObject);
            }

            while (cards.Count < registry.Count) cards.Add(Instantiate(cardPrefab, cardContainer));
            for (int i = 0; i < cards.Count; i++)
            {
                bool used = i < registry.Count;
                cards[i].gameObject.SetActive(used);
                if (!used) continue;

                var game = registry.Get(i);
                int index = i;
                cards[i].Setup(game, i + 1, registry.IsUnlocked(i), SaveData.GetStars(game.id),
                    () => GameFlow.Instance.PlayGame(index));
            }
        }
    }
}
