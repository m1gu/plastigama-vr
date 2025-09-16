using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;

public class TourManager : MonoBehaviour
{
    [System.Serializable]
    public class TourStep
    {
        public string id;                       // "CanalesBajantes", "TanqueSeptico", "Tanque"
        public Transform startPoint;            // Punto de inicio del usuario para este paso
        public AudioClip narration;             // Locución IA
        public AudioClip narration2;     // Locución 2 opcional para este paso
        public GameObject infoPanel;            // Panel de información (tu UI manual)
        public GameObject card1;         // Card que se muestra con la locución 1
        public GameObject card2;         // Card que se muestra con la locución 2
        public BlinkHighlighter[] blinkTargets; // Opcional: grupos a parpadear (solo donde aplique)
        public UnityEvent onEnter;              // Eventos especiales (p.ej. VFX "aparece tanque", anim tapa)
        public UnityEvent onExit;
        public bool autoAdvance = true;         // Avanzar al terminar locución
        public Animator animTarget;        // Objeto a animar (debe tener Animator)
        public AnimationClip oneShotClip;  // Clip que se ejecuta 1 vez
        public bool playClipOnEnter;       // Si true, se dispara al entrar al step
    }

    [Header("Tour Steps (3 productos en orden)")]
    public TourStep[] steps; // 0: Canales/Bajantes, 1: Tanque Séptico, 2: Tanque

    [Header("Step 1 (visible solo en este paso)")]
    public GameObject step1Exclusive; // arrastra aquí tu modelo 3D

    [Header("General")]
    public Transform xrRig;             // XROrigin raíz (o rig principal)
    public CanvasGroup fader;           // CanvasGroup full-screen para fade
    public float fadeTime = 0.25f;
    public AudioSource voice;           // AudioSource para locución
    public Transform homePoint;         // Punto final (afuera) o de retorno a MenuCasa

    [Header("UI Botones")]
    public Button btnIniciar;
    public Button btnSiguiente;
    public Button btnFinalizar;

    int index = -1;
    Coroutine running;

    // --- NUEVO: control interno de visibilidad de botones durante locución ---
    bool navButtonsAllowed = true;              // si false, se ocultan aunque SetButtons los “quiera” visibles
    bool desiredIniciar, desiredSiguiente, desiredFinalizar; // estado deseado por SetButtons()

    PlayableGraph _oneShotGraph;
    bool _oneShotGraphActive = false;

    void Awake()
    {
        // Estado inicial UI
        SetAllPanels(false);
        SetButtons(iniciar: true, siguiente: false, finalizar: false);
        SetFade(0f);

        if (step1Exclusive) step1Exclusive.SetActive(false);

    }

    void OnEnable()
    {
        if (btnIniciar) btnIniciar.onClick.AddListener(StartTour);
        if (btnSiguiente) btnSiguiente.onClick.AddListener(Next);
        if (btnFinalizar) btnFinalizar.onClick.AddListener(FinishTour);
    }

    void OnDisable()
    {
        if (btnIniciar) btnIniciar.onClick.RemoveListener(StartTour);
        if (btnSiguiente) btnSiguiente.onClick.RemoveListener(Next);
        if (btnFinalizar) btnFinalizar.onClick.RemoveListener(FinishTour);
    }

    // --- API Botones ---
    public void StartTour()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(CoGoToStep(0));
    }

    public void Next()
    {
        if (index + 1 < steps.Length)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(CoGoToStep(index + 1));
        }
        else
        {
            FinishTour();
        }
    }

    public void FinishTour()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(CoFinish());
    }

    // --- Core ---
    IEnumerator CoGoToStep(int target)
    {
        // Asegurar que no quede un gráfico anterior activo
        if (_oneShotGraphActive)
        {
            _oneShotGraph.Destroy();
            _oneShotGraphActive = false;
        }

        // cerrar paso anterior
        if (index >= 0 && index < steps.Length)
        {
            var prev = steps[index];
            prev.onExit?.Invoke();
            if (prev.infoPanel) prev.infoPanel.SetActive(false);
        }

        // fade out
        yield return CoFade(1f, fadeTime);

        

        // teleport a startPoint
        var step = steps[target];
        TeleportTo(step.startPoint);

        // preparar paso
        index = target;

        if (step1Exclusive) step1Exclusive.SetActive(index == 0);

        SetButtons(iniciar: false, siguiente: true, finalizar: (index == steps.Length - 1));

        // abrir panel + efectos
        if (step.infoPanel) step.infoPanel.SetActive(true);
        if (step.blinkTargets != null)
            foreach (var b in step.blinkTargets) if (b) b.PlayBlink();

        step.onEnter?.Invoke();

        // fade in
        yield return CoFade(0f, fadeTime);

        // --- Step 2 (Tanque): reproducir clip 1 vez y quedarse en el último frame ---
        if (index == 1) // tercer producto (0,1,2)
        {
            if (step.playClipOnEnter && step.animTarget && step.oneShotClip)
            {
                // Iniciar gráfico de Playables para reproducir el clip una vez
                AnimationPlayableUtilities.PlayClip(step.animTarget, step.oneShotClip, out _oneShotGraph);
                _oneShotGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _oneShotGraphActive = true;

                // Pausar en el último frame (sin bucle)
                StartCoroutine(CoHoldLastFrame(step.oneShotClip.length));
            }
        }

        // locución (soporta 1 o 2 clips y alterna card1 / card2 si están asignados)
        if (voice && (step.narration || step.narration2))
        {
            // Ocultar botones durante la(s) locución(es)
            SetNavButtonsAllowed(false);

            // Estado inicial de cards (si existen): card1 ON, card2 OFF
            if (step.card1) step.card1.SetActive(true);
            if (step.card2) step.card2.SetActive(false);

            // ----- Locución 1 -----
            if (step.narration)
            {
                voice.Stop();
                voice.clip = step.narration;
                voice.Play();
                yield return new WaitWhile(() => voice.isPlaying);
            }

            // Cambiar cards: card1 OFF, card2 ON
            if (step.card1) step.card1.SetActive(false);
            if (step.card2) step.card2.SetActive(true);

            // ----- Locución 2 -----
            if (step.narration2)
            {
                voice.Stop();
                voice.clip = step.narration2;
                voice.Play();
                yield return new WaitWhile(() => voice.isPlaying);
            }

            // Si es el último paso, conservar sólo "Finalizar"
            if (index == steps.Length - 1)
            {
                SetNavButtonsAllowed(true);
                SetButtons(iniciar: false, siguiente: false, finalizar: true);
                yield break; // Espera a que el usuario pulse "Finalizar"
            }

            // Pasos intermedios: respetar comportamiento existente
            if (step.autoAdvance)
            {
                yield return new WaitForSeconds(0.1f);
                Next();
                yield break;
            }
            else
            {
                SetNavButtonsAllowed(true);
            }
        }
        else
        {
            // No hay locuciones en este paso: restaurar botones
            SetNavButtonsAllowed(true);
        }
    }

    IEnumerator CoFinish()
    {
        // cierre del paso actual
        if (index >= 0 && index < steps.Length)
        {
            var cur = steps[index];
            cur.onExit?.Invoke();
            if (cur.infoPanel) cur.infoPanel.SetActive(false);
        }

        // fade + teleport home + fade in
        yield return CoFade(1f, fadeTime);
        if (homePoint) TeleportTo(homePoint);
        yield return CoFade(0f, fadeTime);

        // reset UI
        SetNavButtonsAllowed(true); // asegurar que los botones vuelvan a estar permitidos
        SetButtons(iniciar: true, siguiente: false, finalizar: false);
        index = -1;

        // --- Ir al menú ---
        SceneManager.LoadScene("01_MenuCasa");
    }

    void TeleportTo(Transform t)
    {
        if (!t || !xrRig) return;

        // Si usas CharacterController en el rig, hay que deshabilitarlo al mover
        var cc = xrRig.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        xrRig.SetPositionAndRotation(t.position, t.rotation);

        if (cc) cc.enabled = true;
    }

    void SetAllPanels(bool state)
    {
        if (steps == null) return;
        foreach (var s in steps)
            if (s != null && s.infoPanel) s.infoPanel.SetActive(state);
    }

    void SetButtons(bool iniciar, bool siguiente, bool finalizar)
    {
        // Guardar el estado "deseado"
        desiredIniciar = iniciar;
        desiredSiguiente = siguiente;
        desiredFinalizar = finalizar;

        // Aplicar según si están permitidos (no hay locución)
        if (btnIniciar) btnIniciar.gameObject.SetActive(desiredIniciar && navButtonsAllowed);
        if (btnSiguiente) btnSiguiente.gameObject.SetActive(desiredSiguiente && navButtonsAllowed);
        if (btnFinalizar) btnFinalizar.gameObject.SetActive(desiredFinalizar && navButtonsAllowed);
    }

    // --- NUEVO: permitir/ocultar botones globalmente durante locución ---
    void SetNavButtonsAllowed(bool allowed)
    {
        navButtonsAllowed = allowed;
        // reaplica el último estado deseado para reflejar el cambio
        if (btnIniciar) btnIniciar.gameObject.SetActive(desiredIniciar && navButtonsAllowed);
        if (btnSiguiente) btnSiguiente.gameObject.SetActive(desiredSiguiente && navButtonsAllowed);
        if (btnFinalizar) btnFinalizar.gameObject.SetActive(desiredFinalizar && navButtonsAllowed);
    }

    void SetFade(float a)
    {
        if (!fader) return;
        fader.alpha = a;
        fader.blocksRaycasts = a > 0.001f;
        fader.interactable = a > 0.001f;
    }

    IEnumerator CoFade(float target, float time)
    {
        if (!fader || time <= 0f) { SetFade(target); yield break; }
        float start = fader.alpha;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(start, target, t / time));
            yield return null;
        }
        SetFade(target);
    }

    IEnumerator CoHoldLastFrame(float clipLen)
    {
        // Espera a que termine el clip
        yield return new WaitForSeconds(clipLen);

        if (_oneShotGraphActive && _oneShotGraph.IsValid())
        {
            // Ir al final y pausar (se queda mostrando el último frame)
            var root = _oneShotGraph.GetRootPlayable(0);
            root.SetTime(clipLen);
            root.SetSpeed(0);
            // Importante: NO destruir el graph aquí para que conserve la pose.
            // Se destruirá automáticamente cuando salgas del paso (al entrar a otro step), en el bloque de limpieza.
        }
    }

}
