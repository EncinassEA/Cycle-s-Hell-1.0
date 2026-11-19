using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CyclesHell.UI
{
    /// <summary>
    /// Controlador de la pantalla de Créditos (Creditos.unity) con estética VHS 2004.
    /// Permite regresar al Menú Principal mediante botón o tecla Esc (Nuevo Input System).
    /// </summary>
    public class CreditsMenuUI : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private string mainMenuSceneName = "SampleScene";

        private InputAction backAction;

        private void Awake()
        {
            backAction = new InputAction("CreditsBack", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            backAction?.Enable();
            if (backButton != null)
            {
                backButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void OnDisable()
        {
            backAction?.Disable();
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(ReturnToMainMenu);
            }
        }

        private void OnDestroy()
        {
            backAction?.Dispose();
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettingsManager.ApplySavedSettings();
        }

        private void Update()
        {
            if (backAction.WasPressedThisFrame())
            {
                ReturnToMainMenu();
            }
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }
    }
}
