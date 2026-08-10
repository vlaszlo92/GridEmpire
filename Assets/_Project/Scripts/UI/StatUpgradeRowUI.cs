using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using GridEmpire.Data;

namespace GridEmpire.UI
{
    public class StatUpgradeRowUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statNameText;
        [SerializeField] private TextMeshProUGUI valueAndLevelText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI buttonText; // A gomb belso felirata

        private StatType _statType;
        private Action<StatType> _onUpgradeClicked;

        public void Setup(string displayName, float currentValue, int level, int nextCost, StatType statType, Action<StatType> onUpgradeClicked)
        {
            _statType = statType;
            _onUpgradeClicked = onUpgradeClicked;

            if (statNameText != null)
                statNameText.text = displayName;

            if (valueAndLevelText != null)
                valueAndLevelText.text = $"{currentValue:F1} ({level})";

            if (buttonText != null)
                buttonText.text = $"[{nextCost}g] +";

            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(() => _onUpgradeClicked?.Invoke(_statType));
        }

        public void SetButtonInteractable(bool canAfford)
        {
            if (upgradeButton != null)
                upgradeButton.interactable = canAfford;
        }
    }
}