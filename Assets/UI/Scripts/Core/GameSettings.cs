using System;
using UnityEngine;

namespace GameUI
{
    // Player settings shared by every minigame, saved in PlayerPrefs.
    // Read them anywhere (GameSettings.Vibration ...) and listen to Changed to react live.
    public static class GameSettings
    {
        const string Prefix = "settings.";

        public static event Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "music", 0.8f);
            set => SetFloat("music", Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "sfx", 1f);
            set => SetFloat("sfx", Mathf.Clamp01(value));
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(Prefix + "vibration", 1) == 1;
            set => SetInt("vibration", value ? 1 : 0);
        }

        // Flips tilt controls (for phones whose sensor reads the other way round).
        public static bool InvertTilt
        {
            get => PlayerPrefs.GetInt(Prefix + "invertTilt", 0) == 1;
            set => SetInt("invertTilt", value ? 1 : 0);
        }

        // Multiplier on tilt controls: 0.5 = gentle, 1 = normal, 1.5 = sharp.
        public static float TiltSensitivity
        {
            get => PlayerPrefs.GetFloat(Prefix + "tiltSensitivity", 1f);
            set => SetFloat("tiltSensitivity", Mathf.Clamp(value, 0.5f, 1.5f));
        }

        // Shows the developer readouts (EggRun debug text, sensor panel).
        public static bool ShowDebug
        {
            get => PlayerPrefs.GetInt(Prefix + "showDebug", 0) == 1;
            set => SetInt("showDebug", value ? 1 : 0);
        }

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            Save();
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            Save();
        }

        static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
