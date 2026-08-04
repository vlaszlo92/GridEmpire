using GridEmpire.Core;
using System.Collections.Generic;
using UnityEngine;

namespace GridEmpire.Visuals
{
    public class FogOfWarManager : MonoBehaviour
    {
        public static FogOfWarManager Instance { get; private set; }
        [SerializeField] private GameObject fogPrefab;

        private Dictionary<CellVisual, GameObject> fogObjects = new();

        void Awake() => Instance = this;

        public void SetFog(CellVisual cell, bool hidden)
        {
            if (hidden) ShowFog(cell);
            else HideFog(cell);
        }

        private void ShowFog(CellVisual cell)
        {
            if (fogObjects.ContainsKey(cell)) return;
            var fog = Instantiate(fogPrefab, cell.transform.position, Quaternion.identity);
            fog.transform.SetParent(cell.transform);
            fogObjects[cell] = fog;
        }

        private void HideFog(CellVisual cell)
        {
            if (!fogObjects.TryGetValue(cell, out var fog)) return;
            Destroy(fog);
            fogObjects.Remove(cell);
        }
    }
}