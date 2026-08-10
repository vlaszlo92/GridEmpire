using System;
using UnityEngine;

namespace GridEmpire.Data
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
    public class StatUpgradeState
    {
        public StatType statType;
        public int level = 0;
        public int baseUpgradeCost = 100;
        public float costMultiplier = 1.5f; // Szintenkent ennyivel dragul
        public float valuePerLevel = 10f;   // Szintenkent ennyit ad hozza

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