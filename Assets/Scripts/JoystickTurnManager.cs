using UnityEngine;
using ChessEngine.Game;
using ChessEngine;

public class JoystickTurnManager : MonoBehaviour
{
    [Header("References")]
    public ChessGameManager gameManager;
    public RectTransform dynamicJoystickRoot; // Sahneye attığın "Dynamic Joystick" prefab'ını bağla

    [Header("Brawl Stars Style Zones")]
    [Tooltip("Beyaz oyuncunun hareket edebileceği geniş alan (Sol Alt Çeyrek/Yarı)")]
    public Vector2 whiteZoneMin = new Vector2(0, 0);
    public Vector2 whiteZoneMax = new Vector2(0.5f, 0.7f); 

    [Header("Black Player Zone")]
    [Tooltip("Siyah oyuncunun hareket edebileceği geniş alan (Sağ Alt Çeyrek/Yarı)")]
    public Vector2 blackZoneMin = new Vector2(0.5f, 0);
    public Vector2 blackZoneMax = new Vector2(1f, 0.7f);

    private void Awake()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<ChessGameManager>();
        
        if (gameManager != null)
        {
            gameManager.TurnStarted.AddListener(OnTurnStarted);
        }
    }

    private void Start()
    {
        if (gameManager != null)
            SetupJoystickZone(gameManager.ChessInstance.turn);
    }

    private void OnTurnStarted(ChessColor turn)
    {
        SetupJoystickZone(turn);
    }

    private void SetupJoystickZone(ChessColor turn)
    {
        if (dynamicJoystickRoot == null) return;

        // Joystick'in KÖK (Root) objesinin alanını turn'e göre değiştiriyoruz.
        // Dynamic Joystick, bu alanın NERESİNE dokunulursa orada kendiliğinden açılır.
        if (turn == ChessColor.White)
        {
            dynamicJoystickRoot.anchorMin = whiteZoneMin;
            dynamicJoystickRoot.anchorMax = whiteZoneMax;
        }
        else
        {
            dynamicJoystickRoot.anchorMin = blackZoneMin;
            dynamicJoystickRoot.anchorMax = blackZoneMax;
        }

        // Alanı tam ekran koordinatlarına oturtmak için offsetleri sıfırla
        dynamicJoystickRoot.offsetMin = Vector2.zero;
        dynamicJoystickRoot.offsetMax = Vector2.zero;

        // ÖNEMLİ: Dynamic Joystick prefab'ının içindeki Background ve Handle objeleri 
        // başlangıçta bu alanın ortasında durabilir. Dokunana kadar gizli kalmasını istiyorsan
        // Dynamic Joystick bileşenindeki ayarları kontrol et.
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.TurnStarted.RemoveListener(OnTurnStarted);
    }
}
