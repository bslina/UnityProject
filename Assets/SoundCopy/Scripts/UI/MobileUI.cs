using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // Layout helpers for the Sound Copy screens, sized for a phone held upright (canvas 1080 x 2400).
    // Everything is placed in pixels from the top or bottom edge, so screens of any height keep
    // the same comfortable sizes: text >= 34 px, buttons >= 150 px tall, main action at the bottom.
    public static class MobileUI
    {
        public const float Side = 56f;            // left / right margin
        public const float TopBarHeight = 150f;
        public const float ButtonHeight = 170f;
        public const float BottomMargin = 56f;

        // Set by the scene builder before building; stored in every text so they survive in the scene.
        public static TMP_FontAsset DisplayFont;   // Anton: titles, big numbers, buttons
        public static TMP_FontAsset BodyFont;      // Barlow Condensed: everything else

        // ---------------------------------------------------------------- page structure

        public static RectTransform Page(Transform parent, Color background, bool pattern = true)
        {
            GameObject page = UIFactory.CreateRect("Page", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Bleeds a little past the safe area so notches/rounded corners never show a gap.
            var bg = UIFactory.CreateRect("Background", page.transform, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
            UIFactory.AddRounded(bg, background, 0f, true); // raycast: blocks taps on hidden panels
            if (pattern)
            {
                var dots = UIFactory.CreateRect("Pattern", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var graphic = dots.AddComponent<PatternGraphic>();
                graphic.color = new Color(1f, 1f, 1f, 0.06f);
                graphic.Spacing = 46f;
                graphic.DotSize = 3f;
                graphic.raycastTarget = false;
            }
            return (RectTransform)page.transform;
        }

        // Back arrow (left), title (centre) and an optional chip (right). Returns the back button, or null.
        public static Button TopBar(RectTransform page, string title, bool withBack, Color textColor,
            out TextMeshProUGUI titleText, out TextMeshProUGUI chipText, bool withChip = false)
        {
            var bar = Row(page, "TopBar", 24f, TopBarHeight, 32f);
            Button back = null;
            if (withBack)
            {
                var backGo = UIFactory.CreateRect("BackButton", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                var rt = (RectTransform)backGo.transform;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(130f, 130f);
                var g = UIFactory.AddRounded(backGo, SoundCopyTheme.Cream, 65f, true);
                UIFactory.AddNativeOutline(backGo, SoundCopyTheme.Ink, 4f);
                back = backGo.AddComponent<Button>();
                back.targetGraphic = g;
                Text(backGo.transform, "Arrow", "<", 64f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            }

            titleText = Text(bar, "Title", title, 58f, textColor, TextAlignmentOptions.Center, true);
            titleText.rectTransform.offsetMin = new Vector2(150f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-150f, 0f);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 40f;
            titleText.fontSizeMax = 58f;

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

        public static RoundedRectGraphic Card(GameObject go, Color fill, float radius = 40f, bool outline = true, bool raycast = false)
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
            var g = UIFactory.AddRounded(go, fill, 46f, true);
            UIFactory.AddNativeOutline(go, SoundCopyTheme.Ink, 4f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = SoundCopyTheme.Ink;
            shadow.effectDistance = new Vector2(0f, -12f);
            shadow.useGraphicAlpha = true;
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
