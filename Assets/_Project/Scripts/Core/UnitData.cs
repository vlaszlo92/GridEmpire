using UnityEngine;

namespace GridEmpire.Core
{
    // Ez a sor teszi lehetove, hogy az Asset mappaban jobb klikkel letrehozz ilyen fajlt
    [CreateAssetMenu(fileName = "NewUnitData", menuName = "GridEmpire/Unit Data")]
    public class UnitData : ScriptableObject
    {
        [Header("Basic Info")]
        public int index;
        public string unitName;
        public UnitType type;
        public UnitType strongAgainst;
        public int recruitmentTime = 2; // Hany Tick (kor) alatt keszul el

        [Header("Stats")]
        public int cost;
        public int costPerTurn;
        public int maxHp;
        public float staminaPerTurn;
        public float maxStamina;
        public float conquerSpeed;
        public float exploreSpeed;

        [Header("Combat")]
        public int baseDamage = 40;
        public int bonusDamage = 10;

        [Header("Visuals")]
        public GameObject unitPrefab; // Maga a 3D modell, amit majd le akarunk rakni
        public Sprite icon;
    }

    public enum UnitType { Axeman, Spearman, Cavalry, Scout }
}