using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BlinkHighlighter : MonoBehaviour
{
    [ColorUsage(true, true)] public Color colorA = Color.white;
    [ColorUsage(true, true)] public Color colorB = new Color(1f, 0.5f, 0f); // naranja
    public float duration = 4f;
    public float frequency = 6f; // parpadeos por segundo

    List<Renderer> rends = new List<Renderer>();
    List<MaterialPropertyBlock> mpbs = new List<MaterialPropertyBlock>();

    void Awake()
    {
        GetComponentsInChildren(true, rends);
        foreach (var r in rends)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpbs.Add(mpb);
        }
    }

    public void PlayBlink()
    {
        StopAllCoroutines();
        StartCoroutine(CoBlink());
    }

    IEnumerator CoBlink()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.PingPong(t * frequency, 1f);
            Color c = Color.Lerp(colorA, colorB, s);

            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i];
                var mpb = mpbs[i];
                // URP usa _BaseColor; fallback a _Color
                if (r.sharedMaterial.HasProperty("_BaseColor"))
                    mpb.SetColor("_BaseColor", c);
                else if (r.sharedMaterial.HasProperty("_Color"))
                    mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
            yield return null;
        }

        // reset (blanco)
        for (int i = 0; i < rends.Count; i++)
        {
            var r = rends[i];
            var mpb = mpbs[i];
            if (r.sharedMaterial.HasProperty("_BaseColor"))
                mpb.SetColor("_BaseColor", colorA);
            else if (r.sharedMaterial.HasProperty("_Color"))
                mpb.SetColor("_Color", colorA);
            r.SetPropertyBlock(mpb);
        }
    }
}
