using GridEmpire.Core;
using GridEmpire.Shared;
using System.Linq;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class UnitAnimator : MonoBehaviour
    {
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponentsInChildren<Animator>().FirstOrDefault();
        }

        public void Play(ActionType action)
        {
            if (_animator.GetInteger("State") == 4) return;
            int state = action switch
            {
                ActionType.Move => 1,
                ActionType.Attack => 2,
                ActionType.Capture => 3,
                ActionType.Spawn => 0,
                ActionType.Idle => 0,
                _ => 0
            };

            _animator.SetInteger("State", state);
        }
        public void PlayDeath(System.Action onFadeComplete = null)
        {
            _animator.SetInteger("State", 4);

            float deathAnimLength = 0f;
            var clips = _animator.runtimeAnimatorController.animationClips;
            foreach (var clip in clips)
                if (clip.name.ToLower().Contains("death") || clip.name.ToLower().Contains("die"))
                { deathAnimLength = clip.length; break; }

            var fade = GetComponent<FadeAway>();
            if (fade != null) fade.Begin(deathAnimLength + 1f, onFadeComplete);
        }
    }
}