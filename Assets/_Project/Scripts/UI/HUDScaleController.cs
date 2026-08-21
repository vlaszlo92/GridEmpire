using UnityEngine;

namespace GridEmpire.UI
{
    public static class HUDScaleController
    {
        private const string ScaleKey = "HUDScale";
        public static event System.Action<float> OnHUDScaleChanged;

        public static float GetScale() => PlayerPrefs.GetFloat(ScaleKey, 1f);

        public static void SetScale(float value)
        {
            value = Mathf.Clamp(value, 0.75f, 1.5f);
            PlayerPrefs.SetFloat(ScaleKey, value);
            PlayerPrefs.Save();
            OnHUDScaleChanged?.Invoke(value);
        }
    }
}