using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuGalpon : MonoBehaviour
{
    public void AbrirGalpon()
    {
        SceneManager.LoadScene("05_Galpon");
    }
}
