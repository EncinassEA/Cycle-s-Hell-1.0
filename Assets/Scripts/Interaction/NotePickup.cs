using UnityEngine;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Expediente / Nota del laboratorio (Nov. 2004). Al presionar E abre el panel de lectura en pantalla.
    /// </summary>
    public class NotePickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "[E] Leer Expediente";
        [SerializeField] private string noteTitle = "EXPEDIENTE #04-B — PROYECTO CICLO";
        [SerializeField] private string noteDate = "06 DE NOVIEMBRE DE 2004 — ANEXO DE LA CABAÑA";

        [TextArea(8, 20)]
        [SerializeField] private string noteContent =
            "REGISTRO INTERNO — ACCESO RESTRINGIDO\n\n" +
            "Han pasado setenta y dos horas desde la última transmisión del Dr. Morales y la Dra. Reyes en la cabaña del sector norte. " +
            "Las cintas MiniDV recuperadas del pasillo subterráneo muestran interferencias severas exactamente a las 23:14, " +
            "seguidas de pasos que no corresponden a ningún miembro del equipo.\n\n" +
            "Los sujetos de prueba de la Fase III reaccionan de forma violenta ante la luz directa, pero el espectro infrarrojo " +
            "de las cámaras portátiles permite detectar su desplazamiento en la oscuridad.\n\n" +
            "Sellamos el acceso al Sector B. La llave de respaldo del laboratorio quedó oculta junto a los casilleros del corredor central. " +
            "Si alguien encuentra este informe: NO apague la cámara y revise su batería antes de cruzar la puerta.";

        public string GetInteractionPrompt(PlayerInteraction interactor) => promptText;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null) return;
            interactor.OpenNote(noteTitle, noteDate, noteContent);
        }
    }
}
