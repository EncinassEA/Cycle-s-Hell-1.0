namespace CyclesHell.Interaction
{
    /// <summary>
    /// Contrato común para todos los objetos interactuables de Cycle's Hell.
    /// </summary>
    public interface IInteractable
    {
        string GetInteractionPrompt(PlayerInteraction interactor);
        bool CanInteract(PlayerInteraction interactor);
        void Interact(PlayerInteraction interactor);
    }
}
