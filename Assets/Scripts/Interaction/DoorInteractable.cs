using UnityEngine;
using UnityEngine.AI;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Puerta interactuable con soporte para puertas desbloqueadas (abrir/cerrar con E para cortar línea de visión)
    /// y puertas cerradas con llave o tarjeta.
    /// Solo las puertas bloqueadas con llave/tarjeta mantienen activo su NavMeshObstacle (carving) para bloquear al enemigo;
    /// las puertas desbloqueadas permiten el paso de Sujeto_04 y se abren automáticamente cuando el enemigo las empuja.
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

        [Header("Navegación IA")]
        [SerializeField] private NavMeshObstacle navMeshObstacle;
        [SerializeField] private bool allowEnemyAutoOpen = true;

        [Header("Indicador Visual Opcional")]
        [SerializeField] private Renderer statusLedRenderer;
        [SerializeField] private Color lockedLedColor = new Color(1.2f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color unlockedLedColor = new Color(0.15f, 1.2f, 0.25f, 1f);

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;
        private float rattleTimer;

        public bool IsLocked => isLocked;
        public bool IsOpen => isOpen;
        public bool AllowEnemyAutoOpen => allowEnemyAutoOpen;

        private void Awake()
        {
            if (doorPivot == null)
            {
                doorPivot = transform;
            }

            if (navMeshObstacle == null)
            {
                navMeshObstacle = GetComponentInChildren<NavMeshObstacle>(true);
            }

            closedRotation = doorPivot.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngleY, 0f);
            UpdateLedVisual();
            UpdateNavMeshObstacleState();
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
                UpdateNavMeshObstacleState();
                interactor.ShowStatusMessage(unlockedMessage);
                return;
            }

            isOpen = !isOpen;
        }

        /// <summary>
        /// Llamado por EnemyAI cuando Sujeto_04 se acerca a una puerta desbloqueada cerrada para empujarla y abrirla.
        /// </summary>
        public void OpenByEnemy()
        {
            if (isLocked || !allowEnemyAutoOpen || isOpen) return;
            isOpen = true;
        }

        public void UpdateNavMeshObstacleState()
        {
            if (navMeshObstacle == null) return;
            navMeshObstacle.carving = isLocked;
            navMeshObstacle.enabled = isLocked;
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
