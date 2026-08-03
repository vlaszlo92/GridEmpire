using GridEmpire.Core;
using GridEmpire.Shared;
using System.Collections;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class UnitAnimator : MonoBehaviour
    {
        private Animator[] _animators;
        private const float _baseSpeed = 1f;
        private CombatAudioPlayer _infantryAudio;

        private void Awake()
        {
            _animators = GetComponentsInChildren<Animator>();
            var allAudioPlayers = GetComponentsInChildren<CombatAudioPlayer>();
            foreach (var player in allAudioPlayers)
                if (!player.IsMount) { _infantryAudio = player; break; }
        }

        public void Play(ActionType action)
        {
            for (int i = 0; i < _animators.Length; i++)
                if (_animators[i].GetInteger("State") == 4) return;

            int state = action switch
            {
                ActionType.Move => 1,
                ActionType.Attack => 2,
                ActionType.Capture => 3,
                ActionType.Spawn => 0,
                ActionType.Idle => 0,
                _ => 0
            };

            float delay = Random.Range(0f, 0.5f);
            float speed = _baseSpeed + Random.Range(-0.25f, 0.25f);
            StartCoroutine(PlayDelayed(state, speed, delay));
        }

        private IEnumerator PlayDelayed(int state, float speed, float delay)
        {
            yield return new WaitForSeconds(delay);

            for (int i = 0; i < _animators.Length; i++)
            {
                _animators[i].SetInteger("State", state);
                _animators[i].speed = speed;
            }
        }

        public void PlayDeath(System.Action onFadeComplete = null)
        {
            for (int i = 0; i < _animators.Length; i++)
                _animators[i].SetInteger("State", 4);

            float deathAnimLength = 0f;
            var clips = _animators[0].runtimeAnimatorController.animationClips;
            foreach (var clip in clips)
                if (clip.name.ToLower().Contains("death") || clip.name.ToLower().Contains("die"))
                { deathAnimLength = clip.length; break; }

            var fade = GetComponent<FadeAway>();
            if (fade != null) fade.Begin(deathAnimLength + 1f, onFadeComplete);
        }
    }
}