using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SoundCopy.UI
{
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
            continueButton?.onClick.RemoveListener(Continue);
            replayButton?.onClick.RemoveListener(Replay);
            backButton?.onClick.RemoveListener(Back);
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.errorReceived -= OnVideoError;
            }
            ReleaseTexture();
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            GameObject shell = ScreenScaffold.Build(transform, "ÉCOUTE", "Le son du round", "Regarde et mémorise chaque détail.");

            GameObject frame = UIFactory.CreateRect("VideoFrame", shell.transform, new Vector2(0.075f, 0.34f), new Vector2(0.925f, 0.69f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(frame, SoundCopyTheme.DarkPlum, 38f);
            UIFactory.AddNativeOutline(frame, SoundCopyTheme.Ink, 3f);
            GameObject imageObject = UIFactory.CreateRect("VideoImage", frame.transform, new Vector2(0.035f, 0.055f), new Vector2(0.965f, 0.945f), Vector2.zero, Vector2.zero);
            videoImage = imageObject.AddComponent<RawImage>();
            videoImage.color = Color.white;
            videoImage.raycastTarget = false;
            videoPlayer = imageObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;

            statusText = UIFactory.CreateText("Status", shell.transform, "Prêt à lancer le son", 24f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);
            statusText.rectTransform.anchorMin = new Vector2(0.075f, 0.285f);
            statusText.rectTransform.anchorMax = new Vector2(0.925f, 0.33f);
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;

            GameObject replay = UIFactory.CreateRect("ReplayAction", shell.transform, new Vector2(0.075f, 0.195f), new Vector2(0.43f, 0.26f), Vector2.zero, Vector2.zero);
            replayButton = UIFactory.CreateButton("ReplayButton", replay.transform, "Revoir", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 28f, out _, false);
            GameObject next = UIFactory.CreateRect("ContinueAction", shell.transform, new Vector2(0.46f, 0.195f), new Vector2(0.925f, 0.26f), Vector2.zero, Vector2.zero);
            continueButton = UIFactory.CreateButton("ContinueButton", next.transform, "Commencer les imitations", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 28f, out _);
            continueButton.interactable = false;
            GameObject back = UIFactory.CreateRect("BackAction", shell.transform, new Vector2(0.075f, 0.105f), new Vector2(0.43f, 0.16f), Vector2.zero, Vector2.zero);
            backButton = UIFactory.CreateButton("BackButton", back.transform, "<  Retour", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 26f, out _, false);
        }

        public void PlayForCategory(ChallengeCategory category)
        {
            VideoClip clip = ChooseClip(category);
            if (clip == null)
            {
                statusText.text = "Aucune vidéo assignée à cette catégorie";
                continueButton.interactable = false;
                return;
            }
            EnsureTexture();
            lastClip = clip;
            videoPlayer.clip = clip;
            continueButton.interactable = false;
            statusText.text = "Écoute en cours…";
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
            // A square target works for portrait, landscape and square source clips.
            // VideoAspectRatio.FitInside adds letterboxing instead of cropping.
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
            statusText.text = "À vous de l’imiter !";
            continueButton.interactable = true;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            statusText.text = "Impossible de lire cette vidéo";
            Debug.LogError("Sound Copy video error: " + message);
        }

        private void Replay()
        {
            if (videoPlayer.clip == null) return;
            continueButton.interactable = false;
            statusText.text = "Écoute en cours…";
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
