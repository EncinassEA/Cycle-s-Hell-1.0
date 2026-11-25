using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CyclesHell.Camcorder;

namespace CyclesHell.Survival
{
    /// <summary>
    /// Inventario ligero de supervivencia en el jugador: botiquines, baterías de videocámara y llaves.
    /// Gestiona la recarga de la videocámara al 100% mediante la tecla B (Nuevo Input System).
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private CamcorderController camcorder;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Contadores Iniciales")]
        [SerializeField] private int startingMedkits = 0;
        [SerializeField] private int startingBatteries = 0;

        private readonly HashSet<string> collectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private InputAction useBatteryAction;

        public int MedkitCount { get; private set; }
        public int BatteryCount { get; private set; }
        public bool InputBlocked { get; set; }

        public event Action OnInventoryChanged;
        public event Action<string> OnStatusMessage;

        private void Awake()
        {
            if (camcorder == null)
            {
                camcorder = GetComponentInChildren<CamcorderController>();
                if (camcorder == null)
                {
                    camcorder = FindAnyObjectByType<CamcorderController>();
                }
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            MedkitCount = Mathf.Max(0, startingMedkits);
            BatteryCount = Mathf.Max(0, startingBatteries);

            useBatteryAction = new InputAction("UseBattery", InputActionType.Button, "<Keyboard>/b");
        }

        private void OnEnable()
        {
            useBatteryAction?.Enable();
        }

        private void OnDisable()
        {
            useBatteryAction?.Disable();
        }

        private void OnDestroy()
        {
            useBatteryAction?.Dispose();
        }

        private void Start()
        {
            OnInventoryChanged?.Invoke();
        }

        private void Update()
        {
            if (InputBlocked || (playerHealth != null && playerHealth.IsDead))
            {
                return;
            }

            if (useBatteryAction.WasPressedThisFrame())
            {
                TryUseBattery();
            }
        }

        public void AddMedkit(int amount = 1)
        {
            if (amount <= 0) return;
            MedkitCount += amount;
            OnInventoryChanged?.Invoke();
        }

        public bool ConsumeMedkit()
        {
            if (MedkitCount <= 0) return false;
            MedkitCount--;
            OnInventoryChanged?.Invoke();
            return true;
        }

        public void AddBattery(int amount = 1)
        {
            if (amount <= 0) return;
            BatteryCount += amount;
            OnInventoryChanged?.Invoke();
        }

        public bool TryUseBattery()
        {
            if (BatteryCount <= 0)
            {
                OnStatusMessage?.Invoke("No tienes baterías de repuesto.");
                return false;
            }

            if (camcorder == null)
            {
                return false;
            }

            if (camcorder.CurrentBattery >= camcorder.MaxBattery - 0.1f)
            {
                OnStatusMessage?.Invoke("La batería de la cámara ya está al 100%.");
                return false;
            }

            BatteryCount--;
            camcorder.RechargeBattery(camcorder.MaxBattery);
            OnInventoryChanged?.Invoke();
            OnStatusMessage?.Invoke("Batería reemplazada — Cámara al 100%");
            return true;
        }

        public void AddKey(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId)) return;
            if (collectedKeys.Add(keyId.Trim()))
            {
                OnInventoryChanged?.Invoke();
            }
        }

        public bool HasKey(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId)) return true;
            return collectedKeys.Contains(keyId.Trim());
        }

        public bool HasAnyKey => collectedKeys.Count > 0;

        public string GetCollectedKeysSummary()
        {
            if (collectedKeys.Count == 0) return string.Empty;

            var labels = new List<string>(collectedKeys.Count);
            foreach (string key in collectedKeys)
            {
                switch (key.ToLowerInvariant())
                {
                    case "maintenancekey":
                        labels.Add("LLAVE MANT.");
                        break;
                    case "archivoscard":
                        labels.Add("TARJ. ARCHIVOS");
                        break;
                    case "labkey":
                        labels.Add("LLAVE SECTOR B");
                        break;
                    case "bedroomkey":
                        labels.Add("LLAVE RECÁMARA");
                        break;
                    default:
                        labels.Add(key.ToUpperInvariant());
                        break;
                }
            }

            return "ACCESOS: " + string.Join(" | ", labels);
        }
    }
}
