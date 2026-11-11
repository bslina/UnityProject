using UnityEngine;
using UnityEngine.InputSystem;
namespace DuoRoad
{

// Phone-as-a-controller for the solo rounds. Hold the phone in landscape like a wheel.
//
//   STEER  roll the phone left / right (like turning a wheel)       -> Read() returns -1..1
//   AIM    pitch the phone: top edge AWAY from you = aim BEHIND,     -> AimBack
//          back to level (or towards you) = aim to the FRONT
//
// - The neutral position is whatever angle the phone has when Calibrate() is called
//   (GameManager calls it when a round starts and when you resume from pause).
//   At that moment the gun looks to the front and the car goes straight.
// - Roll and pitch are measured from the direction of gravity, so they are independent:
//   you can steer and aim at the same time.
// - Needs the Input System's "compensate for screen orientation" (on by default) so that the
//   accelerometer axes follow the screen: x = screen right, y = screen up, z = out of the screen.
// - No accelerometer (Unity editor, PC): HasSensor is false and TouchInput uses the keyboard.
public class TiltInput
{
    [System.NonSerialized] public float fullLockDegrees = 30f;    // roll that gives full steering
    [System.NonSerialized] public float deadZoneDegrees = 3f;     // ignore tiny hand tremors around neutral
    [System.NonSerialized] public float smoothing = 12f;          // higher = snappier, lower = smoother
    [System.NonSerialized] public float aimBackDegrees = 12f;     // pitch (top edge away) that flips the gun to the back
    [System.NonSerialized] public float aimHysteresis = 6f;       // must come back this much below it to flip to the front

    public bool HasSensor { get { return Accelerometer.current != null; } }

    // True while the gun should look behind the car
    public bool AimBack { get; private set; }

    float rollNeutral, rollSmoothed;
    float pitchNeutral, pitchSmoothed;
    float sign = 1f;       // some platforms report gravity with the opposite sign: detected at calibration
    bool pending = true;   // calibrate on the first valid sensor sample

    // Call once (the Android / iOS accelerometer is off until it is enabled)
    public void Enable()
    {
        var a = Accelerometer.current;
        if (a != null && !a.enabled) InputSystem.EnableDevice(a);
    }

    // The current phone angle becomes "straight ahead, gun to the front"
    public void Calibrate()
    {
        Enable();
        pending = true;
        AimBack = false;
    }

    // Steering value from -1 (full left) to 1 (full right). Also updates AimBack. Call once per frame.
    public float Read(float dt)
    {
        Vector3 g;
        if (!TryReadGravity(out g)) return 0f;

        if (pending)
        {
            // Upright landscape: gravity points to the screen bottom (y < 0) on most platforms
            sign = g.y <= 0.1f ? 1f : -1f;
            rollNeutral = Roll(g);
            rollSmoothed = rollNeutral;
            pitchNeutral = Pitch(g);
            pitchSmoothed = pitchNeutral;
            AimBack = false;
            pending = false;
        }

        float k = 1f - Mathf.Exp(-smoothing * dt);
        rollSmoothed = Mathf.LerpAngle(rollSmoothed, Roll(g), k);
        pitchSmoothed = Mathf.Lerp(pitchSmoothed, Pitch(g), k);

        // Aim: top edge away from you (pitch goes up) = look behind. Hysteresis stops flickering.
        float dp = pitchSmoothed - pitchNeutral;
        if (!AimBack && dp > aimBackDegrees) AimBack = true;
        else if (AimBack && dp < aimBackDegrees - aimHysteresis) AimBack = false;

        // Steer: + = turned clockwise = steer right
        float delta = Mathf.DeltaAngle(rollNeutral, rollSmoothed);
        float mag = Mathf.Abs(delta);
        if (mag <= deadZoneDegrees) return 0f;

        float steer = (mag - deadZoneDegrees) / Mathf.Max(1f, fullLockDegrees - deadZoneDegrees);
        return Mathf.Clamp(Mathf.Sign(delta) * steer, -1f, 1f);
    }

    // Rotation about the screen normal (turning the phone like a wheel)
    float Roll(Vector3 g)
    {
        return Mathf.Atan2(sign * g.x, -sign * g.y) * Mathf.Rad2Deg;
    }

    // Top edge tilting away from you (screen turning to face the sky) = positive
    float Pitch(Vector3 g)
    {
        float inPlane = Mathf.Sqrt(g.x * g.x + g.y * g.y);
        return Mathf.Atan2(-sign * g.z, inPlane) * Mathf.Rad2Deg;
    }

    static bool TryReadGravity(out Vector3 g)
    {
        g = Vector3.zero;
        var a = Accelerometer.current;
        if (a == null) return false;

        g = a.acceleration.ReadValue();
        return g.sqrMagnitude > 0.01f;
    }
}
}
