using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DuoRoad
{

// EASY mode controls (landscape, one phone).
//   Round 1 (Wheel mode, two players):
//     RIGHT (driver):  a steering wheel you turn with a finger, brake and gas pedals underneath.
//     LEFT  (gunner):  an analog stick at the bottom for aiming, FIRE above it, BOMB next to FIRE.
//   Rounds 2 and 3 (Tilt mode, one player):
//     RIGHT:  brake and gas pedals (no wheel: tilt the phone left / right to steer).
//     LEFT:   FIRE and BOMB (no stick: tilt the top edge away to aim behind, back to level to aim ahead).
// Everything is drawn from code (no images needed) and supports several fingers at once.
//
//   Rounds 4 and 5 (Voice mode, one player): tilt to steer and aim like rounds 2 and 3, but NO buttons at all.
//     The gas is automatic. Say "BRAKE" (more words = harder braking), "SHOOT" (one shot per word)
//     and "BOMB" (uses a bomb potion). See VoiceInput.
//
// Test in the Unity editor with keyboard + mouse:
//   Driver: A/D or arrows = steer, W/Up = gas, S/Down = brake
//   Gunner: J/L = aim left/right, I/K = aim up/down, Space = fire, E = bomb
//   Tilt rounds: A/D = steer, hold K = aim behind (release = ahead)
//   Voice rounds: each key press = one spoken word: S = "brake", Space = "shoot", E = "bomb"
//   Mouse: drag the wheel or the stick, click the pedals or the fire button
public class TouchInput : MonoBehaviour, IGameInput, IVoiceControls
{
    [Header("Wheel")]
    public float maxLockDegrees = 140f;
    public float wheelReturnSpeed = 450f;   // degrees per second when you let go

    public enum SteerMode { Wheel, Tilt }

    [Header("Steering mode")]
    public SteerMode steerMode = SteerMode.Wheel;   // Wheel = on-screen wheel, Tilt = tilt the phone (no wheel drawn)
    public bool inputEnabled = true;                // false = nothing is pressed (menus, pause, round over)
    public TiltInput tilt = new TiltInput();

    [Header("Voice rounds")]
    public VoiceInput voice;                  // added by GameBootstrap
    public float brakePerWord = 0.35f;        // seconds of braking added by each "brake" (a bit of braking)
    public float maxBrakeTime = 1.5f;         // many words in a row stack up to this (= full stop to minimum speed)

    public float Steer { get; private set; }
    public bool Accelerate { get; private set; }
    public bool Brake { get; private set; }
    public float AimX { get; private set; }
    public float AimY { get; private set; }
    public bool Fire { get; private set; }
    public bool Special { get; private set; }

    // Set by PlayerAbilities so the BOMB button can show how many charges are left
    public int SpecialCharges;

    // ---- voice state
    public bool VoiceMode { get { return voiceMode; } }
    public float BrakeLevel { get { return Mathf.Clamp01(brakeTimer / maxBrakeTime); } }

    bool voiceMode;
    float brakeTimer;
    int pendingShots, pendingBombs;
    readonly float[] heardAt = { -10f, -10f, -10f };

    struct Pointer { public int id; public Vector2 pos; }
    const int None = int.MinValue;

    readonly List<Pointer> pointers = new List<Pointer>();

    // Layout in pixels, origin at the bottom-left of the screen
    Vector2 wheelC, stickC, fireC, specialC;
    float wheelR, stickR, fireR, specialR;
    Rect gasRect, brakeRect;

    int wheelId = None, stickId = None;
    float wheelAngle, lastPhi;
    Vector2 stickVec;

    Texture2D wheelTex, stickBaseTex, knobTex, fireTex, specialTex;
    GUIStyle labelStyle;

    void Awake()
    {
        EnsureTextures();
        tilt.Enable();
    }

    // Wheel (round 1) or Tilt (solo rounds). Switching clears any leftover steering.
    public void SetSteerMode(SteerMode mode)
    {
        steerMode = mode;
        wheelId = None;
        wheelAngle = 0f;
        Steer = 0f;
        if (mode == SteerMode.Tilt) tilt.Calibrate();
    }

    // Voice rounds (4 and 5): no buttons, the player's voice does gas / brake / fire / bomb.
    // Call after SetSteerMode(Tilt). Turning it on also starts listening to the microphone.
    public void SetVoiceMode(bool on)
    {
        voiceMode = on;
        ClearVoice();

        if (voice == null) return;
        voice.CommandHeard -= OnVoiceCommand;
        if (on)
        {
            voice.CommandHeard += OnVoiceCommand;
            voice.StartListening();
        }
        else
        {
            voice.StopListening();
        }
    }

    // Microphone on / off without leaving voice mode (off when the round is over)
    public void SetVoiceListening(bool on)
    {
        if (voice == null || !voiceMode) return;
        if (on) voice.StartListening();
        else voice.StopListening();
    }

    void ClearVoice()
    {
        brakeTimer = 0f;
        pendingShots = 0;
        pendingBombs = 0;
    }

    // One spoken word (or one key press in the editor)
    void OnVoiceCommand(VoiceInput.Command c)
    {
        if (!voiceMode || !inputEnabled) return;

        heardAt[(int)c] = Time.unscaledTime;

        switch (c)
        {
            case VoiceInput.Command.Brake:
                brakeTimer = Mathf.Min(maxBrakeTime, brakeTimer + brakePerWord);
                break;
            case VoiceInput.Command.Shoot:
                pendingShots = Mathf.Min(pendingShots + 1, 12);
                break;
            case VoiceInput.Command.Bomb:
                pendingBombs = Mathf.Min(pendingBombs + 1, 3);
                break;
        }
    }

    public int ConsumeShots()
    {
        int n = pendingShots;
        pendingShots = 0;
        return n;
    }

    public int ConsumeBombs()
    {
        int n = pendingBombs;
        pendingBombs = 0;
        return n;
    }

    // The current phone angle becomes "straight ahead" (called when a round starts / resumes)
    public void Recalibrate()
    {
        tilt.Calibrate();
    }

    void ReleaseAll()
    {
        wheelId = None;
        stickId = None;
        stickVec = Vector2.zero;
        wheelAngle = 0f;
        Steer = 0f;
        AimX = 0f;
        AimY = 0f;
        Accelerate = false;
        Brake = false;
        Fire = false;
        Special = false;
        ClearVoice();
    }

    // ------------------------------------------------------------------ input

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        ComputeLayout();

        if (!inputEnabled)
        {
            ReleaseAll();
            return;
        }

        GatherPointers();

        var kb = Keyboard.current;
        UpdateWheel(dt, kb);
        UpdateStick(kb);
        UpdateButtons(dt, kb);
    }

    void ComputeLayout()
    {
        float h = Screen.height, w = Screen.width;

        // RIGHT side = driving: the wheel (round 1 only) with the brake and gas pedals underneath
        wheelC = new Vector2(w - 0.34f * h, 0.60f * h);
        wheelR = 0.24f * h;
        brakeRect = new Rect(w - 0.57f * h, 0.05f * h, 0.20f * h, 0.20f * h);   // wide and short
        gasRect = new Rect(w - 0.25f * h, 0.05f * h, 0.15f * h, 0.28f * h);     // narrow and tall

        // LEFT side = shooting
        if (steerMode == SteerMode.Wheel)
        {
            stickC = new Vector2(0.26f * h, 0.19f * h);
            stickR = 0.15f * h;
            fireC = new Vector2(0.26f * h, 0.52f * h);
            fireR = 0.11f * h;

            // Big bomb button: to the right of the fire button, clear of the stick
            specialC = new Vector2(0.56f * h, 0.45f * h);
            specialR = 0.09f * h;
        }
        else if (voiceMode)
        {
            // Voice rounds: no buttons. The bomb counter just shows how many potions are left.
            specialC = new Vector2(0.16f * h, 0.17f * h);
            specialR = 0.10f * h;
        }
        else
        {
            // Solo rounds: no stick, so FIRE sits where the thumb rests and BOMB right above it
            fireC = new Vector2(0.26f * h, 0.24f * h);
            fireR = 0.14f * h;
            specialC = new Vector2(0.26f * h, 0.60f * h);
            specialR = 0.10f * h;
        }
    }

    void GatherPointers()
    {
        pointers.Clear();

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (var t in touchscreen.touches)
            {
                if (t.press.isPressed)
                    pointers.Add(new Pointer { id = t.touchId.ReadValue(), pos = t.position.ReadValue() });
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
            pointers.Add(new Pointer { id = -1, pos = mouse.position.ReadValue() });
    }

    bool TryGetPointer(int id, out Vector2 pos)
    {
        foreach (var p in pointers)
        {
            if (p.id == id) { pos = p.pos; return true; }
        }
        pos = Vector2.zero;
        return false;
    }

    void UpdateTilt(float dt, Keyboard kb)
    {
        wheelId = None;
        wheelAngle = 0f;

        if (tilt.HasSensor)
        {
            Steer = tilt.Read(dt);
            return;
        }

        // Unity editor / PC: A-D or the arrow keys stand in for tilting the phone
        float key = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) key -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) key += 1f;
        }
        Steer = Mathf.MoveTowards(Steer, key, 4f * dt);
    }

    void UpdateWheel(float dt, Keyboard kb)
    {
        if (steerMode == SteerMode.Tilt)
        {
            UpdateTilt(dt, kb);
            return;
        }

        if (wheelId != None)
        {
            if (TryGetPointer(wheelId, out var pos))
            {
                Vector2 d = pos - wheelC;
                if (d.magnitude > wheelR * 0.1f)
                {
                    // Clockwise angle of the finger around the wheel centre
                    float phi = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    wheelAngle += Mathf.DeltaAngle(lastPhi, phi);
                    lastPhi = phi;
                }
            }
            else
            {
                wheelId = None;
            }
        }
        else
        {
            float key = 0f;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) key -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) key += 1f;
            }
            float target = key * maxLockDegrees;
            wheelAngle = Mathf.MoveTowards(wheelAngle, target, wheelReturnSpeed * dt);

            foreach (var p in pointers)
            {
                if (p.id == stickId) continue;
                Vector2 d = p.pos - wheelC;
                float dist = d.magnitude;
                if (dist > wheelR * 0.25f && dist < wheelR * 1.4f)
                {
                    wheelId = p.id;
                    lastPhi = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    break;
                }
            }
        }

        wheelAngle = Mathf.Clamp(wheelAngle, -maxLockDegrees, maxLockDegrees);
        Steer = wheelAngle / maxLockDegrees;
    }

    void UpdateStick(Keyboard kb)
    {
        if (steerMode == SteerMode.Tilt)
        {
            // No stick in the solo rounds: the phone pitch picks front or back
            stickId = None;
            stickVec = Vector2.zero;

            bool back = tilt.HasSensor ? tilt.AimBack : (kb != null && kb.kKey.isPressed);
            AimX = 0f;
            AimY = back ? -1f : 1f;     // +1 = straight ahead, -1 = straight behind
            return;
        }

        if (stickId != None)
        {
            if (TryGetPointer(stickId, out var pos))
            {
                Vector2 v = (pos - stickC) / stickR;
                if (v.magnitude > 1f) v.Normalize();
                stickVec = v;
            }
            else
            {
                stickId = None;
                stickVec = Vector2.zero;
            }
        }
        else
        {
            stickVec = Vector2.zero;

            foreach (var p in pointers)
            {
                if (p.id == wheelId) continue;
                if ((p.pos - stickC).magnitude < stickR * 1.5f)
                {
                    stickId = p.id;
                    break;
                }
            }

            if (kb != null)
            {
                var a = Vector2.zero;
                if (kb.lKey.isPressed) a.x += 1f;
                if (kb.jKey.isPressed) a.x -= 1f;
                if (kb.iKey.isPressed) a.y += 1f;
                if (kb.kKey.isPressed) a.y -= 1f;
                stickVec = Vector2.ClampMagnitude(a, 1f);
            }
        }

        AimX = stickVec.x;
        AimY = stickVec.y;
    }

    void UpdateButtons(float dt, Keyboard kb)
    {
        if (voiceMode)
        {
            UpdateVoice(dt, kb);
            return;
        }

        bool gas = false, brake = false, fire = false, special = false;

        foreach (var p in pointers)
        {
            // A finger that is steering or aiming never presses a pedal by accident
            if (p.id == wheelId || p.id == stickId) continue;

            if (gasRect.Contains(p.pos)) gas = true;
            if (brakeRect.Contains(p.pos)) brake = true;
            if ((p.pos - fireC).magnitude < fireR * 1.25f) fire = true;
            if ((p.pos - specialC).magnitude < specialR * 1.3f) special = true;
        }

        if (kb != null)
        {
            gas |= kb.wKey.isPressed || kb.upArrowKey.isPressed;
            brake |= kb.sKey.isPressed || kb.downArrowKey.isPressed;
            fire |= kb.spaceKey.isPressed;
            special |= kb.eKey.isPressed;
        }

        Accelerate = gas;
        Brake = brake;
        Fire = fire;
        Special = special;
    }

    // Voice rounds: the gas is automatic, a spoken "brake" lifts it and brakes for a moment.
    // The pedals, FIRE and BOMB buttons do not exist: touches are ignored.
    void UpdateVoice(float dt, Keyboard kb)
    {
        if (kb != null)
        {
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) OnVoiceCommand(VoiceInput.Command.Brake);
            if (kb.spaceKey.wasPressedThisFrame) OnVoiceCommand(VoiceInput.Command.Shoot);
            if (kb.eKey.wasPressedThisFrame) OnVoiceCommand(VoiceInput.Command.Bomb);
        }

        if (brakeTimer > 0f) brakeTimer = Mathf.Max(0f, brakeTimer - dt);

        Brake = brakeTimer > 0f;
        Accelerate = !Brake;      // auto gas: it comes back by itself when the braking is over
        Fire = false;             // shots come from ConsumeShots()
        Special = false;          // bombs come from ConsumeBombs()
    }

    // ---------------------------------------------------------------- drawing

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        EnsureTextures();
        ComputeLayout();

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
        labelStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.032f);

        // Steering wheel (turns with the finger). Not drawn in the solo rounds: the phone is the wheel.
        if (steerMode == SteerMode.Wheel) DrawCircle(wheelTex, wheelC, wheelR, wheelAngle);

        // Pedals under the wheel (not in the voice rounds)
        if (!voiceMode)
        {
            DrawPedal(brakeRect, Brake, "BRAKE");
            DrawPedal(gasRect, Accelerate, "GAS");
        }

        // Analog stick: base + knob that follows the finger (round 1 only)
        if (steerMode == SteerMode.Wheel)
        {
            DrawCircle(stickBaseTex, stickC, stickR, 0f);
            DrawCircle(knobTex, stickC + stickVec * stickR * 0.75f, stickR * 0.5f, 0f);
        }

        // Fire button above the stick (not in the voice rounds)
        if (!voiceMode)
        {
            DrawCircle(fireTex, fireC, Fire ? fireR * 0.92f : fireR, 0f);
            var fireGui = new Rect(fireC.x - fireR, Screen.height - fireC.y - fireR, fireR * 2f, fireR * 2f);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(fireGui, "FIRE", labelStyle);
        }

        // Big bomb button (orange when you have a charge, grey when empty)
        DrawSpecial();

        if (voiceMode) DrawVoiceHud();

        GUI.color = Color.white;
    }

    // Voice rounds: the three words to say (they light up when heard), the brake meter and the microphone state
    void DrawVoiceHud()
    {
        float W = Screen.width, H = Screen.height;
        var white = Texture2D.whiteTexture;

        // The three words
        string[] names = { "BRAKE", "SHOOT", "BOMB" };
        float cw = 0.20f * H, ch = 0.085f * H, gap = 0.02f * H;
        float x0 = (W - (cw * 3f + gap * 2f)) * 0.5f;
        float y = H - ch - 0.045f * H;
        for (int i = 0; i < 3; i++)
        {
            float lit = Mathf.Clamp01(1f - (Time.unscaledTime - heardAt[i]) / 0.35f);
            bool dim = i == 2 && SpecialCharges <= 0;
            var r = new Rect(x0 + i * (cw + gap), y, cw, ch);

            GUI.color = Color.Lerp(new Color(0.08f, 0.08f, 0.10f, 0.80f), new Color(1f, 0.75f, 0.15f, 0.95f), lit);
            GUI.DrawTexture(r, white);
            labelStyle.normal.textColor = Color.Lerp(new Color(1f, 1f, 1f, dim ? 0.35f : 0.85f), Color.black, lit);
            GUI.Label(r, names[i], labelStyle);
        }

        // Microphone state
        string mic;
        Color micColor = Color.white;
        if (voice == null)
        {
            mic = "No voice engine (keyboard: S / Space / E)";
            micColor = new Color(1f, 0.6f, 0.3f);
        }
        else
        {
            switch (voice.CurrentStatus)
            {
                case VoiceInput.Status.Listening: mic = "MIC ON  -  say it!"; micColor = new Color(0.5f, 1f, 0.55f); break;
                case VoiceInput.Status.Starting: mic = "MIC starting..."; micColor = new Color(1f, 0.9f, 0.4f); break;
                case VoiceInput.Status.NoPermission: mic = "MIC blocked: allow the microphone"; micColor = new Color(1f, 0.45f, 0.4f); break;
                case VoiceInput.Status.Unavailable: mic = "MIC unavailable (keyboard: S / Space / E)"; micColor = new Color(1f, 0.6f, 0.3f); break;
                default: mic = "MIC off"; micColor = new Color(1f, 1f, 1f, 0.6f); break;
            }
        }
        labelStyle.normal.textColor = micColor;
        GUI.Label(new Rect(0f, y - 0.065f * H, W, 0.05f * H), mic, labelStyle);

        // Loudness meter under the text
        if (voice != null && voice.CurrentStatus == VoiceInput.Status.Listening)
        {
            float mw = cw * 3f + gap * 2f;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x0, y - 0.012f * H, mw, 0.008f * H), white);
            GUI.color = new Color(0.5f, 1f, 0.55f, 0.9f);
            GUI.DrawTexture(new Rect(x0, y - 0.012f * H, mw * voice.Level, 0.008f * H), white);
        }

        // Brake meter on the right: fills with every "brake", then empties as the car brakes
        var m = new Rect(W - 0.17f * H, H * 0.48f, 0.06f * H, 0.40f * H);
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(m, white);
        float fill = BrakeLevel;
        float pad = 3f;
        float fh = (m.height - pad * 2f) * fill;
        GUI.color = Color.Lerp(new Color(1f, 0.65f, 0.1f), new Color(1f, 0.15f, 0.1f), fill);
        GUI.DrawTexture(new Rect(m.x + pad, m.yMax - pad - fh, m.width - pad * 2f, fh), white);
        labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(m.x - m.width, m.y - 0.06f * H, m.width * 3f, 0.05f * H), "BRAKE", labelStyle);

        GUI.color = Color.white;
    }

    void DrawCircle(Texture2D tex, Vector2 centerUp, float radius, float angle)
    {
        var c = new Vector2(centerUp.x, Screen.height - centerUp.y);
        var rect = new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f);

        var old = GUI.matrix;
        if (angle != 0f) GUIUtility.RotateAroundPivot(angle, c);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, tex);
        GUI.matrix = old;
    }

    void DrawPedal(Rect r, bool pressed, string label)
    {
        var g = new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
        var white = Texture2D.whiteTexture;

        // Metal plate
        GUI.color = new Color(0.08f, 0.08f, 0.09f, 0.95f);
        GUI.DrawTexture(g, white);

        // Rubber pad (sinks a little when pressed)
        float inset = pressed ? g.width * 0.04f : 0f;
        var pad = new Rect(g.x + g.width * 0.08f + inset, g.y + g.height * 0.06f + inset,
                           g.width * 0.84f - inset * 2f, g.height * 0.88f - inset * 2f);
        GUI.color = pressed ? new Color(0.38f, 0.38f, 0.42f) : new Color(0.22f, 0.22f, 0.25f);
        GUI.DrawTexture(pad, white);

        // Grip ribs
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        int ribs = 9;
        float ribH = Mathf.Max(2f, g.height * 0.018f);
        for (int i = 1; i < ribs; i++)
        {
            float y = pad.y + pad.height * (i / (float)ribs);
            GUI.DrawTexture(new Rect(pad.x + pad.width * 0.08f, y, pad.width * 0.84f, ribH), white);
        }

        labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(g.x, g.y + g.height * 0.02f, g.width, g.height * 0.2f), label, labelStyle);
    }

    // ------------------------------------------------- generated textures

    void EnsureTextures()
    {
        if (wheelTex == null) wheelTex = MakeTex(256, WheelPixel);
        if (stickBaseTex == null) stickBaseTex = MakeTex(128, StickBasePixel);
        if (knobTex == null) knobTex = MakeTex(128, KnobPixel);
        if (fireTex == null) fireTex = MakeTex(128, FirePixel);
        if (specialTex == null) specialTex = MakeTex(128, SpecialPixel);
    }

    static Texture2D MakeTex(int size, System.Func<float, float, Color> pixel)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = pixel(nx, ny);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // 1 inside the edge, 0 outside, with a soft anti-aliased border
    static float Cov(float d, float edge)
    {
        return Mathf.Clamp01((edge - d) / 0.02f + 0.5f);
    }

    static Color WheelPixel(float nx, float ny)
    {
        float r = Mathf.Sqrt(nx * nx + ny * ny);

        float ring = Mathf.Clamp01(Cov(r, 1f) - Cov(r, 0.78f));
        float spokeH = Cov(Mathf.Abs(ny), 0.075f) * Cov(r, 0.85f);                       // left + right spokes
        float spokeV = ny < 0f ? Cov(Mathf.Abs(nx), 0.075f) * Cov(r, 0.85f) : 0f;       // bottom spoke
        float hub = Cov(r, 0.24f);

        float a = Mathf.Clamp01(ring + spokeH + spokeV + hub);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

        float shade = 0.14f + 0.10f * Mathf.Clamp01((-nx + ny) * 0.5f + 0.5f);
        var c = new Color(shade, shade, shade * 1.05f, a);

        if (hub > 0.5f) c = new Color(0.30f, 0.30f, 0.33f, a);

        // Orange marker at the top of the rim (shows how far the wheel is turned)
        if (ring > 0.5f && Mathf.Abs(nx) < 0.05f && ny > 0f) c = new Color(1f, 0.55f, 0.1f, a);

        return c;
    }

    static Color StickBasePixel(float nx, float ny)
    {
        float r = Mathf.Sqrt(nx * nx + ny * ny);
        float a = Cov(r, 1f);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

        float rim = Cov(r, 1f) - Cov(r, 0.9f);
        float v = rim > 0.5f ? 0.45f : 0.10f + 0.05f * (1f - r);
        return new Color(v, v, v * 1.05f, a * 0.9f);
    }

    static Color KnobPixel(float nx, float ny)
    {
        float r = Mathf.Sqrt(nx * nx + ny * ny);
        float a = Cov(r, 1f);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

        float dx = nx + 0.35f, dy = ny - 0.35f;
        float light = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 1.3f);
        float v = 0.15f + 0.35f * light;
        if (r > 0.92f) v *= 0.5f;
        return new Color(v, v, v * 1.05f, a);
    }

    void DrawSpecial()
    {
        bool ready = SpecialCharges > 0;
        float r = (Special && ready) ? specialR * 0.92f : specialR;
        var c = new Vector2(specialC.x, Screen.height - specialC.y);
        var rect = new Rect(c.x - r, c.y - r, r * 2f, r * 2f);

        GUI.color = ready ? new Color(1f, 0.62f, 0.1f) : new Color(0.35f, 0.35f, 0.38f, 0.85f);
        GUI.DrawTexture(rect, specialTex);

        labelStyle.normal.textColor = ready ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        GUI.Label(rect, "BOMB\nx" + SpecialCharges, labelStyle);
        GUI.color = Color.white;
    }

    // Grey shaded ball: the button colour is applied as a tint when drawing
    static Color SpecialPixel(float nx, float ny)
    {
        float r = Mathf.Sqrt(nx * nx + ny * ny);
        float a = Cov(r, 1f);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

        float dx = nx + 0.35f, dy = ny - 0.35f;
        float light = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 1.2f);
        float v = 0.55f + 0.45f * light;
        if (r > 0.9f) v *= 0.55f;
        return new Color(v, v, v, a);
    }

    static Color FirePixel(float nx, float ny)
    {
        float r = Mathf.Sqrt(nx * nx + ny * ny);
        float a = Cov(r, 1f);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

        float dx = nx + 0.35f, dy = ny - 0.35f;
        float light = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 1.2f);
        var c = new Color(0.78f + 0.2f * light, 0.06f + 0.3f * light, 0.06f + 0.3f * light, a);
        if (r > 0.9f) c = new Color(0.45f, 0.03f, 0.03f, a);
        return c;
    }
}
}
