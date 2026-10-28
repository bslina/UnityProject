using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Hold-to-walk button. Space also works, for testing in the Editor.
public class WalkButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float pressedScale = 0.9f;

    bool pointerHeld;

    public bool IsHeld => pointerHeld || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        transform.localScale = Vector3.one * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    public void OnPointerExit(PointerEventData eventData) => Release();

    void OnDisable() => Release();

    void Release()
    {
        pointerHeld = false;
        transform.localScale = Vector3.one;
    }
}
