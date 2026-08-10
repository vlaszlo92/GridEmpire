using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GridEmpire.UI
{
    public class StatRowConnector : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI labelText;
        public TMP_InputField inputField;

        [Header("Flash Settings")]
        [SerializeField] private Image flashTargetImage;
        [SerializeField] private Color flashColor = Color.red;
        [SerializeField] private float flashDuration = 1f;

        private Color _originalColor;
        private Coroutine _flashCoroutine;
        private Image FlashTarget => flashTargetImage != null ? flashTargetImage : inputField?.image;

        public void Init(string label, float value, bool interactable = true)
        {
            if (labelText != null) labelText.text = label;
            if (inputField != null)
            {
                inputField.text = value.ToString();
                inputField.interactable = interactable;
            }
        }

        public void UpdateValue(float value)
        {
            if (inputField != null) inputField.text = value.ToString();
        }

        public void Flash()
        {
            var target = FlashTarget;
            if (target == null) return;

            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine(target));
        }

        private IEnumerator FlashRoutine(Image target)
        {
            _originalColor = target.color;
            target.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            target.color = _originalColor;
            _flashCoroutine = null;
        }
    }
}