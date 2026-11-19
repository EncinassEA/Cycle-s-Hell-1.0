using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CyclesHell.UI
{
    /// <summary>
    /// Controlador de la escena Configuracion.unity con estética VHS 2004:
    /// Sliders de Volumen General, Brillo (con previsualización en vivo) y Sensibilidad del ratón.
    /// Usa exclusivamente el Nuevo Input System.
    /// </summary>
    public class SettingsMenuUI : MonoBehaviour
    {
        [Header("Sliders")]
        [SerializeField] private UnityEngine.UI.Slider volumeSlider;
        [SerializeField] private UnityEngine.UI.Slider brightnessSlider;
        [SerializeField] private UnityEngine.UI.Slider sensitivitySlider;

        [Header("Etiquetas de Valores")]
        [SerializeField] private TMP_Text volumeValueText;
        [SerializeField] private TMP_Text brightnessValueText;
        [SerializeField] private TMP_Text sensitivityValueText;

        [Header("Previsualización de Brillo")]
        [SerializeField] private UnityEngine.UI.Image brightnessPreviewOverlay;

        [Header("Botones y Navegación")]
        [SerializeField] private UnityEngine.UI.Button resetButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private string mainMenuSceneName = "SampleScene";

        private InputAction backAction;
        private bool suppressCallbacks;

        private void Awake()
        {
            backAction = new InputAction("BackToMenu", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            backAction?.Enable();

            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            if (brightnessSlider != null) brightnessSlider.onValueChanged.AddListener(HandleBrightnessChanged);
            if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
            if (resetButton != null) resetButton.onClick.AddListener(HandleResetClicked);
            if (backButton != null) backButton.onClick.AddListener(ReturnToMainMenu);
        }

        private void OnDisable()
        {
            backAction?.Disable();

            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
            if (brightnessSlider != null) brightnessSlider.onValueChanged.RemoveListener(HandleBrightnessChanged);
            if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
            if (resetButton != null) resetButton.onClick.RemoveListener(HandleResetClicked);
            if (backButton != null) backButton.onClick.RemoveListener(ReturnToMainMenu);
        }

        private void OnDestroy()
        {
            backAction?.Dispose();
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameSettingsManager.ApplySavedSettings();
            SyncUIFromManager();
        }

        private void Update()
        {
            if (backAction.WasPressedThisFrame())
            {
                ReturnToMainMenu();
            }
        }

        private void SyncUIFromManager()
        {
            suppressCallbacks = true;

            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.value = GameSettingsManager.MasterVolume;
            }

            if (brightnessSlider != null)
            {
                brightnessSlider.minValue = GameSettingsManager.MinBrightness;
                brightnessSlider.maxValue = GameSettingsManager.MaxBrightness;
                brightnessSlider.value = GameSettingsManager.Brightness;
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = GameSettingsManager.MinSensitivity;
                sensitivitySlider.maxValue = GameSettingsManager.MaxSensitivity;
                sensitivitySlider.value = GameSettingsManager.MouseSensitivity;
            }

            suppressCallbacks = false;
            RefreshLabelsAndPreview();
        }

        private void HandleVolumeChanged(float value)
        {
            if (suppressCallbacks) return;
            GameSettingsManager.SetMasterVolume(value);
            RefreshLabelsAndPreview();
        }

        private void HandleBrightnessChanged(float value)
        {
            if (suppressCallbacks) return;
            GameSettingsManager.SetBrightness(value);
            RefreshLabelsAndPreview();
        }

        private void HandleSensitivityChanged(float value)
        {
            if (suppressCallbacks) return;
            GameSettingsManager.SetMouseSensitivity(value);
            RefreshLabelsAndPreview();
        }

        private void HandleResetClicked()
        {
            GameSettingsManager.ResetToDefaults();
            SyncUIFromManager();
        }

        private void RefreshLabelsAndPreview()
        {
            if (volumeValueText != null)
            {
                volumeValueText.text = $"{Mathf.RoundToInt(GameSettingsManager.MasterVolume * 100f)}%";
            }

            if (brightnessValueText != null)
            {
                float b = GameSettingsManager.Brightness;
                string sign = b >= 0f ? "+" : "";
                brightnessValueText.text = string.Format(CultureInfo.InvariantCulture, "{0}{1:0.00} EV", sign, b);
            }

            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", GameSettingsManager.MouseSensitivity);
            }

            if (brightnessPreviewOverlay != null)
            {
                // Previsualización inmediata en la tarjeta de calibración de brillo
                float norm = Mathf.InverseLerp(GameSettingsManager.MinBrightness, GameSettingsManager.MaxBrightness, GameSettingsManager.Brightness);
                float gray = Mathf.Lerp(0.06f, 0.58f, norm);
                brightnessPreviewOverlay.color = new Color(gray, gray * 1.04f, gray, 1f);
            }
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }
    }
}
