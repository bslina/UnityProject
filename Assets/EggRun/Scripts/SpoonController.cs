using UnityEngine;

// Moves the spoon with the player's hand and tilts it with the phone.
// Walking makes the spoon unsteady, so the player has to keep correcting with the tilt:
//   - wobble: left/right rock in rhythm with the steps
//   - sway:   faster random shake
//   - drift:  a SLOW random lean that holds for seconds (this is what rolls the egg off if ignored)
//   - stumbles: every few seconds a sudden lurch to one side
// The egg itself is moved only by physics gravity.
[RequireComponent(typeof(Rigidbody))]
public class SpoonController : MonoBehaviour
{
    [SerializeField] PlayerWalker walker;
    [Tooltip("Spoon bowl position relative to the player's root.")]
    [SerializeField] Vector3 handOffset = new Vector3(1.1f, 1.25f, 0f);
    [Tooltip("Left/right rock in rhythm with the steps, in degrees at full speed.")]
    [SerializeField] float wobbleAngle = 18f;
    [Tooltip("Extra random sway while walking, in degrees at full speed.")]
    [SerializeField] float swayAngle = 10f;
    [SerializeField] float swaySpeed = 1.1f;
    [SerializeField] float bobHeight = 0.08f;

    [Header("Walking difficulty")]
    [Tooltip("Slow lean that builds up while walking, in degrees at full speed. Must be corrected with the tilt.")]
    [SerializeField] float driftAngle = 12f;
    [Tooltip("How quickly the slow lean changes direction (lower = longer leans).")]
    [SerializeField] float driftSpeed = 0.22f;
    [Tooltip("Strength of a stumble, in degrees (random between x and y).")]
    [SerializeField] Vector2 stumbleAngle = new Vector2(10f, 18f);
    [Tooltip("Seconds of walking between stumbles (random between x and y). 0,0 = no stumbles.")]
    [SerializeField] Vector2 stumbleInterval = new Vector2(2.5f, 5f);
    [Tooltip("Seconds for a stumble to reach full strength, then to settle again.")]
    [SerializeField] Vector2 stumbleRiseFall = new Vector2(0.15f, 0.8f);
    [Tooltip("Multiplies every walking effect above. Quick way to tune the difficulty.")]
    [SerializeField, Range(0f, 2f)] float walkDifficulty = 1f;
    [Tooltip("Small jiggle while the phone is being shaken, in degrees. 0 = shaking is totally safe.")]
    [SerializeField] float shakeJiggle = 3f;
    [Tooltip("Caps how fast the spoon can turn, so a sensor spike cannot fling the egg.")]
    [SerializeField] float maxTurnSpeed = 360f;

    // When false (before the round starts) the spoon stays level.
    public bool FollowTilt { get; set; }
    // Extra tilt from outside forces, e.g. a bird's weight on the rim. Same sign as the phone tilt.
    public float ExtraTilt { get; set; }
    public float CurrentAngle { get; private set; }

    Rigidbody rb;
    float noiseSeed;
    float stumbleTimer;
    float stumbleAge = 10f;
    float stumbleTarget;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        noiseSeed = Random.value * 100f;
        stumbleTimer = Random.Range(stumbleInterval.x, stumbleInterval.y);
    }

    void FixedUpdate()
    {
        float walk = walker != null ? walker.Speed01 : 0f;
        float phase = walker != null ? walker.StepPhase : 0f;
        float tilt = FollowTilt && TiltInput.Instance != null ? TiltInput.Instance.Angle : 0f;

        float sway = Mathf.PerlinNoise(noiseSeed, Time.time * swaySpeed) * 2f - 1f;
        // Drift only moves while walking, so standing still is a safe way to recover.
        driftTime += walk * driftSpeed * Time.fixedDeltaTime;
        float drift = Mathf.PerlinNoise(noiseSeed + 37.1f, driftTime) * 2f - 1f;
        float wobble = walk * (wobbleAngle * Mathf.Sin(phase) + swayAngle * sway + driftAngle * drift)
            + Stumble(walk);
        wobble *= walkDifficulty;
        if (ShakeDetector.Instance != null && ShakeDetector.Instance.IsShaking)
            wobble += shakeJiggle * Mathf.Sin(Time.time * 40f);

        // A negative Z rotation lowers the right side, matching a positive phone tilt.
        float targetAngle = -(tilt + wobble + ExtraTilt);
        CurrentAngle = Mathf.MoveTowardsAngle(CurrentAngle, targetAngle, maxTurnSpeed * Time.fixedDeltaTime);

        Vector3 playerPos = walker != null ? walker.NextPosition : Vector3.zero;
        Vector3 bob = Vector3.up * (bobHeight * walk * Mathf.Abs(Mathf.Sin(phase)));
        rb.MovePosition(playerPos + handOffset + bob);
        rb.MoveRotation(Quaternion.Euler(0f, 0f, CurrentAngle));
    }

    float driftTime;

    // A sudden lurch to one side every few seconds of walking.
    float Stumble(float walk)
    {
        float dt = Time.fixedDeltaTime;
        stumbleAge += dt;

        if (stumbleInterval.y > 0f && walk > 0.5f)
        {
            stumbleTimer -= dt * walk;
            if (stumbleTimer <= 0f)
            {
                stumbleTimer = Random.Range(stumbleInterval.x, stumbleInterval.y);
                stumbleAge = 0f;
                stumbleTarget = Random.Range(stumbleAngle.x, stumbleAngle.y) * (Random.value < 0.5f ? -1f : 1f);
                Haptics.Pulse(40, 120);
            }
        }

        float rise = Mathf.Max(0.01f, stumbleRiseFall.x);
        float fall = Mathf.Max(0.01f, stumbleRiseFall.y);
        float envelope = stumbleAge < rise
            ? Mathf.SmoothStep(0f, 1f, stumbleAge / rise)
            : Mathf.SmoothStep(1f, 0f, (stumbleAge - rise) / fall);
        return stumbleTarget * envelope;
    }
}
