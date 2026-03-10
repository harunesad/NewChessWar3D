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
        if (Input.GetMouseButtonDown(0))
        {
            lastFrameFingerPositionX = Input.mousePosition.x;
        }
        else if (Input.GetMouseButton(0))
        {
            moveFactorX = Input.mousePosition.x - lastFrameFingerPositionX;
            //lastFrameFingerPositionX = Input.mousePosition.x;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            Move();
            moveFactorX = 0f;
        }
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
        // Eğer son kaydırmanın üzerinden yeterli süre geçmediyse bu hareketi görmezden gel
        if (Time.time < lastSwipeTime + swipeCooldown) return;

        if (gestureName == "SwipeLeft")
        {
            Debug.Log("[Bodylink] SwipeLeft algılandı - Kamera sola dönüyor");
            MoveLeft();
            lastSwipeTime = Time.time;
        }
        else if (gestureName == "SwipeRight")
        {
            Debug.Log("[Bodylink] SwipeRight algılandı - Kamera sağa dönüyor");
            MoveRight();
            lastSwipeTime = Time.time;
        }
    }
}
