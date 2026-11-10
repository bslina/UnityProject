using System.Collections.Generic;
using UnityEngine;

// Endless road: a few road segments move toward the camera, and each one that passes
// behind the player is moved to the front. The car never moves forward; the world does.
// Each segment wears the theme of the zone it is in (desert, beach, Hollywood...). When a
// segment is moved to the front and its zone is different, its ground and scenery are swapped.
public class RoadScroller : MonoBehaviour
{
    public float roadWidth = 12f;
    public float segmentLength = 30f;
    public int segmentCount = 8;

    // Set every frame by the car (units per second).
    public float Speed;

    // Set by GameBootstrap. If it is null, the road stays in the first zone (desert).
    public ZoneManager zones;

    class Segment
    {
        public Transform root;
        public GameObject asphalt, groundLeft, groundRight;
        public Transform decor;
        public ZoneTheme theme;
    }

    readonly List<Segment> segments = new List<Segment>();

    static readonly Color White = new Color(0.92f, 0.92f, 0.92f);

    void Awake()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            segments.Add(BuildSegment((i - 1) * segmentLength, i));
        }
    }

    void Update()
    {
        float dz = Speed * Time.deltaTime;
        float maxZ = float.MinValue;

        foreach (var s in segments)
        {
            s.root.position += Vector3.back * dz;
            if (s.root.position.z > maxZ) maxZ = s.root.position.z;
        }

        float limit = -1.5f * segmentLength;
        foreach (var s in segments)
        {
            if (s.root.position.z < limit)
            {
                maxZ += segmentLength;
                s.root.position = new Vector3(0f, 0f, maxZ);
                ApplyTheme(s, ThemeFor(maxZ));
            }
        }
    }

    // Re-dress every segment (after a restart, or when the zone is skipped in the editor)
    public void RefreshThemes()
    {
        foreach (var s in segments) ApplyTheme(s, ThemeFor(s.root.position.z));
    }

    ZoneTheme ThemeFor(float z)
    {
        return zones != null ? zones.ThemeForSegment(z) : ZoneTheme.All[0];
    }

    Segment BuildSegment(float z, int index)
    {
        var seg = new Segment();

        var root = new GameObject("Segment " + index).transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(0f, 0f, z);
        seg.root = root;

        float len = segmentLength + 0.1f;   // small overlap to avoid seams
        float half = roadWidth / 2f;
        var first = ZoneTheme.All[0];

        seg.asphalt = Prim.Make(PrimitiveType.Cube, "Asphalt", root,
                  new Vector3(0f, -0.05f, 0f), new Vector3(roadWidth, 0.1f, len), first.asphalt);
        seg.groundLeft = Prim.Make(PrimitiveType.Cube, "GroundLeft", root,
                  new Vector3(-(half + 30f), -0.06f, 0f), new Vector3(60f, 0.1f, len), first.groundLeft);
        seg.groundRight = Prim.Make(PrimitiveType.Cube, "GroundRight", root,
                  new Vector3(half + 30f, -0.06f, 0f), new Vector3(60f, 0.1f, len), first.groundRight);

        Prim.Make(PrimitiveType.Cube, "EdgeLeft", root,
                  new Vector3(-half + 0.3f, 0.01f, 0f), new Vector3(0.2f, 0.04f, len), White);
        Prim.Make(PrimitiveType.Cube, "EdgeRight", root,
                  new Vector3(half - 0.3f, 0.01f, 0f), new Vector3(0.2f, 0.04f, len), White);

        // Dashed lane dividers (3 lanes)
        int dashes = 6;
        float step = segmentLength / dashes;
        for (int k = 0; k < dashes; k++)
        {
            float dz = -segmentLength / 2f + step * (k + 0.5f);
            Prim.Make(PrimitiveType.Cube, "Dash", root,
                      new Vector3(-roadWidth / 6f, 0.01f, dz), new Vector3(0.2f, 0.04f, step * 0.5f), White);
            Prim.Make(PrimitiveType.Cube, "Dash", root,
                      new Vector3(roadWidth / 6f, 0.01f, dz), new Vector3(0.2f, 0.04f, step * 0.5f), White);
        }

        ApplyTheme(seg, first);
        return seg;
    }

    // Paints the ground and rebuilds the scenery, but only if the zone really changed
    void ApplyTheme(Segment seg, ZoneTheme theme)
    {
        if (seg.theme == theme) return;
        seg.theme = theme;

        float half = roadWidth / 2f;

        Prim.Paint(seg.asphalt, theme.asphalt);
        Prim.Paint(seg.groundLeft, theme.groundLeft);
        Prim.Paint(seg.groundRight, theme.groundRight);
        seg.groundLeft.transform.localPosition = new Vector3(-(half + 30f), theme.groundLeftY, 0f);

        if (seg.decor != null) Destroy(seg.decor.gameObject);
        seg.decor = RoadDecor.Build(theme, seg.root, segmentLength, half);
    }
}
