using UnityEngine;
using UnityEngine.InputSystem;
using CyclesHell.Player;
using CyclesHell.Camcorder;
using CyclesHell.Survival;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Sistema de interacción en primera persona mediante Raycast desde el centro de la cámara (~3m).
    /// Usa exclusivamente el Nuevo Input System (E para interactuar, E o Esc para cerrar expedientes).
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private UnityEngine.Camera playerCamera;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private CamcorderController camcorderController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private SurvivalHUDController survivalHUD;

        [Header("Raycast")]
        [SerializeField] private float interactionRange = 3.0f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private InputAction interactAction;
        private InputAction closeNoteAction;

        private IInteractable currentTarget;
        private bool isReadingNote;
        private int noteOpenedFrame = -1;
        private int lastNoteClosedFrame = -1;

        public PlayerInventory Inventory => playerInventory;
        public PlayerHealth Health => playerHealth;
        public CamcorderController Camcorder => camcorderController;
        public bool IsReadingNote => isReadingNote;
        public int LastNoteClosedFrame => lastNoteClosedFrame;
        public bool InputBlocked { get; set; }

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<UnityEngine.Camera>();
                if (playerCamera == null)
                {
                    playerCamera = UnityEngine.Camera.main;
                }
            }

            if (firstPersonController == null) firstPersonController = GetComponent<FirstPersonController>();
            if (camcorderController == null) camcorderController = GetComponentInChildren<CamcorderController>();
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
            if (playerInventory == null) playerInventory = GetComponent<PlayerInventory>();
            if (survivalHUD == null) survivalHUD = FindAnyObjectByType<SurvivalHUDController>();

            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            closeNoteAction = new InputAction("CloseNote", InputActionType.Button);
            closeNoteAction.AddBinding("<Keyboard>/escape");
            closeNoteAction.AddBinding("<Keyboard>/e");
        }

        private void OnEnable()
        {
            interactAction?.Enable();
            closeNoteAction?.Enable();

            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied += HandlePlayerDied;
            }
        }

        private void OnDisable()
        {
            interactAction?.Disable();
            closeNoteAction?.Disable();

            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied -= HandlePlayerDied;
            }
        }

        private void OnDestroy()
        {
            interactAction?.Dispose();
            closeNoteAction?.Dispose();
        }

        private void Update()
        {
            if (InputBlocked || (playerHealth != null && playerHealth.IsDead))
            {
                ClearCurrentTarget();
                return;
            }

            if (isReadingNote)
            {
                if (Time.frameCount > noteOpenedFrame && closeNoteAction.WasPressedThisFrame())
                {
                    CloseNote();
                }
                return;
            }

            UpdateRaycastTarget();

            if (currentTarget != null && interactAction.WasPressedThisFrame())
            {
                if (currentTarget.CanInteract(this))
                {
                    currentTarget.Interact(this);
                }
            }
        }

        private void UpdateRaycastTarget()
        {
            if (playerCamera == null)
            {
                ClearCurrentTarget();
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionLayers, QueryTriggerInteraction.Collide))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(this))
                {
                    currentTarget = interactable;
                    if (survivalHUD != null)
                    {
                        survivalHUD.ShowInteractionPrompt(interactable.GetInteractionPrompt(this));
                    }
                    return;
                }
            }

            ClearCurrentTarget();
        }

        private void ClearCurrentTarget()
        {
            currentTarget = null;
            if (survivalHUD != null)
            {
                survivalHUD.HideInteractionPrompt();
            }
        }

        public void ShowStatusMessage(string message)
        {
            if (survivalHUD != null)
            {
                survivalHUD.ShowStatusMessage(message);
            }
        }

        public void OpenNote(string title, string date, string body)
        {
            if (isReadingNote) return;

            isReadingNote = true;
            noteOpenedFrame = Time.frameCount;
            ClearCurrentTarget();

            if (firstPersonController != null)
            {
                firstPersonController.SetControlPaused(pauseMovement: true, pauseLook: true, pauseAutoCursor: true);
            }

            if (camcorderController != null)
            {
                camcorderController.InputEnabled = false;
            }

            if (playerInventory != null)
            {
                playerInventory.InputBlocked = true;
            }

            if (playerHealth != null)
            {
                playerHealth.HealInputBlocked = true;
            }

            if (survivalHUD != null)
            {
                survivalHUD.ShowNote(title, date, body);
            }
        }

        public void CloseNote()
        {
            if (!isReadingNote) return;

            isReadingNote = false;
            lastNoteClosedFrame = Time.frameCount;

            if (survivalHUD != null)
            {
                survivalHUD.HideNote();
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

            if (camcorderController != null)
            {
                camcorderController.InputEnabled = true;
            }

            if (playerInventory != null)
            {
                playerInventory.InputBlocked = false;
            }

            if (playerHealth != null)
            {
                playerHealth.HealInputBlocked = false;
            }
        }

        private void HandlePlayerDied()
        {
            if (isReadingNote)
            {
                isReadingNote = false;
                if (survivalHUD != null)
                {
                    survivalHUD.HideNote();
                }
            }
            ClearCurrentTarget();
        }
    }
}
