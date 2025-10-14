using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class HiddenHotspot : MonoBehaviour
{
    public string sceneToLoad = "01_MenuPrincipal";
    [Tooltip("Segundos de pulsación continua requeridos")]
    public float holdSeconds = 2.5f;

    XRSimpleInteractable interactable;
    Coroutine holding;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
        // Sin feedback visual ni audio; es invisible
    }

    void OnDestroy()
    {
        if (interactable)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (holding == null) holding = StartCoroutine(CoHoldThenLoad());
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (holding != null) { StopCoroutine(holding); holding = null; }
    }

    IEnumerator CoHoldThenLoad()
    {
        float t = 0f;
        while (t < holdSeconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
        SceneManager.LoadScene(sceneToLoad);
    }
}
