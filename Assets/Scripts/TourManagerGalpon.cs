using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Collections;

public class TourManagerGalpon : MonoBehaviour
{
    // ======================
    //   XR / ESCENARIO
    // ======================
    [Header("XR Rig (Animator con States: XR_GoUp, XR_GoDownCenter, XR_GoOutside)")]
    public Animator xrRigAnimator;

    [Header("Animadores del escenario")]
    public Animator roofAnimator;           // State: Roof_Open
    public Animator floorInteriorAnimator;  // State: FloorInterior_Open
    public Animator floorExteriorAnimator;  // (Opcional) State: FloorExterior_Open

    [Header("Triggers (Animator Parameters)")]
    public string xrTrig_GoUp = "GoUp";
    public string xrTrig_GoDownCenter = "GoDownCenter";
    public string xrTrig_GoOutside = "GoOutside";
    public string roofTrig_Open = "OpenRoof";
    public string floorInteriorTrig_Open = "OpenFloor";
    public string floorExteriorTrig_Open = "OpenExterior";

    [Header("State names (Animator States exactos)")]
    public string xrState_GoUp = "XR_GoUp";
    public string xrState_GoDownCenter = "XR_GoDownCenter";
    public string xrState_GoOutside = "XR_GoOutside";
    public string roofState_Open = "Roof_Open";
    public string floorInteriorState_Open = "FloorInterior_Open";
    public string floorExteriorState_Open = "FloorExterior_Open"; // opcional

    [Header("Tiempos del guion")]
    public float waitOnRoofSeconds = 2f;
    public float waitAfterRoofOpenSeconds = 2f;

    // ======================
    //   VIDEOS (SIN AUDIO)
    // ======================
    [Header("Videos (sin audio)")]
    [Tooltip("VideoPlayer del panel del Step 1 (RenderTexture ya asignado en TargetTexture)")]
    public VideoPlayer video1;
    [Tooltip("Nombre del archivo MP4 en StreamingAssets (ej: producto1.mp4)")]
    public string video1FileName = "producto1.mp4";

    [Tooltip("VideoPlayer del panel del Step 2")]
    public VideoPlayer video2;
    public string video2FileName = "producto2.mp4";

    [Tooltip("VideoPlayer del panel del Step 3")]
    public VideoPlayer video3;
    public string video3FileName = "producto3.mp4";

    [Header("Opciones de Video")]
    [Tooltip("Si está activo, copiará el MP4 de StreamingAssets a persistentDataPath en Android para máxima compatibilidad.")]
    public bool androidCopyToPersistent = true;

    // ======================
    //   LOCUCIONES / PANELES
    // ======================
    [Header("STEP 1 - Techo (locución aparte)")]
    public AudioSource voice1;
    public AudioClip locucion1;
    public GameObject panelInfo1;

    [Header("STEP 2 - Piso Interior")]
    public AudioSource voice2;
    public AudioClip locucion2;
    public GameObject panelInfo2;

    [Header("STEP 3 - Exterior")]
    public AudioSource voice3;
    public AudioClip locucion3;
    public GameObject panelInfo3;

    // ======================
    //   UI / ESCENA
    // ======================
    [Header("UI Botones")]
    public Button btnIniciar;    // INICIAR
    public Button btnSiguiente;  // SIGUIENTE (Step1 y Step2)
    public Button btnFinalizar;  // FINALIZAR (Step3)

    [Header("Fader opcional")]
    public CanvasGroup fader;
    public float fadeTime = 0.25f;

    [Header("Escena final")]
    public string sceneToLoadOnFinish = "01_MenuPrincipal";

    // ----------------------
    //  NUEVO: Modelos 3D que giran por producto
    // ----------------------
    [Header("Modelos 3D por producto (se activan al entrar al paso)")]
    public GameObject product1Model;  // asigna el modelo del producto 1 (desactivado al inicio)
    public GameObject product2Model;  // asigna el modelo del producto 2 (desactivado al inicio)
    public GameObject product3Model;  // asigna el modelo del producto 3 (desactivado al inicio)

    [Tooltip("Velocidad de giro en grados/segundo alrededor del eje Y (mundo)")]
    public float productModelRotateSpeed = 30f;

    // ----------------------
    // Estado interno
    // ----------------------
    private enum TourState { IdleOutside, Step1, Step2, Step3, Finished }
    private TourState state = TourState.IdleOutside;
    private Coroutine running;
    private bool navButtonsAllowed = true;
    private bool wantStart, wantNext, wantFinish;

    // ======================
    //   Unity lifecycle
    // ======================
    void Awake()
    {
        // Panels off
        SafeSetActive(panelInfo1, false);
        SafeSetActive(panelInfo2, false);
        SafeSetActive(panelInfo3, false);

        // VideoPlayers config (sin audio, RT, no auto-play)
        SetupVideoPlayer(video1);
        SetupVideoPlayer(video2);
        SetupVideoPlayer(video3);

        // NUEVO: asegurar que los modelos arranquen ocultos (si están asignados)
        SafeSetActive(product1Model, false);
        SafeSetActive(product2Model, false);
        SafeSetActive(product3Model, false);

        SetButtons(true, false, false);
        SetFadeImmediate(0f);
    }

    void OnEnable()
    {
        if (btnIniciar) btnIniciar.onClick.AddListener(OnStartClicked);
        if (btnSiguiente) btnSiguiente.onClick.AddListener(OnNextClicked);
        if (btnFinalizar) btnFinalizar.onClick.AddListener(OnFinishClicked);
    }

    void OnDisable()
    {
        if (btnIniciar) btnIniciar.onClick.RemoveListener(OnStartClicked);
        if (btnSiguiente) btnSiguiente.onClick.RemoveListener(OnNextClicked);
        if (btnFinalizar) btnFinalizar.onClick.RemoveListener(OnFinishClicked);
    }

    // NUEVO: girar modelos activos
    void Update()
    {
        RotateIfActive(product1Model);
        RotateIfActive(product2Model);
        RotateIfActive(product3Model);
    }

    // ======================
    //   UI Callbacks
    // ======================
    private void OnStartClicked()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(CoStep1());
    }

    private void OnNextClicked()
    {
        if (state == TourState.Step1)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(CoStep2());
        }
        else if (state == TourState.Step2)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(CoStep3());
        }
    }

    private void OnFinishClicked()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(CoFinish());
    }

    // ======================
    //   Pasos del Tour
    // ======================

    // STEP 1: XR_GoUp - pausa - Roof_Open - pausa - XR_GoDownCenter - locución1 + panel + video1 - SIGUIENTE
    private IEnumerator CoStep1()
    {
        state = TourState.Step1;
        SetButtons(false, false, false);

        // Subir al techo
        SetTriggerSafe(xrRigAnimator, xrTrig_GoUp);
        yield return WaitForState(xrRigAnimator, xrState_GoUp);

        if (waitOnRoofSeconds > 0f) yield return new WaitForSeconds(waitOnRoofSeconds);

        // Abrir techo
        SetTriggerSafe(roofAnimator, roofTrig_Open);
        yield return WaitForState(roofAnimator, roofState_Open);

        if (waitAfterRoofOpenSeconds > 0f) yield return new WaitForSeconds(waitAfterRoofOpenSeconds);

        // Bajar al centro
        SetTriggerSafe(xrRigAnimator, xrTrig_GoDownCenter);
        yield return WaitForState(xrRigAnimator, xrState_GoDownCenter);

        // Mostrar panel + reproducir video 1 (SIN audio)
        SafeSetActive(panelInfo1, true);
        yield return PrepareAndPlay(video1, video1FileName);

        // NUEVO: activar modelo 3D del producto 1
        SafeSetActive(product1Model, true);

        // Locución aparte
        SetNavButtonsAllowed(false);
        yield return PlayVoice(voice1, locucion1);

        SetNavButtonsAllowed(true);
        SetButtons(false, true, false); // SIGUIENTE
    }

    // STEP 2: FloorInterior_Open - locución2 + panel + video2 - SIGUIENTE
    private IEnumerator CoStep2()
    {
        state = TourState.Step2;
        // cerrar step 1
        SafeSetActive(panelInfo1, false);
        StopVideo(video1);
        // NUEVO: ocultar modelo 3D del producto 1
        SafeSetActive(product1Model, false);

        SetButtons(false, false, false);

        // Abrir piso interior
        SetTriggerSafe(floorInteriorAnimator, floorInteriorTrig_Open);
        yield return WaitForState(floorInteriorAnimator, floorInteriorState_Open);

        // Panel + video 2
        SafeSetActive(panelInfo2, true);
        yield return PrepareAndPlay(video2, video2FileName);

        // NUEVO: activar modelo 3D del producto 2
        SafeSetActive(product2Model, true);

        SetNavButtonsAllowed(false);
        yield return PlayVoice(voice2, locucion2);

        SetNavButtonsAllowed(true);
        SetButtons(false, true, false);
    }

    // STEP 3: XR_GoOutside - (opcional) FloorExterior_Open - locución3 + panel + video3 - FINALIZAR
    private IEnumerator CoStep3()
    {
        state = TourState.Step3;
        // cerrar step 2
        SafeSetActive(panelInfo2, false);
        StopVideo(video2);
        // NUEVO: ocultar modelo 3D del producto 2
        SafeSetActive(product2Model, false);

        SetButtons(false, false, false);

        // XR afuera
        SetTriggerSafe(xrRigAnimator, xrTrig_GoOutside);
        yield return WaitForState(xrRigAnimator, xrState_GoOutside);

        // Piso exterior (opcional)
        if (floorExteriorAnimator && !string.IsNullOrEmpty(floorExteriorTrig_Open))
        {
            SetTriggerSafe(floorExteriorAnimator, floorExteriorTrig_Open);
            if (!string.IsNullOrEmpty(floorExteriorState_Open))
                yield return WaitForState(floorExteriorAnimator, floorExteriorState_Open);
            else
                yield return new WaitForSeconds(0.25f);
        }

        // Panel + video 3
        SafeSetActive(panelInfo3, true);
        yield return PrepareAndPlay(video3, video3FileName);

        // NUEVO: activar modelo 3D del producto 3
        SafeSetActive(product3Model, true);

        SetNavButtonsAllowed(false);
        yield return PlayVoice(voice3, locucion3);

        SetNavButtonsAllowed(true);
        SetButtons(false, false, true); // FINALIZAR
    }

    private IEnumerator CoFinish()
    {
        state = TourState.Finished;

        SafeSetActive(panelInfo1, false);
        SafeSetActive(panelInfo2, false);
        SafeSetActive(panelInfo3, false);

        StopVideo(video1);
        StopVideo(video2);
        StopVideo(video3);

        // NUEVO: ocultar todos los modelos 3D al finalizar
        SafeSetActive(product1Model, false);
        SafeSetActive(product2Model, false);
        SafeSetActive(product3Model, false);

        SetButtons(false, false, false);
        yield return CoFade(1f, fadeTime);

        if (!string.IsNullOrEmpty(sceneToLoadOnFinish))
            SceneManager.LoadScene(sceneToLoadOnFinish);
        else
            yield return CoFade(0f, fadeTime);
    }

    // ======================
    //   Helpers
    // ======================

    private void SetupVideoPlayer(VideoPlayer vp)
    {
        if (!vp) return;
        vp.playOnAwake = false;
        vp.waitForFirstFrame = true;
        vp.skipOnDrop = true;
        vp.isLooping = false;
        vp.renderMode = VideoRenderMode.RenderTexture; // asegúrate que tenga TargetTexture asignado en el Inspector
        vp.audioOutputMode = VideoAudioOutputMode.None; // SIN AUDIO
        // Si el clip estuviera asignado en Inspector, lo ignoraremos al usar URL
    }

    private void StopVideo(VideoPlayer vp)
    {
        if (!vp) return;
        vp.Stop();
        var go = vp.gameObject;
        if (go && go.activeSelf) go.SetActive(false); // oculta el panel contenedor si lo deseas
    }

    private void SafeSetActive(GameObject go, bool state)
    {
        if (go && go.activeSelf != state) go.SetActive(state);
    }

    private void SetButtons(bool iniciar, bool siguiente, bool finalizar)
    {
        wantStart = iniciar;
        wantNext = siguiente;
        wantFinish = finalizar;

        if (btnIniciar) btnIniciar.gameObject.SetActive(iniciar && navButtonsAllowed);
        if (btnSiguiente) btnSiguiente.gameObject.SetActive(siguiente && navButtonsAllowed);
        if (btnFinalizar) btnFinalizar.gameObject.SetActive(finalizar && navButtonsAllowed);
    }

    private void SetNavButtonsAllowed(bool allowed)
    {
        navButtonsAllowed = allowed;
        if (btnIniciar) btnIniciar.gameObject.SetActive(wantStart && navButtonsAllowed);
        if (btnSiguiente) btnSiguiente.gameObject.SetActive(wantNext && navButtonsAllowed);
        if (btnFinalizar) btnFinalizar.gameObject.SetActive(wantFinish && navButtonsAllowed);
    }

    private void SetTriggerSafe(Animator anim, string trigger)
    {
        if (anim && !string.IsNullOrEmpty(trigger))
            anim.SetTrigger(trigger);
    }

    private IEnumerator WaitForState(Animator anim, string stateName, int layer = 0)
    {
        if (!anim || string.IsNullOrEmpty(stateName))
        {
            yield return null; yield break;
        }

        // esperar a ENTRAR al state
        var info = anim.GetCurrentAnimatorStateInfo(layer);
        while (!info.IsName(stateName))
        {
            yield return null;
            info = anim.GetCurrentAnimatorStateInfo(layer);
        }

        // esperar a COMPLETAR (normalizedTime >= 1)
        while (anim.GetCurrentAnimatorStateInfo(layer).normalizedTime < 1f)
            yield return null;
    }

    private IEnumerator PlayVoice(AudioSource src, AudioClip clip)
    {
        if (src && clip)
        {
            src.Stop();
            src.clip = clip;
            src.Play();
            yield return new WaitWhile(() => src.isPlaying);
        }
        else
        {
            yield return null;
        }
    }

    private void SetFadeImmediate(float a)
    {
        if (!fader) return;
        fader.alpha = a;
        fader.blocksRaycasts = a > 0.001f;
        fader.interactable = a > 0.001f;
    }

    private IEnumerator CoFade(float target, float time)
    {
        if (!fader || time <= 0f) { SetFadeImmediate(target); yield break; }
        float start = fader.alpha;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            SetFadeImmediate(Mathf.Lerp(start, target, t / time));
            yield return null;
        }
        SetFadeImmediate(target);
    }

    // --- VIDEO: cargar desde StreamingAssets y preparar ---
    private IEnumerator PrepareAndPlay(VideoPlayer vp, string fileName)
    {
        if (!vp || string.IsNullOrEmpty(fileName)) yield break;

        // Mostrar el panel contenedor (si estaba oculto)
        var go = vp.gameObject;
        if (go && !go.activeSelf) go.SetActive(true);

        // Construir ruta
        string saPath = System.IO.Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (androidCopyToPersistent)
        {
            // Compatibilidad máxima Android: copiar a persistentDataPath
            string dst = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            if (!System.IO.File.Exists(dst))
            {
                using (var req = UnityEngine.Networking.UnityWebRequest.Get(saPath))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Debug.LogError("Error leyendo StreamingAssets: " + req.error);
                        yield break;
                    }
                    System.IO.File.WriteAllBytes(dst, req.downloadHandler.data);
                }
            }
            vp.source = VideoSource.Url;
            vp.url = dst;
        }
        else
        {
            vp.source = VideoSource.Url;
            vp.url = saPath; // algunos Unity aceptan jar: paths directo
        }
#else
        vp.source = VideoSource.Url;
        vp.url = saPath;
#endif

        vp.Prepare();
        while (!vp.isPrepared) yield return null;

        vp.Play(); // SIN audio (audioOutputMode=None)
    }

    // ----------------------
    // NUEVO: helpers de rotación
    // ----------------------
    private void RotateIfActive(GameObject go)
    {
        if (!go || !go.activeInHierarchy) return;
        // Gira alrededor del eje Y del mundo para que no dependa de la rotación local del contenedor
        go.transform.Rotate(0f, productModelRotateSpeed * Time.deltaTime, 0f, Space.World);
    }
}
