using UnityEngine;

namespace GridEmpire.UI
{
    public class MainActionZoneController : MonoBehaviour
    {
        [Header("Sub-panels")]
        [SerializeField] private GameObject mainButtonsGroup;
        [SerializeField] private GameObject settingsGroup;

        [Header("Trigger")]
        [SerializeField] private UnityEngine.UI.Button settingsBtn;

        private void Start()
        {
            if (settingsBtn != null) settingsBtn.onClick.AddListener(ToggleSettings);
            ShowMainButtons();
        }

        public void ToggleSettings()
        {
            if (mainButtonsGroup != null) mainButtonsGroup.SetActive(!mainButtonsGroup.activeSelf);
            if (settingsGroup != null) settingsGroup.SetActive(!settingsGroup.activeSelf);
            Debug.Log("Toggled settings panel. Main buttons active: " + mainButtonsGroup.activeSelf + ", Settings active: " + settingsGroup.activeSelf);
        }

        public void ShowMainButtons()
        {
            if (settingsGroup != null) settingsGroup.SetActive(false);
            if (mainButtonsGroup != null) mainButtonsGroup.SetActive(true);
        }
    }
}