using UnityEngine;
using UnityEngine.UI;

namespace GridEmpire.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public class HUDScaleTarget : MonoBehaviour
    {
        private CanvasScaler _scaler;

        private void Awake() => _scaler = GetComponent<CanvasScaler>();

        private void OnEnable()
        {
            HUDScaleController.OnHUDScaleChanged += Apply;
            Apply(HUDScaleController.GetScale());
        }

        private void OnDisable() => HUDScaleController.OnHUDScaleChanged -= Apply;

        private void Apply(float scale)
        {
            // "Constant Pixel Size" módnál ez közvetlenül működik.
            // Ha "Scale With Screen Size" módot használsz, inkább a
            // referenceResolution-t oszd el a scale-lel az alábbi sorral:
            // _scaler.referenceResolution /= scale;
            _scaler.scaleFactor = scale;
        }
    }
}