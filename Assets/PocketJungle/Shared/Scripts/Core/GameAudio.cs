using System.Collections.Generic;
using UnityEngine;

namespace GameUI
{
    // Sounds by name, from Assets/PocketJungle/Shared/Resources/Audio/<name>.wav (or .mp3/.ogg).
    // To change a sound, replace that file with your own using the same name. No wiring needed.
    //
    //   GameAudio.Play("egg_crack");
    //
    // Music: music_menu (all menus) and music_<minigame id> (e.g. music_egg_run), played by the AudioManager.
    public static class GameAudio
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!cache.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                cache[name] = clip; // remember misses too
            }
            return clip;
        }

        // Plays a sound effect at the player's "Sound effects" volume (Settings).
        public static void Play(string name, float volume = 1f)
        {
            AudioClip clip = Get(name);
            if (clip == null) return;
            if (GameFlow.Instance != null && GameFlow.Instance.Audio != null) GameFlow.Instance.Audio.PlaySfx(clip, volume);
            else AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, GameSettings.SfxVolume * volume);
        }
    }
}
