using System;
using UnityEngine;

// The egg is a normal physics object: gravity rolls it around the spoon.
// It breaks as soon as it touches anything that is not the spoon.
[RequireComponent(typeof(Rigidbody))]
public class Egg : MonoBehaviour
{
    [SerializeField] GameObject wholeVisual;
    [SerializeField] GameObject brokenVisual;
    [Tooltip("Safety net: breaks the egg if it falls below this height.")]
    [SerializeField] float killHeight = -5f;

    public event Action Dropped;
    public bool IsBroken { get; private set; }

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (brokenVisual != null) brokenVisual.SetActive(false);
    }

    // Holds the egg still in the spoon until the round starts.
    public void Freeze(bool frozen)
    {
        rb.isKinematic = frozen;
    }

    void FixedUpdate()
    {
        if (!IsBroken && rb.position.y < killHeight)
            Break();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsBroken) return;
        if (collision.collider.GetComponentInParent<SpoonController>() == null)
            Break();
    }

    void Break()
    {
        IsBroken = true;

        rb.constraints = RigidbodyConstraints.FreezeAll;
        rb.rotation = Quaternion.identity;
        if (wholeVisual != null) wholeVisual.SetActive(false);
        if (brokenVisual != null) brokenVisual.SetActive(true);

        Haptics.Pulse(400);
        Dropped?.Invoke();
    }
}
