using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class BubbleGameManager : MonoBehaviour
{
    [Header("Dirección")]
    public bool invertDirection = false;

    [Header("Despawn (seguro por recorrido)")]
    [Tooltip("Distancia máxima que puede recorrer una burbuja antes de eliminarse.")]
    public float maxTravelMeters = 12f;

    [Header("Burbujas / Prefab")]
    [Tooltip("Prefab de burbuja (debe tener BubbleAppearance en el prefab).")]
    public GameObject bubblePrefab;

    [Tooltip("Área de spawn (BoxCollider). Z+ debe apuntar hacia el jugador.")]
    public BoxCollider spawnArea;

    [Header("Sprites de Productos")]
    public List<Sprite> productSprites = new List<Sprite>();
    public float productSize = 0.25f;
    public Vector3 productLocalOffset = Vector3.zero;
    public bool productBillboard = true;

    [Header("Movimiento y spawn")]
    public float minSpeed = 0.7f;
    public float maxSpeed = 1.8f;
    public float speedMultiplier = 1f;
    public float spawnInterval = 0.8f;
    public int maxConcurrentBubbles = 25;
    public float bubbleTriggerRadius = 0.18f;

    [Header("Jugador / VR")]
    public Transform userHead;
    public LayerMask catchLayers;
    public float despawnAfterUserMeters = 1.5f;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI countdownText;
    public GameObject panelInicio;
    public GameObject panelFinal;
    public TextMeshProUGUI finalScoreText;

    [Header("Tiempo de juego")]
    public int initialTimeSeconds = 60;

    [Header("Sonidos")]
    public AudioSource sfxSource;
    public AudioClip sfxOnScore;
    public AudioClip sfxOnStart;
    public AudioClip sfxOnTimeUp;

    [Header("Flujo (Escenas)")]
    public string mainMenuSceneName = "01_MenuPrincipal";

    // Internos
    private readonly List<BubbleData> activeBubbles = new List<BubbleData>();
    private Coroutine spawnCo, timerCo, countdownCo;
    private int score = 0;
    private float timeLeft = 0f;
    private bool playing = false;

    private struct BubbleData
    {
        public Transform t;
        public float speed;
        public Vector3 dir;
        public BubbleProxy proxy;

        // NUEVO: para saber si ya cruzó el plano del usuario
        public bool crossedUserPlane;

        // NUEVO: punto de partida para medir recorrido
        public Vector3 startPos;
    }

    // Reenvía colisiones al GM
    private class BubbleProxy : MonoBehaviour
    {
        public BubbleGameManager gm;
        public SphereCollider triggerCol;
        private void OnTriggerEnter(Collider other)
        {
            if (gm == null) return;
            if (((1 << other.gameObject.layer) & gm.catchLayers.value) != 0)
                gm.OnBubbleTouched(this);
        }
    }

    // Billboard del sprite de producto
    private class BillboardToCamera : MonoBehaviour
    {
        public Transform target;
        void LateUpdate()
        {
            if (!target) return;
            Vector3 fwd = (transform.position - target.position).normalized;
            if (fwd.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }

    private void Awake()
    {
        if (panelInicio) panelInicio.SetActive(true);
        if (panelFinal) panelFinal.SetActive(false);
        if (countdownText) countdownText.text = "";
        UpdateScoreUI();
        UpdateTimeUI(initialTimeSeconds);

        if (!spawnArea) Debug.LogError("[GM] Asigna spawnArea.");
        if (!userHead) Debug.LogError("[GM] Asigna userHead (Main Camera).");
        if (!bubblePrefab) Debug.LogError("[GM] Asigna bubblePrefab (con BubbleAppearance).");
    }

    // Botones
    public void OnStartButtonClicked()
    {
        if (countdownCo != null) StopCoroutine(countdownCo);
        countdownCo = StartCoroutine(StartSequence());
    }
    public void OnRestartButtonClicked() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    public void OnMainMenuButtonClicked()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName)) SceneManager.LoadScene(mainMenuSceneName);
        else Debug.LogWarning("[GM] mainMenuSceneName vacío.");
    }

    // Flujo
    private IEnumerator StartSequence()
    {
        if (panelInicio) panelInicio.SetActive(false);
        if (panelFinal) panelFinal.SetActive(false);
        score = 0; UpdateScoreUI();

        yield return ShowCountdown("3", 0.8f);
        yield return ShowCountdown("2", 0.8f);
        yield return ShowCountdown("1", 0.8f);
        yield return ShowCountdown("GO!", 0.6f);
        yield return ShowCountdown("", 0f);

        PlaySFX(sfxOnStart);
        StartGame();
    }
    private IEnumerator ShowCountdown(string msg, float dur)
    {
        if (countdownText) countdownText.text = msg;
        yield return new WaitForSeconds(dur);
    }
    private void StartGame()
    {
        playing = true;
        timeLeft = initialTimeSeconds;
        UpdateTimeUI(timeLeft);

        if (spawnCo != null) StopCoroutine(spawnCo);
        if (timerCo != null) StopCoroutine(timerCo);

        spawnCo = StartCoroutine(SpawnLoop());
        timerCo = StartCoroutine(TimerLoop());
    }
    private IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);
        while (playing)
        {
            if (activeBubbles.Count < maxConcurrentBubbles)
                SpawnOneBubble();
            yield return wait;
        }
    }
    private IEnumerator TimerLoop()
    {
        while (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft < 0f) timeLeft = 0f;
            UpdateTimeUI(timeLeft);
            yield return null;
        }
        EndGame();
    }
    private void EndGame()
    {
        playing = false;
        if (spawnCo != null) StopCoroutine(spawnCo);
        if (timerCo != null) StopCoroutine(timerCo);

        PlaySFX(sfxOnTimeUp);

        for (int i = activeBubbles.Count - 1; i >= 0; i--)
            if (activeBubbles[i].t) Destroy(activeBubbles[i].t.gameObject);
        activeBubbles.Clear();

        if (finalScoreText) finalScoreText.text = score.ToString();
        if (panelFinal) panelFinal.SetActive(true);
    }

    private void FitSpriteInsideShell(Transform bubbleRoot, Transform product, float padding = 0.85f)
    {
        var sr = product.GetComponent<SpriteRenderer>();
        if (!sr || !sr.sprite) return;

        // 1) Diámetro del shell (si existe), con fallback
        float shellDiameter = 0.35f; // fallback
        var shell = bubbleRoot.Find("Shell");
        if (shell)
        {
            var r = shell.GetComponent<Renderer>();
            if (r) shellDiameter = Mathf.Min(r.bounds.size.x, r.bounds.size.y);
        }

        // 2) Ancho del sprite (unidades, escala = 1)
        float spriteWidthUnits = Mathf.Max(0.0001f, sr.sprite.bounds.size.x);

        // 3) Escala objetivo: quepa dentro con ‘padding’; respeta tu productSize como multiplicador fino
        float scale = (shellDiameter * padding) / spriteWidthUnits;
        product.localScale = Vector3.one * (scale * productSize);
    }

    // Spawn
    private void SpawnOneBubble()
    {
        if (!spawnArea || productSprites.Count == 0 || !bubblePrefab) return;

        //Vector3 local = RandomPointInBounds(spawnArea.center, spawnArea.size);
        //Vector3 worldPos = spawnArea.transform.TransformPoint(local);
        Vector3 worldPos = GetSafeSpawnWorldPos((invertDirection ? -spawnArea.transform.forward : spawnArea.transform.forward).normalized);
        Vector3 dir = (invertDirection ? -spawnArea.transform.forward : spawnArea.transform.forward).normalized;

        GameObject go = Instantiate(bubblePrefab, worldPos, Quaternion.identity);
        go.transform.forward = dir;

        // Trigger + Proxy
        var proxy = go.AddComponent<BubbleProxy>();
        proxy.gm = this;

        var col = go.GetComponent<SphereCollider>();
        if (!col) col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = bubbleTriggerRadius;
        proxy.triggerCol = col;

        var rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // Solo el sprite del producto (la apariencia ya la hace BubbleAppearance en el prefab)
        EnsureProductSprite(go.transform);

        float spd = Random.Range(minSpeed, maxSpeed) * Mathf.Max(0.01f, speedMultiplier);
        //activeBubbles.Add(new BubbleData { t = go.transform, speed = spd, dir = dir, proxy = proxy });

        float s0 = Vector3.Dot(worldPos - userHead.position, dir);
        activeBubbles.Add(new BubbleData
        {
            t = go.transform,
            speed = spd,
            dir = dir,
            proxy = proxy,
            crossedUserPlane = (s0 >= 0f), // si nace delante del usuario ya está "cruzada"
            startPos = worldPos
        });
    }

    private void EnsureProductSprite(Transform parent)
    {
        Transform prod = parent.Find("Product");
        if (!prod)
        {
            var go = new GameObject("Product");
            prod = go.transform;
            prod.SetParent(parent, false);
        }

        prod.localPosition = productLocalOffset;
        prod.localRotation = Quaternion.identity;
        //prod.localScale = Vector3.one * productSize; // escala inicial (luego se ajusta)
        prod.localScale = Vector3.one * 0.001f; // <- evitar “flash” grande el primer frame

        var sr = prod.GetComponent<SpriteRenderer>();
        if (!sr) sr = prod.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = productSprites[Random.Range(0, productSprites.Count)];
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sortingOrder = 10;

        sr.enabled = false; // <- NO mostrar hasta hacer el fit

        var bb = prod.GetComponent<BillboardToCamera>();
        if (productBillboard)
        {
            if (!bb) bb = prod.gameObject.AddComponent<BillboardToCamera>();
            bb.target = userHead;
        }
        else if (bb) Destroy(bb);

        // Hacer el “fit” en el siguiente frame (cuando ya exista el Shell creado por BubbleAppearance)
        StartCoroutine(FitSpriteNextFrame(parent, prod, 0.85f)); // padding 0.85 = deja margen dentro de la burbuja
    }

    private System.Collections.IEnumerator FitSpriteNextFrame(Transform bubbleRoot, Transform product, float padding)
    {
        yield return null; // espera a que corran los Start() (se creará "Shell")
        FitSpriteInsideShell(bubbleRoot, product, padding);

        var sr = product.GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = true; // <- ahora sí, ya con la escala correcta
    }

    private Vector3 RandomPointInBounds(Vector3 center, Vector3 size)
    {
        return new Vector3(
            center.x + (Random.value - 0.5f) * size.x,
            center.y + (Random.value - 0.5f) * size.y,
            center.z + (Random.value - 0.5f) * size.z
        );
    }

    private void Update()
    {
        if (!playing || activeBubbles.Count == 0) return;
        if (!userHead || !spawnArea) return;

        Vector3 forward = spawnArea.transform.forward.normalized;


        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            var b = activeBubbles[i];
            if (!b.t) { activeBubbles.RemoveAt(i); continue; }

            b.t.position += b.dir * b.speed * Time.deltaTime;

            // Distancia *firmada* al plano del usuario en la dirección real de la burbuja
            float s = Vector3.Dot(b.t.position - userHead.position, b.dir);

            // Marca cuándo CRUZA el plano del usuario (s pasa a >= 0)
            if (!b.crossedUserPlane && s >= 0f)
            {
                b.crossedUserPlane = true;
            }

            // 1) Regla principal: eliminar SOLO después de cruzar el plano del usuario
            //    y cuando ya esté N metros MÁS ALLÁ en esa misma dirección.
            if (b.crossedUserPlane && s >= despawnAfterUserMeters)
            {
                Destroy(b.t.gameObject);
                activeBubbles.RemoveAt(i);
                continue;
            }

            // 2) Seguro por recorrido absoluto (opcional pero recomendado):
            //    si la burbuja ya recorrió maxTravelMeters, elimínala.
            float traveled = Vector3.Distance(b.t.position, b.startPos);
            if (traveled >= maxTravelMeters)
            {
                Destroy(b.t.gameObject);
                activeBubbles.RemoveAt(i);
                continue;
            }

            activeBubbles[i] = b;
        }
    }

    private void OnBubbleTouched(BubbleProxy proxy)
    {
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i].proxy == proxy)
            {
                score++;
                UpdateScoreUI();
                PlaySFX(sfxOnScore);
                if (activeBubbles[i].t)
                    Destroy(activeBubbles[i].t.gameObject);
                activeBubbles.RemoveAt(i);
                break;
            }
        }
    }

    private void UpdateScoreUI() { if (scoreText) scoreText.text = score.ToString("0"); }
    private void UpdateTimeUI(float seconds)
    {
        if (!timeText) return;
        int s = Mathf.CeilToInt(seconds);
        timeText.text = $"{(s / 60):00}:{(s % 60):00}";
    }
    private void PlaySFX(AudioClip clip) { if (sfxSource && clip) sfxSource.PlayOneShot(clip); }

    private void OnDrawGizmosSelected()
    {
        if (spawnArea)
        {
            Gizmos.color = new Color(0f, 0.6f, 1f, 0.15f);
            Matrix4x4 m = Matrix4x4.TRS(spawnArea.transform.TransformPoint(spawnArea.center), spawnArea.transform.rotation, spawnArea.transform.lossyScale);
            Gizmos.matrix = m;
            Gizmos.DrawCube(Vector3.zero, spawnArea.size);

            Gizmos.color = Color.cyan;
            Vector3 origin = spawnArea.transform.position;
            Vector3 dir = spawnArea.transform.forward * 0.8f;
            Gizmos.DrawLine(origin, origin + dir);
            Gizmos.DrawSphere(origin + dir, 0.03f);
        }
    }

    // Devuelve un punto de spawn que esté ANTES del plano del usuario (s < -margen)
    private Vector3 GetSafeSpawnWorldPos(Vector3 dir, float margin = 0.05f, int maxTries = 6)
    {
        Vector3 worldPos = spawnArea.transform.TransformPoint(RandomPointInBounds(spawnArea.center, spawnArea.size));
        float s = Vector3.Dot(worldPos - userHead.position, dir);

        int tries = 0;
        while (s >= -margin && tries < maxTries)
        {
            worldPos = spawnArea.transform.TransformPoint(RandomPointInBounds(spawnArea.center, spawnArea.size));
            s = Vector3.Dot(worldPos - userHead.position, dir);
            tries++;
        }

        // Si no se logró un punto válido, empuja un poco hacia atrás del usuario
        if (s >= -margin)
            worldPos -= dir * (margin * 2f);

        return worldPos;
    }

}
