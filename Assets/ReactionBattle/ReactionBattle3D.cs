using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ReactionBattle 3D v2 - everything is built in code. No assets needed.
///
/// SETUP (Unity 6, "Universal 3D" template)
///  1. Drop this file anywhere in Assets/ (e.g. Assets/Scripts/)  (replace the old version)
///  2. Open SampleScene and press Play. Nothing to attach.
///
/// FLOW
///  1. MENU    : its OWN screen (a little showroom): tap 3, 5, 10 or 15 = points needed to win
///  2. ROUNDS  : tap the lamp, wait, press your button when the signal comes
///               Fully random: random wait look, 0-3 fake alarms (random colours + sounds),
///               then the real signal = light+sound / light only / sound only (even in a random order)
///  3. BONUS   : its OWN screen (the arena). When a player reaches a bonus milestone they grab the phone and SHAKE it.
///               Random objects spawn; each shake collects some. Collect them all in time = bonus point.
///
/// BONUS MILESTONES (edit BonusMilestones() below)
///  3 points  : no bonus
///  5 points  : bonus when a player reaches 3
///  10 points : bonus at 3 and 7
///  15 points : bonus at 4, 8 and 12
///
/// TESTING ON A MAC (no accelerometer in the editor)
///  Z = Player 1, M = Player 2, Space = start round / fake "shake" during a bonus
/// </summary>
public class ReactionBattle3D : MonoBehaviour
{
    // ---------- Settings ----------
    const float MinDelay = 0.8f, MaxDelay = 6.0f;   // wait before the first event (wide = unpredictable)
    const float TextScale = 1f;          // make all 3D text bigger/smaller
    const int BonusPoints = 1;           // points for a successful bonus
    const float ShakeThreshold = 1.0f;   // lower = more sensitive shake (in g)

    static int[] BonusMilestones(int t)
    {
        switch (t)
        {
            case 5:  return new[] { 3 };
            case 10: return new[] { 3, 7 };
            case 15: return new[] { 4, 8, 12 };
            default: return new int[0];
        }
    }
    static int BonusItemCount(int t) { return t == 5 ? 5 : t == 10 ? 7 : 9; }
    static float BonusSeconds(int t) { return t == 5 ? 8f : t == 10 ? 7f : 6f; }

    static readonly Color P1Color = new Color(0.95f, 0.55f, 0.15f);
    static readonly Color P2Color = new Color(0.25f, 0.50f, 1.00f);

    // Score bar layout (in each player's local space)
    const float BarW = 1.8f, BarLeft = 2.6f, BarZ = -7.3f;

    // ---------- State ----------
    enum State { Menu, Idle, Waiting, Go, RoundOver, Bonus }
    enum Signal { Both, LightOnly, SoundOnly }

    State state = State.Menu;
    Signal signal;
    bool matchOver;
    int target = 5;
    int[] milestones = new int[0];
    readonly HashSet<int> bonusesDone = new HashSet<int>();
    int score1, score2;
    float goTime;
    Coroutine roundRoutine;

    // Bonus state
    bool bonusActive;
    int bonusPlayer;
    float lastShakeTime;
    Vector3 lastAccel;
    bool hasLastAccel, accelUnitKnown, accelInMs2;

    class Item
    {
        public Transform t;
        public float size, phase, spawn, flyStart;
        public Vector3 basePos, from, to;
    }
    readonly List<Item> items = new List<Item>();
    readonly List<Item> flying = new List<Item>();

    // ---------- Scene refs ----------
    Camera cam;
    Font font;
    AudioSource audioSrc;
    AudioClip goClip, collectClip, successClip, bonusStartClip;
    AudioClip[] decoyClips;
    Transform lampRoot, menuRoot;

    // Bonus arena: a separate little world far away from the table
    static readonly Vector3 ArenaOrigin = new Vector3(300f, 0f, 0f);
    Vector3 ArenaCamPos { get { return ArenaOrigin + new Vector3(0f, 6f, -14f); } }
    Transform arenaRoot, collector;
    Renderer collectorRenderer;
    GameObject arenaHud;
    TextMesh arenaTitle, arenaInfo;
    float camKick;

    // Three separate screens: the table (game), the menu showroom and the bonus arena
    enum ViewMode { Table, Menu, Arena }
    ViewMode viewMode = ViewMode.Table;
    static readonly Vector3 MenuOrigin = new Vector3(-300f, 0f, 0f);
    Vector3 MenuCamPos { get { return MenuOrigin + new Vector3(0f, 4.5f, -13f); } }
    static readonly Color TableBg = new Color(0.05f, 0.06f, 0.09f);
    GameObject menuHud;
    Renderer lampBulb;
    Light lampLight;

    readonly TextMesh[] statusTexts = new TextMesh[2];
    readonly TextMesh[] subTexts = new TextMesh[2];
    readonly TextMesh[] scoreTexts = new TextMesh[2];
    readonly TextMesh[] targetTexts = new TextMesh[2];
    readonly Transform[] barFill = new Transform[2];
    readonly Transform[] zoneT = new Transform[2];
    readonly List<GameObject>[] ticks = { new List<GameObject>(), new List<GameObject>() };
    readonly Transform[] buttonTops = new Transform[2];
    readonly Dictionary<Collider, System.Action> targets = new Dictionary<Collider, System.Action>();
    readonly List<Vector2> pointerDowns = new List<Vector2>();

    // Auto-create so you never have to attach anything.
    // Pocket Jungle: only in this minigame's own scene (the app starts on the main menu and loads
    // other scenes later, so it listens to every scene load instead of only the first one).
    public const string SceneName = "ReactionBattle";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) TryCreate(scene);
    }

    static void TryCreate(UnityEngine.SceneManagement.Scene scene)
    {
        if (scene.name != SceneName) return;
        if (FindAnyObjectByType<ReactionBattle3D>() == null)
            new GameObject("ReactionBattle3D").AddComponent<ReactionBattle3D>();
    }

    // Pocket Jungle: the match is over, show the shared Win screen (after a moment to see the result).
    void ReportMatch(int winner)
    {
        if (!GameUI.MiniGameBridge.IsAvailable) return;
        int best = Mathf.Max(score1, score2), other = Mathf.Min(score1, score2);
        GameUI.MiniGameBridge.Win("Player " + winner + " wins " + best + " - " + other + "!", 3, best, 2.5f);
    }

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if ENABLE_INPUT_SYSTEM
        if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
#endif
        BuildCameraAndLight();
        BuildAudio();
        BuildTable();
        BuildLamp();
        BuildZone(0, P1Color);
        BuildZone(1, P2Color);
        BuildMenu();
        BuildBonusArena();
        ShowMenu();
    }

    // =====================================================
    //  WORLD BUILDING
    // =====================================================
    void BuildCameraAndLight()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        // Top-down view, screen-up = +Z. Player 1 at the bottom, Player 2 at the top.
        cam.transform.SetPositionAndRotation(new Vector3(0, 18f, 0), Quaternion.Euler(90, 0, 0));
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = TableBg;

        if (FindAnyObjectByType<Light>() == null)
        {
            var go = new GameObject("Directional Light");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(55, -25, 0);
        }
    }

    void BuildTable()
    {
        Prim(PrimitiveType.Cube, "Table", transform, new Vector3(0, -0.25f, 0), new Vector3(9.4f, 0.5f, 19f), new Color(0.10f, 0.13f, 0.20f));
        Prim(PrimitiveType.Cube, "TableFrame", transform, new Vector3(0, -0.55f, 0), new Vector3(10.6f, 0.5f, 20.2f), new Color(0.35f, 0.20f, 0.10f));
    }

    void BuildLamp()
    {
        lampRoot = new GameObject("Lamp").transform;
        lampRoot.SetParent(transform, false);

        Prim(PrimitiveType.Cylinder, "LampBase", lampRoot, new Vector3(0, 0.15f, 0), new Vector3(3.4f, 0.15f, 3.4f), new Color(0.12f, 0.12f, 0.14f));

        var bulb = Prim(PrimitiveType.Sphere, "LampBulb", lampRoot, new Vector3(0, 0.8f, 0), Vector3.one * 2.6f, Color.gray, true);
        lampBulb = bulb.GetComponent<Renderer>();
        targets[bulb.GetComponent<Collider>()] = StartRound;

        var lightGO = new GameObject("LampLight");
        lightGO.transform.SetParent(lampRoot, false);
        lightGO.transform.localPosition = new Vector3(0, 3f, 0);
        lampLight = lightGO.AddComponent<Light>();
        lampLight.type = LightType.Point;
        lampLight.range = 11f;
        lampLight.intensity = 6f;
        lampLight.shadows = LightShadows.None;
    }

    // One player zone, built in Player 1's local space. Player 2's zone is simply rotated 180 degrees.
    void BuildZone(int i, Color color)
    {
        int player = i + 1;
        var zone = new GameObject("Zone_P" + player).transform;
        zone.SetParent(transform, false);
        zone.localRotation = Quaternion.Euler(0, i == 0 ? 0 : 180, 0);
        zoneT[i] = zone;

        // Button base + moving top
        Prim(PrimitiveType.Cylinder, "ButtonBase", zone, new Vector3(0, 0.15f, -6.5f), new Vector3(4.6f, 0.15f, 4.6f), new Color(0.1f, 0.1f, 0.12f));

        var top = new GameObject("ButtonTop").transform;
        top.SetParent(zone, false);
        top.localPosition = new Vector3(0, 0.6f, -6.5f);
        buttonTops[i] = top;

        var mesh = Prim(PrimitiveType.Cylinder, "ButtonMesh", top, Vector3.zero, new Vector3(4f, 0.3f, 4f), color);
        Destroy(mesh.GetComponent<Collider>());
        var box = mesh.AddComponent<BoxCollider>();
        box.size = new Vector3(1.3f, 2f, 1.3f);   // forgiving hit area
        int p = player;
        targets[box] = () => OnPlayerPress(p);

        MakeText(top, "PRESS", new Vector3(0, 0.32f, 0), 1.1f, new Color(0.25f, 0.06f, 0.06f));

        // Score, "of N" and progress bar (gold ticks = bonus milestones)
        scoreTexts[i] = MakeText(zone, "0", new Vector3(3.5f, 0.05f, -6.0f), 1.8f, Color.white);
        targetTexts[i] = MakeText(zone, "", new Vector3(3.5f, 0.05f, -5.0f), 0.45f, new Color(0.75f, 0.75f, 0.8f));

        Prim(PrimitiveType.Cube, "BarBg", zone, new Vector3(BarLeft + BarW / 2f, 0.03f, BarZ), new Vector3(BarW, 0.06f, 0.28f), new Color(0.2f, 0.2f, 0.25f));
        var fill = Prim(PrimitiveType.Cube, "BarFill", zone, new Vector3(BarLeft, 0.07f, BarZ), new Vector3(0.001f, 0.08f, 0.28f), color);
        Glow(fill.GetComponent<Renderer>(), color, 1.5f);
        barFill[i] = fill.transform;

        // Status text readable by this player
        statusTexts[i] = MakeText(zone, "", new Vector3(0, 0.05f, -2.3f), 0.8f, Color.white);
        subTexts[i] = MakeText(zone, "", new Vector3(0, 0.05f, -3.7f), 0.5f, new Color(0.8f, 0.8f, 0.85f));
    }

    // The MENU SCREEN: a separate little showroom with four big blocks (3, 5, 10, 15).
    void BuildMenu()
    {
        menuRoot = new GameObject("MenuRoom").transform;
        menuRoot.SetParent(transform, false);
        menuRoot.position = MenuOrigin;

        Prim(PrimitiveType.Cube, "MenuFloor", menuRoot, new Vector3(0, -0.25f, 0), new Vector3(24f, 0.5f, 18f), new Color(0.10f, 0.12f, 0.20f));
        Prim(PrimitiveType.Cube, "MenuWall", menuRoot, new Vector3(0, 9f, 5.5f), new Vector3(30f, 22f, 0.5f), new Color(0.07f, 0.09f, 0.17f));

        var lg = new GameObject("MenuLight");
        lg.transform.SetParent(menuRoot, false);
        lg.transform.localPosition = new Vector3(0, 9f, -4f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 30f;
        l.intensity = 10f;
        l.shadows = LightShadows.None;

        int[] options = { 3, 5, 10, 15 };
        float[] xs = { -1.5f, 1.5f, -1.5f, 1.5f };
        float[] ys = { 5.9f, 5.9f, 2.7f, 2.7f };

        for (int i = 0; i < options.Length; i++)
        {
            int n = options[i];
            Color col = Color.HSVToRGB(0.50f + 0.08f * i, 0.65f, 0.9f);

            var block = new GameObject("Block" + n).transform;
            block.SetParent(menuRoot, false);
            block.localPosition = new Vector3(xs[i], ys[i], 0f);

            var mesh = Prim(PrimitiveType.Cube, "BlockMesh", block, Vector3.zero, new Vector3(2.8f, 2.8f, 1.4f), col, true);
            Glow(mesh.GetComponent<Renderer>(), col, 0.35f);
            targets[mesh.GetComponent<Collider>()] = () => SelectTarget(n);

            var num = MakeText(block, n.ToString(), new Vector3(0, 0.35f, -0.72f), 1.5f, Color.white);
            num.transform.localRotation = Quaternion.identity;
            var info = MakeText(block, BonusInfo(n), new Vector3(0, -0.85f, -0.72f), 0.42f, new Color(1f, 0.92f, 0.5f));
            info.transform.localRotation = Quaternion.identity;
        }

        // Title + footer stuck to the camera
        menuHud = new GameObject("MenuHud");
        menuHud.transform.SetParent(cam.transform, false);
        var title = MakeText(menuHud.transform, "CHOOSE YOUR\nGAME", new Vector3(0, 3.5f, 8f), 0.8f, Color.white);
        title.transform.localRotation = Quaternion.identity;
        var foot = MakeText(menuHud.transform, "Tap a number", new Vector3(0, -3.4f, 8f), 0.45f, new Color(0.8f, 0.8f, 0.85f));
        foot.transform.localRotation = Quaternion.identity;
        menuHud.SetActive(false);

        menuRoot.gameObject.SetActive(false);
    }

    static string BonusInfo(int n)
    {
        int c = BonusMilestones(n).Length;
        return c == 0 ? "no bonus" : c == 1 ? "1 bonus" : c + " bonuses";
    }

    // ---------- Bonus arena (different view, nothing to do with the buttons) ----------
    void BuildBonusArena()
    {
        arenaRoot = new GameObject("BonusArena").transform;
        arenaRoot.SetParent(transform, false);
        arenaRoot.position = ArenaOrigin;

        Prim(PrimitiveType.Cube, "ArenaFloor", arenaRoot, new Vector3(0, -0.25f, 0), new Vector3(18f, 0.5f, 14f), new Color(0.14f, 0.09f, 0.26f));
        Prim(PrimitiveType.Cube, "ArenaWall", arenaRoot, new Vector3(0, 9f, 7.25f), new Vector3(18f, 20f, 0.5f), new Color(0.22f, 0.12f, 0.40f));
        Prim(PrimitiveType.Cube, "ArenaWallL", arenaRoot, new Vector3(-9.25f, 9f, 0), new Vector3(0.5f, 20f, 14f), new Color(0.18f, 0.10f, 0.34f));
        Prim(PrimitiveType.Cube, "ArenaWallR", arenaRoot, new Vector3(9.25f, 9f, 0), new Vector3(0.5f, 20f, 14f), new Color(0.18f, 0.10f, 0.34f));

        // Glowing pillars for atmosphere
        for (int i = 0; i < 6; i++)
        {
            float x = i % 2 == 0 ? -4.6f : 4.6f;
            float z = (i / 2) * 2.5f;
            var pillar = Prim(PrimitiveType.Cylinder, "Pillar" + i, arenaRoot, new Vector3(x, 3f, z), new Vector3(0.7f, 3f, 0.7f), Color.white);
            Glow(pillar.GetComponent<Renderer>(), Color.HSVToRGB(0.70f + 0.05f * i, 0.7f, 1f), 1.5f);
        }

        // The glowing pad that the shaken items fly into
        var pad = Prim(PrimitiveType.Cylinder, "Collector", arenaRoot, new Vector3(0, 0.1f, -3f), new Vector3(3.4f, 0.1f, 3.4f), Color.white);
        collectorRenderer = pad.GetComponent<Renderer>();
        collector = pad.transform;

        var lg = new GameObject("ArenaLight");
        lg.transform.SetParent(arenaRoot, false);
        lg.transform.localPosition = new Vector3(0, 9f, -4f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 30f;
        l.intensity = 10f;
        l.color = new Color(0.9f, 0.8f, 1f);
        l.shadows = LightShadows.None;

        // Text stuck to the camera so it is always readable during the bonus
        arenaHud = new GameObject("BonusHud");
        arenaHud.transform.SetParent(cam.transform, false);
        arenaTitle = MakeText(arenaHud.transform, "", new Vector3(0, 3.3f, 8f), 0.75f, Color.white);
        arenaInfo = MakeText(arenaHud.transform, "", new Vector3(0, 1.9f, 8f), 0.6f, new Color(1f, 0.9f, 0.4f));
        arenaTitle.transform.localRotation = Quaternion.identity;
        arenaInfo.transform.localRotation = Quaternion.identity;
        arenaHud.SetActive(false);

        arenaRoot.gameObject.SetActive(false);
    }

    void EnterArena(int player)
    {
        SetView(ViewMode.Arena);
        Glow(collectorRenderer, player == 1 ? P1Color : P2Color, 2f);
    }

    void ExitArena()
    {
        SetView(ViewMode.Table);
    }

    // Switches between the three screens: Table (game), Menu (showroom) and Arena (bonus)
    void SetView(ViewMode v)
    {
        viewMode = v;
        menuRoot.gameObject.SetActive(v == ViewMode.Menu);
        menuHud.SetActive(v == ViewMode.Menu);
        arenaRoot.gameObject.SetActive(v == ViewMode.Arena);
        arenaHud.SetActive(v == ViewMode.Arena);

        switch (v)
        {
            case ViewMode.Table:
                cam.transform.SetPositionAndRotation(new Vector3(0, 18f, 0), Quaternion.Euler(90, 0, 0));
                cam.backgroundColor = TableBg;
                break;
            case ViewMode.Menu:
                cam.transform.SetPositionAndRotation(MenuCamPos, Quaternion.Euler(5f, 0f, 0f));
                cam.backgroundColor = new Color(0.04f, 0.05f, 0.10f);
                break;
            case ViewMode.Arena:
                cam.transform.SetPositionAndRotation(ArenaCamPos, Quaternion.Euler(10f, 0f, 0f));
                cam.backgroundColor = new Color(0.10f, 0.05f, 0.18f);
                break;
        }
    }

    void SetBonusTexts(string title, string info)
    {
        arenaTitle.text = title;
        arenaInfo.text = info;
    }

    void SetBonusInfo(string info)
    {
        arenaInfo.text = info;
    }

    // ---------- Helpers ----------
    GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material.color = color;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        return go;
    }

    static void Glow(Renderer r, Color c, float intensity)
    {
        var m = r.material;
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
    }

    TextMesh MakeText(Transform parent, string content, Vector3 localPos, float lineHeight, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90, 0, 0); // lie flat, readable from above
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 100;
        tm.characterSize = lineHeight * 0.1f * TextScale;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        tm.text = content;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return tm;
    }

    // =====================================================
    //  AUDIO (generated in code)
    // =====================================================
    void BuildAudio()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        goClip = MakeTone("GoBeep", 880f, 0.35f, 0.6f, false);
        decoyClips = new[]
        {
            MakeTone("DecoyBuzzLow", 170f, 0.40f, 0.5f, true),
            MakeTone("DecoyBuzzMid", 330f, 0.35f, 0.5f, true),
            MakeTone("DecoyTone", 520f, 0.30f, 0.5f, false),
        };
        collectClip = MakeTone("Collect", 1300f, 0.09f, 0.5f, false);
        successClip = MakeTone("Success", 1500f, 0.35f, 0.5f, false);
        bonusStartClip = MakeTone("BonusStart", 660f, 0.30f, 0.5f, false);
    }

    static AudioClip MakeTone(string name, float freq, float seconds, float volume, bool buzzy)
    {
        const int rate = 44100;
        int n = (int)(rate * seconds);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float s = Mathf.Sin(2f * Mathf.PI * freq * t);
            if (buzzy) s = 0.6f * Mathf.Sign(s) + 0.4f * s;
            float env = Mathf.Min(1f, i / 300f) * Mathf.Min(1f, (n - i) / 3000f);
            data[i] = s * volume * env;
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void Play(AudioClip clip) { audioSrc.PlayOneShot(clip); }

    static void Vibrate()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    // =====================================================
    //  INPUT (works with the new Input System and the old one)
    // =====================================================
    void Update()
    {
        CollectPointerDowns();
        foreach (var pos in pointerDowns)
        {
            var ray = cam.ScreenPointToRay(pos);
            if (Physics.Raycast(ray, out var hit, 100f) && targets.TryGetValue(hit.collider, out var action))
                action();
        }

        if (KeyDown(0)) OnPlayerPress(1);
        if (KeyDown(1)) OnPlayerPress(2);
        if (state != State.Bonus && KeyDown(2)) StartRound();

        if (bonusActive) HandleShake();

        if (viewMode == ViewMode.Arena)
        {
            camKick = Mathf.MoveTowards(camKick, 0f, 4f * Time.deltaTime);
            cam.transform.position = ArenaCamPos + Random.insideUnitSphere * camKick;
        }

        AnimateItems();
    }

    void CollectPointerDowns()
    {
        pointerDowns.Clear();
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
            foreach (var t in Touchscreen.current.touches)
                if (t.press.wasPressedThisFrame) pointerDowns.Add(t.position.ReadValue());
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            pointerDowns.Add(Mouse.current.position.ReadValue());
#else
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) pointerDowns.Add(Input.GetTouch(i).position);
        if (Input.GetMouseButtonDown(0)) pointerDowns.Add(Input.mousePosition);
#endif
    }

    // 0 = Player 1 (Z), 1 = Player 2 (M), 2 = Start / fake shake (Space)
    static bool KeyDown(int which)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;
        return which == 0 ? k.zKey.wasPressedThisFrame
             : which == 1 ? k.mKey.wasPressedThisFrame
             : k.spaceKey.wasPressedThisFrame;
#else
        return which == 0 ? Input.GetKeyDown(KeyCode.Z)
             : which == 1 ? Input.GetKeyDown(KeyCode.M)
             : Input.GetKeyDown(KeyCode.Space);
#endif
    }

    // ---------- Shake detection ----------
    bool ReadAccel(out Vector3 a)
    {
#if ENABLE_INPUT_SYSTEM
        var acc = Accelerometer.current;
        if (acc != null) { a = acc.acceleration.ReadValue(); return true; }
        a = Vector3.zero;
        return false;
#else
        if (SystemInfo.supportsAccelerometer) { a = Input.acceleration; return true; }
        a = Vector3.zero;
        return false;
#endif
    }

    float ReadShake()
    {
        float s = 0f;
        Vector3 a;
        if (ReadAccel(out a))
        {
            // Some devices report m/s^2, others g. Detect once from the resting reading.
            if (!accelUnitKnown && a.magnitude > 0.2f) { accelInMs2 = a.magnitude > 3f; accelUnitKnown = true; }
            if (accelInMs2) a /= 9.81f;
            if (hasLastAccel) s = (a - lastAccel).magnitude;
            lastAccel = a;
            hasLastAccel = true;
        }
        if (KeyDown(2)) s = Mathf.Max(s, ShakeThreshold + 1f); // Space = fake shake in the editor
        return s;
    }

    void HandleShake()
    {
        float s = ReadShake();
        if (s > ShakeThreshold && Time.time - lastShakeTime > 0.12f)
        {
            lastShakeTime = Time.time;
            Collect(s > 3f ? 2 : 1);   // a really hard shake grabs two at once
        }
    }

    // =====================================================
    //  MENU
    // =====================================================
    void ShowMenu()
    {
        state = State.Menu;
        matchOver = false;
        score1 = score2 = 0;
        bonusesDone.Clear();
        ClearItems();
        ClearTicks();
        lampRoot.gameObject.SetActive(false);
        SetView(ViewMode.Menu);   // the menu is its own screen
        SetVisual("idle");
        SetTexts("", "");
        for (int i = 0; i < 2; i++) targetTexts[i].text = "";
        UpdateScores();
    }

    void SelectTarget(int n)
    {
        if (state != State.Menu) return;

        target = n;
        milestones = BonusMilestones(n);
        bonusesDone.Clear();
        score1 = score2 = 0;
        matchOver = false;

        SetView(ViewMode.Table);   // back to the game screen
        lampRoot.gameObject.SetActive(true);
        SetupTicks();
        UpdateScores();

        state = State.Idle;
        SetVisual("idle");
        SetTexts("TAP THE LAMP", "First to " + n + " wins");
    }

    void ClearTicks()
    {
        for (int i = 0; i < 2; i++)
        {
            foreach (var g in ticks[i]) if (g != null) Destroy(g);
            ticks[i].Clear();
        }
    }

    void SetupTicks()
    {
        ClearTicks();
        for (int i = 0; i < 2; i++)
        {
            targetTexts[i].text = "of " + target;
            foreach (int m in milestones)
            {
                float x = BarLeft + BarW * m / target;
                var tick = Prim(PrimitiveType.Cube, "Tick", zoneT[i], new Vector3(x, 0.12f, BarZ), new Vector3(0.09f, 0.16f, 0.5f), Color.yellow);
                Glow(tick.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.2f), 2f);
                ticks[i].Add(tick);
            }
        }
    }

    // =====================================================
    //  GAME LOGIC
    // =====================================================
    void StartRound()
    {
        if (state != State.Idle && state != State.RoundOver) return;

        if (matchOver)
        {
            ShowMenu();   // match finished: back to the menu to pick a new game
            return;
        }
        roundRoutine = StartCoroutine(RoundRoutine());
    }

    // Everything about the start of a round is random, so no round feels like the last one
    static readonly string[] WaitLooks  = { "waiting", "waiting", "dark", "blue" };
    static readonly string[] WaitTitles = { "WAIT...", "READY?", "HOLD ON...", "STEADY...", "..." };
    static readonly string[] WaitSubs   = { "Eyes and ears open", "Stay sharp", "Don't blink", "Listen closely", "Patience..." };
    static readonly Color[] DecoyColors =
    {
        new Color(1.0f, 0.85f, 0.10f), new Color(1.0f, 0.45f, 0.05f), new Color(1.0f, 0.25f, 0.80f),
        new Color(0.10f, 0.85f, 1.0f), new Color(1.0f, 1.0f, 1.0f)
    };

    static int RandomDecoyCount()
    {
        float r = Random.value;
        return r < 0.30f ? 0 : r < 0.60f ? 1 : r < 0.85f ? 2 : 3;
    }

    IEnumerator RoundRoutine()
    {
        state = State.Waiting;

        // Random "waiting" look: red, dark or blue - so the start never gives anything away
        string look = WaitLooks[Random.Range(0, WaitLooks.Length)];
        SetVisual(look);
        SetTexts(WaitTitles[Random.Range(0, WaitTitles.Length)], WaitSubs[Random.Range(0, WaitSubs.Length)]);

        yield return new WaitForSeconds(Random.Range(MinDelay, MaxDelay));

        // 0 to 3 fake alarms: each one a random mix of coloured flash and/or sound
        int decoys = RandomDecoyCount();
        for (int d = 0; d < decoys; d++)
        {
            int kind = Random.Range(0, 3);   // 0 = light, 1 = sound, 2 = both
            if (kind != 1) SetLamp(DecoyColors[Random.Range(0, DecoyColors.Length)], 3f);
            if (kind != 0) Play(decoyClips[Random.Range(0, decoyClips.Length)]);
            yield return new WaitForSeconds(Random.Range(0.2f, 0.6f));
            SetVisual(look);
            yield return new WaitForSeconds(Random.Range(0.4f, 2.2f));
        }

        // The real signal: green light and/or the high beep
        float r = Random.value;
        signal = r < 0.30f ? Signal.Both : r < 0.65f ? Signal.LightOnly : Signal.SoundOnly;
        state = State.Go;
        goTime = Time.unscaledTime;

        switch (signal)
        {
            case Signal.Both:
            {
                int order = Random.Range(0, 3);          // 0 together, 1 sound first, 2 light first
                if (order != 1) ShowGo();
                if (order != 2) Play(goClip);
                if (order == 1) { yield return new WaitForSeconds(0.25f); ShowGo(); }
                if (order == 2) { yield return new WaitForSeconds(0.25f); Play(goClip); }
                break;
            }
            case Signal.LightOnly:
                ShowGo();
                break;
            case Signal.SoundOnly:
                Play(goClip);   // the lamp keeps its waiting look - the trap
                break;
        }
    }

    void ShowGo()
    {
        SetVisual("go");
        SetTexts("GO!", "");
    }

    void OnPlayerPress(int player)
    {
        int other = player == 1 ? 2 : 1;
        if (state == State.Waiting || state == State.Go)
        {
            StartCoroutine(PressAnim(buttonTops[player - 1]));
            if (roundRoutine != null) StopCoroutine(roundRoutine);   // also cancels a half-finished signal
        }

        if (state == State.Waiting)
        {
            if (roundRoutine != null) StopCoroutine(roundRoutine);
            EndRound(other, "foul", "PLAYER " + player + "\nJUMPED THE GUN!", "Point to Player " + other + "\nTap the lamp");
        }
        else if (state == State.Go)
        {
            int ms = Mathf.RoundToInt((Time.unscaledTime - goTime) * 1000f);
            EndRound(player, "idle", "PLAYER " + player + "\nWINS THE ROUND!", ms + " ms (" + SignalName() + ")\nTap the lamp");
        }
    }

    void EndRound(int winner, string visual, string status, string sub)
    {
        state = State.RoundOver;
        if (winner == 1) score1++; else score2++;
        UpdateScores();
        int s = winner == 1 ? score1 : score2;

        if (s >= target)
        {
            matchOver = true;
            SetVisual("over");
            SetTexts("PLAYER " + winner + "\nWINS THE MATCH!", "Tap the lamp\nfor a new game");
            ReportMatch(winner);
            return;
        }

        SetVisual(visual);

        // Bonus milestone reached for the first time?
        if (System.Array.IndexOf(milestones, s) >= 0 && !bonusesDone.Contains(s))
        {
            bonusesDone.Add(s);
            state = State.Bonus;
            SetTexts(status, "Bonus coming up...");
            StartCoroutine(BonusRoutine(winner));
            return;
        }

        SetTexts(status, sub);
    }

    string SignalName()
    {
        return signal == Signal.Both ? "light + sound" : signal == Signal.LightOnly ? "light only" : "sound only";
    }

    IEnumerator PressAnim(Transform t)
    {
        var up = new Vector3(0, 0.6f, -6.5f);
        t.localPosition = up + Vector3.down * 0.18f;
        yield return new WaitForSeconds(0.12f);
        t.localPosition = up;
    }

    // =====================================================
    //  BONUS ROUND: grab the phone and SHAKE
    // =====================================================
    IEnumerator BonusRoutine(int player)
    {
        yield return new WaitForSeconds(2.2f);   // let everyone read the round result

        // Switch to the separate bonus arena: a different view, away from the buttons
        EnterArena(player);
        SetVisual("bonus");
        Play(bonusStartClip);
        Vibrate();
        SetBonusTexts("BONUS TIME!\nPLAYER " + player, "Grab the phone!");
        yield return new WaitForSeconds(2.2f);

        for (int c = 3; c >= 1; c--)
        {
            SetBonusTexts(c.ToString(), "Get ready to SHAKE");
            yield return new WaitForSeconds(0.8f);
        }

        SetBonusTexts("SHAKE!", "");
        SpawnItems(BonusItemCount(target));

        bonusPlayer = player;
        hasLastAccel = false;
        lastShakeTime = Time.time;
        bonusActive = true;

        float t = BonusSeconds(target);
        while (bonusActive && t > 0f)
        {
            t -= Time.deltaTime;
            SetBonusInfo(items.Count + " left    " + Mathf.Max(0f, t).ToString("0.0") + "s");
            yield return null;
        }
        bonusActive = false;

        bool success = items.Count == 0;
        SetBonusTexts(success ? "ALL COLLECTED!" : "TOO SLOW!", "");
        yield return new WaitForSeconds(1.0f);
        if (!success) ClearItems();
        ExitArena();   // back to the table

        if (success)
        {
            if (player == 1) score1 += BonusPoints; else score2 += BonusPoints;
            UpdateScores();
            Play(successClip);
            Vibrate();

            int sc = player == 1 ? score1 : score2;
            if (sc >= target)
            {
                matchOver = true;
                state = State.RoundOver;
                SetVisual("over");
                SetTexts("PLAYER " + player + "\nWINS THE MATCH!", "Tap the lamp\nfor a new game");
                ReportMatch(player);
                yield break;
            }
            SetVisual("idle");
            SetTexts("BONUS WON!\n+" + BonusPoints + " PLAYER " + player, "Tap the lamp");
        }
        else
        {
            SetVisual("idle");
            SetTexts("TOO SLOW!", "No bonus - tap the lamp");
        }
        state = State.RoundOver;
    }

    void SpawnItems(int count)
    {
        PrimitiveType[] types = { PrimitiveType.Sphere, PrimitiveType.Cube, PrimitiveType.Capsule, PrimitiveType.Cylinder };
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = ArenaOrigin + new Vector3(Random.Range(-3f, 3f), Random.Range(1.8f, 6f), Random.Range(-2.5f, 2.5f));

            Color col = Color.HSVToRGB(Random.value, 0.75f, 1f);
            var go = Prim(types[Random.Range(0, types.Length)], "BonusItem", transform, pos, Vector3.zero, col);
            Glow(go.GetComponent<Renderer>(), col, 1.2f);

            items.Add(new Item
            {
                t = go.transform,
                size = Random.Range(0.9f, 1.4f),
                phase = Random.Range(0f, 6.28f),
                spawn = Time.time + i * 0.05f,
                basePos = pos
            });
        }
    }

    void Collect(int n)
    {
        for (int k = 0; k < n && items.Count > 0; k++)
        {
            var it = items[Random.Range(0, items.Count)];
            items.Remove(it);
            it.from = it.t.position;
            it.to = collector.position + Vector3.up * 0.6f;
            it.flyStart = Time.time;
            flying.Add(it);
            Play(collectClip);
            camKick = 0.4f;
        }
        if (items.Count == 0) bonusActive = false;
    }

    void AnimateItems()
    {
        foreach (var it in items)
        {
            float pop = Mathf.Clamp01((Time.time - it.spawn) / 0.3f);
            it.t.localScale = Vector3.one * it.size * pop;
            it.t.position = it.basePos + Vector3.up * Mathf.Sin(Time.time * 3f + it.phase) * 0.2f;
            it.t.Rotate(40f * Time.deltaTime * (1f + it.phase % 1f), 70f * Time.deltaTime, 0f);
        }

        for (int i = flying.Count - 1; i >= 0; i--)
        {
            var it = flying[i];
            float k = (Time.time - it.flyStart) / 0.45f;
            if (k >= 1f)
            {
                Destroy(it.t.gameObject);
                flying.RemoveAt(i);
                continue;
            }
            it.t.position = Vector3.Lerp(it.from, it.to, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.5f;
            it.t.localScale = Vector3.one * it.size * (1f - k);
        }
    }

    void ClearItems()
    {
        foreach (var it in items) if (it.t != null) Destroy(it.t.gameObject);
        foreach (var it in flying) if (it.t != null) Destroy(it.t.gameObject);
        items.Clear();
        flying.Clear();
    }

    // =====================================================
    //  VISUAL UPDATES
    // =====================================================
    void SetVisual(string name)
    {
        Color c;
        float glow;
        switch (name)
        {
            case "waiting": c = new Color(1.0f, 0.12f, 0.12f); glow = 2.0f; break;
            case "blue":    c = new Color(0.20f, 0.45f, 1.00f); glow = 2.0f; break;
            case "dark":    c = new Color(0.06f, 0.06f, 0.08f); glow = 0.0f; break;
            case "go":      c = new Color(0.10f, 1.00f, 0.30f); glow = 3.0f; break;
            case "decoy":   c = new Color(1.0f, 0.85f, 0.10f);  glow = 3.0f; break;
            case "foul":    c = new Color(1.0f, 0.40f, 0.05f);  glow = 2.5f; break;
            case "over":    c = new Color(1.0f, 0.80f, 0.25f);  glow = 2.0f; break;
            case "bonus":   c = new Color(0.70f, 0.30f, 1.00f); glow = 2.5f; break;
            default:        c = new Color(0.55f, 0.58f, 0.65f); glow = 0.4f; break; // idle
        }
        SetLamp(c, glow);
    }

    void SetLamp(Color c, float glow)
    {
        Glow(lampBulb, c, glow);
        lampLight.color = c;
        lampLight.intensity = glow < 0.1f ? 0.3f : 6f;
    }

    void SetTexts(string status, string sub)
    {
        for (int i = 0; i < 2; i++)
        {
            statusTexts[i].text = status;
            subTexts[i].text = sub;
        }
    }

    void SetSub(string sub)
    {
        for (int i = 0; i < 2; i++) subTexts[i].text = sub;
    }

    void UpdateScores()
    {
        int[] s = { score1, score2 };
        for (int i = 0; i < 2; i++)
        {
            scoreTexts[i].text = s[i].ToString();
            float frac = Mathf.Clamp01(s[i] / (float)Mathf.Max(1, target));
            float w = Mathf.Max(BarW * frac, 0.001f);
            barFill[i].localScale = new Vector3(w, 0.08f, 0.28f);
            barFill[i].localPosition = new Vector3(BarLeft + w / 2f, 0.07f, BarZ);
        }
    }
}
