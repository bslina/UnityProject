using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Sun events for Egg Run. Every so often the sun comes out:
//   1. Warning: a sun rises in the corner, the screen starts to brighten, the phone buzzes twice.
//   2. Sun: a blinding glare covers the screen AND heats the egg.
//      Covering the front camera with a thumb (or the light sensor) = shade:
//      the glare fades so you can see, and the egg cools down.
//   3. If the heat meter fills up, the egg is cooked and the round is lost.
// Builds its own overlay canvas at runtime. Drag your own art/sounds into the slots to replace the placeholders.
public class SunGlare : MonoBehaviour
{
    enum Phase { Idle, Warning, Sun }

    [Header("Timing (seconds of play)")]
    [SerializeField] Vector2 firstSunDelay = new Vector2(8f, 12f);
    [Tooltip("Time between the end of one sun and the start of the next warning (random in this range).")]
    [SerializeField] Vector2 timeBetweenSuns = new Vector2(9f, 15f);
    [SerializeField] float warningTime = 1.5f;
    [Tooltip("How long the sun stays out (random in this range).")]
    [SerializeField] Vector2 sunDuration = new Vector2(5f, 8f);
    [Tooltip("No new sun starts when less time than this is left on the timer.")]
    [SerializeField] float noSunInLastSeconds = 6f;

    [Header("Heat")]
    [Tooltip("Seconds of full sun (sensor not covered) to cook the egg from cold.")]
    [SerializeField] float secondsToCook = 4.5f;
    [Tooltip("Seconds in the shade to cool the egg from fully hot to cold.")]
    [SerializeField] float secondsToCool = 5f;
    [SerializeField] Color cookedEggColor = new Color(0.55f, 0.3f, 0.12f);

    [Header("Look")]
    [SerializeField] Color glareColor = new Color(1f, 0.98f, 0.86f);
    [Tooltip("Glare strength in full sun (1 = solid, nothing visible).")]
    [SerializeField, Range(0f, 1f)] float fullGlare = 0.96f;
    [Tooltip("Glare strength while the sensor is covered.")]
    [SerializeField, Range(0f, 1f)] float shadedGlare = 0.1f;
    [SerializeField] float glareFadeSpeed = 4f;
    [SerializeField] Color shadeColor = new Color(0.02f, 0.05f, 0.15f, 0.45f);

    [Header("Art (optional, placeholders are drawn when empty)")]
    [SerializeField] Sprite sunSprite;
    [SerializeField] Sprite flareSprite;
    [Tooltip("Dark edges shown while the sensor is covered (the 'hand shadow').")]
    [SerializeField] Sprite shadeSprite;
    [SerializeField] Font font;

    [Header("Sounds (optional)")]
    [SerializeField] AudioClip warningSound;
    [Tooltip("Looped while the egg is heating; volume follows the heat.")]
    [SerializeField] AudioClip sizzleLoop;

    [Header("Texts")]
    [SerializeField] string cameraHint = "Cover the front camera with your thumb\nto block the sun!";
    [SerializeField] string sensorHint = "Cover the top of your phone\n(the light sensor next to the camera) to block the sun!";
    [SerializeField] string buttonHint = "Hold SHADE to block the sun!";
    [SerializeField] string cookedText = "The egg got cooked!";

    public event Action Cooked;
    public string CookedText => cookedText;
    public float Heat01 { get; private set; }
    public bool IsRunning => running;
    public string PhaseName => phase.ToString();

    const string HintSeenKey = "eggrun.sunHintSeen.v2";

    LightCoverDetector detector;
    Func<float> timeLeft;
    bool running;
    Phase phase;
    float phaseTimer;
    float phaseLength;
    bool firstSunThisRound;
    bool dangerBuzzed;

    // UI
    Canvas canvas;
    Image glare, flare, shade, sun, heatFill, heatBack;
    RectTransform heatBar;
    Text heatLabel, hint;
    GameObject shadeButton;
    float glareAlpha, shadeAlpha, sunAlpha;
    Coroutine hintRoutine;

    // Egg tint
    Renderer[] eggRenderers;
    Color[] eggColors;
    MaterialPropertyBlock block;
    AudioSource sizzle;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        detector = GetComponent<LightCoverDetector>();
        if (detector == null) detector = gameObject.AddComponent<LightCoverDetector>();
        BuildUI();

        sizzle = gameObject.AddComponent<AudioSource>();
        sizzle.playOnAwake = false;
        sizzle.loop = true;
        // Empty slots use the files in Assets/UI/Resources/Audio (sun_warning, sizzle).
        if (warningSound == null) warningSound = GameUI.GameAudio.Get("sun_warning");
        if (sizzleLoop == null) sizzleLoop = GameUI.GameAudio.Get("sizzle");
        sizzle.clip = sizzleLoop;
    }

    // ---------------------------------------------------------------- control

    // Call while the start panel is showing: asks for camera permission and lets the
    // camera learn the room's brightness before the round starts.
    public void Prepare()
    {
        if (GameUI.GameSettings.SunEvents) detector.StartCamera();
    }

    public void Begin(Egg egg, Func<float> getTimeLeft)
    {
        timeLeft = getTimeLeft;
        Heat01 = 0f;
        dangerBuzzed = false;
        firstSunThisRound = true;
        CacheEgg(egg);

        running = GameUI.GameSettings.SunEvents;
        if (running) detector.StartCamera();
        SetPhase(Phase.Idle, RandomIn(firstSunDelay));
    }

    public void Stop()
    {
        running = false;
        phase = Phase.Idle;
        if (sizzle.isPlaying) sizzle.Stop();
        detector.StopCamera(); // also turns off Android's "camera in use" dot
    }

    // ---------------------------------------------------------------- loop

    void Update()
    {
        if (running) Tick(Time.deltaTime);
        UpdateVisuals(Time.unscaledDeltaTime);
    }

    void Tick(float dt)
    {
        if (dt <= 0f) return; // paused

        phaseTimer += dt;
        switch (phase)
        {
            case Phase.Idle:
                if (phaseTimer >= phaseLength)
                {
                    if (timeLeft != null && timeLeft() < noSunInLastSeconds + warningTime) break;
                    StartWarning();
                }
                break;
            case Phase.Warning:
                if (phaseTimer >= phaseLength) SetPhase(Phase.Sun, RandomIn(sunDuration));
                break;
            case Phase.Sun:
                if (phaseTimer >= phaseLength) SetPhase(Phase.Idle, RandomIn(timeBetweenSuns));
                break;
        }

        bool exposed = phase == Phase.Sun && !detector.IsCovered;
        if (exposed) Heat01 += dt / Mathf.Max(0.1f, secondsToCook);
        else Heat01 -= dt / Mathf.Max(0.1f, secondsToCool);
        Heat01 = Mathf.Clamp01(Heat01);

        if (Heat01 > 0.75f && !dangerBuzzed)
        {
            dangerBuzzed = true;
            Haptics.Pulse(150, 200);
        }
        if (Heat01 < 0.5f) dangerBuzzed = false;

        if (sizzle.clip != null)
        {
            if (exposed && !sizzle.isPlaying) sizzle.Play();
            else if (!exposed && sizzle.isPlaying) sizzle.Stop();
            sizzle.volume = GameUI.GameSettings.SfxVolume * Mathf.Lerp(0.3f, 1f, Heat01);
        }

        TintEgg(Heat01);

        if (Heat01 >= 1f)
        {
            Stop();
            Cooked?.Invoke();
        }
    }

    void StartWarning()
    {
        SetPhase(Phase.Warning, warningTime);
        StartCoroutine(DoubleBuzz());
        if (warningSound != null)
        {
            if (GameUI.GameFlow.Instance != null && GameUI.GameFlow.Instance.Audio != null) GameUI.GameFlow.Instance.Audio.PlaySfx(warningSound);
            else AudioSource.PlayClipAtPoint(warningSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero, GameUI.GameSettings.SfxVolume);
        }

        if (firstSunThisRound)
        {
            firstSunThisRound = false;
            bool useButton = !detector.SensorUsable;
            if (useButton || PlayerPrefs.GetInt(HintSeenKey, 0) == 0)
            {
                ShowHint(useButton ? buttonHint : detector.CameraUsable ? cameraHint : sensorHint);
                if (!useButton)
                {
                    PlayerPrefs.SetInt(HintSeenKey, 1);
                    PlayerPrefs.Save();
                }
            }
        }
    }

    void SetPhase(Phase next, float length)
    {
        phase = next;
        phaseTimer = 0f;
        phaseLength = length;
    }

    static float RandomIn(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);

    IEnumerator DoubleBuzz()
    {
        Haptics.Pulse(60, 180);
        yield return new WaitForSecondsRealtime(0.18f);
        Haptics.Pulse(60, 180);
    }

    // ---------------------------------------------------------------- visuals

    void UpdateVisuals(float dt)
    {
        bool covered = detector.IsCovered;

        float glareTarget = 0f;
        if (running && phase == Phase.Warning) glareTarget = Mathf.Lerp(0f, 0.35f, phaseTimer / Mathf.Max(0.01f, phaseLength));
        else if (running && phase == Phase.Sun) glareTarget = covered ? shadedGlare : fullGlare;
        if (running && phase == Phase.Warning && covered) glareTarget = Mathf.Min(glareTarget, shadedGlare);

        float speed = running ? glareFadeSpeed : glareFadeSpeed * 2f;
        glareAlpha = Mathf.MoveTowards(glareAlpha, glareTarget, speed * dt);

        bool sunEvent = running && phase != Phase.Idle;
        float shadeTarget = !running || !covered ? 0f : sunEvent ? 1f : 0.35f;
        shadeAlpha = Mathf.MoveTowards(shadeAlpha, shadeTarget, 5f * dt);
        sunAlpha = Mathf.MoveTowards(sunAlpha, sunEvent ? 1f : 0f, 3f * dt);

        SetAlpha(glare, glareColor, glareAlpha);
        SetAlpha(flare, Color.white, glareAlpha);
        SetAlpha(shade, shadeColor, shadeAlpha * shadeColor.a);
        SetAlpha(sun, Color.white, sunAlpha);

        if (sunAlpha > 0f)
        {
            float pulse = phase == Phase.Warning ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 12f) : 1f;
            float rise = phase == Phase.Warning ? Mathf.SmoothStep(0.4f, 1f, phaseTimer / Mathf.Max(0.01f, phaseLength)) : 1f;
            sun.rectTransform.localScale = Vector3.one * (pulse * rise);
            sun.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 20f);
        }

        // Heat bar: visible while there is heat or a sun.
        bool showHeat = running && (sunEvent || Heat01 > 0.01f);
        heatBar.gameObject.SetActive(showHeat);
        if (showHeat)
        {
            heatFill.rectTransform.anchorMax = new Vector2(Heat01, 1f);
            Color c = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(0.95f, 0.15f, 0.1f), Heat01);
            bool blink = Heat01 > 0.7f && Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f;
            heatFill.color = blink ? Color.white : c;
            heatLabel.text = covered && sunEvent ? "SHADE - cooling" : sunEvent ? "EGG HEAT - cover the sensor!" : "EGG HEAT";
        }

        // Fallback button when the phone has no usable sensor.
        bool wantButton = running && !detector.SensorUsable;
        if (shadeButton.activeSelf != wantButton)
        {
            shadeButton.SetActive(wantButton);
            if (!wantButton) detector.ManualCover = false;
        }
    }

    static void SetAlpha(Image image, Color color, float alpha)
    {
        color.a = alpha;
        image.color = color;
        image.enabled = alpha > 0.001f;
    }

    void ShowHint(string text)
    {
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(HintRoutine(text));
    }

    IEnumerator HintRoutine(string text)
    {
        hint.text = text;
        hint.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(4f);
        hint.gameObject.SetActive(false);
        hintRoutine = null;
    }

    // ---------------------------------------------------------------- egg tint

    void CacheEgg(Egg egg)
    {
        if (egg == null) return;
        block ??= new MaterialPropertyBlock();
        eggRenderers = egg.GetComponentsInChildren<Renderer>(true);
        eggColors = new Color[eggRenderers.Length];
        for (int i = 0; i < eggRenderers.Length; i++)
        {
            var mat = eggRenderers[i].sharedMaterial;
            eggColors[i] = mat == null ? Color.white
                : mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId)
                : mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.white;
        }
        TintEgg(0f);
    }

    void TintEgg(float heat)
    {
        if (eggRenderers == null) return;
        for (int i = 0; i < eggRenderers.Length; i++)
        {
            var r = eggRenderers[i];
            if (r == null) continue;
            Color c = Color.Lerp(eggColors[i], cookedEggColor, heat * 0.85f);
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }
    }

    // ---------------------------------------------------------------- UI build

    void BuildUI()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (flareSprite == null) flareSprite = RadialSprite(256, r => Mathf.Pow(1f - r, 2f));
        if (sunSprite == null) sunSprite = SunPlaceholder(256);
        if (shadeSprite == null) shadeSprite = RadialSprite(256, r => Mathf.SmoothStep(0.15f, 1f, r));

        var go = new GameObject("SunGlareCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // above the Egg Run UI, below the shared pause button (100)
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        var root = go.transform;

        glare = MakeImage(root, "Glare", null);
        Stretch(glare.rectTransform, -50f);

        flare = MakeImage(root, "Flare", flareSprite);
        Place(flare.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(170, -170), new Vector2(3200, 3200));

        shade = MakeImage(root, "Shade", shadeSprite);
        Stretch(shade.rectTransform, -300f);

        sun = MakeImage(root, "Sun", sunSprite);
        sun.preserveAspect = true;
        Place(sun.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(170, -170), new Vector2(260, 260));

        // Heat bar under the timer.
        var bar = new GameObject("HeatBar", typeof(RectTransform));
        bar.transform.SetParent(root, false);
        heatBar = (RectTransform)bar.transform;
        Place(heatBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(560, 46));
        heatBack = MakeImage(heatBar, "Back", null);
        heatBack.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(heatBack.rectTransform, 0f);
        heatFill = MakeImage(heatBar, "Fill", null);
        var fillRt = heatFill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.offsetMin = new Vector2(4, 4);
        fillRt.offsetMax = new Vector2(-4, -4);
        heatLabel = MakeText(heatBar, "Label", "EGG HEAT", 28);
        Stretch(heatLabel.rectTransform, 0f);
        heatLabel.color = Color.white;
        var outline = heatLabel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        bar.SetActive(false);

        hint = MakeText(root, "Hint", "", 52);
        Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1500, 220));
        hint.color = new Color(0.35f, 0.15f, 0.05f);
        hint.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.9f);
        hint.gameObject.SetActive(false);

        // Fallback "Shade" hold-button, bottom-left (the walk button is bottom-right).
        shadeButton = new GameObject("ShadeButton", typeof(RectTransform));
        shadeButton.transform.SetParent(root, false);
        Place((RectTransform)shadeButton.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(220, 220), new Vector2(280, 280));
        var buttonImage = shadeButton.AddComponent<Image>();
        buttonImage.sprite = RadialSprite(128, r => r < 0.95f ? 1f : 0f);
        buttonImage.color = new Color(0.15f, 0.2f, 0.35f, 0.85f);
        shadeButton.AddComponent<ShadeHoldButton>().detector = detector;
        var label = MakeText(shadeButton.transform, "Label", "SHADE", 54);
        Stretch(label.rectTransform, 0f);
        label.color = Color.white;
        shadeButton.SetActive(false);
    }

    Image MakeImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false; // taps go through to the game (walk button keeps working)
        image.enabled = false;
        return image;
    }

    Text MakeText(Transform parent, string name, string content, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.text = content;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rt, float outset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(outset, outset);
        rt.offsetMax = new Vector2(-outset, -outset);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // White texture whose alpha is f(distance from centre, 0..1).
    static Sprite RadialSprite(int size, Func<float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = Mathf.Clamp01(new Vector2(x - half, y - half).magnitude / half);
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(r)));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Yellow disc with a soft orange glow and rays.
    static Sprite SunPlaceholder(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var d = new Vector2(x - half, y - half);
            float r = d.magnitude / half;
            float angle = Mathf.Atan2(d.y, d.x);
            float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 6f); // 12 rays
            Color c;
            if (r < 0.45f) c = Color.Lerp(new Color(1f, 0.97f, 0.6f), new Color(1f, 0.82f, 0.15f), r / 0.45f);
            else
            {
                float glow = Mathf.Clamp01(1f - (r - 0.45f) / 0.55f);
                float a = Mathf.Max(glow * glow * 0.6f, ray * glow);
                c = new Color(1f, 0.7f, 0.1f, a);
            }
            pixels[y * size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
