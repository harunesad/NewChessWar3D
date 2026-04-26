using UnityEngine;
using ChessEngine;
using ChessEngine.Game;
using System.Collections;

public class BodylinkTurnManager : MonoBehaviour
{
    [Header("Single Character References")]
    [SerializeField] private ChessGameManager chessGameManager;
    [SerializeField] private BodylinkHumanoidAvatar targetAvatar;
    [SerializeField] private BodylinkTwoHandInteractor targetInteractor;
    [SerializeField] private Camera mainCamera;

    [Header("Position Settings")]
    [SerializeField] private Vector3 whiteSideOffset = new Vector3(0, 0, 0); // Beyaz tarafındaki merkezi
    [SerializeField] private Vector3 blackSideOffset = new Vector3(0, 0, 0); // Siyah tarafındaki merkezi

    [Header("Camera Transition Settings")]
    [SerializeField] private float transitionSpeed = 3f;
    [SerializeField] private float rotationSpeed = 3f;

    private Transform currentTargetAnchor;
    private bool isTransitioning = false;

    void Start()
    {
        if (chessGameManager == null) chessGameManager = FindAnyObjectByType<ChessGameManager>();
        if (mainCamera == null) mainCamera = Camera.main;

        chessGameManager.TurnStarted.AddListener(OnTurnStarted);

        // İlk sıra
        UpdateTurnStates(ChessColor.White);
    }

    void OnTurnStarted(ChessColor activeColor)
    {
        UpdateTurnStates(activeColor);
    }

    private void UpdateTurnStates(ChessColor activeColor)
    {
        bool isWhiteTurn = (activeColor == ChessColor.White);

        if (targetAvatar != null)
        {
            // OYUNCU DEĞİŞTİR: PlayerIndex'i güncelle
            targetAvatar.playerIndex = isWhiteTurn ? 0 : 1;
            
            // YÖNÜ DEĞİŞTİR: Karşıya dön
            targetAvatar.rotationOffset = isWhiteTurn ? 0 : -180f;
            
            // POZİSYONU DEĞİŞTİR: Tahtanın diğer ucuna geç
            targetAvatar.offset = isWhiteTurn ? whiteSideOffset : blackSideOffset;
            
            // Kalibrasyonu sıfırla (yeni kişi için)
            targetAvatar.DeepReset();

            if (targetAvatar.cameraAnchor != null)
                currentTargetAnchor = targetAvatar.cameraAnchor;
        }

        if (targetInteractor != null)
        {
            // İnteraktör oyuncu endeksini de güncelle
            targetInteractor.playerIndex = isWhiteTurn ? 0 : 1;
        }

        if (currentTargetAnchor != null) isTransitioning = true;
    }

    void LateUpdate()
    {
        if (currentTargetAnchor == null) return;

        // Kamerayı pürüzsüzce hedefe taşı ve döndür
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, currentTargetAnchor.position, Time.deltaTime * transitionSpeed);
        mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, currentTargetAnchor.rotation, Time.deltaTime * rotationSpeed);

        // Eğer hedefe çok yaklaştıysak geçişi durdurabiliriz (opsiyonel)
        if (Vector3.Distance(mainCamera.transform.position, currentTargetAnchor.position) < 0.01f && 
            Quaternion.Angle(mainCamera.transform.rotation, currentTargetAnchor.rotation) < 0.1f)
        {
            isTransitioning = false;
        }
    }
}
