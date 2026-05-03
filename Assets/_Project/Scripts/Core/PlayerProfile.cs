using System.Collections.Generic;
using UnityEngine;

namespace GridEmpire.Core
{
    [System.Serializable]
    public class PlayerProfile
    {
        // Serialized backing fields (Inspector számára)
        [SerializeField] private int id;
        [SerializeField] private string name;
        [SerializeField] private Color color;
        [SerializeField] private bool isAI;
        [SerializeField] private bool isLocalPlayer;
        [SerializeField] private bool isAlive = true;

        [SerializeField] private int ownedCellCount;
        [SerializeField] private float gold;
        [SerializeField] private float goldIncome;

        [SerializeField] private CellData baseCell;
        [SerializeField] private CellData selectedCell;

        // Runtime-only lista IUnit interfészen keresztül (nem serializált)
        private readonly List<IUnit> _activeUnits = new List<IUnit>();
        public IReadOnlyList<IUnit> ActiveUnits => _activeUnits;

        // Publikus propertyk — API változatlan, csak a backing field-ekre hivatkoznak
        public int Id => id;
        public string Name => name;
        public Color Color => color;
        public bool IsAI => isAI;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsAlive => isAlive;

        public int OwnedCellCount => ownedCellCount;
        public float Gold => gold;
        public float GoldIncome => goldIncome;
        public void SetGold(float amount) => gold = amount;
        public CellData BaseCell { get => baseCell; set => baseCell = value; }
        public CellData SelectedCell { get => selectedCell; set => selectedCell = value; }

        // Konstruktor — beállítja a serialized backing fieldeket is
        public PlayerProfile(int id, string name, Color color, bool isAi, bool isLocal, CellData selectedCell)
        {
            this.id = id;
            this.name = name;
            this.color = color;
            this.isAI = isAi;
            this.isLocalPlayer = isLocal;
            this.gold = 10000f;
            this.selectedCell = selectedCell;
        }

        // API: runtime kezelés (megtartva a te logikádat)
        public void AddUnit(IUnit unit)
        {
            if (unit == null) return;
            if (!_activeUnits.Contains(unit)) _activeUnits.Add(unit);
            RecalculateIncome();
        }

        public void RemoveUnit(IUnit unit)
        {
            if (unit == null) return;
            if (_activeUnits.Remove(unit)) RecalculateIncome();
        }

        public void ChangeOwnedCells(int delta)
        {
            ownedCellCount = Mathf.Max(0, ownedCellCount + delta);
            RecalculateIncome();
        }

        public void RecalculateIncome()
        {
            float unitMaintenance = 0f;
            foreach (var u in _activeUnits)
            {
                if (u?.Data != null) unitMaintenance += u.Data.costPerTurn;
            }
            goldIncome = Mathf.Max(0f, 1f + (ownedCellCount * 0.1f) - unitMaintenance);
        }

        public void AddGold(float amount) => gold += amount;

        public bool SpendGold(float amount)
        {
            if (gold < amount) return false;
            gold -= amount;
            return true;
        }
    }
}
