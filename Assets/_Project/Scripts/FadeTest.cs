using System.Collections;
using UnityEngine;

public class FadeTest : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float waitBetween = 5f;

    private Material[] _materials;
    private float _currentAlpha = 1f;
    private float _targetAlpha = 1f;
    private bool _isFading = false;
    private MeshRenderer[] _renderers;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>();
        _materials = System.Array.FindAll(
            System.Array.ConvertAll(_renderers, r => r.material),
            m => m.HasProperty("_BaseColor")
        );
    }

    private IEnumerator Start()
    {
        while (true)
        {
            yield return new WaitForSeconds(waitBetween);
            FadeTo(0f);
            yield return new WaitForSeconds(waitBetween);
            FadeTo(1f);
        }
    }

    private void FadeTo(float target) => _targetAlpha = target;

    private void Update()
    {
        if (Mathf.Approximately(_currentAlpha, _targetAlpha)) return;

        _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, Time.deltaTime / fadeDuration);

        // Megjelenítéskor azonnal enabled = true
        if (_targetAlpha > 0f)
            foreach (var r in _renderers) r.enabled = true;

        foreach (var mat in _materials)
        {
            Color c = mat.GetColor("_BaseColor");
            c.a = _currentAlpha;
            mat.SetColor("_BaseColor", c);
        }

        // Elrejtéskor csak akkor kapcsoljuk ki ha teljesen átlátszó
        if (Mathf.Approximately(_currentAlpha, 0f))
            foreach (var r in _renderers) r.enabled = false;
    }
}