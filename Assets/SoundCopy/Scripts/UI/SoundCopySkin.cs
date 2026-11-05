using UnityEngine;

namespace SoundCopy.UI
{
    // Sprites used when SoundCopy > Build Scenes generates the screens, so Sound Copy matches the
    // Pocket Jungle menus (2D Casual UI pack + jungle background). The scene builder fills these in.
    // After building, every shape is a normal UI Image: select any element in Main.unity and drag
    // another sprite into its Image > Source Image to restyle it.
    public static class SoundCopySkin
    {
        // Plain shapes for elements whose colour changes in game (player colours, selections).
        public static Sprite Rounded;        // white rounded rectangle (9-sliced)
        public static Sprite Circle;         // white disc

        // Pack art.
        public static Sprite PageBackground; // jungle picture behind every screen
        public static Sprite Panel;          // framed green window (9-sliced)
        public static Sprite Button;         // green button (9-sliced)
        public static Sprite RoundButton;    // round green button (back, pause)
        public static Sprite Ribbon;         // title banner (9-sliced left/right)
        public static Sprite IconBack;
        public static Sprite IconPause;
        public static Sprite IconTrophy;
        public static Sprite BarTrack;       // dark bar behind progress / score bars

        // Tints applied to the green button for each role (the sprite is green, colours multiply).
        public static readonly Color SecondaryTint = new Color(0.83f, 0.44f, 0.29f); // same orange-brown as the pause menu
        public static readonly Color DarkTint = new Color(0.32f, 0.32f, 0.32f);
    }
}
