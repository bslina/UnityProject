using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public enum TextRole { Body, Title, Button, Light, Icon }

    // Takes its font and colour from the UI Theme. Font size and alignment stay as set on the Text.
    [ExecuteAlways]
    [RequireComponent(typeof(Text))]
    public class ThemedText : MonoBehaviour
    {
        [SerializeField] TextRole role;
        [SerializeField] bool useThemeFont = true;
        [SerializeField] bool useThemeColor = true;

        Text text;

        void OnEnable()
        {
            UITheme.Changed += Apply;
            Apply();
        }

        void OnDisable() => UITheme.Changed -= Apply;

        // Applied a frame later: changing a Graphic inside OnValidate makes Unity log warnings.
        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += Apply;
#endif
        }

        public void Apply()
        {
            if (this == null) return;
            if (text == null) text = GetComponent<Text>();
            var theme = UITheme.Current;
            if (theme == null || text == null) return;

            if (useThemeFont)
            {
                Font font = role == TextRole.Title && theme.titleFont != null ? theme.titleFont : theme.bodyFont;
                if (font != null) text.font = font;
            }

            if (useThemeColor)
            {
                switch (role)
                {
                    case TextRole.Title: text.color = theme.titleTextColor; break;
                    case TextRole.Button: text.color = theme.buttonTextColor; break;
                    case TextRole.Light: text.color = theme.lightTextColor; break;
                    case TextRole.Icon: text.color = theme.iconColor; break;
                    default: text.color = theme.bodyTextColor; break;
                }
            }
        }
    }
}
