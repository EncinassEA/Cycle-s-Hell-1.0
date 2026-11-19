using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CyclesHell.Camcorder
{
    /// <summary>
    /// Sistema de videocámara found-footage año 2004 (estilo REC / Outlast).
    /// - Tecla C o clic derecho: encender/apagar el visor de la cámara.
    /// - Tecla L: encender/apagar la lámpara halógena frontal (solo si la cámara está encendida y sin visión nocturna).
    /// - Tecla F: activar/desactivar visión nocturna (apaga automáticamente la lámpara blanca para no quemar la imagen).
    /// - Rueda del ratón: zoom suave entre 1X y 3X cuando la cámara está encendida.
    /// Usa exclusivamente UnityEngine.InputSystem y URP Global Volume.
    /// </summary>
    public class CamcorderController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Light infraredLight;
        [SerializeField] private Light camcorderLampLight;
        [SerializeField] private Volume nightVisionVolume;

        [Header("Estado Inicial")]
        [SerializeField] private bool startWithCamcorderOn = true;
        [SerializeField] private bool startWithLampOn = true;

        [Header("Zoom (1X - 3X)")]
        [SerializeField] private float baseFieldOfView = 65f;
        [SerializeField] private float minZoom = 1f;
        [SerializeField] private float maxZoom = 3f;
        [SerializeField] private float zoomStepPerNotch = 0.25f;
        [SerializeField] private float zoomSmoothSpeed = 10f;

        [Header("Visión Nocturna, Lámpara y Batería")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float nightVisionDrainPerSecond = 2.5f;
        [SerializeField] private float lampDrainPerSecond = 0.55f;
        [SerializeField] private float volumeTransitionSpeed = 10f;
        [SerializeField] private float lowBatteryThreshold = 0.15f;
        [SerializeField] private float baseInfraredIntensity = 4.2f;
        [SerializeField] private float baseLampIntensity = 3.2f;

        private InputAction toggleCamcorderAction;
        private InputAction toggleNightVisionAction;
        private InputAction toggleLampAction;
        private InputAction zoomScrollAction;

        private float currentBattery;
        private float targetZoom = 1f;
        private float currentZoom = 1f;
        private float currentVolumeWeight;
        private bool hasInitializedLampOnce;

        public bool IsCamcorderOn { get; private set; }
        public bool IsNightVisionOn { get; private set; }
        public bool IsLampOn { get; private set; }
        public bool InputEnabled { get; set; } = true;
        public float CurrentBattery => currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryNormalized => maxBattery > 0f ? Mathf.Clamp01(currentBattery / maxBattery) : 0f;
        public float CurrentZoom => currentZoom;
        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;

        public event Action<bool> OnCamcorderStateChanged;
        public event Action<bool> OnNightVisionStateChanged;
        public event Action<bool> OnLampStateChanged;

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

            if (camcorderLampLight != null && baseLampIntensity <= 0f)
            {
                baseLampIntensity = camcorderLampLight.intensity;
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

            toggleNightVisionAction = new InputAction("ToggleNightVision", InputActionType.Button, "<Keyboard>/f");
            toggleLampAction = new InputAction("ToggleLamp", InputActionType.Button, "<Keyboard>/l");
            zoomScrollAction = new InputAction("ZoomScroll", InputActionType.Value, "<Mouse>/scroll");
        }

        private void OnEnable()
        {
            toggleCamcorderAction?.Enable();
            toggleNightVisionAction?.Enable();
            toggleLampAction?.Enable();
            zoomScrollAction?.Enable();
        }

        private void OnDisable()
        {
            toggleCamcorderAction?.Disable();
            toggleNightVisionAction?.Disable();
            toggleLampAction?.Disable();
            zoomScrollAction?.Disable();
        }

        private void OnDestroy()
        {
            toggleCamcorderAction?.Dispose();
            toggleNightVisionAction?.Dispose();
            toggleLampAction?.Dispose();
            zoomScrollAction?.Dispose();
        }

        private void Start()
        {
            SetNightVisionActive(false, notify: true);
            SetCamcorderActive(startWithCamcorderOn, notify: true);
            ApplyImmediateVisualState();
        }

        private void Update()
        {
            HandleInput();
            UpdateBattery();
            UpdateZoom();
            UpdateVisualLightsAndVolume();
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
                        // Al activar Visión Nocturna, la lámpara blanca se apaga automáticamente.
                        SetNightVisionActive(true, notify: true);
                    }
                }
            }

            if (toggleLampAction.WasPressedThisFrame())
            {
                if (IsCamcorderOn && !IsNightVisionOn)
                {
                    if (IsLampOn)
                    {
                        SetLampActive(false, notify: true);
                    }
                    else if (currentBattery > 0.01f)
                    {
                        SetLampActive(true, notify: true);
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
            if (!IsCamcorderOn)
            {
                return;
            }

            float drainRate = 0f;
            if (IsNightVisionOn)
            {
                drainRate = nightVisionDrainPerSecond;
            }
            else if (IsLampOn)
            {
                drainRate = lampDrainPerSecond;
            }

            if (drainRate <= 0f)
            {
                return;
            }

            currentBattery = Mathf.Max(0f, currentBattery - drainRate * Time.deltaTime);
            if (currentBattery <= 0f)
            {
                if (IsNightVisionOn) SetNightVisionActive(false, notify: true);
                if (IsLampOn) SetLampActive(false, notify: true);
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

        private void UpdateVisualLightsAndVolume()
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

            float lowBatFlicker = 1f;
            if (BatteryNormalized < lowBatteryThreshold)
            {
                float noise = Mathf.PerlinNoise(Time.time * 18f, 0.37f);
                lowBatFlicker = noise < 0.32f ? 0.35f : Mathf.Lerp(0.7f, 1.05f, noise);
            }

            if (infraredLight != null)
            {
                bool shouldIrBeOn = IsCamcorderOn && IsNightVisionOn;
                if (infraredLight.enabled != shouldIrBeOn)
                {
                    infraredLight.enabled = shouldIrBeOn;
                }

                if (shouldIrBeOn)
                {
                    infraredLight.intensity = baseInfraredIntensity * lowBatFlicker;
                }
            }

            if (camcorderLampLight != null)
            {
                bool shouldLampBeOn = IsCamcorderOn && IsLampOn && !IsNightVisionOn;
                if (camcorderLampLight.enabled != shouldLampBeOn)
                {
                    camcorderLampLight.enabled = shouldLampBeOn;
                }

                if (shouldLampBeOn)
                {
                    camcorderLampLight.intensity = baseLampIntensity * lowBatFlicker;
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

            if (camcorderLampLight != null)
            {
                camcorderLampLight.enabled = IsCamcorderOn && IsLampOn && !IsNightVisionOn;
                camcorderLampLight.intensity = baseLampIntensity;
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseFieldOfView / Mathf.Max(1f, currentZoom);
            }
        }

        public void SetCamcorderActive(bool active, bool notify = true)
        {
            IsCamcorderOn = active;

            if (!IsCamcorderOn)
            {
                if (IsNightVisionOn) SetNightVisionActive(false, notify);
                if (IsLampOn) SetLampActive(false, notify);
            }
            else if (!hasInitializedLampOnce)
            {
                hasInitializedLampOnce = true;
                if (startWithLampOn && !IsNightVisionOn && currentBattery > 0.01f)
                {
                    SetLampActive(true, notify);
                }
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

            // Regla REC/Outlast: al activar Visión Nocturna, apagar la lámpara blanca frontal.
            if (IsNightVisionOn && IsLampOn)
            {
                SetLampActive(false, notify);
            }

            if (notify)
            {
                OnNightVisionStateChanged?.Invoke(IsNightVisionOn);
            }
        }

        public void SetLampActive(bool active, bool notify = true)
        {
            bool nextState = active && IsCamcorderOn && !IsNightVisionOn && currentBattery > 0.01f;
            IsLampOn = nextState;

            if (notify)
            {
                OnLampStateChanged?.Invoke(IsLampOn);
            }
        }

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
