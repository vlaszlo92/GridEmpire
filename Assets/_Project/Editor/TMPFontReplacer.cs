using UnityEngine;
using UnityEditor;
using TMPro;

public class TMPFontReplacer : EditorWindow
{
    private TMP_FontAsset newFontAsset;

    [MenuItem("Tools/Replace All TMP Fonts")]
    public static void ShowWindow()
    {
        GetWindow<TMPFontReplacer>("TMP Font Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace All TMP Fonts", EditorStyles.boldLabel);

        newFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("New Font Asset", newFontAsset, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Replace in Current Scene"))
        {
            ReplaceInScene();
        }

        if (GUILayout.Button("Replace in All Prefabs (Project Folder)"))
        {
            ReplaceInPrefabs();
        }
    }

    private void ReplaceInScene()
    {
        if (!ValidateFont()) return;

        TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (TMP_Text text in allTexts)
        {
            Undo.RecordObject(text, "Replace TMP Font");
            text.font = newFontAsset;
            EditorUtility.SetDirty(text);
            count++;
        }

        Debug.Log($"Replaced font on {count} TMP Text components in Scene.");
    }

    private void ReplaceInPrefabs()
    {
        if (!ValidateFont()) return;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int count = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length == 0) continue;

                bool modified = false;
                foreach (TMP_Text text in texts)
                {
                    if (text.font != newFontAsset)
                    {
                        text.font = newFontAsset;
                        EditorUtility.SetDirty(text);
                        modified = true;
                        count++;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Replaced font on {count} TMP Text components across Prefabs.");
    }

    private bool ValidateFont()
    {
        if (newFontAsset == null)
        {
            Debug.LogError("Assign a new TMP Font Asset first!");
            return false;
        }
        return true;
    }
}