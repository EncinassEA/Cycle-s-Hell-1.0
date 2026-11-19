using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CyclesHell.Player;
using CyclesHell.Camcorder;
using CyclesHell.Survival;
using CyclesHell.Interaction;

namespace CyclesHell.UI
{
    /// <summary>
    /// Menú de Pausa estilo VHS 2004 (tecla Esc).
    /// Solo se abre si el jugador no está leyendo un expediente ni muerto.
    /// Usa exclusivamente el Nuevo Input System (UnityEngine.InputSystem).
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI de Pausa")]
        [SerializeField] private GameObject pausePanelRoot;
        [SerializeField] private UnityEngine.UI.Button resumeButton;
        [SerializeField] private UnityEngine.UI.Button mainMenuButton;
        [SerializeField] private string mainMenuSceneName = "SampleScene";

        [Header("Referencias del Jugador")]
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private CamcorderController camcorderController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerInteraction playerInteraction;

        private InputAction pauseAction;
        private bool isPaused;

        public bool IsPaused => isPaused;

        private void Awake()
        {
            if (firstPersonController == null) firstPersonController = FindAnyObjectByType<FirstPersonController>();
            if (camcorderController == null) camcorderController = FindAnyObjectByType<CamcorderController>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerInventory == null) playerInventory = FindAnyObjectByType<PlayerInventory>();
            if (playerInteraction == null) playerInteraction = FindAnyObjectByType<PlayerInteraction>();

            pauseAction = new InputAction("PauseToggle", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            pauseAction?.Enable();

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(ResumeGame);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void OnDisable()
        {
            pauseAction?.Disable();

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(ResumeGame);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            }
        }

        private void OnDestroy()
        {
            pauseAction?.Dispose();
            if (isPaused)
            {
                Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            isPaused = false;
            Time.timeScale = 1f;
            if (pausePanelRoot != null)
            {
                pausePanelRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (!pauseAction.WasPressedThisFrame())
            {
                return;
            }

            // No abrir pausa si el jugador está muerto, viendo la intro/victoria o leyendo un expediente
            if (playerHealth != null && playerHealth.IsDead)
            {
                return;
            }

            if (StoryObjectiveManager.Instance != null &&
                (StoryObjectiveManager.Instance.IsIntroActive ||
                 StoryObjectiveManager.Instance.IsVictoryActive ||
                 Time.frameCount <= StoryObjectiveManager.Instance.LastIntroClosedFrame))
            {
                return;
            }

            if (playerInteraction != null &&
                (playerInteraction.IsReadingNote || Time.frameCount <= playerInteraction.LastNoteClosedFrame))
            {
                return;
            }

            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            if (isPaused) return;
            isPaused = true;

            Time.timeScale = 0f;

            if (firstPersonController != null)
            {
                firstPersonController.SetControlPaused(pauseMovement: true, pauseLook: true, pauseAutoCursor: true);
                firstPersonController.SetCursorLocked(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (camcorderController != null) camcorderController.InputEnabled = false;
            if (playerInventory != null) playerInventory.InputBlocked = true;
            if (playerHealth != null) playerHealth.HealInputBlocked = true;
            if (playerInteraction != null) playerInteraction.InputBlocked = true;

            if (pausePanelRoot != null)
            {
                pausePanelRoot.SetActive(true);
            }
        }

        public void ResumeGame()
        {
            if (!isPaused) return;
            isPaused = false;

            Time.timeScale = 1f;

            if (pausePanelRoot != null)
            {
                pausePanelRoot.SetActive(false);
            }

            if (playerHealth != null && playerHealth.IsDead)
            {
                return;
            }

            if (firstPersonController != null)
            {
                firstPersonController.SetControlPaused(pauseMovement: false, pauseLook: false, pauseAutoCursor: false);
                firstPersonController.SetCursorLocked(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (camcorderController != null) camcorderController.InputEnabled = true;
            if (playerInventory != null) playerInventory.InputBlocked = false;
            if (playerHealth != null) playerHealth.HealInputBlocked = false;
            if (playerInteraction != null) playerInteraction.InputBlocked = false;
        }

        public void ReturnToMainMenu()
        {
            isPaused = false;
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
