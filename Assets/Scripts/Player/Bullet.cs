using UnityEngine;

// A bullet fired by the gunner. It flies in a straight line and asks the EnemySpawner
// whether it hit an enemy car or popped a bomb in the air.
// EnemySpawner.TryHit checks 3 points between the old and new position, so fast bullets
// never skip through a target.
public class Bullet : MonoBehaviour
{
    public Vector3 velocity;
    public EnemySpawner enemies;

    float life = 1.6f;

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 prev = transform.position;
        Vector3 next = prev + velocity * dt;
        transform.position = next;
        life -= dt;

        if (enemies != null && enemies.TryHit(prev, next))
        {
            Destroy(gameObject);
            return;
        }

        if (life <= 0f || next.z > 220f || next.z < -80f || Mathf.Abs(next.x) > 80f) Destroy(gameObject);
    }
}
