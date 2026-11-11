using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Layout helpers for the Sound Copy screens, sized for a phone held sideways.
    // Canvas: 1920 x 1080 reference, matched on HEIGHT, so every screen is 1080 px tall and
    // 1920 (16:9) to ~2400 (20:9) px wide. Screens use two columns; the main action sits
    // bottom-right, under the right thumb. Text >= 30 px, buttons >= 120 px tall.
    public static class MobileUI
    {
        public const float Side = 56f;            // left / right margin
        public const float TopBarHeight = 120f;
        public const float ContentTop = 165f;      // first pixel below the top bar
        public const float ButtonHeight = 150f;
        public const float ButtonWidth = 640f;
        public const float BottomMargin = 40f;

        // Set by the scene builder before building; stored in every text so they survive in the scene.
        public static TMP_FontAsset DisplayFont;   // Anton: titles, big numbers, buttons
        public static TMP_FontAsset BodyFont;      // Barlow Condensed: everything else

        // ---------------------------------------------------------------- page structure

        // Full-screen page. Dark pages show the jungle picture (like the Pocket Jungle menus) under a
        // dark veil; other colours (the hand-over screen) are a plain coloured Image.
        public static RectTransform Page(Transform parent, Color background, bool picture = true)
        {
            GameObject page = UIFactory.CreateRect("Page", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Bleeds a little past the safe area so notches/rounded corners never show a gap.
            var bg = UIFactory.CreateRect("Background", page.transform, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
            bool usePicture = picture && SoundCopySkin.PageBackground != null;
            UIFactory.AddSprite(bg, null, usePicture ? Color.black : background, true); // raycast: blocks taps on hidden panels
            if (usePicture)
            {
                bg.AddComponent<RectMask2D>();
                var art = UIFactory.CreateRect("Picture", bg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                UIFactory.AddSprite(art, SoundCopySkin.PageBackground, Color.white);
                var fitter = art.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = SoundCopySkin.PageBackground.rect.width / SoundCopySkin.PageBackground.rect.height;
                var veil = UIFactory.CreateRect("Veil", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                UIFactory.AddSprite(veil, null, new Color(0.05f, 0.03f, 0.07f, 0.5f));
            }
            return (RectTransform)page.transform;
        }

        // Framed green window from the pack (for static containers).
        public static Image Panel(GameObject go, bool raycast = false)
        {
            if (SoundCopySkin.Panel == null) return Card(go, new Color(0.17f, 0.11f, 0.21f), 40f, true, raycast);
            var image = UIFactory.AddSprite(go, SoundCopySkin.Panel, Color.white, raycast);
            image.pixelsPerUnitMultiplier = 1.4f; // thinner frame on big windows
            return image;
        }

        // Round green pack button with an icon (back, pause). Falls back to a cream disc with text.
        public static Button RoundButton(Transform parent, string name, Sprite icon, string fallbackText, bool flipIcon = false)
        {
            var go = UIFactory.CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image face = SoundCopySkin.RoundButton != null
                ? UIFactory.AddSprite(go, SoundCopySkin.RoundButton, Color.white, true)
                : UIFactory.AddCircle(go, SoundCopyTheme.Cream, true);
            var button = go.AddComponent<Button>();
            button.targetGraphic = face;
            if (icon != null)
            {
                var iconGo = UIFactory.CreateRect("Icon", go.transform, new Vector2(0.24f, 0.24f), new Vector2(0.76f, 0.76f), Vector2.zero, Vector2.zero);
                var iconImage = UIFactory.AddSprite(iconGo, icon, Color.white);
                iconImage.preserveAspect = true;
                if (flipIcon) iconGo.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                Text(go.transform, "Label", fallbackText, 60f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            }
            return button;
        }

        // Back arrow (left), title (centre) and an optional chip (right). Returns the back button, or null.
        public static Button TopBar(RectTransform page, string title, bool withBack, Color textColor,
            out TextMeshProUGUI titleText, out TextMeshProUGUI chipText, bool withChip = false)
        {
            var bar = Row(page, "TopBar", 22f, TopBarHeight, 32f);
            Button back = null;
            if (withBack)
            {
                var backGo = UIFactory.CreateRect("BackButton", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                var rt = (RectTransform)backGo.transform;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(116f, 116f);
                back = RoundButton(backGo.transform, "Button", SoundCopySkin.IconBack, "<", true);
            }

            // Ribbon banner behind the title, like the menus.
            if (SoundCopySkin.Ribbon != null)
            {
                var ribbon = UIFactory.CreateRect("Ribbon", bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                ((RectTransform)ribbon.transform).sizeDelta = new Vector2(980f, 150f);
                UIFactory.AddSprite(ribbon, SoundCopySkin.Ribbon, Color.white);
            }

            titleText = Text(bar, "Title", title, 60f, textColor, TextAlignmentOptions.Center, true);
            titleText.rectTransform.offsetMin = new Vector2(170f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-170f, 0f);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 40f;
            titleText.fontSizeMax = 60f;

            chipText = null;
            if (withChip)
            {
                var chip = UIFactory.CreateRect("Chip", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
                var rt = (RectTransform)chip.transform;
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(170f, 90f);
                UIFactory.AddRounded(chip, SoundCopyTheme.Lime, 45f);
                UIFactory.AddNativeOutline(chip, SoundCopyTheme.Ink, 3f);
                chipText = Text(chip.transform, "Label", "", 38f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            }
            return back;
        }

        // A full-width block placed `top` px below the top edge.
        public static RectTransform Row(Transform parent, string name, float top, float height, float side = Side)
        {
            var go = UIFactory.CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(side, -top - height);
            rt.offsetMax = new Vector2(-side, -top);
            return rt;
        }

        // A full-width block placed `bottom` px above the bottom edge.
        public static RectTransform BottomRow(Transform parent, string name, float bottom, float height, float side = Side)
        {
            var go = UIFactory.CreateRect(name, parent, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(side, bottom);
            rt.offsetMax = new Vector2(-side, bottom + height);
            return rt;
        }

        // A block that stretches between `top` px from the top and `bottom` px from the bottom.
        public static RectTransform Fill(Transform parent, string name, float top, float bottom, float side = Side)
        {
            var go = UIFactory.CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.offsetMin = new Vector2(side, bottom);
            rt.offsetMax = new Vector2(-side, -top);
            return rt;
        }

        // A block spanning a horizontal fraction of the parent (xMin..xMax, with `side` px padding on
        // the outer edges and half a gutter between columns), `top` px from the top, `height` px tall.
        public static RectTransform ColRow(Transform parent, string name, float xMin, float xMax, float top, float height, float gutter = 48f)
        {
            var go = UIFactory.CreateRect(name, parent, new Vector2(xMin, 1f), new Vector2(xMax, 1f), Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 1f);
            float left = xMin <= 0f ? Side : gutter * 0.5f;
            float right = xMax >= 1f ? Side : gutter * 0.5f;
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // A block of fixed size pinned to the bottom-right corner (the main action).
        public static RectTransform BottomRight(Transform parent, string name, float width = ButtonWidth, float height = ButtonHeight,
            float right = Side, float bottom = BottomMargin)
        {
            var go = UIFactory.CreateRect(name, parent, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-right, bottom);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        // A block pinned to the left edge, `top` px from the top, fixed size.
        public static RectTransform TopLeft(Transform parent, string name, float left, float top, float width, float height)
        {
            var go = UIFactory.CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        // Splits a parent horizontally: returns the cell `index` of `count` (with a gap in pixels).
        public static RectTransform Cell(Transform parent, string name, int index, int count, float gap = 24f)
        {
            float w = 1f / count;
            var go = UIFactory.CreateRect(name, parent, new Vector2(index * w, 0f), new Vector2((index + 1) * w, 1f), Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.offsetMin = new Vector2(index == 0 ? 0f : gap * 0.5f, 0f);
            rt.offsetMax = new Vector2(index == count - 1 ? 0f : -gap * 0.5f, 0f);
            return rt;
        }

        // ---------------------------------------------------------------- widgets

        public static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions align, bool display = false)
        {
            var text = UIFactory.CreateText(name, parent, value, size, color, align, display ? FontStyles.Normal : FontStyles.Bold);
            var font = display ? DisplayFont : BodyFont;
            if (font != null) text.font = font;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        public static Image Card(GameObject go, Color fill, float radius = 40f, bool outline = true, bool raycast = false)
        {
            var g = UIFactory.AddRounded(go, fill, radius, raycast);
            if (outline) UIFactory.AddNativeOutline(go, SoundCopyTheme.Ink, 4f);
            return g;
        }

        // Big chunky button filling its parent. Returns the button; label via out.
        public static Button BigButton(Transform parent, string name, string label, Color fill, Color textColor,
            out TextMeshProUGUI labelText, float fontSize = 54f)
        {
            var go = UIFactory.CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image g;
            if (SoundCopySkin.Button != null)
            {
                // Green pack button: lime = as drawn, cream = orange-brown (as in the pause menu), dark = grey.
                Color tint = fill == SoundCopyTheme.Lime ? Color.white
                    : fill == SoundCopyTheme.Cream ? SoundCopySkin.SecondaryTint
                    : fill == SoundCopyTheme.Ink ? SoundCopySkin.DarkTint : fill;
                g = UIFactory.AddSprite(go, SoundCopySkin.Button, tint, true);
                textColor = Color.white;
            }
            else
            {
                g = UIFactory.AddRounded(go, fill, 46f, true);
                UIFactory.AddNativeOutline(go, SoundCopyTheme.Ink, 4f);
            }
            var button = go.AddComponent<Button>();
            button.targetGraphic = g;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            button.colors = colors;
            labelText = Text(go.transform, "Label", label, fontSize, textColor, TextAlignmentOptions.Center, true);
            labelText.rectTransform.offsetMin = new Vector2(24f, 0f);
            labelText.rectTransform.offsetMax = new Vector2(-24f, 0f);
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 34f;
            labelText.fontSizeMax = fontSize;
            return button;
        }

        public static Button BigButton(Transform parent, string name, string label, Color fill, Color textColor, float fontSize = 54f)
            => BigButton(parent, name, label, fill, textColor, out _, fontSize);

        // A coloured circle with a short label inside (J1 / J2 badges).
        public static GameObject Badge(Transform parent, string name, string label, Color fill, float size, float fontSize)
        {
            var go = UIFactory.CreateRect(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            UIFactory.AddRounded(go, fill, size * 0.5f);
            UIFactory.AddNativeOutline(go, SoundCopyTheme.Ink, 3f);
            Text(go.transform, "Label", label, fontSize, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            return go;
        }

        public static Color PlayerColor(int player) => player == 1 ? SoundCopyTheme.Lime : SoundCopyTheme.Coral;
    }
}
