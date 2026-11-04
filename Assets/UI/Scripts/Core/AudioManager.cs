using UnityEngine;

namespace GameUI
{
    // Menu music, button clicks and win/lose jingles. Drag the clips in on the GameUI prefab
    // (Assets/UI/Resources/GameUI.prefab, root object). Per-minigame music comes from the MiniGame Registry.
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource sfxSource;

        [Header("Sounds (optional)")]
        [SerializeField] AudioClip menuMusic;
        [SerializeField] AudioClip buttonClick;
        [SerializeField] AudioClip winSound;
        [SerializeField] AudioClip loseSound;

        void OnEnable()
        {
            // Pausing mutes the game (AudioListener.pause); menu clicks must still be heard.
            if (sfxSource != null) sfxSource.ignoreListenerPause = true;
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
            AudioClip clip = game != null ? game.music : menuMusic;
            if (musicSource == null || musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.clip = clip;
            if (clip != null) musicSource.Play();
            else musicSource.Stop();
        }

        public void PlayClick() => PlaySfx(buttonClick);

        public void PlayResult(bool won) => PlaySfx(won ? winSound : loseSound);

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
        }
    }
}
