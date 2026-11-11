using UnityEngine;

// Drives the player character's Animator (Assets/PocketJungle/MiniGames/EggRun/Character/EggRunCharacter.controller):
//   Speed (float)  idle <-> walking, from the WALK button
//   Win / Lose     Victory / Defeat at the end of the round
//   Shoo           waves an arm at the bird (arms-only layer "Arms", so the body keeps walking)
// Set up automatically by Tools > Egg Run > Set Up Character.
public class PlayerAnimator : MonoBehaviour
{
    public static PlayerAnimator Instance { get; private set; }

    [SerializeField] PlayerWalker walker;
    [SerializeField] Animator animator;
    [SerializeField] string speedParameter = "Speed";
    [Tooltip("Speeds the walk cycle up or down so the feet don't slide.")]
    [SerializeField] float walkAnimationSpeed = 1f;
    [Tooltip("Animator layer that only moves the arms (used for shooing the bird).")]
    [SerializeField] string armsLayer = "Arms";
    [Tooltip("How long the shoo wave lasts, in seconds.")]
    [SerializeField] float shooDuration = 1.3f;

    int speedHash, winHash, loseHash, shooHash;
    int armsLayerIndex = -1;
    float armsWeight;
    float shooUntil = -1f;
    bool finished;

    void Awake()
    {
        Instance = this;
        speedHash = Animator.StringToHash(speedParameter);
        winHash = Animator.StringToHash("Win");
        loseHash = Animator.StringToHash("Lose");
        shooHash = Animator.StringToHash("Shoo");
        if (walker == null) walker = GetComponent<PlayerWalker>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            armsLayerIndex = animator.GetLayerIndex(armsLayer);
    }

    void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        if (!finished && walker != null)
        {
            float speed = walker.Speed01;
            animator.SetFloat(speedHash, speed);
            animator.speed = speed > 0.01f ? Mathf.Lerp(1f, walkAnimationSpeed, speed) : 1f;
        }

        // The arms layer fades in only while shooing, so normally the walk/idle arms show.
        if (armsLayerIndex >= 0)
        {
            float target = Time.time < shooUntil ? 1f : 0f;
            armsWeight = Mathf.MoveTowards(armsWeight, target, Time.deltaTime * 6f);
            animator.SetLayerWeight(armsLayerIndex, armsWeight);
        }
    }

    // The player shook the phone: wave the bird away.
    public void Shoo()
    {
        if (animator == null || animator.runtimeAnimatorController == null || finished) return;
        animator.SetTrigger(shooHash);
        shooUntil = Time.time + shooDuration;
    }

    // End of the round: Victory or Defeat.
    public void ShowResult(bool won)
    {
        if (animator == null || animator.runtimeAnimatorController == null || finished) return;
        finished = true;
        animator.speed = 1f;
        animator.SetFloat(speedHash, 0f);
        animator.SetTrigger(won ? winHash : loseHash);
    }
}
