using UnityEngine;

namespace GameUI
{
    // Music and the shared sound effects (click, win, lose).
    // Clips dragged in here win; empty slots use the files in Assets/UI/Resources/Audio:
    //   music_menu, click, win, lose, and music_<minigame id> for each minigame (e.g. music_egg_run).
    // A minigame's own Music slot in the MiniGame Registry also beats the file.
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource sfxSource;

        [Header("Sounds (optional: empty = Resources/Audio files)")]
        [SerializeField] AudioClip menuMusic;
        [SerializeField] AudioClip buttonClick;
        [SerializeField] AudioClip winSound;
        [SerializeField] AudioClip loseSound;

        [Header("Mix")]
        [Tooltip("Music level under the effects (multiplied with the player's Music volume).")]
        [SerializeField, Range(0f, 1f)] float musicLevel = 0.55f;

        bool musicPaused;

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
            if (musicSource != null) musicSource.volume = GameSettings.MusicVolume * musicLevel;
            if (sfxSource != null) sfxSource.volume = GameSettings.SfxVolume;
        }

        // Menu scenes play the menu music; each minigame plays its own.
        public void PlayMusicFor(MiniGameInfo game)
        {
            AudioClip clip = game != null
                ? (game.music != null ? game.music : GameAudio.Get("music_" + game.id))
                : (menuMusic != null ? menuMusic : GameAudio.Get("music_menu"));
            musicPaused = false;
            if (musicSource == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.clip = clip;
            musicSource.loop = true;
            if (clip != null) musicSource.Play();
            else musicSource.Stop();
        }

        // For moments that need silence (e.g. Sound Copy while the microphone records).
        public void SetMusicPaused(bool paused)
        {
            if (musicSource == null || paused == musicPaused) return;
            musicPaused = paused;
            if (paused) musicSource.Pause();
            else if (musicSource.clip != null) musicSource.UnPause();
        }

        public void PlayClick() => PlaySfx(buttonClick != null ? buttonClick : GameAudio.Get("click"), 0.8f);

        public void PlayResult(bool won) => PlaySfx(won ? (winSound != null ? winSound : GameAudio.Get("win"))
                                                        : (loseSound != null ? loseSound : GameAudio.Get("lose")), won ? 1f : 0.7f);

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, volume);
        }
    }
}
