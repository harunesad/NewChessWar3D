using UnityEngine;
using System.Collections.Generic;
using ChessEngine;
using ChessEngine.Game;

public class MiniMapMarker : MonoBehaviour
{
    private VisualChessPiece piece;
    private bool isInitialized = false;

    private Vector3 originalScale;
    private bool isSwapped = false;

    private List<MaterialChange> matChangesList = new List<MaterialChange>();

    private struct ActiveMaterialPair
    {
        public Renderer renderer;
        public Material originalMaterial;
    }
    private List<ActiveMaterialPair> activeMaterials = new List<ActiveMaterialPair>();

    private static Material minimapWhiteMat;
    private static Material minimapBlackMat;

    public void Initialize(VisualChessPiece pPiece)
    {
        piece = pPiece;
        originalScale = piece.transform.localScale;

        // Taşın altındaki tüm MaterialChange script'li daire objelerini bul
        MaterialChange[] matChanges = piece.GetComponentsInChildren<MaterialChange>(true);
        if (matChanges != null)
        {
            matChangesList.AddRange(matChanges);
        }

        // Mini harita için unlit materyalleri bir kez oluştur
        if (minimapWhiteMat == null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            minimapWhiteMat = new Material(unlitShader);
            minimapWhiteMat.color = new Color(0f, 0.7f, 1f); // Parlak Açık Mavi (Cyan)

            minimapBlackMat = new Material(unlitShader);
            minimapBlackMat.color = new Color(0.95f, 0.1f, 0.1f); // Parlak Kırmızı
        }

        isInitialized = true;
    }

    void OnEnable()
    {
        Camera.onPreCull += OnCameraPreCull;
        Camera.onPostRender += OnCameraPostRender;
        
        // SRP / URP / Unity 6 uyumluluğu için RenderPipelineManager olaylarını dinle
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPostRender -= OnCameraPostRender;

        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        
        RestoreOriginals();
    }

    private void OnBeginCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
    {
        OnCameraPreCull(cam);
    }

    private void OnEndCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
    {
        OnCameraPostRender(cam);
    }

    private void OnCameraPreCull(Camera cam)
    {
        if (!isInitialized || piece == null || cam == null) return;

        // Mini harita kamerası ise özel görünüme geç, diğer tüm kameralarda (Main, Scene vb.) orijinal görünüme dön
        if (cam.name == "MiniMapCamera")
        {
            SwapToMinimapVisuals();
        }
        else
        {
            RestoreOriginals();
        }
    }

    private void OnCameraPostRender(Camera cam)
    {
        if (!isInitialized || piece == null || cam == null) return;

        // Mini harita kamerasının render işlemi bittiğinde de güvenli şekilde orijinal durumuna geri çek
        if (cam.name == "MiniMapCamera")
        {
            RestoreOriginals();
        }
    }

    private void SwapToMinimapVisuals()
    {
        if (isSwapped) return;

        // 1. Taşın altındaki tüm dairelerin o anki materyalini kaydet ve unlit mavi/kırmızı yap
        foreach (var mc in matChangesList)
        {
            if (mc != null)
            {
                Renderer r = mc.GetComponent<Renderer>();
                if (r != null)
                {
                    // O anki materyali kaydet
                    ActiveMaterialPair pair = activeMaterials.Find(x => x.renderer == r);
                    if (pair.renderer == null)
                    {
                        activeMaterials.Add(new ActiveMaterialPair 
                        { 
                            renderer = r, 
                            originalMaterial = r.sharedMaterial 
                        });
                    }
                    else
                    {
                        // Zaten varsa materyali güncelle (seçim rengi vb. değişmiş olabilir)
                        int index = activeMaterials.FindIndex(x => x.renderer == r);
                        activeMaterials[index] = new ActiveMaterialPair 
                        { 
                            renderer = r, 
                            originalMaterial = r.sharedMaterial 
                        };
                    }

                    // Mini map için mavi/kırmızı materyali uygula
                    r.sharedMaterial = (piece.Piece.Color == ChessColor.White) ? minimapWhiteMat : minimapBlackMat;
                }
            }
        }

        // 2. Taşın boyutunu mini haritada kuş bakışı daha rahat görülmesi için %25 büyüt
        if (piece != null)
        {
            piece.transform.localScale = originalScale * 1.25f;
        }

        isSwapped = true;
    }

    private void RestoreOriginals()
    {
        if (!isSwapped) return;

        // 1. Kaydedilen orijinal materyalleri geri yükle
        foreach (var pair in activeMaterials)
        {
            if (pair.renderer != null)
            {
                pair.renderer.sharedMaterial = pair.originalMaterial;
            }
        }

        // 2. Orijinal boyuta geri dön
        if (piece != null)
        {
            piece.transform.localScale = originalScale;
        }

        isSwapped = false;
    }

    void Update()
    {
        if (isInitialized && piece == null)
        {
            Destroy(gameObject);
        }
    }
}
