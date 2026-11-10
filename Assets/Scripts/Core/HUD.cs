using UnityEngine;

// On-screen display.
//   While a round is played: speed + round label (top-left), health bar (top-centre),
//   score and kills (top-right, left of the round pause button), pop-up messages, red hit flash.
//   Full-screen screens: round intro, pause, round over and match results.
// Everything is drawn from code; menu taps are read through ScreenTap (no GUI.Button needed).
public class HUD : MonoBehaviour
{
    public CarController car;
    public PlayerHealth health;
    public GameManager game;

    GUIStyle style;
    Texture2D circleTex;
    Texture2D bloodTex;
    float bloodAlpha;       // 0..1, fades in/out when health crosses the threshold
    float flash;
    string toast = "";
    float toastTimer;

    static readonly Color Gold = new Color(1f, 0.8f, 0.2f);
    static readonly Color Soft = new Color(1f, 1f, 1f, 0.8f);

    // Blood on the screen sides when health is at or below this value
    const float LowHealth = 30f;

    void Start()
    {
        if (health != null) health.Damaged += OnDamaged;
        if (game != null) game.Message += OnMessage;
    }

    void OnDamaged(float amount)
    {
        flash = 0.35f;
    }

    void OnMessage(string text)
    {
        toast = text;
        toastTimer = 1.3f;
    }

    void Update()
    {
        // Scaled time: the flash and the pop-up freeze while paused
        if (flash > 0f) flash -= Time.deltaTime;
        if (toastTimer > 0f) toastTimer -= Time.deltaTime;

        // Blood fades in below the threshold and fades out after a heal / a new round
        if (health != null)
        {
            float target = health.Current <= LowHealth ? 1f : 0f;
            bloodAlpha = Mathf.MoveTowards(bloodAlpha, target, Time.unscaledDeltaTime * 2.5f);
        }

        HandleTaps();
    }

    // ------------------------------------------------------------------ buttons

    // Round pause button, top-right corner (GUI coordinates, origin top-left)
    Rect PauseRect()
    {
        float H = Screen.height, W = Screen.width;
        float d = H * 0.09f, m = H * 0.025f;
        return new Rect(W - m - d, m, d, d);
    }

    Rect ResumeRect()
    {
        float H = Screen.height, W = Screen.width;
        float bw = H * 0.7f, bh = H * 0.15f;
        return new Rect(W * 0.5f - bw * 0.5f, H * 0.38f, bw, bh);
    }

    Rect RestartRect()
    {
        float H = Screen.height, W = Screen.width;
        float bw = H * 0.7f, bh = H * 0.15f;
        return new Rect(W * 0.5f - bw * 0.5f, H * 0.60f, bw, bh);
    }

    static Rect Grow(Rect r, float k)
    {
        float dw = r.width * (k - 1f), dh = r.height * (k - 1f);
        return new Rect(r.x - dw * 0.5f, r.y - dh * 0.5f, r.width + dw, r.height + dh);
    }

    void HandleTaps()
    {
        if (game == null) return;

        Vector2 tap;
        if (!ScreenTap.TryGet(out tap)) return;

        if (game.Current == GameManager.State.Playing)
        {
            // The small delay stops the tap that started the round from also pausing it
            if (game.StateAge > 0.3f && Grow(PauseRect(), 1.35f).Contains(tap)) game.Pause();
        }
        else if (game.Current == GameManager.State.Paused)
        {
            if (game.StateAge < 0.2f) return;
            if (ResumeRect().Contains(tap)) game.Resume();
            else if (RestartRect().Contains(tap)) game.RestartMatch();
        }
    }

    // ------------------------------------------------------------------ drawing

    void OnGUI()
    {
        if (car == null || health == null || game == null) return;

        GUI.depth = -10;   // in front of the controls, the zone banner and everything else

        float W = Screen.width, H = Screen.height;
        var white = Texture2D.whiteTexture;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            style.wordWrap = false;
        }

        var state = game.Current;
        bool inRound = state == GameManager.State.Playing
                    || state == GameManager.State.Paused
                    || state == GameManager.State.RoundOver;

        // Red flash when hit
        if (flash > 0f && inRound)
        {
            GUI.color = new Color(1f, 0f, 0f, Mathf.Clamp01(flash / 0.35f) * 0.35f);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), white);
        }

        // Blood on the left and right edges at low health
        if (inRound && bloodAlpha > 0.01f) DrawBlood(W, H);

        if (inRound) DrawTopHud(W, H, white);

        // Pause button: only while a round is being played
        if (state == GameManager.State.Playing) DrawPauseButton(white);

        // Pop-up message ("+30 HP", "BIG BOMB!")
        if (toastTimer > 0f && state == GameManager.State.Playing)
        {
            Text(new Rect(0f, H * 0.17f, W, H * 0.12f), toast, 0.07f, TextAnchor.MiddleCenter,
                 new Color(1f, 0.9f, 0.3f, Mathf.Clamp01(toastTimer / 0.5f)));
        }

        switch (state)
        {
            case GameManager.State.Intro: DrawIntro(W, H, white); break;
            case GameManager.State.Paused: DrawPause(W, H, white); break;
            case GameManager.State.RoundOver: DrawRoundOver(W, H, white); break;
            case GameManager.State.Results: DrawResults(W, H, white); break;
        }

        GUI.color = Color.white;
    }

    void DrawTopHud(float W, float H, Texture2D white)
    {
        // Speed + round label (left)
        Text(new Rect(W * 0.02f, H * 0.02f, W * 0.3f, H * 0.1f),
             Mathf.RoundToInt(car.Speed * 3.6f) + " km/h", 0.05f, TextAnchor.UpperLeft, Color.white);
        Text(new Rect(W * 0.02f, H * 0.088f, W * 0.3f, H * 0.06f),
             "Round " + game.RoundNumber + "/" + GameManager.RoundCount + "  ·  " + game.RoundLabel,
             0.03f, TextAnchor.UpperLeft, Soft);

        // Score + kills (right), shifted left to leave room for the pause button
        float right = PauseRect().x - H * 0.025f;
        Text(new Rect(right - W * 0.3f, H * 0.02f, W * 0.3f, H * 0.1f),
             "Score " + game.Score, 0.05f, TextAnchor.UpperRight, Color.white);
        Text(new Rect(right - W * 0.3f, H * 0.075f, W * 0.3f, H * 0.08f),
             "Kills " + game.Kills, 0.035f, TextAnchor.UpperRight, Color.white);

        // Health bar (top centre)
        float bw = W * 0.26f, bh = H * 0.05f;
        var bar = new Rect(W * 0.5f - bw * 0.5f, H * 0.03f, bw, bh);
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(bar, white);

        float f = health.Fraction;
        GUI.color = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(0.2f, 0.85f, 0.3f), f);
        GUI.DrawTexture(new Rect(bar.x + 3f, bar.y + 3f, (bar.width - 6f) * f, bar.height - 6f), white);

        Text(bar, Mathf.CeilToInt(health.Current) + " HP", 0.035f, TextAnchor.MiddleCenter, Color.white);
    }

    void DrawBlood(float W, float H)
    {
        EnsureBlood();

        // 0 at LowHealth -> 1 at 0 HP: more blood, wider, faster heartbeat as health drops
        float severity = Mathf.Clamp01(1f - health.Current / LowHealth);
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(4f, 9f, severity));

        float a = bloodAlpha * Mathf.Lerp(0.6f, 1f, severity) * (0.8f + 0.2f * beat);
        float sideW = W * Mathf.Lerp(0.16f, 0.26f, severity) * (1f + 0.05f * beat);

        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.DrawTexture(new Rect(0f, 0f, sideW, H), bloodTex);
        // Right side = the same texture mirrored
        GUI.DrawTextureWithTexCoords(new Rect(W - sideW, 0f, sideW, H), bloodTex, new Rect(1f, 0f, -1f, 1f));
        GUI.color = Color.white;
    }

    // Procedural blood texture for the LEFT edge: ragged border + a few drips, darkest at the screen edge
    void EnsureBlood()
    {
        if (bloodTex != null) return;

        const int w = 128, h = 256;
        bloodTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        bloodTex.wrapMode = TextureWrapMode.Clamp;
        bloodTex.filterMode = FilterMode.Bilinear;

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float slow = Mathf.PerlinNoise(y * 0.035f, 3.7f);                 // big waves
            float fine = Mathf.PerlinNoise(y * 0.16f, 9.1f);                  // small ragged bits
            float drip = Mathf.Max(0f, Mathf.PerlinNoise(y * 0.11f, 21.3f) - 0.62f) * 1.6f;  // long drips
            float depth = Mathf.Clamp01(0.32f + 0.22f * slow + 0.08f * fine + drip);

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;                                     // 0 = screen edge
                float t = Mathf.Clamp01(1f - u / depth);
                float alpha = t * t * (3f - 2f * t);                          // smooth falloff
                float shade = Mathf.Lerp(0.85f, 0.45f, t);                   // darker red near the edge
                px[y * w + x] = new Color32((byte)(shade * 200f), (byte)(shade * 6f), (byte)(shade * 8f),
                                            (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        bloodTex.SetPixels32(px);
        bloodTex.Apply();
    }

    void DrawPauseButton(Texture2D white)
    {
        EnsureCircle();
        Rect r = PauseRect();

        // Ring + dark disc
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(r, circleTex);
        float inset = r.width * 0.07f;
        GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.88f);
        GUI.DrawTexture(new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f), circleTex);

        // Two bars
        GUI.color = Color.white;
        float bw = r.width * 0.13f, bh = r.height * 0.38f, gap = r.width * 0.13f;
        float cx = r.center.x, cy = r.center.y;
        GUI.DrawTexture(new Rect(cx - gap * 0.5f - bw, cy - bh * 0.5f, bw, bh), white);
        GUI.DrawTexture(new Rect(cx + gap * 0.5f, cy - bh * 0.5f, bw, bh), white);
        GUI.color = Color.white;
    }

    // ------------------------------------------------------------------ screens

    void DrawIntro(float W, float H, Texture2D white)
    {
        Dim(W, H, white, 0.72f);

        Text(new Rect(0f, H * 0.07f, W, H * 0.16f),
             "ROUND " + game.RoundNumber + " / " + GameManager.RoundCount, 0.12f, TextAnchor.MiddleCenter, Color.white);

        if (game.IsDuoRound)
        {
            Text(new Rect(0f, H * 0.29f, W, H * 0.1f), "PLAYER 1 DRIVES", 0.07f, TextAnchor.MiddleCenter, Gold);
            Text(new Rect(0f, H * 0.38f, W, H * 0.08f), "Wheel and pedals on the right", 0.042f, TextAnchor.MiddleCenter, Soft);
            Text(new Rect(0f, H * 0.50f, W, H * 0.1f), "PLAYER 2 SHOOTS", 0.07f, TextAnchor.MiddleCenter, Gold);
            Text(new Rect(0f, H * 0.59f, W, H * 0.08f), "Stick, FIRE and BOMB on the left", 0.042f, TextAnchor.MiddleCenter, Soft);
        }
        else if (game.IsVoiceRound)
        {
            Text(new Rect(0f, H * 0.24f, W, H * 0.11f), "PLAYER " + game.SoloPlayer + " PLAYS ALONE  -  VOICE ROUND", 0.07f, TextAnchor.MiddleCenter, Gold);
            Text(new Rect(0f, H * 0.35f, W, H * 0.07f), "No buttons: the gas is automatic. Tilt to steer and aim.", 0.04f, TextAnchor.MiddleCenter, Color.white);
            Text(new Rect(0f, H * 0.44f, W, H * 0.07f), "Say  BRAKE  -  once = a little, many times = a lot", 0.04f, TextAnchor.MiddleCenter, Color.white);
            Text(new Rect(0f, H * 0.52f, W, H * 0.07f), "Say  SHOOT  -  one word = one shot", 0.04f, TextAnchor.MiddleCenter, Color.white);
            Text(new Rect(0f, H * 0.60f, W, H * 0.07f), "Say  BOMB  -  uses a bomb potion", 0.04f, TextAnchor.MiddleCenter, Color.white);
            Text(new Rect(0f, H * 0.68f, W, H * 0.07f), "Allow the microphone if asked  ·  hold the phone level, then tap", 0.034f, TextAnchor.MiddleCenter, Soft);
        }
        else
        {
            Text(new Rect(0f, H * 0.29f, W, H * 0.11f), "PLAYER " + game.SoloPlayer + " PLAYS ALONE", 0.08f, TextAnchor.MiddleCenter, Gold);
            Text(new Rect(0f, H * 0.40f, W, H * 0.08f), "Tilt left / right to STEER", 0.05f, TextAnchor.MiddleCenter, Color.white);
            Text(new Rect(0f, H * 0.48f, W, H * 0.08f), "Top edge away from you = aim BEHIND  ·  back to level = aim AHEAD", 0.038f, TextAnchor.MiddleCenter, Color.white);
            Text(new Rect(0f, H * 0.57f, W, H * 0.07f), "Gas and brake on the right  ·  FIRE and BOMB on the left", 0.036f, TextAnchor.MiddleCenter, Soft);
            Text(new Rect(0f, H * 0.64f, W, H * 0.07f), "Hold the phone level, then tap", 0.036f, TextAnchor.MiddleCenter, Soft);
        }

        Blink(new Rect(0f, H * 0.79f, W, H * 0.1f), "Tap to start", 0.055f);
    }

    void DrawPause(float W, float H, Texture2D white)
    {
        Dim(W, H, white, 0.72f);

        Text(new Rect(0f, H * 0.12f, W, H * 0.2f), "PAUSED", 0.12f, TextAnchor.MiddleCenter, Color.white);

        DrawButton(ResumeRect(), "Resume", new Color(0.15f, 0.6f, 0.25f), white);
        DrawButton(RestartRect(), "Restart match", new Color(0.75f, 0.17f, 0.15f), white);

        Text(new Rect(0f, H * 0.80f, W, H * 0.06f), "Restart match goes back to Round 1 with both scores at zero",
             0.03f, TextAnchor.MiddleCenter, Soft);
    }

    void DrawRoundOver(float W, float H, Texture2D white)
    {
        Dim(W, H, white, 0.65f);

        Text(new Rect(0f, H * 0.10f, W, H * 0.16f), "ROUND " + game.RoundNumber + " OVER", 0.11f, TextAnchor.MiddleCenter, Color.white);
        Text(new Rect(0f, H * 0.30f, W, H * 0.1f), "Round score: " + game.LastRoundScore, 0.065f, TextAnchor.MiddleCenter, Color.white);

        if (game.IsDuoRound)
        {
            Text(new Rect(0f, H * 0.43f, W, H * 0.08f), "Player 1 (driving): +" + game.LastRoundDriver, 0.048f, TextAnchor.MiddleCenter, Gold);
            Text(new Rect(0f, H * 0.51f, W, H * 0.08f), "Player 2 (shooting): +" + game.LastRoundGunner, 0.048f, TextAnchor.MiddleCenter, Gold);
        }

        int nextRound = game.Round + 1;
        Text(new Rect(0f, H * 0.65f, W, H * 0.07f),
             "Next: Round " + (nextRound + 1) + " of " + GameManager.RoundCount + "  ·  Player " + GameManager.SoloPlayerOf(nextRound)
             + " plays alone" + (GameManager.IsVoiceRoundIndex(nextRound) ? " (voice)" : ""),
             0.04f, TextAnchor.MiddleCenter, Soft);

        if (game.CanAdvance) Blink(new Rect(0f, H * 0.78f, W, H * 0.1f), "Tap to continue", 0.05f);
    }

    void DrawResults(float W, float H, Texture2D white)
    {
        Dim(W, H, white, 0.82f);

        Text(new Rect(0f, H * 0.04f, W, H * 0.12f), "MATCH RESULTS", 0.09f, TextAnchor.MiddleCenter, Color.white);

        // Both scores side by side
        float pw = W * 0.34f, ph = H * 0.40f, gap = W * 0.04f, y = H * 0.17f;
        var left = new Rect(W * 0.5f - gap * 0.5f - pw, y, pw, ph);
        var rightR = new Rect(W * 0.5f + gap * 0.5f, y, pw, ph);

        DrawPanel(left, "PLAYER 1", game.P1Total,
                  "Round 1 driving: " + game.Round1Driver, "Round 2 solo: " + game.Round2Score,
                  "Round 4 voice: " + game.Round4Score,
                  game.Winner == 1, white);
        DrawPanel(rightR, "PLAYER 2", game.P2Total,
                  "Round 1 shooting: " + game.Round1Gunner, "Round 3 solo: " + game.Round3Score,
                  "Round 5 voice: " + game.Round5Score,
                  game.Winner == 2, white);

        string verdict = game.Winner == 0 ? "Draw!" : "Player " + game.Winner + " wins!";
        Text(new Rect(0f, H * 0.60f, W, H * 0.12f), verdict, 0.09f, TextAnchor.MiddleCenter, Gold);

        Text(new Rect(0f, H * 0.73f, W, H * 0.06f),
             "Match total: " + game.MatchTotal + "   ·   Best: " + game.BestScore, 0.038f, TextAnchor.MiddleCenter, Soft);

        if (game.CanAdvance) Blink(new Rect(0f, H * 0.83f, W, H * 0.1f), "Tap to start a new match", 0.05f);
    }

    void DrawPanel(Rect r, string name, int score, string line1, string line2, string line3, bool winner, Texture2D white)
    {
        // Border (gold for the winner) + dark body
        GUI.color = winner ? Gold : new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(r, white);
        float b = Mathf.Max(2f, r.height * 0.025f);
        GUI.color = new Color(0.07f, 0.07f, 0.09f, 0.95f);
        GUI.DrawTexture(new Rect(r.x + b, r.y + b, r.width - b * 2f, r.height - b * 2f), white);

        Text(new Rect(r.x, r.y + r.height * 0.05f, r.width, r.height * 0.2f), name, 0.055f,
             TextAnchor.MiddleCenter, winner ? Gold : Color.white);
        Text(new Rect(r.x, r.y + r.height * 0.21f, r.width, r.height * 0.33f), score.ToString(), 0.11f,
             TextAnchor.MiddleCenter, Color.white);
        Text(new Rect(r.x, r.y + r.height * 0.55f, r.width, r.height * 0.12f), line1, 0.03f, TextAnchor.MiddleCenter, Soft);
        Text(new Rect(r.x, r.y + r.height * 0.68f, r.width, r.height * 0.12f), line2, 0.03f, TextAnchor.MiddleCenter, Soft);
        Text(new Rect(r.x, r.y + r.height * 0.81f, r.width, r.height * 0.12f), line3, 0.03f, TextAnchor.MiddleCenter, Soft);
    }

    void DrawButton(Rect r, string label, Color fill, Texture2D white)
    {
        GUI.color = Color.white;
        GUI.DrawTexture(r, white);
        float b = Mathf.Max(2f, r.height * 0.06f);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(r.x + b, r.y + b, r.width - b * 2f, r.height - b * 2f), white);
        Text(r, label, 0.06f, TextAnchor.MiddleCenter, Color.white);
    }

    // ------------------------------------------------------------------ helpers

    void Dim(float W, float H, Texture2D white, float alpha)
    {
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, W, H), white);
        GUI.color = Color.white;
    }

    // Text size is a fraction of the screen height, so it scales on every phone
    void Text(Rect r, string s, float sizeOfHeight, TextAnchor anchor, Color color)
    {
        style.fontSize = Mathf.RoundToInt(Screen.height * sizeOfHeight);
        style.alignment = anchor;
        style.normal.textColor = color;
        GUI.color = Color.white;
        GUI.Label(r, s, style);
    }

    // "Tap to ..." prompt that gently pulses (real time, so it also works while paused)
    void Blink(Rect r, string s, float sizeOfHeight)
    {
        float a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
        Text(r, s, sizeOfHeight, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, a));
    }

    void EnsureCircle()
    {
        if (circleTex != null) return;

        const int size = 96;
        circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        circleTex.wrapMode = TextureWrapMode.Clamp;
        circleTex.filterMode = FilterMode.Bilinear;

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp01((1f - r) / 0.03f + 0.5f);   // soft anti-aliased edge
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        circleTex.SetPixels32(px);
        circleTex.Apply();
    }
}
