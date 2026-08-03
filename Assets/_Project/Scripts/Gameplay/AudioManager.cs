using System.Collections;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioClip[] _musicTracks;
        [SerializeField] private AudioSource _musicSource;

        private int _currentTrack = 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_musicTracks.Length > 0)
                StartCoroutine(PlayMusicQueue());
        }

        private IEnumerator PlayMusicQueue()
        {
            while (true)
            {
                _musicSource.clip = _musicTracks[_currentTrack];
                _musicSource.Play();
                yield return new WaitUntil(() => !_musicSource.isPlaying);
                _currentTrack = (_currentTrack + 1) % _musicTracks.Length;
            }
        }
    }
}