using UnityEngine;
using UnityEngine.InputSystem;

namespace SoundCopy
{
    // Shared phone-motion readings for Sound Copy (Input System sensors, enabled on first use).
    //   Upright: 0 = lying flat (screen up or down) ... 1 = standing straight up (raised like a microphone)
    //   SpinSpeed: rotation speed around the screen's axis, in turns per second (+ = anticlockwise)
    public static class PhoneMotion
    {
        static Vector3 gravity;
        static bool hasGravity;
        static int lastFrame = -1;

        public static bool HasAccelerometer => Accelerometer.current != null;
        public static bool HasGyroscope => UnityEngine.InputSystem.Gyroscope.current != null;

        public static Vector3 Gravity { get { Sample(); return hasGravity ? gravity : new Vector3(0f, 0f, -1f); } }

        public static float Upright => 1f - Mathf.Abs(Gravity.z);

        public static bool IsFlat => HasAccelerometer && hasGravity && Upright < 0.25f;

        public static float SpinSpeed
        {
            get
            {
                var gyro = UnityEngine.InputSystem.Gyroscope.current;
                if (gyro == null) return 0f;
                if (!gyro.enabled) InputSystem.EnableDevice(gyro);
                return gyro.angularVelocity.ReadValue().z / (2f * Mathf.PI);
            }
        }

        // Wake the sensors early so they have readings by the time they are needed.
        public static void Warm()
        {
            Sample();
            _ = SpinSpeed;
        }

        static void Sample()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            var acc = Accelerometer.current;
            if (acc == null) return;
            if (!acc.enabled) InputSystem.EnableDevice(acc);
            Vector3 a = acc.acceleration.ReadValue();
            if (a.sqrMagnitude < 1e-4f) return;
            Vector3 direction = a.normalized;
            // Smooth out hand shake; works whatever units the sensor reports (only the direction is used).
            gravity = hasGravity ? Vector3.Slerp(gravity, direction, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)) : direction;
            hasGravity = true;
        }
    }
}
