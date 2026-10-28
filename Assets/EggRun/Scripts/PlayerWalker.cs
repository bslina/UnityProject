using UnityEngine;

// Walks the player to the right while the walk button is held.
// Runs before SpoonController so the spoon always follows this frame's position.
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody))]
public class PlayerWalker : MonoBehaviour
{
    [SerializeField] WalkButton walkButton;
    [SerializeField] float walkSpeed = 1.6f;
    [Tooltip("How fast the player speeds up and slows down, in m/s per second.")]
    [SerializeField] float acceleration = 3f;
    [SerializeField] float stepsPerSecond = 2.2f;
    [Tooltip("The visual body that bobs up and down with each step (optional).")]
    [SerializeField] Transform body;
    [SerializeField] float bodyBob = 0.05f;

    public bool CanWalk { get; set; }
    public float Speed01 => walkSpeed > 0f ? speed / walkSpeed : 0f;
    // Grows by PI with each step: sin(StepPhase) rocks left/right, |sin| bobs once per step.
    public float StepPhase { get; private set; }
    // Where the player will be after this physics step.
    public Vector3 NextPosition { get; private set; }

    Rigidbody rb;
    float speed;
    Vector3 bodyRest;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        NextPosition = rb.position;
        if (body != null) bodyRest = body.localPosition;
    }

    void FixedUpdate()
    {
        bool wantsToWalk = CanWalk && walkButton != null && walkButton.IsHeld;
        speed = Mathf.MoveTowards(speed, wantsToWalk ? walkSpeed : 0f, acceleration * Time.fixedDeltaTime);

        NextPosition = rb.position + Vector3.right * (speed * Time.fixedDeltaTime);
        rb.MovePosition(NextPosition);
        StepPhase += Speed01 * stepsPerSecond * Mathf.PI * Time.fixedDeltaTime;
    }

    void Update()
    {
        if (body != null)
            body.localPosition = bodyRest + Vector3.up * (Mathf.Abs(Mathf.Sin(StepPhase)) * bodyBob * Speed01);
    }
}
