using UnityEngine;

// Drives the player car. It reads an IGameInput, so it works the same with
// touch (Easy), tilt (Medium) or voice (Hard).
// The road never stops while you play: the car always keeps at least minSpeed.
public class CarController : MonoBehaviour
{
    public RoadScroller road;
    public IGameInput controls;
    public bool controlsEnabled = true;

    [Header("Speed (units per second)")]
    public float minSpeed = 22f;
    public float maxSpeed = 65f;
    public float acceleration = 22f;
    public float brakePower = 40f;
    public float coastDrag = 3f;

    [Header("Steering")]
    public float lateralSpeed = 9f;
    public float xLimit = 4.8f;

    public float Speed { get; private set; }
    public float Distance { get; private set; }   // metres travelled
    public float Shake { get; private set; }      // camera shake after a crash (0..1)

    float steerSmoothed;

    void Start()
    {
        Speed = minSpeed;
    }

    // Called when the car hits something: lose speed and shake the camera.
    public void Crash()
    {
        Speed = Mathf.Max(minSpeed, Speed * 0.5f);
        Shake = 0.6f;
    }

    public void AddShake(float amount)
    {
        Shake = Mathf.Max(Shake, amount);
    }

    public void ResetCar()
    {
        Distance = 0f;
        Speed = minSpeed;
        steerSmoothed = 0f;
        Shake = 0f;
        controlsEnabled = true;

        var p = transform.position;
        p.x = 0f;
        transform.position = p;
        transform.rotation = Quaternion.identity;
    }

    // Stops the car dead (used while a menu is shown between rounds, so the road does not keep rolling)
    public void Park()
    {
        Speed = 0f;
        Shake = 0f;
        if (road != null) road.Speed = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (Shake > 0f) Shake = Mathf.Max(0f, Shake - dt * 1.5f);

        if (!controlsEnabled)
        {
            // Game over: the car rolls to a stop
            Speed = Mathf.MoveTowards(Speed, 0f, 35f * dt);
            if (road != null) road.Speed = Speed;
            return;
        }

        if (controls == null) return;

        if (controls.Accelerate) Speed += acceleration * dt;
        else if (controls.Brake) Speed -= brakePower * dt;
        else Speed -= coastDrag * dt;
        Speed = Mathf.Clamp(Speed, minSpeed, maxSpeed);

        steerSmoothed = Mathf.MoveTowards(steerSmoothed, controls.Steer, 6f * dt);
        float speedFactor = Mathf.Lerp(0.6f, 1f, Speed / maxSpeed);

        var pos = transform.position;
        pos.x = Mathf.Clamp(pos.x + steerSmoothed * lateralSpeed * speedFactor * dt, -xLimit, xLimit);
        transform.position = pos;

        // Small visual lean when steering
        transform.rotation = Quaternion.Euler(0f, steerSmoothed * 10f, -steerSmoothed * 4f);

        if (road != null) road.Speed = Speed;
        Distance += Speed * dt;
    }
}
