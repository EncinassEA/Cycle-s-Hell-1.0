using UnityEngine;
using CyclesHell.UI;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Pista interactuable de la historia (4 pistas en total).
    /// Al presionar E registra el avance en StoryObjectiveManager, abre el expediente legible
    /// y opcionalmente dispara un JumpscareTrigger vinculado.
    /// </summary>
    public class CluePickup : MonoBehaviour, IInteractable
    {
        [Header("Identidad de Pista")]
        [SerializeField] private string clueId = "Clue_1";
        [SerializeField] private string promptText = "[E] Examinar Pista";
        [SerializeField] private string inspectedPromptText = "[E] Releer Pista";
        [SerializeField] private string clueTitle = "PISTA DEL LABORATORIO";
        [SerializeField] private string clueDate = "NOV. 2004";

        [TextArea(6, 16)]
        [SerializeField] private string clueBody = "";

        [Header("Evento Opcional")]
        [SerializeField] private JumpscareTrigger linkedJumpscare;
        [SerializeField] private Renderer highlightMarkerRenderer;

        private bool collected;

        public string GetInteractionPrompt(PlayerInteraction interactor) => collected ? inspectedPromptText : promptText;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null) return;

            bool newlyCollected = false;
            if (!collected)
            {
                collected = true;
                if (StoryObjectiveManager.Instance != null)
                {
                    newlyCollected = StoryObjectiveManager.Instance.RegisterClue(clueId);
                }

                if (highlightMarkerRenderer != null)
                {
                    highlightMarkerRenderer.enabled = false;
                }
            }

            interactor.OpenNote(clueTitle, clueDate, clueBody);

            if (newlyCollected && StoryObjectiveManager.Instance != null)
            {
                int found = StoryObjectiveManager.Instance.CluesFound;
                int total = StoryObjectiveManager.Instance.RequiredClues;
                string msg = found >= total
                    ? $"¡Todos los expedientes reunidos ({found}/{total})! Escapa por la salida del laboratorio."
                    : $"Expediente registrado ({found}/{total})";
                interactor.ShowStatusMessage(msg);
            }

            if (newlyCollected && linkedJumpscare != null)
            {
                linkedJumpscare.TriggerScare();
            }
        }
    }
}
