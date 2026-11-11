using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SoundCopy.UI
{
    // One player's imitation: SHAKE THE PHONE (or tap the mic) -> 3-2-1 -> record for the length of the
    // video (live level meter + progress bar) -> listen back, redo once, or validate.
    // Two quick shakes start the take; the 3-2-1 that follows gives the hands time to settle, so the
    // shake itself is never recorded. The mic button always works too. Editor: press R to "shake".
    public sealed class RecordingPanel : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int MeterBars = 28;

        [Header("Shake to record (accelerometer)")]
        [Tooltip("Start recording when the phone is shaken.")]
        [SerializeField] private bool shakeToRecord = true;
        [Tooltip("How hard a shake must be (extra force compared with gravity: 0.6 = 60% more than holding still).")]
        [SerializeField, Range(0.2f, 2f)] private float shakeStrength = 0.6f;
        [Tooltip("Shakes needed within the time below (2 = a quick double shake, so a bump doesn't start it).")]
        [SerializeField, Range(1, 4)] private int shakesNeeded = 2;
        [Tooltip("Seconds in which those shakes must happen.")]
        [SerializeField] private float shakeWindow = 0.8f;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI playerText;
        [SerializeField] private Image micGraphic;
        [SerializeField] private Button micButton;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private GameObject micIcon;
        [SerializeField] private RectTransform[] meterBars = new RectTransform[MeterBars];
        [SerializeField] private Image[] meterGraphics = new Image[MeterBars];
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private Image progressGraphic;
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
        private bool countingDown;
        private bool done;
        private bool cancelRequested;
        private float restLevel;     // accelerometer reading when the phone is held still
        private bool inShake;
        private int shakeCount;
        private float firstShakeTime = -10f;
        private bool liftedTake;     // this take was started by lifting the phone (not by the button)

        bool RaiseAvailable => shakeToRecord && (PhoneMotion.HasAccelerometer || Application.isEditor);

        public void Initialize(GameManager manager) => gameManager = manager;

        private void OnEnable() => PhoneMotion.Warm();

        // Watches the phone: a quick double shake starts the take (a take is never cancelled by moving the phone).
        private void Update()
        {
            if (!RaiseAvailable || done || busy || GameUI.MiniGameBridge.IsPaused) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) { BeginRecording(true); return; }

            var accelerometer = UnityEngine.InputSystem.Accelerometer.current;
            if (accelerometer == null) return;
            if (!accelerometer.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(accelerometer);
            float magnitude = accelerometer.acceleration.ReadValue().magnitude;
            if (magnitude < 1e-4f) return;

            // "Holding still" level, learned continuously (works whether the phone reports g or m/s2).
            if (restLevel <= 0f) restLevel = magnitude;
            float force = Mathf.Abs(magnitude - restLevel) / restLevel;
            if (force < shakeStrength * 0.5f)
                restLevel = Mathf.Lerp(restLevel, magnitude, 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.8f));

            float now = Time.unscaledTime;
            if (!inShake && force >= shakeStrength)
            {
                inShake = true;
                if (now - firstShakeTime > shakeWindow) { firstShakeTime = now; shakeCount = 0; }
                shakeCount++;
                if (shakeCount >= shakesNeeded)
                {
                    shakeCount = 0;
                    firstShakeTime = -10f;
                    BeginRecording(true);
                }
            }
            else if (inShake && force < shakeStrength * 0.4f) inShake = false;
        }

        private void Awake()
        {
            micButton?.onClick.AddListener(() => BeginRecording(false));
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
            busy = recording = redoUsed = done = countingDown = cancelRequested = false;
            // Shake detection starts fresh for each player.
            restLevel = 0f;
            shakeCount = 0;
            liftedTake = false;
            recordingDuration = gameManager != null ? Mathf.Max(1f, gameManager.CurrentChallengeDuration) : 4f;
            Color color = MobileUI.PlayerColor(index);

            if (titleText != null) titleText.text = "IMITATION";
            if (playerText != null) { playerText.text = "YOUR TURN, " + playerName.ToUpperInvariant() + " !"; playerText.color = color; }
            if (micGraphic != null) micGraphic.color = color;
            if (progressGraphic != null) progressGraphic.color = color;
            for (int i = 0; i < meterGraphics.Length; i++) if (meterGraphics[i] != null) meterGraphics[i].color = color;
            bool last = gameManager != null && gameManager.Recorded(3 - index);
            if (validateLabel != null) validateLabel.text = last ? "LET THE AI JUDGE  >" : "NEXT PLAYER  >";
            ResetMeter();
            SetProgress(0f);
            ShowReady();
        }

        void ShowReady()
        {
            if (micIcon != null) micIcon.SetActive(true);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            done = false;
            if (timerText != null) timerText.text = recordingDuration.ToString("0.0") + " s";
            if (statusText != null)
                statusText.text = RaiseAvailable
                    ? "Shake the phone to start recording\n(or tap the mic)"
                    : "Tap the mic when you're ready";
            if (micButton != null) micButton.interactable = true;   // the mic button always works
            shakeCount = 0;
            liftedTake = false;
            if (afterRow != null) afterRow.SetActive(false);
            if (validateButton != null) validateButton.gameObject.SetActive(false);
            // Inside Pocket Jungle the pause menu is the way out (no back buttons).
            if (backButton != null) backButton.gameObject.SetActive(!GameUI.MiniGameBridge.IsAvailable);
        }

        // Pocket Jungle's pause menu opened: stop the countdown / recording; the player starts the take again.
        public void CancelTakeForPause()
        {
            if (!busy) return;
            StopAllCoroutines();
            StopMicrophone();
            busy = recording = countingDown = cancelRequested = false;
            if (countdownText != null) countdownText.rectTransform.localScale = Vector3.one;
            ResetMeter();
            SetProgress(0f);
            ShowReady();
            if (statusText != null) statusText.text = "Paused: shake the phone or tap the mic to record again";
        }

        void ShowDone()
        {
            done = true;
            if (micIcon != null) micIcon.SetActive(true);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (timerText != null) timerText.text = "OK!";
            if (statusText != null) statusText.text = "Imitation saved. Play it back or confirm.";
            if (micButton != null) micButton.interactable = false;
            if (afterRow != null) afterRow.SetActive(true);
            if (redoButton != null) redoButton.interactable = !redoUsed;
            if (redoLabel != null) redoLabel.text = redoUsed ? "ALREADY REDONE" : "REDO";
            if (validateButton != null) validateButton.gameObject.SetActive(true);
            if (backButton != null) backButton.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- recording

        private void BeginRecording(bool lifted)
        {
            if (busy) return;
            liftedTake = lifted;
            StartCoroutine(RecordRoutine());
        }

        private IEnumerator RecordRoutine()
        {
            busy = true;
            micButton.interactable = false;
            if (backButton != null) backButton.gameObject.SetActive(false);

            statusText.text = "Asking for the mic…";
            bool allowed = false;
            yield return RequestMicrophone(granted => allowed = granted);
            if (!allowed)
            {
                statusText.text = "Mic access denied (Settings > Apps)";
                busy = false;
                micButton.interactable = true;
                yield break;
            }
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                statusText.text = "No microphone found";
                busy = false;
                micButton.interactable = true;
                yield break;
            }

            // 3 - 2 - 1 (felt as buzzes too, since the screen is near your face). Lowering the phone cancels.
            micIcon.SetActive(false);
            countdownText.gameObject.SetActive(true);
            statusText.text = "Get ready…";
            countingDown = true;
            cancelRequested = false;
            for (int n = 3; n >= 1; n--)
            {
                countdownText.text = n.ToString();
                Haptics.Pulse(40, 120);
                float t = 0f;
                while (t < 1f)
                {
                    if (cancelRequested)
                    {
                        countingDown = false;
                        countdownText.rectTransform.localScale = Vector3.one;
                        busy = false;
                        ShowReady();
                        statusText.text = "Cancelled: shake the phone again (or tap the mic)";
                        yield break;
                    }
                    t += Time.unscaledDeltaTime;
                    float pop = 1f + 0.25f * Mathf.Max(0f, 1f - t * 4f);
                    countdownText.rectTransform.localScale = new Vector3(pop, pop, 1f);
                    yield return null;
                }
            }
            countingDown = false;
            countdownText.rectTransform.localScale = Vector3.one;
            countdownText.text = "GO !";
            Haptics.Pulse(120, 220);

            int bufferSeconds = Mathf.CeilToInt(recordingDuration) + 1;
            microphoneClip = Microphone.Start(null, false, bufferSeconds, SampleRate);
            if (microphoneClip == null)
            {
                statusText.text = "Couldn't start the mic";
                busy = false;
                ShowReady();
                yield break;
            }
            recording = true;
            statusText.text = "RECORDING…  COPY THE SOUND!";
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
                statusText.text = "Recording failed, try again";
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
            ShowReady(); // shake the phone again (or tap the mic) for the new take
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

            // Left: big round mic button.
            const float micSize = 520f;
            var mic = MobileUI.TopLeft(page, "MicButton", 150f, 190f, micSize, micSize);
            micGraphic = UIFactory.AddRounded(mic.gameObject, SoundCopyTheme.Lime, micSize * 0.5f, true);
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

            // Right: player, live level meter, progress, time and status.
            var side = UIFactory.CreateRect("Side", page, Vector2.zero, Vector2.one, new Vector2(150f + micSize + 100f, 0f), new Vector2(-MobileUI.Side, 0f));
            var player = MobileUI.Row(side.transform, "Player", 170f, 90f, 0f);
            playerText = MobileUI.Text(player, "Text", "YOUR TURN!", 64f, SoundCopyTheme.Lime, TextAlignmentOptions.MidlineLeft, true);
            playerText.enableAutoSizing = true;
            playerText.fontSizeMin = 40f;
            playerText.fontSizeMax = 64f;

            var meter = MobileUI.Row(side.transform, "LevelMeter", 290f, 200f, 0f);
            for (int i = 0; i < MeterBars; i++)
            {
                float w = 1f / MeterBars;
                var bar = UIFactory.CreateRect("Bar" + i, meter, new Vector2(i * w + w * 0.2f, 0f), new Vector2(i * w + w * 0.8f, 1f), Vector2.zero, Vector2.zero);
                ((RectTransform)bar.transform).pivot = new Vector2(0.5f, 0.5f);
                meterGraphics[i] = UIFactory.AddRounded(bar, SoundCopyTheme.Lime, 8f);
                meterBars[i] = (RectTransform)bar.transform;
                meterBars[i].localScale = new Vector3(1f, 0.08f, 1f);
            }

            var track = MobileUI.Row(side.transform, "Progress", 520f, 36f, 0f);
            if (SoundCopySkin.BarTrack != null) UIFactory.AddSprite(track.gameObject, SoundCopySkin.BarTrack, Color.white);
            else UIFactory.AddRounded(track.gameObject, new Color(1f, 1f, 1f, 0.15f), 18f);
            var fill = UIFactory.CreateRect("Fill", track, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            progressGraphic = UIFactory.AddRounded(fill, SoundCopyTheme.Lime, 18f);
            progressFill = (RectTransform)fill.transform;

            var timer = MobileUI.Row(side.transform, "Timer", 575f, 110f, 0f);
            timerText = MobileUI.Text(timer, "Text", "4.0 s", 92f, SoundCopyTheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            var status = MobileUI.Row(side.transform, "Status", 690f, 80f, 0f);
            statusText = MobileUI.Text(status, "Text", "Tap the mic when you're ready", 38f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.TopLeft);
            statusText.textWrappingMode = TextWrappingModes.Normal;

            // Bottom: listen back / redo (left of the main button), validate (bottom-right).
            var after = UIFactory.CreateRect("AfterActions", page, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            var afterRt = (RectTransform)after.transform;
            afterRt.pivot = new Vector2(0.5f, 0f);
            afterRt.offsetMin = new Vector2(150f + micSize + 100f, MobileUI.BottomMargin);
            afterRt.offsetMax = new Vector2(-(MobileUI.Side + MobileUI.ButtonWidth + 30f), MobileUI.BottomMargin + MobileUI.ButtonHeight);
            afterRow = after;
            listenButton = MobileUI.BigButton(MobileUI.Cell(afterRt, "Listen", 0, 2, 24f), "ListenButton", "PLAY BACK", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 40f);
            redoButton = MobileUI.BigButton(MobileUI.Cell(afterRt, "Redo", 1, 2, 24f), "RedoButton", "REDO", SoundCopyTheme.Cream, SoundCopyTheme.Ink, out redoLabel, 40f);
            afterRow.SetActive(false);

            var validate = MobileUI.BottomRight(page, "ValidateAction");
            validateButton = MobileUI.BigButton(validate, "ValidateButton", "NEXT PLAYER  >", SoundCopyTheme.Lime, SoundCopyTheme.Ink, out validateLabel);
            validateButton.gameObject.SetActive(false);
        }
    }
}
