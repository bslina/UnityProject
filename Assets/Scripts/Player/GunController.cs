using UnityEngine;

// The gunner's turret on the roof of the car.
//   Aim:  the analog stick turns the turret through a FULL circle:
//         stick up = straight ahead, left/right = turn, stick down = shoot backwards.
//   Fire: hold the FIRE button for automatic fire.
//   Voice rounds: no button. Every spoken "shoot" fires exactly one bullet.
// A red laser line shows where you are aiming.
public class GunController : MonoBehaviour
{
    public IGameInput controls;
    public EnemySpawner enemies;
    public bool controlsEnabled = true;

    public float fireRate = 6f;        // shots per second
    public float bulletSpeed = 120f;
    public float turnSpeed = 540f;     // degrees per second
    public float voiceShotRate = 10f;  // fastest rate when "shoot shoot shoot" is said quickly (shots queue up)

    Transform pivot, muzzle;
    GameObject laser;
    float yaw, cooldown;
    int queuedShots;

    void Awake()
    {
        pivot = new GameObject("GunPivot").transform;
        pivot.SetParent(transform, false);
        pivot.localPosition = new Vector3(0f, 1.35f, -0.3f);

        Prim.Make(PrimitiveType.Cylinder, "TurretBase", pivot,
                  new Vector3(0f, -0.1f, 0f), new Vector3(0.7f, 0.12f, 0.7f), new Color(0.2f, 0.2f, 0.22f));
        Prim.Make(PrimitiveType.Cube, "Barrel", pivot,
                  new Vector3(0f, 0.05f, 0.8f), new Vector3(0.18f, 0.18f, 1.6f), new Color(0.55f, 0.57f, 0.6f));

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(pivot, false);
        muzzle.localPosition = new Vector3(0f, 0.05f, 1.6f);

        // Laser sight: a thin red line starting at the muzzle
        laser = Prim.Make(PrimitiveType.Cube, "Laser", pivot,
                          new Vector3(0f, 0.05f, 21.6f), new Vector3(0.05f, 0.05f, 40f), new Color(1f, 0.1f, 0.1f));
    }

    public void ResetGun()
    {
        yaw = 0f;
        cooldown = 0f;
        queuedShots = 0;
        controlsEnabled = true;
        pivot.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (!controlsEnabled || controls == null)
        {
            laser.SetActive(false);
            queuedShots = 0;
            return;
        }
        laser.SetActive(true);

        // Stick direction -> turret angle (full circle). Stick released = back to straight ahead.
        float targetYaw = 0f;
        var v = new Vector2(controls.AimX, controls.AimY);
        if (v.magnitude > 0.2f) targetYaw = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;

        yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, turnSpeed * dt);
        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

        cooldown -= dt;

        var voice = controls as IVoiceControls;
        if (voice != null && voice.VoiceMode)
        {
            // One "shoot" = one bullet. Words said faster than the gun can fire wait in a short queue.
            queuedShots = Mathf.Min(queuedShots + voice.ConsumeShots(), 12);
            if (queuedShots > 0 && cooldown <= 0f)
            {
                queuedShots--;
                cooldown = 1f / voiceShotRate;
                Shoot();
            }
            return;
        }

        if (controls.Fire && cooldown <= 0f)
        {
            cooldown = 1f / fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        Vector3 dir = muzzle.forward;
        dir.y = 0f;
        dir.Normalize();

        var go = Prim.Make(PrimitiveType.Cube, "Bullet", null,
                           muzzle.position, new Vector3(0.14f, 0.14f, 0.9f), new Color(1f, 0.9f, 0.2f));
        go.transform.rotation = Quaternion.LookRotation(dir);

        var bullet = go.AddComponent<Bullet>();
        bullet.velocity = dir * bulletSpeed;
        bullet.enemies = enemies;
    }
}
