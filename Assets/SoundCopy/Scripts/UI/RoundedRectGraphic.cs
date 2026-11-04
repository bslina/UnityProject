using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedRectGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float cornerRadius = 28f;
        [SerializeField, Range(2, 16)] private int cornerSegments = 8;

        public float CornerRadius
        {
            get => cornerRadius;
            set
            {
                cornerRadius = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            Color32 vertexColor = color;

            if (radius <= 0.01f)
            {
                AddQuad(vh, rect.xMin, rect.yMin, rect.xMax, rect.yMax, vertexColor, rect);
                return;
            }

            // Three overlapping rectangular zones form a continuous body.
            // The four quarter-circle fans only fill the corners. This avoids
            // the long centre-to-perimeter triangles that produced visible seams.
            AddQuad(vh, rect.xMin + radius, rect.yMin, rect.xMax - radius, rect.yMax, vertexColor, rect);
            AddQuad(vh, rect.xMin, rect.yMin + radius, rect.xMin + radius, rect.yMax - radius, vertexColor, rect);
            AddQuad(vh, rect.xMax - radius, rect.yMin + radius, rect.xMax, rect.yMax - radius, vertexColor, rect);

            int segments = Mathf.Max(2, cornerSegments);
            AddCorner(vh, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, segments, vertexColor, rect);
            AddCorner(vh, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, segments, vertexColor, rect);
            AddCorner(vh, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, segments, vertexColor, rect);
            AddCorner(vh, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, segments, vertexColor, rect);
        }

        private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color32 vertexColor, Rect bounds)
        {
            if (xMax <= xMin || yMax <= yMin)
                return;

            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(xMin, yMin), vertexColor, ToUv(new Vector2(xMin, yMin), bounds));
            vh.AddVert(new Vector2(xMin, yMax), vertexColor, ToUv(new Vector2(xMin, yMax), bounds));
            vh.AddVert(new Vector2(xMax, yMax), vertexColor, ToUv(new Vector2(xMax, yMax), bounds));
            vh.AddVert(new Vector2(xMax, yMin), vertexColor, ToUv(new Vector2(xMax, yMin), bounds));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddCorner(VertexHelper vh, Vector2 center, float radius, float startAngle, float endAngle,
            int segments, Color32 vertexColor, Rect bounds)
        {
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, vertexColor, ToUv(center, bounds));

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vh.AddVert(point, vertexColor, ToUv(point, bounds));
            }

            for (int i = 0; i < segments; i++)
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
        }

        private static Vector2 ToUv(Vector2 point, Rect bounds)
        {
            return new Vector2(
                Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x),
                Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y));
        }
    }
}
