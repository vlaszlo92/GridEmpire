using System;
using System.Collections.Generic;
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

        public int OwnerId = -1;
        public bool IsBase = false;
        public bool IsOccupied;
        public VisibilityState CurrentVisibility = VisibilityState.Hidden;

        // Játékos ID -> Befolyás
        private Dictionary<int, float> _playerInfluences = new Dictionary<int, float>();

        // Inspector megjelenítéshez
        public List<InfluenceEntry> InfluenceDisplay = new List<InfluenceEntry>();
        public List<UnityEngine.Object> OccupyingUnits = new List<UnityEngine.Object>();

        public Action OnVisualUpdateRequired;

        public CellData(int q, int r)
        {
            Q = q;
            R = r;
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
            return _playerInfluences.TryGetValue(playerId, out float p) ? p : 0f;
        }

        public void UpdateCapture(int playerId, float amount)
        {
            if (OwnerId == playerId) return;

            if (OwnerId != -1)
            {
                ModifyInfluence(OwnerId, -amount);
                if (GetCaptureProgress(OwnerId) <= 0) OwnerId = -1;
            }
            else
            {
                ModifyInfluence(playerId, amount);
                if (GetCaptureProgress(playerId) >= 1.0f)
                {
                    OwnerId = playerId;
                    ClearOtherInfluences(playerId);
                }
            }
            OnVisualUpdateRequired?.Invoke();
        }

        private void ModifyInfluence(int playerId, float delta)
        {
            if (!_playerInfluences.ContainsKey(playerId)) _playerInfluences[playerId] = 0f;
            _playerInfluences[playerId] = Math.Clamp(_playerInfluences[playerId] + delta, 0f, 1f);

            var entry = InfluenceDisplay.Find(e => e.PlayerId == playerId);
            if (entry == null)
            {
                entry = new InfluenceEntry { PlayerId = playerId };
                InfluenceDisplay.Add(entry);
            }
            entry.Influence = _playerInfluences[playerId];
        }

        private void ClearOtherInfluences(int activePlayerId)
        {
            _playerInfluences.Clear();
            _playerInfluences[activePlayerId] = 1.0f;
            InfluenceDisplay.Clear();
            InfluenceDisplay.Add(new InfluenceEntry { PlayerId = activePlayerId, Influence = 1.0f });
        }
    }
}