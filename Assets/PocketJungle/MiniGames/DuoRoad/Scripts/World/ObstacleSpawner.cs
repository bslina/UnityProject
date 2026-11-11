using System.Collections.Generic;
using UnityEngine;
namespace DuoRoad
{

// Slow traffic cars appear far ahead on the road and you must dodge them.
// Collisions are checked directly in code (simple box overlap), so no physics setup is needed.
public class ObstacleSpawner : MonoBehaviour
{
    public CarController player;
    public PlayerHealth health;
    public RoadScroller road;

    public bool spawning = true;
    public float damage = 20f;

    [Header("Spawning")]
    public float spawnDistanceAhead = 150f;
    public float despawnBehind = -25f;
    public float minGap = 45f;     // metres travelled between two waves of traffic
    public float maxGap = 80f;

    [Header("Hit box (half sizes)")]
    public float hitHalfWidth = 1.5f;
    public float hitHalfLength = 3.8f;

    static readonly float[] LaneX = { -4f, 0f, 4f };

    static readonly Color[] Colors =
    {
        new Color(0.15f, 0.35f, 0.85f),
        new Color(0.95f, 0.80f, 0.15f),
        new Color(0.20f, 0.65f, 0.30f),
        new Color(0.90f, 0.90f, 0.92f),
        new Color(0.55f, 0.25f, 0.75f),
        new Color(0.10f, 0.65f, 0.65f)
    };

    class TrafficCar
    {
        public Transform t;
        public float ownSpeed;   // how fast this car drives forward
    }

    readonly List<TrafficCar> cars = new List<TrafficCar>();
    float distSinceSpawn;
    float nextGap = 40f;

    void Update()
    {
        if (player == null || health == null) return;

        float dt = Time.deltaTime;
        float roadSpeed = road != null ? road.Speed : 0f;

        for (int i = cars.Count - 1; i >= 0; i--)
        {
            var c = cars[i];
            var p = c.t.position;
            p.z -= (roadSpeed - c.ownSpeed) * dt;
            c.t.position = p;

            if (p.z < despawnBehind)
            {
                Destroy(c.t.gameObject);
                cars.RemoveAt(i);
                continue;
            }

            var pp = player.transform.position;
            if (Mathf.Abs(p.x - pp.x) < hitHalfWidth && Mathf.Abs(p.z - pp.z) < hitHalfLength)
            {
                if (health.TakeDamage(damage))
                {
                    player.Crash();
                    Destroy(c.t.gameObject);
                    cars.RemoveAt(i);
                }
            }
        }

        if (!spawning) return;

        distSinceSpawn += roadSpeed * dt;
        if (distSinceSpawn >= nextGap)
        {
            distSinceSpawn = 0f;
            nextGap = NextGap();
            SpawnWave();
        }
    }

    float NextGap()
    {
        // Traffic gets denser the further you go
        float k = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(player.Distance / 4000f));
        return Random.Range(minGap, maxGap) * k;
    }

    void SpawnWave()
    {
        int lane = Random.Range(0, LaneX.Length);
        TrySpawn(lane);

        // Sometimes a second car in ANOTHER lane (never all three lanes at once)
        if (Random.value < 0.25f)
        {
            int other = (lane + Random.Range(1, LaneX.Length)) % LaneX.Length;
            TrySpawn(other);
        }
    }

    void TrySpawn(int lane)
    {
        float x = LaneX[lane];

        foreach (var c in cars)
        {
            if (Mathf.Abs(c.t.position.x - x) < 1f &&
                Mathf.Abs(c.t.position.z - spawnDistanceAhead) < 15f) return;
        }

        var t = CarBuilder.Build("Traffic Car", Colors[Random.Range(0, Colors.Length)]);
        t.SetParent(transform, false);
        t.position = new Vector3(x, 0f, spawnDistanceAhead);

        cars.Add(new TrafficCar { t = t, ownSpeed = Random.Range(2f, 8f) });
    }

    // True if a traffic car is in (or about to be in) this lane near this distance.
    public bool IsBusy(float x, float z)
    {
        foreach (var c in cars)
        {
            if (Mathf.Abs(c.t.position.x - x) < 1.5f && Mathf.Abs(c.t.position.z - z) < 20f) return true;
        }
        return false;
    }

    // Big bomb: blows up every traffic car. Returns how many were destroyed.
    public int DestroyAll()
    {
        int n = cars.Count;
        foreach (var c in cars)
        {
            if (c.t == null) continue;
            Effects.Explosion(c.t.position, 3f);
            Destroy(c.t.gameObject);
        }
        cars.Clear();
        return n;
    }

    public void Clear()
    {
        foreach (var c in cars)
        {
            if (c.t != null) Destroy(c.t.gameObject);
        }
        cars.Clear();
        distSinceSpawn = 0f;
        nextGap = 40f;
    }
}
}
