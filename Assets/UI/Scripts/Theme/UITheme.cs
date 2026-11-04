using System;
using UnityEngine;

namespace GameUI
{
    // ONE place to skin every menu. Select Assets/UI/Resources/UITheme.asset and drag in
    // your GUI pack's sprites, fonts and sounds: every ThemedImage / ThemedText updates
    // (live in the Editor too). Empty slots keep Unity's default rounded look.
    [CreateAssetMenu(menuName = "Game UI/UI Theme", fileName = "UITheme")]
    public class UITheme : ScriptableObject
    {
        [Header("Fonts (drag a .ttf / .otf)")]
        public Font bodyFont;
        [Tooltip("Titles and big numbers. Falls back to Body Font.")]
        public Font titleFont;

        [Header("Backgrounds & panels")]
        public Sprite menuBackground;
        public Sprite logo;
        public Sprite panel;
        [Tooltip("Level cards in the level menu.")]
        public Sprite card;

        [Header("Buttons")]
        public Sprite primaryButton;
        public Sprite secondaryButton;
        [Tooltip("Small square/round buttons (pause, back).")]
        public Sprite iconButton;

        [Header("Icons (optional; text labels are shown when empty)")]
        public Sprite pauseIcon;
        public Sprite backIcon;
        public Sprite settingsIcon;
        public Sprite lockIcon;
        public Sprite starFull;
        public Sprite starEmpty;
        public Sprite winIcon;
        public Sprite loseIcon;

        [Header("Sliders & toggles")]
        public Sprite sliderBackground;
        public Sprite sliderFill;
        public Sprite sliderHandle;
        public Sprite toggleBackground;
        public Sprite toggleCheckmark;

        [Header("Colours (multiplied with the sprites; keep white to show sprites as drawn)")]
        public Color backgroundColor = new Color(0.36f, 0.68f, 0.93f);
        public Color panelColor = new Color(1f, 0.97f, 0.88f);
        public Color cardColor = Color.white;
        public Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        public Color primaryColor = new Color(1f, 0.67f, 0.18f);
        public Color secondaryColor = new Color(0.42f, 0.75f, 0.42f);
        public Color iconButtonColor = new Color(1f, 1f, 1f, 0.9f);
        public Color iconColor = new Color(0.25f, 0.2f, 0.15f);
        public Color bodyTextColor = new Color(0.25f, 0.2f, 0.15f);
        public Color titleTextColor = new Color(0.25f, 0.2f, 0.15f);
        public Color buttonTextColor = Color.white;
        public Color lightTextColor = Color.white;
        public Color sliderBackgroundColor = new Color(0.82f, 0.76f, 0.66f);
        public Color sliderFillColor = new Color(1f, 0.67f, 0.18f);
        public Color sliderHandleColor = Color.white;
        public Color starColor = new Color(1f, 0.82f, 0.1f);
        public Color starEmptyColor = new Color(0.75f, 0.72f, 0.66f);
        public Color winColor = new Color(0.36f, 0.75f, 0.36f);
        public Color loseColor = new Color(0.9f, 0.35f, 0.3f);

        [Header("Sounds")]
        public AudioClip menuMusic;
        public AudioClip buttonClick;
        public AudioClip winSound;
        public AudioClip loseSound;

        // Raised when the theme is edited in the Inspector, so the scene restyles live.
        public static event Action Changed;

        static UITheme current;
        public static UITheme Current
        {
            get
            {
                if (current == null) current = Resources.Load<UITheme>("UITheme");
                return current;
            }
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => Changed?.Invoke();
#endif
        }
    }
}
