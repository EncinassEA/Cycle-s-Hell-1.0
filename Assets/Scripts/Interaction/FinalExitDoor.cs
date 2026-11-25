using UnityEngine;
using UnityEngine.SceneManagement;
using CyclesHell.UI;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Puerta de salida final del escenario.
    /// Se desbloquea automáticamente al reunir todos los expedientes.
    /// Si nextSceneName está vacío, muestra la pantalla de "CINTA RECUPERADA - NIVEL COMPLETADO";
    /// si contiene el nombre de una escena siguiente (ej. "Nivel_2"), carga ese escenario automáticamente.
    /// </summary>
    public class FinalExitDoor : MonoBehaviour, IInteractable
    {
        [Header("Progresión Modular de Niveles")]
        [Tooltip("Déjalo vacío para mostrar la pantalla de nivel completado. Escribe el nombre de la siguiente escena (ej. 'Nivel_2') para cargarla automáticamente.")]
        [SerializeField] private string nextSceneName = "";

        [Header("Indicador Visual")]
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
                return string.IsNullOrWhiteSpace(nextSceneName)
                    ? "[E] Escapar del Laboratorio"
                    : "[E] Avanzar al Siguiente Sector";
            }

            int found = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.CluesFound : 0;
            int total = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.RequiredClues : 5;
            return $"[E] Salida Bloqueada ({found}/{total} expedientes)";
        }

        public bool CanInteract(PlayerInteraction interactor) => interactor != null;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null) return;

            if (!IsUnlocked)
            {
                int found = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.CluesFound : 0;
                int total = StoryObjectiveManager.Instance != null ? StoryObjectiveManager.Instance.RequiredClues : 5;
                interactor.ShowStatusMessage($"Necesitas investigar qué pasó con tus compañeros ({found}/{total} expedientes).");
                return;
            }

            if (!string.IsNullOrWhiteSpace(nextSceneName))
            {
                if (Application.CanStreamedLevelBeLoaded(nextSceneName))
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(nextSceneName);
                    return;
                }
                else
                {
                    Debug.LogWarning($"[FinalExitDoor] La escena '{nextSceneName}' no está en Build Settings; mostrando pantalla de nivel completado.");
                }
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
