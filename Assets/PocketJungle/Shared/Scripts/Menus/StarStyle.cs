using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // How an earned / not-earned star looks (used by the level cards and the Win screen).
    // Leave a sprite empty to keep the sprite set on the star Image itself.
    // Untick "Tint" to keep the colours set on the star Images.
    [Serializable]
    public class StarStyle
    {
        public Sprite fullSprite;
        public Sprite emptySprite;
        public bool tint = true;
        public Color fullColor = new Color(1f, 0.82f, 0.1f);
        public Color emptyColor = new Color(0.75f, 0.72f, 0.66f);

        public void Apply(Image star, bool earned)
        {
            if (star == null) return;
            Sprite sprite = earned ? fullSprite : emptySprite;
            if (sprite != null) star.sprite = sprite;
            if (tint) star.color = earned ? fullColor : emptyColor;
        }
    }
}
