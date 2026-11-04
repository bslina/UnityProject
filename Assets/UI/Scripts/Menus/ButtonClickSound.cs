using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    // Plays the theme's click sound when this button is pressed. Add it to any button.
    [RequireComponent(typeof(Selectable))]
    public class ButtonClickSound : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            var selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;
            if (GameFlow.Instance != null && GameFlow.Instance.Audio != null)
                GameFlow.Instance.Audio.PlayClick();
        }
    }
}
