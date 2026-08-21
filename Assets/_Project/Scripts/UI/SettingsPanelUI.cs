using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GridEmpire.Gameplay;

namespace GridEmpire.UI
{
    public class SettingsPanelUI : MonoBehaviour
    {

        [Header("Left Column - Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider effectsSlider;
        [SerializeField] private Slider uiSlider;

        [Header("Right Column - Display")]
        [SerializeField] private Slider cursorScaleSlider;
        [SerializeField] private Slider hudScaleSlider;
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Zone Controller")]
        [SerializeField] private MainActionZoneController zoneController;

        private List<Resolution> _resolutions;

        private void Start()
        {            
            InitAudioSlider(masterSlider, AudioManager.MasterKey, v => AudioManager.Instance?.SetMasterVolume(v));
            InitAudioSlider(musicSlider, AudioManager.MusicKey, v => AudioManager.Instance?.SetMusicVolume(v));
            InitAudioSlider(effectsSlider, AudioManager.EffectsKey, v => AudioManager.Instance?.SetEffectsVolume(v));
            InitAudioSlider(uiSlider, AudioManager.UIKey, v => AudioManager.Instance?.SetUIVolume(v));

            InitCursorSlider();
            InitHudSlider();
            InitResolutionDropdown();
        }

        private void InitAudioSlider(Slider slider, string key, System.Action<float> onValueChanged)
        {
            if (slider == null || AudioManager.Instance == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(AudioManager.Instance.GetVolume(key));
            slider.onValueChanged.AddListener(v => onValueChanged(v));
        }

        private void InitCursorSlider()
        {
            if (cursorScaleSlider == null || CursorManager.Instance == null) return;
            cursorScaleSlider.minValue = 0.5f;
            cursorScaleSlider.maxValue = 2.5f;
            cursorScaleSlider.SetValueWithoutNotify(CursorManager.Instance.GetCursorScale());

            var trigger = cursorScaleSlider.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                          ?? cursorScaleSlider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var entry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp
            };
            entry.callback.AddListener(_ => CursorManager.Instance?.SetCursorScale(cursorScaleSlider.value));
            trigger.triggers.Add(entry);
        }

        private void InitHudSlider()
        {
            if (hudScaleSlider == null) return;
            hudScaleSlider.minValue = 0.75f;
            hudScaleSlider.maxValue = 1.5f;
            hudScaleSlider.SetValueWithoutNotify(HUDScaleController.GetScale());
            hudScaleSlider.onValueChanged.AddListener(v => HUDScaleController.SetScale(v));
        }

        private void InitResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            _resolutions = ResolutionManager.GetAvailableResolutions();
            resolutionDropdown.ClearOptions();

            var options = new List<string>();
            foreach (var r in _resolutions)
                options.Add($"{r.width} x {r.height}");

            resolutionDropdown.AddOptions(options);

            int savedIndex = ResolutionManager.GetSavedIndex(_resolutions);
            resolutionDropdown.SetValueWithoutNotify(savedIndex);

            resolutionDropdown.onValueChanged.AddListener(idx =>
                ResolutionManager.ApplyResolution(_resolutions, idx));
        }
    }
}