using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    // Llama este m�todo desde el bot�n "CASA"
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
