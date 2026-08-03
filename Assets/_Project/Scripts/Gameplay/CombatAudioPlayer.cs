using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class CombatAudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _swordClips;
        [SerializeField] private AudioClip[] _infantryFootstepClips;
        [SerializeField] private AudioClip[] _mountFootstepClips;
        [SerializeField] private AudioClip[] _deathClips;
        [SerializeField] private AudioClip[] _conqueringClips;

        [SerializeField] private bool _isMount = false;
        public bool IsMount => _isMount;

        private AudioSource _audioSource;
        private Animator[] _animators;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _animators = GetComponentsInChildren<Animator>();
        }
        public void PlaySwordHit()
        {
            PlayRandom(_swordClips);
        }

        public void PlayFootstep()
        {
            PlayRandom(_isMount ? _mountFootstepClips : _infantryFootstepClips);
        }

        public void PlayConquering()
        {
            PlayRandom(_conqueringClips);
        }

        public void PlayDeath()
        {
            PlayRandom(_deathClips);
        }

        private void PlayRandom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            _audioSource.PlayOneShot(clip);
        }
    }
}