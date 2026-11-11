using System.Collections.Generic;
using UnityEngine;
namespace DuoRoad
{

// Builds coloured 3D shapes from Unity primitives, with no manual materials to set up.
// Every shape gets a copy of ONE template material. The template is a real URP Lit material
// stored in Assets/Resources/PrimLit.mat (created automatically by Editor/PrimMaterialSetup.cs),
// because a material that exists as a file is always included in a phone build, with its shader.
public static class Prim
{
    static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
    static Material template;

    static Material GetMaterial(Color c)
    {
        if (cache.TryGetValue(c, out var existing) && existing != null) return existing;

        if (template == null)
        {
            // 1) The material file in Resources (works in the editor AND in a phone build)
            template = Resources.Load<Material>("PrimLit");

            // 2) Otherwise the default material of the active render pipeline
            if (template == null)
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (rp != null) template = rp.defaultMaterial;
            }

            // 3) Last resort: the default primitive material
            if (template == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template = tmp.GetComponent<Renderer>().sharedMaterial;
                UnityEngine.Object.Destroy(tmp);
            }
        }

        var m = new Material(template) { color = c };
        cache[c] = m;
        return m;
    }

    public static GameObject Make(PrimitiveType type, string name, Transform parent,
                                  Vector3 localPos, Vector3 localScale, Color color,
                                  bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);

        if (!keepCollider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
        }
        return go;
    }

    // Change the colour of a shape made with Make()
    public static void Paint(GameObject go, Color c)
    {
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(c);
    }

    public static Color Darken(Color c, float f)
    {
        return new Color(c.r * f, c.g * f, c.b * f, 1f);
    }
}
}
