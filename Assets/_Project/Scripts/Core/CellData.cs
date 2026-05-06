using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace GridEmpire.Core
{
    public enum VisibilityState { Hidden, Explored, Visible }

    [Serializable]
    public class InfluenceEntry
    {
        public int PlayerId;
        [Range(0, 1)] public float Influence;
    }

    [Serializable]
    public class CellData
    {
        [field: SerializeField] public int Q { get; }
        [field: SerializeField] public int R { get; }
        public int S => -Q - R;

        public int Id;
        public int OwnerId = -1;
        public bool IsBase = false;
        public bool IsOccupied;
        private VisibilityState _currentVisibility = VisibilityState.Hidden;

        public VisibilityState CurrentVisibility
        {
            get => _currentVisibility;
            set
            {
                if (_currentVisibility == value) return; // csak ha változott
                _currentVisibility = value;

                // Ha van rajta egység, értesítjük
                foreach (var obj in OccupyingUnits)
                {
                    var unit = obj as IUnit;
                    unit?.OnCellVisibilityChanged(value);
                }
            }
        }

        private Dictionary<int, float> _playerInfluences = new Dictionary<int, float>();

        public List<InfluenceEntry> InfluenceDisplay = new List<InfluenceEntry>();
        public List<UnityEngine.Object> OccupyingUnits = new List<UnityEngine.Object>();
        public List<int> CapturingUnitIds = new List<int>();

        public Action OnVisualUpdateRequired;
        public static System.Action<int, int> OnCellOwnerChanged;

        public CellData(int q, int r, int id)
        {
            Q = q;
            R = r;
            Id = id;
        }

        public void RegisterOccupier(object unit)
        {
            UnityEngine.Object unityObj = unit as UnityEngine.Object;
            if (unityObj != null && !OccupyingUnits.Contains(unityObj))
            {
                OccupyingUnits.Add(unityObj);
                IsOccupied = true;
            }
        }

        public void UnregisterOccupier(object unit)
        {
            UnityEngine.Object unityObj = unit as UnityEngine.Object;
            if (OccupyingUnits.Contains(unityObj))
            {
                OccupyingUnits.Remove(unityObj);
                if (OccupyingUnits.Count == 0) IsOccupied = false;
            }
        }

        public object GetFirstOccupier()
        {
            return OccupyingUnits.Count > 0 ? OccupyingUnits[0] : null;
        }

        public float GetCaptureProgress(int playerId)
        {
            if (_playerInfluences == null)
                _playerInfluences = new Dictionary<int, float>();
            return _playerInfluences.TryGetValue(playerId, out float p) ? p : 0f;
        }

        public void UpdateCapture(int playerId, float amount)
        {
            if (OwnerId == playerId) return;

            if (OwnerId != -1)
            {
                // Ellenséges terület hódítása (Conquer)
                ModifyInfluence(OwnerId, -amount);
                if (GetCaptureProgress(OwnerId) <= 0)
                {
                    int oldOwner = OwnerId;
                    OwnerId = -1;
                    // ESEMÉNY: Valaki elveszítette, mostantól senkié (-1)
                    OnCellOwnerChanged?.Invoke(oldOwner, -1);
                }
            }
            else
            {
                // Semleges terület felfedezése (Explore)
                ModifyInfluence(playerId, amount);
                if (GetCaptureProgress(playerId) >= 1.0f)
                {
                    OwnerId = playerId;
                    SetInfluence(playerId);
                    // ESEMÉNY: A semleges területnek lett új gazdája
                    OnCellOwnerChanged?.Invoke(-1, playerId);
                }
            }
            //Debug.Log($"[CellData] INVOKE !!! Cell:{Id} Player:{playerId} Capture updated. Owner:{OwnerId} Progress:{GetCaptureProgress(OwnerId):F2}");
            OnVisualUpdateRequired?.Invoke();
        }

        private void ModifyInfluence(int playerId, float delta)
        {
            if (_playerInfluences == null)
                _playerInfluences = new Dictionary<int, float>();
            if (!_playerInfluences.ContainsKey(playerId)) _playerInfluences[playerId] = 0f;
            _playerInfluences[playerId] = Math.Clamp(_playerInfluences[playerId] + delta, 0f, 1f);

            var entry = InfluenceDisplay.Find(e => e.PlayerId == playerId);
            if (entry == null)
            {
                entry = new InfluenceEntry { PlayerId = playerId };
                InfluenceDisplay.Add(entry);
            }
            entry.Influence = _playerInfluences[playerId];
            //Debug.Log($"[CellData] Cell:{Id} Player:{playerId} Influence updated to {entry.Influence:F2}");
        }            

        public void SetInfluence(int playerId, float value = 1.0f)
        {
            if (_playerInfluences == null)
                _playerInfluences = new Dictionary<int, float>();
            _playerInfluences.Clear();
            _playerInfluences[playerId] = value;
            InfluenceDisplay.Clear();
            InfluenceDisplay.Add(new InfluenceEntry { PlayerId = playerId, Influence = value });
        }
    }
}