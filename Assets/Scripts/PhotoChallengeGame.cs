using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.InferenceEngine;

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
    private RawImage referencePreview;
    private TMP_Text setupStatusText;
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

    private bool photoCaptured;

    [Header("Turn Timer")]
    [SerializeField, Min(1f)] private float secondsPerTurn = 10f;
    private float turnTimeRemaining;
    private bool turnTimerRunning;
    private bool turnResolved;
    private int lastDisplayedSeconds = -1;

    private readonly string[] players = { "Player 1", "Player 2" };
    private readonly int[] scores = { 0, 0 };

    private IEnumerator InitializeCamera()
    {
        statusText.text = "Starting camera...";

#if UNITY_IOS || UNITY_WEBGL
    yield return Application.RequestUserAuthorization(
        UserAuthorization.WebCam);

    if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
    {
        statusText.text = "Camera permission denied.";
        StartTurnTimerIfGameIsActive();
        yield break;
    }
#endif

        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices == null || devices.Length == 0)
        {
            statusText.text = "No webcam found. Camera access is required.";
            Debug.LogWarning("Photo Challenge: No webcam detected.");
            StartTurnTimerIfGameIsActive();
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

        webcamTexture = new WebCamTexture(
            devices[selectedCamera].name, 1280, 720, 30);

        cameraPreview.texture = webcamTexture;
        cameraPreview.color = Color.white;

        webcamTexture.Play();

        Debug.Log(
    $"Camera: {webcamTexture.deviceName}, " +
    $"Rotation: {webcamTexture.videoRotationAngle}, " +
    $"Vertically Mirrored: {webcamTexture.videoVerticallyMirrored}"
);


        float timeout = 10f;

        while (timeout > 0f &&
               (webcamTexture.width <= 16 || webcamTexture.height <= 16))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (webcamTexture.width > 16)
        {
            statusText.text = "Aim at the object and tap TAKE PHOTO.";
            StartTurnTimerIfGameIsActive();
        }
        else
        {
            statusText.text = "Camera not ready. Check permissions.";
            Debug.LogWarning("Camera did not initialize in time.");
            StartTurnTimerIfGameIsActive();
        }
    }

    private void CreateCameraPreview(GameObject parent, float y, float height)
    {
        GameObject frame = new GameObject("CameraPreviewFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.45f, y - height / 2f);
        frameRect.anchorMax = new Vector2(0.94f, y + height / 2f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        Image frameImage = frame.GetComponent<Image>();
        ApplyRoundedImage(frameImage, Player2Blue);

        GameObject preview = new GameObject("LiveCameraPreview", typeof(RectTransform), typeof(RawImage));
        preview.transform.SetParent(frame.transform, false);
        RectTransform previewRect = preview.GetComponent<RectTransform>();
        Stretch(previewRect);
        previewRect.offsetMin = new Vector2(5f, 5f);
        previewRect.offsetMax = new Vector2(-5f, -5f);

        cameraPreview = preview.GetComponent<RawImage>();
        cameraPreview.color = Color.clear;
        cameraPreview.raycastTarget = false;
    }

    private void CreateReferencePreview(GameObject parent, float y, float height)
    {
        GameObject frame = new GameObject("ReferenceImageFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.06f, y - height / 2f);
        frameRect.anchorMax = new Vector2(0.41f, y + height / 2f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        ApplyRoundedImage(frame.GetComponent<Image>(), Player1Orange);

        GameObject imageObject = new GameObject("ReferenceImage", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(frame.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        Stretch(imageRect);
        imageRect.offsetMin = new Vector2(5f, 5f);
        imageRect.offsetMax = new Vector2(-5f, -5f);

        referencePreview = imageObject.GetComponent<RawImage>();
        referencePreview.color = Color.white;
        referencePreview.raycastTarget = false;
    }

    private void CreateSideLabel(GameObject parent, string label, float xMin, float xMax, float y)
    {
        Color labelColor = label.ToUpperInvariant().Contains("REFERENCE") ? Player1Orange : Player2Blue;
        CreateTextAt(parent, label, 23, xMin, xMax, y, 0.035f, labelColor);
    }

    // Missions are now driven by the reference images assigned in the Inspector.
    // This ensures the game never chooses a mission that has no comparison image.

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
    private readonly Color secondaryColor = new Color32(64, 128, 255, 255);

    private void Start()
    {
        CreateInterface();
        ShowHome();
        StartCoroutine(InitializeImageSimilarityModel());
    }

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

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
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

        // Two short neon rails keep the palette playful without adding new colors.
        CreateColorStrip(canvas.transform, "TopOrangeRail", 0.08f, 0.28f, 0.955f, 0.960f, Player1Orange);
        CreateColorStrip(canvas.transform, "TopBlueRail", 0.72f, 0.92f, 0.955f, 0.960f, Player2Blue);
        CreateColorStrip(canvas.transform, "BottomOrangeRail", 0.08f, 0.20f, 0.035f, 0.040f, Player1Orange);
        CreateColorStrip(canvas.transform, "BottomBlueRail", 0.80f, 0.92f, 0.035f, 0.040f, Player2Blue);
    }

    private GameObject CreatePanel(string panelName)
    {
        GameObject panel = new GameObject(
            panelName,
            typeof(RectTransform)
        );

        panel.transform.SetParent(canvas.transform, false);

        Stretch(panel.GetComponent<RectTransform>());

        return panel;
    }

    private void AddPanelBackground(GameObject panel, Sprite backgroundSprite)
    {
        if (panel == null)
            return;

        GameObject backgroundObject = new GameObject(
            "JungleBackground",
            typeof(RectTransform),
            typeof(Image));

        backgroundObject.transform.SetParent(panel.transform, false);
        backgroundObject.transform.SetAsFirstSibling();

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        Stretch(backgroundRect);

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;
        backgroundImage.raycastTarget = false;

        // Keep the original dark look until a background sprite is assigned.
        // Once assigned, the art is rendered fully so its details remain vivid.
        backgroundImage.color = backgroundSprite != null ? Color.white : backgroundColor;
    }

    private void BuildHomeScreen()
    {
        CreateAccent(homePanel, 0.925f);
        CreateDecorativeStar(homePanel, 0.08f, 0.825f, 54, Player1Orange);
        CreateDecorativeStar(homePanel, 0.83f, 0.745f, 48, Player2Blue);
        CreateDecorativeStar(homePanel, 0.13f, 0.585f, 34, Player2Blue);
        CreateDecorativeStar(homePanel, 0.86f, 0.565f, 34, Player1Orange);

        CreateText(homePanel, "PHOTO", 112, 0.785f, 0.105f, Player1Orange);
        CreateText(homePanel, "CHALLENGE", 91, 0.685f, 0.095f, Player2Blue);
        CreateText(homePanel, "THE OBJECT HUNT!", 34, 0.585f, 0.055f, Color.white);

        CreateOutlinedPlate(homePanel, "TwoPlayerBadge", 0.08f, 0.92f, 0.485f, 0.070f, Player2Blue, 5f);
        CreateText(homePanel, "2 PLAYERS  •  10-SECOND TURNS", 25, 0.485f, 0.060f, Color.white);

        CreateButton(homePanel, "LET'S PLAY!", 0.355f, Player1Orange, OpenSetup);
        CreateButton(homePanel, "HOW TO PLAY", 0.245f, BlueSignalColor, ShowHowTo);
        CreateButton(homePanel, "EXIT GAME", 0.135f, RedSignalColor, ExitGame);
    }

    private void BuildSetupScreen()
    {
        CreateAccent(setupPanel, 0.925f);
        CreateText(setupPanel, "PLAYER SETUP", 65, 0.845f, 0.075f, Color.white);
        CreateText(setupPanel, "PICK YOUR NAMES. GET READY!", 28, 0.775f, 0.045f, BlueSignalColor);

        CreateOutlinedPlate(setupPanel, "PlayerOneCard", 0.08f, 0.92f, 0.645f, 0.175f, Player1Orange, 6f);
        CreateTextAt(setupPanel, "PLAYER 1  •  ORANGE", 27, 0.13f, 0.87f, 0.695f, 0.040f, Player1Orange);
        player1Input = CreateInput(setupPanel, "ENTER PLAYER 1 NAME", 0.615f, Player1Orange, 0.14f, 0.86f);

        CreateOutlinedPlate(setupPanel, "PlayerTwoCard", 0.08f, 0.92f, 0.425f, 0.175f, Player2Blue, 6f);
        CreateTextAt(setupPanel, "PLAYER 2  •  BLUE", 27, 0.13f, 0.87f, 0.475f, 0.040f, Player2Blue);
        player2Input = CreateInput(setupPanel, "ENTER PLAYER 2 NAME", 0.395f, Player2Blue, 0.14f, 0.86f);

        setupStatusText = CreateText(setupPanel, "", 22, 0.315f, 0.040f, RedSignalColor);
        CreateButton(setupPanel, "START PLAYING", 0.225f, BlueSignalColor, StartGame);
        CreateButton(setupPanel, "BACK", 0.125f, Player1Orange, ShowHome);
    }

    private void BuildGameScreen()
    {
        // Keep the top rails above the header so the title never collides with them.
        CreateAccent(gamePanel, 0.965f);
        CreateDecorativeStar(gamePanel, 0.10f, 0.865f, 42, Player1Orange);
        CreateDecorativeStar(gamePanel, 0.90f, 0.865f, 42, Player2Blue);
        gameTitle = CreateText(gamePanel, "PLAYER'S TURN", 48, 0.895f, 0.058f, Color.white);
        CreateText(gamePanel, "READY? FIND THE OBJECT!", 27, 0.847f, 0.038f, BlueSignalColor);

        CreateOutlinedPlate(gamePanel, "PlayerOneScorePlate", 0.06f, 0.47f, 0.785f, 0.074f, Player1Orange, 5f);
        player1ScoreText = CreateTextAt(gamePanel, "PLAYER 1  •  0", 32, 0.075f, 0.455f, 0.785f, 0.064f, Player1Orange);

        CreateOutlinedPlate(gamePanel, "PlayerTwoScorePlate", 0.53f, 0.94f, 0.785f, 0.074f, Player2Blue, 5f);
        player2ScoreText = CreateTextAt(gamePanel, "PLAYER 2  •  0", 32, 0.545f, 0.925f, 0.785f, 0.064f, Player2Blue);

        CreateOutlinedPlate(gamePanel, "MissionCard", 0.06f, 0.94f, 0.680f, 0.105f, Player1Orange, 6f);
        missionText = CreateText(gamePanel, "FIND:\nA CHAIR", 46, 0.680f, 0.094f, Color.white);

        CreateSideLabel(gamePanel, "REFERENCE", 0.06f, 0.41f, 0.568f);
        CreateSideLabel(gamePanel, "CAMERA", 0.45f, 0.94f, 0.568f);
        CreateReferencePreview(gamePanel, 0.425f, 0.235f);
        CreateCameraPreview(gamePanel, 0.425f, 0.235f);

        timerText = CreateText(gamePanel, "TIME LEFT: 10s", 36, 0.267f, 0.048f, BlueSignalColor);
        statusText = CreateText(gamePanel, "AIM AT THE OBJECT. TAP TAKE PHOTO!", 25, 0.205f, 0.055f, Color.white);
        CreateButton(gamePanel, "TAKE PHOTO", 0.112f, TakePhotoGreen, CapturePhoto);
    }

    private void BuildResultScreen()
    {
        CreateAccent(resultPanel, 0.925f);
        CreateText(resultPanel, "ROUND COMPLETE!", 62, 0.845f, 0.075f, Color.white);
        CreateTrophyGraphic(resultPanel, 0.710f);
        resultText = CreateText(resultPanel, "PLAYER WINS!", 58, 0.585f, 0.078f, Player1Orange);

        CreateOutlinedPlate(resultPanel, "WinnerPlayerOnePlate", 0.07f, 0.48f, 0.445f, 0.105f, Player1Orange, 6f);
        resultP1ScoreText = CreateTextAt(resultPanel, "PLAYER 1\n0 POINTS", 27, 0.085f, 0.465f, 0.445f, 0.092f, Player1Orange);

        CreateOutlinedPlate(resultPanel, "WinnerPlayerTwoPlate", 0.52f, 0.93f, 0.445f, 0.105f, Player2Blue, 6f);
        resultP2ScoreText = CreateTextAt(resultPanel, "PLAYER 2\n0 POINTS", 27, 0.535f, 0.915f, 0.445f, 0.092f, Player2Blue);

        CreateButton(resultPanel, "PLAY AGAIN", 0.285f, Player1Orange, OpenSetup);
        CreateButton(resultPanel, "MAIN MENU", 0.175f, BlueSignalColor, ShowHome);
    }

    private void BuildHowToScreen()
    {
        CreateAccent(howToPanel, 0.925f);
        CreateText(howToPanel, "HOW TO PLAY", 70, 0.845f, 0.080f, Player2Blue);
        CreateText(howToPanel, "FOUR STEPS TO BECOME A PHOTO STAR", 24, 0.775f, 0.045f, Color.white);

        CreateOutlinedPlate(howToPanel, "HowToStepOne", 0.07f, 0.93f, 0.665f, 0.100f, Player1Orange, 5f);
        CreateText(howToPanel, "01  FIND THE OBJECT\nLook at the reference picture.", 27, 0.665f, 0.088f, Color.white);

        CreateOutlinedPlate(howToPanel, "HowToStepTwo", 0.07f, 0.93f, 0.535f, 0.100f, Player2Blue, 5f);
        CreateText(howToPanel, "02  BE QUICK!\nYou have 10 seconds each turn.", 27, 0.535f, 0.088f, Color.white);

        CreateOutlinedPlate(howToPanel, "HowToStepThree", 0.07f, 0.93f, 0.405f, 0.100f, Player1Orange, 5f);
        CreateText(howToPanel, "03  TAKE A PHOTO\nMatch the real object to the picture.", 27, 0.405f, 0.088f, Color.white);

        CreateOutlinedPlate(howToPanel, "HowToStepFour", 0.07f, 0.93f, 0.275f, 0.100f, Player2Blue, 5f);
        CreateText(howToPanel, "04  SCORE A POINT\nThe player with most points wins.", 27, 0.275f, 0.088f, Color.white);

        CreateButton(howToPanel, "BACK TO MENU", 0.125f, BlueSignalColor, ShowHome);
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
        text.fontSizeMin = Mathf.Max(20f, fontSize * 0.72f);
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        text.richText = true;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(xMin, y - height / 2f);
        rect.anchorMax = new Vector2(xMax, y + height / 2f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    private TMP_InputField CreateInput(GameObject parent, string hint, float y, Color borderColor,
        float xMin = 0.14f, float xMax = 0.86f)
    {
        GameObject obj = new GameObject(hint, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        obj.transform.SetParent(parent.transform, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, y - 0.031f);
        rect.anchorMax = new Vector2(xMax, y + 0.031f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image borderImage = obj.GetComponent<Image>();
        ApplyRoundedImage(borderImage, borderColor);
        borderImage.raycastTarget = true; // InputField needs a raycastable target graphic.

        GameObject face = new GameObject("InputFace", typeof(RectTransform), typeof(Image));
        face.transform.SetParent(obj.transform, false);
        RectTransform faceRect = face.GetComponent<RectTransform>();
        Stretch(faceRect);
        faceRect.offsetMin = new Vector2(4f, 4f);
        faceRect.offsetMax = new Vector2(-4f, -4f);
        ApplyRoundedImage(face.GetComponent<Image>(), backgroundColor);
        face.GetComponent<Image>().raycastTarget = false;

        TMP_InputField input = obj.GetComponent<TMP_InputField>();
        input.targetGraphic = borderImage;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 18;
        input.contentType = TMP_InputField.ContentType.Standard;

        TMP_Text valueText = CreateChildText(face.transform, 30);
        valueText.alignment = TextAlignmentOptions.Left;
        valueText.fontStyle = FontStyles.Bold;
        input.textComponent = valueText;

        TMP_Text placeholder = CreateChildText(face.transform, 24);
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
        text.fontSizeMin = Mathf.Max(20f, fontSize * 0.72f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(16f, 4f);
        rect.offsetMax = new Vector2(-16f, -4f);
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

    private void CreateCard(GameObject parent, float y, float height)
    {
        CreateOutlinedPlate(parent, "ArcadeCard", 0.06f, 0.94f, y, height, secondaryColor, 5f);
    }

    private void CreateAccent(GameObject parent, float y)
    {
        CreateColorStrip(parent.transform, "AccentOrange", 0.12f, 0.48f, y, y + 0.007f, Player1Orange);
        CreateColorStrip(parent.transform, "AccentBlue", 0.52f, 0.88f, y, y + 0.007f, Player2Blue);
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

    private void CreateDecorativeStar(GameObject parent, float x, float y, int fontSize, Color color)
    {
        GameObject starObject = new GameObject("ArcadeStar", typeof(RectTransform), typeof(Image));
        starObject.transform.SetParent(parent.transform, false);
        RectTransform rect = starObject.GetComponent<RectTransform>();
        float width = Mathf.Clamp(fontSize / 500f, 0.055f, 0.105f);
        float height = width * 1.05f;
        rect.anchorMin = new Vector2(x - width / 2f, y - height / 2f);
        rect.anchorMax = new Vector2(x + width / 2f, y + height / 2f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = starObject.GetComponent<Image>();
        image.sprite = GetStarUiSprite();
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    private void CreateTrophyGraphic(GameObject parent, float centerY)
    {
        // Draw a trophy using rounded UI shapes so it renders with the built-in TMP font too.
        CreateTrophyPiece(parent, "TrophyLeftHandle", 0.345f, 0.425f, centerY + 0.012f, 0.052f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyRightHandle", 0.575f, 0.655f, centerY + 0.012f, 0.052f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyLeftHandleCutout", 0.362f, 0.414f, centerY + 0.012f, 0.027f, backgroundColor);
        CreateTrophyPiece(parent, "TrophyRightHandleCutout", 0.586f, 0.638f, centerY + 0.012f, 0.027f, backgroundColor);
        Image cup = CreateTrophyPiece(parent, "TrophyCup", 0.405f, 0.595f, centerY + 0.020f, 0.080f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyStem", 0.480f, 0.520f, centerY - 0.040f, 0.035f, Player1Orange);
        CreateTrophyPiece(parent, "TrophyBase", 0.435f, 0.565f, centerY - 0.064f, 0.022f, Player1Orange);
        resultTrophyImages.Add(cup);
        // Keep all visible trophy parts synchronized with the winning player's color.
        foreach (Transform child in parent.transform)
        {
            if (child.name.StartsWith("Trophy") && child.name.Contains("Cutout") == false)
            {
                Image image = child.GetComponent<Image>();
                if (image != null && !resultTrophyImages.Contains(image))
                    resultTrophyImages.Add(image);
            }
        }
    }

    private Image CreateTrophyPiece(GameObject parent, string pieceName, float xMin, float xMax,
        float centerY, float height, Color color)
    {
        GameObject piece = new GameObject(pieceName, typeof(RectTransform), typeof(Image));
        piece.transform.SetParent(parent.transform, false);
        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, centerY - height / 2f);
        rect.anchorMax = new Vector2(xMax, centerY + height / 2f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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

    private void CreateButton(GameObject parent, string label, float y, Color color,
        UnityEngine.Events.UnityAction action)
    {
        float height = label == "TAKE PHOTO" ? 0.100f : 0.082f;
        float xMin = label == "BACK" ? 0.16f : 0.075f;
        float xMax = label == "BACK" ? 0.84f : 0.925f;

        GameObject root = new GameObject("ArcadeButton_" + label, typeof(RectTransform));
        root.transform.SetParent(parent.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(xMin, y - height / 2f);
        rootRect.anchorMax = new Vector2(xMax, y + height / 2f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // Shadow and glow are separate layers, keeping buttons chunky and easy to hit.
        GameObject shadow = CreateSurfaceChild(root.transform, "ButtonShadow", backgroundColor);
        RectTransform shadowRect = shadow.GetComponent<RectTransform>();
        Stretch(shadowRect);
        shadowRect.offsetMin = new Vector2(0f, -7f);
        shadowRect.offsetMax = new Vector2(0f, -7f);

        Color glowColor = new Color(color.r, color.g, color.b, 0.38f);
        GameObject glow = CreateSurfaceChild(root.transform, "ButtonGlow", glowColor);
        RectTransform glowRect = glow.GetComponent<RectTransform>();
        Stretch(glowRect);
        glowRect.offsetMin = new Vector2(-4f, -3f);
        glowRect.offsetMax = new Vector2(4f, 4f);

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

        int fontSize = label == "TAKE PHOTO" ? 38 : 34;
        TMP_Text text = CreateChildText(face.transform, fontSize);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
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

    private void ShowHome()
    {
        SetOnlyPanel(homePanel);
    }

    private void ShowHowTo()
    {
        SetOnlyPanel(howToPanel);
    }

    private void StartGame()
    {
        if (!similarityReady)
        {
            setupStatusText.text = similarityLoading
                ? "AI comparison model is loading. Please wait a moment."
                : "AI comparison is not ready. Check the model and reference images in Inspector.";
            return;
        }

        if (GetAvailableMissions().Count == 0)
        {
            setupStatusText.text = "Add at least one mission label and reference image to GameManager.";
            return;
        }

        players[0] = string.IsNullOrWhiteSpace(player1Input.text)
            ? "Player 1" : player1Input.text.Trim();

        players[1] = string.IsNullOrWhiteSpace(player2Input.text)
            ? "Player 2" : player2Input.text.Trim();

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
            gamePanel.SetActive(false);
            setupPanel.SetActive(true);
            setupStatusText.text = "No usable reference image found. Assign a Mission Label and Reference Image.";
            return;
        }

        homePanel.SetActive(false);
        setupPanel.SetActive(false);
        gamePanel.SetActive(true);
        resultPanel.SetActive(false);
        if (howToPanel != null) howToPanel.SetActive(false);

        gameTitle.text = players[currentPlayer] + "'S TURN";
        gameTitle.color = currentPlayer == 0 ? Player1Orange : Player2Blue;

        MissionReference selectedReference = DrawRandomMission(availableMissions);
        currentMission = selectedReference.missionLabel.Trim();
        Debug.Log("Photo Challenge: random mission '" + currentMission + "' assigned to " + players[currentPlayer] + ".");
        missionText.text = "FIND:\n" + currentMission;
        referencePreview.texture = selectedReference.referenceImage;
        referencePreview.color = Color.white;

        RefreshScoreText();

        photoCaptured = false;
        turnResolved = false;
        turnTimerRunning = false;
        turnTimeRemaining = secondsPerTurn;
        lastDisplayedSeconds = -1;
        UpdateTimerLabel();

        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.color = Color.white;
            statusText.text = "Match your photo to the reference. You have 10 seconds!";
            StartTurnTimer();
        }
        else
        {
            statusText.text = "Starting camera...";
            StartCoroutine(InitializeCamera());
        }
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

            // Avoid repeating the previous mission at the boundary between decks,
            // whenever there is more than one configured mission.
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

        if (webcamTexture == null ||
            !webcamTexture.isPlaying ||
            webcamTexture.width <= 16)
        {
            statusText.text =
                "Camera unavailable. Check permissions or try a phone.";
            return;
        }

        if (capturedPhoto != null)
            Destroy(capturedPhoto);

        capturedPhoto = new Texture2D(
            webcamTexture.width,
            webcamTexture.height,
            TextureFormat.RGB24,
            false);

        capturedPhoto.SetPixels(webcamTexture.GetPixels());
        capturedPhoto.Apply();

        // Freeze the captured image, then compare it with the mission's reference image.
        cameraPreview.texture = capturedPhoto;
        photoCaptured = true;

        statusText.text = "Comparing your photo with the reference...";
        CompareCapturedPhoto(capturedPhoto);
    }

    private void RefreshScoreText()
    {
        if (player1ScoreText != null)
            player1ScoreText.text = players[0].ToUpperInvariant() + "  •  " + scores[0];
        if (player2ScoreText != null)
            player2ScoreText.text = players[1].ToUpperInvariant() + "  •  " + scores[1];
    }

    private void StartTurnTimerIfGameIsActive()
    {
        if (gameStarted && gamePanel != null && gamePanel.activeSelf)
            StartTurnTimer();
    }

    private void StartTurnTimer()
    {
        if (!gameStarted || turnResolved) return;

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

        lastDisplayedSeconds = seconds;
        timerText.text = "TIME LEFT: " + seconds + "s";
        timerText.color = seconds <= 3 ? RedSignalColor : BlueSignalColor;
    }

    private void EndTurnForTimeout()
    {
        if (!gameStarted || turnResolved) return;

        turnTimerRunning = false;
        turnResolved = true;
        photoCaptured = false;
        timerText.text = "TIME'S UP!";
        statusText.text = "No point this turn. Next player...";

        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.color = Color.white;
        }

        StartCoroutine(AdvanceToNextPlayerAfterDelay());
    }

    private IEnumerator AdvanceToNextPlayerAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.0f);

        if (!gameStarted) yield break;

        turnsPlayed++;
        currentPlayer = 1 - currentPlayer;
        ShowMission();
    }

    private void AllowAnotherPhoto(string message)
    {
        photoCaptured = false;
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.color = Color.white;
        }
        statusText.text = message;
    }

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
            AllowAnotherPhoto("AI comparison is not ready. Check the model in Inspector.");
            return;
        }

        if (photo == null)
        {
            AllowAnotherPhoto("Photo unavailable. Please take another photo.");
            return;
        }

        string key = NormalizeMissionKey(currentMission);
        if (!referenceEmbeddings.TryGetValue(key, out List<float[]> referenceList) || referenceList.Count == 0)
        {
            AllowAnotherPhoto("There is no reference image for this mission. Check GameManager setup.");
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
                    timerText.text = "MATCH!";
                    statusText.text = "REFERENCE MATCH! Similarity: " + bestSimilarity.ToString("0.00") + ". Point awarded.";
                    StartCoroutine(AdvanceToNextPlayerAfterDelay());
                }
            }
            else
            {
                AllowAnotherPhoto("Not similar enough (" + bestSimilarity.ToString("0.00") +
                    "). Center the object and try again.");
            }
        }
        catch (Exception ex)
        {
            AllowAnotherPhoto("Image comparison failed. Check the Console, then try again.");
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
            .Trim('\uFEFF')
            .Trim('"')
            .Replace("_", " ")
            .ToLowerInvariant();
    }

    private void Update()
    {
        if (!gameStarted || !turnTimerRunning || turnResolved)
            return;

        turnTimeRemaining -= Time.unscaledDeltaTime;
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
        homePanel.SetActive(false);
        setupPanel.SetActive(false);
        gamePanel.SetActive(false);
        resultPanel.SetActive(true);
        if (howToPanel != null) howToPanel.SetActive(false);

        if (scores[0] > scores[1])
        {
            resultText.text = players[0].ToUpperInvariant() + " WINS!";
            resultText.color = Player1Orange;
            SetTrophyColor(Player1Orange);
        }
        else if (scores[1] > scores[0])
        {
            resultText.text = players[1].ToUpperInvariant() + " WINS!";
            resultText.color = Player2Blue;
            SetTrophyColor(Player2Blue);
        }
        else
        {
            resultText.text = "IT'S A DRAW!";
            resultText.color = BlueSignalColor;
            SetTrophyColor(BlueSignalColor);
        }

        if (resultP1ScoreText != null)
            resultP1ScoreText.text = players[0].ToUpperInvariant() + "\n" + scores[0] + " POINTS";
        if (resultP2ScoreText != null)
            resultP2ScoreText.text = players[1].ToUpperInvariant() + "\n" + scores[1] + " POINTS";
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

        photoCaptured = false;
    }
    private void ExitGame()
    {
        Debug.Log("Exit game requested.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void OnDestroy()
    {
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
