using UnityEngine;

// Moves the spoon with the player's hand and tilts it with the phone.
// Walking adds a rocking wobble; the egg itself is moved only by physics gravity.
[RequireComponent(typeof(Rigidbody))]
public class SpoonController : MonoBehaviour
{
    [SerializeField] PlayerWalker walker;
    [Tooltip("Spoon bowl position relative to the player's root.")]
    [SerializeField] Vector3 handOffset = new Vector3(1.1f, 1.25f, 0f);
    [Tooltip("Left/right rock in rhythm with the steps, in degrees at full speed.")]
    [SerializeField] float wobbleAngle = 14f;
    [Tooltip("Extra random sway while walking, in degrees at full speed.")]
    [SerializeField] float swayAngle = 7f;
    [SerializeField] float swaySpeed = 0.9f;
    [SerializeField] float bobHeight = 0.05f;
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

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        noiseSeed = Random.value * 100f;
    }

    void FixedUpdate()
    {
        float walk = walker != null ? walker.Speed01 : 0f;
        float phase = walker != null ? walker.StepPhase : 0f;
        float tilt = FollowTilt && TiltInput.Instance != null ? TiltInput.Instance.Angle : 0f;

        float sway = Mathf.PerlinNoise(noiseSeed, Time.time * swaySpeed) * 2f - 1f;
        float wobble = walk * (wobbleAngle * Mathf.Sin(phase) + swayAngle * sway);
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
}
