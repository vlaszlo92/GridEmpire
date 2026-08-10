using GridEmpire.Core;
using GridEmpire.Data;
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

        [Header("Upgrade System")]
        [SerializeField] private Transform rowsContainer; // Az UpgradeRowsContainer RectTransform-ja
        [SerializeField] private GameObject statRowPrefab;  // A StatUpgradeRowUI Prefabja

        private List<StatUpgradeRowUI> _spawnedRows = new List<StatUpgradeRowUI>();

        public void RefreshPanel(UnitData baseData, Dictionary<StatType, StatUpgradeState> unitUpgrades, int currentPlayerGold)
        {
            // 1. Statikus adatok kiirasa, amik nem szintezhetok
            if (unitNameText != null) unitNameText.text = baseData.unitName;

            if (staticStatsText != null)
            {
                staticStatsText.text = $"Cost: {baseData.cost} Gold | Rec. Time: {baseData.recruitmentTime} turn(s)\n" +
                                      $"Upkeep: {baseData.costPerTurn} Gold/turn | Counter: {baseData.strongAgainst}";
            }

            // 2. Szintezheto statok osszegyujtese egy listaba
            var statList = GetStatDisplayData(baseData, unitUpgrades);

            // 3. UI sorok ujrahasznositasa / generalasa
            EnsureRowPoolSize(statList.Count);

            for (int i = 0; i < statList.Count; i++)
            {
                var item = statList[i];
                var rowUI = _spawnedRows[i];
                rowUI.gameObject.SetActive(true);

                rowUI.Setup(
                    item.displayName,
                    item.currentValue,
                    item.state.level,
                    item.state.GetCurrentCost(),
                    item.type,
                    (type) => OnUpgradeButtonClicked(baseData.index, type)
                );

                // Gomb letiltasa, ha nincs eleg aranya a jatekosnak
                rowUI.SetButtonInteractable(currentPlayerGold >= item.state.GetCurrentCost());
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
            // Tovabbitjuk a kerest a GameManager-nek, ami levonja az aranyat es emeli a szintet
            // GameManager.Instance.UpgradeUnitStat(unitIndex, statType);
        }

        // Segedstruktura a megjelenites megkonnyitesere
        private struct StatDisplayItem
        {
            public string displayName;
            public float currentValue;
            public StatType type;
            public StatUpgradeState state;
        }

        private List<StatDisplayItem> GetStatDisplayData(UnitData baseData, Dictionary<StatType, StatUpgradeState> upgrades)
        {
            return new List<StatDisplayItem>
            {
                new StatDisplayItem { displayName = "Max HP", currentValue = upgrades[StatType.MaxHp].GetUpgradedValue(baseData.maxHp), type = StatType.MaxHp, state = upgrades[StatType.MaxHp] },
                new StatDisplayItem { displayName = "Stamina / Turn", currentValue = upgrades[StatType.StaminaPerTurn].GetUpgradedValue(baseData.staminaPerTurn), type = StatType.StaminaPerTurn, state = upgrades[StatType.StaminaPerTurn] },
                new StatDisplayItem { displayName = "Max Stamina", currentValue = upgrades[StatType.MaxStamina].GetUpgradedValue(baseData.maxStamina), type = StatType.MaxStamina, state = upgrades[StatType.MaxStamina] },
                new StatDisplayItem { displayName = "Conquer Speed", currentValue = upgrades[StatType.ConquerSpeed].GetUpgradedValue(baseData.conquerSpeed), type = StatType.ConquerSpeed, state = upgrades[StatType.ConquerSpeed] },
                new StatDisplayItem { displayName = "Explore Speed", currentValue = upgrades[StatType.ExploreSpeed].GetUpgradedValue(baseData.exploreSpeed), type = StatType.ExploreSpeed, state = upgrades[StatType.ExploreSpeed] },
                new StatDisplayItem { displayName = "Base Damage", currentValue = upgrades[StatType.BaseDamage].GetUpgradedValue(baseData.baseDamage), type = StatType.BaseDamage, state = upgrades[StatType.BaseDamage] },
                new StatDisplayItem { displayName = "Bonus Damage", currentValue = upgrades[StatType.BonusDamage].GetUpgradedValue(baseData.bonusDamage), type = StatType.BonusDamage, state = upgrades[StatType.BonusDamage] }
            };
        }
    }
}