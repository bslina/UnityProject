#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor-only helper: creates Assets/Resources/PrimLit.mat (a URP Lit material) if it is missing.
// Prim.cs loads it, so the shapes are drawn correctly in a phone build.
// It runs by itself when Unity reloads scripts. You can also run it from the menu:
//   Tools > RoadDuo > Create primitive material
[InitializeOnLoad]
public static class PrimMaterialSetup
{
    const string AssetPath = "Assets/Resources/PrimLit.mat";

    static PrimMaterialSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Tools/RoadDuo/Create primitive material")]
    public static void Ensure()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(AssetPath) != null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[PrimMaterialSetup] Shader 'Universal Render Pipeline/Lit' not found: is URP installed?");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(new Material(shader), AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[PrimMaterialSetup] Created " + AssetPath);
    }
}
#endif
