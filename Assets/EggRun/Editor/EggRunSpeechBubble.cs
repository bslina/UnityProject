using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Egg Run > Make Tips a Speech Bubble
// Restyles the GuideBubble (the tips) as a cartoon speech bubble: white rounded body with a dark
// outline and a tail pointing at the character. It grows out of the tail when it pops in.
// Also run automatically by "Build Start Panel". Runs once: if the bubble already has a Tail, nothing changes.
public static partial class EggRunCharacterSetup
{
    const string BubbleBodyPath = Folder + "/UI/SpeechBubble.png";
    const string BubbleTailPath = Folder + "/UI/SpeechBubbleTail.png";
    static readonly Color BubbleOutline = new Color(0.20f, 0.13f, 0.08f);
    static readonly Color BubbleText = new Color(0.22f, 0.15f, 0.10f);

    [MenuItem("Tools/Egg Run/Make Tips a Speech Bubble")]
    public static void MakeSpeechBubbleMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(EggRunScene, OpenSceneMode.Single);
        string report = StyleSpeechBubble();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(startScene) && startScene != EggRunScene) EditorSceneManager.OpenScene(startScene);
        EditorUtility.DisplayDialog("Egg Run", report, "OK");
    }

    static string StyleSpeechBubble()
    {
        var panel = FindByName("StartPanel");
        if (panel == null) return "No StartPanel found.";
        Transform bubble = panel.transform.Find("GuideBubble") ?? panel.transform.Find("Card/GuideBubble");
        if (bubble == null) return "No GuideBubble yet: run Tools > Egg Run > Set Up Character first.";
        if (bubble.Find("Tail") != null) return "The tips are already a speech bubble (nothing changed).";

        var rt = (RectTransform)bubble;
        bool inCard = bubble.parent != null && bubble.parent.name == "Card";

        // Body: white rounded bubble with an outline (9-sliced, so any size works).
        var image = bubble.GetComponent<Image>();
        image.sprite = BubbleBodySprite();
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        // Grow out of the tail (left side) when it pops.
        Vector2 size = rt.rect.size;
        Vector2 newPivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition += Vector2.Scale(newPivot - rt.pivot, size);
        rt.pivot = newPivot;
        if (inCard)
        {
            // Hang out of the card's left edge, towards the character.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(880f, 300f);
            rt.anchoredPosition = new Vector2(-620f, 40f);
        }

        // Tail on the left, pointing at the character.
        var tail = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        tail.transform.SetParent(bubble, false);
        tail.transform.SetAsFirstSibling();          // under the text
        var trt = (RectTransform)tail.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
        trt.pivot = new Vector2(1f, 0.5f);
        trt.anchoredPosition = new Vector2(16f, -30f);   // overlaps the body edge so the outline joins
        trt.sizeDelta = new Vector2(130f, 110f);
        var tailImage = tail.GetComponent<Image>();
        tailImage.sprite = BubbleTailSprite();
        tailImage.raycastTarget = false;

        // Dark text on the white bubble.
        foreach (var text in bubble.GetComponentsInChildren<Text>(true))
        {
            bool hint = text.name == "TapHint";
            text.color = hint ? new Color(BubbleText.r, BubbleText.g, BubbleText.b, 0.6f) : BubbleText;
            var shadow = text.GetComponent<Shadow>();
            if (shadow != null) shadow.enabled = false;
        }

        return "The tips are now a speech bubble coming from the character.\n" +
               "Move GuideBubble to place it; select GuideBubble > Tail to aim the tail.";
    }

    // ---------------------------------------------------------------- generated art

    static Sprite BubbleBodySprite()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BubbleBodyPath);
        if (sprite != null) return sprite;
        const int size = 160; const float radius = 62f, outline = 7f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // signed distance to a rounded square (negative = inside)
                float px = Mathf.Abs(x + 0.5f - size * 0.5f) - (half - radius);
                float py = Mathf.Abs(y + 0.5f - size * 0.5f) - (half - radius);
                float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                tex.SetPixel(x, y, Shade(d, outline));
            }
        return SaveSprite(tex, BubbleBodyPath, new Vector4(radius + 8, radius + 8, radius + 8, radius + 8));
    }

    static Sprite BubbleTailSprite()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BubbleTailPath);
        if (sprite != null) return sprite;
        const int w = 130, h = 110; const float outline = 7f;
        // Triangle: tip on the left (slightly low), wide base on the right edge (which hides under the body).
        Vector2 tip = new Vector2(4f, 22f), top = new Vector2(w + 4f, 92f), bottom = new Vector2(w + 4f, 40f);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d1 = EdgeDistance(tip, top, p);       // > 0 inside
                float d2 = EdgeDistance(bottom, tip, p);
                float d = -Mathf.Min(d1, d2);                // signed, negative inside
                tex.SetPixel(x, y, Shade(d, outline));
            }
        return SaveSprite(tex, BubbleTailPath, Vector4.zero);
    }

    // Distance from p to the line a->b, positive on the right-hand side (inside for clockwise points).
    static float EdgeDistance(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 e = (b - a).normalized;
        return e.x * (p.y - a.y) - e.y * (p.x - a.x) < 0f
            ? Mathf.Abs(e.x * (p.y - a.y) - e.y * (p.x - a.x))
            : -Mathf.Abs(e.x * (p.y - a.y) - e.y * (p.x - a.x));
    }

    // White fill, dark outline band of 'outline' pixels at the edge, anti-aliased.
    static Color Shade(float d, float outline)
    {
        float alpha = Mathf.Clamp01(0.5f - d);
        float edge = Mathf.Clamp01(d + outline + 0.5f);   // 0 = fill, 1 = outline
        Color c = Color.Lerp(Color.white, BubbleOutline, edge);
        c.a = alpha;
        return c;
    }

    static Sprite SaveSprite(Texture2D tex, string path, Vector4 border)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
