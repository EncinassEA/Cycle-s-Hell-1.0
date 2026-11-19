using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace CyclesHell.Camcorder
{
    /// <summary>
    /// Controlador del HUD de videocámara 2004 ("CamcorderHUD") usando uGUI y TextMeshPro.
    /// Muestra esquinas del visor, barra de batería, indicador REC parpadeante,
    /// fecha "NOV. 08. 2004" con reloj en vivo iniciando en 23:14:48, e indicador de zoom 1X-3X.
    /// </summary>
    public class CamcorderHUDController : MonoBehaviour
    {
        [Header("Referencia Principal")]
        [SerializeField] private CamcorderController camcorder;
        [SerializeField] private GameObject viewfinderRoot;

        [Header("Batería (Arriba Izquierda)")]
        [SerializeField] private UnityEngine.UI.Image batteryFillImage;
        [SerializeField] private TMP_Text batteryPercentText;
        [SerializeField] private Color batteryHealthyColor = new Color(0.92f, 0.96f, 0.92f, 0.92f);
        [SerializeField] private Color batteryNightVisionColor = new Color(0.45f, 1f, 0.55f, 0.95f);
        [SerializeField] private Color batteryWarningColor = new Color(1f, 0.72f, 0.18f, 0.95f);
        [SerializeField] private Color batteryCriticalColor = new Color(0.95f, 0.18f, 0.18f, 0.98f);

        [Header("REC, Lámpara y Visión Nocturna (Arriba Derecha)")]
        [SerializeField] private UnityEngine.UI.Image recDotImage;
        [SerializeField] private TMP_Text recText;
        [SerializeField] private TMP_Text nightVisionBadgeText;
        [SerializeField] private TMP_Text lampStatusText;
        [SerializeField] private Color lampOnColor = new Color(1f, 0.94f, 0.78f, 0.95f);
        [SerializeField] private Color lampOffColor = new Color(0.72f, 0.75f, 0.72f, 0.55f);
        [SerializeField] private float recBlinkInterval = 0.55f;

        [Header("Fecha y Reloj (Abajo Izquierda)")]
        [SerializeField] private TMP_Text dateTimeText;
        [SerializeField] private int startYear = 2004;
        [SerializeField] private int startMonth = 11;
        [SerializeField] private int startDay = 8;
        [SerializeField] private int startHour = 23;
        [SerializeField] private int startMinute = 14;
        [SerializeField] private int startSecond = 48;

        [Header("Zoom (Abajo Derecha)")]
        [SerializeField] private TMP_Text zoomText;
        [SerializeField] private UnityEngine.UI.Image zoomFillImage;

        [Header("Flash Morado (UV)")]
        [SerializeField] private TMP_Text uvFlashStatusText;
        [SerializeField] private UnityEngine.UI.Image uvFlashScreenOverlay;
        [SerializeField] private Color uvReadyColor = new Color(0.80f, 0.45f, 1f, 0.96f);
        [SerializeField] private Color uvCooldownColor = new Color(0.75f, 0.65f, 0.82f, 0.65f);
        [SerializeField] private float uvOverlayPeakAlpha = 0.42f;
        [SerializeField] private float uvOverlayFadeSpeed = 1.6f;

        [Header("Tinte del Visor")]
        [SerializeField] private UnityEngine.UI.Image[] viewfinderBracketImages;
        [SerializeField] private Color normalBracketColor = new Color(1f, 1f, 1f, 0.72f);
        [SerializeField] private Color nightVisionBracketColor = new Color(0.5f, 1f, 0.58f, 0.85f);

        private DateTime baseDateTime;
        private double elapsedClockSeconds;
        private int lastDisplayedSecond = -1;
        private float blinkTimer;
        private bool recDotVisible = true;
        private float currentUvOverlayAlpha;

        private void Awake()
        {
            if (camcorder == null)
            {
                camcorder = FindAnyObjectByType<CamcorderController>();
            }

            baseDateTime = new DateTime(startYear, startMonth, startDay, startHour, startMinute, startSecond);
        }

        private void OnEnable()
        {
            if (camcorder != null)
            {
                camcorder.OnCamcorderStateChanged += HandleCamcorderToggled;
                camcorder.OnNightVisionStateChanged += HandleNightVisionToggled;
                camcorder.OnLampStateChanged += HandleLampToggled;
                camcorder.OnUvFlashFired += HandleUvFlashFired;
            }
        }

        private void OnDisable()
        {
            if (camcorder != null)
            {
                camcorder.OnCamcorderStateChanged -= HandleCamcorderToggled;
                camcorder.OnNightVisionStateChanged -= HandleNightVisionToggled;
                camcorder.OnLampStateChanged -= HandleLampToggled;
                camcorder.OnUvFlashFired -= HandleUvFlashFired;
            }
        }

        private void Start()
        {
            if (camcorder != null)
            {
                HandleCamcorderToggled(camcorder.IsCamcorderOn);
                HandleNightVisionToggled(camcorder.IsNightVisionOn);
                HandleLampToggled(camcorder.IsLampOn);
            }

            UpdateDateTime(forceUpdate: true);
            UpdateBatteryUI();
            UpdateZoomUI();
        }

        private void Update()
        {
            // El reloj interno de la cinta sigue corriendo incluso si se baja el visor.
            elapsedClockSeconds += Time.deltaTime;

            if (camcorder == null || !camcorder.IsCamcorderOn)
            {
                return;
            }

            UpdateRecBlink();
            UpdateDateTime(forceUpdate: false);
            UpdateBatteryUI();
            UpdateZoomUI();
            UpdateUvFlashUI();
        }

        private void HandleUvFlashFired()
        {
            currentUvOverlayAlpha = uvOverlayPeakAlpha;
        }

        private void UpdateUvFlashUI()
        {
            if (uvFlashScreenOverlay != null)
            {
                if (currentUvOverlayAlpha > 0f)
                {
                    currentUvOverlayAlpha = Mathf.MoveTowards(currentUvOverlayAlpha, 0f, uvOverlayFadeSpeed * Time.deltaTime);
                }
                Color c = uvFlashScreenOverlay.color;
                c.a = currentUvOverlayAlpha;
                uvFlashScreenOverlay.color = c;
            }

            if (uvFlashStatusText == null || camcorder == null) return;

            if (camcorder.UvFlashCooldownRemaining > 0.05f)
            {
                uvFlashStatusText.text = string.Format(CultureInfo.InvariantCulture, "UV FLASH: {0:0.0}S", camcorder.UvFlashCooldownRemaining);
                uvFlashStatusText.color = uvCooldownColor;
            }
            else if (!camcorder.HasEnoughBatteryForUv)
            {
                uvFlashStatusText.text = "UV FLASH: SIN BAT.";
                uvFlashStatusText.color = batteryCriticalColor;
            }
            else
            {
                uvFlashStatusText.text = "UV FLASH [Q]: LISTO";
                uvFlashStatusText.color = uvReadyColor;
            }
        }

        private void HandleCamcorderToggled(bool isOn)
        {
            if (viewfinderRoot != null)
            {
                viewfinderRoot.SetActive(isOn);
            }

            if (isOn)
            {
                blinkTimer = 0f;
                recDotVisible = true;
                if (recDotImage != null)
                {
                    recDotImage.enabled = true;
                }
                UpdateDateTime(forceUpdate: true);
                UpdateBatteryUI();
                UpdateZoomUI();
            }
        }

        private void HandleNightVisionToggled(bool isNightVisionOn)
        {
            if (nightVisionBadgeText != null)
            {
                nightVisionBadgeText.gameObject.SetActive(isNightVisionOn);
            }

            if (camcorder != null)
            {
                HandleLampToggled(camcorder.IsLampOn);
            }

            Color targetBracketColor = isNightVisionOn ? nightVisionBracketColor : normalBracketColor;
            if (viewfinderBracketImages != null)
            {
                for (int i = 0; i < viewfinderBracketImages.Length; i++)
                {
                    if (viewfinderBracketImages[i] != null)
                    {
                        viewfinderBracketImages[i].color = targetBracketColor;
                    }
                }
            }
        }

        private void HandleLampToggled(bool isLampOn)
        {
            if (lampStatusText == null)
            {
                return;
            }

            lampStatusText.text = isLampOn ? "LIGHT: ON" : "LIGHT: OFF";
            lampStatusText.color = isLampOn ? lampOnColor : lampOffColor;
        }

        private void UpdateRecBlink()
        {
            if (recDotImage == null)
            {
                return;
            }

            blinkTimer += Time.deltaTime;
            if (blinkTimer >= recBlinkInterval)
            {
                blinkTimer -= recBlinkInterval;
                recDotVisible = !recDotVisible;
                recDotImage.enabled = recDotVisible;
            }
        }

        private void UpdateDateTime(bool forceUpdate)
        {
            if (dateTimeText == null)
            {
                return;
            }

            int wholeSeconds = (int)elapsedClockSeconds;
            if (!forceUpdate && wholeSeconds == lastDisplayedSecond)
            {
                return;
            }

            lastDisplayedSecond = wholeSeconds;
            DateTime current = baseDateTime.AddSeconds(wholeSeconds);
            string monthAbbr = current.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            string formatted = string.Format(
                CultureInfo.InvariantCulture,
                "{0}. {1:00}. {2:0000}\n{3:00}:{4:00}:{5:00}",
                monthAbbr,
                current.Day,
                current.Year,
                current.Hour,
                current.Minute,
                current.Second);

            dateTimeText.text = formatted;
        }

        private void UpdateBatteryUI()
        {
            if (camcorder == null)
            {
                return;
            }

            float normalized = camcorder.BatteryNormalized;

            if (batteryFillImage != null)
            {
                batteryFillImage.fillAmount = normalized;

                Color barColor;
                if (normalized <= 0.15f)
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 10f);
                    barColor = Color.Lerp(batteryCriticalColor * 0.6f, batteryCriticalColor, pulse);
                }
                else if (normalized <= 0.30f)
                {
                    barColor = batteryWarningColor;
                }
                else
                {
                    barColor = camcorder.IsNightVisionOn ? batteryNightVisionColor : batteryHealthyColor;
                }

                batteryFillImage.color = barColor;
            }

            if (batteryPercentText != null)
            {
                int percent = Mathf.RoundToInt(normalized * 100f);
                batteryPercentText.text = $"{percent}%";
            }
        }

        private void UpdateZoomUI()
        {
            if (camcorder == null)
            {
                return;
            }

            float zoom = camcorder.CurrentZoom;
            if (zoomText != null)
            {
                zoomText.text = string.Format(CultureInfo.InvariantCulture, "ZOOM {0:0.0}X", zoom);
            }

            if (zoomFillImage != null)
            {
                float t = Mathf.InverseLerp(camcorder.MinZoom, camcorder.MaxZoom, zoom);
                zoomFillImage.fillAmount = Mathf.Clamp01(t);
            }
        }
    }
}
