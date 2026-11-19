using UnityEngine;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Batería recolectable para la videocámara. Se suma al contador y se usa con B para recargar al 100%.
    /// </summary>
    public class BatteryPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "[E] Tomar Batería";
        [SerializeField] private int amount = 1;

        public string GetInteractionPrompt(PlayerInteraction interactor) => promptText;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null && interactor.Inventory != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null || interactor.Inventory == null) return;

            interactor.Inventory.AddBattery(amount);
            interactor.ShowStatusMessage("Batería recogida — Pulsa [B] para recargar la cámara al 100%");
            Destroy(gameObject);
        }
    }
}
