using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BodylinkSDK;
using ChessEngine.Game;
using Mediapipe.Tasks.Components.Containers;

public class BodylinkChessInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ChessGameManager chessGameManager;
    [SerializeField] private GameObject cursorVisual;

    [Header("Settings")]
    [SerializeField] private float maxRaycastDistance = 1000f;
    [SerializeField] private LayerMask tileLayer;
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
    [SerializeField] private float smoothSpeed = 0.15f; // Lerp hızı

    private Bodylink bodylink;
    private Vector2 currentScreenPos;
    private bool isGestureActive = false;

    private const float MIN_VISIBILITY = 0.05f;

    void Start()
    {
        bodylink = Bodylink.Instance;
        
        if (chessGameManager == null)
            chessGameManager = FindAnyObjectByType<ChessGameManager>();
            
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (bodylink != null)
        {
            bodylink.inputEvents.OnPoseDetection += HandlePoseDetection;
            bodylink.OnInitialized += () => {
                HideBodylinkVisuals();
                CreateMiniCam();
            };
            
            if (bodylink.IsInitialized)
            {
                HideBodylinkVisuals();
                CreateMiniCam();
            }
        }
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

        Debug.Log("Bodylink: SDK görselleri (Canvas/Silüet/Maske) temizlendi.");
    }

    private void CreateMiniCam()
    {
        if (bodylink == null || bodylink.cameraScreen == null) return;

        Canvas mainCanvas = FindAnyObjectByType<Canvas>();
        if (mainCanvas == null) return;

        GameObject rawImageObj = new GameObject("Bodylink_MiniCam");
        rawImageObj.transform.SetParent(mainCanvas.transform, false);
        var rawImage = rawImageObj.AddComponent<UnityEngine.UI.RawImage>();

        rawImage.texture = bodylink.cameraScreen.texture;

        RectTransform rect = rawImage.rectTransform;
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-250, 20);
        rect.sizeDelta = new Vector2(240, 135);

        rawImage.color = new Color(1, 1, 1, 0.4f);
        rect.localScale = new Vector3(-1, 1, 1);

        Debug.Log($"Bodylink: Şeffaf Mini-Cam '{mainCanvas.name}' altına eklendi.");
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
        // Sadece belirlenen eli ve birinci oyuncuyu (player 0) dinle
        if (playerIndex != 0 || side != activeHand) return;

        // "Victory" (Zafer İşareti) jestini tıklama olarak kabul et
        if (pose == HandPose.Victory)
        {
            if (!isGestureActive)
            {
                isGestureActive = true;
                TrySelectTile();
            }
        }
        else if (pose == HandPose.Open_Palm || pose == HandPose.None || pose == HandPose.Pointing_Up)
        {
            isGestureActive = false;
        }
    }

    void Update()
    {
        if (bodylink == null || !bodylink.IsInitialized || bodylink.players == null || bodylink.players.Length == 0) return;

        UpdateCursorPosition();
    }

    private void UpdateCursorPosition()
    {
        // Tıklama (yumruk) aktifken imleç pozisyonunu dondur (Click Freeze)
        if (isGestureActive) return;

        var player = bodylink.players[0];
        float x = 0, y = 0;
        bool found = false;

        // Öncelik: El (Hand) Landmarkları - İşaret Parmağı Ucu (Index Tip - 8)
        var hand = (activeHand == Side.Left) ? player.handPoints[0] : player.handPoints[1];
        if (hand != null && hand.handLandmark != null && hand.handLandmark.Count > 8)
        {
            var point = hand.handLandmark[8]; // İşaret parmağı ucu
            x = point.x;
            y = 1f - point.y;
            found = true;
        }
        else
        {
            // Yedek: Vücut (Pose) Landmarkları - Bilek
            NormalizedLandmark wrist = (activeHand == Side.Left) 
                ? (useSmoothedPoints ? player.body2DSmoothed.leftWrist : player.body2D.leftWrist)
                : (useSmoothedPoints ? player.body2DSmoothed.rightWrist : player.body2D.rightWrist);

            if (wrist.visibility >= MIN_VISIBILITY)
            {
                x = wrist.x;
                y = wrist.y;
                found = true;
            }
        }

        if (!found) return;

        // X Aynalama
        if (mirrorX) x = 1f - x;

        // Hassasiyet ve Ofset Uygulama (Mapping)
        x = (x - 0.5f - xOffset) * sensitivity + 0.5f;
        y = (y - 0.5f - yOffset) * sensitivity + 0.5f;

        // Ekran sınırlarına kilitle (Clamp)
        x = Mathf.Clamp01(x);
        y = Mathf.Clamp01(y);

        Vector2 targetScreenPos = new Vector2(x * Screen.width, y * Screen.height);
        
        // Lerp ile akıcı yumuşatma (CursorPointer referansı)
        currentScreenPos = Vector2.Lerp(currentScreenPos, targetScreenPos, smoothSpeed);

        // Görsel imleci güncelle
        if (cursorVisual != null)
        {
            cursorVisual.transform.position = currentScreenPos;
        }
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
}
