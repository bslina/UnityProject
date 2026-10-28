using UnityEngine;
using UnityEngine.InputSystem;

// Turns the phone's gravity sensor into one smoothed left/right tilt angle in degrees.
// Positive angle = the right side of the phone is lowered.
// Falls back to the accelerometer, then to the keyboard (A/D or arrows) in the Editor.
public class TiltInput : MonoBehaviour
{
    public static TiltInput Instance { get; private set; }

    [SerializeField] float maxAngle = 45f;
    [Tooltip("Higher = follows the phone faster, lower = smoother.")]
    [SerializeField] float smoothing = 12f;
    [Tooltip("Small tilts below this many degrees are ignored.")]
    [SerializeField] float deadZone = 1f;
    [Tooltip("Flip this if the spoon tilts the wrong way on your phone.")]
    [SerializeField] bool invert = false;
    [Tooltip("Keyboard fallback: degrees per second while A/D or the arrow keys are held.")]
    [SerializeField] float keyboardSpeed = 60f;

    public float Angle { get; private set; }
    public string Source { get; private set; } = "none";

    float neutral;
    float keyboardAngle;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        EnableSensors();
    }

    // Also called every frame, because a sensor can appear late (e.g. Unity Remote connecting).
    void EnableSensors()
    {
        if (GravitySensor.current != null && !GravitySensor.current.enabled) InputSystem.EnableDevice(GravitySensor.current);
        if (Accelerometer.current != null && !Accelerometer.current.enabled) InputSystem.EnableDevice(Accelerometer.current);
    }

    void OnDisable()
    {
        if (GravitySensor.current != null) InputSystem.DisableDevice(GravitySensor.current);
        if (Accelerometer.current != null) InputSystem.DisableDevice(Accelerometer.current);
    }

    // Makes the way the player is holding the phone right now count as "level".
    public void Calibrate()
    {
        keyboardAngle = 0f;
        neutral = ReadRaw();
        Angle = 0f;
    }

    void Update()
    {
        EnableSensors();
        float target = Mathf.DeltaAngle(neutral, ReadRaw());
        if (Mathf.Abs(target) < deadZone) target = 0f;
        if (invert) target = -target;
        target = Mathf.Clamp(target, -maxAngle, maxAngle);

        Angle = Mathf.Lerp(Angle, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }

    float ReadRaw()
    {
        Vector3 g = Vector3.zero;
        if (GravitySensor.current != null && GravitySensor.current.enabled)
        {
            g = GravitySensor.current.gravity.ReadValue();
            Source = "Gravity sensor";
        }
        else if (Accelerometer.current != null && Accelerometer.current.enabled)
        {
            g = Accelerometer.current.acceleration.ReadValue();
            Source = "Accelerometer";
        }

        // No sensor, or it is not sending data yet (e.g. in the Editor without Unity Remote).
        if (g.sqrMagnitude < 0.01f)
            return ReadKeyboard();

        // Side-to-side tilt. Works whether the phone is held upright or flat.
        return Mathf.Asin(Mathf.Clamp(g.x / g.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
    }

    float ReadKeyboard()
    {
        Source = "Keyboard (A/D)";
        var kb = Keyboard.current;
        if (kb == null) return keyboardAngle;

        float dir = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir -= 1f;
        keyboardAngle = Mathf.Clamp(keyboardAngle + dir * keyboardSpeed * Time.deltaTime, -maxAngle, maxAngle);
        return keyboardAngle;
    }
}
