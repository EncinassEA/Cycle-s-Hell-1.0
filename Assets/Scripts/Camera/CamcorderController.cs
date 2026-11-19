using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using CyclesHell.AI;
using CyclesHell.Audio;

namespace CyclesHell.Camcorder
{
    /// <summary>
    /// Sistema de videocámara found-footage año 2004 (estilo REC / Outlast).
    /// - Tecla C o clic derecho: encender/apagar el visor de la cámara.
    /// - Tecla L: encender/apagar la lámpara halógena frontal.
    /// - Tecla F: activar/desactivar visión nocturna verde (apaga la lámpara blanca automáticamente).
    /// - Tecla Q o Clic Izquierdo: disparar Flash Morado (UV) para ahuyentar a Sujeto_04 (consume 15% batería, cooldown 4s).
    /// - Rueda del ratón: zoom suave entre 1X y 3X cuando la cámara está encendida.
    /// Usa exclusivamente UnityEngine.InputSystem y URP Global Volume.
    /// </summary>
    public class CamcorderController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Light infraredLight;
        [SerializeField] private Light camcorderLampLight;
        [SerializeField] private Light uvFlashLight;
        [SerializeField] private Volume nightVisionVolume;
        [SerializeField] private EnemyAI enemyAI;

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

        [Header("Flash Morado (UV)")]
        [SerializeField] private float uvFlashBatteryCost = 15f;
        [SerializeField] private float uvFlashCooldown = 4.0f;
        [SerializeField] private float uvFlashDuration = 0.35f;
        [SerializeField] private float uvFlashPeakIntensity = 24f;
        [SerializeField] private float uvFlashMaxRange = 14f;
        [SerializeField] private float uvFlashMaxHalfAngle = 48f;
        [SerializeField] private LayerMask uvFlashObstacleMask = ~0;

        private InputAction toggleCamcorderAction;
        private InputAction toggleNightVisionAction;
        private InputAction toggleLampAction;
        private InputAction uvFlashAction;
        private InputAction zoomScrollAction;

        private float currentBattery;
        private float targetZoom = 1f;
        private float currentZoom = 1f;
        private float currentVolumeWeight;
        private bool hasInitializedLampOnce;

        private float uvCooldownTimer;
        private float uvFlashActiveTimer;

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
        public float UvFlashCooldownRemaining => Mathf.Max(0f, uvCooldownTimer);
        public float UvFlashCooldownDuration => uvFlashCooldown;
        public bool IsUvFlashReady => IsCamcorderOn && uvCooldownTimer <= 0.01f && currentBattery >= uvFlashBatteryCost;
        public bool HasEnoughBatteryForUv => currentBattery >= uvFlashBatteryCost;

        public event Action<bool> OnCamcorderStateChanged;
        public event Action<bool> OnNightVisionStateChanged;
        public event Action<bool> OnLampStateChanged;
        public event Action OnUvFlashFired;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<UnityEngine.Camera>();
                if (targetCamera == null) targetCamera = UnityEngine.Camera.main;
            }

            if (enemyAI == null) enemyAI = FindAnyObjectByType<EnemyAI>();

            if (targetCamera != null && baseFieldOfView <= 0f) baseFieldOfView = targetCamera.fieldOfView;
            if (infraredLight != null && baseInfraredIntensity <= 0f) baseInfraredIntensity = infraredLight.intensity;
            if (camcorderLampLight != null && baseLampIntensity <= 0f) baseLampIntensity = camcorderLampLight.intensity;

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

            uvFlashAction = new InputAction("UvFlash", InputActionType.Button);
            uvFlashAction.AddBinding("<Keyboard>/q");
            uvFlashAction.AddBinding("<Mouse>/leftButton");

            zoomScrollAction = new InputAction("ZoomScroll", InputActionType.Value, "<Mouse>/scroll");
        }

        private void OnEnable()
        {
            toggleCamcorderAction?.Enable();
            toggleNightVisionAction?.Enable();
            toggleLampAction?.Enable();
            uvFlashAction?.Enable();
            zoomScrollAction?.Enable();
        }

        private void OnDisable()
        {
            toggleCamcorderAction?.Disable();
            toggleNightVisionAction?.Disable();
            toggleLampAction?.Disable();
            uvFlashAction?.Disable();
            zoomScrollAction?.Disable();
        }

        private void OnDestroy()
        {
            toggleCamcorderAction?.Dispose();
            toggleNightVisionAction?.Dispose();
            toggleLampAction?.Dispose();
            uvFlashAction?.Dispose();
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
            if (uvCooldownTimer > 0f)
            {
                uvCooldownTimer = Mathf.Max(0f, uvCooldownTimer - Time.deltaTime);
            }

            HandleInput();
            UpdateBattery();
            UpdateZoom();
            UpdateVisualLightsAndVolume();
        }

        private void HandleInput()
        {
            if (!InputEnabled) return;

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

            if (toggleLampAction.WasPressedThisFrame())
            {
                if (IsCamcorderOn && !IsNightVisionOn)
                {
                    if (IsLampOn) SetLampActive(false, notify: true);
                    else if (currentBattery > 0.01f) SetLampActive(true, notify: true);
                }
            }

            if (uvFlashAction.WasPressedThisFrame() && Cursor.lockState == CursorLockMode.Locked)
            {
                TryFireUvFlash();
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

        public bool TryFireUvFlash()
        {
            if (!IsCamcorderOn || uvCooldownTimer > 0.01f || currentBattery < uvFlashBatteryCost)
            {
                return false;
            }

            currentBattery = Mathf.Max(0f, currentBattery - uvFlashBatteryCost);
            uvCooldownTimer = uvFlashCooldown;
            uvFlashActiveTimer = uvFlashDuration;

            if (currentBattery <= 0f)
            {
                if (IsNightVisionOn) SetNightVisionActive(false, notify: true);
                if (IsLampOn) SetLampActive(false, notify: true);
            }

            if (uvFlashLight != null)
            {
                uvFlashLight.enabled = true;
                uvFlashLight.intensity = uvFlashPeakIntensity;
            }

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.11f, 0.22f);
            }

            if (HorrorAudioManager.Instance != null)
            {
                HorrorAudioManager.Instance.PlayUvFlashSound();
            }

            OnUvFlashFired?.Invoke();
            TryRepelEnemyWithUvFlash();
            return true;
        }

        private void TryRepelEnemyWithUvFlash()
        {
            if (enemyAI == null) enemyAI = FindAnyObjectByType<EnemyAI>();
            if (enemyAI == null || targetCamera == null || !enemyAI.gameObject.activeInHierarchy) return;

            Vector3 camPos = targetCamera.transform.position;
            Vector3 enemyCenter = enemyAI.transform.position + Vector3.up * 1.25f;
            Vector3 toEnemy = enemyCenter - camPos;
            float dist = toEnemy.magnitude;

            if (dist > uvFlashMaxRange) return;

            float angle = Vector3.Angle(targetCamera.transform.forward, toEnemy.normalized);
            if (angle > uvFlashMaxHalfAngle) return;

            if (Physics.Raycast(camPos, toEnemy.normalized, out RaycastHit hit, dist, uvFlashObstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == enemyAI.transform || hit.transform.IsChildOf(enemyAI.transform))
                {
                    enemyAI.TriggerUvFlashFlee(camPos);
                }
            }
            else
            {
                enemyAI.TriggerUvFlashFlee(camPos);
            }
        }

        private void UpdateBattery()
        {
            if (!IsCamcorderOn) return;

            float drainRate = 0f;
            if (IsNightVisionOn) drainRate = nightVisionDrainPerSecond;
            else if (IsLampOn) drainRate = lampDrainPerSecond;

            if (drainRate <= 0f) return;

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

            if (Mathf.Abs(currentVolumeWeight - targetWeight) < 0.002f) currentVolumeWeight = targetWeight;
            if (nightVisionVolume != null) nightVisionVolume.weight = currentVolumeWeight;

            float lowBatFlicker = 1f;
            if (BatteryNormalized < lowBatteryThreshold)
            {
                float noise = Mathf.PerlinNoise(Time.time * 18f, 0.37f);
                lowBatFlicker = noise < 0.32f ? 0.35f : Mathf.Lerp(0.7f, 1.05f, noise);
            }

            if (infraredLight != null)
            {
                bool shouldIrBeOn = IsCamcorderOn && IsNightVisionOn;
                if (infraredLight.enabled != shouldIrBeOn) infraredLight.enabled = shouldIrBeOn;
                if (shouldIrBeOn) infraredLight.intensity = baseInfraredIntensity * lowBatFlicker;
            }

            if (camcorderLampLight != null)
            {
                bool shouldLampBeOn = IsCamcorderOn && IsLampOn && !IsNightVisionOn;
                if (camcorderLampLight.enabled != shouldLampBeOn) camcorderLampLight.enabled = shouldLampBeOn;
                if (shouldLampBeOn) camcorderLampLight.intensity = baseLampIntensity * lowBatFlicker;
            }

            if (uvFlashLight != null)
            {
                if (uvFlashActiveTimer > 0f)
                {
                    uvFlashActiveTimer -= Time.deltaTime;
                    float norm = Mathf.Clamp01(uvFlashActiveTimer / Mathf.Max(0.01f, uvFlashDuration));
                    uvFlashLight.enabled = true;
                    uvFlashLight.intensity = uvFlashPeakIntensity * norm * norm;
                }
                else if (uvFlashLight.enabled)
                {
                    uvFlashLight.intensity = 0f;
                    uvFlashLight.enabled = false;
                }
            }
        }

        private void ApplyImmediateVisualState()
        {
            currentVolumeWeight = (IsCamcorderOn && IsNightVisionOn) ? 1f : 0f;
            if (nightVisionVolume != null) nightVisionVolume.weight = currentVolumeWeight;
            if (infraredLight != null) { infraredLight.enabled = IsCamcorderOn && IsNightVisionOn; infraredLight.intensity = baseInfraredIntensity; }
            if (camcorderLampLight != null) { camcorderLampLight.enabled = IsCamcorderOn && IsLampOn && !IsNightVisionOn; camcorderLampLight.intensity = baseLampIntensity; }
            if (uvFlashLight != null) { uvFlashLight.enabled = false; uvFlashLight.intensity = 0f; }
            if (targetCamera != null) targetCamera.fieldOfView = baseFieldOfView / Mathf.Max(1f, currentZoom);
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

            if (notify) OnCamcorderStateChanged?.Invoke(IsCamcorderOn);
        }

        public void SetNightVisionActive(bool active, bool notify = true)
        {
            bool nextState = active && IsCamcorderOn && currentBattery > 0.01f;
            IsNightVisionOn = nextState;

            if (IsNightVisionOn && IsLampOn) SetLampActive(false, notify);
            if (notify) OnNightVisionStateChanged?.Invoke(IsNightVisionOn);
        }

        public void SetLampActive(bool active, bool notify = true)
        {
            bool nextState = active && IsCamcorderOn && !IsNightVisionOn && currentBattery > 0.01f;
            IsLampOn = nextState;
            if (notify) OnLampStateChanged?.Invoke(IsLampOn);
        }

        public void RechargeBattery(float amount)
        {
            if (amount <= 0f) return;
            currentBattery = Mathf.Clamp(currentBattery + amount, 0f, maxBattery);
        }
    }
}
