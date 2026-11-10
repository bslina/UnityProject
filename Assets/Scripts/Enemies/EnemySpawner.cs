using System.Collections.Generic;
using UnityEngine;

// Enemy cars that chase the player from BEHIND and from the FRONT.
// Rear enemies catch up from behind the camera and stay just behind the car.
// Front enemies arrive from far ahead and stay in front of the car.
// They weave sideways around the player. Bullets from GunController call TryHit().
// Each enemy throws bombs at the player (see BombLauncher).
// Collisions are checked directly in code, no physics setup needed.
public class EnemySpawner : MonoBehaviour
{
    public CarController player;
    public GameManager game;
    public BombLauncher bombs;
    public bool spawning = true;

    [Header("Spawning")]
    public float firstSpawnDelay = 2f;
    public float minSpawnGap = 4f;       // seconds between two spawns
    public float maxSpawnGap = 7f;
    public float respawnAfterKill = 3f;  // minimum pause after a kill
    public int maxPerSide = 1;           // enemies behind AND enemies in front, each side
    public int maxPerSideLate = 2;       // allowed after lateDistance metres
    public float lateDistance = 2000f;

    [Header("Enemy")]
    public int health = 3;               // bullets needed to destroy one
    public float rearSpawnZ = -30f;      // starts behind the camera view
    public float frontSpawnZ = 120f;     // starts far ahead
    public float rearHoldZ = -7.5f;      // stays this far behind the car (visible on screen)
    public float frontHoldZ = 24f;       // stays this far ahead of the car
    public float rearApproach = 14f;     // catch-up speed (metres per second)
    public float frontApproach = 30f;
    public float lateralSpeed = 5f;

    [Header("Bombs")]
    public float firstBombDelay = 2.5f;  // seconds after an enemy appears
    public float bombMinGap = 3f;        // seconds between two bombs from the same enemy
    public float bombMaxGap = 5f;
    public float bombScatter = 1.5f;     // random offset around the player's spot (metres)

    [Header("Hit box (half sizes)")]
    public float halfWidth = 1.2f;
    public float halfLength = 2.3f;
    public float height = 1.9f;

    class Enemy
    {
        public Transform t;
        public int hp;
        public int side;        // -1 = behind the car, +1 = in front
        public float holdZ;
        public float approach;
        public float phase;
        public float flash;     // short "pop" when hit
        public float nextBomb;  // time of the next bomb throw
    }

    class Piece
    {
        public Transform t;
        public Vector3 v;
        public float life;
    }

    readonly List<Enemy> enemies = new List<Enemy>();
    readonly List<Piece> pieces = new List<Piece>();
    Camera cam;
    float nextSpawn;
    GUIStyle style;

    static readonly Color EnemyColor = new Color(0.10f, 0.10f, 0.12f);
    static readonly Color LightRed = new Color(1f, 0.1f, 0.1f);
    static readonly Color Fire = new Color(1f, 0.55f, 0.1f);

    void Start()
    {
        cam = Camera.main;
        nextSpawn = Time.time + firstSpawnDelay;
    }

    void Update()
    {
        if (player == null) return;

        float dt = Time.deltaTime;
        var pp = player.transform.position;

        // Move every enemy toward its place around the player
        foreach (var e in enemies)
        {
            float t = Time.time;
            float sway = Mathf.Sin(t * 0.9f + e.phase);
            float targetZ = pp.z + e.holdZ + sway * (e.side > 0 ? 3f : 1.2f);
            float targetX = Mathf.Clamp(pp.x + Mathf.Sin(t * 0.7f + e.phase * 1.7f) * 3f,
                                        -player.xLimit, player.xLimit);

            var p = e.t.position;
            p.z = Mathf.MoveTowards(p.z, targetZ, e.approach * dt);
            p.x = Mathf.MoveTowards(p.x, targetX, lateralSpeed * dt);
            e.t.position = p;

            // Throw a bomb at the spot where the player is now (it lands 1.3 s later)
            if (spawning && bombs != null && Time.time >= e.nextBomb && Mathf.Abs(p.z - targetZ) < 3f)
            {
                var aim = new Vector3(pp.x + Random.Range(-bombScatter, bombScatter), 0.1f,
                                      pp.z + Random.Range(-bombScatter, bombScatter));
                bool thrown = bombs.Launch(e.t.position + Vector3.up * 1.5f, aim);
                e.nextBomb = Time.time + (thrown ? Random.Range(bombMinGap, bombMaxGap) : 0.5f);
            }

            if (e.flash > 0f)
            {
                e.flash -= dt;
                e.t.localScale = Vector3.one * (1f + 0.12f * Mathf.Clamp01(e.flash / 0.1f));
            }
        }

        // Spawn new ones
        if (spawning && Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + (TrySpawn() ? Random.Range(minSpawnGap, maxSpawnGap) : 1f);
        }

        UpdatePieces(dt);
    }

    bool TrySpawn()
    {
        int cap = player.Distance > lateDistance ? maxPerSideLate : maxPerSide;
        int rear = 0, front = 0;
        foreach (var e in enemies)
        {
            if (e.side < 0) rear++; else front++;
        }

        var options = new List<int>();
        if (rear < cap) options.Add(-1);
        if (front < cap) options.Add(1);
        if (options.Count == 0) return false;

        Spawn(options[Random.Range(0, options.Count)]);
        return true;
    }

    void Spawn(int side)
    {
        var pp = player.transform.position;

        var t = CarBuilder.Build("Enemy Car", EnemyColor);
        Prim.Make(PrimitiveType.Cube, "LightBar", t,
                  new Vector3(0f, 1.38f, -0.3f), new Vector3(0.9f, 0.12f, 0.3f), LightRed);
        t.SetParent(transform, false);

        float x = Mathf.Clamp(pp.x + Random.Range(-3f, 3f), -player.xLimit, player.xLimit);
        float z = pp.z + (side < 0 ? rearSpawnZ : frontSpawnZ);
        t.position = new Vector3(x, 0f, z);

        enemies.Add(new Enemy
        {
            t = t,
            hp = health,
            side = side,
            holdZ = side < 0 ? rearHoldZ : frontHoldZ,
            approach = side < 0 ? rearApproach : frontApproach,
            phase = Random.Range(0f, 6.28f),
            nextBomb = Time.time + firstBombDelay + Random.Range(0f, 1f)
        });
    }

    // Called by GunController for every bullet step. Returns true if the bullet hit an enemy.
    public bool TryHit(Vector3 from, Vector3 to)
    {
        for (int s = 1; s <= 3; s++)
        {
            var p = Vector3.Lerp(from, to, s / 3f);

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                var ep = e.t.position;

                if (Mathf.Abs(p.x - ep.x) < halfWidth &&
                    Mathf.Abs(p.z - ep.z) < halfLength &&
                    p.y > 0f && p.y < height)
                {
                    e.hp--;
                    e.flash = 0.1f;
                    if (e.hp <= 0) Kill(i);
                    return true;
                }
            }
        }
        // No enemy hit: a bullet can still pop a bomb in the air
        return bombs != null && bombs.TryHit(from, to);
    }

    void Kill(int index)
    {
        var e = enemies[index];
        Explode(e.t.position);
        Destroy(e.t.gameObject);
        enemies.RemoveAt(index);

        if (game != null) game.AddKill();
        nextSpawn = Mathf.Max(nextSpawn, Time.time + respawnAfterKill);
    }

    void Explode(Vector3 pos)
    {
        for (int i = 0; i < 10; i++)
        {
            var t = Prim.Make(PrimitiveType.Cube, "Debris", transform,
                              pos + Vector3.up * 0.8f, Vector3.one * 0.4f,
                              i % 2 == 0 ? Fire : EnemyColor).transform;
            pieces.Add(new Piece
            {
                t = t,
                v = new Vector3(Random.Range(-6f, 6f), Random.Range(4f, 10f), Random.Range(-6f, 6f)),
                life = 0.9f
            });
        }
    }

    void UpdatePieces(float dt)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var p = pieces[i];
            p.v.y -= 25f * dt;
            p.t.position += p.v * dt;
            p.life -= dt;
            p.t.localScale = Vector3.one * 0.4f * Mathf.Clamp01(p.life / 0.9f);

            if (p.life <= 0f)
            {
                Destroy(p.t.gameObject);
                pieces.RemoveAt(i);
            }
        }
    }

    // Big bomb: blows up every enemy car and pops every bomb in the air.
    public void DestroyAll()
    {
        for (int i = enemies.Count - 1; i >= 0; i--) Kill(i);
        if (bombs != null) bombs.PopAll();
    }

    public void Clear()
    {
        foreach (var e in enemies)
        {
            if (e.t != null) Destroy(e.t.gameObject);
        }
        enemies.Clear();

        foreach (var p in pieces)
        {
            if (p.t != null) Destroy(p.t.gameObject);
        }
        pieces.Clear();

        if (bombs != null) bombs.Clear();

        nextSpawn = Time.time + firstSpawnDelay;
    }

    // Small health bar above each enemy
    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        var white = Texture2D.whiteTexture;
        float H = Screen.height;
        float w = H * 0.07f, h = H * 0.01f;

        foreach (var e in enemies)
        {
            Vector3 sp = cam.WorldToScreenPoint(e.t.position + Vector3.up * 2.4f);
            if (sp.z <= 0f) continue;

            var r = new Rect(sp.x - w * 0.5f, H - sp.y - h * 0.5f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), white);
            GUI.color = new Color(1f, 0.25f, 0.15f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(e.hp / (float)health), r.height), white);
        }
        GUI.color = Color.white;
    }
}
