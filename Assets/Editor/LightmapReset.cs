using UnityEngine;
using UnityEditor;

public class LightmapReset : MonoBehaviour
{
    [MenuItem("Tools/Clear Lightmap Data From Scene")]
    static void ClearLightmapData()
    {
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        foreach (Renderer r in renderers)
        {
            // Forzar lightmap index a -1
            SerializedObject so = new SerializedObject(r);
            var prop = so.FindProperty("m_LightmapIndex");
            if (prop != null)
            {
                prop.intValue = -1;
                so.ApplyModifiedProperties();
            }

            // Desactivar contribución a GI si está presente
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~StaticEditorFlags.ContributeGI);
        }

        // Borra datos bakeados de la escena
        Lightmapping.Clear();

        Debug.Log("✅ Todos los objetos quedaron libres de lightmaps y GI.");
    }
}
