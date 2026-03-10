using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BodylinkSDK;
using Mediapipe.Tasks.Components.Containers;

public class BodylinkUIInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cursorVisual;
    [SerializeField] private EventSystem eventSystem;

    [Header("Settings")]
    [SerializeField] private Side activeHand = Side.Right;
    [SerializeField] private bool useSmoothedPoints = true;
    [SerializeField] private bool mirrorX = true;

    [Header("Mapping & Sensitivity")]
    [Range(1f, 5f)]
    [SerializeField] private float sensitivity = 1.8f; // CursorPointer hissi için biraz artırıldı
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float yOffset = 0.15f; // Üst butonlara erişim için ideal ofset
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float xOffset = 0f;
    [Range(0.01f, 1f)]
    [SerializeField] private float smoothSpeed = 0.15f; // CursorPointer'daki gibi akıcı hareket

    private Bodylink bodylink;
    private Vector2 currentScreenPos;
    private bool isGestureActive = false;
    private PointerEventData pointerData;
    private List<RaycastResult> raycastResults = new List<RaycastResult>();
    private RawImage miniCamRawImage; // Texture senkronizasyonu için referans

    private const float MIN_VISIBILITY = 0.05f;
    
    [Header("Hover Click Settings")]
    [SerializeField] private bool useHoverClick = true;
    [SerializeField] private float hoverDuration = 2.0f;
    [SerializeField] private float hoverMoveThreshold = 50f; // İmleç bu kadar hareket ederse sayaç sıfırlanır
    
    [Header("Scroll (Pinch & Drag) Settings")]
    [SerializeField] private bool usePinchScroll = true;
    [SerializeField] private float pinchThreshold = 0.05f; // Baş ve işaret parmağı mesafesi
    [SerializeField] private float scrollSensitivity = 1.5f; // Kaydırma hızı çarpanı
    
    private float hoverTimer = 0f;
    private Vector2 lastHoverPos;
    private Image cursorImage;

    // Scroll Değişkenleri
    private bool isPinching = false;
    private float lastPinchY = 0f;
    private ScrollRect activeScrollRect;

    void Start()
    {
        bodylink = Bodylink.Instance;
        
        if (eventSystem == null)
            eventSystem = EventSystem.current;

        if (bodylink != null)
        {
            // Olay aboneliğini temizleyip yeniden yap (Çift aboneliği önler)
            bodylink.inputEvents.OnPoseDetection -= HandlePoseDetection;
            bodylink.inputEvents.OnPoseDetection += HandlePoseDetection;

            bodylink.OnInitialized -= InitializeBodylinkComponents;
            bodylink.OnInitialized += InitializeBodylinkComponents;
            
            if (bodylink.IsInitialized)
            {
                InitializeBodylinkComponents();
                
                // Menüye dönüşte akışı zorla canlandır
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
        
        // Mini-Cam oluşturmayı başlat
        StopCoroutine("CreateMiniCamRoutine");
        StartCoroutine("CreateMiniCamRoutine");
    }

    private void HideBodylinkVisuals()
    {
        if (bodylink == null || bodylink.PoseLandmarkerRunnerInstance == null) return;

        // 1) SDK'nın oluşturduğu tüm Canvas bileşenlerini bul ve devre dışı bırak
        // Bu, silüet, maske ve UI panellerini tamamen gizler ama scriptlerin çalışmasını bozmaz.
        var canvases = bodylink.PoseLandmarkerRunnerInstance.GetComponentsInChildren<Canvas>(true);
        foreach (var canvas in canvases)
        {
            canvas.enabled = false;
        }

        // 2) Diğer yardımcı görselleştiricileri de pasif yap
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

        Debug.Log("Bodylink: SDK görselleri (Canvas/Silüet/Maske) tamamen gizlendi.");
    }

    private IEnumerator CreateMiniCamRoutine()
    {
        // Bodylink ve CameraScreen hazır olana kadar bekle
        while (bodylink == null || bodylink.cameraScreen == null)
            yield return null;

        // Texture hazır olana kadar bekle (İlk açılışta donanımın ısınması zaman alabilir)
        // Sabit bir timeout yerine, akış gelene kadar bekleyelim 
        // Ama SDK'yı da hafifçe dürtelim
        int retryCount = 0;
        while (bodylink.cameraScreen.texture == null)
        {
            retryCount++;
            if (retryCount % 100 == 0) // Periyodik olarak akışı tazele
            {
                bodylink.DisplayCameraFeed(true);
                if (bodylink.imageSource != null) StartCoroutine(bodylink.imageSource.Resume());
            }
            yield return null;
        }

        // 1) Sahnedeki mevcut ana Canvas'ı bul
        Canvas mainCanvas = FindAnyObjectByType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogWarning("Bodylink UI: Sahnede Canvas bulunamadı.");
            yield break;
        }

        // Varsa eski Mini-Cam'i temizle
        GameObject oldCam = GameObject.Find("Bodylink_MiniCam");
        if (oldCam != null) Destroy(oldCam);
        yield return new WaitForEndOfFrame(); // Yok olma işlemini bekle

            // 2) RawImage (Görüntü) oluştur ve mevcut Canvas'a bağla
        GameObject rawImageObj = new GameObject("Bodylink_MiniCam");
        rawImageObj.transform.SetParent(mainCanvas.transform, false);
        miniCamRawImage = rawImageObj.AddComponent<UnityEngine.UI.RawImage>();

        // 3) Konumlandırma (Sağ Alt Köşe)
        RectTransform rect = miniCamRawImage.rectTransform;
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-250, 20); 
        rect.sizeDelta = new Vector2(240, 135); 

        // 4) Şeffaflık ve Aynalama
        miniCamRawImage.color = new Color(1, 1, 1, 0.4f); 
        rect.localScale = new Vector3(-1, 1, 1); 

        Debug.Log("Bodylink UI: Mini-Cam objesi oluşturuldu, senkronizasyon Update'de sürecek.");
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

        // "Victory" (Zafer İşareti - İki parmak) jestini tıklama olarak kabul et
        if (pose == HandPose.Victory)
        {
            if (!isGestureActive)
            {
                isGestureActive = true;
                TryClickUI();
            }
        }
        else if (pose == HandPose.Open_Palm || pose == HandPose.None || pose == HandPose.Pointing_Up)
        {
            isGestureActive = false;
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
    }

    private void UpdateCursorPosition()
    {
        // El (Hand) Landmarkları veya Vücut (Pose) Landmarkları ile pozisyon hesapla
        var player = bodylink.players[0];
        float x = 0, y = 0;
        bool found = false;

        var hand = (activeHand == Side.Left) ? player.handPoints[0] : player.handPoints[1];
        if (hand != null && hand.handLandmark != null && hand.handLandmark.Count > 8)
        {
            var point = hand.handLandmark[8];
            x = point.x;
            y = 1f - point.y;
            found = true;
        }
        else
        {
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

        // HOVER CLICK LOGIC
        if (useHoverClick && !isGestureActive && !isPinching)
        {
            HandleHoverLogic();
        }

        // SCROLL (PINCH & DRAG) LOGIC
        if (usePinchScroll)
        {
            HandleScrollLogic(player);
        }
    }

    private void HandleScrollLogic(BodylinkPlayerAvatar player)
    {
        var hand = (activeHand == Side.Left) ? player.handPoints[0] : player.handPoints[1];
        if (hand == null || hand.handLandmark == null || hand.handLandmark.Count < 9)
        {
            ReleaseScroll();
            return;
        }

        var thumbTip = hand.handLandmark[4];
        var indexTip = hand.handLandmark[8];

        float distance = Vector2.Distance(new Vector2(thumbTip.x, thumbTip.y), new Vector2(indexTip.x, indexTip.y));

        if (distance < pinchThreshold)
        {
            // PINCH BAŞLADI / DEVAM EDİYOR
            if (!isPinching)
            {
                // İlk tutuşta ScrollRect ara
                activeScrollRect = FindScrollRectUnderPointer();
                if (activeScrollRect != null)
                {
                    isPinching = true;
                    lastPinchY = currentScreenPos.y;
                    
                    // Görsel geri bildirim
                    if (cursorImage != null) cursorImage.color = Color.cyan;
                    cursorVisual.transform.localScale = Vector3.one * 0.8f;
                }
            }
            else if (activeScrollRect != null && activeScrollRect.gameObject.activeInHierarchy)
            {
                // Kaydırma (Drag) işlemi
                float deltaY = currentScreenPos.y - lastPinchY;
                
                // Screen deltaY'yi ScrollRect'in content yapısına uyarla
                // Bu basit bir oranlamadır, gerekirse geliştirilebilir
                float scrollAmount = (deltaY / Screen.height) * scrollSensitivity;
                
                // ScrollRect'i hareket ettir (sadece dikey)
                if (activeScrollRect.vertical)
                {
                    float newPos = activeScrollRect.verticalNormalizedPosition + scrollAmount;
                    activeScrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
                }

                lastPinchY = currentScreenPos.y; // Güncelle
            }
        }
        else
        {
            ReleaseScroll();
        }
    }

    private ScrollRect FindScrollRectUnderPointer()
    {
        if (eventSystem == null) return null;

        pointerData.position = currentScreenPos;
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);

        foreach (var result in raycastResults)
        {
            GameObject target = result.gameObject;
            while (target != null)
            {
                ScrollRect scroll = target.GetComponent<ScrollRect>();
                if (scroll != null) return scroll;

                if (target.transform.parent == null) break;
                target = target.transform.parent.gameObject;
            }
        }
        return null;
    }

    private void ReleaseScroll()
    {
        if (isPinching)
        {
            isPinching = false;
            activeScrollRect = null;
            ResetHover(); // Rengi ve boyutu eski haline getirir
        }
    }

    private void HandleHoverLogic()
    {
        // 1) Önce imlecin altında etkileşimli bir şey var mı kontrol et
        bool isOverInteractable = CheckIfPointerOverUI();

        if (!isOverInteractable)
        {
            ResetHover();
            return;
        }

        // 2) İmleç hareket etti mi kontrol et (Hassasiyeti artırmak için threshold'u biraz düşürebiliriz)
        if (Vector2.Distance(currentScreenPos, lastHoverPos) > hoverMoveThreshold)
        {
            lastHoverPos = currentScreenPos;
            hoverTimer = 0f; // Sadece sayacı sıfırla, görseli ResetHover ile tamamen temizleme (titremeyi önlemek için)
        }
        else
        {
            // Sabit duruyoruz ve nesne üzerindeyiz
            hoverTimer += Time.unscaledDeltaTime;
            
            if (cursorImage != null)
            {
                cursorImage.color = Color.Lerp(Color.white, Color.green, hoverTimer / hoverDuration);
                cursorVisual.transform.localScale = Vector3.one * (1f + (hoverTimer / hoverDuration) * 0.5f);
            }

            if (hoverTimer >= hoverDuration)
            {
                Debug.Log("Bodylink UI: Akıllı Hover süresi doldu, tıklanıyor...");
                TryClickUI();
                ResetHover();
            }
        }
    }

    private bool CheckIfPointerOverUI()
    {
        if (eventSystem == null) return false;

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

    private void TryClickUI()
    {
        if (eventSystem == null) return;

        pointerData.position = currentScreenPos;
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);

        if (raycastResults.Count > 0)
        {
            foreach (var result in raycastResults)
            {
                // Buton veya tıklanabilir bir nesne bulana kadar hiyerarşide yukarı çık
                GameObject target = result.gameObject;
                while (target != null)
                {
                    var button = target.GetComponent<Button>();
                    if (button != null && button.interactable)
                    {
                        Debug.Log($"Bodylink UI: Butona tıklandı: {button.name}");
                        button.onClick.Invoke();
                        return;
                    }
                    
                    var pointerHandler = target.GetComponent<IPointerClickHandler>();
                    if (pointerHandler != null)
                    {
                        Debug.Log($"Bodylink UI: Tıklanabilir nesne tetiklendi: {target.name}");
                        pointerHandler.OnPointerClick(pointerData);
                        return;
                    }

                    if (target.transform.parent == null) break;
                    target = target.transform.parent.gameObject;
                }
            }
        }
    }
}
