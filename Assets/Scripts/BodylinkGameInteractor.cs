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
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ChessGameManager chessGameManager;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private GameObject cursorVisual;

    [Header("Settings")]
    [SerializeField] private float maxRaycastDistance = 1000f;
    [SerializeField] private LayerMask tileLayer = ~0; // Varsayılan olarak her şeyi tara (Default dahil)
    [SerializeField] private Side activeHand = Side.Right;
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
    [SerializeField] private float smoothSpeed = 0.15f;

    private Bodylink bodylink;
    private Vector2 currentScreenPos;
    private bool isGestureActive = false;
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
    [SerializeField] private float pinchThreshold = 0.05f; 
    [SerializeField] private float liftAmount = 1.5f; 
    [SerializeField] private float pinchGraceTime = 0.2f; // Titremeyi önlemek için ek süre (sn)
    
    private bool isPinching = false;
    private float pinchGraceTimer = 0f;
    private VisualChessPiece grabbedPiece = null;
    private Vector3 originalPiecePos;
    private VisualChessTableTile originalTile = null;
    
    private RawImage miniCamRawImage; // Texture senkronizasyonu için referans

    private const float MIN_VISIBILITY = 0.05f;

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

        HideBodylinkVisuals();
        
        // İlk açılışta görüntüyü garantilemek için feed'i aktif et
        bodylink.DisplayCameraFeed(true);

        // Mini-Cam oluşturmayı bir Coroutine ile başlat
        StopCoroutine("CreateMiniCamRoutine");
        StartCoroutine("CreateMiniCamRoutine");
    }

    private void HideBodylinkVisuals()
    {
        if (bodylink == null || bodylink.PoseLandmarkerRunnerInstance == null) return;

        var canvases = bodylink.PoseLandmarkerRunnerInstance.GetComponentsInChildren<Canvas>(true);
        foreach (var canvas in canvases)
            canvas.enabled = false;

        var maskAnnotations = bodylink.PoseLandmarkerRunnerInstance.GetComponentsInChildren<Mediapipe.Unity.MultiPoseLandmarkListWithMaskAnnotation>(true);
        foreach (var mask in maskAnnotations)
            mask.gameObject.SetActive(false);

        var handAnnotations = bodylink.PoseLandmarkerRunnerInstance.GetComponentsInChildren<Mediapipe.Unity.MultiHandLandmarkListAnnotation>(true);
        foreach (var hand in handAnnotations)
            hand.gameObject.SetActive(false);

        if (bodylink.skeletonVisualizers != null)
        {
            foreach (var skel in bodylink.skeletonVisualizers)
                skel.gameObject.SetActive(false);
        }
    }

    private IEnumerator CreateMiniCamRoutine()
    {
        // Bodylink ve CameraScreen hazır olana kadar bekle
        while (bodylink == null || bodylink.cameraScreen == null)
            yield return null;

        // Texture hazır olana kadar bekle (Periyodik olarak SDK'yı dürt)
        int retryCount = 0;
        while (bodylink.cameraScreen.texture == null)
        {
            retryCount++;
            if (retryCount % 100 == 0)
            {
                bodylink.DisplayCameraFeed(true);
                if (bodylink.imageSource != null) StartCoroutine(bodylink.imageSource.Resume());
            }
            yield return null;
        }

        Canvas mainCanvas = FindAnyObjectByType<Canvas>();
        if (mainCanvas == null) yield break;

        // Varsa eski Mini-Cam'i temizle
        GameObject oldCam = GameObject.Find("Bodylink_MiniCam");
        if (oldCam != null) Destroy(oldCam);
        yield return new WaitForEndOfFrame();

        GameObject rawImageObj = new GameObject("Bodylink_MiniCam");
        rawImageObj.transform.SetParent(mainCanvas.transform, false);
        miniCamRawImage = rawImageObj.AddComponent<UnityEngine.UI.RawImage>();

        RectTransform rect = miniCamRawImage.rectTransform;
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-250, 20);
        rect.sizeDelta = new Vector2(240, 135);

        miniCamRawImage.color = new Color(1, 1, 1, 0.4f);
        rect.localScale = new Vector3(-1, 1, 1);
        
        Debug.Log("Bodylink Game: Mini-Cam objesi oluşturuldu, senkronizasyon Update'de sürecek.");
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
        if (playerIndex != 0 || side != activeHand) return;

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
        if (bodylink == null || !bodylink.IsInitialized) return;

        // Mini-Cam Texture Senkronizasyonu
        if (miniCamRawImage != null && bodylink.cameraScreen != null)
        {
            if (miniCamRawImage.texture != bodylink.cameraScreen.texture)
            {
                miniCamRawImage.texture = bodylink.cameraScreen.texture;
            }
        }

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

        var hand = (activeHand == Side.Left) ? player.handPoints[0] : player.handPoints[1];
        if (hand != null && hand.handLandmark != null && hand.handLandmark.Count > 12)
        {
            // İşaret parmağı ucu imleç pozisyonu için
            var indexTip = hand.handLandmark[8];
            x = indexTip.x;
            y = 1f - indexTip.y;
            found = true;

            // Pinch (Cımbız) Mesafesi Hesapla
            float dist = CalculatePinchDistance(hand);
            bool pinchDetected = dist < pinchThreshold;

            if (pinchDetected)
            {
                pinchGraceTimer = pinchGraceTime;
                if (!isPinching) StartPinch();
            }
            else
            {
                if (pinchGraceTimer > 0)
                {
                    pinchGraceTimer -= Time.unscaledDeltaTime;
                }
                else if (isPinching)
                {
                    EndPinch();
                }
            }
        }
        else
        {
            // El kaybolursa hemen bırak
            if (isPinching) EndPinch();
            
            NormalizedLandmark wrist = (activeHand == Side.Left) 
                ? (useSmoothedPoints ? player.body2DSmoothed.leftWrist : player.body2D.leftWrist)
                : (useSmoothedPoints ? player.body2DSmoothed.rightWrist : player.body2D.rightWrist);

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
        currentScreenPos = Vector2.Lerp(currentScreenPos, targetScreenPos, smoothSpeed);

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
        var p4 = hand.handLandmark[4]; // Baş
        var p8 = hand.handLandmark[8]; // İşaret
        var p12 = hand.handLandmark[12]; // Orta

        float d48 = Vector3.Distance(new Vector3(p4.x, p4.y, p4.z), new Vector3(p8.x, p8.y, p8.z));
        float d812 = Vector3.Distance(new Vector3(p8.x, p8.y, p8.z), new Vector3(p12.x, p12.y, p12.z));
        float d412 = Vector3.Distance(new Vector3(p4.x, p4.y, p4.z), new Vector3(p12.x, p12.y, p12.z));

        float avgDist = (d48 + d812 + d412) / 3f;
        
        // Debug için konsolda her karede değil, mesafe düşükken log basabiliriz
        if (avgDist < pinchThreshold * 2f)
        {
            // Debug.Log($"Bodylink: Mevcut Pinch Mesafesi: {avgDist:F4} (Hedef: < {pinchThreshold})");
        }

        return avgDist;
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
            
            // ÖNEMLİ: Taş bırakıldığında her durumda UpdatePosition'ı çağırıyoruz.
            // Eğer hamle geçerliyse zaten Chess Engine tarafından animasyonla yeni yerine gider.
            // Eğer hamle geçersizse, taş animasyonla ESKİ yerine otomatik döner.
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
            grabbedPiece.transform.position = Vector3.Lerp(grabbedPiece.transform.position, targetPos, 0.2f);
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
