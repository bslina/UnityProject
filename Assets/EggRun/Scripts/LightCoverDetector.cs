using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// Detects the player covering the top of the phone to make shade for the egg.
//
// Main signal: the FRONT CAMERA. A thumb over the lens turns its picture dark and blurry,
// which works on every phone (many budget phones, e.g. Galaxy A17, only have "virtual"
// light/proximity sensors that barely react to a hand).
// The camera runs at low resolution and only its average brightness is measured; nothing is recorded or shown.
//
// Backups: the light sensor (light drops well below the room's normal level) and the
// proximity sensor ("near"). By default they are ignored while the camera works, because
// virtual sensors can give false readings.
// In the Editor, hold L. Without any usable sensor/camera, SunGlare shows a Shade button.
public class LightCoverDetector : MonoBehaviour
{
    public static LightCoverDetector Instance { get; private set; }

    [Header("Front camera (main signal)")]
    [SerializeField] bool useFrontCamera = true;
    [Tooltip("Also use the computer's webcam in the Editor (off: hold L instead).")]
    [SerializeField] bool useCameraInEditor = false;
    [SerializeField] float cameraSampleInterval = 0.1f;
    [Tooltip("Covered when the picture gets darker than this fraction of the room's normal brightness.")]
    [SerializeField, Range(0.05f, 0.9f)] float cameraCoverRatio = 0.4f;
    [Tooltip("Uncovered again above this fraction (higher than Cover Ratio, avoids flicker).")]
    [SerializeField, Range(0.1f, 0.95f)] float cameraUncoverRatio = 0.55f;
    [Tooltip("A picture darker than this (0-1) always counts as covered, unless the whole room is that dark.")]
    [SerializeField, Range(0f, 0.3f)] float cameraDarkLevel = 0.06f;
    [Tooltip("A thumb on the lens gives a flat, blurry picture. Below this contrast (and dimmer than the room) = covered, even if the camera brightens it.")]
    [SerializeField, Range(0f, 0.2f)] float cameraFlatContrast = 0.035f;
    [Tooltip("While the camera works, ignore the light and proximity sensors (they are often virtual and unreliable).")]
    [SerializeField] bool cameraOnlyWhenAvailable = true;

    [Header("Light sensor (lux)")]
    [SerializeField, Range(0.05f, 0.9f)] float coverRatio = 0.25f;
    [SerializeField, Range(0.1f, 0.95f)] float uncoverRatio = 0.4f;
    [Tooltip("Below this many lux the room is too dark to tell a covered sensor from a dark room.")]
    [SerializeField] float minUsableLux = 12f;

    [Header("Room brightness learning (camera and light sensor)")]
    [Tooltip("Seconds to follow the room when it gets darker (slow, so a hand isn't learned as 'the room').")]
    [SerializeField] float baselineFallTime = 6f;
    [Tooltip("Seconds to follow the room when it gets brighter (fast).")]
    [SerializeField] float baselineRiseTime = 0.5f;

    [Header("Proximity sensor")]
    [SerializeField, Range(0.1f, 0.9f)] float nearFraction = 0.5f;

    [Header("Feel")]
    [Tooltip("The cover must last this long to count (ignores a finger brushing past).")]
    [SerializeField] float debounce = 0.12f;

    public event Action<bool> CoverChanged;

    public bool IsCovered { get; private set; }
    public bool SensorUsable => CameraUsable || LightUsable || ProximityUsable;
    public bool CameraUsable => cam != null && cam.isPlaying && camHasFrame;
    public bool LightUsable => LightSensor.current != null && hasLux && luxBaseline >= minUsableLux;
    public bool ProximityUsable => ProximitySensor.current != null && farDistance > 0f;

    // Live values for the debug text.
    public bool CameraCovered { get; private set; }
    public float CameraBrightness { get; private set; }
    public float CameraContrast { get; private set; }
    public string CameraStatus { get; private set; } = "off";
    public float Lux { get; private set; }
    public float Distance { get; private set; } = -1f;
    public bool LightCovered { get; private set; }
    public bool ProximityNear { get; private set; }

    // Set by the on-screen Shade button.
    public bool ManualCover { get; set; }

    float luxBaseline;
    bool hasLux;
    float farDistance;
    bool rawCovered;
    float rawChangedAt;

    WebCamTexture cam;
    Color32[] pixels;
    bool camHasFrame;
    float camBaseline;
    float nextSample;
    float lastSampleTime;
    Coroutine starting;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        StopCamera();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        ReadCamera();
        ReadLight();
        ReadProximity();

        bool sensors = cameraOnlyWhenAvailable && CameraUsable
            ? CameraCovered
            : CameraCovered || LightCovered || ProximityNear;
        bool key = Keyboard.current != null && Keyboard.current.lKey.isPressed;
        bool raw = sensors || ManualCover || key;

        if (raw != rawCovered)
        {
            rawCovered = raw;
            rawChangedAt = Time.unscaledTime;
        }
        bool next = rawCovered && Time.unscaledTime - rawChangedAt >= debounce;
        if (next != IsCovered)
        {
            IsCovered = next;
            CoverChanged?.Invoke(next);
        }
    }

    // ---------------------------------------------------------------- camera

    // Asks for camera permission (once) and starts the front camera. Safe to call repeatedly.
    public void StartCamera()
    {
        if (!useFrontCamera || cam != null || starting != null) return;
        if (Application.isEditor && !useCameraInEditor)
        {
            CameraStatus = "off in Editor (hold L)";
            return;
        }
        starting = StartCoroutine(StartCameraRoutine());
    }

    public void StopCamera()
    {
        if (starting != null)
        {
            StopCoroutine(starting);
            starting = null;
        }
        if (cam != null)
        {
            cam.Stop();
            Destroy(cam);
            cam = null;
        }
        camHasFrame = false;
        CameraCovered = false;
        if (CameraStatus == "running") CameraStatus = "off";
    }

    IEnumerator StartCameraRoutine()
    {
        CameraStatus = "asking permission";
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            bool answered = false;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            Permission.RequestUserPermission(Permission.Camera, callbacks);
            float giveUp = Time.realtimeSinceStartup + 60f;
            while (!answered && Time.realtimeSinceStartup < giveUp) yield return null;
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            CameraStatus = "permission refused";
            starting = null;
            yield break;
        }
#else
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            CameraStatus = "permission refused";
            starting = null;
            yield break;
        }
#endif

        string deviceName = null;
        foreach (var device in WebCamTexture.devices)
        {
            if (device.isFrontFacing) { deviceName = device.name; break; }
        }
        if (deviceName == null && Application.isEditor && WebCamTexture.devices.Length > 0)
            deviceName = WebCamTexture.devices[0].name; // laptop webcam
        if (deviceName == null)
        {
            CameraStatus = "no front camera";
            starting = null;
            yield break;
        }

        cam = new WebCamTexture(deviceName, 160, 120, 15);
        cam.Play();
        camHasFrame = false;
        CameraStatus = "running";
        starting = null;
    }

    void ReadCamera()
    {
        if (cam == null || !cam.isPlaying)
        {
            CameraCovered = false;
            return;
        }
        if (Time.unscaledTime < nextSample || !cam.didUpdateThisFrame || cam.width < 32) return;
        nextSample = Time.unscaledTime + cameraSampleInterval;

        int count = cam.width * cam.height;
        if (pixels == null || pixels.Length != count) pixels = new Color32[count];
        cam.GetPixels32(pixels);

        // Average brightness and contrast over a 32 x 24 grid of points.
        const int gx = 32, gy = 24;
        float sum = 0f, sumSq = 0f;
        for (int y = 0; y < gy; y++)
        {
            int row = (int)((y + 0.5f) / gy * cam.height) * cam.width;
            for (int x = 0; x < gx; x++)
            {
                var p = pixels[row + (int)((x + 0.5f) / gx * cam.width)];
                float l = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
                sum += l;
                sumSq += l * l;
            }
        }
        const int n = gx * gy;
        float mean = sum / n;
        float contrast = Mathf.Sqrt(Mathf.Max(0f, sumSq / n - mean * mean));
        CameraBrightness = mean;
        CameraContrast = contrast;

        float dt = camHasFrame ? Time.unscaledTime - lastSampleTime : 0f;
        lastSampleTime = Time.unscaledTime;
        if (!camHasFrame)
        {
            camHasFrame = true;
            camBaseline = mean;
        }

        if (!CameraCovered)
            camBaseline = Follow(camBaseline, mean, dt);

        bool roomLit = camBaseline > cameraDarkLevel * 3f;
        bool dark = roomLit && mean < cameraDarkLevel;
        bool flatAndDim = roomLit && contrast < cameraFlatContrast && mean < camBaseline * 0.8f;
        float ratio = CameraCovered ? cameraUncoverRatio : cameraCoverRatio;
        CameraCovered = dark || flatAndDim || mean < camBaseline * ratio;
    }

    float Follow(float baseline, float value, float dt)
    {
        float time = value > baseline ? baselineRiseTime : baselineFallTime;
        return Mathf.Lerp(baseline, value, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, time)));
    }

    // ---------------------------------------------------------------- light + proximity

    void ReadLight()
    {
        var sensor = LightSensor.current;
        if (sensor == null) { LightCovered = false; return; }
        if (!sensor.enabled) InputSystem.EnableDevice(sensor);

        float lux = sensor.lightLevel.ReadValue();
        Lux = lux;
        if (!hasLux)
        {
            if (lux <= 0f) return; // no reading yet
            hasLux = true;
            luxBaseline = lux;
        }

        if (!LightCovered) luxBaseline = Follow(luxBaseline, lux, Time.unscaledDeltaTime);
        if (luxBaseline < minUsableLux) { LightCovered = false; return; }

        float ratio = lux / luxBaseline;
        LightCovered = LightCovered ? ratio < uncoverRatio : ratio < coverRatio;
    }

    void ReadProximity()
    {
        var sensor = ProximitySensor.current;
        if (sensor == null) { ProximityNear = false; return; }
        if (!sensor.enabled) InputSystem.EnableDevice(sensor);

        float d = sensor.distance.ReadValue();
        Distance = d;
        // Phones report a fixed "far" value (often 5 or 8 cm) and ~0 when near; learn the far one.
        if (d > farDistance) farDistance = d;
        ProximityNear = farDistance > 0f && d < farDistance * nearFraction;
    }

    public string DebugLine()
    {
        string camera = CameraUsable
            ? $"cam {CameraBrightness:0.00} / room {camBaseline:0.00}  contrast {CameraContrast:0.000}{(CameraCovered ? " COVERED" : "")}"
            : "cam " + CameraStatus;
        string light = LightSensor.current == null ? "no light sensor"
            : $"light {Lux:0} / room {luxBaseline:0}{(LightUsable ? "" : " (too dark)")}";
        string prox = ProximitySensor.current == null ? "no prox" : $"prox {Distance:0.0}/{farDistance:0.0}";
        return $"{camera}\n{light}  {prox}  covered={IsCovered}";
    }
}
