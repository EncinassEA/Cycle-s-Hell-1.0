using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CyclesHell.Camcorder
{
    /// <summary>
    /// Sistema de videocámara found-footage año 2004 (estilo REC / Outlast).
    /// - Tecla C o clic derecho: encender/apagar el visor de la cámara.
    /// - Tecla F: activar/desactivar visión nocturna (solo si la cámara está encendida y hay batería).
    /// - Rueda del ratón: zoom suave entre 1X y 3X cuando la cámara está encendida.
    /// Usa exclusivamente UnityEngine.InputSystem y URP Global Volume.
    /// </summary>
    public class CamcorderController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Light infraredLight;
        [SerializeField] private Volume nightVisionVolume;

        [Header("Estado Inicial")]
        [SerializeField] private bool startWithCamcorderOn = true;

        [Header("Zoom (1X - 3X)")]
        [SerializeField] private float baseFieldOfView = 65f;
        [SerializeField] private float minZoom = 1f;
        [SerializeField] private float maxZoom = 3f;
        [SerializeField] private float zoomStepPerNotch = 0.25f;
        [SerializeField] private float zoomSmoothSpeed = 10f;

        [Header("Visión Nocturna y Batería")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float nightVisionDrainPerSecond = 2.5f;
        [SerializeField] private float volumeTransitionSpeed = 10f;
        [SerializeField] private float lowBatteryThreshold = 0.15f;
        [SerializeField] private float baseInfraredIntensity = 4.2f;

        private InputAction toggleCamcorderAction;
        private InputAction toggleNightVisionAction;
        private InputAction zoomScrollAction;

        private float currentBattery;
        private float targetZoom = 1f;
        private float currentZoom = 1f;
        private float currentVolumeWeight;

        public bool IsCamcorderOn { get; private set; }
        public bool IsNightVisionOn { get; private set; }
        public bool InputEnabled { get; set; } = true;
        public float CurrentBattery => currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryNormalized => maxBattery > 0f ? Mathf.Clamp01(currentBattery / maxBattery) : 0f;
        public float CurrentZoom => currentZoom;
        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;

        public event Action<bool> OnCamcorderStateChanged;
        public event Action<bool> OnNightVisionStateChanged;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<UnityEngine.Camera>();
                if (targetCamera == null)
                {
                    targetCamera = UnityEngine.Camera.main;
                }
            }

            if (targetCamera != null && baseFieldOfView <= 0f)
            {
                baseFieldOfView = targetCamera.fieldOfView;
            }

            if (infraredLight != null && baseInfraredIntensity <= 0f)
            {
                baseInfraredIntensity = infraredLight.intensity;
            }

            currentBattery = maxBattery;
            targetZoom = minZoom;
            currentZoom = minZoom;

            SetupInputActions();
        }

        private void SetupInputActions()
        {
            toggleCamcorderAction = new InputAction("ToggleCamcorder", InputActionType.Button);
            toggleCamcorderAction.AddBinding("<Keyboard>/c");
            toggleCamcorderAction.AddBinding("<Mouse>/rightButton");

            toggleNightVisionAction = new InputAction("ToggleNightVision", InputActionType.Button);
            toggleNightVisionAction.AddBinding("<Keyboard>/f");

            zoomScrollAction = new InputAction("ZoomScroll", InputActionType.Value, "<Mouse>/scroll");
        }

        private void OnEnable()
        {
            toggleCamcorderAction?.Enable();
            toggleNightVisionAction?.Enable();
            zoomScrollAction?.Enable();
        }

        private void OnDisable()
        {
            toggleCamcorderAction?.Disable();
            toggleNightVisionAction?.Disable();
            zoomScrollAction?.Disable();
        }

        private void OnDestroy()
        {
            toggleCamcorderAction?.Dispose();
            toggleNightVisionAction?.Dispose();
            zoomScrollAction?.Dispose();
        }

        private void Start()
        {
            SetCamcorderActive(startWithCamcorderOn, notify: true);
            SetNightVisionActive(false, notify: true);
            ApplyImmediateVisualState();
        }

        private void Update()
        {
            HandleInput();
            UpdateBattery();
            UpdateZoom();
            UpdateNightVisionEffects();
        }

        private void HandleInput()
        {
            if (!InputEnabled)
            {
                return;
            }

            if (toggleCamcorderAction.WasPressedThisFrame())
            {
                SetCamcorderActive(!IsCamcorderOn, notify: true);
            }

            if (toggleNightVisionAction.WasPressedThisFrame())
            {
                if (IsCamcorderOn)
                {
                    if (IsNightVisionOn)
                    {
                        SetNightVisionActive(false, notify: true);
                    }
                    else if (currentBattery > 0.01f)
                    {
                        SetNightVisionActive(true, notify: true);
                    }
                }
            }

            if (IsCamcorderOn)
            {
                Vector2 scrollValue = zoomScrollAction.ReadValue<Vector2>();
                if (Mathf.Abs(scrollValue.y) > 0.01f)
                {
                    float direction = Mathf.Sign(scrollValue.y);
                    targetZoom = Mathf.Clamp(targetZoom + direction * zoomStepPerNotch, minZoom, maxZoom);
                }
            }
        }

        private void UpdateBattery()
        {
            if (!IsNightVisionOn)
            {
                return;
            }

            currentBattery = Mathf.Max(0f, currentBattery - nightVisionDrainPerSecond * Time.deltaTime);
            if (currentBattery <= 0f)
            {
                SetNightVisionActive(false, notify: true);
            }
        }

        private void UpdateZoom()
        {
            float desiredZoom = IsCamcorderOn ? targetZoom : minZoom;
            float smooth = 1f - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime);
            currentZoom = Mathf.Lerp(currentZoom, desiredZoom, smooth);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseFieldOfView / Mathf.Max(1f, currentZoom);
            }
        }

        private void UpdateNightVisionEffects()
        {
            float targetWeight = (IsCamcorderOn && IsNightVisionOn) ? 1f : 0f;
            float smooth = 1f - Mathf.Exp(-volumeTransitionSpeed * Time.deltaTime);
            currentVolumeWeight = Mathf.Lerp(currentVolumeWeight, targetWeight, smooth);

            if (Mathf.Abs(currentVolumeWeight - targetWeight) < 0.002f)
            {
                currentVolumeWeight = targetWeight;
            }

            if (nightVisionVolume != null)
            {
                nightVisionVolume.weight = currentVolumeWeight;
            }

            if (infraredLight != null)
            {
                bool shouldLightBeOn = IsCamcorderOn && IsNightVisionOn;
                if (infraredLight.enabled != shouldLightBeOn)
                {
                    infraredLight.enabled = shouldLightBeOn;
                }

                if (shouldLightBeOn)
                {
                    float flicker = 1f;
                    if (BatteryNormalized < lowBatteryThreshold)
                    {
                        float noise = Mathf.PerlinNoise(Time.time * 18f, 0.37f);
                        flicker = noise < 0.32f ? 0.35f : Mathf.Lerp(0.7f, 1.05f, noise);
                    }
                    infraredLight.intensity = baseInfraredIntensity * flicker;
                }
            }
        }

        private void ApplyImmediateVisualState()
        {
            currentVolumeWeight = (IsCamcorderOn && IsNightVisionOn) ? 1f : 0f;

            if (nightVisionVolume != null)
            {
                nightVisionVolume.weight = currentVolumeWeight;
            }

            if (infraredLight != null)
            {
                infraredLight.enabled = IsCamcorderOn && IsNightVisionOn;
                infraredLight.intensity = baseInfraredIntensity;
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseFieldOfView / Mathf.Max(1f, currentZoom);
            }
        }

        public void SetCamcorderActive(bool active, bool notify = true)
        {
            IsCamcorderOn = active;
            if (!IsCamcorderOn && IsNightVisionOn)
            {
                SetNightVisionActive(false, notify);
            }

            if (notify)
            {
                OnCamcorderStateChanged?.Invoke(IsCamcorderOn);
            }
        }

        public void SetNightVisionActive(bool active, bool notify = true)
        {
            bool nextState = active && IsCamcorderOn && currentBattery > 0.01f;
            IsNightVisionOn = nextState;

            if (notify)
            {
                OnNightVisionStateChanged?.Invoke(IsNightVisionOn);
            }
        }

        /// <summary>
        /// Permite recargar la batería (por ejemplo, al recoger pilas en niveles futuros).
        /// </summary>
        public void RechargeBattery(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentBattery = Mathf.Clamp(currentBattery + amount, 0f, maxBattery);
        }
    }
}
