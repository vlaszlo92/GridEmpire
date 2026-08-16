using System;
using UnityEngine;

namespace GridEmpire.Core
{
    [Serializable]
    public enum StatType
    {
        MaxHp,
        StaminaPerTurn,
        MaxStamina,
        ConquerSpeed,
        ExploreSpeed,
        BaseDamage,
        BonusDamage
    }

    [Serializable]
    public class StatUpgradeSettings
    {
        public StatType statType;
        public bool enabled = true;
        public int maxLevel = 5;
        public int baseUpgradeCost = 100;
        public float costMultiplier = 1.5f;
        public float valuePerLevel = 10f;

        public StatUpgradeState ToState(int level)
        {
            return new StatUpgradeState
            {
                statType = statType,
                level = level,
                maxLevel = maxLevel,
                baseUpgradeCost = baseUpgradeCost,
                costMultiplier = costMultiplier,
                valuePerLevel = valuePerLevel
            };
        }
    }

    [Serializable]
    public class StatUpgradeState
    {
        public StatType statType;
        public int level = 0;
        public int maxLevel = 5;
        public int baseUpgradeCost;
        public float costMultiplier;
        public float valuePerLevel;

        public bool IsMaxed => level >= maxLevel;

        public int GetCurrentCost()
        {
            return Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(costMultiplier, level));
        }

        public float GetUpgradedValue(float baseValue)
        {
            return baseValue + (level * valuePerLevel);
        }
    }
}