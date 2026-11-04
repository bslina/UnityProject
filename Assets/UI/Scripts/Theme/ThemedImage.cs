using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public enum ImageRole
    {
        None,
        MenuBackground, Logo, Panel, Card, Dim,
        PrimaryButton, SecondaryButton, IconButton,
        PauseIcon, BackIcon, SettingsIcon, LockIcon,
        StarFull, StarEmpty, WinIcon, LoseIcon,
        SliderBackground, SliderFill, SliderHandle,
        ToggleBackground, ToggleCheckmark,
    }

    // Takes its sprite and colour from the UI Theme according to its role.
    // To skin ONE element differently, untick "Use Theme Sprite/Colour" and set the Image yourself.
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class ThemedImage : MonoBehaviour
    {
        [SerializeField] ImageRole role;
        [SerializeField] bool useThemeSprite = true;
        [SerializeField] bool useThemeColor = true;
        [Tooltip("Hide this Image while the theme has no sprite for its role (for optional icons/logos).")]
        [SerializeField] bool hideIfNoSprite;
        [Tooltip("Shown while the theme has no sprite for this role, hidden once it has one (e.g. a text label standing in for an icon).")]
        [SerializeField] GameObject fallback;

        Image image;

        public ImageRole Role => role;

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

        public void SetRole(ImageRole newRole)
        {
            role = newRole;
            Apply();
        }

        public void Apply()
        {
            if (this == null) return;
            if (image == null) image = GetComponent<Image>();
            var theme = UITheme.Current;
            if (theme == null || image == null || role == ImageRole.None) return;

            Sprite sprite = SpriteFor(theme, role);
            if (useThemeSprite && sprite != null)
            {
                image.sprite = sprite;
                image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
            if (useThemeColor) image.color = ColorFor(theme, role);

            if (hideIfNoSprite) image.enabled = sprite != null;
            if (fallback != null) fallback.SetActive(sprite == null);
        }

        static Sprite SpriteFor(UITheme t, ImageRole r)
        {
            switch (r)
            {
                case ImageRole.MenuBackground: return t.menuBackground;
                case ImageRole.Logo: return t.logo;
                case ImageRole.Panel: return t.panel;
                case ImageRole.Card: return t.card;
                case ImageRole.PrimaryButton: return t.primaryButton;
                case ImageRole.SecondaryButton: return t.secondaryButton;
                case ImageRole.IconButton: return t.iconButton;
                case ImageRole.PauseIcon: return t.pauseIcon;
                case ImageRole.BackIcon: return t.backIcon;
                case ImageRole.SettingsIcon: return t.settingsIcon;
                case ImageRole.LockIcon: return t.lockIcon;
                case ImageRole.StarFull: return t.starFull;
                case ImageRole.StarEmpty: return t.starEmpty != null ? t.starEmpty : t.starFull;
                case ImageRole.WinIcon: return t.winIcon;
                case ImageRole.LoseIcon: return t.loseIcon;
                case ImageRole.SliderBackground: return t.sliderBackground;
                case ImageRole.SliderFill: return t.sliderFill;
                case ImageRole.SliderHandle: return t.sliderHandle;
                case ImageRole.ToggleBackground: return t.toggleBackground;
                case ImageRole.ToggleCheckmark: return t.toggleCheckmark;
                default: return null;
            }
        }

        static Color ColorFor(UITheme t, ImageRole r)
        {
            switch (r)
            {
                case ImageRole.MenuBackground: return t.backgroundColor;
                case ImageRole.Panel: return t.panelColor;
                case ImageRole.Card: return t.cardColor;
                case ImageRole.Dim: return t.dimColor;
                case ImageRole.PrimaryButton: return t.primaryColor;
                case ImageRole.SecondaryButton: return t.secondaryColor;
                case ImageRole.IconButton: return t.iconButtonColor;
                case ImageRole.PauseIcon:
                case ImageRole.BackIcon:
                case ImageRole.SettingsIcon:
                case ImageRole.LockIcon: return t.iconColor;
                case ImageRole.StarFull: return t.starColor;
                case ImageRole.StarEmpty: return t.starEmptyColor;
                case ImageRole.WinIcon: return t.winColor;
                case ImageRole.LoseIcon: return t.loseColor;
                case ImageRole.SliderBackground:
                case ImageRole.ToggleBackground: return t.sliderBackgroundColor;
                case ImageRole.SliderFill:
                case ImageRole.ToggleCheckmark: return t.sliderFillColor;
                case ImageRole.SliderHandle: return t.sliderHandleColor;
                default: return Color.white;
            }
        }
    }
}
