using UnityEngine;
using TMPro;

namespace GridEmpire.UI
{
    public class StatRowConnector : MonoBehaviour
    {
        // Ez a lista pontosan leképezi a UnitStats változóit
        public enum StatType
        {
            Cost,
            CostPerTurn,
            MaxHp,
            StaminaPerTurn,
            MaxStamina,
            ConquerSpeed,
            ExploreSpeed,
            BaseDamage,
            BonusDamage
        }

        [Header("Beállítások")]
        public int unitIndex; // 0: Fejszés, 1: Lándzsás, 2: Lovas, 3: Felderítő
        public StatType type;

        [Header("UI Referenciák")]
        public TextMeshProUGUI labelText; // A sor neve (pl. "Cost")
        public TMP_InputField inputField; // Az érték beviteli mezője

        // Ezt a függvényt hívja majd meg a MainMenuController az induláskor
        public void Init(string name, float value)
        {
            if (labelText != null) labelText.text = name;
            if (inputField != null) inputField.text = value.ToString();
        }
    }
}