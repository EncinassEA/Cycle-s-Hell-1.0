using UnityEngine;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Llave recolectable para desbloquear puertas cerradas (por defecto, la llave del laboratorio).
    /// </summary>
    public class KeyPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "[E] Tomar Llave";
        [SerializeField] private string keyId = "LabKey";
        [SerializeField] private string keyDisplayName = "Llave del laboratorio";

        public string GetInteractionPrompt(PlayerInteraction interactor) => promptText;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null && interactor.Inventory != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null || interactor.Inventory == null) return;

            interactor.Inventory.AddKey(keyId);
            interactor.ShowStatusMessage($"Recogiste: {keyDisplayName}");
            Destroy(gameObject);
        }
    }
}
