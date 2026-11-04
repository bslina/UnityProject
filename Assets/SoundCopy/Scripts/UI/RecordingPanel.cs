using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoundCopy.UI
{
    public sealed class RecordingPanel : MonoBehaviour
    {
        private const int SampleRate = 44100;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TextMeshProUGUI playerText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button backButton;
        [SerializeField] private RoundedRectGraphic recordGraphic;
        private int playerIndex = 1;
        private AudioClip microphoneClip;
        private bool recording;
        private float recordingDuration = 4f;

        public void Initialize(GameManager manager) => gameManager = manager;

        private void Awake()
        {
            recordButton?.onClick.AddListener(BeginRecording);
            continueButton?.onClick.AddListener(Continue);
            backButton?.onClick.AddListener(Back);
        }

        private void OnDisable() => StopMicrophone();

        private void OnDestroy()
        {
            recordButton?.onClick.RemoveListener(BeginRecording);
            continueButton?.onClick.RemoveListener(Continue);
            backButton?.onClick.RemoveListener(Back);
            StopMicrophone();
        }

        public void BuildUI()
        {
            ScreenScaffold.Clear(transform);
            GameObject shell = ScreenScaffold.Build(transform, "IMITATION", "À toi de jouer", "Reproduis le son pendant toute sa durée.");

            playerText = UIFactory.CreateText("PlayerName", shell.transform, "Joueur 1", 42f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            playerText.rectTransform.anchorMin = new Vector2(0.075f, 0.64f);
            playerText.rectTransform.anchorMax = new Vector2(0.925f, 0.70f);
            playerText.rectTransform.offsetMin = Vector2.zero;
            playerText.rectTransform.offsetMax = Vector2.zero;

            GameObject recordingCard = UIFactory.CreateRect("RecordingCard", shell.transform, new Vector2(0.075f, 0.34f), new Vector2(0.925f, 0.625f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(recordingCard, SoundCopyTheme.DarkPlum, 44f);
            UIFactory.AddNativeOutline(recordingCard, SoundCopyTheme.Ink, 3f);

            GameObject wave = UIFactory.CreateRect("Waveform", recordingCard.transform, new Vector2(0.07f, 0.18f), new Vector2(0.93f, 0.82f), Vector2.zero, Vector2.zero);
            float[] heights = { .22f, .42f, .66f, .35f, .78f, .48f, .28f, .58f, .86f, .44f, .70f, .31f, .55f, .76f, .38f, .64f, .26f };
            for (int i = 0; i < heights.Length; i++)
            {
                float width = 1f / heights.Length;
                float x = i * width + width * 0.31f;
                float h = heights[i];
                GameObject bar = UIFactory.CreateRect("Wave" + i, wave.transform,
                    new Vector2(x, 0.5f - h * 0.35f), new Vector2(x + width * 0.38f, 0.5f + h * 0.35f), Vector2.zero, Vector2.zero);
                UIFactory.AddRounded(bar, i % 4 == 0 ? SoundCopyTheme.Coral : SoundCopyTheme.Lime, 8f);
            }

            GameObject mic = UIFactory.CreateRect("RecordButton", recordingCard.transform, new Vector2(0.37f, 0.12f), new Vector2(0.63f, 0.88f), Vector2.zero, Vector2.zero);
            AspectRatioFitter aspect = mic.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1f;
            recordGraphic = UIFactory.AddRounded(mic, SoundCopyTheme.Lime, 120f, true);
            UIFactory.AddNativeOutline(mic, SoundCopyTheme.Cream, 4f);
            Shadow shadow = mic.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(0f, -9f);
            shadow.useGraphicAlpha = true;
            recordButton = mic.AddComponent<Button>();
            recordButton.targetGraphic = recordGraphic;

            GameObject micHead = UIFactory.CreateRect("MicHead", mic.transform, new Vector2(0.40f, 0.28f), new Vector2(0.60f, 0.70f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(micHead, SoundCopyTheme.Ink, 40f);
            GameObject micStem = UIFactory.CreateRect("MicStem", mic.transform, new Vector2(0.475f, 0.18f), new Vector2(0.525f, 0.36f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(micStem, SoundCopyTheme.Ink, 8f);
            GameObject micBase = UIFactory.CreateRect("MicBase", mic.transform, new Vector2(0.36f, 0.15f), new Vector2(0.64f, 0.21f), Vector2.zero, Vector2.zero);
            UIFactory.AddRounded(micBase, SoundCopyTheme.Ink, 8f);

            timerText = UIFactory.CreateText("Timer", shell.transform, "4.0", 60f, SoundCopyTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            timerText.rectTransform.anchorMin = new Vector2(0.075f, 0.275f);
            timerText.rectTransform.anchorMax = new Vector2(0.925f, 0.335f);
            timerText.rectTransform.offsetMin = Vector2.zero;
            timerText.rectTransform.offsetMax = Vector2.zero;
            statusText = UIFactory.CreateText("Status", shell.transform, "Appuie sur le bouton quand tu es prêt", 23f, SoundCopyTheme.MutedGray, TextAlignmentOptions.Center, FontStyles.Bold);
            statusText.rectTransform.anchorMin = new Vector2(0.075f, 0.225f);
            statusText.rectTransform.anchorMax = new Vector2(0.925f, 0.275f);
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;

            GameObject next = UIFactory.CreateRect("ContinueAction", shell.transform, new Vector2(0.075f, 0.13f), new Vector2(0.925f, 0.205f), Vector2.zero, Vector2.zero);
            continueButton = UIFactory.CreateButton("ContinueButton", next.transform, "Joueur suivant", SoundCopyTheme.Lime, SoundCopyTheme.Ink, 32f, out _);
            continueButton.interactable = false;
            GameObject back = UIFactory.CreateRect("BackAction", shell.transform, new Vector2(0.075f, 0.055f), new Vector2(0.43f, 0.105f), Vector2.zero, Vector2.zero);
            backButton = UIFactory.CreateButton("BackButton", back.transform, "<  Retour", SoundCopyTheme.Cream, SoundCopyTheme.Ink, 26f, out _, false);
        }

        public void Prepare(int index, string playerName)
        {
            StopAllCoroutines();
            StopMicrophone();
            playerIndex = index;
            microphoneClip = null;
            recording = false;
            recordingDuration = gameManager != null ? Mathf.Max(1f, gameManager.CurrentChallengeDuration) : 4f;
            if (playerText != null) playerText.text = playerName;
            if (timerText != null) timerText.text = recordingDuration.ToString("0.0");
            if (statusText != null) statusText.text = "Appuie sur le micro quand tu es prêt";
            if (recordButton != null) recordButton.interactable = true;
            if (continueButton != null)
            {
                continueButton.interactable = false;
                TextMeshProUGUI label = continueButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = index == 1 ? "Joueur suivant" : "Analyser avec l’IA";
            }
            if (recordGraphic != null) recordGraphic.color = index == 1 ? SoundCopyTheme.Lime : SoundCopyTheme.Coral;
        }

        private void BeginRecording()
        {
            if (!recording) StartCoroutine(RecordRoutine());
        }

        private IEnumerator RecordRoutine()
        {
            recordButton.interactable = false;
            statusText.text = "Autorisation du microphone…";
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                statusText.text = "Accès au microphone refusé";
                recordButton.interactable = true;
                yield break;
            }
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                statusText.text = "Aucun microphone détecté";
                recordButton.interactable = true;
                yield break;
            }

            int microphoneBufferSeconds = Mathf.CeilToInt(recordingDuration) + 1;
            microphoneClip = Microphone.Start(null, false, microphoneBufferSeconds, SampleRate);
            if (microphoneClip == null)
            {
                statusText.text = "Impossible de démarrer le microphone";
                recordButton.interactable = true;
                yield break;
            }
            recording = true;
            statusText.text = "ENREGISTREMENT EN COURS";
            float elapsed = 0f;
            while (elapsed < recordingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                timerText.text = Mathf.Max(0f, recordingDuration - elapsed).ToString("0.0");
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
            bool saved = gameManager != null && gameManager.StoreRecording(playerIndex, microphoneClip);
            recordButton.interactable = true;
            if (saved)
            {
                timerText.text = "OK";
                statusText.text = "Imitation enregistrée";
                continueButton.interactable = true;
            }
            else
            {
                timerText.text = "!";
                statusText.text = "Enregistrement impossible : vérifie l’espace disque";
                continueButton.interactable = false;
            }
        }

        private void StopMicrophone()
        {
            if (recording && Microphone.IsRecording(null)) Microphone.End(null);
            recording = false;
        }

        private void Continue() => gameManager?.OnRecordingContinue(playerIndex);
        private void Back() => gameManager?.OnRecordingBack();
    }
}
