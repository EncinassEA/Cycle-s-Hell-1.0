using UnityEngine;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Botiquín recolectable. Al presionarse E se agrega al inventario para curarse con H (+40 salud).
    /// </summary>
    public class MedkitPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "[E] Tomar Botiquín";
        [SerializeField] private int amount = 1;

        public string GetInteractionPrompt(PlayerInteraction interactor) => promptText;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null && interactor.Inventory != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null || interactor.Inventory == null) return;

            interactor.Inventory.AddMedkit(amount);
            interactor.ShowStatusMessage("Botiquín recogido — Pulsa [H] para curarte (+40 Salud)");
            Destroy(gameObject);
        }
    }
}
