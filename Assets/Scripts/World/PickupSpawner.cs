using System.Collections.Generic;
using UnityEngine;

// Potions lie on the road. Drive through them to collect:
//   GREEN  = heals you.
//   ORANGE = adds a big bomb charge (use it with the BOMB button).
// The game gives more green potions when you are hurt.
public class PickupSpawner : MonoBehaviour
{
    public CarController player;
    public PlayerHealth health;
    public RoadScroller road;
    public ObstacleSpawner traffic;
    public PlayerAbilities abilities;
    public GameManager game;

    public bool spawning = true;
    public float healAmount = 30f;
    public float spawnDistanceAhead = 170f;
    public float minGap = 160f;     // metres travelled between two potions
    public float maxGap = 260f;

    static readonly float[] LaneX = { -4f, 0f, 4f };
    static readonly Color Green = new Color(0.20f, 0.90f, 0.35f);
    static readonly Color Orange = new Color(1f, 0.55f, 0.10f);

    enum Kind { Heal, Bomb }

    class Pickup
    {
        public Transform t;
        public Transform visual;
        public Kind kind;
        public float phase;
    }

    readonly List<Pickup> pickups = new List<Pickup>();
    float distSinceSpawn;
    float nextGap = 120f;

    void Update()
    {
        if (player == null || health == null) return;

        float dt = Time.deltaTime;
        float roadSpeed = road != null ? road.Speed : 0f;
        Vector3 pp = player.transform.position;

        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            var k = pickups[i];
            var p = k.t.position;
            p.z -= roadSpeed * dt;
            k.t.position = p;

            // Spin and float
            k.visual.Rotate(0f, 140f * dt, 0f, Space.World);
            k.visual.localPosition = new Vector3(0f, 1.1f + Mathf.Sin(Time.time * 3f + k.phase) * 0.25f, 0f);

            if (p.z < -20f)
            {
                Destroy(k.t.gameObject);
                pickups.RemoveAt(i);
                continue;
            }

            if (Mathf.Abs(p.x - pp.x) < 1.7f && Mathf.Abs(p.z - pp.z) < 2.6f)
            {
                Collect(k);
                Destroy(k.t.gameObject);
                pickups.RemoveAt(i);
            }
        }

        if (!spawning) return;

        distSinceSpawn += roadSpeed * dt;
        if (distSinceSpawn >= nextGap)
        {
            distSinceSpawn = 0f;
            nextGap = Random.Range(minGap, maxGap);
            Spawn();
        }
    }

    void Spawn()
    {
        // Hurt -> mostly healing. Bomb stock full -> only healing.
        float healChance = Mathf.Lerp(0.8f, 0.25f, health.Fraction);
        if (abilities != null && abilities.Charges >= abilities.maxCharges) healChance = 1f;
        var kind = Random.value < healChance ? Kind.Heal : Kind.Bomb;

        // Pick a lane with no traffic car nearby
        for (int attempt = 0; attempt < 4; attempt++)
        {
            float x = LaneX[Random.Range(0, LaneX.Length)];
            if (traffic != null && traffic.IsBusy(x, spawnDistanceAhead)) continue;

            var t = BuildPotion(kind);
            t.SetParent(transform, false);
            t.position = new Vector3(x, 0f, spawnDistanceAhead);

            pickups.Add(new Pickup
            {
                t = t,
                visual = t.Find("Visual"),
                kind = kind,
                phase = Random.value * 6f
            });
            return;
        }
    }

    static Transform BuildPotion(Kind kind)
    {
        Color c = kind == Kind.Heal ? Green : Orange;

        var root = new GameObject(kind == Kind.Heal ? "Heal Potion" : "Bomb Potion").transform;

        // Glowing disc on the road so it is easy to see from far away
        Prim.Make(PrimitiveType.Cylinder, "Marker", root,
                  new Vector3(0f, 0.03f, 0f), new Vector3(2.2f, 0.02f, 2.2f), c);

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root, false);

        Prim.Make(PrimitiveType.Sphere, "Bottle", visual,
                  Vector3.zero, new Vector3(1.4f, 1.4f, 1.4f), c);
        Prim.Make(PrimitiveType.Cylinder, "Neck", visual,
                  new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 0.3f, 0.5f), Prim.Darken(c, 0.8f));
        Prim.Make(PrimitiveType.Cube, "Cork", visual,
                  new Vector3(0f, 1.25f, 0f), new Vector3(0.4f, 0.25f, 0.4f), new Color(0.55f, 0.35f, 0.2f));

        if (kind == Kind.Heal)
        {
            // White cross on the bottle
            Prim.Make(PrimitiveType.Cube, "CrossH", visual,
                      new Vector3(0f, 0f, 0.62f), new Vector3(0.7f, 0.2f, 0.1f), Color.white);
            Prim.Make(PrimitiveType.Cube, "CrossV", visual,
                      new Vector3(0f, 0f, 0.62f), new Vector3(0.2f, 0.7f, 0.1f), Color.white);
        }
        else
        {
            // Dark ring so the bomb potion looks different even without colour
            Prim.Make(PrimitiveType.Cylinder, "Band", visual,
                      Vector3.zero, new Vector3(1.45f, 0.12f, 1.45f), new Color(0.1f, 0.1f, 0.1f));
        }

        return root;
    }

    void Collect(Pickup k)
    {
        if (k.kind == Kind.Heal)
        {
            health.Heal(healAmount);
            Effects.Explosion(k.t.position, 2f, Green);
            if (game != null) game.Say("+" + Mathf.RoundToInt(healAmount) + " HP");
        }
        else
        {
            if (abilities != null) abilities.AddCharge();
            Effects.Explosion(k.t.position, 2f, Orange);
            if (game != null) game.Say("BIG BOMB +1");
        }
    }

    public void Clear()
    {
        foreach (var k in pickups)
        {
            if (k.t != null) Destroy(k.t.gameObject);
        }
        pickups.Clear();
        distSinceSpawn = 0f;
        nextGap = 120f;
    }
}
