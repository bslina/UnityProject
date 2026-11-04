using UnityEngine;

namespace GameUI
{
    // Menu music, button clicks and win/lose jingles. The clips come from the UI Theme
    // (Assets/UI/Resources/UITheme.asset), and per-minigame music from the MiniGame Registry.
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource sfxSource;

        void OnEnable()
        {
            GameSettings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        void OnDisable() => GameSettings.Changed -= ApplyVolumes;

        void ApplyVolumes()
        {
            if (musicSource != null) musicSource.volume = GameSettings.MusicVolume;
            if (sfxSource != null) sfxSource.volume = GameSettings.SfxVolume;
        }

        // Menu scenes play the theme's menu music; minigames play their own (or nothing).
        public void PlayMusicFor(MiniGameInfo game)
        {
            var theme = UITheme.Current;
            AudioClip clip = game != null ? game.music : (theme != null ? theme.menuMusic : null);
            if (musicSource == null || musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.clip = clip;
            if (clip != null) musicSource.Play();
            else musicSource.Stop();
        }

        public void PlayClick() => PlaySfx(UITheme.Current != null ? UITheme.Current.buttonClick : null);

        public void PlayResult(bool won)
        {
            var theme = UITheme.Current;
            if (theme != null) PlaySfx(won ? theme.winSound : theme.loseSound);
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
        }
    }
}
