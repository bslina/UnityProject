using UnityEngine;

// Feeds the walking speed to the character's Animator so idle/walk blend with the WALK button.
// Put it on the Player, drag the character model's Animator into the slot,
// and give the Animator Controller a float parameter called "Speed" (0 = idle, 1 = full walk).
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] PlayerWalker walker;
    [SerializeField] Animator animator;
    [SerializeField] string speedParameter = "Speed";
    [Tooltip("Speeds the walk cycle up or down so the feet don't slide.")]
    [SerializeField] float walkAnimationSpeed = 1f;

    int speedHash;

    void Awake()
    {
        speedHash = Animator.StringToHash(speedParameter);
        if (walker == null) walker = GetComponent<PlayerWalker>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (walker == null || animator == null) return;
        float speed = walker.Speed01;
        animator.SetFloat(speedHash, speed);
        animator.speed = speed > 0.01f ? Mathf.Lerp(1f, walkAnimationSpeed, speed) : 1f;
    }
}
