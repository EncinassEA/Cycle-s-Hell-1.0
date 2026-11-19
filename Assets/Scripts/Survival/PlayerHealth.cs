using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CyclesHell.Player;
using CyclesHell.Camcorder;

namespace CyclesHell.Survival
{
    /// <summary>
    /// Sistema de vida (0-100), curación con botiquín (H), muerte estilo VHS (R para reiniciar)
    /// y tecla de prueba temporal (K) usando exclusivamente el Nuevo Input System.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private CamcorderController camcorderController;
        [SerializeField] private PlayerInventory inventory;

        [Header("Salud y Curación")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float medkitHealAmount = 40f;

        [Header("Pruebas Temporales")]
        // TEMPORAL PARA PRUEBAS: Desactivar o eliminar este bloque y testDamageAction antes de la versión final.
        [SerializeField] private bool enableDebugDamageKey = true;
        [SerializeField] private float debugDamageAmount = 25f;

        private InputAction healAction;
        private InputAction restartAction;
        private InputAction debugDamageAction; // TEMPORAL PARA PRUEBAS (Tecla K)

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;
        public bool IsDead { get; private set; }
        public bool HealInputBlocked { get; set; }

        public event Action<float, float> OnHealthChanged;
        public event Action<float> OnDamaged;
        public event Action OnPlayerDied;
        public event Action<string> OnStatusMessage;

        private void Awake()
        {
            if (firstPersonController == null)
            {
                firstPersonController = GetComponent<FirstPersonController>();
            }

            if (camcorderController == null)
            {
                camcorderController = GetComponentInChildren<CamcorderController>();
                if (camcorderController == null)
                {
                    camcorderController = FindAnyObjectByType<CamcorderController>();
                }
            }

            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            CurrentHealth = maxHealth;
            IsDead = false;

            SetupInputActions();
        }

        private void SetupInputActions()
        {
            healAction = new InputAction("Heal", InputActionType.Button, "<Keyboard>/h");
            restartAction = new InputAction("RestartOnDeath", InputActionType.Button, "<Keyboard>/r");

            // TEMPORAL PARA PRUEBAS: Tecla K para simular daño (-25 salud). Quitar en versión final.
            debugDamageAction = new InputAction("DebugDamage", InputActionType.Button, "<Keyboard>/k");
        }

        private void OnEnable()
        {
            healAction?.Enable();
            restartAction?.Enable();
            debugDamageAction?.Enable();
        }

        private void OnDisable()
        {
            healAction?.Disable();
            restartAction?.Disable();
            debugDamageAction?.Disable();
        }

        private void OnDestroy()
        {
            healAction?.Dispose();
            restartAction?.Dispose();
            debugDamageAction?.Dispose();
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void Update()
        {
            if (IsDead)
            {
                if (restartAction.WasPressedThisFrame())
                {
                    RestartCurrentScene();
                }
                return;
            }

            if (!HealInputBlocked && healAction.WasPressedThisFrame())
            {
                TryUseMedkit();
            }

            // TEMPORAL PARA PRUEBAS: Presionar K resta 25 de vida para probar daño, curación y muerte.
            // Eliminar este condicional antes de lanzar el juego final.
            if (enableDebugDamageKey && debugDamageAction.WasPressedThisFrame())
            {
                TakeDamage(debugDamageAmount);
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0f, maxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnDamaged?.Invoke(amount);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public bool TryUseMedkit()
        {
            if (IsDead || inventory == null)
            {
                return false;
            }

            if (inventory.MedkitCount <= 0)
            {
                OnStatusMessage?.Invoke("No tienes botiquines disponibles.");
                return false;
            }

            if (CurrentHealth >= maxHealth - 0.01f)
            {
                OnStatusMessage?.Invoke("Tu salud ya está al máximo.");
                return false;
            }

            if (inventory.ConsumeMedkit())
            {
                Heal(medkitHealAmount);
                OnStatusMessage?.Invoke($"Botiquín usado (+{Mathf.RoundToInt(medkitHealAmount)} Salud)");
                return true;
            }

            return false;
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;

            if (firstPersonController != null)
            {
                firstPersonController.SetControlPaused(pauseMovement: true, pauseLook: true, pauseAutoCursor: true);
                firstPersonController.SetCursorLocked(false);
            }

            if (camcorderController != null)
            {
                camcorderController.InputEnabled = false;
                camcorderController.SetNightVisionActive(false, notify: true);
            }

            OnPlayerDied?.Invoke();
        }

        public void RestartCurrentScene()
        {
            Time.timeScale = 1f;
            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    activeScene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(activeScene.name);
#endif
            }
        }
    }
}
