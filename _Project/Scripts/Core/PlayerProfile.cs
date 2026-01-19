using UnityEngine;
using System.Collections.Generic;

namespace GridEmpire.Core
{
    [System.Serializable]
    public class PlayerProfile
    {
        public int Id;
        public string Name;
        public Color Color;
        public float Gold;
        public bool IsAI;
        public bool IsLocalPlayer;
        public bool IsAlive = true;
        public int OwnedCellCount;
        public float GoldIncome;

        public CellData BaseCell;
        public CellData SelectedCell;

        // Itt most már az interfészt tároljuk
        public List<IUnit> ActiveUnits = new List<IUnit>();

        public PlayerProfile(int id, string name, Color color, bool isAi, bool isLocal, CellData selectedCell)
        {
            Id = id;
            Name = name;
            Color = color;
            IsAI = isAi;
            IsLocalPlayer = isLocal;
            Gold = 1;
            SelectedCell = selectedCell;
        }
    }
}