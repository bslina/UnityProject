using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SoundCopy.UI
{
    // One player's imitation: tap the mic -> 3-2-1 -> record for the length of the video
    // (live level meter + progress bar) -> listen back, redo once, or validate.
    public sealed class RecordingPanel : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int MeterBars = 28;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI playerText;
        [SerializeField] private RoundedRectGraphic micGraphic;
        [SerializeField] private Button micButton;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private GameObject micIcon;
        [SerializeField] private RectTransform[] meterBars = new RectTransform[MeterBars];
        [SerializeField] private RoundedRectGraphic[] meterGraphics = new RoundedRectGraphic[MeterBars];
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private RoundedRectGraphic progressGraphic;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject afterRow;
        [SerializeField] private Button listenButton;
        [SerializeField] private Button redoButton;
        [SerializeField] private TextMeshProUGUI redoLabel;
        [SerializeField] private Button validateButton;
        [SerializeField] private TextMeshProUGUI validateLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private AudioSource playback;

        private int playerIndex = 1;
        private AudioClip microphoneClip;
        private bool busy;           // countdown or recording in progress
        private bool recording;
        private bool redoUsed;
        private float recordingDuration = 4f;
        private readonly float[] levels = new float[MeterBars];
        private float[] levelBuffer = new float[1024];
        private float levelTimer;
        private float smoothLevel;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            micButton?.onClick.AddListener(BeginRecording);
            listenButton?.onClick.AddListener(Listen);
            redoButton?.onClick.AddListener(Redo);
            validateButton?.onClick.AddListener(Validate);
            backButton?.onClick.AddListener(Back);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            StopMicrophone();
            busy = false;
            if (playback != null) playback.Stop();
        }

        public void Prepare(int index, string playerName)
        {
            StopAllCoroutines();
            StopMicrophone();
            playerIndex = index;
            microphoneClip = null;
            busy = recording = redoUsed = false;
            recordingDuration = gameManager != null ? Mathf.Max(1f, gameManager.CurrentChallengeDuration) : 4f;
            Color color = MobileUI.PlayerColor(index);

            if (titleText != null) titleText.text = "IMITATION";
            if (playerText != null) { playerText.text = "À TOI, " + playerName.ToUpperInvariant() + " !"; playerText.color = color; }
            if (micGraphic != null) micGraphic.color = color;
            if (progressGraphic != null) progressGraphic.color = color;
            for (int i = 0; i < meterGraphics.Length; i++) if (meterGraphics[i] != null) meterGraphics[i].color = color;
            if (validateLabel != null) validateLabel.text = index == 1 ? "JOUEUR SUIVANT  >" : "ANALYSER AVEC L'IA  >";
            ResetMeter();
            SetProgress(0f);
            ShowReady();
        }

        void ShowReady()
        {
            if (micIcon != null) micIcon.SetActive(true);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (timerText != null) timerText.text = recordingDuration.ToString("0.0") + " s";
            if (statusText != null) statusText.text = "Appuie sur le micro quand tu es prêt";
            if (micButton != null) micButton.interactable = true;
            if (afterRow != null) afterRow.SetActive(false);
            if (validateButton != null) validateButton.gameObject.SetActive(false);
            if (backButton != null) backButton.gameObject.SetActive(true);
        }

        void ShowDone()
        {
            if (micIcon != null) micIcon.SetActive(true);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (timerText != null) timerText.text = "OK !";
            if (statusText != null) statusText.text = "Imitation enregistrée. Écoute-la ou valide.";
            if (micButton != null) micButton.interactable = false;
            if (afterRow != null) afterRow.SetActive(true);
            if (redoButton != null) redoButton.interactable = !redoUsed;
            if (redoLabel != null) redoLabel.text = redoUsed ? "DÉJÀ REFAIT" : "REFAIRE";
            if (validateButton != null) validateButton.gameObject.SetActive(true);
            if (backButton != null) backButton.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- recording

        private void BeginRecording()
        {
            if (!busy) StartCoroutine(RecordRoutine());
        }

        private IEnumerator RecordRoutine()
        {
            busy = true;
            micButton.interactable = false;
            if (backButton != null) backButton.gameObject.SetActive(false);

            statusText.text = "Autorisation du micro…";
            bool allowed = false;
            yield return RequestMicrophone(granted => allowed = granted);
            if (!allowed)
            {
                statusText.text = "Accès au micro refusé (Paramètres > Applis)";
                busy = false;
                micButton.interactable = true;
                yield break;
            }
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                statusText.text = "Aucun micro détecté";
                busy = false;
                micButton.interactable = true;
                yield break;
            }

            // 3 - 2 - 1
            micIcon.SetActive(false);
            countdownText.gameObject.SetActive(true);
            statusText.text = "Prépare-toi…";
            for (int n = 3; n >= 1; n--)
            {
                countdownText.text = n.ToString();
                Haptics.Pulse(40, 120);
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime;
                    float pop = 1f + 0.25f * Mathf.Max(0f, 1f - t * 4f);
                    countdownText.rectTransform.localScale = new Vector3(pop, pop, 1f);
                    yield return null;
                }
            }
            countdownText.rectTransform.localScale = Vector3.one;
            countdownText.text = "GO !";
            Haptics.Pulse(120, 220);

            int bufferSeconds = Mathf.CeilToInt(recordingDuration) + 1;
            microphoneClip = Microphone.Start(null, false, bufferSeconds, SampleRate);
            if (microphoneClip == null)
            {
                statusText.text = "Impossible de démarrer le micro";
                busy = false;
                ShowReady();
                yield break;
            }
            recording = true;
            statusText.text = "ENREGISTREMENT…  IMITE LE SON !";
            float elapsed = 0f;
            while (elapsed < recordingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 0.5f && countdownText.gameObject.activeSelf)
                {
                    countdownText.gameObject.SetActive(false);
                    micIcon.SetActive(true);
                }
                timerText.text = Mathf.Max(0f, recordingDuration - elapsed).ToString("0.0") + " s";
                SetProgress(elapsed / recordingDuration);
                UpdateMeter();
                yield return null;
            }

            int position = Microphone.GetPosition(null);
            Microphone.End(null);
            recording = false;
            int samples = Mathf.Clamp(position, 1, microphoneClip.samples);
            float[] data = new float[samples * microphoneClip.channels];
            microphoneClip.GetData(data, 0);
            AudioClip trimmed = AudioClip.Create($"Player{playerIndex}_Recording", samples, microphoneClip.channels, microphoneClip.frequency, false);
            trimmed.SetData(data, 0);
            microphoneClip = trimmed;
            SetProgress(1f);
            busy = false;

            if (gameManager != null && gameManager.StoreRecording(playerIndex, microphoneClip)) ShowDone();
            else
            {
                statusText.text = "Enregistrement impossible, réessaie";
                ShowReady();
            }
        }

        IEnumerator RequestMicrophone(System.Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                bool answered = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => answered = true;
                callbacks.PermissionDenied += _ => answered = true;
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                float giveUp = Time.realtimeSinceStartup + 60f;
                while (!answered && Time.realtimeSinceStartup < giveUp) yield return null;
            }
            done(Permission.HasUserAuthorizedPermission(Permission.Microphone));
#else
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            done(Application.HasUserAuthorization(UserAuthorization.Microphone));
#endif
        }

        // Live level: RMS of the last ~23 ms of microphone input, scrolled across the bars.
        void UpdateMeter()
        {
            if (microphoneClip == null) return;
            int pos = Microphone.GetPosition(null);
            float rms = 0f;
            if (pos >= levelBuffer.Length)
            {
                microphoneClip.GetData(levelBuffer, pos - levelBuffer.Length);
                float sum = 0f;
                for (int i = 0; i < levelBuffer.Length; i++) sum += levelBuffer[i] * levelBuffer[i];
                rms = Mathf.Sqrt(sum / levelBuffer.Length);
            }
            smoothLevel = Mathf.Lerp(smoothLevel, Mathf.Clamp01(rms * 9f), 0.5f);

            levelTimer += Time.unscaledDeltaTime;
            if (levelTimer < 0.05f) return;
            levelTimer = 0f;
            for (int i = 0; i < levels.Length - 1; i++) levels[i] = levels[i + 1];
            levels[levels.Length - 1] = smoothLevel;
            ApplyMeter();
        }

        void ResetMeter()
        {
            for (int i = 0; i < levels.Length; i++) levels[i] = 0f;
            smoothLevel = 0f;
            ApplyMeter();
        }

        void ApplyMeter()
        {
            for (int i = 0; i < meterBars.Length; i++)
                if (meterBars[i] != null) meterBars[i].localScale = new Vector3(1f, Mathf.Lerp(0.08f, 1f, levels[i]), 1f);
        }

        void SetProgress(float t)
        {
            if (progressFill != null) progressFill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
        }

        private void StopMicrophone()
        {
            if (recording && Microphone.IsRecording(null)) Microphone.End(null);
            recording = false;
        }

        // ---------------------------------------------------------------- after recording

        void Listen()
        {
            if (playback == null || microphoneClip == null) return;
            playback.Stop();
            playback.clip = microphoneClip;
            playback.volume = GameUI.GameSettings.SfxVolume;
            playback.Play();
        }

        void Redo()
        {
            if (redoUsed) return;
            redoUsed = true;
            if (playback != null) playback.Stop();
            ResetMeter();
            SetProgress(0f);
            ShowReady();
            BeginRecording();
        }

        void Validate()
        {
            if (playback != null) playback.Stop();
            gameManager?.OnRecordingDone(playerIndex);
        }

        private void Back() => gameManager?.OnRecordingBack();

        // ---------------------------------------------------------------- build (scene builder)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            backButton = MobileUI.TopBar(page, "IMITATION", true, SoundCopyTheme.Cream, out titleText, out _);
            playback = gameObject.GetComponent<AudioSource>();
            if (playback == null) playback = gameObject.AddComponent<AudioSource>();
            playback.playOnAwake = false;

            var player = MobileUI.Row(page, "Player", 190f, 90f);
            playerText = MobileUI.Text(player, "Text", "À TOI !", 64f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, true);
            playerText.enableAutoSizing = true;
            playerText.fontSizeMin = 40f;
            playerText.fontSizeMax = 64f;

            // Big round mic button.
            var mic = MobileUI.Row(page, "MicButton", 310f, 540f, 270f);
            micGraphic = UIFactory.AddRounded(mic.gameObject, SoundCopyTheme.Lime, 270f, true);
            UIFactory.AddNativeOutline(mic.gameObject, SoundCopyTheme.Cream, 6f);
            var micShadow = mic.gameObject.AddComponent<Shadow>();
            micShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            micShadow.effectDistance = new Vector2(0f, -14f);
            micButton = mic.gameObject.AddComponent<Button>();
            micButton.targetGraphic = micGraphic;
            micIcon = UIFactory.CreateRect("MicIcon", mic, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var head = UIFactory.CreateRect("Head", micIcon.transform, new Vector2(0.39f, 0.40f), new Vector2(0.61f, 0.78f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(head, SoundCopyTheme.Ink, 60f);
            var cradle = UIFactory.CreateRect("Cradle", micIcon.transform, new Vector2(0.33f, 0.34f), new Vector2(0.67f, 0.40f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(cradle, SoundCopyTheme.Ink, 14f);
            var stem = UIFactory.CreateRect("Stem", micIcon.transform, new Vector2(0.48f, 0.24f), new Vector2(0.52f, 0.36f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(stem, SoundCopyTheme.Ink, 8f);
            var foot = UIFactory.CreateRect("Foot", micIcon.transform, new Vector2(0.38f, 0.20f), new Vector2(0.62f, 0.25f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(foot, SoundCopyTheme.Ink, 10f);
            countdownText = MobileUI.Text(mic, "Countdown", "3", 260f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            countdownText.gameObject.SetActive(false);

            // Live level meter.
            var meter = MobileUI.Row(page, "LevelMeter", 900f, 200f);
            for (int i = 0; i < MeterBars; i++)
            {
                float w = 1f / MeterBars;
                var bar = UIFactory.CreateRect("Bar" + i, meter, new Vector2(i * w + w * 0.2f, 0f), new Vector2(i * w + w * 0.8f, 1f), Vector2.zero, Vector2.zero);
                ((RectTransform)bar.transform).pivot = new Vector2(0.5f, 0.5f);
                meterGraphics[i] = UIFactory.AddRounded(bar, SoundCopyTheme.Lime, 8f);
                meterBars[i] = (RectTransform)bar.transform;
                meterBars[i].localScale = new Vector3(1f, 0.08f, 1f);
            }

            // Progress bar + time left.
            var track = MobileUI.Row(page, "Progress", 1140f, 40f);
            UIFactory.AddRounded(track.gameObject, new Color(1f, 1f, 1f, 0.15f), 20f);
            var fill = UIFactory.CreateRect("Fill", track, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            progressGraphic = UIFactory.AddRounded(fill, SoundCopyTheme.Lime, 20f);
            progressFill = (RectTransform)fill.transform;

            var timer = MobileUI.Row(page, "Timer", 1200f, 120f);
            timerText = MobileUI.Text(timer, "Text", "4.0 s", 96f, SoundCopyTheme.Cream, TextAlignmentOptions.Center, true);
            var status = MobileUI.Row(page, "Status", 1330f, 80f);
            statusText = MobileUI.Text(status, "Text", "Appuie sur le micro quand tu es prêt", 40f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            statusText.textWrappingMode = TextWrappingModes.Normal;

            // After recording: listen back / redo, then validate.
            var after = MobileUI.BottomRow(page, "AfterActions", MobileUI.BottomMargin + MobileUI.ButtonHeight + 30f, 140f);
            afterRow = after.gameObject;
            listenButton = MobileUI.BigButton(MobileUI.Cell(after, "Listen", 0, 2, 30f), "ListenButton", "RÉÉCOUTER", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 44f);
            redoButton = MobileUI.BigButton(MobileUI.Cell(after, "Redo", 1, 2, 30f), "RedoButton", "REFAIRE", SoundCopyTheme.Cream, SoundCopyTheme.Ink, out redoLabel, 44f);
            afterRow.SetActive(false);

            var validate = MobileUI.BottomRow(page, "ValidateAction", MobileUI.BottomMargin, MobileUI.ButtonHeight);
            validateButton = MobileUI.BigButton(validate, "ValidateButton", "JOUEUR SUIVANT  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink, out validateLabel);
            validateButton.gameObject.SetActive(false);
        }
    }
}
