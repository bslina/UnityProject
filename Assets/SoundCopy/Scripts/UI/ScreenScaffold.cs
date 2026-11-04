using TMPro;
using UnityEngine;

namespace SoundCopy.UI
{
    public static class ScreenScaffold
    {
        public static GameObject Build(Transform parent, string step, string title, string subtitle)
        {
            GameObject background = UIFactory.CreateRect("OuterBackground", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(background, SoundCopyTheme.DarkPlum, 0f);
            GameObject patternObject = UIFactory.CreateRect("BackgroundPattern", background.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PatternGraphic pattern = patternObject.AddComponent<PatternGraphic>();
            pattern.color = new Color(SoundCopyTheme.Cream.r, SoundCopyTheme.Cream.g, SoundCopyTheme.Cream.b, 0.07f);
            pattern.Spacing = 46f;
            pattern.DotSize = 2.5f;
            pattern.raycastTarget = false;

            GameObject shell = UIFactory.CreateRect("AppShell", parent, new Vector2(0.045f, 0.018f), new Vector2(0.955f, 0.982f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(shell, SoundCopyTheme.Cream, 64f);
            UIFactory.AddNativeOutline(shell, SoundCopyTheme.Ink, 3f);

            GameObject header = UIFactory.CreateRect("Header", shell.transform, new Vector2(0.065f, 0.885f), new Vector2(0.935f, 0.97f), Vector2.zero, Vector2.zero);
            GameObject logo = UIFactory.CreateRect("Logo", header.transform, new Vector2(0f, 0.12f), new Vector2(0.1f, 0.88f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(logo, SoundCopyTheme.Ink, 18f);
            for (int i = 0; i < 4; i++)
            {
                float h = 0.34f + i * 0.13f;
                float x = 0.18f + i * 0.17f;
                GameObject bar = UIFactory.CreateRect("Bar" + i, logo.transform, new Vector2(x, 0.5f - h * 0.5f), new Vector2(x + 0.09f, 0.5f + h * 0.5f), Vector2.zero, Vector2.zero);
                UIFactory.AddRounded(bar, i == 2 ? SoundCopyTheme.Pink : SoundCopyTheme.Lime, 4f);
            }
            TextMeshProUGUI brand = UIFactory.CreateText("Brand", header.transform, "SOUND COPY", 36f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            brand.rectTransform.anchorMin = new Vector2(0.13f, 0f);
            brand.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            brand.rectTransform.offsetMin = Vector2.zero;
            brand.rectTransform.offsetMax = Vector2.zero;
            GameObject badge = UIFactory.CreateRect("StepBadge", header.transform, new Vector2(0.68f, 0.22f), new Vector2(1f, 0.78f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(badge, SoundCopyTheme.SoftGreen, 24f);
            UIFactory.CreateText("Label", badge.transform, step, 19f, new Color(0.12f, 0.5f, 0.34f), TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject divider = UIFactory.CreateRect("Divider", shell.transform, new Vector2(0.065f, 0.875f), new Vector2(0.935f, 0.877f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(divider, new Color(0.55f, 0.5f, 0.48f, 0.25f), 0f);

            TextMeshProUGUI heading = UIFactory.CreateText("ScreenTitle", shell.transform, title, 66f, SoundCopyTheme.Ink, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 44f;
            heading.fontSizeMax = 66f;
            heading.rectTransform.anchorMin = new Vector2(0.075f, 0.77f);
            heading.rectTransform.anchorMax = new Vector2(0.925f, 0.855f);
            heading.rectTransform.offsetMin = Vector2.zero;
            heading.rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI sub = UIFactory.CreateText("ScreenSubtitle", shell.transform, subtitle, 24f, SoundCopyTheme.MutedGray, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            sub.rectTransform.anchorMin = new Vector2(0.075f, 0.715f);
            sub.rectTransform.anchorMax = new Vector2(0.925f, 0.77f);
            sub.rectTransform.offsetMin = Vector2.zero;
            sub.rectTransform.offsetMax = Vector2.zero;
            return shell;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }
    }
}
