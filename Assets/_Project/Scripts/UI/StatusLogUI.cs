using System.Collections.Generic;
using TMPro;
using UnityEngine;
using GridEmpire.Networking;

namespace GridEmpire.UI
{
    public class StatusLogUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private int maxLines = 5;
        [SerializeField] private float messageLifetime = 8f;

        private readonly List<string> _lines = new List<string>();

        private void OnEnable() => ConnectionManager.OnStatusMessage += HandleStatusMessage;
        private void OnDisable() => ConnectionManager.OnStatusMessage -= HandleStatusMessage;

        private void HandleStatusMessage(string message)
        {
            _lines.Add(message);
            if (_lines.Count > maxLines) _lines.RemoveAt(0);
            RefreshText();

            if (messageLifetime > 0f)
                Invoke(nameof(ClearOldest), messageLifetime);
        }

        private void ClearOldest()
        {
            if (_lines.Count == 0) return;
            _lines.RemoveAt(0);
            RefreshText();
        }

        private void RefreshText()
        {
            if (logText != null) logText.text = string.Join("\n", _lines);
        }
    }
}