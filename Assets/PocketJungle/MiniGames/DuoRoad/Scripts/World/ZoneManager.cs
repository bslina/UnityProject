using UnityEngine;
using UnityEngine.InputSystem;
namespace DuoRoad
{

// Changes the zone every zoneLength metres: desert -> beach -> Hollywood in the rain -> desert...
//   - the roadside scenery changes exactly at the zone border (RoadScroller asks us which theme to use)
//   - sky, fog, sun and rain blend smoothly while you cross the border
//   - a banner shows the new zone name, and you get a small score bonus
// In the Unity editor, press Z to jump to the next zone and test it quickly.
public class ZoneManager : MonoBehaviour
{
    public CarController car;
    public GameManager game;
    public RoadScroller road;
    public Camera cam;
    public Light sun;

    [Header("Zones")]
    public float zoneLength = 2000f;   // metres driven in each zone (same unit as the score distance)
    public float blendLength = 240f;   // metres over which the sky and weather change at a border
    public int zoneBonus = 250;        // score for reaching a new zone (0 = no bonus)
    public bool randomStartZone = true; // each new game starts in a random zone, then the order continues as usual

    [Header("Testing")]
    public bool debugKey = true;       // Editor only: Z = next zone

    RainFx rain;
    float debugOffset;
    int lastZone;
    int startOffset;                   // which zone of ZoneTheme.All the run starts in
    int shownTheme = -1;               // theme of the road the player is on right now (-1 = first launch)
    float baseAmbient = 1f;

    float lightning, flashAt;
    int flashesLeft;
    float nextLightning;

    float bannerTime = -10f;
    string bannerTitle = "", bannerSub = "";
    GUIStyle big, small;

    // Distance used for the zones (the debug key shifts it, the real score is untouched)
    float D { get { return (car != null ? car.Distance : 0f) + debugOffset; } }

    // ------------------------------------------------------------------ themes by distance

    int ZoneIndex(float distance)
    {
        return Mathf.Max(0, Mathf.FloorToInt(distance / zoneLength));
    }

    ZoneTheme Theme(int zoneIndex)
    {
        return ZoneTheme.All[(Mathf.Max(0, zoneIndex) + startOffset) % ZoneTheme.All.Length];
    }

    // Theme of a road segment whose centre is zAhead metres in front of the car
    public ZoneTheme ThemeForSegment(float zAhead)
    {
        return Theme(ZoneIndex(D + zAhead));
    }

    // ------------------------------------------------------------------ life cycle

    void Start()
    {
        baseAmbient = RenderSettings.ambientIntensity;

        rain = gameObject.AddComponent<RainFx>();
        rain.follow = car != null ? car.transform : null;
        rain.road = road;

        nextLightning = Time.time + 6f;
        ResetZones();
    }

    // Called when the game restarts (the car distance is back to 0)
    public void ResetZones()
    {
        debugOffset = 0f;
        lastZone = 0;
        int n = ZoneTheme.All.Length;
        if (!randomStartZone) startOffset = 0;
        else if (shownTheme < 0 || n < 2) startOffset = Random.Range(0, n);              // first launch: any zone
        else startOffset = (shownTheme + Random.Range(1, n)) % n;                        // restart: never the road we just played on
        shownTheme = startOffset;

        // Banner with the name of the zone we start in
        bannerTime = Time.time;
        bannerTitle = Theme(0).name;
        bannerSub = "ZONE 1";
        lightning = 0f;
        flashesLeft = 0;

        if (road != null) road.RefreshThemes();
        ApplyAmbience(0f);
    }

    void Update()
    {
        if (car == null) return;

        if (debugKey && Application.isEditor && Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
        {
            debugOffset += zoneLength - (D % zoneLength) + 1f;
            if (road != null) road.RefreshThemes();
        }

        int zone = ZoneIndex(D);
        if (zone != lastZone)
        {
            if (zone > lastZone) OnEnterZone(zone);
            lastZone = zone;
        }
        shownTheme = (zone + startOffset) % ZoneTheme.All.Length;

        ApplyAmbience(Time.deltaTime);
    }

    void OnEnterZone(int zone)
    {
        bannerTime = Time.time;
        bannerTitle = Theme(zone).name;
        bannerSub = "ZONE " + (zone + 1) + (zoneBonus > 0 ? "   +" + zoneBonus : "");

        if (game != null && zoneBonus > 0) game.AddScore(zoneBonus, true);   // zone bonus = driving points
    }

    // ------------------------------------------------------------------ sky, fog, sun, rain

    void ApplyAmbience(float dt)
    {
        float u = D / zoneLength;
        int k = Mathf.Max(0, Mathf.RoundToInt(u));   // nearest zone border

        ZoneTheme a, b;
        float w;
        if (k == 0)
        {
            a = b = Theme(0);
            w = 1f;
        }
        else
        {
            a = Theme(k - 1);
            b = Theme(k);
            float off = (u - k) * zoneLength;       // metres from that border (negative = before it)
            w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blendLength * 0.5f, blendLength * 0.5f, off));
        }

        float rainAmount = Mathf.Lerp(a.rain, b.rain, w);
        UpdateLightning(dt, rainAmount);

        Color sky = Color.Lerp(a.sky, b.sky, w);
        sky = Color.Lerp(sky, new Color(0.9f, 0.93f, 1f), lightning * 0.6f);

        if (cam != null) cam.backgroundColor = sky;
        RenderSettings.fogColor = sky;
        RenderSettings.fogStartDistance = Mathf.Lerp(a.fogStart, b.fogStart, w);
        RenderSettings.fogEndDistance = Mathf.Lerp(a.fogEnd, b.fogEnd, w);
        RenderSettings.ambientIntensity = baseAmbient * Mathf.Lerp(a.ambient, b.ambient, w);

        if (sun != null)
        {
            sun.color = Color.Lerp(a.sunColor, b.sunColor, w);
            sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, w) + lightning * 1.5f;
            sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.sunEuler), Quaternion.Euler(b.sunEuler), w);
        }

        if (rain != null) rain.Amount = rainAmount;
    }

    // Now and then a flash of lightning (one or two quick flashes) while it rains hard
    void UpdateLightning(float dt, float rainAmount)
    {
        lightning = Mathf.Max(0f, lightning - dt * 5f);

        if (rainAmount > 0.8f && flashesLeft == 0 && Time.time >= nextLightning)
        {
            flashesLeft = Random.value < 0.5f ? 2 : 1;
            flashAt = Time.time;
            nextLightning = Time.time + Random.Range(7f, 16f);
        }

        if (flashesLeft > 0 && Time.time >= flashAt)
        {
            lightning = 1f;
            flashesLeft--;
            flashAt = Time.time + 0.2f;
        }
    }

    // ------------------------------------------------------------------ on-screen text

    void OnGUI()
    {
        if (car == null || game == null) return;
        if (Event.current.type != EventType.Repaint) return;

        float W = Screen.width, H = Screen.height;

        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        }

        // Small zone name under the speed
        if (game.Current == GameManager.State.Playing)
        {
            small.fontSize = Mathf.RoundToInt(H * 0.03f);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(W * 0.02f, H * 0.085f, W * 0.3f, H * 0.06f), Theme(ZoneIndex(D)).name, small);
        }

        // Banner when a new zone starts
        float t = Time.time - bannerTime;
        const float duration = 3f;
        if (t < 0f || t > duration) { GUI.color = Color.white; return; }

        float alpha = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((duration - t) / 0.6f);

        GUI.color = new Color(0f, 0f, 0f, 0.45f * alpha);
        GUI.DrawTexture(new Rect(0f, H * 0.33f, W, H * 0.19f), Texture2D.whiteTexture);

        big.fontSize = Mathf.RoundToInt(H * 0.09f);
        big.normal.textColor = Color.white;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(0f, H * 0.33f, W, H * 0.12f), bannerTitle, big);

        big.fontSize = Mathf.RoundToInt(H * 0.04f);
        big.normal.textColor = new Color(1f, 0.9f, 0.3f);
        GUI.Label(new Rect(0f, H * 0.44f, W, H * 0.07f), bannerSub, big);

        big.normal.textColor = Color.white;
        GUI.color = Color.white;
    }
}
}
