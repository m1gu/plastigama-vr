using UnityEngine;

/// <summary>
/// Apariencia de burbuja para URP (Meta Quest).
/// Usa URP/Lit en modo Transparente con color naranja y centro más transparente.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class BubbleAppearanceURP : MonoBehaviour
{
    [Header("Apariencia")]
    [Tooltip("Color base (naranja). El canal A se sobreescribe con centerAlpha).")]
    public Color bubbleColor = new Color(1f, 0.62f, 0.2f, 1f); // naranja

    [Tooltip("Transparencia del centro (0 = invisible, 1 = opaco).")]
    [Range(0f, 1f)] public float centerAlpha = 0.12f;

    [Tooltip("Brillo especular (0-1).")]
    [Range(0f, 1f)] public float smoothness = 0.9f;

    [Tooltip("Metalicidad (0-1).")]
    [Range(0f, 1f)] public float metallic = 0.0f;

    [Tooltip("¿Superficie doble cara? (útil para cascarón delgado)")]
    public bool doubleSided = true;

    Material _mat;

    void Start()
    {
        var rend = GetComponent<Renderer>();
        if (!rend) return;

        // IMPORTANTE: URP/Lit (no Standard)
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null)
        {
            Debug.LogError("URP/Lit no encontrado. Asegúrate de que el proyecto usa URP.");
            return;
        }

        _mat = new Material(sh);
        _mat.name = "Bubble_URPLit_Instance";

        // Color base con alpha del centro
        var c = bubbleColor;
        c.a = Mathf.Clamp01(centerAlpha);
        _mat.SetColor("_BaseColor", c);

        // Transparente
        // _Surface: 0=Opaque, 1=Transparent
        _mat.SetFloat("_Surface", 1f);
        _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // Mezcla alfa clásica
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_ZWrite", 0); // no escribir Z para transparencia
        _mat.DisableKeyword("_ALPHATEST_ON");
        _mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // dejamos mezcla alfa normal
        _mat.renderQueue = 3000;

        // Especular y reflejos para que “marque” bordes con la luz
        _mat.SetFloat("_Smoothness", smoothness);
        _mat.SetFloat("_Metallic", metallic);
        _mat.EnableKeyword("_SPECULARHIGHLIGHTS_ON");
        _mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_ON");

        // Doble cara opcional (para cascarón fino)
        _mat.SetFloat("_Cull", doubleSided ? 0f : 2f); // 0 = Off, 2 = Back

        // Asignar al renderer
        rend.material = _mat;
    }
}
