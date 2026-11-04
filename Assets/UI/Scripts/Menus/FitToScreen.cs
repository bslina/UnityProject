using UnityEngine;

namespace GameUI
{
    // Scales a fixed-size window down when the screen (canvas) is too small to show it whole,
    // e.g. the Settings window on 20:9 phones. Never scales up.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class FitToScreen : MonoBehaviour
    {
        [Tooltip("Free space kept around the window, as a fraction of the screen.")]
        [SerializeField, Range(0f, 0.3f)] float margin = 0.04f;

        RectTransform rt;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void Update() => Fit(); // cheap; catches canvas resizes (rotation, editor game view)

        void Fit()
        {
            if (rt == null) rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;

            Vector2 size = rt.rect.size;
            Vector2 room = parent.rect.size * (1f - 2f * margin);
            if (size.x <= 0f || size.y <= 0f || room.x <= 0f || room.y <= 0f) return;

            float scale = Mathf.Min(1f, room.x / size.x, room.y / size.y);
            if (!Mathf.Approximately(rt.localScale.x, scale)) rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
