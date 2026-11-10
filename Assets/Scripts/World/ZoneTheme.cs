using UnityEngine;

// The look of one zone of the road. To add a zone, add one more entry to All (and, if you
// want new roadside objects, a new Decor value + a builder in RoadDecor). Nothing else to change.
// After the last zone the list starts again from the first one, so the road is endless.
public class ZoneTheme
{
    public enum Decor { Desert, Beach, Hollywood }

    public string name;

    // Atmosphere
    public Color sky;
    public float fogStart, fogEnd;
    public Color sunColor;
    public float sunIntensity;
    public Vector3 sunEuler;
    public float ambient = 1f;     // multiplier for the ambient light
    public float rain;             // 0 = dry, 1 = heavy rain

    // Ground
    public Color asphalt, groundLeft, groundRight;
    public float groundLeftY = -0.06f;   // lower than the road = sea
    public Decor decor;

    public static readonly ZoneTheme[] All =
    {
        // 1. The original sunny Texas desert (same values as before)
        new ZoneTheme
        {
            name = "TEXAS DESERT",
            sky = new Color(0.53f, 0.81f, 0.92f), fogStart = 90f, fogEnd = 250f,
            sunColor = Color.white, sunIntensity = 1.1f, sunEuler = new Vector3(50f, -30f, 0f),
            ambient = 1f, rain = 0f,
            asphalt = new Color(0.18f, 0.18f, 0.20f),
            groundLeft = new Color(0.87f, 0.72f, 0.45f), groundRight = new Color(0.87f, 0.72f, 0.45f),
            groundLeftY = -0.06f, decor = Decor.Desert
        },

        // 2. Beach road at golden hour: the sea on the left, palms and sand on the right
        new ZoneTheme
        {
            name = "MALIBU BEACH",
            sky = new Color(0.99f, 0.72f, 0.48f), fogStart = 70f, fogEnd = 240f,
            sunColor = new Color(1f, 0.78f, 0.55f), sunIntensity = 1.0f, sunEuler = new Vector3(24f, -65f, 0f),
            ambient = 0.95f, rain = 0f,
            asphalt = new Color(0.20f, 0.20f, 0.22f),
            groundLeft = new Color(0.08f, 0.52f, 0.72f), groundRight = new Color(0.95f, 0.85f, 0.62f),
            groundLeftY = -0.20f, decor = Decor.Beach
        },

        // 3. Hollywood in the rain: dark sky, wet road, palms, street lamps, buildings
        new ZoneTheme
        {
            name = "HOLLYWOOD RAIN",
            sky = new Color(0.36f, 0.40f, 0.46f), fogStart = 60f, fogEnd = 200f,
            sunColor = new Color(0.72f, 0.80f, 0.92f), sunIntensity = 0.5f, sunEuler = new Vector3(60f, -20f, 0f),
            ambient = 0.6f, rain = 1f,
            asphalt = new Color(0.09f, 0.10f, 0.12f),
            groundLeft = new Color(0.13f, 0.30f, 0.17f), groundRight = new Color(0.13f, 0.30f, 0.17f),
            groundLeftY = -0.06f, decor = Decor.Hollywood
        }
    };
}
