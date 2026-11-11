using UnityEngine;
namespace DuoRoad
{

// Phone vibration. Call Haptics.Vibrate(milliseconds) from anywhere.
//   Android: real duration control (VibrationEffect on Android 8+, plain vibrate below).
//   iOS: Handheld.Vibrate() (fixed length, duration ignored).
//   Editor / PC: nothing happens except a log line, so you can check the call is made.
public static class Haptics
{
    // Keeps the pattern from stacking when several hits land at once
    static float lastTime = -10f;
    const float MinGap = 0.08f;

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject vibrator;
    static int sdk;
    static bool ready;

    static void Init()
    {
        if (ready) return;
        ready = true;
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            using (var ver = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                sdk = ver.GetStatic<int>("SDK_INT");
            }
        }
        catch (System.Exception) { vibrator = null; }
    }
#endif

    public static void Vibrate(long milliseconds)
    {
        if (Time.unscaledTime - lastTime < MinGap) return;
        lastTime = Time.unscaledTime;

#if UNITY_EDITOR
        Debug.Log("[Haptics] Vibrate " + milliseconds + " ms");
#elif UNITY_ANDROID
        Init();
        try
        {
            if (vibrator == null) { Handheld.Vibrate(); return; }

            if (sdk >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, -1))
                {
                    vibrator.Call("vibrate", effect);
                }
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
#elif UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
}
