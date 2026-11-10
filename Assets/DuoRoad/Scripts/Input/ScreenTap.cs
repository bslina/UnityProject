using UnityEngine;
using UnityEngine.InputSystem;
namespace DuoRoad
{

// One-frame "tap" detection for the menus (pause button, intro / round over / results screens).
// Positions come back in GUI coordinates (origin at the TOP-left), so they can be compared
// directly with the Rects used in OnGUI. Works with touch, mouse and (for Any) the keyboard.
public static class ScreenTap
{
    // True on the frame a finger / the mouse button went down
    public static bool TryGet(out Vector2 guiPos)
    {
        guiPos = Vector2.zero;

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 p = touch.primaryTouch.position.ReadValue();
            guiPos = new Vector2(p.x, Screen.height - p.y);
            return true;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 p = mouse.position.ReadValue();
            guiPos = new Vector2(p.x, Screen.height - p.y);
            return true;
        }

        return false;
    }

    // "Tap anywhere to continue" (keyboard Space / Enter / R also work in the editor)
    public static bool Any()
    {
        Vector2 p;
        if (TryGet(out p)) return true;

        var kb = Keyboard.current;
        return kb != null && (kb.spaceKey.wasPressedThisFrame
                           || kb.enterKey.wasPressedThisFrame
                           || kb.rKey.wasPressedThisFrame);
    }
}
}
