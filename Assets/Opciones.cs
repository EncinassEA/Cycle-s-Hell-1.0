using UnityEngine;
using UnityEngine.SceneManagement;

public class Opciones : MonoBehaviour
{
    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CargarEscena(string NombreScena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(NombreScena);
    }

    public void Salir()
    {
        Application.Quit();
    }
}
