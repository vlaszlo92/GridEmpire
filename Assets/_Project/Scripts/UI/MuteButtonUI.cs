using UnityEngine;
using UnityEngine.UI;
using GridEmpire.Gameplay;

namespace GridEmpire.UI
{
    public class MuteButtonUI : MonoBehaviour
    {
        [SerializeField] private Button muteButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private Sprite mutedIcon;
        [SerializeField] private Sprite unmutedIcon;

        private void OnEnable()
        {
            Debug.Log("MuteButtonUI OnEnable");
            AudioManager.OnMuteStateChanged += RefreshIcon;
            if (muteButton != null) muteButton.onClick.AddListener(HandleClick);

            if (AudioManager.Instance != null) RefreshIcon(AudioManager.Instance.IsMuted);
        }

        private void OnDisable()
        {
            AudioManager.OnMuteStateChanged -= RefreshIcon;
            if (muteButton != null) muteButton.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick() => AudioManager.Instance?.ToggleMute();

        private void RefreshIcon(bool isMuted)
        {
            if (iconImage != null)
                iconImage.sprite = isMuted ? mutedIcon : unmutedIcon;
        }
    }
}