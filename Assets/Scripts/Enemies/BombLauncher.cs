using System.Collections.Generic;
using UnityEngine;

// Bombs thrown by the enemy cars. A bomb flies in an arc toward a spot on the road near the player.
// A red disc on the road grows while the bomb is in the air: that is where it will explode.
// Drive out of the disc, or shoot the bomb down in the air (bonus points).
// Everything is checked in code, no physics setup needed.
public class BombLauncher : MonoBehaviour
{
    public CarController player;
    public PlayerHealth health;
    public GameManager game;

    [Header("Bombs")]
    public float flightTime = 1.3f;     // seconds from the enemy to the road
    public float gravity = 20f;
    public float blastRadius = 3.5f;    // metres around the landing spot
    public float damage = 15f;
    public int maxBombs = 4;            // in the air at the same time
    public float shootRadius = 2f;      // how close a bullet must pass to pop a bomb (flat distance on the road)
    public int shootBonus = 10;         // points for popping a bomb

    class Bomb
    {
        public Transform t;
        public Transform ring;
        public Vector3 start;
        public Vector3 target;
        public Vector3 vel;
        public float age;
    }

    class Blast
    {
        public Transform t;
        public float age;
        public float size;
    }

    const float BlastDuration = 0.35f;

    readonly List<Bomb> bombs = new List<Bomb>();
    readonly List<Blast> blasts = new List<Blast>();

    static readonly Color BombColor = new Color(0.08f, 0.08f, 0.08f);
    static readonly Color FuseColor = new Color(1f, 0.15f, 0.1f);
    static readonly Color WarnColor = new Color(0.95f, 0.1f, 0.1f);
    static readonly Color BlastColor = new Color(1f, 0.6f, 0.1f);

    // Throws a bomb from "from" so that it lands on "target" (a point on the road).
    public bool Launch(Vector3 from, Vector3 target)
    {
        if (bombs.Count >= maxBombs) return false;

        var root = new GameObject("Bomb").transform;
        root.SetParent(transform, false);
        Prim.Make(PrimitiveType.Sphere, "Body", root, Vector3.zero, Vector3.one * 0.7f, BombColor);
        Prim.Make(PrimitiveType.Sphere, "Fuse", root, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.25f, FuseColor);
        root.position = from;

        var ring = Prim.Make(PrimitiveType.Cylinder, "Warning", transform,
                             new Vector3(target.x, 0.05f, target.z),
                             new Vector3(1f, 0.01f, 1f), WarnColor).transform;

        float T = flightTime;
        var vel = new Vector3((target.x - from.x) / T,
                              (target.y - from.y + 0.5f * gravity * T * T) / T,
                              (target.z - from.z) / T);

        bombs.Add(new Bomb { t = root, ring = ring, start = from, target = target, vel = vel });
        return true;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var b = bombs[i];
            b.age += dt;
            float a = b.age;

            b.t.position = b.start + new Vector3(b.vel.x * a, b.vel.y * a - 0.5f * gravity * a * a, b.vel.z * a);

            // Warning disc grows until the bomb lands
            float k = Mathf.Clamp01(a / flightTime);
            float d = blastRadius * 2f * Mathf.Lerp(0.3f, 1f, k);
            b.ring.localScale = new Vector3(d, 0.01f, d);

            if (a >= flightTime)
            {
                Detonate(b.target, true);
                Remove(i);
            }
        }

        for (int i = blasts.Count - 1; i >= 0; i--)
        {
            var bl = blasts[i];
            bl.age += dt;
            float k = bl.age / BlastDuration;

            if (k >= 1f)
            {
                Destroy(bl.t.gameObject);
                blasts.RemoveAt(i);
                continue;
            }
            bl.t.localScale = Vector3.one * bl.size * Mathf.Sin(k * Mathf.PI);
        }
    }

    void Detonate(Vector3 pos, bool harmful)
    {
        var t = Prim.Make(PrimitiveType.Sphere, "Blast", transform, pos + Vector3.up * 0.5f,
                          Vector3.zero, BlastColor).transform;
        blasts.Add(new Blast { t = t, size = harmful ? blastRadius * 2f : 2f });

        if (!harmful || player == null || health == null) return;

        var pp = player.transform.position;
        float dx = pp.x - pos.x, dz = pp.z - pos.z;
        if (dx * dx + dz * dz < blastRadius * blastRadius)
        {
            if (health.TakeDamage(damage)) player.Crash();
        }
    }

    // Called for every bullet step. Returns true if the bullet popped a bomb.
    public bool TryHit(Vector3 from, Vector3 to)
    {
        for (int s = 1; s <= 3; s++)
        {
            var p = Vector3.Lerp(from, to, s / 3f);

            for (int i = bombs.Count - 1; i >= 0; i--)
            {
                var bp = bombs[i].t.position;
                float dx = p.x - bp.x, dz = p.z - bp.z;
                if (dx * dx + dz * dz < shootRadius * shootRadius)
                {
                    Detonate(bp, false);
                    Remove(i);
                    if (game != null) game.AddBonus(shootBonus);
                    return true;
                }
            }
        }
        return false;
    }

    // Big bomb: pops every bomb in the air (harmless) and gives the shoot bonus for each.
    public void PopAll()
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            Detonate(bombs[i].t.position, false);
            Remove(i);
            if (game != null) game.AddBonus(shootBonus);
        }
    }

    void Remove(int index)
    {
        var b = bombs[index];
        if (b.t != null) Destroy(b.t.gameObject);
        if (b.ring != null) Destroy(b.ring.gameObject);
        bombs.RemoveAt(index);
    }

    public void Clear()
    {
        for (int i = bombs.Count - 1; i >= 0; i--) Remove(i);

        foreach (var bl in blasts)
        {
            if (bl.t != null) Destroy(bl.t.gameObject);
        }
        blasts.Clear();
    }
}
