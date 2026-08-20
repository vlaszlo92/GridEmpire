#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class ExportSelectedSpritesToPNG
{
    private const int TARGET_SIZE = 1024; // Állítsd 32-re vagy 64-re a kívánt méret szerint

    [MenuItem("Tools/Export Selected Sprites to PNG")]
    public static void Export()
    {
        Object[] selectedObjects = Selection.objects;
        int count = 0;

        foreach (Object obj in selectedObjects)
        {
            if (obj is not Sprite sprite) continue;

            Texture2D sourceTex = sprite.texture;

            if (!sourceTex.isReadable)
            {
                Debug.LogError($"[SpriteExporter] A(z) '{sourceTex.name}' textúránál nincs bepipálva a Read/Write Enabled opció!");
                continue;
            }

            Rect cropRect = sprite.textureRect;

            // 1. Kivágjuk az eredeti méretű pixeleket
            Texture2D croppedTex = new Texture2D((int)cropRect.width, (int)cropRect.height);
            Color[] pixels = sourceTex.GetPixels(
                (int)cropRect.x,
                (int)cropRect.y,
                (int)cropRect.width,
                (int)cropRect.height
            );
            croppedTex.SetPixels(pixels);
            croppedTex.Apply();

            // 2. Leméretezzük a kívánt TARGET_SIZE méretre (RenderTexture segítségével a jó minőségért)
            RenderTexture rt = RenderTexture.GetTemporary(TARGET_SIZE, TARGET_SIZE);
            Graphics.Blit(croppedTex, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D resizedTex = new Texture2D(TARGET_SIZE, TARGET_SIZE, TextureFormat.RGBA32, false);
            resizedTex.ReadPixels(new Rect(0, 0, TARGET_SIZE, TARGET_SIZE), 0, 0);
            resizedTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            // 3. Kimentés PNG-be az eredeti fájl mellé
            byte[] bytes = resizedTex.EncodeToPNG();
            string dirPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sprite));
            string filePath = Path.Combine(dirPath, $"{sprite.name}.png");

            File.WriteAllBytes(filePath, bytes);

            // Beállítjuk a kimentett PNG-t Cursor típusra
            AssetDatabase.ImportAsset(filePath);
            TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Cursor;
                importer.SaveAndReimport();
            }

            count++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[SpriteExporter] {count} sprite kimentve és leméretezve {TARGET_SIZE}x{TARGET_SIZE} méretre.");
    }
}
#endif