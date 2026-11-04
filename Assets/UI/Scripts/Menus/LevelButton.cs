using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // One card in the level menu. The look lives in Assets/UI/Prefabs/LevelButton.prefab.
    public class LevelButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Text numberText;
        [SerializeField] Text nameText;
        [SerializeField] Text descriptionText;
        [Tooltip("Shows the minigame's icon from the registry; the number is shown when there is none.")]
        [SerializeField] Image icon;
        [SerializeField] ThemedImage[] stars;
        [SerializeField] GameObject starsRow;
        [SerializeField] GameObject lockedOverlay;
        [SerializeField] GameObject comingSoonOverlay;

        public void Setup(MiniGameInfo game, int number, bool unlocked, int starCount, Action onClick)
        {
            bool playable = game.IsPlayable;

            if (numberText != null)
            {
                numberText.text = number.ToString();
                numberText.gameObject.SetActive(game.icon == null);
            }
            if (nameText != null) nameText.text = game.displayName;
            if (descriptionText != null) descriptionText.text = game.description;
            if (icon != null)
            {
                icon.sprite = game.icon;
                icon.gameObject.SetActive(game.icon != null);
            }

            if (starsRow != null) starsRow.SetActive(playable && unlocked);
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null) stars[i].SetRole(i < starCount ? ImageRole.StarFull : ImageRole.StarEmpty);

            if (comingSoonOverlay != null) comingSoonOverlay.SetActive(!playable);
            if (lockedOverlay != null) lockedOverlay.SetActive(playable && !unlocked);

            button.interactable = playable && unlocked;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}
