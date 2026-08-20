using UnityEngine;
using UnityEngine.UI;

public class SmokeDrift : MonoBehaviour
{
    [System.Serializable]
    private class SmokePuff
    {
        public RectTransform rect;
        public RawImage image;
        public Vector2 velocity;
        public float baseAlpha;
        public float phase;
    }

    [SerializeField] private RawImage[] smokeImages;
    [SerializeField] private float minSpeed = 8f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float minAlpha = 0.05f;
    [SerializeField] private float maxAlpha = 0.18f;
    [SerializeField] private float fadeCycleDuration = 12f;
    [SerializeField] private float wrapMargin = 400f;

    private SmokePuff[] _puffs;
    private RectTransform _bounds;

    private void Awake()
    {
        _bounds = GetComponent<RectTransform>();
        _puffs = new SmokePuff[smokeImages.Length];

        for (int i = 0; i < smokeImages.Length; i++)
        {
            var rt = smokeImages[i].rectTransform;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(minSpeed, maxSpeed);

            _puffs[i] = new SmokePuff
            {
                rect = rt,
                image = smokeImages[i],
                velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                baseAlpha = Random.Range(minAlpha, maxAlpha),
                phase = Random.Range(0f, fadeCycleDuration)
            };

            rt.anchoredPosition = new Vector2(
                Random.Range(-_bounds.rect.width * 0.5f, _bounds.rect.width * 0.5f),
                Random.Range(-_bounds.rect.height * 0.5f, _bounds.rect.height * 0.5f)
            );
        }
    }

    private void Update()
    {
        float w = _bounds.rect.width * 0.5f + wrapMargin;
        float h = _bounds.rect.height * 0.5f + wrapMargin;

        foreach (var puff in _puffs)
        {
            Vector2 pos = puff.rect.anchoredPosition + puff.velocity * Time.deltaTime;

            if (pos.x > w) pos.x = -w;
            else if (pos.x < -w) pos.x = w;
            if (pos.y > h) pos.y = -h;
            else if (pos.y < -h) pos.y = h;

            puff.rect.anchoredPosition = pos;

            float t = (Time.time + puff.phase) % fadeCycleDuration / fadeCycleDuration;
            float pulse = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;
            float alpha = puff.baseAlpha * Mathf.Lerp(0.5f, 1f, pulse);

            Color c = puff.image.color;
            c.a = alpha;
            puff.image.color = c;
        }
    }
}