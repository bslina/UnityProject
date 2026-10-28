using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Detects a deliberate shake from the accelerometer: several sharp jerks in a short time.
// The slow part of the signal (gravity) is what TiltInput uses; this only looks at the fast part.
// While jerks are happening, IsShaking is true so the tilt can be frozen and the egg stays safe.
// In the Editor, press S to fake a shake.
public class ShakeDetector : MonoBehaviour
{
    public static ShakeDetector Instance { get; private set; }

    [Tooltip("How hard one jerk must be, in g (1 = the strength of gravity).")]
    [SerializeField] float jerkThreshold = 0.8f;
    [Tooltip("How many jerks count as a shake.")]
    [SerializeField] int jerksNeeded = 3;
    [Tooltip("The jerks must all happen within this many seconds.")]
    [SerializeField] float shakeWindow = 0.8f;
    [SerializeField] float minTimeBetweenJerks = 0.08f;
    [SerializeField] float cooldown = 0.5f;
    [Tooltip("IsShaking stays true this long after the last jerk.")]
    [SerializeField] float holdTime = 0.35f;
    [Tooltip("How quickly the gravity estimate follows the phone. Lower = smoother.")]
    [SerializeField] float gravityFilter = 3f;

    public event Action Shaken;
    public bool IsShaking => Time.time - lastJerkTime < holdTime;
    // Current jerk strength in g, for the debug readout.
    public float Strength { get; private set; }

    readonly Queue<float> jerkTimes = new Queue<float>();
    Vector3 slow;
    bool hasSlow;
    bool wasAbove;
    float lastJerkTime = -10f;
    float lastCountedJerk = -10f;
    float cooldownUntil;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
        {
            lastJerkTime = Time.time;
            Fire();
        }

        var acc = Accelerometer.current;
        if (acc == null) return;
        if (!acc.enabled) InputSystem.EnableDevice(acc);

        Vector3 a = acc.acceleration.ReadValue();
        if (!hasSlow)
        {
            slow = a;
            hasSlow = true;
        }
        slow = Vector3.Lerp(slow, a, 1f - Mathf.Exp(-gravityFilter * Time.deltaTime));

        float g = slow.magnitude;
        if (g < 0.01f) return;

        // Measured relative to gravity, so it works whatever units the sensor uses.
        Strength = (a - slow).magnitude / g;
        bool above = Strength > jerkThreshold;
        if (above)
        {
            lastJerkTime = Time.time;
            if (!wasAbove && Time.time - lastCountedJerk > minTimeBetweenJerks)
            {
                jerkTimes.Enqueue(Time.time);
                lastCountedJerk = Time.time;
            }
        }
        wasAbove = above;

        while (jerkTimes.Count > 0 && Time.time - jerkTimes.Peek() > shakeWindow)
            jerkTimes.Dequeue();

        if (jerkTimes.Count >= jerksNeeded && Time.time >= cooldownUntil)
            Fire();
    }

    void Fire()
    {
        jerkTimes.Clear();
        cooldownUntil = Time.time + cooldown;
        Shaken?.Invoke();
    }
}
