using UnityEngine;

// Footstep sounds while the player walks, in time with the character's walk animation
// (2 steps per walk loop). Without an animated character it follows PlayerWalker's step rhythm.
// Added to the Player automatically by the GameManager; add it yourself to tweak the settings.
// Sounds: footstep_1 ... footstep_4 in Assets/PocketJungle/Shared/Resources/Audio (a random one per step).
public class Footsteps : MonoBehaviour
{
    [SerializeField] PlayerWalker walker;
    [Tooltip("The character's Animator (found automatically).")]
    [SerializeField] Animator animator;
    [SerializeField] string soundPrefix = "footstep_";
    [SerializeField] int soundCount = 4;
    [SerializeField, Range(0f, 1f)] float volume = 1f;
    [Tooltip("Steps per loop of the walk animation (Mixamo walks: 2).")]
    [SerializeField] int stepsPerLoop = 2;
    [Tooltip("Moves the sound earlier/later so it lands when the foot touches the ground (0-1 of a step).")]
    [SerializeField, Range(0f, 1f)] float stepOffset = 0.1f;
    [Tooltip("No footsteps below this walking speed (0-1).")]
    [SerializeField, Range(0f, 1f)] float minSpeed = 0.2f;

    AudioClip[] clips = new AudioClip[0];
    int lastStep = int.MinValue;
    int lastClip = -1;

    void Awake()
    {
        if (walker == null) walker = GetComponent<PlayerWalker>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        var list = new System.Collections.Generic.List<AudioClip>();
        for (int i = 1; i <= soundCount; i++)
        {
            var clip = GameUI.GameAudio.Get(soundPrefix + i);
            if (clip != null) list.Add(clip);
        }
        clips = list.ToArray();
    }

    void Update()
    {
        if (walker == null || clips.Length == 0) return;
        float speed = walker.Speed01;
        if (!walker.CanWalk || speed < minSpeed) { lastStep = int.MinValue; return; }

        // Count steps: from the walk animation if there is one, otherwise from the walker's rhythm.
        float steps;
        if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
            steps = animator.GetCurrentAnimatorStateInfo(0).normalizedTime * stepsPerLoop + stepOffset;
        else
            steps = walker.StepPhase / Mathf.PI + stepOffset;

        int step = Mathf.FloorToInt(steps);
        if (step == lastStep) return;
        bool first = lastStep == int.MinValue;
        lastStep = step;
        if (first) return;                    // don't play a step the moment walking starts mid-stride

        int pick = Random.Range(0, clips.Length);
        if (clips.Length > 1 && pick == lastClip) pick = (pick + 1) % clips.Length;   // no twice the same
        lastClip = pick;
        GameUI.GameAudio.Play(clips[pick].name, volume * Mathf.Lerp(0.8f, 1f, speed));
    }
}
