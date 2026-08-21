using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursor Textures")]
    [SerializeField] private Texture2D defaultCursor;
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Texture2D grabCursor;

    [Header("Settings")]
    [SerializeField] private Vector2 defaultHotspot = Vector2.zero;
    [SerializeField] private Vector2 hoverHotspot = Vector2.zero;
    [SerializeField] private Vector2 grabHotspot = Vector2.zero;

    private const string ScaleKey = "CursorScale";
    private const float MinCursorScale = 0.5f;
    private const float MaxCursorScale = 3f;
    private const float DefaultCursorScale = 1f;

    private float _scale = DefaultCursorScale;

    private enum ActiveCursorType
    {
        Default,
        Hover,
        Grab
    }

    private ActiveCursorType _currentType = ActiveCursorType.Default;

    private Texture2D _scaledDefault;
    private Texture2D _scaledHover;
    private Texture2D _scaledGrab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _scale = PlayerPrefs.GetFloat(ScaleKey, DefaultCursorScale);
        _scale = Mathf.Clamp(_scale, MinCursorScale, MaxCursorScale);

        RegenerateScaledTextures();
    }

    private void Start()
    {
        SetDefaultCursor();
    }

    public float GetCursorScale() => _scale;

    public void SetCursorScale(float scale)
    {
        _scale = Mathf.Clamp(scale, MinCursorScale, MaxCursorScale);

        PlayerPrefs.SetFloat(ScaleKey, _scale);
        PlayerPrefs.Save();

        RegenerateScaledTextures();
        ReapplyCurrentCursor();
    }

    private void RegenerateScaledTextures()
    {
        float textureScale = _scale / MaxCursorScale;

        if (defaultCursor != null)
            _scaledDefault = ScaleTexture(defaultCursor, textureScale);

        if (hoverCursor != null)
            _scaledHover = ScaleTexture(hoverCursor, textureScale);

        if (grabCursor != null)
            _scaledGrab = ScaleTexture(grabCursor, textureScale);
    }

    private Texture2D ScaleTexture(Texture2D src, float scale)
    {
        int newW = Mathf.Max(1, Mathf.RoundToInt(src.width * scale));
        int newH = Mathf.Max(1, Mathf.RoundToInt(src.height * scale));

        var result = new Texture2D(
            newW,
            newH,
            TextureFormat.RGBA32,
            false
        );

        for (int y = 0; y < newH; y++)
        {
            for (int x = 0; x < newW; x++)
            {
                float u = (float)x / newW;
                float v = (float)y / newH;

                result.SetPixel(
                    x,
                    y,
                    src.GetPixelBilinear(u, v)
                );
            }
        }

        result.Apply();
        return result;
    }

    private void ReapplyCurrentCursor()
    {
        switch (_currentType)
        {
            case ActiveCursorType.Default:
                SetDefaultCursor();
                break;

            case ActiveCursorType.Hover:
                SetHoverCursor();
                break;

            case ActiveCursorType.Grab:
                SetGrabCursor();
                break;
        }
    }

    public void SetDefaultCursor()
    {
        _currentType = ActiveCursorType.Default;

        float textureScale = _scale / MaxCursorScale;

        var tex = _scaledDefault != null
            ? _scaledDefault
            : defaultCursor;

        Cursor.SetCursor(
            tex,
            defaultHotspot * textureScale,
            CursorMode.ForceSoftware
        );
    }

    public void SetHoverCursor()
    {
        _currentType = ActiveCursorType.Hover;

        float textureScale = _scale / MaxCursorScale;

        var tex = _scaledHover != null
            ? _scaledHover
            : hoverCursor;

        Cursor.SetCursor(
            tex,
            hoverHotspot * textureScale,
            CursorMode.ForceSoftware
        );
    }

    public void SetGrabCursor()
    {
        _currentType = ActiveCursorType.Grab;

        float textureScale = _scale / MaxCursorScale;

        var tex = _scaledGrab != null
            ? _scaledGrab
            : grabCursor;

        Cursor.SetCursor(
            tex,
            grabHotspot * textureScale,
            CursorMode.ForceSoftware
        );
    }
}