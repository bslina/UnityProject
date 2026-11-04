using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SoundCopy.UI
{
    // Plays the round's meme video (the sound to imitate).
    public sealed class VideoPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button backButton;
        [SerializeField] private VideoClip[] internationalClips;
        [SerializeField] private VideoClip[] tunisianClips;
        private RenderTexture renderTexture;
        private VideoClip lastClip;
        private bool resumeAfterPause;
        public string CurrentClipId => lastClip != null ? lastClip.name : string.Empty;
        public float CurrentClipDuration => lastClip != null ? (float)lastClip.length : 4f;

        public void Initialize(GameManager manager, VideoClip[] international, VideoClip[] tunisian)
        {
            gameManager = manager;
            internationalClips = international;
            tunisianClips = tunisian;
        }

        private void Awake()
        {
            continueButton?.onClick.AddListener(Continue);
            replayButton?.onClick.AddListener(Replay);
            backButton?.onClick.AddListener(Back);
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.errorReceived -= OnVideoError;
            }
            ReleaseTexture();
        }

        private void OnDisable()
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        }

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            backButton = MobileUI.TopBar(page, "ÉCOUTE LE SON", true, SoundCopyTheme.Cream, out _, out _);

            // Square frame: the video is letterboxed inside, whatever its shape.
            var frame = MobileUI.Row(page, "VideoFrame", 200f, 968f);
            MobileUI.Card(frame.gameObject, SoundCopyTheme.Ink, 44f);
            UIFactory.AddNativeOutline(frame.gameObject, SoundCopyTheme.Cream, 4f);
            var imageObject = UIFactory.CreateRect("VideoImage", frame, Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -16f));
            videoImage = imageObject.AddComponent<RawImage>();
            videoImage.color = Color.white;
            videoImage.raycastTarget = false;
            videoPlayer = imageObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;

            var status = MobileUI.Row(page, "Status", 1200f, 80f);
            statusText = MobileUI.Text(status, "Text", "Prêt à lancer le son", 48f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, true);
            var hint = MobileUI.Row(page, "Hint", 1290f, 110f);
            var hintText = MobileUI.Text(hint, "Text", "Mémorise bien le rythme et la voix :\nvous allez devoir l'imiter !", 36f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            hintText.textWrappingMode = TextWrappingModes.Normal;

            var replay = MobileUI.BottomRow(page, "ReplayAction", MobileUI.BottomMargin + MobileUI.ButtonHeight + 30f, 140f);
            replayButton = MobileUI.BigButton(replay, "ReplayButton", "REVOIR", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 46f);
            var next = MobileUI.BottomRow(page, "ContinueAction", MobileUI.BottomMargin, MobileUI.ButtonHeight);
            continueButton = MobileUI.BigButton(next, "ContinueButton", "C'EST PARTI  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
            continueButton.interactable = false;
        }

        public void PlayForCategory(ChallengeCategory category)
        {
            VideoClip clip = ChooseClip(category);
            if (clip == null)
            {
                statusText.text = "Aucune vidéo pour cette catégorie";
                continueButton.interactable = false;
                return;
            }
            EnsureTexture();
            lastClip = clip;
            videoPlayer.clip = clip;
            continueButton.interactable = false;
            statusText.text = "Écoute bien…";
            videoPlayer.Play();
        }

        private VideoClip ChooseClip(ChallengeCategory category)
        {
            VideoClip[] source;
            if (category == ChallengeCategory.International) source = internationalClips;
            else if (category == ChallengeCategory.Tunisian) source = tunisianClips;
            else source = UnityEngine.Random.value < 0.5f ? internationalClips : tunisianClips;
            if (source == null || source.Length == 0) return null;
            VideoClip selected = source[UnityEngine.Random.Range(0, source.Length)];
            if (source.Length > 1 && selected == lastClip)
                selected = source[(Array.IndexOf(source, selected) + 1) % source.Length];
            return selected;
        }

        private void EnsureTexture()
        {
            if (renderTexture != null) return;
            // A square target works for portrait, landscape and square clips (FitInside letterboxes).
            renderTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32) { name = "SoundCopyVideoRT" };
            renderTexture.Create();
            videoPlayer.targetTexture = renderTexture;
            videoImage.texture = renderTexture;
        }

        private void ReleaseTexture()
        {
            if (renderTexture == null) return;
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        private void OnVideoFinished(VideoPlayer source)
        {
            statusText.text = "À vous de l'imiter !";
            continueButton.interactable = true;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            statusText.text = "Impossible de lire cette vidéo";
            continueButton.interactable = lastClip != null; // the AI can still use the reference audio
            Debug.LogError("Sound Copy video error: " + message);
        }

        private void Replay()
        {
            if (videoPlayer.clip == null) return;
            statusText.text = "Écoute bien…";
            videoPlayer.Stop();
            videoPlayer.Play();
        }

        public void PauseForOverlay()
        {
            resumeAfterPause = videoPlayer != null && videoPlayer.isPlaying;
            if (resumeAfterPause) videoPlayer.Pause();
        }

        public void ResumeAfterOverlay()
        {
            if (resumeAfterPause && videoPlayer != null) videoPlayer.Play();
            resumeAfterPause = false;
        }

        private void Continue() => gameManager?.OnVideoContinue();
        private void Back() => gameManager?.OnVideoBack();
    }
}
