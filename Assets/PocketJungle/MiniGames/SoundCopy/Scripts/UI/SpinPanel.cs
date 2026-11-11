using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    // "Spin the phone" before each round (gyroscope).
    // The phone lies flat between the players: P1 on the bottom side of the screen, P2 on the top side.
    // Someone spins it like a bottle. When it stops, the side the TOP of the screen points to goes first.
    // A fast spin makes the round a x2 round. No gyroscope (or in the Editor): press SPACE or "Spin for me".
    public sealed class SpinPanel : MonoBehaviour
    {
        enum Stage { WaitFlat, Ready, Spinning, Done }

        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI detailText;
        [SerializeField] private TextMeshProUGUI topSideText;
        [SerializeField] private TextMeshProUGUI bottomSideText;
        [SerializeField] private RectTransform speedFill;
        [SerializeField] private Image speedFillImage;
        [SerializeField] private RectTransform fastMarker;
        [SerializeField] private Button mainButton;
        [SerializeField] private TextMeshProUGUI mainLabel;
        [SerializeField] private Button backButton;

        [Header("Wheel (Tools > Sound Copy > Pocket Jungle Screens)")]
        [Tooltip("Turns with the phone's spin (or by itself with the button). Top half = player 2, bottom = player 1; the pointer is at the top.")]
        [SerializeField] private RectTransform wheel;
        [SerializeField] private Image wheelHalf1;
        [SerializeField] private Image wheelHalf2;
        [SerializeField] private TextMeshProUGUI wheelName1;
        [SerializeField] private TextMeshProUGUI wheelName2;
        [Tooltip("Seconds the wheel spins when the button spins it.")]
        [SerializeField] private float buttonSpinTime = 2.6f;

        [Header("Spin (turns per second)")]
        [Tooltip("Rotation speed that counts as 'the phone is spinning'.")]
        [SerializeField] private float startSpeed = 0.6f;
        [Tooltip("Below this the phone counts as stopped.")]
        [SerializeField] private float stopSpeed = 0.15f;
        [Tooltip("A spin must reach at least this top speed, otherwise: spin harder.")]
        [SerializeField] private float minimumPeak = 0.8f;
        [Tooltip("Top speed needed for a x2 round.")]
        [SerializeField] private float fastPeak = 2.2f;

        Stage stage;
        float angle;        // degrees turned since the spin started (anticlockwise +)
        float peak;
        float slowSince = -1f;
        int firstPlayer = 1;
        bool doubled;
        bool autoSpin;      // the button is spinning the wheel (the phone itself isn't moving)

        bool HasGyro => PhoneMotion.HasGyroscope;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            mainButton?.onClick.AddListener(OnMainButton);
            backButton?.onClick.AddListener(() => gameManager?.OnSpinBack());
        }

        public void Begin()
        {
            PhoneMotion.Warm();
            StopAllCoroutines();
            autoSpin = false;
            if (mainButton != null) mainButton.interactable = true;
            angle = peak = 0f;
            SetWheel(0f);
            if (gameManager != null)
            {
                if (wheelName1 != null) wheelName1.text = gameManager.Player1Name.ToUpperInvariant();
                if (wheelName2 != null) wheelName2.text = gameManager.Player2Name.ToUpperInvariant();
            }
            if (wheelHalf1 != null) wheelHalf1.color = MobileUI.PlayerColor(1);
            if (wheelHalf2 != null) wheelHalf2.color = MobileUI.PlayerColor(2);
            slowSince = -1f;
            if (gameManager != null)
            {
                if (topSideText != null) topSideText.text = "^  " + gameManager.Player2Name.ToUpperInvariant() + "'S SIDE  ^";
                if (bottomSideText != null) bottomSideText.text = "v  " + gameManager.Player1Name.ToUpperInvariant() + "'S SIDE  v";
            }
            SetStage(PhoneMotion.IsFlat || !PhoneMotion.HasAccelerometer ? Stage.Ready : Stage.WaitFlat);
            SetSpeedBar(0f);
        }

        void SetStage(Stage next)
        {
            stage = next;
            switch (next)
            {
                case Stage.WaitFlat:
                    Set("PUT THE PHONE FLAT", "Lay it on the table between you and spin it like a bottle\nto see who goes first, or tap SPIN THE WHEEL.");
                    SetMain("SPIN THE WHEEL");
                    break;
                case Stage.Ready:
                    Set(HasGyro ? "SPIN THE PHONE!" : "SPIN THE WHEEL!", HasGyro
                        ? "Spin the phone like a bottle to see who goes first,\nor tap SPIN THE WHEEL. A fast spin = x2 round!"
                        : "Tap SPIN THE WHEEL to see who goes first.");
                    SetMain("SPIN THE WHEEL");
                    break;
                case Stage.Spinning:
                    Set("SPINNING…", "");
                    break;
                case Stage.Done:
                    string name = gameManager != null ? gameManager.PlayerName(firstPlayer).ToUpperInvariant() : "P" + firstPlayer;
                    Set(name + " GOES FIRST!", doubled ? "<color=#FFF1B8>FAST SPIN: x2 ROUND!</color>" : "Normal round.");
                    SetMain("LISTEN TO THE SOUND  >");
                    Haptics.Pulse(doubled ? 300 : 150, 230);
                    GameUI.GameAudio.Play(doubled ? "spin_x2" : "spin_result");
                    break;
            }
        }

        void Set(string status, string detail)
        {
            if (statusText != null) statusText.text = status;
            if (detailText != null) detailText.text = detail;
        }

        void SetMain(string label)
        {
            if (mainLabel != null) mainLabel.text = label;
        }

        private void Update()
        {
            if (GameUI.MiniGameBridge.IsPaused) return;   // the phone moved in the pause menu: not a spin
            if (autoSpin) return;                          // the wheel is spinning by itself
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (stage != Stage.Done && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                FakeSpin();
                return;
            }
            if (!HasGyro || stage == Stage.Done) return;

            float speed = PhoneMotion.SpinSpeed;
            float abs = Mathf.Abs(speed);

            switch (stage)
            {
                case Stage.WaitFlat:
                    if (PhoneMotion.IsFlat) SetStage(Stage.Ready);
                    break;

                case Stage.Ready:
                    if (!PhoneMotion.IsFlat && PhoneMotion.HasAccelerometer) { SetStage(Stage.WaitFlat); break; }
                    if (abs >= startSpeed)
                    {
                        angle = peak = 0f;
                        slowSince = -1f;
                        SetStage(Stage.Spinning);
                    }
                    break;

                case Stage.Spinning:
                    angle += speed * 360f * Time.unscaledDeltaTime;
                    SetWheel(angle);   // the wheel turns with the phone
                    peak = Mathf.Max(peak, abs);
                    SetSpeedBar(abs);
                    if (detailText != null) detailText.text = abs.ToString("0.0") + " turns / s";
                    if (abs < stopSpeed)
                    {
                        if (slowSince < 0f) slowSince = Time.unscaledTime;
                        if (Time.unscaledTime - slowSince > 0.5f) FinishSpin();
                    }
                    else slowSince = -1f;
                    break;
            }
        }

        void FinishSpin()
        {
            SetSpeedBar(peak);
            if (peak < minimumPeak)
            {
                SetStage(Stage.Ready);
                Set("SPIN HARDER!", "That was too slow to count. Try again.");
                return;
            }
            // The top of the screen started facing P2. After turning by `angle`, it points to P2's side
            // if it turned less than a quarter turn either way (mod 360), otherwise to P1's side.
            float a = Mathf.Repeat(angle, 360f);
            firstPlayer = Mathf.Cos(a * Mathf.Deg2Rad) >= 0f ? 2 : 1;
            doubled = peak >= fastPeak;
            SetStage(Stage.Done);
        }

        // No gyroscope, Editor (SPACE), or the SPIN THE WHEEL button: the wheel spins by itself and slows down.
        void FakeSpin()
        {
            if (stage == Stage.Spinning || stage == Stage.Done) return;
            peak = Random.Range(minimumPeak, fastPeak * 1.25f);
            float target = Random.Range(360f * 3f, 360f * 6f);
            if (wheel == null) { angle = target; FinishSpin(); return; }
            StartCoroutine(SpinWheel(target));
        }

        System.Collections.IEnumerator SpinWheel(float target)
        {
            autoSpin = true;
            SetStage(Stage.Spinning);
            if (mainButton != null) mainButton.interactable = false;
            float t = 0f, from = angle;
            while (t < buttonSpinTime)
            {
                if (!GameUI.MiniGameBridge.IsPaused) t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / buttonSpinTime);
                float eased = 1f - Mathf.Pow(1f - x, 3f);          // fast, then slows down like a real wheel
                angle = Mathf.Lerp(from, target, eased);
                SetWheel(angle);
                SetSpeedBar(peak * (1f - x));
                yield return null;
            }
            angle = target;
            autoSpin = false;
            if (mainButton != null) mainButton.interactable = true;
            FinishSpin();
        }

        void SetWheel(float degrees)
        {
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        void OnMainButton()
        {
            if (stage == Stage.Done) gameManager?.OnSpinDone(firstPlayer, doubled);
            else FakeSpin();
        }

        void SetSpeedBar(float speed)
        {
            float t = Mathf.Clamp01(speed / (fastPeak * 1.4f));
            if (speedFill != null) speedFill.anchorMax = new Vector2(t, 1f);
            if (speedFillImage != null) speedFillImage.color = speed >= fastPeak ? SoundCopyTheme.PaleBonus : SoundCopyTheme.Lime;
        }

        // ---------------------------------------------------------------- build (scene builder / Add Spin Screen)

        public void BuildUI()
        {
            UIFactory.ClearChildren(transform);
            var page = MobileUI.Page(transform, SoundCopyTheme.DarkPlum);
            backButton = MobileUI.TopBar(page, "SPIN THE PHONE", true, SoundCopyTheme.Cream, out _, out _);

            // Left: a phone lying flat, top side = P2, bottom side = P1.
            var phone = MobileUI.TopLeft(page, "PhoneIcon", 120f, 290f, 600f, 340f);
            MobileUI.Card(phone.gameObject, SoundCopyTheme.Ink, 56f, false);
            var screen = UIFactory.CreateRect("Screen", phone, Vector2.zero, Vector2.one, new Vector2(70f, 24f), new Vector2(-70f, -24f));
            UIFactory.AddRounded(screen, SoundCopyTheme.Cream, 28f);
            MobileUI.Text(screen.transform, "Arrow", "^", 200f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, true);
            var top = MobileUI.TopLeft(page, "TopSide", 60f, 200f, 720f, 70f);
            topSideText = MobileUI.Text(top, "Text", "^  P2'S SIDE  ^", 40f, SoundCopyTheme.Coral, TextAlignmentOptions.Center, true);
            var bottom = MobileUI.TopLeft(page, "BottomSide", 60f, 650f, 720f, 70f);
            bottomSideText = MobileUI.Text(bottom, "Text", "v  P1'S SIDE  v", 40f, SoundCopyTheme.Lime, TextAlignmentOptions.Center, true);

            // Right: what to do / result, and the spin speed.
            var side = UIFactory.CreateRect("Side", page, Vector2.zero, Vector2.one, new Vector2(860f, 0f), new Vector2(-MobileUI.Side, 0f));
            var status = MobileUi_Row(side.transform, "Status", 200f, 140f);
            statusText = MobileUI.Text(status, "Text", "SPIN IT!", 90f, SoundCopyTheme.Lime, TextAlignmentOptions.MidlineLeft, true);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 50f;
            statusText.fontSizeMax = 90f;
            var detail = MobileUi_Row(side.transform, "Detail", 360f, 150f);
            detailText = MobileUI.Text(detail, "Text", "Spin the phone like a bottle.", 38f, SoundCopyTheme.Cream, TextAlignmentOptions.TopLeft);
            detailText.textWrappingMode = TextWrappingModes.Normal;

            var label = MobileUi_Row(side.transform, "SpeedLabel", 540f, 50f);
            MobileUI.Text(label, "Text", "SPIN SPEED", 32f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.MidlineLeft);
            var track = MobileUi_Row(side.transform, "SpeedBar", 600f, 44f);
            if (SoundCopySkin.BarTrack != null) UIFactory.AddSprite(track.gameObject, SoundCopySkin.BarTrack, Color.white);
            else UIFactory.AddRounded(track.gameObject, new Color(1f, 1f, 1f, 0.15f), 22f);
            var fill = UIFactory.CreateRect("Fill", track, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            speedFillImage = UIFactory.AddRounded(fill, SoundCopyTheme.Lime, 22f);
            speedFill = (RectTransform)fill.transform;
            // Marker where x2 starts (fastPeak / (fastPeak * 1.4)).
            var marker = UIFactory.CreateRect("X2Marker", track, new Vector2(1f / 1.4f, -0.3f), new Vector2(1f / 1.4f, 1.3f), Vector2.zero, Vector2.zero);
            ((RectTransform)marker.transform).sizeDelta = new Vector2(8f, 0f);
            UIFactory.AddRounded(marker, SoundCopyTheme.PaleBonus, 4f);
            fastMarker = (RectTransform)marker.transform;
            var x2 = MobileUi_Row(side.transform, "X2Label", 650f, 50f);
            MobileUI.Text(x2, "Text", "x2 ROUND  >", 30f, SoundCopyTheme.PaleBonus, TextAlignmentOptions.MidlineRight);

            var main = MobileUI.BottomRight(page, "MainAction");
            mainButton = MobileUI.BigButton(main, "MainButton", "LET THE PHONE CHOOSE", SoundCopyTheme.Lime, SoundCopyTheme.Ink, out mainLabel);
        }

        static RectTransform MobileUi_Row(Transform parent, string name, float top, float height) => MobileUI.Row(parent, name, top, height, 0f);
    }
}
