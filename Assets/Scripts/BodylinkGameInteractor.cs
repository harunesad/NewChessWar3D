using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BodylinkSDK;
using ChessEngine.Game;
using Mediapipe.Tasks.Components.Containers;

public class BodylinkGameInteractor : MonoBehaviour
{
    public static BodylinkGameInteractor Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ChessGameManager chessGameManager;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private GameObject cursorVisual;

    [Header("Settings")]
    [SerializeField] private float maxRaycastDistance = 1000f;
    [SerializeField] private LayerMask tileLayer = ~0; // Varsayılan olarak her şeyi tara (Default dahil)
    [SerializeField] private Side activeHand = Side.Right;
    [SerializeField] private bool autoSelectHand = true; 
    [SerializeField] private bool useSmoothedPoints = true;
    [SerializeField] private bool mirrorX = true;

    [Header("Mapping & Sensitivity")]
    [Range(1f, 5f)]
    [SerializeField] private float sensitivity = 1.8f;
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float yOffset = 0.15f;
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float xOffset = 0f;
    [Range(0.01f, 1f)]
    [SerializeField] private float smoothFactor = 0.15f; // EMA filter factor

    private Bodylink bodylink;
    public Vector2 currentScreenPos;
    public bool isGestureActive = false;
    private PointerEventData pointerData;
    private List<RaycastResult> raycastResults = new List<RaycastResult>();
    
    [Header("Hover Click Settings")]
    [SerializeField] private bool useHoverClick = true;
    [SerializeField] private float hoverDuration = 2.0f;
    [SerializeField] private float hoverMoveThreshold = 50f;
    
    private float hoverTimer = 0f;
    private Vector2 lastHoverPos;
    private Image cursorImage;

    [Header("Pinch Grab Settings")]
    [SerializeField] private float pinchThreshold = 0.065f; 
    [SerializeField] private float releaseThreshold = 0.09f; 
    [SerializeField] private float liftAmount = 1.5f; 
    [SerializeField] private float pinchGraceTime = 0.2f; 
    
    private float smoothedPinchDist = 0.5f; 
    
    public bool isPinching = false;
    private float pinchGraceTimer = 0f;
    private VisualChessPiece grabbedPiece = null;
    private Vector3 originalPiecePos;
    private VisualChessTableTile originalTile = null;
    
    private RawImage miniCamRawImage; // Texture senkronizasyonu için referans

    private const float MIN_VISIBILITY = 0.05f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        bodylink = Bodylink.Instance;
        
        if (chessGameManager == null)
            chessGameManager = FindAnyObjectByType<ChessGameManager>();
            
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (eventSystem == null)
            eventSystem = FindAnyObjectByType<EventSystem>();

        if (bodylink != null)
        {
            // Olay aboneliğini temizleyip yeniden yap
            bodylink.inputEvents.OnPoseDetection -= HandlePoseDetection;
            bodylink.inputEvents.OnPoseDetection += HandlePoseDetection;

            bodylink.OnInitialized -= InitializeBodylinkComponents;
            bodylink.OnInitialized += InitializeBodylinkComponents;
            
            // Bu sahne için kalibrasyonu otomatik atla
            if (!bodylink.IsCalibrated)
            {
                bodylink.DisableCalibration();
            }

            if (bodylink.IsInitialized)
            {
                InitializeBodylinkComponents();
                
                // Restart durumunda akışı zorla canlandır
                if (bodylink.imageSource != null)
                {
                    StartCoroutine(bodylink.imageSource.Resume());
                }
            }
        }

        pointerData = new PointerEventData(eventSystem);

        if (cursorVisual != null)
            cursorImage = cursorVisual.GetComponent<Image>();
    }

    private void InitializeBodylinkComponents()
    {
        if (bodylink == null) return;
        bodylink.DisplayCameraFeed(true);
    }



    void OnDestroy()
    {
        if (bodylink != null && bodylink.inputEvents != null)
        {
            bodylink.inputEvents.OnPoseDetection -= HandlePoseDetection;
        }
    }

    private void HandlePoseDetection(int playerIndex, string poseName, Side side, HandPose pose)
    {
        if (playerIndex != 0) return;
        
        // Otomatik el seçiminde her iki ele de bak
        if (!autoSelectHand && side != activeHand) return;

        if (pose == HandPose.Victory)
        {
            if (!isGestureActive)
            {
                isGestureActive = true;
                HandleSmartClick();
            }
        }
        else if (pose == HandPose.Open_Palm || pose == HandPose.None || pose == HandPose.Pointing_Up)
        {
            isGestureActive = false;
        }
    }

    private void HandleSmartClick()
    {
        // 1) Önce UI Kontrolü
        if (eventSystem != null)
        {
            pointerData.position = currentScreenPos;
            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                Button btn = result.gameObject.GetComponentInParent<Button>();
                if (btn != null && btn.interactable)
                {
                    Debug.Log($"Bodylink: UI Butonuna tıklandı: {btn.gameObject.name}");
                    btn.onClick.Invoke();
                    return; // UI'da işlem yapıldıysa 3D'ye bakma
                }
            }
        }

        // 2) Eğer UI'da bir şey yoksa, Satranç Tahtası Kontrolü
        TrySelectTile();
    }

    private void TrySelectTile()
    {
        if (mainCamera == null || chessGameManager == null) return;

        Ray ray = mainCamera.ScreenPointToRay(currentScreenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, tileLayer))
        {
            VisualChessTableTile tile = hit.collider.GetComponent<VisualChessTableTile>();
            if (tile != null)
            {
                Debug.Log($"Bodylink: Kare seçildi: {tile.Tile.TileIndex}");
                tile.Select();
            }
        }
    }

    void Update()
    {
        if (bodylink.players == null || bodylink.players.Length == 0) return;

        UpdateCursorPosition();
        
        // Pinch & Drag Mantığı
        if (grabbedPiece != null)
        {
            HandleDragging();
        }
    }

    private void UpdateCursorPosition()
    {
        var player = bodylink.players[0];
        float x = 0, y = 0;
        bool found = false;

        // DUAL-HAND LOGIC: Hangi el o an görülüyorsa ona geçiş yap
        BodylinkHandPoints hand = null;
        var leftH = player.handPoints[0];
        var rightH = player.handPoints[1];
        
        bool leftValid = leftH != null && leftH.handLandmark != null && leftH.handLandmark.Count > 10;
        bool rightValid = rightH != null && rightH.handLandmark != null && rightH.handLandmark.Count > 10;

        if (autoSelectHand)
        {
            // Eğer iki el de varsa, o anki aktif eli korumaya çalış, yoksa görülen eli seç
            if (leftValid && rightValid) hand = (activeHand == Side.Left) ? leftH : rightH;
            else if (leftValid) { hand = leftH; activeHand = Side.Left; }
            else if (rightValid) { hand = rightH; activeHand = Side.Right; }
        }
        else
        {
            hand = (activeHand == Side.Left) ? leftH : rightH;
            // Manuel seçimde de el bulunamazsa diğerine fallback yap (opsiyonel ama daha stabil)
            if ((hand == null || hand.handLandmark == null) && (leftValid || rightValid))
            {
                hand = leftValid ? leftH : rightH;
                activeHand = leftValid ? Side.Left : Side.Right;
            }
        }

        if (hand != null && hand.handLandmark != null && hand.handLandmark.Count > 8)
        {
            var thumbTip = hand.handLandmark[4];
            var indexTip = hand.handLandmark[8];
            
            x = (thumbTip.x + indexTip.x) / 2f;
            y = 1f - ((thumbTip.y + indexTip.y) / 2f);
            found = true;

            // Pinch Mesafesi (EMA Filtresi uygulanmış haliyle)
            float dist = CalculatePinchDistance(hand);
            
            if (!isPinching)
            {
                if (dist < pinchThreshold) 
                {
                    pinchGraceTimer = 0;
                    StartPinch();
                }
            }
            else
            {
                // Bırakma kontrolünde Grace Time (Zarif Bırakma Süresi) kullan
                if (dist > releaseThreshold) 
                {
                    pinchGraceTimer += Time.deltaTime;
                    if (pinchGraceTimer >= pinchGraceTime)
                    {
                        EndPinch();
                    }
                }
                else
                {
                    pinchGraceTimer = 0; // Mesafe tekrar daralırsa süreyi sıfırla
                }
            }
        }
        else
        {
            if (isPinching) EndPinch();
            
            NormalizedLandmark wrist = (activeHand == Side.Left) 
                ? (useSmoothedPoints ? player.body2DSmoothed.leftWrist : player.body2D.leftWrist)
                : (useSmoothedPoints ? player.body2D.rightWrist : player.body2DSmoothed.rightWrist);

            if (wrist.visibility >= MIN_VISIBILITY)
            {
                x = wrist.x;
                y = wrist.y;
                found = true;
            }
            
            if (isPinching) EndPinch();
        }

        if (!found)
        {
            ResetHover();
            return;
        }

        if (mirrorX) x = 1f - x;

        x = (x - 0.5f - xOffset) * sensitivity + 0.5f;
        y = (y - 0.5f - yOffset) * sensitivity + 0.5f;

        x = Mathf.Clamp01(x);
        y = Mathf.Clamp01(y);

        Vector2 targetScreenPos = new Vector2(x * Screen.width, y * Screen.height);
        currentScreenPos = Vector2.Lerp(currentScreenPos, targetScreenPos, smoothFactor);

        if (cursorVisual != null)
        {
            cursorVisual.transform.position = currentScreenPos;
        }

        // HOVER CLICK LOGIC (Sadece bir şey tutmuyorsak ve manuel jest yoksa)
        if (useHoverClick && !isGestureActive && grabbedPiece == null)
        {
            HandleHoverLogic();
        }
    }

    private float CalculatePinchDistance(BodylinkSDK.BodylinkHandPoints hand)
    {
        // Sadece Baş Parmak (4) ve İşaret Parmağı (8) ucu mesafesi
        // 3D Landmark kullanıyoruz çünkü derinlik farkı iki parmağın üst üste bindiği "sahte" pinch'leri önler
        var p4 = hand.handLandmark[4]; // Baş parmak ucu
        var p8 = hand.handLandmark[8]; // İşaret parmağı ucu

        // NormalizedLandmark -> Vector3 çevirisi 
        Vector3 thumb = new Vector3(p4.x, p4.y, p4.z);
        Vector3 index = new Vector3(p8.x, p8.y, p8.z);

        float rawDist = Vector3.Distance(thumb, index);
        
        // EMA Filter for pinch distance
        smoothedPinchDist = Mathf.Lerp(smoothedPinchDist, rawDist, 0.4f); 

        return smoothedPinchDist;
    }

    private void StartPinch()
    {
        isPinching = true;
        
        if (CheckIfPointerOverUI()) return;

        Ray ray = mainCamera.ScreenPointToRay(currentScreenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
        {
            // Önce doğrudan taşın (VisualChessPiece) kendisine çarpıp çarpmadığına bak
            VisualChessPiece piece = hit.collider.GetComponentInParent<VisualChessPiece>();
            
            // Eğer doğrudan bir taş bulamadıysak, altındaki kareye (Tile) çarpıp çarpmadığına bak
            if (piece == null)
            {
                VisualChessTableTile visualTile = hit.collider.GetComponent<VisualChessTableTile>();
                if (visualTile != null)
                {
                    // Karenin üzerindeki taşı engine'in kendi metoduyla bulalım
                    piece = visualTile.GetVisualPiece();
                }
            }

            if (piece != null)
            {
                // 🔥 SIRA KONTROLÜ: Sadece sırası gelen rengin taşları tutulabilir
                if (chessGameManager != null && chessGameManager.ChessInstance != null)
                {
                    if (piece.Piece.Color != chessGameManager.ChessInstance.turn)
                    {
                        Debug.Log($"Bodylink: <color=yellow>Tutma reddedildi: Sıra {chessGameManager.ChessInstance.turn} oyuncusunda.</color>");
                        isPinching = false;
                        return;
                    }
                }

                grabbedPiece = piece;
                originalPiecePos = piece.transform.position;
                originalTile = VisualTableTileFromPiece(piece);
                
                Debug.Log($"Bodylink: <color=lime>Taş yakalandı: {piece.name}</color>");
                
                if (originalTile != null) originalTile.Select();
            }
            else
            {
                 Debug.Log($"Bodylink: Pinch yapıldı ama {hit.collider.name} üzerinde (veya bu karede) taş bulunamadı.");
            }
        }
    }

    private void EndPinch()
    {
        if (!isPinching) return;
        isPinching = false;
        pinchGraceTimer = 0;

        if (grabbedPiece != null)
        {
            // Bırakılan yerdeki kareyi bul
            Ray ray = mainCamera.ScreenPointToRay(currentScreenPos);
            
            // Sadece karoları (tileLayer) dikkate al
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, tileLayer))
            {
                VisualChessTableTile tile = hit.collider.GetComponent<VisualChessTableTile>();
                if (tile != null)
                {
                    Debug.Log($"Bodylink: Taş bırakıldı: {tile.Tile.TileIndex}");
                    tile.Select(); // Hamle yapmayı dene
                }
            }

            // KRİTİK DÜZELTME V2: Bırakma işlemi bittiğine göre, eğer hala bir şey seçili kalmışsa
            // (kareye isabet etmemiş olabilir veya hamle geçersiz olabilir), seçimi temizle.
            if (chessGameManager != null && chessGameManager.Selected.visualPiece != null)
            {
                chessGameManager.Deselect();
            }

            VisualChessPiece pieceToReset = grabbedPiece;
            grabbedPiece = null;
            pieceToReset.UpdatePosition(true);
        }
    }

    private void HandleDragging()
    {
        if (grabbedPiece == null) return;

        // Tahtanın yüzeyinde bir nokta bul. 
        // ÖNEMLİ: tileLayer katmanını 'Everything' yerine sadece tahta/karoların olduğu katman yapmalısınız.
        Ray ray = mainCamera.ScreenPointToRay(currentScreenPos);
        
        // Taşı sürüklerken kendinden geçsin diye raycast'te ray'in taşın kendisine çarpmamasını sağlamalıyız.
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, tileLayer))
        {
            // Eğer çarptığımız şey tuttuğumuz taşın kendisiyse, raycast'i biraz öteleyerek tekrar deneyebiliriz
            // veya Layermask kullanarak taşın katmanını (Piece) bu raycast'ten hariç tutabiliriz.
            if (hit.collider.transform.IsChildOf(grabbedPiece.transform)) return;

            Vector3 targetPos = hit.point + Vector3.up * liftAmount;
            // Tutulan taşı sürüklerken de düzgün yumuşatma uygula
            grabbedPiece.transform.position = Vector3.Lerp(grabbedPiece.transform.position, targetPos, smoothFactor * 1.5f);
        }
    }

    private bool CheckIfPointerOverUI()
    {
        if (eventSystem == null) return false;
        pointerData.position = currentScreenPos;
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);
        foreach (var r in raycastResults)
        {
            if (r.gameObject.GetComponentInParent<Button>() != null) return true;
        }
        return false;
    }

    private VisualChessTableTile VisualTableTileFromPiece(VisualChessPiece piece)
    {
        // Taşın altındaki tile'ı bulmak için basit bir raycast aşağıya
        if (Physics.Raycast(piece.transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f, tileLayer))
        {
            return hit.collider.GetComponent<VisualChessTableTile>();
        }
        return null;
    }

    private void HandleHoverLogic()
    {
        // 1) UI veya Satranç Karosu üzerinde miyiz kontrol et
        bool isOverInteractable = CheckIfPointerOverInteractable();

        if (!isOverInteractable)
        {
            ResetHover();
            return;
        }

        if (Vector2.Distance(currentScreenPos, lastHoverPos) > hoverMoveThreshold)
        {
            lastHoverPos = currentScreenPos;
            hoverTimer = 0f;
        }
        else
        {
            hoverTimer += Time.unscaledDeltaTime;
            
            if (cursorImage != null)
            {
                cursorImage.color = Color.Lerp(Color.white, Color.green, hoverTimer / hoverDuration);
                cursorVisual.transform.localScale = Vector3.one * (1f + (hoverTimer / hoverDuration) * 0.5f);
            }

            if (hoverTimer >= hoverDuration)
            {
                Debug.Log("Bodylink Game: Akıllı Hover süresi doldu, tıklanıyor...");
                HandleSmartClick();
                ResetHover();
            }
        }
    }

    private bool CheckIfPointerOverInteractable()
    {
        // A) Önce UI kontrolü
        if (eventSystem != null)
        {
            pointerData.position = currentScreenPos;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerData, raycastResults);

            if (raycastResults.Count > 0)
            {
                foreach (var result in raycastResults)
                {
                    GameObject target = result.gameObject;
                    while (target != null)
                    {
                        if (target.GetComponent<Button>() != null)
                            return true;

                        if (target.transform.parent == null) break;
                        target = target.transform.parent.gameObject;
                    }
                }
            }
        }

        return false;
    }

    private void ResetHover()
    {
        hoverTimer = 0f;
        if (cursorImage != null)
        {
            cursorImage.color = Color.white;
            cursorVisual.transform.localScale = Vector3.one;
        }
    }
}
