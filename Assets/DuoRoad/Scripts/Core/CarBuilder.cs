using UnityEngine;
namespace DuoRoad
{

// Builds a simple low-poly car out of primitives. The car faces +Z.
// Later we can swap this for a real 3D model without touching the gameplay code.
public static class CarBuilder
{
    public static Transform Build(string name, Color bodyColor)
    {
        var root = new GameObject(name).transform;

        Prim.Make(PrimitiveType.Cube, "Body", root,
                  new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.6f, 4.2f), bodyColor);
        Prim.Make(PrimitiveType.Cube, "Cabin", root,
                  new Vector3(0f, 1.05f, -0.3f), new Vector3(1.5f, 0.5f, 2.0f), Prim.Darken(bodyColor, 0.6f));

        var tire = new Color(0.08f, 0.08f, 0.08f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var wheel = Prim.Make(PrimitiveType.Cylinder, "Wheel", root,
                                      new Vector3(sx * 0.95f, 0.35f, sz * 1.3f),
                                      new Vector3(0.7f, 0.15f, 0.7f), tire);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        var lamp = new Color(1f, 0.95f, 0.6f);
        Prim.Make(PrimitiveType.Cube, "HeadlightL", root,
                  new Vector3(-0.6f, 0.6f, 2.1f), new Vector3(0.35f, 0.2f, 0.1f), lamp);
        Prim.Make(PrimitiveType.Cube, "HeadlightR", root,
                  new Vector3(0.6f, 0.6f, 2.1f), new Vector3(0.35f, 0.2f, 0.1f), lamp);

        return root;
    }
}
}
