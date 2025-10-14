using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    // Llama este método desde el botón "CASA"
    public void AbrirCasa()
    {
        SceneManager.LoadScene("01_MenuCasa");
    }

    // (Opcional) si luego tienes otros botones:
    public void AbrirGalpon()
    {
        SceneManager.LoadScene("02_MenuGalpon");
    }

}
