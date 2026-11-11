using UnityEngine;
namespace DuoRoad
{

// Small visual effects built from primitives.
public static class Effects
{
    // A quick fireball that grows, then shrinks away (orange by default).
    public static void Explosion(Vector3 position, float size)
    {
        Explosion(position, size, new Color(1f, 0.55f, 0.1f));
    }

    public static void Explosion(Vector3 position, float size, Color color)
    {
        var go = Prim.Make(PrimitiveType.Sphere, "Explosion", null,
                           position + Vector3.up * 0.6f, Vector3.zero, color);
        go.AddComponent<ExplosionFx>().size = size;
    }
}
}
