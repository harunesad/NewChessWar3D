using UnityEngine;
using BodylinkSDK;
using ChessEngine.Game;
using System.Collections;

public class BodylinkTwoHandInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChessGameManager chessGameManager;
    [SerializeField] private Transform characterTransform;
    [SerializeField] private Camera mainCamera;

    [Header("Grab Settings")]
    [Range(0.1f, 1.0f)]
    public float grabThreshold = 0.35f;    // Eller ne kadar yakın olmalı? (Omuz genişliğine oranla)
    [Range(0.2f, 1.5f)]
    public float releaseThreshold = 0.65f; // Eller ne kadar ayrılmalı?
    [SerializeField] private float interactorCooldown = 0.5f;
    [SerializeField] private LayerMask tileLayer;

    [Header("Multiplayer")]
    public int playerIndex = 0;
    public bool isTurnActive = true;

    [Header("Ghost Preview")]
    [SerializeField] private Material ghostMaterial;
    private GameObject ghostInstance;
    private VisualChessTableTile currentHoverTile;

    private Bodylink bodylink;
    private bool isHoldingGesture = false;
    private float lastActionTime = 0f;
    private Vector3 originalCameraPos;
    private float debugHandDist = 0;

    void Start()
    {
        bodylink = Bodylink.Instance;
        if (chessGameManager == null) chessGameManager = FindAnyObjectByType<ChessGameManager>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (characterTransform == null) characterTransform = transform;
    }

    void Update()
    {
        if (bodylink == null || !bodylink.IsInitialized || bodylink.players == null || bodylink.players.Length == 0) return;
        if (playerIndex >= bodylink.players.Length) return;

        // Sadece sırası gelince tutma/bırakma algıla
        if (isTurnActive)
        {
            HandleGrabDetection();
            UpdateGhostPreview();
        }
        else
        {
            ClearGhost();
        }
    }

    private void UpdateGhostPreview()
    {
        // Eğer bir taş seçili değilse hayalet taş olamaz
        if (chessGameManager.Selected.visualPiece == null)
        {
            ClearGhost();
            return;
        }

        // Altımızdaki kareyi bul
        Ray ray = new Ray(characterTransform.position + Vector3.up * 1f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 5f, tileLayer))
        {
            VisualChessTableTile tile = hit.collider.GetComponent<VisualChessTableTile>();
            if (tile != null)
            {
                // Hayalet taşı oluştur veya güncelle
                ShowGhostAt(tile);
                return;
            }
        }
        
        ClearGhost();
    }

    private void ShowGhostAt(VisualChessTableTile tile)
    {
        if (ghostInstance == null)
        {
            // Seçili taşın bir kopyasını oluştur
            GameObject original = chessGameManager.Selected.visualPiece.gameObject;
            ghostInstance = Instantiate(original);
            
            // Kolaylık için tüm colliderları kapat
            foreach (var col in ghostInstance.GetComponentsInChildren<Collider>()) col.enabled = false;
            
            // Materyalleri hayalet materyali ile değiştir
            foreach (var renderer in ghostInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.material = ghostMaterial;
            }
        }

        // Pozisyonu güncelle (Kareye oturt)
        ghostInstance.transform.position = tile.transform.position;
        ghostInstance.SetActive(true);
    }

    private void ClearGhost()
    {
        if (ghostInstance != null)
        {
            Destroy(ghostInstance);
            ghostInstance = null;
        }
    }

    private void HandleGrabDetection()
    {
        var player = bodylink.players[playerIndex];
        
        var wL = player.body2DSmoothed.leftWrist;
        var wR = player.body2DSmoothed.rightWrist;
        var sL = player.body2DSmoothed.leftShoulder;
        var sR = player.body2DSmoothed.rightShoulder;

        // Omuz Genişliği (Normalizasyon için)
        float shoulderWidth = Vector2.Distance(new Vector2(sL.x, sL.y), new Vector2(sR.x, sR.y));
        if (shoulderWidth < 0.01f) return;

        // İki el arasındaki mesafe (Omuz genişliğine oranla)
        float currentDist = Vector2.Distance(new Vector2(wL.x, wL.y), new Vector2(wR.x, wR.y)) / shoulderWidth;
        debugHandDist = currentDist;

        if (Time.time < lastActionTime + interactorCooldown) return;

        // 1. TUTMA: Eller birleşti mi?
        if (!isHoldingGesture && currentDist < grabThreshold)
        {
            isHoldingGesture = true;
            ExecuteInteraction("TUTULDU", true);
        }
        // 2. BIRAKMA: Eller ayrıldı mı?
        else if (isHoldingGesture && currentDist > releaseThreshold)
        {
            isHoldingGesture = false;
            ExecuteInteraction("BIRAKILDI", false);
        }
    }

    private void ExecuteInteraction(string actionName, bool isGrab)
    {
        lastActionTime = Time.time;
        
        bool isAlreadyHolding = (chessGameManager.Selected.visualTile != null);

        if (isGrab && isAlreadyHolding) return; 
        if (!isGrab && !isAlreadyHolding) return; 

        Debug.Log($"<color=lime>[Grab] {actionName} (P{playerIndex})!</color>");

        if (isGrab)
        {
            // TUTMA: Normal seçim yap
            if (TrySelectCurrentTile(true))
            {
                if (chessGameManager.Selected.visualPiece != null)
                {
                    AudioManager audioManager = FindAnyObjectByType<AudioManager>();
                    if (audioManager != null)
                    {
                        audioManager.Pick();
                    }
                }
            }
        }
        else
        {
            // BIRAKMA: Sadece geçerli hamleyse yap, yoksa iptal et (Yeni taş seçme!)
            TrySelectCurrentTile(false);
        }
    }

    private bool TrySelectCurrentTile(bool canSelectNew)
    {
        Ray ray = new Ray(characterTransform.position + Vector3.up * 1f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 5f, tileLayer))
        {
            VisualChessTableTile tile = hit.collider.GetComponent<VisualChessTableTile>();
            if (tile != null)
            {
                if (canSelectNew)
                {
                    chessGameManager.SelectTile(tile);
                    return true;
                }
                else
                {
                    // Sadece hamle listesindeyse işle (Yoksa sadece seçimi iptal et)
                    var sel = chessGameManager.Selected;
                    bool isValidMove = false;

                    // Hamleleri kontrol et
                    if (sel.validMoves != null) {
                        foreach(var m in sel.validMoves) {
                            if (chessGameManager.visualTable.GetVisualTile(m) == tile) { isValidMove = true; break; }
                        }
                    }
                    // Saldırıları kontrol et
                    if (!isValidMove && sel.validAttacks != null) {
                        foreach(var a in sel.validAttacks) {
                            if (chessGameManager.visualTable.GetVisualTile(a.attackTile) == tile) { isValidMove = true; break; }
                        }
                    }

                    if (isValidMove) {
                        chessGameManager.SelectTile(tile);
                    } else {
                        Debug.Log("<color=orange>[Grab] Invalid move/piece on Drop. Deselecting.</color>");
                        chessGameManager.Selected = default;
                    }
                    return true;
                }
            }
        }
        
        // Hiç kare bulunamadıysa ve bırakılıyorsa iptal et
        if (!canSelectNew) chessGameManager.Selected = default;
        return false;
    }


    void OnGUI() {
        var player = bodylink.players[0];
        
        // Yardımcı fonksiyon: Hem puanı hem de ekran sınırlarını kontrol eder
        bool IsInFrame(Mediapipe.Tasks.Components.Containers.NormalizedLandmark p) => 
            p.visibility > 0.5f && p.x > 0.01f && p.x < 0.99f && p.y > 0.01f && p.y < 0.99f;

        // Detaylı Görünürlük Kontrolü
        bool headOk = IsInFrame(player.body2DSmoothed.head);
        bool shouldersOk = IsInFrame(player.body2DSmoothed.leftShoulder) && IsInFrame(player.body2DSmoothed.rightShoulder);
        bool hipsOk = IsInFrame(player.body2DSmoothed.leftHip) && IsInFrame(player.body2DSmoothed.rightHip);

        GUIStyle style = new GUIStyle(); 
        style.fontSize = 40; 
        style.fontStyle = FontStyle.Bold;
        
        // 1. Detaylı Vücut Takip Durumu
        string bodyStatus;
        if (headOk && shouldersOk && hipsOk) {
            style.normal.textColor = Color.green;
            bodyStatus = "BODY TRACKING: FULLY VISIBLE";
        } else {
            style.normal.textColor = Color.red;
            string missing = "";
            if (!headOk) missing += "[HEAD] ";
            if (!shouldersOk) missing += "[SHOULDERS] ";
            if (!hipsOk) missing += "[HIPS] ";
            bodyStatus = "MISSING OR OUT OF FRAME: " + missing;
        }
        GUI.Label(new Rect(20, Screen.height / 2 - 50, 1200, 100), bodyStatus, style);

        // 2. El Mesafesi ve Tutma Durumu
        style.normal.textColor = Color.yellow;
        string handStatus = isHoldingGesture ? "<color=cyan>HOLDING</color>" : "HANDS OPEN";
        string handText = $"Hand Distance: {debugHandDist:F2} | Status: {handStatus}";
        GUI.Label(new Rect(20, Screen.height / 2 + 20, 1200, 100), handText, style);
    }
    public float GetHandDistance() { return debugHandDist; }
}
