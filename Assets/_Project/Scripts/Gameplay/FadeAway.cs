using GridEmpire.Core;
using System.Collections;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class FadeAway : MonoBehaviour
    {
        public void Begin(float delay, System.Action onComplete = null)
        {
            float fadeDuration = TurnManager.Instance != null ? TurnManager.Instance.TickDuration : 1f;
            StartCoroutine(FadeAndDestroy(delay, fadeDuration, onComplete));
        }

        private IEnumerator FadeAndDestroy(float delay, float fadeDuration, System.Action onComplete)
        {
            yield return new WaitForSeconds(delay);

            var renderers = GetComponentsInChildren<Renderer>();
            var materials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material;
                SetTransparent(materials[i]);
            }

            float fadeDelay = fadeDuration * 0.5f;
            yield return new WaitForSeconds(fadeDelay);

            float elapsed = 0f;
            float actualFadeDuration = fadeDuration - fadeDelay;
            while (elapsed < actualFadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / actualFadeDuration);
                foreach (var mat in materials)
                    if (mat != null)
                    {
                        Color c = mat.GetColor("_BaseColor");
                        mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
                    }
                yield return null;
            }

            onComplete?.Invoke();
        }

        private void SetTransparent(Material mat)
        {
            if (mat == null) return;
            mat.SetFloat("_Surface", 1);                    // 1 = Transparent
            mat.SetFloat("_Blend", 0);                      // 0 = Alpha
            mat.SetFloat("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
    }
}