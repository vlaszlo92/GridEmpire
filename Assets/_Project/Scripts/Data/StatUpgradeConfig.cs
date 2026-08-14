using System.Collections.Generic;

namespace GridEmpire.Data
{
    public static class StatUpgradeConfig
    {
        private static readonly Dictionary<StatType, (int baseCost, float costMultiplier, float valuePerLevel)> Table = new()
        {
            { StatType.MaxHp,          (100, 1.5f, 20f) },
            { StatType.StaminaPerTurn, (100, 1.5f, 0.1f) },
            { StatType.MaxStamina,     (100, 1.5f, 0.25f) },
            { StatType.ConquerSpeed,   (100, 1.5f, 0.05f) },
            { StatType.ExploreSpeed,   (100, 1.5f, 0.05f) },
            { StatType.BaseDamage,     (100, 1.5f, 5f) },
            { StatType.BonusDamage,    (100, 1.5f, 5f) },
        };

        public static StatUpgradeState CreateState(StatType type, int level)
        {
            var (baseCost, multiplier, valuePerLevel) = Table[type];
            return new StatUpgradeState
            {
                statType = type,
                level = level,
                baseUpgradeCost = baseCost,
                costMultiplier = multiplier,
                valuePerLevel = valuePerLevel
            };
        }
    }
}