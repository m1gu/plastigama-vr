using UnityEngine;

/// <summary>
/// Hace girar el objeto continuamente.
/// - Ajusta el eje y la velocidad en el Inspector.
/// - Puede rotar en espacio local (por defecto) o mundo.
/// </summary>
public class GiroContinuo : MonoBehaviour
{
    [Header("Parámetros de giro")]
    [Tooltip("Eje de giro. Ej: (0,1,0) para girar en Y.")]
    public Vector3 eje = new Vector3(0f, 1f, 0f);

    [Tooltip("Velocidad en grados por segundo.")]
    public float velocidad = 30f;

    [Tooltip("Usar espacio de mundo (true) o local (false).")]
    public bool espacioMundo = false;

    [Tooltip("Iniciar girando (puedes pausar en tiempo de ejecución).")]
    public bool activo = true;

    void Update()
    {
        if (!activo) return;

        // Normaliza el eje por seguridad
        Vector3 ejeNormal = eje.sqrMagnitude > 0.0001f ? eje.normalized : Vector3.up;
        float deltaGrados = velocidad * Time.deltaTime;

        transform.Rotate(ejeNormal, deltaGrados, espacioMundo ? Space.World : Space.Self);
    }

    /// <summary> Permite pausar/reanudar desde otros scripts o eventos UI. </summary>
    public void SetActivo(bool value) => activo = value;
}
