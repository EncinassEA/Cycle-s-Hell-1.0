using UnityEngine;
using UnityEngine.SceneManagement;
using CyclesHell.UI;

public class Opciones : MonoBehaviour
{
    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameSettingsManager.ApplySavedSettings();
    }

    public void CargarEscena(string NombreScena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(NombreScena);
    }

    public void Salir()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
