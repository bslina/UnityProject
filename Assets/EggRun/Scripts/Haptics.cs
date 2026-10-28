using UnityEngine;

// Short vibration pulses on Android. Handheld.Vibrate() can only do one long buzz,
// so this calls Android's Vibrator directly for a set length and strength.
public static class Haptics
{
#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject vibrator;
    static int sdkVersion;
    static bool initialized;

    static void Init()
    {
        initialized = true;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                sdkVersion = version.GetStatic<int>("SDK_INT");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Haptics: no vibrator available. " + e.Message);
            vibrator = null;
        }
    }
#endif

    // milliseconds: pulse length. strength: 1 (faint) to 255 (strongest).
    public static void Pulse(long milliseconds, int strength = 255)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!initialized) Init();
        if (vibrator == null)
        {
            Handheld.Vibrate();
            return;
        }

        try
        {
            if (sdkVersion >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(strength, 1, 255)))
                    vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
        catch (System.Exception)
        {
            Handheld.Vibrate();
        }
#endif
    }
}
