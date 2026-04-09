using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.AI;
using BodylinkSDK; // Bodylink SDK eklendi

public class CamSwerve : MonoBehaviour
{
    float lastFrameFingerPositionX;
    public float moveFactorX;
    [SerializeField] List<Transform> camPos;
    [SerializeField] Vector3 lookPos;
    public int camPosIndex;

    private BodylinkEvents bodylinkEvents;
    private bool wasPinchingLastFrame = false;
    private bool isDraggingWithBodylink = false;

    void Start()
    {
        // Bodylink aboneliği başlat
        bodylinkEvents = FindAnyObjectByType<BodylinkEvents>();
        if (bodylinkEvents != null)
        {
            bodylinkEvents.OnGestureDetection += OnGestureDetected;
        }
    }

    void OnDestroy()
    {
        // Bodylink aboneliğini iptal et
        if (bodylinkEvents != null)
        {
            bodylinkEvents.OnGestureDetection -= OnGestureDetected;
        }
    }

    void Update()
    {
        System();
        transform.LookAt(lookPos);
    }

    public void System()
    {
        bool isPinching = (BodylinkGameInteractor.Instance != null && BodylinkGameInteractor.Instance.isPinching);
        // Eğer bodylink interactor varsa onun imlecini, yoksa normal fareyi kullan
        Vector2 currentInputPos = (BodylinkGameInteractor.Instance != null) ? BodylinkGameInteractor.Instance.currentScreenPos : (Vector2)Input.mousePosition;

        // DRAG BAŞLANGICI (Fare Tıklama veya Bodylink Pinch Başlangıcı)
        if (Input.GetMouseButtonDown(0) || (isPinching && !wasPinchingLastFrame))
        {
            lastFrameFingerPositionX = (Input.GetMouseButtonDown(0)) ? Input.mousePosition.x : currentInputPos.x;
            // Eğer bodylink ile başladıysak bu sürükleme oturumunu işaretle
            if (isPinching && !Input.GetMouseButton(0)) isDraggingWithBodylink = true;
        }
        // DRAG DEVAM (Fare Basılı veya Bodylink Pinch Devam)
        else if (Input.GetMouseButton(0) || (isPinching && isDraggingWithBodylink))
        {
            float currentX = (Input.GetMouseButton(0)) ? Input.mousePosition.x : currentInputPos.x;
            moveFactorX = currentX - lastFrameFingerPositionX;
        }
        // DRAG BİTİŞ (Fare Bırakma veya Bodylink Pinch Bırakma)
        else if (Input.GetMouseButtonUp(0) || (!isPinching && wasPinchingLastFrame && isDraggingWithBodylink))
        {
            Move();
            moveFactorX = 0f;
            isDraggingWithBodylink = false;
        }
        
        wasPinchingLastFrame = isPinching;
    }

    // Fare/Ekran kaydırma kontrolü
    public void Move()
    {
        if (moveFactorX < -500)
        {
            MoveLeft();
        }
        else if (moveFactorX > 500)
        {
            MoveRight();
        }
    }

    // Ortak Sola Dönüş (Fare moveFactorX < -500 VEYA SwipeLeft)
    private void MoveLeft()
    {
        if (camPosIndex == 0)
        {
            camPosIndex = 3;
        }
        else
        {
            camPosIndex--;
        }
        transform.DOMove(camPos[camPosIndex].position, 0.4f);
    }

    // Ortak Sağa Dönüş (Fare moveFactorX > 500 VEYA SwipeRight)
    private void MoveRight()
    {
        if (camPosIndex == 3)
        {
            camPosIndex = 0;
        }
        else
        {
            camPosIndex++;
        }
        transform.DOMove(camPos[camPosIndex].position, 0.4f);
    }

    // Cooldown mekanizması (peş peşe yanlış tetiklenmeleri önler)
    private float lastSwipeTime = 0f;
    private float swipeCooldown = 0.6f; // Kameranın dönmesi bitene kadar bekle (0.4f + biraz pay)

    // Bodylink Swipe Algılayıcısı
    private void OnGestureDetected(int playerIndex, string gestureName, object[] values)
    {
        if (Time.time < lastSwipeTime + swipeCooldown) return;
        
        // Kafa hareketi (Look) için ekstra stabilizasyon: Jest ismine göre küçük bir filtre
        if (gestureName.Contains("Look"))
        {
            // Hafif yatmaları engellemek için Debug log ile takip edebiliriz
            Debug.Log($"[Bodylink] Kafa Jesti: {gestureName}");
        }

        if (gestureName == "SwipeLeft" || gestureName == "LookLeft")
        {
            Debug.Log("[Bodylink] Sola Bakış/Kaydırma algılandı - Kamera sola dönüyor");
            MoveLeft();
            lastSwipeTime = Time.time;
        }
        else if (gestureName == "SwipeRight" || gestureName == "LookRight")
        {
            Debug.Log("[Bodylink] Sağa Bakış/Kaydırma algılandı - Kamera sağa dönüyor");
            MoveRight();
            lastSwipeTime = Time.time;
        }
    }
}
