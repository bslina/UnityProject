using UnityEngine;
namespace DuoRoad
{

// Grows the explosion sphere, then shrinks it and removes it.
public class ExplosionFx : MonoBehaviour
{
    public float size = 2f;

    const float GrowTime = 0.12f;
    const float ShrinkTime = 0.30f;

    float t;

    void Update()
    {
        t += Time.deltaTime;

        float s = t < GrowTime
            ? Mathf.Lerp(0f, size, t / GrowTime)
            : Mathf.Lerp(size, 0f, (t - GrowTime) / ShrinkTime);

        transform.localScale = Vector3.one * Mathf.Max(0f, s);

        if (t > GrowTime + ShrinkTime) Destroy(gameObject);
    }
}
}
