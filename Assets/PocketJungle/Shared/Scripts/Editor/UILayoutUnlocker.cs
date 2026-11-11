using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    // Removes the automatic layouts (Vertical/Horizontal Layout Group, Content Size Fitter, Layout Element)
    // from the menus, so every button, text and image can be dragged anywhere by hand.
    // Each element keeps the exact position and size the layout had given it.
    // The level cards container (inside the level menu's scroll view) keeps its layout,
    // because the cards are created at runtime and need to line up by themselves.
    public static class UILayoutUnlocker
    {
        const string ScenesDir = "Assets/PocketJungle/Shared/Scenes";
        const string LevelButtonPath = "Assets/PocketJungle/Shared/Prefabs/LevelButton.prefab";

        [MenuItem("Tools/Game UI/Unlock Layouts (move elements freely)", priority = 25)]
        static void UnlockAllMenus()
        {
            if (!EditorUtility.DisplayDialog("Unlock layouts",
                    "Removes the automatic layout components from every menu scene in Assets/PocketJungle/Shared/Scenes and from LevelButton.prefab, " +
                    "so you can move each button/text/image freely. Positions and sizes stay as they are now.\n\n" +
                    "Open scenes are saved first.", "Unlock", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var setup = EditorSceneManager.GetSceneManagerSetup();
            int total = 0;

            foreach (var path in Directory.GetFiles(ScenesDir, "*.unity"))
            {
                var scene = EditorSceneManager.OpenScene(path.Replace('\\', '/'), OpenSceneMode.Single);
                int count = 0;
                foreach (var root in scene.GetRootGameObjects()) count += Unlock(root);
                if (count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                total += count;
                Debug.Log($"Game UI: unlocked {count} layout component(s) in {Path.GetFileName(path)}");
            }

            if (File.Exists(LevelButtonPath))
            {
                using (var prefab = new PrefabUtility.EditPrefabContentsScope(LevelButtonPath))
                {
                    int count = Unlock(prefab.prefabContentsRoot, keepRootLayoutElement: true);
                    total += count;
                    Debug.Log($"Game UI: unlocked {count} layout component(s) in LevelButton.prefab");
                }
            }

            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            Debug.Log($"Game UI: done, {total} layout component(s) removed. Every element can now be moved freely.");
        }

        // Unlocks everything under root. keepRootLayoutElement: the level card's own size inside the card row.
        public static int Unlock(GameObject root, bool keepRootLayoutElement = false)
        {
            // 1. Let the layouts place everything one last time.
            Canvas.ForceUpdateCanvases();
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                if (rt.GetComponent<LayoutGroup>() != null || rt.GetComponent<ContentSizeFitter>() != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            // 2. Find the layout components to remove (not the runtime card container).
            var keep = new HashSet<Transform>();
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
                if (scroll.content != null) keep.Add(scroll.content);

            var groups = new List<Component>();
            foreach (var g in root.GetComponentsInChildren<LayoutGroup>(true))
                if (!keep.Contains(g.transform)) groups.Add(g);
            foreach (var f in root.GetComponentsInChildren<ContentSizeFitter>(true))
                if (!keep.Contains(f.transform)) groups.Add(f);

            var elements = new List<LayoutElement>();
            foreach (var e in root.GetComponentsInChildren<LayoutElement>(true))
            {
                if (keepRootLayoutElement && e.gameObject == root) continue;
                if (e.transform.parent != null && keep.Contains(e.transform.parent)) continue;
                elements.Add(e);
            }

            // 3. Remember where every affected element is right now.
            var affected = new HashSet<RectTransform>();
            var layoutChildren = new HashSet<RectTransform>(); // placed by a layout group
            foreach (var c in groups)
            {
                affected.Add((RectTransform)c.transform);
                if (!(c is LayoutGroup)) continue;
                foreach (Transform child in c.transform)
                {
                    affected.Add((RectTransform)child);
                    layoutChildren.Add((RectTransform)child);
                }
            }
            foreach (var e in elements) affected.Add((RectTransform)e.transform);

            var saved = new Dictionary<RectTransform, (Vector3 pos, Vector2 size)>();
            foreach (var rt in affected) saved[rt] = (rt.localPosition, rt.rect.size);

            // 4. Remove the layout components.
            int removed = 0;
            foreach (var c in groups) { Object.DestroyImmediate(c, true); removed++; }
            foreach (var e in elements) { Object.DestroyImmediate(e, true); removed++; }

            // 5. Put every element back where it was, with plain centre anchors (easy to drag).
            //    Elements stretched to fill their parent (backgrounds, overlays) are left as they are.
            foreach (var pair in saved)
            {
                var rt = pair.Key;
                if (rt == null || rt.parent == null || rt == root.transform) continue;
                bool stretched = rt.anchorMin != rt.anchorMax;
                if (stretched && !layoutChildren.Contains(rt)) continue;

                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = pair.Value.size;
                rt.localPosition = pair.Value.pos;
                EditorUtility.SetDirty(rt);
            }
            return removed;
        }
    }
}
