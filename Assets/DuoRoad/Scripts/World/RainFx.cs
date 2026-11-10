using UnityEngine;
namespace DuoRoad
{

// Rain made of thin falling streaks that stay around the car. No particle system and no
// shader needed (works the same in the editor and on the phone).
// ZoneManager sets Amount (0 = no rain, 1 = heavy rain); the streaks fade in and out with it.
public class RainFx : MonoBehaviour
{
    public Transform follow;       // the car: the rain falls around it
    public RoadScroller road;      // the rain leans backwards when the road is fast
    public int maxDrops = 140;     // lower this if the phone is slow

    [Range(0f, 1f)] public float Amount;

    const float Height = 24f;
    const float Fall = 38f;

    static readonly Color DropColor = new Color(0.78f, 0.87f, 1f);

    Transform[] drops;
    int active;

    void Awake()
    {
        drops = new Transform[maxDrops];
        for (int i = 0; i < maxDrops; i++)
        {
            var d = Prim.Make(PrimitiveType.Cube, "Drop", transform, Vector3.zero,
                              new Vector3(0.03f, 1f, 0.03f), DropColor);
            d.SetActive(false);
            drops[i] = d.transform;
        }
    }

    void Update()
    {
        float cx = follow != null ? follow.position.x : 0f;

        // Show more or fewer streaks depending on the rain amount
        int want = Mathf.RoundToInt(maxDrops * Amount);
        while (active < want)
        {
            drops[active].position = RandomPos(cx, true);
            drops[active].gameObject.SetActive(true);
            active++;
        }
        while (active > want)
        {
            active--;
            drops[active].gameObject.SetActive(false);
        }
        if (active == 0) return;

        float dt = Time.deltaTime;
        float speed = road != null ? road.Speed : 0f;
        var vel = new Vector3(0f, -Fall, -speed * 0.35f);
        var rot = Quaternion.FromToRotation(Vector3.up, -vel.normalized);

        for (int i = 0; i < active; i++)
        {
            var t = drops[i];
            var p = t.position + vel * dt;
            if (p.y < 0.2f || p.z < -25f) p = RandomPos(cx, false);
            t.position = p;
            t.rotation = rot;
        }
    }

    static Vector3 RandomPos(float cx, bool anyHeight)
    {
        return new Vector3(cx + Random.Range(-28f, 28f),
                           anyHeight ? Random.Range(0f, Height) : Height + Random.Range(0f, 4f),
                           Random.Range(-14f, 70f));
    }
}
}
