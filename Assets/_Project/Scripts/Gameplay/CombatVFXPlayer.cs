using GridEmpire.Core;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class CombatVFXPlayer : MonoBehaviour
    {
        [SerializeField] private GameObject _hitEffectPrefab;
        [SerializeField] private GameObject _footstepDustPrefab;
        [SerializeField] private GameObject _deathEffectPrefab;
        [SerializeField] private GameObject _conquerHitEffectPrefab;

        [SerializeField] private Transform _weaponPoint;
        [SerializeField] private Transform[] _footPoints;   
        [SerializeField] private Transform _chestPoint;

        public void PlayHitEffect() => Spawn(_hitEffectPrefab, _weaponPoint);
        public void PlayFootstepDust(int footIndex)
        {
            if (_footPoints == null || footIndex < 0 || footIndex >= _footPoints.Length) return;
            Spawn(_footstepDustPrefab, _footPoints[footIndex]);
        }
        public void PlayDeathEffect() => Spawn(_deathEffectPrefab, _chestPoint);
        public void PlayConquerHitEffect() => Spawn(_conquerHitEffectPrefab, _chestPoint);

        private void Spawn(GameObject prefab, Transform point)
        {
            if (prefab == null || point == null) return;
            if (!IsVisibleToLocalPlayer(point.position)) return;

            GameObject vfx = Instantiate(prefab, point.position, point.rotation);
            Destroy(vfx, 2f);
        }
        private bool IsVisibleToLocalPlayer(Vector3 worldPos)
        {
            var gm = GridManager.Instance;
            if (gm == null) return true;
            var cell = gm.GetCellAtPosition(worldPos);
            return cell == null || cell.CurrentVisibility == VisibilityState.Visible;
        }
    }
}