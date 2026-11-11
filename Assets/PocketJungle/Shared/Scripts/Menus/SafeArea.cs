using UnityEngine;

namespace GameUI
{
    // Shrinks this RectTransform to the phone's safe area so buttons never sit under a notch
    // or rounded corner. Put corner buttons (pause, back) inside an object with this.
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void OnEnable() => Fit();

        void Update()
        {
            if (applied != Screen.safeArea || screen.x != Screen.width || screen.y != Screen.height) Fit();
        }

        void Fit()
        {
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rt.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
