using UnityEngine;
using UnityEngine.EventSystems;

// Hold-to-shade button that SunGlare shows when the phone has no usable light/proximity sensor.
public class ShadeHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public LightCoverDetector detector;

    public void OnPointerDown(PointerEventData e) => Set(true);
    public void OnPointerUp(PointerEventData e) => Set(false);
    public void OnPointerExit(PointerEventData e) => Set(false);
    void OnDisable() => Set(false);

    void Set(bool covered)
    {
        if (detector != null) detector.ManualCover = covered;
    }
}
