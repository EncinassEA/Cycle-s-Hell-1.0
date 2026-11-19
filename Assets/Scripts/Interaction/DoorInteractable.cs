using UnityEngine;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Puerta interactuable con cerradura por llave y apertura suave mediante rotación del pivote.
    /// </summary>
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("Cerradura")]
        [SerializeField] private bool isLocked = true;
        [SerializeField] private string requiredKeyId = "LabKey";
        [SerializeField] private string lockedMessage = "Cerrada - Necesitas la llave del laboratorio";
        [SerializeField] private string unlockedMessage = "Puerta desbloqueada con la llave del laboratorio";

        [Header("Animación de Apertura")]
        [SerializeField] private Transform doorPivot;
        [SerializeField] private float openAngleY = 95f;
        [SerializeField] private float rotationSpeed = 3.6f;

        [Header("Indicador Visual Opcional")]
        [SerializeField] private Renderer statusLedRenderer;
        [SerializeField] private Color lockedLedColor = new Color(1.2f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color unlockedLedColor = new Color(0.15f, 1.2f, 0.25f, 1f);

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;
        private float rattleTimer;

        private void Awake()
        {
            if (doorPivot == null)
            {
                doorPivot = transform;
            }

            closedRotation = doorPivot.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngleY, 0f);
            UpdateLedVisual();
        }

        private void Update()
        {
            if (doorPivot == null) return;

            if (rattleTimer > 0f)
            {
                rattleTimer -= Time.deltaTime;
                float shake = Mathf.Sin(rattleTimer * 55f) * 1.6f * Mathf.Clamp01(rattleTimer / 0.25f);
                doorPivot.localRotation = closedRotation * Quaternion.Euler(0f, shake, 0f);
                if (rattleTimer <= 0f)
                {
                    doorPivot.localRotation = closedRotation;
                }
                return;
            }

            Quaternion targetRot = isOpen ? openRotation : closedRotation;
            doorPivot.localRotation = Quaternion.Slerp(
                doorPivot.localRotation,
                targetRot,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        }

        public string GetInteractionPrompt(PlayerInteraction interactor)
        {
            if (isLocked)
            {
                bool hasKey = interactor != null && interactor.Inventory != null && interactor.Inventory.HasKey(requiredKeyId);
                return hasKey ? "[E] Desbloquear y Abrir Puerta" : "[E] Abrir Puerta (Cerrada)";
            }

            return isOpen ? "[E] Cerrar Puerta" : "[E] Abrir Puerta";
        }

        public bool CanInteract(PlayerInteraction interactor) => interactor != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null) return;

            if (isLocked)
            {
                bool hasKey = interactor.Inventory != null && interactor.Inventory.HasKey(requiredKeyId);
                if (!hasKey)
                {
                    rattleTimer = 0.25f;
                    interactor.ShowStatusMessage(lockedMessage);
                    return;
                }

                isLocked = false;
                isOpen = true;
                UpdateLedVisual();
                interactor.ShowStatusMessage(unlockedMessage);
                return;
            }

            isOpen = !isOpen;
        }

        private void UpdateLedVisual()
        {
            if (statusLedRenderer == null) return;

            Color c = isLocked ? lockedLedColor : unlockedLedColor;
            Material mat = statusLedRenderer.material;
            if (mat != null)
            {
                mat.SetColor("_BaseColor", c);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 1.4f);
            }
        }
    }
}
