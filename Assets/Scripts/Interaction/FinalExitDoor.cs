using UnityEngine;
using CyclesHell.UI;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Puerta de salida final del laboratorio subterráneo.
    /// Se desbloquea automáticamente al reunir las 4 pistas y abre la pantalla de fin de nivel ("CINTA RECUPERADA - SOBREVIVISTE").
    /// </summary>
    public class FinalExitDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer statusLedRenderer;
        [SerializeField] private Color lockedColor = new Color(1.2f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color unlockedColor = new Color(0.18f, 1.4f, 0.35f, 1f);

        private void OnEnable()
        {
            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.OnAllCluesCollected += RefreshLed;
            }
        }

        private void OnDisable()
        {
            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.OnAllCluesCollected -= RefreshLed;
            }
        }

        private void Start()
        {
            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.OnAllCluesCollected -= RefreshLed;
                StoryObjectiveManager.Instance.OnAllCluesCollected += RefreshLed;
            }
            RefreshLed();
        }

        private bool IsUnlocked => StoryObjectiveManager.Instance != null && StoryObjectiveManager.Instance.HasAllClues;

        public string GetInteractionPrompt(PlayerInteraction interactor)
        {
            if (IsUnlocked)
            {
                return "[E] Escapar del Laboratorio";
            }

            int found = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.CluesFound : 0;
            int total = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.RequiredClues : 4;
            return $"[E] Salida Bloqueada ({found}/{total} pistas)";
        }

        public bool CanInteract(PlayerInteraction interactor) => interactor != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null) return;

            if (!IsUnlocked)
            {
                int found = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.CluesFound : 0;
                int total = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.RequiredClues : 4;
                interactor.ShowStatusMessage($"Necesitas investigar qué pasó con tus compañeros ({found}/{total} pistas).");
                return;
            }

            StoryObjectiveManager.Instance.TriggerVictory();
        }

        private void RefreshLed()
        {
            if (statusLedRenderer == null) return;
            Color c = IsUnlocked ? unlockedColor : lockedColor;
            Material mat = statusLedRenderer.material;
            if (mat != null)
            {
                mat.SetColor("_BaseColor", c);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 1.5f);
            }
        }
    }
}
