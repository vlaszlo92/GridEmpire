#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using GridEmpire.Gameplay;

public class AttachClickSoundToButtons
{
    [MenuItem("Tools/Attach Click Sound To All Buttons")]
    public static void Attach()
    {
        AudioManager audioManager = Object.FindAnyObjectByType<AudioManager>();
        if (audioManager == null)
        {
            Debug.LogError("[AttachClickSound] Nem található AudioManager a Scene-ben!");
            return;
        }

        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int addedCount = 0;

        foreach (Button btn in buttons)
        {
            // Ellenőrizzük, hogy nincs-e már hozzáadva
            bool alreadyAdded = false;
            int eventCount = btn.onClick.GetPersistentEventCount();

            for (int i = 0; i < eventCount; i++)
            {
                if (btn.onClick.GetPersistentTarget(i) == (Object)audioManager &&
                    btn.onClick.GetPersistentMethodName(i) == nameof(AudioManager.PlayButtonClick))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                Undo.RecordObject(btn, "Add Click Sound");
                UnityEventTools.AddVoidPersistentListener(btn.onClick, audioManager.PlayButtonClick);
                EditorUtility.SetDirty(btn);
                addedCount++;
            }
        }

        Debug.Log($"[AttachClickSound] {addedCount} gomb OnClick eseményéhez hozzáadva az AudioManager.PlayButtonClick.");
    }
}
#endif