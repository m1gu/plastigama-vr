using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuCasa : MonoBehaviour
{
    // Llama este método desde el botón "INICIAR RECORRIDO"
    public void AbrirCasa()
    {
        SceneManager.LoadScene("03_Casa");
    }

    public void AbrirJuego()
    {
        SceneManager.LoadScene("04_JuegoBurbujas");
    }

}
