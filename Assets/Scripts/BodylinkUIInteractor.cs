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
    [SerializeField] private float smoothFactor = 0.15f; // EMA smoothing factor (0.01 = very smooth, 1 = raw)

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
    [SerializeField] private float pinchThreshold = 0.065f; // Tutma eşiği
    [SerializeField] private float releaseThreshold = 0.09f; // Bırakma eşiği (Hysteresis)
    [SerializeField] private float scrollSensitivity = 1.5f; 
    
    private float hoverTimer = 0f;
    private Vector2 lastHoverPos;
    private Image cursorImage;

    // Scroll Değişkenleri
    private bool isPinching = false;
    private float lastPinchY = 0f;
    private ScrollRect activeScrollRect;
    private Slider activeSlider;

    // Hybrid Controller/Keyboard Navigation variables
    private Vector2 lastCursorScreenPos;
    private bool isUsingController = false;
    private float lastControllerInputTime = -10f;
    private float controllerLockoutDuration = 1.5f;

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
        
        // İlk açılışta görüntüyü garantilemek için feed'i aktif et
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
        
        // Sadece sağ ele (imleci kontrol eden ele) bak
        if (side != Side.Right) return;

        // "Fist" (Yumruk) veya "Victory" (İki parmak) jestini tıklama olarak kabul et
        if (pose == HandPose.Closed_Fist || pose == HandPose.Victory)
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

    private GameObject FindSceneDefaultSelectedButton()
    {
        var menuManager = FindAnyObjectByType<MenuUIManager>();
        if (menuManager != null)
        {
            return menuManager.GetDefaultSelectedButton();
        }

        var gameManager = FindAnyObjectByType<GameUIManager>();
        if (gameManager != null)
        {
            return gameManager.GetDefaultSelectedButton();
        }

        return null;
    }

    void Update()
    {
        eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        // Detect gamepad / keyboard arrow keys or stick movement
        bool hasControllerInput = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.2f ||
                                  Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.2f ||
                                  Input.GetButtonDown("Submit") ||
                                  Input.GetButtonDown("Cancel") ||
                                  Input.GetKeyDown(KeyCode.UpArrow) ||
                                  Input.GetKeyDown(KeyCode.DownArrow) ||
                                  Input.GetKeyDown(KeyCode.LeftArrow) ||
                                  Input.GetKeyDown(KeyCode.RightArrow) ||
                                  Input.GetKeyDown(KeyCode.Return) ||
                                  Input.GetKeyDown(KeyCode.Space);

        if (hasControllerInput)
        {
            lastControllerInputTime = Time.unscaledTime;
            bool selectionRestored = false;

            if (!isUsingController)
            {
                isUsingController = true;
                if (cursorVisual != null && cursorVisual.activeSelf)
                {
                    cursorVisual.SetActive(false);
                }

                GameObject defaultBtn = FindSceneDefaultSelectedButton();
                if (defaultBtn != null && eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(defaultBtn);
                    selectionRestored = true;
                }
            }

            if (!selectionRestored && eventSystem != null && eventSystem.currentSelectedGameObject == null)
            {
                GameObject defaultBtn = FindSceneDefaultSelectedButton();
                if (defaultBtn != null)
                {
                    eventSystem.SetSelectedGameObject(defaultBtn);
                }
            }
        }

        // Direct D-pad/Remote Scroll support
        if (isUsingController)
        {
            float verticalInput = Input.GetAxisRaw("Vertical");
            float horizontalInput = Input.GetAxisRaw("Horizontal");

            // KeyCode fallbacks in case axes are unconfigured
            if (Input.GetKey(KeyCode.UpArrow)) verticalInput = 1f;
            else if (Input.GetKey(KeyCode.DownArrow)) verticalInput = -1f;

            if (Input.GetKey(KeyCode.RightArrow)) horizontalInput = 1f;
            else if (Input.GetKey(KeyCode.LeftArrow)) horizontalInput = -1f;

            if (Mathf.Abs(verticalInput) > 0.1f || Mathf.Abs(horizontalInput) > 0.1f)
            {
                ScrollRect activeScroll = FindActiveScrollRect();
                if (activeScroll != null)
                {
                    if (activeScroll.vertical && Mathf.Abs(verticalInput) > 0.1f)
                    {
                        float scrollAmount = verticalInput * Time.unscaledDeltaTime * 1.5f;
                        activeScroll.verticalNormalizedPosition = Mathf.Clamp01(activeScroll.verticalNormalizedPosition + scrollAmount);
                    }
                    if (activeScroll.horizontal && Mathf.Abs(horizontalInput) > 0.1f)
                    {
                        float scrollAmount = horizontalInput * Time.unscaledDeltaTime * 1.5f;
                        activeScroll.horizontalNormalizedPosition = Mathf.Clamp01(activeScroll.horizontalNormalizedPosition + scrollAmount);
                    }
                }
            }
        }

        UpdateCursorPosition();
    }

    private ScrollRect FindActiveScrollRect()
    {
        ScrollRect[] scrolls = FindObjectsByType<ScrollRect>(FindObjectsSortMode.None);
        foreach (var scroll in scrolls)
        {
            if (scroll.gameObject.activeInHierarchy)
            {
                CanvasGroup cg = scroll.GetComponentInParent<CanvasGroup>();
                if (cg == null || (cg.gameObject.activeInHierarchy && cg.alpha > 0.1f))
                {
                    return scroll;
                }
            }
        }
        return null;
    }

    private void UpdateCursorPosition()
    {
        if (bodylink.players == null || bodylink.players.Length == 0) return;
        
        var player = bodylink.players[0];
        float x = 0, y = 0;
        bool found = false;

        // WRIST TRACKING: Bilek takibi çok daha stabildir (Özellikle TV mesafesinde)
        NormalizedLandmark wrist = useSmoothedPoints ? player.body2DSmoothed.rightWrist : player.body2D.rightWrist;

        if (wrist.visibility >= MIN_VISIBILITY)
        {
            x = wrist.x;
            y = wrist.y;
            found = true;
        }

        if (!found) 
        {
            ResetHover();
            return;
        }

        if (mirrorX) x = 1f - x;

        // Hassasiyet ve Ofset Ayarları
        x = (x - 0.5f - xOffset) * sensitivity + 0.5f;
        y = (y - 0.5f - yOffset) * sensitivity + 0.5f;

        x = Mathf.Clamp01(x);
        y = Mathf.Clamp01(y);

        Vector2 targetScreenPos = new Vector2(x * Screen.width, y * Screen.height);
        
        if (isUsingController)
        {
            // Lock out hand movement checks if gamepad button was pressed very recently (to prevent hand noise from immediately reclaiming focus)
            if (Time.unscaledTime - lastControllerInputTime < controllerLockoutDuration)
            {
                lastCursorScreenPos = targetScreenPos;
                ResetHover();
                return;
            }

            float handMovement = Vector2.Distance(targetScreenPos, lastCursorScreenPos);
            // If hand moves significantly (e.g. more than 20 pixels), restore hand control
            if (handMovement > 20f)
            {
                isUsingController = false;
                if (cursorVisual != null && !cursorVisual.activeSelf)
                {
                    cursorVisual.SetActive(true);
                }
                // Deselect current button from EventSystem to hand focus back to cursor hover
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }
        lastCursorScreenPos = targetScreenPos;

        // Pürüzsüzleştirme (Smoothing)
        currentScreenPos = Vector2.Lerp(currentScreenPos, targetScreenPos, smoothFactor);

        if (cursorVisual != null && cursorVisual.activeSelf)
        {
            cursorVisual.transform.position = currentScreenPos;
        }

        if (!isUsingController)
        {
            // Hover click is retained as a helpful pointer fallback
            if (useHoverClick && !isGestureActive)
            {
                HandleHoverLogic();
            }
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
        var wrist = hand.handLandmark[0];
        var middleKnuckle = hand.handLandmark[9];

        // Elin o anki görsel boyutu (Normalizasyon için)
        float handSize = Vector3.Distance(
            new Vector3(wrist.x, wrist.y, wrist.z),
            new Vector3(middleKnuckle.x, middleKnuckle.y, middleKnuckle.z)
        );
        if (handSize < 0.001f) handSize = 0.1f; // Güvenlik

        // Parmak mesafesi (Elin kendi boyutuna oranla)
        // Artık kameraya uzaklıktan bağımsızdır!
        float rawDist = Vector3.Distance(
            new Vector3(thumbTip.x, thumbTip.y, thumbTip.z), 
            new Vector3(indexTip.x, indexTip.y, indexTip.z)
        );
        float distance = rawDist / handSize;

        if (!isPinching)
        {
            // Eşikler artık el boyutuna oranlıdır (Örn: 0.3 = El boyunun %30'u kadar yakın)
            if (distance < pinchThreshold * 6f) 
            {
                // PINCH BAŞLADI
                activeScrollRect = FindScrollRectUnderPointer();
                if (activeScrollRect != null)
                {
                    isPinching = true;
                    lastPinchY = currentScreenPos.y;
                    
                    if (cursorImage != null) cursorImage.color = Color.cyan;
                    cursorVisual.transform.localScale = Vector3.one * 0.8f;
                }
            }
        }
        else
        {
            // PINCH DEVAM EDİYOR
            if (distance > releaseThreshold * 6f)
            {
                ReleaseScroll();
            }
            else if (activeScrollRect != null && activeScrollRect.gameObject.activeInHierarchy)
            {
                // Kaydırma (Drag) işlemi
                float deltaY = currentScreenPos.y - lastPinchY;
                float scrollAmount = (deltaY / Screen.height) * scrollSensitivity;
                
                if (activeScrollRect.vertical)
                {
                    float newPos = activeScrollRect.verticalNormalizedPosition + scrollAmount;
                    activeScrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
                }

                lastPinchY = currentScreenPos.y; 
            }
        }
    }

    private void HandleScrollMovement() // Yardımcı metod (yapısal temizlik için eklendi sayılır ama mevcut akışta kalsın)
    {
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
                    if (target.GetComponent<Button>() != null || target.GetComponent<Slider>() != null)
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

    private Slider FindSliderUnderPointer()
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
                Slider slider = target.GetComponent<Slider>();
                if (slider != null && slider.interactable) return slider;

                if (target.transform.parent == null) break;
                target = target.transform.parent.gameObject;
            }
        }
        return null;
    }

    private void UpdateSliderValue()
    {
        if (activeSlider == null) return;

        RectTransform rectTransform = activeSlider.transform as RectTransform;
        if (rectTransform != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, currentScreenPos, null, out Vector2 localPoint))
            {
                float width = rectTransform.rect.width;
                if (width > 0f)
                {
                    float normalizedX = (localPoint.x - rectTransform.rect.xMin) / width;
                    normalizedX = Mathf.Clamp01(normalizedX);

                    float value = activeSlider.minValue + normalizedX * (activeSlider.maxValue - activeSlider.minValue);
                    activeSlider.value = value;
                }
            }
        }
    }

    public bool IsControllerActive()
    {
        return isUsingController;
    }
}
