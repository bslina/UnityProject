using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // Settings popup, shared by the main menu and the pause menu.
    // Every control is optional: delete a row from the prefab and leave its slot empty.
    public class SettingsUI : UIPanel
    {
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Toggle vibrationToggle;
        [SerializeField] Toggle invertTiltToggle;
        [SerializeField] Slider tiltSensitivitySlider;
        [SerializeField] Toggle debugToggle;
        [SerializeField] Button resetProgressButton;
        [SerializeField] Text resetProgressLabel;
        [SerializeField] Button closeButton;
        [SerializeField] Text versionText;

        bool resetArmed;
        string resetLabel;

        protected override void Awake()
        {
            base.Awake();

            if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => GameSettings.MusicVolume = v);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => GameSettings.SfxVolume = v);
            if (vibrationToggle != null) vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);
            if (invertTiltToggle != null) invertTiltToggle.onValueChanged.AddListener(v => GameSettings.InvertTilt = v);
            if (tiltSensitivitySlider != null)
            {
                tiltSensitivitySlider.minValue = 0.5f;
                tiltSensitivitySlider.maxValue = 1.5f;
                tiltSensitivitySlider.onValueChanged.AddListener(v => GameSettings.TiltSensitivity = v);
            }
            if (debugToggle != null) debugToggle.onValueChanged.AddListener(v => GameSettings.ShowDebug = v);
            if (resetProgressButton != null) resetProgressButton.onClick.AddListener(OnResetPressed);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (resetProgressLabel != null) resetLabel = resetProgressLabel.text;
            if (versionText != null) versionText.text = "v" + Application.version;
        }

        public override void Show()
        {
            // Load the saved values without firing the change callbacks.
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            if (vibrationToggle != null) vibrationToggle.SetIsOnWithoutNotify(GameSettings.Vibration);
            if (invertTiltToggle != null) invertTiltToggle.SetIsOnWithoutNotify(GameSettings.InvertTilt);
            if (tiltSensitivitySlider != null) tiltSensitivitySlider.SetValueWithoutNotify(GameSettings.TiltSensitivity);
            if (debugToggle != null) debugToggle.SetIsOnWithoutNotify(GameSettings.ShowDebug);
            DisarmReset();
            base.Show();
        }

        void OnVibrationChanged(bool on)
        {
            GameSettings.Vibration = on;
            if (on) Haptics.Pulse(60);
        }

        // First tap asks for confirmation, second tap wipes the saved progress.
        void OnResetPressed()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                if (resetProgressLabel != null) resetProgressLabel.text = "Tap again to confirm";
                return;
            }
            SaveData.ResetAll(MiniGameRegistry.Instance);
            DisarmReset();
            if (resetProgressLabel != null) resetProgressLabel.text = "Progress reset";
            LevelSelectUI.RefreshAll();
        }

        void DisarmReset()
        {
            resetArmed = false;
            if (resetProgressLabel != null && resetLabel != null) resetProgressLabel.text = resetLabel;
        }
    }
}
