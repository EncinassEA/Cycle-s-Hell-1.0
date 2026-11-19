using System.Globalization;
using TMPro;
using UnityEngine;

namespace CyclesHell.Survival
{
    /// <summary>
    /// Controlador de UI de supervivencia e interacción integrado en el Canvas "CamcorderHUD".
    /// Gestiona barra de salud retro 2004, contadores de botiquines/baterías/llaves,
    /// destello rojo de daño en bordes, mira central, prompts de interacción, lector de notas y pantalla de muerte VHS.
    /// </summary>
    public class SurvivalHUDController : MonoBehaviour
    {
        [Header("Referencias de Jugador")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerInventory playerInventory;

        [Header("Salud (Estética Retro 2004)")]
        [SerializeField] private UnityEngine.UI.Image healthFillImage;
        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private Color healthHighColor = new Color(0.32f, 0.90f, 0.42f, 0.92f);
        [SerializeField] private Color healthMidColor = new Color(0.96f, 0.78f, 0.20f, 0.94f);
        [SerializeField] private Color healthLowColor = new Color(0.92f, 0.18f, 0.18f, 0.96f);

        [Header("Contadores de Suministros")]
        [SerializeField] private TMP_Text suppliesCounterText;
        [SerializeField] private TMP_Text keyBadgeText;

        [Header("Feedback de Daño (Bordes Rojos)")]
        [SerializeField] private UnityEngine.UI.Image damageVignetteImage;
        [SerializeField] private float damageFlashPeakAlpha = 0.75f;
        [SerializeField] private float damageFlashFadeSpeed = 2.4f;

        [Header("Mira y Prompts de Interacción")]
        [SerializeField] private UnityEngine.UI.Image centerDotImage;
        [SerializeField] private Color centerDotIdleColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color centerDotActiveColor = new Color(0.55f, 1f, 0.62f, 0.95f);
        [SerializeField] private GameObject interactionPromptRoot;
        [SerializeField] private TMP_Text interactionPromptText;
        [SerializeField] private TMP_Text statusToastText;
        [SerializeField] private float statusToastDuration = 2.6f;

        [Header("Panel de Lectura de Expediente")]
        [SerializeField] private GameObject notePanelRoot;
        [SerializeField] private TMP_Text noteTitleText;
        [SerializeField] private TMP_Text noteDateText;
        [SerializeField] private TMP_Text noteBodyText;

        [Header("Pantalla de Muerte VHS")]
        [SerializeField] private GameObject deathScreenRoot;
        [SerializeField] private TMP_Text deathTitleText;
        [SerializeField] private UnityEngine.UI.Button restartButton;

        private float currentDamageAlpha;
        private float toastTimer;

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            if (playerInventory == null)
            {
                playerInventory = FindAnyObjectByType<PlayerInventory>();
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
                playerHealth.OnDamaged += HandleDamaged;
                playerHealth.OnPlayerDied += HandlePlayerDied;
                playerHealth.OnStatusMessage += ShowStatusMessage;
            }

            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged += UpdateSuppliesUI;
                playerInventory.OnStatusMessage += ShowStatusMessage;
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(HandleRestartButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
                playerHealth.OnDamaged -= HandleDamaged;
                playerHealth.OnPlayerDied -= HandlePlayerDied;
                playerHealth.OnStatusMessage -= ShowStatusMessage;
            }

            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged -= UpdateSuppliesUI;
                playerInventory.OnStatusMessage -= ShowStatusMessage;
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(HandleRestartButtonClicked);
            }
        }

        private void Start()
        {
            if (damageVignetteImage != null)
            {
                Color c = damageVignetteImage.color;
                c.a = 0f;
                damageVignetteImage.color = c;
            }

            if (interactionPromptRoot != null)
            {
                interactionPromptRoot.SetActive(false);
            }

            if (statusToastText != null)
            {
                statusToastText.gameObject.SetActive(false);
            }

            if (notePanelRoot != null)
            {
                notePanelRoot.SetActive(false);
            }

            if (deathScreenRoot != null)
            {
                deathScreenRoot.SetActive(false);
            }

            if (playerHealth != null)
            {
                HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }

            UpdateSuppliesUI();
        }

        private void Update()
        {
            UpdateDamageVignette();
            UpdateStatusToast();
            UpdateDeathScreenFlicker();
        }

        private void HandleHealthChanged(float current, float max)
        {
            float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = normalized;
                if (normalized > 0.6f)
                {
                    healthFillImage.color = healthHighColor;
                }
                else if (normalized > 0.3f)
                {
                    healthFillImage.color = healthMidColor;
                }
                else
                {
                    healthFillImage.color = healthLowColor;
                }
            }

            if (healthValueText != null)
            {
                healthValueText.text = string.Format(CultureInfo.InvariantCulture, "SALUD {0:0}", Mathf.CeilToInt(current));
            }
        }

        private void HandleDamaged(float amount)
        {
            currentDamageAlpha = damageFlashPeakAlpha;
        }

        private void HandlePlayerDied()
        {
            HideInteractionPrompt();
            HideNote();

            if (centerDotImage != null)
            {
                centerDotImage.gameObject.SetActive(false);
            }

            if (deathScreenRoot != null)
            {
                deathScreenRoot.SetActive(true);
            }
        }

        private void HandleRestartButtonClicked()
        {
            if (playerHealth != null)
            {
                playerHealth.RestartCurrentScene();
            }
        }

        private void UpdateSuppliesUI()
        {
            if (playerInventory == null) return;

            if (suppliesCounterText != null)
            {
                suppliesCounterText.text = $"[H] BOTIQ: {playerInventory.MedkitCount}   |   [B] BAT: {playerInventory.BatteryCount}";
            }

            if (keyBadgeText != null)
            {
                bool hasLabKey = playerInventory.HasKey("LabKey");
                keyBadgeText.gameObject.SetActive(hasLabKey);
            }
        }

        private void UpdateDamageVignette()
        {
            if (damageVignetteImage == null) return;

            if (currentDamageAlpha > 0f)
            {
                currentDamageAlpha = Mathf.MoveTowards(currentDamageAlpha, 0f, damageFlashFadeSpeed * Time.deltaTime);
            }

            float lowHealthPulse = 0f;
            if (playerHealth != null && !playerHealth.IsDead && playerHealth.HealthNormalized <= 0.25f)
            {
                lowHealthPulse = 0.14f + 0.08f * Mathf.Sin(Time.time * 4.5f);
            }

            Color c = damageVignetteImage.color;
            c.a = Mathf.Clamp01(Mathf.Max(currentDamageAlpha, lowHealthPulse));
            damageVignetteImage.color = c;
        }

        private void UpdateStatusToast()
        {
            if (statusToastText == null || toastTimer <= 0f) return;

            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f)
            {
                statusToastText.gameObject.SetActive(false);
            }
            else if (toastTimer < 0.6f)
            {
                Color c = statusToastText.color;
                c.a = Mathf.Clamp01(toastTimer / 0.6f);
                statusToastText.color = c;
            }
        }

        private void UpdateDeathScreenFlicker()
        {
            if (deathScreenRoot == null || !deathScreenRoot.activeSelf || deathTitleText == null)
            {
                return;
            }

            float flicker = 0.80f + 0.20f * Mathf.PerlinNoise(Time.unscaledTime * 16f, 1.3f);
            Color c = deathTitleText.color;
            c.a = flicker;
            deathTitleText.color = c;
        }

        public void ShowInteractionPrompt(string prompt)
        {
            if (interactionPromptRoot != null && !interactionPromptRoot.activeSelf)
            {
                interactionPromptRoot.SetActive(true);
            }

            if (interactionPromptText != null)
            {
                interactionPromptText.text = prompt;
            }

            if (centerDotImage != null)
            {
                centerDotImage.color = centerDotActiveColor;
                centerDotImage.rectTransform.localScale = Vector3.one * 1.45f;
            }
        }

        public void HideInteractionPrompt()
        {
            if (interactionPromptRoot != null && interactionPromptRoot.activeSelf)
            {
                interactionPromptRoot.SetActive(false);
            }

            if (centerDotImage != null)
            {
                centerDotImage.color = centerDotIdleColor;
                centerDotImage.rectTransform.localScale = Vector3.one;
            }
        }

        public void ShowStatusMessage(string message)
        {
            if (statusToastText == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            statusToastText.text = message;
            Color c = statusToastText.color;
            c.a = 1f;
            statusToastText.color = c;
            statusToastText.gameObject.SetActive(true);
            toastTimer = statusToastDuration;
        }

        public void ShowNote(string title, string date, string body)
        {
            if (notePanelRoot == null) return;

            if (noteTitleText != null) noteTitleText.text = title;
            if (noteDateText != null) noteDateText.text = date;
            if (noteBodyText != null) noteBodyText.text = body;

            HideInteractionPrompt();
            notePanelRoot.SetActive(true);
        }

        public void HideNote()
        {
            if (notePanelRoot != null && notePanelRoot.activeSelf)
            {
                notePanelRoot.SetActive(false);
            }
        }
    }
}
