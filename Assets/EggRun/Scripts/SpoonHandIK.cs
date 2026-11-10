using UnityEngine;

// Keeps the character's hand on the spoon handle, whatever the walk animation does with the arm
// (Unity's built-in humanoid IK). Goes on the character's Animator object; the Animator Controller's
// base layer needs "IK Pass" ticked (Tools > Egg Run > Set Up Character does both).
[RequireComponent(typeof(Animator))]
public class SpoonHandIK : MonoBehaviour
{
    [Tooltip("What the hand holds. Empty = the spoon's \"Handle\" found automatically.")]
    [SerializeField] Transform target;
    [SerializeField] AvatarIKGoal hand = AvatarIKGoal.RightHand;
    [SerializeField, Range(0f, 1f)] float positionWeight = 1f;
    [Tooltip("0 = the wrist keeps the animation's angle, 1 = follows the spoon's angle.")]
    [SerializeField, Range(0f, 1f)] float rotationWeight = 0.3f;
    [Tooltip("Grip point, in the handle's own space (move it along the handle if the hand looks off).")]
    [SerializeField] Vector3 gripOffset = Vector3.zero;
    [SerializeField] Vector3 rotationOffset = Vector3.zero;

    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (target == null)
        {
            var spoon = FindAnyObjectByType<SpoonController>();
            if (spoon != null)
            {
                Transform handle = spoon.transform.Find("Handle");
                target = handle != null ? handle : spoon.transform;
            }
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || target == null || animator == null) return;
        animator.SetIKPositionWeight(hand, positionWeight);
        animator.SetIKPosition(hand, target.TransformPoint(gripOffset));
        animator.SetIKRotationWeight(hand, rotationWeight);
        animator.SetIKRotation(hand, target.rotation * Quaternion.Euler(rotationOffset));
    }
}
