#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class AssignUnitMaterial
{
    [MenuItem("Tools/Assign Unit Material to All Units")]
    public static void Assign()
    {
        string materialPath = "Assets/_Project/Materials/UnitMat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            Debug.LogError($"[AssignUnitMaterial] Material nem található: {materialPath}");
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Units" });
        int count = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                count++;
            }

            PrefabUtility.SavePrefabAsset(prefab);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AssignUnitMaterial] {count} renderer frissítve.");
    }
}
#endif