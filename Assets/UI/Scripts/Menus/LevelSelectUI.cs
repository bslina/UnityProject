using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // Level menu: one card per entry in the MiniGame Registry, built when the panel opens.
    public class LevelSelectUI : UIPanel
    {
        [SerializeField] LevelButton cardPrefab;
        [Tooltip("Parent of the cards (has a layout group).")]
        [SerializeField] Transform cardContainer;
        [SerializeField] Button backButton;

        public event Action BackPressed;

        static readonly List<LevelSelectUI> open = new List<LevelSelectUI>();
        readonly List<LevelButton> cards = new List<LevelButton>();

        protected override void Awake()
        {
            base.Awake();
            backButton.onClick.AddListener(() => BackPressed?.Invoke());
        }

        void OnEnable()
        {
            open.Add(this);
            Refresh();
        }

        void OnDisable() => open.Remove(this);

        public static void RefreshAll()
        {
            foreach (var panel in open) panel.Refresh();
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
