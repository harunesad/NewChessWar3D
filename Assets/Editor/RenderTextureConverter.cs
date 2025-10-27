using UnityEngine;
using UnityEditor;
using System.IO;

public class RenderTextureConverter
{
    // Assets menüsünde, seçili bir RenderTexture varlýðý üzerinde sað týklandýðýnda görünür.
    [MenuItem("Assets/RenderTexture'ü PNG Olarak Kaydet", true)]
    private static bool ValidateSaveRenderTextureToPNG()
    {
        // Seçimin bir RenderTexture olup olmadýðýný kontrol eder.
        return Selection.activeObject is RenderTexture;
    }

    // Gerçek dönüþtürme ve kaydetme iþlemini yapan menü öðesi.
    [MenuItem("Assets/RenderTexture'ü PNG Olarak Kaydet")]
    private static void SaveRenderTextureToPNG()
    {
        RenderTexture rt = Selection.activeObject as RenderTexture;

        if (rt == null)
        {
            Debug.LogError("Seçili nesne bir RenderTexture deðil.");
            return;
        }

        // 1. Yeni bir Texture2D oluþturun
        // RT'nin formatýna baðlý olarak, TextureFormat'ý ayarlamanýz gerekebilir.
        // Genellikle RGB24 veya RGBA32 güvenli seçeneklerdir.
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);

        // 2. Aktif RenderTexture'ý ayarlayýn (Bu, ReadPixels'ýn nereden okuyacaðýný belirtir)
        RenderTexture previous = RenderTexture.active; // Mevcut aktif RT'yi kaydet
        RenderTexture.active = rt;

        try
        {
            // 3. RenderTexture'daki pikselleri Texture2D'ye okuyun
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            // 4. Texture2D'yi PNG formatýna kodlayýn
            byte[] bytes = ImageConversion.EncodeToPNG(tex);

            // 5. Kaydedilecek dosya yolunu belirleyin
            string assetPath = AssetDatabase.GetAssetPath(rt);
            string folderPath = Path.GetDirectoryName(assetPath);
            string fileName = Path.GetFileNameWithoutExtension(assetPath) + ".png";
            string fullPath = Path.Combine(folderPath, fileName);

            // Eðer isterseniz, proje klasörünüzün dýþýnda bir yere de kaydedebilirsiniz:
            // string fullPath = EditorUtility.SaveFilePanel("PNG olarak kaydet", "", rt.name + ".png", "png");

            if (!string.IsNullOrEmpty(fullPath))
            {
                // 6. Dosyayý diske yazýn
                File.WriteAllBytes(fullPath, bytes);

                // Unity'ye yeni dosyayý görmesi için bilgi verin
                AssetDatabase.ImportAsset(fullPath);

                Debug.Log($"RenderTexture baþarýyla PNG olarak kaydedildi: {fullPath}");
            }
        }
        finally
        {
            // 7. Aktif RenderTexture'ý eski haline getirin ve geçici Texture2D'yi yok edin
            RenderTexture.active = previous;
            Object.DestroyImmediate(tex); // Editörde bellek sýzýntýsýný önlemek için DestroyImmediate kullanýn
        }
    }
}