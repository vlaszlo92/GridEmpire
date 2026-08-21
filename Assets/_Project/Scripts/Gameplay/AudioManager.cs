using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace GridEmpire.Gameplay
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer Reference")]
        [SerializeField] private AudioMixer _audioMixer;

        [Header("Lobby Music")]
        [SerializeField] private AudioClip _lobbyTrack;
        [SerializeField] private string _lobbySceneName = "MainMenuScene";

        [Header("Music Settings")]
        [SerializeField] private AudioClip[] _musicTracks;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private string _gameSceneName = "GameScene";

        [Header("Button Click Sound")]
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioClip buttonClickSound;

        private int[] _shuffledIndices;
        private int _currentIndex = 0;
        private Coroutine _playCoroutine;

        public const string MasterKey = "MasterVolume";
        public const string MusicKey = "MusicVolume";
        public const string EffectsKey = "EffectsVolume";
        public const string UIKey = "UIVolume";
        public const string MuteKey = "IsMuted";

        private bool _isMuted = false;
        public bool IsMuted => _isMuted;

        public static event System.Action<bool> OnMuteStateChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();

            if (_audioMixer != null)
            {
                bool masterSuccess = _audioMixer.SetFloat(MasterKey, 0f);
                bool musicSuccess = _audioMixer.SetFloat(MusicKey, 0f);
            }

            LoadVolumeSettings();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            PlayForScene(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayForScene(scene.name);
        }

        private void PlayForScene(string sceneName)
        {
            if (sceneName == _gameSceneName)
            {
                if (_musicTracks.Length > 0) StartQueue();
            }
            else if (sceneName == _lobbySceneName)
            {
                StartLobbyTrack();
            }
        }

        private void StartQueue()
        {
            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
            _musicSource.loop = false;
            Shuffle();
            _currentIndex = 0;
            _playCoroutine = StartCoroutine(PlayMusicQueue());
        }

        private void StartLobbyTrack()
        {
            if (_lobbyTrack == null) return;
            if (_playCoroutine != null) { StopCoroutine(_playCoroutine); _playCoroutine = null; }
            _musicSource.loop = true;
            _musicSource.clip = _lobbyTrack;
            _musicSource.Play();
        }

        #region Volume Control & Save/Load

        public void SetMasterVolume(float value) => SetVolume(MasterKey, value);
        public void SetMusicVolume(float value) => SetVolume(MusicKey, value);
        public void SetEffectsVolume(float value) => SetVolume(EffectsKey, value);
        public void SetUIVolume(float value) => SetVolume(UIKey, value);

        public void ToggleMute() => SetMuted(!_isMuted);

        public void SetMuted(bool muted)
        {
            _isMuted = muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();

            // A ténylegesen elmentett master volume-ot nem írjuk felül,
            // csak a mixert némítjuk le / állítjuk vissza rá.
            ApplyVolumeToMixer(MasterKey, muted ? 0f : GetVolume(MasterKey));
            OnMuteStateChanged?.Invoke(_isMuted);
        }

        private void SetVolume(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();

            // Ha épp némítva vagyunk, a master csúszka mozgatása ne törje meg
            // a mute-ot, csak a háttérben tárolt értéket frissítse.
            if (key == MasterKey && _isMuted) return;

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
            ApplyVolumeToMixer(UIKey, GetVolume(UIKey));

            _isMuted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            if (_isMuted) ApplyVolumeToMixer(MasterKey, 0f);
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
        public void PlayButtonClick()
        {
            if (buttonClickSound != null && _sfxSource != null)
            {
                _sfxSource.PlayOneShot(buttonClickSound);
            }
        }
    }
}