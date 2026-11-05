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
            backButton = MobileUI.TopBar(page, "LISTEN TO THE SOUND", true, SoundCopyTheme.Cream, out _, out _);

            // Left: square frame as tall as the screen allows (the video is letterboxed inside).
            const float frameTop = 160f, frameBottom = 40f, frameSize = 1080f - frameTop - frameBottom;
            var frame = MobileUI.TopLeft(page, "VideoFrame", MobileUI.Side, frameTop, frameSize, frameSize);
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

            // Right: status, tip and the two actions.
            float rightStart = MobileUI.Side + frameSize + 64f;
            var side = UIFactory.CreateRect("Side", page, Vector2.zero, Vector2.one, new Vector2(rightStart, 0f), Vector2.zero);
            var status = MobileUI.Row(side.transform, "Status", 260f, 110f, 0f);
            statusText = MobileUI.Text(status, "Text", "Ready to play the sound", 64f, SoundCopyTheme.Lime, TextAlignmentOptions.MidlineLeft, true);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 40f;
            statusText.fontSizeMax = 64f;
            var hint = MobileUI.Row(side.transform, "Hint", 390f, 130f, 0f);
            var hintText = MobileUI.Text(hint, "Text", "Remember the rhythm and the voice:\nyou'll have to copy it!", 36f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.TopLeft);
            hintText.textWrappingMode = TextWrappingModes.Normal;

            var replay = MobileUI.BottomRight(page, "ReplayAction", MobileUI.ButtonWidth, 120f, MobileUI.Side, MobileUI.BottomMargin + MobileUI.ButtonHeight + 28f);
            replayButton = MobileUI.BigButton(replay, "ReplayButton", "REPLAY", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 44f);
            var next = MobileUI.BottomRight(page, "ContinueAction");
            continueButton = MobileUI.BigButton(next, "ContinueButton", "LET'S GO  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink);
            continueButton.interactable = false;
        }

        public void PlayForCategory(ChallengeCategory category)
        {
            VideoClip clip = ChooseClip(category);
            if (clip == null)
            {
                statusText.text = "No video for this category";
                continueButton.interactable = false;
                return;
            }
            EnsureTexture();
            lastClip = clip;
            videoPlayer.clip = clip;
            continueButton.interactable = false;
            statusText.text = "Listen carefully…";
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
            statusText.text = "Your turn to copy it!";
            continueButton.interactable = true;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            statusText.text = "Can't play this video";
            continueButton.interactable = lastClip != null; // the AI can still use the reference audio
            Debug.LogError("Sound Copy video error: " + message);
        }

        private void Replay()
        {
            if (videoPlayer.clip == null) return;
            statusText.text = "Listen carefully…";
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
