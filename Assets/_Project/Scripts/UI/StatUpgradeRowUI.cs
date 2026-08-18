using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using GridEmpire.Core;

namespace GridEmpire.UI
{
    public class StatUpgradeRowUI : MonoBehaviour
    {
        [SerializeField] private Image statIconImage;
        [SerializeField] private TextMeshProUGUI statNameText; // opcionális, tooltip/hover-hez megtartható
        [SerializeField] private TextMeshProUGUI valueAndLevelText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI buttonText;

        private StatType _statType;
        private Action<StatType> _onUpgradeClicked;

        public void Setup(Sprite icon, string displayName, float currentValue, int level, int nextCost,
            StatType statType, Action<StatType> onUpgradeClicked, bool isMaxed = false)
        {
            _statType = statType;
            _onUpgradeClicked = onUpgradeClicked;

            if (statIconImage != null) statIconImage.sprite = icon;
            if (statNameText != null) statNameText.text = displayName;

            if (valueAndLevelText != null)
                valueAndLevelText.text = $"{currentValue:F1} ({level})";

            if (buttonText != null)
                buttonText.text = isMaxed ? "MAX" : $"[{nextCost}g] +";

            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(() => _onUpgradeClicked?.Invoke(_statType));
        }

        public void SetButtonInteractable(bool canAfford)
        {
            if (upgradeButton != null) upgradeButton.interactable = canAfford;
        }
    }
}