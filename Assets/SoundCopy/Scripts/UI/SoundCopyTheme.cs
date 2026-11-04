using UnityEngine;

namespace SoundCopy.UI
{
    public static class SoundCopyTheme
    {
        public static readonly Color DarkPlum = Hex("1A1022");
        public static readonly Color Cream = Hex("FFF8EC");
        public static readonly Color Ink = Hex("15101D");
        public static readonly Color Lime = Hex("C6FF35");
        public static readonly Color Coral = Hex("FF5964");
        public static readonly Color Pink = Hex("FF4E91");
        public static readonly Color SoftGreen = Hex("DDF4E4");
        public static readonly Color MutedGray = Hex("858087");
        public static readonly Color White = Color.white;
        public static readonly Color PaleBonus = Hex("FFF1B8");

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString("#" + value, out Color color) ? color : Color.magenta;
        }
    }
}
