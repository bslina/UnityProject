using UnityEngine;

// Side-view camera that follows the player.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(1f, 1.3f, -3.8f);
    [Tooltip("Moves the camera back along its offset: 1 = as set above, 1.45 = 45% further (the whole character fits, head included).")]
    [SerializeField, Range(0.5f, 2.5f)] float zoomOut = 1.45f;
    [SerializeField] float smoothing = 5f;

    Vector3 Desired => target.position + offset * zoomOut;

    void Start()
    {
        // Start in place (no glide from where the camera sits in the scene).
        if (target != null) transform.position = Desired;
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.Lerp(transform.position, Desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }
}
