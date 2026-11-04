using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public static class UIFactory
    {
        public static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            return go;
        }

        public static RoundedRectGraphic AddRounded(GameObject target, Color color, float radius, bool raycast = false)
        {
            if (target.GetComponent<CanvasRenderer>() == null) target.AddComponent<CanvasRenderer>();
            RoundedRectGraphic graphic = target.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycast;
            return graphic;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color,
            TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            if (text.font == null) Debug.LogError("Sound Copy: TMP default font asset is missing. Import TMP Essential Resources.");
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, Color surfaceColor, Color textColor,
            float radius, out TextMeshProUGUI labelText, bool showArrow = true)
        {
            GameObject surface = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RoundedRectGraphic graphic = AddRounded(surface, surfaceColor, radius, true);
            Outline outline = surface.AddComponent<Outline>();
            outline.effectColor = SoundCopyTheme.Ink;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;
            Shadow shadow = surface.AddComponent<Shadow>();
            shadow.effectColor = new Color(SoundCopyTheme.Ink.r, SoundCopyTheme.Ink.g, SoundCopyTheme.Ink.b, 1f);
            shadow.effectDistance = new Vector2(0f, -10f);
            shadow.useGraphicAlpha = true;
            Button button = surface.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 1f, 0.86f, 1f);
            colors.pressedColor = new Color(0.84f, 0.93f, 0.62f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            labelText = CreateText("Label", surface.transform, label, 40f, textColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            RectTransform labelRt = labelText.rectTransform;
            labelRt.offsetMin = new Vector2(42f, 0f);
            labelRt.offsetMax = new Vector2(-100f, 0f);
            if (showArrow)
            {
                TextMeshProUGUI arrow = CreateText("Arrow", surface.transform, ">", 48f, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
                RectTransform arrowRt = arrow.rectTransform;
                arrowRt.anchorMin = new Vector2(0.84f, 0f);
                arrowRt.anchorMax = Vector2.one;
                arrowRt.offsetMin = Vector2.zero;
                arrowRt.offsetMax = Vector2.zero;
            }
            else
            {
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.rectTransform.offsetMin = new Vector2(18f, 0f);
                labelText.rectTransform.offsetMax = new Vector2(-18f, 0f);
            }
            return button;
        }


        public static TMP_InputField CreateInputField(string name, Transform parent, string placeholderValue, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject root = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            RoundedRectGraphic background = AddRounded(root, SoundCopyTheme.Cream, 22f, true);
            AddNativeOutline(root, new Color(0.55f, 0.5f, 0.48f, 0.7f), 2f);
            TMP_InputField input = root.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 16;

            GameObject viewport = CreateRect("TextArea", root.transform, Vector2.zero, Vector2.one, new Vector2(24f, 8f), new Vector2(-24f, -8f));
            viewport.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = CreateText("Placeholder", viewport.transform, placeholderValue, 24f, new Color(SoundCopyTheme.MutedGray.r, SoundCopyTheme.MutedGray.g, SoundCopyTheme.MutedGray.b, 0.75f), TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI value = CreateText("Text", viewport.transform, string.Empty, 28f, SoundCopyTheme.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            input.textViewport = viewport.GetComponent<RectTransform>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.caretColor = SoundCopyTheme.Pink;
            input.selectionColor = new Color(SoundCopyTheme.Lime.r, SoundCopyTheme.Lime.g, SoundCopyTheme.Lime.b, 0.45f);
            return input;
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child); else UnityEngine.Object.DestroyImmediate(child);
            }
        }

        public static void AddNativeOutline(GameObject target, Color color, float distance = 2f)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }
    }
}
