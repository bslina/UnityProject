using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PatternGraphic : MaskableGraphic
    {
        [SerializeField, Min(12f)] private float spacing = 34f;
        [SerializeField, Min(1f)] private float dotSize = 3f;

        public float Spacing { get => spacing; set { spacing = Mathf.Max(12f, value); SetVerticesDirty(); } }
        public float DotSize { get => dotSize; set { dotSize = Mathf.Max(1f, value); SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            Color32 c = color;
            float half = dotSize * 0.5f;
            int columns = Mathf.CeilToInt(r.width / spacing) + 1;
            int rows = Mathf.CeilToInt(r.height / spacing) + 1;
            columns = Mathf.Min(columns, 80);
            rows = Mathf.Min(rows, 120);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float px = r.xMin + x * spacing;
                    float py = r.yMin + y * spacing;
                    int i = vh.currentVertCount;
                    vh.AddVert(new Vector2(px - half, py - half), c, Vector2.zero);
                    vh.AddVert(new Vector2(px - half, py + half), c, Vector2.up);
                    vh.AddVert(new Vector2(px + half, py + half), c, Vector2.one);
                    vh.AddVert(new Vector2(px + half, py - half), c, Vector2.right);
                    vh.AddTriangle(i, i + 1, i + 2);
                    vh.AddTriangle(i, i + 2, i + 3);
                }
            }
        }
    }
}
