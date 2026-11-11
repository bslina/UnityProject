using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Swap the LOOK of the spoon without touching how it plays.
//
// The spoon's physics stays exactly as it is:
//   Spoon (SpoonController + Rigidbody)   moves/tilts the spoon, the bird perches relative to it
//     Bowl 0 ... Bowl 8                    box colliders the egg rolls on (their cubes get hidden)
//     Handle                               where the character's hand holds the spoon (SpoonHandIK)
//     SpoonModel                           <- your new model goes here, colliders removed
//
// Tools > Egg Run > Spoon > Use Selected Model as Spoon
//   Select your spoon model (FBX/prefab in the Project window, or an object in the scene), then run it.
//   The model is sized and centred on the old spoon; rotate/scale SpoonModel if it needs adjusting.
// Tools > Egg Run > Spoon > Show/Hide Physics Shape
//   Shows the old cubes again so you can line the model's bowl up with them.
public static class EggRunSpoonModel
{
    const string ModelName = "SpoonModel";

    [MenuItem("Tools/Egg Run/Spoon/Use Selected Model as Spoon")]
    static void UseSelected()
    {
        var source = Selection.activeGameObject;
        if (source == null)
        {
            EditorUtility.DisplayDialog("Egg Run", "Select your spoon model first (in the Project window or the scene).", "OK");
            return;
        }
        var spoon = Object.FindAnyObjectByType<SpoonController>(FindObjectsInactive.Include);
        if (spoon == null)
        {
            EditorUtility.DisplayDialog("Egg Run", "Open the EggRun scene first (no SpoonController found).", "OK");
            return;
        }
        if (source.GetComponentInParent<SpoonController>() == spoon && source.transform != spoon.transform.Find(ModelName))
        {
            EditorUtility.DisplayDialog("Egg Run", "Select the new model, not a part of the current spoon.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Use model as spoon");
        int group = Undo.GetCurrentGroup();

        // Size of the current spoon look (the cubes), to fit the new model to it.
        Bounds target = VisualBounds(spoon.transform, ModelName);

        var old = spoon.transform.Find(ModelName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject model;
        bool isAsset = EditorUtility.IsPersistent(source);
        if (isAsset && PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.NotAPrefab)
            model = (GameObject)PrefabUtility.InstantiatePrefab(source, spoon.transform);
        else
            model = Object.Instantiate(source, spoon.transform);   // a scene object is copied; the original stays
        Undo.RegisterCreatedObjectUndo(model, "Spoon model");
        model.name = ModelName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // The model is only a look: its own physics would fight the spoon's.
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(c);
        foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Undo.DestroyObjectImmediate(r);

        // Fit: same length as the old spoon, centred on it.
        Bounds mb = RendererBounds(model);
        if (mb.size.sqrMagnitude > 0f && target.size.sqrMagnitude > 0f)
        {
            float scale = Mathf.Max(target.size.x, target.size.y, target.size.z) / Mathf.Max(mb.size.x, mb.size.y, mb.size.z);
            model.transform.localScale = Vector3.one * scale;
            mb = RendererBounds(model);
            model.transform.position += target.center - mb.center;
        }

        SetPhysicsVisible(spoon.transform, false);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = model;
        EditorSceneManager.MarkSceneDirty(spoon.gameObject.scene);

        EditorUtility.DisplayDialog("Egg Run",
            "New spoon model added as Spoon > SpoonModel (its colliders were removed).\n\n" +
            "1. Rotate/scale SpoonModel so the bowl is on the right and the handle on the left, like the old spoon.\n" +
            "2. Tools > Egg Run > Spoon > Show/Hide Physics Shape: line the model's bowl up with the cubes, then hide them again.\n" +
            "3. Move Spoon > Handle to where the hand should grip.\n" +
            "4. Save the scene (Ctrl+S).", "OK");
    }

    [MenuItem("Tools/Egg Run/Spoon/Add Bowl Shape Controls")]
    static void AddShape()
    {
        var spoon = Object.FindAnyObjectByType<SpoonController>(FindObjectsInactive.Include);
        if (spoon == null)
        {
            EditorUtility.DisplayDialog("Egg Run", "Open the EggRun scene first (no SpoonController found).", "OK");
            return;
        }
        var shape = spoon.GetComponent<SpoonShape>() ?? Undo.AddComponent<SpoonShape>(spoon.gameObject);
        shape.Apply();
        Selection.activeGameObject = spoon.gameObject;
        EditorSceneManager.MarkSceneDirty(spoon.gameObject.scene);
        EditorUtility.DisplayDialog("Egg Run",
            "Spoon Shape added to the Spoon (selected now). Bowl Size starts at 0.8 (a bit smaller).\n\n" +
            "Move Bowl Size (smaller = harder) and Rim Plates Removed in the Inspector and watch the spoon in the Scene view.\n" +
            "Bowl Size 1 + Rim Plates 0 = the original spoon. Save the scene when you like it.", "OK");
    }

    [MenuItem("Tools/Egg Run/Spoon/Show or Hide Physics Shape")]
    static void TogglePhysics()
    {
        var spoon = Object.FindAnyObjectByType<SpoonController>(FindObjectsInactive.Include);
        if (spoon == null) return;
        var first = PhysicsRenderers(spoon.transform).FirstOrDefault();
        bool show = first != null && !first.enabled;
        SetPhysicsVisible(spoon.transform, show);
        EditorSceneManager.MarkSceneDirty(spoon.gameObject.scene);
    }

    // The old cubes (Bowl 0-8 + Handle): their colliders stay, only their look is turned off/on.
    static void SetPhysicsVisible(Transform spoon, bool visible)
    {
        foreach (var r in PhysicsRenderers(spoon))
        {
            Undo.RecordObject(r, "Spoon physics shape");
            r.enabled = visible;
        }
    }

    static System.Collections.Generic.IEnumerable<Renderer> PhysicsRenderers(Transform spoon)
    {
        foreach (Transform child in spoon)
        {
            if (child.name == ModelName) continue;
            foreach (var r in child.GetComponents<Renderer>()) yield return r;
        }
    }

    static Bounds VisualBounds(Transform spoon, string skip)
    {
        Bounds b = new Bounds(spoon.position, Vector3.zero);
        bool any = false;
        foreach (Transform child in spoon)
        {
            if (child.name == skip) continue;
            foreach (var r in child.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
        }
        return b;
    }

    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
