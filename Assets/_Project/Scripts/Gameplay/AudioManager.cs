using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace GridEmpire.Gameplay
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer Reference")]
        [SerializeField] private AudioMixer _audioMixer;

        [Header("Music Settings")]
        [SerializeField] private AudioClip[] _musicTracks;
        [SerializeField] private AudioSource _musicSource;

        private int[] _shuffledIndices;
        private int _currentIndex = 0;

        public const string MasterKey = "MasterVolume";
        public const string MusicKey = "MusicVolume";
        public const string EffectsKey = "EffectsVolume";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            LoadVolumeSettings();
            if (_musicTracks.Length > 0)
            {
                Shuffle();
                StartCoroutine(PlayMusicQueue());
            }
        }

        #region Volume Control & Save/Load

        public void SetMasterVolume(float value) => SetVolume(MasterKey, value);
        public void SetMusicVolume(float value) => SetVolume(MusicKey, value);
        public void SetEffectsVolume(float value) => SetVolume(EffectsKey, value);

        private void SetVolume(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();

            ApplyVolumeToMixer(key, value);
        }

        public float GetVolume(string key, float defaultValue = 0.75f)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        private void LoadVolumeSettings()
        {
            ApplyVolumeToMixer(MasterKey, GetVolume(MasterKey));
            ApplyVolumeToMixer(MusicKey, GetVolume(MusicKey));
            ApplyVolumeToMixer(EffectsKey, GetVolume(EffectsKey));
        }

        private void ApplyVolumeToMixer(string key, float value)
        {
            if (_audioMixer == null) return;
            float dB = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
            _audioMixer.SetFloat(key, dB);
        }

        #endregion

        private void Shuffle()
        {
            _shuffledIndices = new int[_musicTracks.Length];
            for (int i = 0; i < _shuffledIndices.Length; i++)
                _shuffledIndices[i] = i;

            for (int i = _shuffledIndices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
            }
        }

        private IEnumerator PlayMusicQueue()
        {
            while (true)
            {
                _musicSource.clip = _musicTracks[_shuffledIndices[_currentIndex]];
                _musicSource.Play();
                yield return new WaitUntil(() => !_musicSource.isPlaying);
                _currentIndex++;
                if (_currentIndex >= _shuffledIndices.Length)
                {
                    _currentIndex = 0;
                    Shuffle();
                }
            }
        }
    }
}