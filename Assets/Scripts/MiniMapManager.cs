using UnityEngine;
using ChessEngine.Game;

public class MiniMapManager : MonoBehaviour
{
    // Editörden bağımsız olarak oyun başladığında sahneye kendini eklemesini sağlayan Unity metodu
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeOnLoad()
    {
        GameObject go = new GameObject("MiniMapManager");
        go.AddComponent<MiniMapManager>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        // Taşları düzenli aralıklarla kontrol et (yeni taşlar oyuna girdiğinde algılamak için)
        InvokeRepeating(nameof(ScanPieces), 0.2f, 0.5f);
    }

    private void ScanPieces()
    {
        VisualChessPiece[] pieces = FindObjectsByType<VisualChessPiece>(FindObjectsSortMode.None);
        foreach (VisualChessPiece piece in pieces)
        {
            if (piece == null || piece.Piece == null) continue;

            // Zaten bir MiniMapMarker'a sahip mi kontrol et
            if (piece.GetComponentInChildren<MiniMapMarker>() == null)
            {
                // İşaretçiyi barındıracak alt bir obje oluştur
                GameObject markerHolder = new GameObject("MiniMapMarkerHolder");
                markerHolder.transform.SetParent(piece.transform, false);
                
                MiniMapMarker marker = markerHolder.AddComponent<MiniMapMarker>();
                marker.Initialize(piece);
            }
        }
    }
}
