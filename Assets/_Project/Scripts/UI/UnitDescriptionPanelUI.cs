using GridEmpire.Core;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GridEmpire.UI
{
    public class UnitDescriptionPanelUI : MonoBehaviour
    {
        [Header("Static Info Header")]
        [SerializeField] private TextMeshProUGUI unitNameText;
        [SerializeField] private TextMeshProUGUI staticStatsText; // Cost, Train Time, Strong Against, stb.
        public static System.Action<int, StatType> OnUpgradeRequested;

        [Header("Upgrade System")]
        [SerializeField] private Transform rowsContainer; // Az UpgradeRowsContainer RectTransform-ja
        [SerializeField] private GameObject statRowPrefab;  // A StatUpgradeRowUI Prefabja

        private List<StatUpgradeRowUI> _spawnedRows = new List<StatUpgradeRowUI>();

        public void RefreshPanel(UnitData baseData, Dictionary<StatType, StatUpgradeState> unitUpgrades, int currentPlayerGold)
        {
            if (unitNameText != null) unitNameText.text = baseData.unitName;

            if (staticStatsText != null)
            {
                staticStatsText.text = $"Cost: {baseData.cost} Gold | Rec. Time: {baseData.recruitmentTime} turn(s)\n" +
                                      $"Upkeep: {baseData.costPerTurn} Gold/turn | Counter: {baseData.strongAgainst}";
            }

            var statList = GetStatDisplayData(baseData, unitUpgrades);
            EnsureRowPoolSize(statList.Count);

            for (int i = 0; i < statList.Count; i++)
            {
                var item = statList[i];
                var rowUI = _spawnedRows[i];
                rowUI.gameObject.SetActive(true);

                bool atCap = item.state.IsMaxed;

                rowUI.Setup(
                    item.displayName,
                    item.currentValue,
                    item.state.level,
                    item.state.GetCurrentCost(),
                    item.type,
                    (type) => OnUpgradeButtonClicked(baseData.index, type),
                    atCap
                );

                rowUI.SetButtonInteractable(!atCap && currentPlayerGold >= item.state.GetCurrentCost());
            }
        }

        private void EnsureRowPoolSize(int requiredCount)
        {
            while (_spawnedRows.Count < requiredCount)
            {
                var newRow = Instantiate(statRowPrefab, rowsContainer).GetComponent<StatUpgradeRowUI>();
                _spawnedRows.Add(newRow);
            }

            for (int i = requiredCount; i < _spawnedRows.Count; i++)
            {
                _spawnedRows[i].gameObject.SetActive(false);
            }
        }

        private void OnUpgradeButtonClicked(int unitIndex, StatType statType)
        {
            OnUpgradeRequested?.Invoke(unitIndex, statType);
        }

        private struct StatDisplayItem
        {
            public string displayName;
            public float currentValue;
            public StatType type;
            public StatUpgradeState state;
        }

        private static readonly Dictionary<StatType, string> DisplayNames = new()
        {
            { StatType.MaxHp, "Max HP" },
            { StatType.StaminaPerTurn, "Stamina / Turn" },
            { StatType.MaxStamina, "Max Stamina" },
            { StatType.ConquerSpeed, "Conquer Speed" },
            { StatType.ExploreSpeed, "Explore Speed" },
            { StatType.BaseDamage, "Base Damage" },
            { StatType.BonusDamage, "Bonus Damage" }
        };

        private List<StatDisplayItem> GetStatDisplayData(UnitData baseData, Dictionary<StatType, StatUpgradeState> upgrades)
        {
            var baseValues = new Dictionary<StatType, float>
    {
        { StatType.MaxHp, baseData.maxHp },
        { StatType.StaminaPerTurn, baseData.staminaPerTurn },
        { StatType.MaxStamina, baseData.maxStamina },
        { StatType.ConquerSpeed, baseData.conquerSpeed },
        { StatType.ExploreSpeed, baseData.exploreSpeed },
        { StatType.BaseDamage, baseData.baseDamage },
        { StatType.BonusDamage, baseData.bonusDamage }
    };

            var result = new List<StatDisplayItem>();
            foreach (StatType type in System.Enum.GetValues(typeof(StatType)))
            {
                if (!upgrades.TryGetValue(type, out var state)) continue;
                result.Add(new StatDisplayItem
                {
                    displayName = DisplayNames[type],
                    currentValue = state.GetUpgradedValue(baseValues[type]),
                    type = type,
                    state = state
                });
            }
            return result;
        }
    }
}