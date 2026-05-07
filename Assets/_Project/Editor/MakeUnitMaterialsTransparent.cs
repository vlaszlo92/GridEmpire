#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class MakeUnitMaterialsTransparent
{
    [MenuItem("Tools/Make Unit Materials Transparent")]
    public static void Convert()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (!mat.shader.name.Contains("Universal Render Pipeline")) continue;

            mat.SetFloat("_Surface", 1f);         // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);            // Alpha blend mód
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster", false);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MaterialConverter] {count} anyag átállítva transparent módra.");
    }
}
#endif