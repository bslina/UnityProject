using System.Collections;
using System.Collections.Generic;
using System;
using GameUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.InferenceEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// Photo Challenge: two players take turns finding an object and photographing it.
// MobileCLIP compares the photo with the mission's reference image; a close enough match scores a point.
//
// Layout: landscape (canvas 1920 x 1080, matched on height). The whole UI is built in code at Start.
// Inside Pocket Jungle: "Retour aux jeux" goes back to the level menu and "Continuer" on the
// result screen opens the shared Win screen (MiniGameBridge). The shared pause stops the turn timer.
public class PhotoChallengeGame : MonoBehaviour
{
    private Canvas canvas;

    private GameObject homePanel;
    private GameObject setupPanel;
    private GameObject gamePanel;
    private GameObject resultPanel;
    private GameObject howToPanel;

    [Header("Jungle Background Art")]
    [Tooltip("Assign Home_Jungle_BG.png (import it as Sprite 2D and UI).")]
    [SerializeField] private Sprite homeJungleBackground;
    [Tooltip("Assign Setup_Jungle_BG.png (import it as Sprite 2D and UI).")]
    [SerializeField] private Sprite setupJungleBackground;
    [Tooltip("Assign Gameplay_Jungle_BG.png (import it as Sprite 2D and UI).")]
    [SerializeField] private Sprite gameplayJungleBackground;
    [Tooltip("Assign Result_Jungle_BG.png (import it as Sprite 2D and UI).")]
    [SerializeField] private Sprite resultJungleBackground;

    [Header("Pocket Jungle skin (same art as the menus; empty = original look)")]
    [Tooltip("Background behind every screen (replaces the jungle art above when set).")]
    [SerializeField] private Sprite skinBackground;
    [Tooltip("Framed green window (2D Casual UI GUI_28) for cards, mission and camera frames.")]
    [SerializeField] private Sprite skinPanel;
    [Tooltip("Green button (2D Casual UI GUI_11).")]
    [SerializeField] private Sprite skinButton;
    [Tooltip("Title ribbon (2D Casual UI GUI_68).")]
    [SerializeField] private Sprite skinRibbon;
    [Tooltip("Trophy icon (2D Casual UI GUI_62).")]
    [SerializeField] private Sprite skinTrophy;
    [Tooltip("Titles and buttons (Starborn).")]
    [SerializeField] private TMP_FontAsset displayFont;
    [Tooltip("Other texts (Cute Dino).")]
    [SerializeField] private TMP_FontAsset bodyFont;

    // Same tints as the menus: the button sprite is green, colours multiply with it.
    private static readonly Color SecondaryTint = new Color(0.83f, 0.44f, 0.29f);
    private static readonly Color DangerTint = new Color(0.9f, 0.35f, 0.3f);
    private bool Skinned => skinButton != null || skinPanel != null;

    private TMP_InputField player1Input;
    private TMP_InputField player2Input;

    private TMP_Text gameTitle;
    private TMP_Text missionText;
    private TMP_Text player1ScoreText;
    private TMP_Text player2ScoreText;
    private TMP_Text resultP1ScoreText;
    private TMP_Text resultP2ScoreText;
    private readonly List<Image> resultTrophyImages = new List<Image>();
    private TMP_Text statusText;
    private TMP_Text timerText;
    private TMP_Text resultText;
    private RawImage cameraPreview;
    private RectTransform cameraArea;
    private RawImage referencePreview;
    private AspectRatioFitter referenceFitter;
    private TMP_Text referenceHiddenText;
    private MissionReference pendingReference;   // shown only once the player taps "Je suis prêt"
    private TMP_Text setupStatusText;
    private TMP_Text mainButtonLabel;
    private Image mainButtonFace;
    private GameObject continueButton;
    private WebCamTexture webcamTexture;
    private Texture2D capturedPhoto;

    [Serializable]
    private class MissionReference
    {
        [Tooltip("Use the exact mission title, e.g. A BOTTLE or A CHAIR.")]
        public string missionLabel = "A BOTTLE";
        [Tooltip("Reference photo of the object. Use a clear, centered object.")]
        public Texture2D referenceImage;
    }

    [Header("Image-to-Image Verification (MobileCLIP)")]
    [SerializeField] private ModelAsset imageEncoderModelAsset;
    [SerializeField] private MissionReference[] missionReferences = Array.Empty<MissionReference>();
    [Tooltip("Starting cosine-similarity threshold. Calibrate with real test photos.")]
    [SerializeField, Range(-1f, 1f)] private float similarityThreshold = 0.45f;

    private Worker similarityWorker;
    private RenderTexture similarityInputTexture;
    private readonly Dictionary<string, List<float[]>> referenceEmbeddings = new Dictionary<string, List<float[]>>();
    private bool similarityReady;
    private bool similarityLoading;
    private string currentMission;
    private const int SimilarityImageSize = 256;

    [Header("Turn Timer")]
    [SerializeField, Min(1f)] private float secondsPerTurn = 10f;
    private float turnTimeRemaining;
    private bool turnTimerRunning;
    private bool turnResolved;
    private bool waitingForReady;   // the next player must take the phone and tap "Je suis prêt"
    private int lastDisplayedSeconds = -1;

    private readonly string[] players = { "Joueur 1", "Joueur 2" };
    private readonly int[] scores = { 0, 0 };

    // French names shown for the mission labels set in the Inspector (the labels themselves stay the keys).
    private static readonly Dictionary<string, string> MissionNamesFr = new Dictionary<string, string>
    {
        { "a bottle", "UNE BOUTEILLE" },
        { "a chair", "UNE CHAISE" },
        { "a cup", "UNE TASSE" },
        { "a book", "UN LIVRE" },
        { "a phone", "UN TÉLÉPHONE" },
    };

    private int currentPlayer;
    private int turnsPlayed;
    private const int TotalTurns = 6;

    // Shared shuffled deck: both players receive random missions without repeats
    // until every configured mission has been used once.
    private readonly List<MissionReference> missionDeck = new List<MissionReference>();
    private string lastMissionKey;

    private bool gameStarted;

    private static readonly Color Player1Orange = new Color32(242, 140, 38, 255); // #F28C26
    private static readonly Color Player2Blue = new Color32(64, 128, 255, 255);   // #4080FF
    private static readonly Color RedSignalColor = new Color32(255, 31, 31, 255); // #FF1F1F
    private static readonly Color BlueSignalColor = new Color32(51, 115, 255, 255); // #3373FF
    private static readonly Color TakePhotoGreen = new Color32(0, 208, 132, 255); // Green is reserved for capture.
    private static Sprite roundedUiSprite;
    private static Sprite starUiSprite;

    private readonly Color backgroundColor = new Color32(15, 15, 20, 255); // #0F0F14
    private readonly Color cardColor = new Color32(15, 15, 20, 255);

    private void Start()
    {
        CreateInterface();
        ShowHome();
        StartCoroutine(InitializeImageSimilarityModel());
    }

    // ================================================================ camera

    private IEnumerator InitializeCamera()
    {
        statusText.text = "Démarrage de la caméra…";

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
            statusText.text = "Accès à la caméra refusé (Paramètres > Applis).";
            yield break;
        }
#elif UNITY_IOS || UNITY_WEBGL
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            statusText.text = "Accès à la caméra refusé.";
            yield break;
        }
#endif

        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            statusText.text = "Aucune caméra trouvée.";
            Debug.LogWarning("Photo Challenge: No webcam detected.");
            yield break;
        }

        int selectedCamera = 0;
        // Prefer the rear-facing camera when one exists.
        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                selectedCamera = i;
                break;
            }
        }

        // Landscape request: phone camera sensors are natively landscape.
        webcamTexture = new WebCamTexture(devices[selectedCamera].name, 1280, 720, 30);
        cameraPreview.texture = webcamTexture;
        cameraPreview.color = Color.white;
        webcamTexture.Play();

        float timeout = 10f;
        while (timeout > 0f && (webcamTexture.width <= 16 || webcamTexture.height <= 16))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.Log($"Photo Challenge camera: {webcamTexture.deviceName}, {webcamTexture.width}x{webcamTexture.height}, " +
                  $"rotation {webcamTexture.videoRotationAngle}, mirrored {webcamTexture.videoVerticallyMirrored}");

        if (webcamTexture.width > 16)
            statusText.text = waitingForReady ? ReadyMessage() : "Vise l'objet et appuie sur PHOTO !";
        else
        {
            statusText.text = "Caméra pas prête. Vérifie les autorisations.";
            Debug.LogWarning("Camera did not initialize in time.");
        }
    }

    // Keeps the live preview upright, unmirrored and with the camera's real proportions.
    private void UpdateCameraPreviewLayout()
    {
        if (cameraPreview == null || cameraArea == null) return;
        Texture texture = cameraPreview.texture;
        if (texture == null || texture.width <= 16) return;

        bool live = texture == webcamTexture;
        int angle = live ? webcamTexture.videoRotationAngle : 0;
        bool mirrored = live && webcamTexture.videoVerticallyMirrored;
        bool sideways = angle == 90 || angle == 270;

        cameraPreview.uvRect = mirrored ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        cameraPreview.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);

        // Fit the (possibly rotated) picture inside the frame without stretching it.
        Vector2 area = cameraArea.rect.size;
        if (area.x <= 0f || area.y <= 0f) return;
        float aspect = texture.width / (float)texture.height;
        float shownAspect = sideways ? 1f / aspect : aspect;
        Vector2 shown = area.x / area.y > shownAspect
            ? new Vector2(area.y * shownAspect, area.y)
            : new Vector2(area.x, area.x / shownAspect);
        cameraPreview.rectTransform.sizeDelta = sideways ? new Vector2(shown.y, shown.x) : shown;
    }

    // Copies the current camera frame into an upright, unmirrored texture.
    private Texture2D CaptureUprightFrame()
    {
        int w = webcamTexture.width;
        int h = webcamTexture.height;
        Color32[] source = webcamTexture.GetPixels32();

        if (webcamTexture.videoVerticallyMirrored)
        {
            var flipped = new Color32[source.Length];
            for (int y = 0; y < h; y++)
                Array.Copy(source, (h - 1 - y) * w, flipped, y * w, w);
            source = flipped;
        }

        int angle = ((webcamTexture.videoRotationAngle % 360) + 360) % 360;
        Color32[] result = source;
        int rw = w, rh = h;
        if (angle == 90 || angle == 180 || angle == 270)
        {
            rw = angle == 180 ? w : h;
            rh = angle == 180 ? h : w;
            result = new Color32[source.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int nx, ny;
                    if (angle == 90) { nx = y; ny = w - 1 - x; }            // clockwise
                    else if (angle == 180) { nx = w - 1 - x; ny = h - 1 - y; }
                    else { nx = h - 1 - y; ny = x; }                       // 270 clockwise
                    result[ny * rw + nx] = source[y * w + x];
                }
            }
        }

        var texture = new Texture2D(rw, rh, TextureFormat.RGB24, false);
        texture.SetPixels32(result);
        texture.Apply();
        return texture;
    }

    private void StopCamera()
    {
        if (cameraPreview != null)
            cameraPreview.texture = null;

        if (webcamTexture != null)
        {
            if (webcamTexture.isPlaying)
                webcamTexture.Stop();
            Destroy(webcamTexture);
            webcamTexture = null;
        }

        if (capturedPhoto != null)
        {
            Destroy(capturedPhoto);
            capturedPhoto = null;
        }
    }

    // ================================================================ interface (landscape)

    private void CreateInterface()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventObject = new GameObject("EventSystem");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject canvasObject = new GameObject("GameCanvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Landscape, like the rest of Pocket Jungle: always 1080 tall, 1920+ wide.
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();

        CreateBackground();

        homePanel = CreatePanel("WelcomeScreen");
        setupPanel = CreatePanel("PlayerSetupScreen");
        gamePanel = CreatePanel("MissionScreen");
        resultPanel = CreatePanel("ResultScreen");
        howToPanel = CreatePanel("HowToPlayScreen");

        // Screen-specific jungle art sits behind the actual interactive UI.
        AddPanelBackground(homePanel, homeJungleBackground);
        AddPanelBackground(setupPanel, setupJungleBackground);
        AddPanelBackground(gamePanel, gameplayJungleBackground);
        AddPanelBackground(resultPanel, resultJungleBackground);
        AddPanelBackground(howToPanel, setupJungleBackground);

        BuildHomeScreen();
        BuildSetupScreen();
        BuildGameScreen();
        BuildResultScreen();
        BuildHowToScreen();
    }

    private void CreateBackground()
    {
        GameObject obj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(canvas.transform, false);
        Image image = obj.GetComponent<Image>();
        image.color = backgroundColor;
        image.raycastTarget = false;
        Stretch(obj.GetComponent<RectTransform>());
    }

    private GameObject CreatePanel(string panelName)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        return panel;
    }

    private void AddPanelBackground(GameObject panel, Sprite backgroundSprite)
    {
        if (panel == null)
            return;
        if (skinBackground != null) backgroundSprite = skinBackground;

        GameObject holder = new GameObject("JungleBackgroundArea", typeof(RectTransform), typeof(RectMask2D));
        holder.transform.SetParent(panel.transform, false);
        holder.transform.SetAsFirstSibling();
        Stretch(holder.GetComponent<RectTransform>());

        GameObject backgroundObject = new GameObject("JungleBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(holder.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.raycastTarget = false;
        backgroundImage.color = backgroundSprite != null ? Color.white : backgroundColor;

        // The jungle art is portrait: cover the landscape screen without stretching it.
        AspectRatioFitter fitter = backgroundObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = backgroundSprite != null ? backgroundSprite.rect.width / backgroundSprite.rect.height : 16f / 9f;

        // A light dark veil keeps the arcade UI readable on top of the art.
        GameObject veil = new GameObject("Veil", typeof(RectTransform), typeof(Image));
        veil.transform.SetParent(holder.transform, false);
        Stretch(veil.GetComponent<RectTransform>());
        Image veilImage = veil.GetComponent<Image>();
        veilImage.color = new Color(0f, 0f, 0f, backgroundSprite != null ? (skinBackground != null ? 0.4f : 0.25f) : 0f);
        veilImage.raycastTarget = false;
    }

    private void BuildHomeScreen()
    {
        CreateAccent(homePanel, 0.93f);
        CreateDecorativeStar(homePanel, 0.06f, 0.80f, 70, Player1Orange);
        CreateDecorativeStar(homePanel, 0.50f, 0.72f, 56, Player2Blue);
        CreateDecorativeStar(homePanel, 0.09f, 0.40f, 44, Player2Blue);

        // Left: title block.
        CreateTextAt(homePanel, "PHOTO", 170, 0.06f, 0.54f, 0.74f, 0.17f, Player1Orange);
        CreateTextAt(homePanel, "CHALLENGE", 130, 0.06f, 0.54f, 0.58f, 0.14f, Player2Blue);
        CreateTextAt(homePanel, "LA CHASSE AUX OBJETS !", 48, 0.06f, 0.54f, 0.465f, 0.07f, Color.white);
        CreateOutlinedPlate(homePanel, "TwoPlayerBadge", 0.10f, 0.50f, 0.355f, 0.09f, Player2Blue, 5f);
        CreateTextAt(homePanel, "2 JOUEURS  •  " + Mathf.RoundToInt(secondsPerTurn) + " S PAR TOUR", 34, 0.11f, 0.49f, 0.355f, 0.075f, Color.white);

        // Right: actions.
        CreateButton(homePanel, "C'EST PARTI !", 0.60f, 0.94f, 0.68f, 0.16f, Player1Orange, OpenSetup, 54);
        CreateButton(homePanel, "COMMENT JOUER", 0.60f, 0.94f, 0.48f, 0.14f, BlueSignalColor, ShowHowTo, 46);
        CreateButton(homePanel, MiniGameBridge.IsAvailable ? "RETOUR AUX JEUX" : "QUITTER", 0.60f, 0.94f, 0.30f, 0.14f, RedSignalColor, ExitGame, 46);
    }

    private void BuildSetupScreen()
    {
        CreateAccent(setupPanel, 0.93f);
        CreateTitleRibbon(setupPanel, 0.84f, 0.11f);
        CreateText(setupPanel, "LES JOUEURS", 90, 0.84f, 0.11f, Color.white);
        CreateText(setupPanel, "CHOISISSEZ VOS PRÉNOMS. PRÊTS ?", 40, 0.755f, 0.06f, BlueSignalColor);

        CreateOutlinedPlate(setupPanel, "PlayerOneCard", 0.06f, 0.48f, 0.52f, 0.30f, Player1Orange, 6f);
        CreateTextAt(setupPanel, "JOUEUR 1  •  ORANGE", 40, 0.09f, 0.45f, 0.61f, 0.07f, Color.white);
        player1Input = CreateInput(setupPanel, "PRÉNOM DU JOUEUR 1", 0.48f, 0.12f, Player1Orange, 0.09f, 0.45f);

        CreateOutlinedPlate(setupPanel, "PlayerTwoCard", 0.52f, 0.94f, 0.52f, 0.30f, Player2Blue, 6f);
        CreateTextAt(setupPanel, "JOUEUR 2  •  BLEU", 40, 0.55f, 0.91f, 0.61f, 0.07f, Color.white);
        player2Input = CreateInput(setupPanel, "PRÉNOM DU JOUEUR 2", 0.48f, 0.12f, Player2Blue, 0.55f, 0.91f);

        setupStatusText = CreateText(setupPanel, "", 32, 0.30f, 0.07f, RedSignalColor);
        CreateButton(setupPanel, "RETOUR", 0.06f, 0.32f, 0.14f, 0.14f, Player1Orange, ShowHome, 44);
        CreateButton(setupPanel, "COMMENCER", 0.56f, 0.94f, 0.14f, 0.16f, BlueSignalColor, StartGame, 54);
    }

    private void BuildGameScreen()
    {
        // Top bar: P1 score, whose turn, P2 score (the top-right corner stays free for the pause button).
        CreateOutlinedPlate(gamePanel, "PlayerOneScorePlate", 0.02f, 0.25f, 0.92f, 0.10f, Player1Orange, 5f);
        player1ScoreText = CreateTextAt(gamePanel, "JOUEUR 1  •  0", 40, 0.03f, 0.24f, 0.92f, 0.085f, Color.white);
        CreateTitleRibbon(gamePanel, 0.92f, 0.10f);
        gameTitle = CreateTextAt(gamePanel, "AU TOUR DE JOUEUR 1", 64, 0.27f, 0.66f, 0.92f, 0.10f, Color.white);
        CreateOutlinedPlate(gamePanel, "PlayerTwoScorePlate", 0.68f, 0.91f, 0.92f, 0.10f, Player2Blue, 5f);
        player2ScoreText = CreateTextAt(gamePanel, "JOUEUR 2  •  0", 40, 0.69f, 0.90f, 0.92f, 0.085f, Color.white);

        // Left column: mission + reference picture.
        CreateOutlinedPlate(gamePanel, "MissionCard", 0.02f, 0.27f, 0.735f, 0.17f, Player1Orange, 6f);
        missionText = CreateTextAt(gamePanel, "TROUVE :\nUNE CHAISE", 54, 0.03f, 0.26f, 0.735f, 0.155f, Color.white);
        CreateSideLabel(gamePanel, "RÉFÉRENCE", 0.02f, 0.27f, 0.61f);
        CreateReferencePreview(gamePanel, 0.02f, 0.27f, 0.075f, 0.585f);

        // Centre: big live camera.
        CreateSideLabel(gamePanel, "CAMÉRA", 0.29f, 0.74f, 0.83f);
        CreateCameraPreview(gamePanel, 0.29f, 0.74f, 0.075f, 0.81f);

        // Right column: timer, status, main button (bottom-right, under the thumb).
        timerText = CreateTextAt(gamePanel, "TEMPS : 15 s", 64, 0.76f, 0.98f, 0.72f, 0.12f, BlueSignalColor);
        statusText = CreateTextAt(gamePanel, "", 34, 0.76f, 0.98f, 0.50f, 0.26f, Color.white);
        GameObject button = CreateButton(gamePanel, "PRENDRE LA PHOTO", 0.76f, 0.98f, 0.18f, 0.24f, TakePhotoGreen, OnMainButton, 50);
        mainButtonLabel = button.GetComponentInChildren<TMP_Text>();
        mainButtonFace = button.transform.Find("ButtonFace").GetComponent<Image>();
    }

    private void BuildResultScreen()
    {
        CreateAccent(resultPanel, 0.93f);
        CreateTitleRibbon(resultPanel, 0.84f, 0.11f);
        CreateText(resultPanel, "PARTIE TERMINÉE !", 86, 0.84f, 0.11f, Color.white);

        // Left: trophy + winner.
        CreateTrophyGraphic(resultPanel, 0.25f, 0.57f);
        resultText = CreateTextAt(resultPanel, "JOUEUR GAGNE !", 80, 0.04f, 0.46f, 0.36f, 0.12f, Player1Orange);

        // Right: both scores.
        CreateOutlinedPlate(resultPanel, "WinnerPlayerOnePlate", 0.52f, 0.94f, 0.65f, 0.17f, Player1Orange, 6f);
        resultP1ScoreText = CreateTextAt(resultPanel, "JOUEUR 1\n0 POINT", 44, 0.54f, 0.92f, 0.65f, 0.15f, Color.white);
        CreateOutlinedPlate(resultPanel, "WinnerPlayerTwoPlate", 0.52f, 0.94f, 0.45f, 0.17f, Player2Blue, 6f);
        resultP2ScoreText = CreateTextAt(resultPanel, "JOUEUR 2\n0 POINT", 44, 0.54f, 0.92f, 0.45f, 0.15f, Color.white);

        // Bottom: replay, own menu, and (inside Pocket Jungle) continue to the shared Win screen.
        CreateButton(resultPanel, "REJOUER", 0.04f, 0.28f, 0.14f, 0.14f, Player1Orange, OpenSetup, 44);
        CreateButton(resultPanel, "MENU", 0.30f, 0.50f, 0.14f, 0.14f, BlueSignalColor, ShowHome, 44);
        continueButton = CreateButton(resultPanel, "CONTINUER  >", 0.56f, 0.94f, 0.14f, 0.16f, TakePhotoGreen, ContinueToBigGame, 54);
        continueButton.SetActive(MiniGameBridge.IsAvailable);
    }

    private void BuildHowToScreen()
    {
        CreateAccent(howToPanel, 0.93f);
        CreateTitleRibbon(howToPanel, 0.84f, 0.11f);
        CreateText(howToPanel, "COMMENT JOUER", 90, 0.84f, 0.11f, Skinned ? Color.white : Player2Blue);
        CreateText(howToPanel, "4 ÉTAPES POUR DEVENIR UNE STAR DE LA PHOTO", 36, 0.755f, 0.06f, Color.white);

        CreateStep(howToPanel, "HowToStepOne", 0.05f, 0.49f, 0.60f, Player1Orange, "01  TROUVE L'OBJET\nRegarde l'image de référence.");
        CreateStep(howToPanel, "HowToStepTwo", 0.51f, 0.95f, 0.60f, Player2Blue, "02  SOIS RAPIDE !\nTu as " + Mathf.RoundToInt(secondsPerTurn) + " secondes par tour.");
        CreateStep(howToPanel, "HowToStepThree", 0.05f, 0.49f, 0.37f, Player2Blue, "03  PRENDS LA PHOTO\nL'objet réel doit ressembler à l'image.");
        CreateStep(howToPanel, "HowToStepFour", 0.51f, 0.95f, 0.37f, Player1Orange, "04  MARQUE UN POINT\nLe plus de points gagne la partie.");

        CreateButton(howToPanel, "RETOUR AU MENU", 0.30f, 0.70f, 0.13f, 0.14f, BlueSignalColor, ShowHome, 46);
    }

    private void CreateStep(GameObject parent, string name, float xMin, float xMax, float y, Color color, string text)
    {
        CreateOutlinedPlate(parent, name, xMin, xMax, y, 0.19f, color, 5f);
        CreateTextAt(parent, text, 40, xMin + 0.02f, xMax - 0.02f, y, 0.17f, Color.white);
    }

    private void CreateCameraPreview(GameObject parent, float xMin, float xMax, float yMin, float yMax)
    {
        GameObject frame = new GameObject("CameraPreviewFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(xMin, yMin);
        frameRect.anchorMax = new Vector2(xMax, yMax);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        ApplyFrame(frame.GetComponent<Image>(), Player2Blue);

        // Black area inside the frame; the picture is fitted (and rotated if needed) inside it.
        GameObject area = new GameObject("CameraArea", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        area.transform.SetParent(frame.transform, false);
        cameraArea = area.GetComponent<RectTransform>();
        Stretch(cameraArea);
        float inset = skinPanel != null ? 26f : 6f; // inside the window's white frame
        cameraArea.offsetMin = new Vector2(inset, inset);
        cameraArea.offsetMax = new Vector2(-inset, -inset);
        ApplyRoundedImage(area.GetComponent<Image>(), Color.black);

        GameObject preview = new GameObject("LiveCameraPreview", typeof(RectTransform), typeof(RawImage));
        preview.transform.SetParent(area.transform, false);
        RectTransform previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewRect.sizeDelta = Vector2.zero;

        cameraPreview = preview.GetComponent<RawImage>();
        cameraPreview.color = Color.clear;
        cameraPreview.raycastTarget = false;
    }

    private void CreateReferencePreview(GameObject parent, float xMin, float xMax, float yMin, float yMax)
    {
        GameObject frame = new GameObject("ReferenceImageFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(xMin, yMin);
        frameRect.anchorMax = new Vector2(xMax, yMax);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        ApplyFrame(frame.GetComponent<Image>(), Player1Orange);

        GameObject area = new GameObject("ReferenceArea", typeof(RectTransform), typeof(Image));
        area.transform.SetParent(frame.transform, false);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        Stretch(areaRect);
        float inset = skinPanel != null ? 26f : 6f;
        areaRect.offsetMin = new Vector2(inset, inset);
        areaRect.offsetMax = new Vector2(-inset, -inset);
        ApplyRoundedImage(area.GetComponent<Image>(), Color.white);

        GameObject imageObject = new GameObject("ReferenceImage", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(area.transform, false);
        Stretch(imageObject.GetComponent<RectTransform>());
        referenceFitter = imageObject.AddComponent<AspectRatioFitter>();
        referenceFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        referencePreview = imageObject.GetComponent<RawImage>();
        referencePreview.color = Color.white;
        referencePreview.raycastTarget = false;

        // Shown instead of the picture until the turn starts.
        GameObject hidden = new GameObject("HiddenLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        hidden.transform.SetParent(area.transform, false);
        Stretch(hidden.GetComponent<RectTransform>());
        referenceHiddenText = hidden.GetComponent<TextMeshProUGUI>();
        referenceHiddenText.text = "?\n<size=40%>APPUIE SUR PRÊT</size>";
        referenceHiddenText.fontSize = 150;
        referenceHiddenText.alignment = TextAlignmentOptions.Center;
        referenceHiddenText.color = new Color(0.25f, 0.2f, 0.15f);
        referenceHiddenText.raycastTarget = false;
        ApplyFont(referenceHiddenText, 150);
    }

    private void CreateSideLabel(GameObject parent, string label, float xMin, float xMax, float y)
    {
        Color labelColor = label.StartsWith("RÉF") ? Player1Orange : Player2Blue;
        CreateTextAt(parent, label, 36, xMin, xMax, y, 0.05f, labelColor);
    }

    private TMP_Text CreateText(GameObject parent, string value, int fontSize, float y, float height, Color color)
    {
        return CreateTextAt(parent, value, fontSize, 0.06f, 0.94f, y, height, color);
    }

    private TMP_Text CreateTextAt(GameObject parent, string value, int fontSize,
        float xMin, float xMax, float y, float height, Color color)
    {
        GameObject obj = new GameObject("Text_" + value, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent.transform, false);

        TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(24f, fontSize * 0.6f);
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        text.richText = true;
        ApplyFont(text, fontSize);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(xMin, y - height / 2f);
        rect.anchorMax = new Vector2(xMax, y + height / 2f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    // Big texts (titles, numbers) use the display font, the rest the body font.
    private void ApplyFont(TMP_Text text, int fontSize)
    {
        TMP_FontAsset font = fontSize >= 56 ? (displayFont != null ? displayFont : bodyFont) : (bodyFont != null ? bodyFont : displayFont);
        if (font != null) text.font = font;
    }

    // Ribbon banner behind a screen title, like the menus.
    private void CreateTitleRibbon(GameObject parent, float y, float height)
    {
        if (skinRibbon == null) return;
        GameObject ribbon = new GameObject("TitleRibbon", typeof(RectTransform), typeof(Image));
        ribbon.transform.SetParent(parent.transform, false);
        RectTransform rect = ribbon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, y);
        rect.sizeDelta = new Vector2(1150f, height * 1080f * 1.35f);
        Image image = ribbon.GetComponent<Image>();
        image.sprite = skinRibbon;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
    }

    // Frame around the camera / reference picture: the menus' green window when skinned.
    private void ApplyFrame(Image image, Color fallback)
    {
        if (skinPanel == null) { ApplyRoundedImage(image, fallback); return; }
        image.sprite = skinPanel;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    // Button colours mapped onto the green button sprite (the same way the menus tint it).
    private Color ButtonTint(Color color)
    {
        if (skinButton == null) return color;
        if (color == Player1Orange) return SecondaryTint;
        if (color == RedSignalColor) return DangerTint;
        return Color.white;
    }

    private TMP_InputField CreateInput(GameObject parent, string hint, float y, float height, Color borderColor,
        float xMin, float xMax)
    {
        GameObject obj = new GameObject(hint, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        obj.transform.SetParent(parent.transform, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, y - height / 2f);
        rect.anchorMax = new Vector2(xMax, y + height / 2f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image borderImage = obj.GetComponent<Image>();
        ApplyRoundedImage(borderImage, borderColor);
        borderImage.raycastTarget = true; // InputField needs a raycastable target graphic.

        GameObject face = new GameObject("InputFace", typeof(RectTransform), typeof(Image));
        face.transform.SetParent(obj.transform, false);
        RectTransform faceRect = face.GetComponent<RectTransform>();
        Stretch(faceRect);
        faceRect.offsetMin = new Vector2(5f, 5f);
        faceRect.offsetMax = new Vector2(-5f, -5f);
        ApplyRoundedImage(face.GetComponent<Image>(), backgroundColor);
        face.GetComponent<Image>().raycastTarget = false;

        TMP_InputField input = obj.GetComponent<TMP_InputField>();
        input.targetGraphic = borderImage;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 18;
        input.contentType = TMP_InputField.ContentType.Standard;

        TMP_Text valueText = CreateChildText(face.transform, 46);
        valueText.alignment = TextAlignmentOptions.Left;
        valueText.fontStyle = FontStyles.Bold;
        input.textComponent = valueText;

        TMP_Text placeholder = CreateChildText(face.transform, 38);
        placeholder.text = hint;
        placeholder.color = new Color(1f, 1f, 1f, 0.65f);
        placeholder.alignment = TextAlignmentOptions.Left;
        input.placeholder = placeholder;
        return input;
    }

    private TMP_Text CreateChildText(Transform parent, int fontSize)
    {
        GameObject obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(24f, fontSize * 0.6f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        ApplyFont(text, fontSize);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(20f, 6f);
        rect.offsetMax = new Vector2(-20f, -6f);
        return text;
    }

    private GameObject CreateOutlinedPlate(GameObject parent, string plateName,
        float xMin, float xMax, float y, float height, Color borderColor, float borderPixels)
    {
        GameObject root = new GameObject(plateName, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(xMin, y - height / 2f);
        rootRect.anchorMax = new Vector2(xMax, y + height / 2f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image border = root.GetComponent<Image>();
        ApplyRoundedImage(border, borderColor);
        border.raycastTarget = false;

        if (Skinned)
        {
            bool playerPlate = plateName.Contains("Player") || plateName.Contains("Winner");
            if (playerPlate) return root;            // solid player colour, white text on top
            if (skinPanel != null)
            {
                border.sprite = skinPanel;           // the menus' green window
                border.color = Color.white;
                return root;
            }
        }

        GameObject face = new GameObject("DarkFace", typeof(RectTransform), typeof(Image));
        face.transform.SetParent(root.transform, false);
        RectTransform faceRect = face.GetComponent<RectTransform>();
        Stretch(faceRect);
        faceRect.offsetMin = new Vector2(borderPixels, borderPixels);
        faceRect.offsetMax = new Vector2(-borderPixels, -borderPixels);
        Image faceImage = face.GetComponent<Image>();
        ApplyRoundedImage(faceImage, cardColor);
        faceImage.raycastTarget = false;
        return root;
    }

    private void CreateAccent(GameObject parent, float y)
    {
        if (Skinned) return; // the menus have no colour rails
        CreateColorStrip(parent.transform, "AccentOrange", 0.12f, 0.48f, y, y + 0.008f, Player1Orange);
        CreateColorStrip(parent.transform, "AccentBlue", 0.52f, 0.88f, y, y + 0.008f, Player2Blue);
    }

    private void CreateColorStrip(Transform parent, string stripName, float xMin, float xMax,
        float yMin, float yMax, Color color)
    {
        GameObject strip = new GameObject(stripName, typeof(RectTransform), typeof(Image));
        strip.transform.SetParent(parent, false);
        RectTransform rect = strip.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        ApplyRoundedImage(strip.GetComponent<Image>(), color);
        strip.GetComponent<Image>().raycastTarget = false;
    }

    // Square star of `size` px centred on (x, y) in screen fractions.
    private void CreateDecorativeStar(GameObject parent, float x, float y, int size, Color color)
    {
        GameObject starObject = new GameObject("ArcadeStar", typeof(RectTransform), typeof(Image));
        starObject.transform.SetParent(parent.transform, false);
        RectTransform rect = starObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(x, y);
        rect.sizeDelta = new Vector2(size * 1.6f, size * 1.6f);
        Image image = starObject.GetComponent<Image>();
        image.sprite = GetStarUiSprite();
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    // Trophy drawn with rounded shapes (sizes in pixels so it keeps its shape on any screen width).
    private void CreateTrophyGraphic(GameObject parent, float centerX, float centerY)
    {
        if (skinTrophy != null)
        {
            Image trophy = CreateTrophyPiece(parent, "Trophy", centerX, centerY, 0f, 0f, 300f, 300f, Player1Orange);
            trophy.sprite = skinTrophy;
            trophy.type = Image.Type.Simple;
            trophy.preserveAspect = true;
            resultTrophyImages.Add(trophy);
            return;
        }
        CreateTrophyPiece(parent, "TrophyLeftHandle", centerX, centerY, -128f, 22f, 86f, 100f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyRightHandle", centerX, centerY, 128f, 22f, 86f, 100f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyLeftHandleCutout", centerX, centerY, -122f, 22f, 56f, 52f, backgroundColor);
        CreateTrophyPiece(parent, "TrophyRightHandleCutout", centerX, centerY, 122f, 22f, 56f, 52f, backgroundColor);
        CreateTrophyPiece(parent, "TrophyCup", centerX, centerY, 0f, 38f, 205f, 154f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyStem", centerX, centerY, 0f, -77f, 44f, 68f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyBase", centerX, centerY, 0f, -123f, 140f, 42f, Player1Orange);
        foreach (Transform child in parent.transform)
        {
            if (child.name.StartsWith("Trophy") && !child.name.Contains("Cutout"))
            {
                Image image = child.GetComponent<Image>();
                if (image != null && !resultTrophyImages.Contains(image))
                    resultTrophyImages.Add(image);
            }
        }
    }

    private Image CreateTrophyPiece(GameObject parent, string pieceName, float anchorX, float anchorY,
        float x, float y, float width, float height, Color color)
    {
        GameObject piece = new GameObject(pieceName, typeof(RectTransform), typeof(Image));
        piece.transform.SetParent(parent.transform, false);
        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, anchorY);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        ApplyRoundedImage(piece.GetComponent<Image>(), color);
        piece.GetComponent<Image>().raycastTarget = false;
        return piece.GetComponent<Image>();
    }

    private void SetTrophyColor(Color color)
    {
        foreach (Image part in resultTrophyImages)
        {
            if (part != null)
                part.color = color;
        }
    }

    // Chunky arcade button. Returns its root so it can be hidden or relabelled.
    private GameObject CreateButton(GameObject parent, string label, float xMin, float xMax, float y, float height,
        Color color, UnityEngine.Events.UnityAction action, int fontSize = 46)
    {
        GameObject root = new GameObject("ArcadeButton_" + label, typeof(RectTransform));
        root.transform.SetParent(parent.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(xMin, y - height / 2f);
        rootRect.anchorMax = new Vector2(xMax, y + height / 2f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        if (skinButton != null)
        {
            // Same green button as the menus (one Image, 9-sliced).
            GameObject skinFace = CreateSurfaceChild(root.transform, "ButtonFace", ButtonTint(color));
            Stretch(skinFace.GetComponent<RectTransform>());
            Image skinImage = skinFace.GetComponent<Image>();
            skinImage.sprite = skinButton;
            skinImage.type = Image.Type.Sliced;
            skinImage.raycastTarget = true;
            Button skinButtonComponent = skinFace.AddComponent<Button>();
            skinButtonComponent.targetGraphic = skinImage;
            skinButtonComponent.onClick.AddListener(action);
            TMP_Text skinLabel = CreateChildText(skinFace.transform, fontSize);
            skinLabel.text = label;
            skinLabel.color = Color.white;
            if (displayFont != null) skinLabel.font = displayFont;
            return root;
        }

        // Shadow and glow are separate layers, keeping buttons chunky and easy to hit.
        GameObject shadow = CreateSurfaceChild(root.transform, "ButtonShadow", backgroundColor);
        RectTransform shadowRect = shadow.GetComponent<RectTransform>();
        Stretch(shadowRect);
        shadowRect.offsetMin = new Vector2(0f, -9f);
        shadowRect.offsetMax = new Vector2(0f, -9f);

        Color glowColor = new Color(color.r, color.g, color.b, 0.38f);
        GameObject glow = CreateSurfaceChild(root.transform, "ButtonGlow", glowColor);
        RectTransform glowRect = glow.GetComponent<RectTransform>();
        Stretch(glowRect);
        glowRect.offsetMin = new Vector2(-5f, -4f);
        glowRect.offsetMax = new Vector2(5f, 5f);

        GameObject face = CreateSurfaceChild(root.transform, "ButtonFace", color);
        RectTransform faceRect = face.GetComponent<RectTransform>();
        Stretch(faceRect);
        faceRect.offsetMin = new Vector2(1f, 3f);
        faceRect.offsetMax = new Vector2(-1f, 3f);

        Image faceImage = face.GetComponent<Image>();
        faceImage.raycastTarget = true; // Button must receive touches through the GraphicRaycaster.
        Button button = face.AddComponent<Button>();
        button.targetGraphic = faceImage;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        TMP_Text text = CreateChildText(face.transform, fontSize);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        return root;
    }

    private GameObject CreateSurfaceChild(Transform parent, string name, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        ApplyRoundedImage(child.GetComponent<Image>(), color);
        child.GetComponent<Image>().raycastTarget = false;
        return child;
    }

    private static void ApplyRoundedImage(Image image, Color color)
    {
        image.sprite = GetRoundedUiSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
    }

    private static Sprite GetStarUiSprite()
    {
        if (starUiSprite != null)
            return starUiSprite;

        const int size = 96;
        const int vertexCount = 10;
        Vector2[] vertices = new Vector2[vertexCount];
        float center = (size - 1) * 0.5f;
        for (int i = 0; i < vertexCount; i++)
        {
            float angle = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float radius = (i % 2 == 0) ? size * 0.47f : size * 0.205f;
            vertices[i] = new Vector2(center + Mathf.Cos(angle) * radius,
                                      center + Mathf.Sin(angle) * radius);
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PhotoChallenge_StarUI_Runtime";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = false;
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
                {
                    bool crosses = (vertices[i].y > point.y) != (vertices[j].y > point.y);
                    if (crosses && point.x < (vertices[j].x - vertices[i].x) *
                        (point.y - vertices[i].y) / (vertices[j].y - vertices[i].y) + vertices[i].x)
                        inside = !inside;
                }
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        starUiSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        starUiSprite.name = "PhotoChallenge_StarUI_Sprite";
        return starUiSprite;
    }

    private static Sprite GetRoundedUiSprite()
    {
        if (roundedUiSprite != null)
            return roundedUiSprite;

        const int size = 64;
        const float radius = 15f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PhotoChallenge_RoundedUI_Runtime";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x - center) - (half - radius);
                float qy = Mathf.Abs(y - center) - (half - radius);
                float dx = Mathf.Max(qx, 0f);
                float dy = Mathf.Max(qy, 0f);
                float distance = Mathf.Min(Mathf.Max(qx, qy), 0f) + Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        roundedUiSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
        roundedUiSprite.name = "PhotoChallenge_RoundedSprite_Runtime";
        return roundedUiSprite;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ================================================================ navigation

    private void SetOnlyPanel(GameObject panelToShow)
    {
        if (homePanel != null) homePanel.SetActive(panelToShow == homePanel);
        if (setupPanel != null) setupPanel.SetActive(panelToShow == setupPanel);
        if (gamePanel != null) gamePanel.SetActive(panelToShow == gamePanel);
        if (resultPanel != null) resultPanel.SetActive(panelToShow == resultPanel);
        if (howToPanel != null) howToPanel.SetActive(panelToShow == howToPanel);
    }

    private void OpenSetup()
    {
        SetOnlyPanel(setupPanel);
        if (setupStatusText != null) setupStatusText.text = string.Empty;
    }

    private void ShowHome() => SetOnlyPanel(homePanel);
    private void ShowHowTo() => SetOnlyPanel(howToPanel);

    // Inside Pocket Jungle: back to the level menu. Played alone: quit.
    private void ExitGame()
    {
        if (MiniGameBridge.IsAvailable && GameFlow.Instance != null)
        {
            StopCamera();
            GameFlow.Instance.LoadLevelMenu();
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Result screen "Continuer": opens Pocket Jungle's Win screen (stars from the winner's score out of 3 turns).
    private void ContinueToBigGame()
    {
        if (!MiniGameBridge.IsAvailable)
        {
            ShowHome();
            return;
        }
        int winner = scores[0] == scores[1] ? -1 : scores[0] > scores[1] ? 0 : 1;
        int best = Mathf.Max(scores[0], scores[1]);
        string detail = winner < 0
            ? $"Égalité {scores[0]} – {scores[1]}"
            : $"{players[winner]} gagne {scores[winner]} – {scores[1 - winner]}";
        int stars = Mathf.Clamp(best, 1, 3);
        MiniGameBridge.Win(detail, stars, best, 0.2f);
    }

    // ================================================================ game flow

    private void StartGame()
    {
        if (!similarityReady)
        {
            setupStatusText.text = similarityLoading
                ? "Le modèle IA se charge, patiente un instant."
                : "La comparaison IA n'est pas prête : vérifie le modèle et les images de référence.";
            return;
        }

        if (GetAvailableMissions().Count == 0)
        {
            setupStatusText.text = "Ajoute au moins une mission avec une image de référence sur le GameManager.";
            return;
        }

        players[0] = string.IsNullOrWhiteSpace(player1Input.text) ? "Joueur 1" : player1Input.text.Trim();
        players[1] = string.IsNullOrWhiteSpace(player2Input.text) ? "Joueur 2" : player2Input.text.Trim();

        scores[0] = 0;
        scores[1] = 0;
        currentPlayer = 0;
        turnsPlayed = 0;
        missionDeck.Clear();
        lastMissionKey = null;
        gameStarted = true;

        ShowMission();
    }

    private void ShowMission()
    {
        if (turnsPlayed >= TotalTurns)
        {
            ShowWinner();
            return;
        }

        List<MissionReference> availableMissions = GetAvailableMissions();
        if (availableMissions.Count == 0)
        {
            gameStarted = false;
            SetOnlyPanel(setupPanel);
            setupStatusText.text = "Aucune image de référence utilisable : renseigne Mission Label et Reference Image.";
            return;
        }

        SetOnlyPanel(gamePanel);

        Color playerColor = currentPlayer == 0 ? Player1Orange : Player2Blue;
        gameTitle.text = "AU TOUR DE " + players[currentPlayer].ToUpperInvariant();
        gameTitle.color = Skinned ? Color.white : playerColor;

        MissionReference selectedReference = DrawRandomMission(availableMissions);
        currentMission = selectedReference.missionLabel.Trim();
        Debug.Log("Photo Challenge: random mission '" + currentMission + "' assigned to " + players[currentPlayer] + ".");
        // Fair start: the object and its picture stay hidden until the player taps "Je suis prêt".
        pendingReference = selectedReference;
        HideReference();

        RefreshScoreText();

        // The turn waits for the player to take the phone: the timer starts on "Je suis prêt".
        turnResolved = false;
        turnTimerRunning = false;
        waitingForReady = true;
        turnTimeRemaining = secondsPerTurn;
        lastDisplayedSeconds = -1;
        UpdateTimerLabel();
        SetMainButton("JE SUIS PRÊT", Player1Orange);
        statusText.text = ReadyMessage();

        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.color = Color.white;
        }
        else
        {
            StartCoroutine(InitializeCamera());
        }
    }

    private void HideReference()
    {
        missionText.text = "TROUVE :\n???";
        referencePreview.texture = null;
        referencePreview.color = Color.clear;
        if (referenceHiddenText != null) referenceHiddenText.gameObject.SetActive(true);
    }

    // Shows the object to find (name + picture) at the moment the turn timer starts.
    private void RevealReference()
    {
        if (pendingReference == null) return;
        missionText.text = "TROUVE :\n" + MissionDisplayName(currentMission);
        referencePreview.texture = pendingReference.referenceImage;
        referencePreview.color = Color.white;
        if (referenceFitter != null && pendingReference.referenceImage != null)
            referenceFitter.aspectRatio = pendingReference.referenceImage.width / (float)pendingReference.referenceImage.height;
        if (referenceHiddenText != null) referenceHiddenText.gameObject.SetActive(false);
    }

    private string ReadyMessage() =>
        "Passe le téléphone à <b>" + players[currentPlayer] + "</b>, puis appuie sur PRÊT.";

    private static string MissionDisplayName(string label)
    {
        string key = NormalizeMissionKey(label);
        return MissionNamesFr.TryGetValue(key, out string french) ? french : label.ToUpperInvariant();
    }

    private void SetMainButton(string label, Color color)
    {
        if (mainButtonLabel != null) mainButtonLabel.text = label;
        if (mainButtonFace != null) mainButtonFace.color = ButtonTint(color);
    }

    // The bottom-right button: "Je suis prêt" starts the turn, then it takes the photo.
    private void OnMainButton()
    {
        if (!gameStarted || turnResolved) return;
        if (waitingForReady)
        {
            waitingForReady = false;
            GameAudio.Play("reveal");
            RevealReference();
            SetMainButton("PRENDRE LA PHOTO", TakePhotoGreen);
            statusText.text = "Vise l'objet et appuie sur PHOTO !";
            StartTurnTimer();
            return;
        }
        CapturePhoto();
    }

    private MissionReference DrawRandomMission(List<MissionReference> availableMissions)
    {
        // Refill and shuffle only after every available mission has been used.
        if (missionDeck.Count == 0)
        {
            missionDeck.AddRange(availableMissions);

            // Fisher-Yates shuffle.
            for (int i = missionDeck.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                MissionReference temporary = missionDeck[i];
                missionDeck[i] = missionDeck[j];
                missionDeck[j] = temporary;
            }

            // Avoid repeating the previous mission at the boundary between decks.
            if (missionDeck.Count > 1 &&
                NormalizeMissionKey(missionDeck[0].missionLabel) == lastMissionKey)
            {
                int swapIndex = UnityEngine.Random.Range(1, missionDeck.Count);
                MissionReference temporary = missionDeck[0];
                missionDeck[0] = missionDeck[swapIndex];
                missionDeck[swapIndex] = temporary;
            }
        }

        MissionReference selected = missionDeck[0];
        missionDeck.RemoveAt(0);
        lastMissionKey = NormalizeMissionKey(selected.missionLabel);
        return selected;
    }

    private void CapturePhoto()
    {
        if (!gameStarted || !turnTimerRunning || turnResolved)
            return;

        if (webcamTexture == null || !webcamTexture.isPlaying || webcamTexture.width <= 16)
        {
            statusText.text = "Caméra indisponible. Vérifie les autorisations.";
            return;
        }

        if (capturedPhoto != null)
            Destroy(capturedPhoto);

        // Upright copy of the frame (camera rotation and mirroring corrected).
        GameAudio.Play("shutter");
        capturedPhoto = CaptureUprightFrame();

        // Freeze the captured image, then compare it with the mission's reference image.
        cameraPreview.texture = capturedPhoto;
        statusText.text = "Comparaison avec la référence…";
        CompareCapturedPhoto(capturedPhoto);
    }

    private void RefreshScoreText()
    {
        if (player1ScoreText != null)
            player1ScoreText.text = players[0].ToUpperInvariant() + "  •  " + scores[0];
        if (player2ScoreText != null)
            player2ScoreText.text = players[1].ToUpperInvariant() + "  •  " + scores[1];
    }

    private void StartTurnTimer()
    {
        if (!gameStarted || turnResolved || waitingForReady) return;

        turnTimeRemaining = secondsPerTurn;
        turnTimerRunning = true;
        lastDisplayedSeconds = -1;
        UpdateTimerLabel();
    }

    private void UpdateTimerLabel()
    {
        if (timerText == null) return;

        int seconds = Mathf.Max(0, Mathf.CeilToInt(turnTimeRemaining));
        if (seconds == lastDisplayedSeconds) return;

        // Ticks for the last 3 seconds of a running turn.
        if (turnTimerRunning && lastDisplayedSeconds > 0 && seconds <= 3 && seconds > 0) GameAudio.Play("tick");
        lastDisplayedSeconds = seconds;
        timerText.text = "TEMPS : " + seconds + " s";
        timerText.color = seconds <= 3 ? RedSignalColor : BlueSignalColor;
    }

    private void EndTurnForTimeout()
    {
        if (!gameStarted || turnResolved) return;

        turnTimerRunning = false;
        turnResolved = true;
        timerText.text = "TEMPS ÉCOULÉ !";
        GameAudio.Play("photo_miss");
        statusText.text = "Pas de point ce tour. Joueur suivant…";

        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.color = Color.white;
        }

        StartCoroutine(AdvanceToNextPlayerAfterDelay());
    }

    private IEnumerator AdvanceToNextPlayerAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.5f);

        if (!gameStarted) yield break;

        turnsPlayed++;
        currentPlayer = 1 - currentPlayer;
        ShowMission();
    }

    private void AllowAnotherPhoto(string message)
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.color = Color.white;
        }
        statusText.text = message;
    }

    private void Update()
    {
        UpdateCameraPreviewLayout();

        if (!gameStarted || !turnTimerRunning || turnResolved)
            return;

        // Game time, so Pocket Jungle's pause (timeScale 0) also pauses the turn.
        turnTimeRemaining -= Time.deltaTime;
        if (turnTimeRemaining <= 0f)
        {
            turnTimeRemaining = 0f;
            UpdateTimerLabel();
            EndTurnForTimeout();
            return;
        }

        UpdateTimerLabel();
    }

    private void ShowWinner()
    {
        gameStarted = false;
        StopCamera();
        SetOnlyPanel(resultPanel);
        if (continueButton != null) continueButton.SetActive(MiniGameBridge.IsAvailable);

        if (scores[0] > scores[1])
        {
            resultText.text = players[0].ToUpperInvariant() + " GAGNE !";
            resultText.color = Player1Orange;
            SetTrophyColor(Player1Orange);
        }
        else if (scores[1] > scores[0])
        {
            resultText.text = players[1].ToUpperInvariant() + " GAGNE !";
            resultText.color = Player2Blue;
            SetTrophyColor(Player2Blue);
        }
        else
        {
            resultText.text = "ÉGALITÉ !";
            resultText.color = BlueSignalColor;
            SetTrophyColor(BlueSignalColor);
        }

        if (resultP1ScoreText != null)
            resultP1ScoreText.text = players[0].ToUpperInvariant() + "\n" + Points(scores[0]);
        if (resultP2ScoreText != null)
            resultP2ScoreText.text = players[1].ToUpperInvariant() + "\n" + Points(scores[1]);
    }

    private static string Points(int value) => value + (value > 1 ? " POINTS" : " POINT");

    // ================================================================ AI comparison (MobileCLIP)

    private IEnumerator InitializeImageSimilarityModel()
    {
        similarityLoading = true;

        if (imageEncoderModelAsset == null)
        {
            similarityLoading = false;
            Debug.LogError("Photo Challenge: Assign MobileCLIP vision_model_fp16.onnx to Image Encoder Model Asset on GameManager.");
            yield break;
        }

        Model model = null;
        bool modelInitialized = false;
        try
        {
            model = ModelLoader.Load(imageEncoderModelAsset);
            try
            {
                similarityWorker = new Worker(model, BackendType.GPUCompute);
            }
            catch (Exception gpuException)
            {
                Debug.LogWarning("Photo Challenge: GPU worker could not start; using CPU. " + gpuException.Message);
                similarityWorker = new Worker(model, BackendType.CPU);
            }

            similarityInputTexture = new RenderTexture(
                SimilarityImageSize, SimilarityImageSize, 0, RenderTextureFormat.ARGB32);
            similarityInputTexture.Create();
            modelInitialized = true;
        }
        catch (Exception ex)
        {
            similarityReady = false;
            Debug.LogError("Photo Challenge: could not initialize MobileCLIP. Check the ONNX model and Console. Details: " + ex);
        }

        if (!modelInitialized)
        {
            similarityLoading = false;
            yield break;
        }

        if (missionReferences == null)
            missionReferences = Array.Empty<MissionReference>();

        int loadedReferences = 0;
        foreach (MissionReference reference in missionReferences)
        {
            if (reference == null || reference.referenceImage == null ||
                string.IsNullOrWhiteSpace(reference.missionLabel))
                continue;

            string key = NormalizeMissionKey(reference.missionLabel);
            try
            {
                float[] embedding = ComputeImageEmbedding(reference.referenceImage);
                if (!referenceEmbeddings.TryGetValue(key, out List<float[]> embeddings))
                {
                    embeddings = new List<float[]>();
                    referenceEmbeddings[key] = embeddings;
                }
                embeddings.Add(embedding);
                loadedReferences++;
                Debug.Log("Photo Challenge: reference embedding ready for '" + reference.missionLabel + "'.");
            }
            catch (Exception embeddingException)
            {
                Debug.LogError("Photo Challenge: could not process reference image for '" +
                    reference.missionLabel + "'. " + embeddingException);
            }

            // Spread reference-image processing across frames to avoid a long startup stall.
            yield return null;
        }

        similarityReady = referenceEmbeddings.Count > 0;
        similarityLoading = false;
        if (similarityReady)
        {
            Debug.Log("Photo Challenge: image-to-image comparison is ready. Loaded " +
                loadedReferences + " reference image(s) for " + referenceEmbeddings.Count + " mission(s).");
        }
        else
        {
            Debug.LogError("Photo Challenge: no reference embeddings loaded. Assign a mission label and image for at least one reference.");
        }
    }

    private float[] ComputeImageEmbedding(Texture source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (similarityWorker == null || similarityInputTexture == null)
            throw new InvalidOperationException("The image similarity model is not initialized.");

        // MobileCLIP-S2 uses a 256x256 RGB tensor. Center-crop to a square instead of
        // stretching the image, preserving object proportions. TextureConverter maps RGB to 0..1 floats.
        // This MobileCLIP-S2 export expects that range, without ImageNet mean/std normalization.
        float aspect = source.width / (float)source.height;
        Vector2 scale;
        Vector2 offset;
        if (aspect > 1f)
        {
            float cropScaleX = 1f / aspect;
            scale = new Vector2(cropScaleX, 1f);
            offset = new Vector2((1f - cropScaleX) * 0.5f, 0f);
        }
        else
        {
            float cropScaleY = aspect;
            scale = new Vector2(1f, cropScaleY);
            offset = new Vector2(0f, (1f - cropScaleY) * 0.5f);
        }
        Graphics.Blit(source, similarityInputTexture, scale, offset);
        using Tensor<float> inputTensor = new Tensor<float>(
            new TensorShape(1, 3, SimilarityImageSize, SimilarityImageSize));
        TextureConverter.ToTensor(similarityInputTexture, inputTensor, default);
        similarityWorker.Schedule(inputTensor);

        // The official MobileCLIP ONNX export names its projected image vector "image_embeds".
        using Tensor<float> embeddingTensor =
            (similarityWorker.PeekOutput("image_embeds") as Tensor<float>).ReadbackAndClone();

        int embeddingSize = embeddingTensor.shape[1];
        float[] embedding = new float[embeddingSize];
        for (int i = 0; i < embeddingSize; i++)
            embedding[i] = embeddingTensor[0, i];

        return embedding;
    }

    private void CompareCapturedPhoto(Texture2D photo)
    {
        if (!similarityReady || similarityWorker == null)
        {
            AllowAnotherPhoto("La comparaison IA n'est pas prête. Vérifie le modèle dans l'Inspector.");
            return;
        }

        if (photo == null)
        {
            AllowAnotherPhoto("Photo indisponible, reprends-en une.");
            return;
        }

        string key = NormalizeMissionKey(currentMission);
        if (!referenceEmbeddings.TryGetValue(key, out List<float[]> referenceList) || referenceList.Count == 0)
        {
            AllowAnotherPhoto("Pas d'image de référence pour cette mission. Vérifie le GameManager.");
            return;
        }

        try
        {
            float[] capturedEmbedding = ComputeImageEmbedding(photo);
            float bestSimilarity = -1f;
            foreach (float[] referenceEmbedding in referenceList)
            {
                float score = CosineSimilarity(capturedEmbedding, referenceEmbedding);
                if (score > bestSimilarity)
                    bestSimilarity = score;
            }

            Debug.Log("Photo Challenge: image comparison target='" + currentMission +
                "'; cosine_similarity=" + bestSimilarity.ToString("0.000") +
                "; threshold=" + similarityThreshold.ToString("0.000") + ".");

            if (bestSimilarity >= similarityThreshold)
            {
                if (!turnResolved && gameStarted)
                {
                    scores[currentPlayer]++;
                    RefreshScoreText();
                    turnResolved = true;
                    turnTimerRunning = false;
                    timerText.text = "TROUVÉ !";
                    GameAudio.Play("photo_match");
                    statusText.text = "C'EST ÇA ! Ressemblance : " + bestSimilarity.ToString("0.00") + ".  +1 point";
                    Haptics.Pulse(150, 200);
                    StartCoroutine(AdvanceToNextPlayerAfterDelay());
                }
            }
            else
            {
                GameAudio.Play("photo_miss", 0.7f);
                AllowAnotherPhoto("Pas assez ressemblant (" + bestSimilarity.ToString("0.00") +
                    "). Centre l'objet et réessaie.");
            }
        }
        catch (Exception ex)
        {
            AllowAnotherPhoto("La comparaison a échoué. Regarde la Console puis réessaie.");
            Debug.LogError("Photo Challenge: image comparison failed: " + ex);
        }
    }

    private static float CosineSimilarity(float[] first, float[] second)
    {
        if (first == null || second == null || first.Length != second.Length || first.Length == 0)
            return -1f;

        double dot = 0.0;
        double firstNorm = 0.0;
        double secondNorm = 0.0;
        for (int i = 0; i < first.Length; i++)
        {
            dot += first[i] * second[i];
            firstNorm += first[i] * first[i];
            secondNorm += second[i] * second[i];
        }

        if (firstNorm <= 0.0 || secondNorm <= 0.0)
            return -1f;
        return (float)(dot / Math.Sqrt(firstNorm * secondNorm));
    }

    private List<MissionReference> GetAvailableMissions()
    {
        List<MissionReference> available = new List<MissionReference>();
        HashSet<string> seen = new HashSet<string>();
        foreach (MissionReference reference in missionReferences)
        {
            if (reference == null || reference.referenceImage == null ||
                string.IsNullOrWhiteSpace(reference.missionLabel))
                continue;

            string key = NormalizeMissionKey(reference.missionLabel);
            if (referenceEmbeddings.ContainsKey(key) && seen.Add(key))
                available.Add(reference);
        }
        return available;
    }

    private static string NormalizeMissionKey(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Trim('﻿')
            .Trim('"')
            .Replace("_", " ")
            .ToLowerInvariant();
    }

    private void OnDestroy()
    {
        StopCamera();
        similarityWorker?.Dispose();
        similarityWorker = null;

        if (similarityInputTexture != null)
        {
            similarityInputTexture.Release();
            Destroy(similarityInputTexture);
            similarityInputTexture = null;
        }
    }
}
