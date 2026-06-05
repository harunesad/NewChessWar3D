using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class UISelectedVisualizer : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale = Vector3.one;
    private float animationDuration = 0.15f;
    private float selectedScaleMultiplier = 1.08f;
    private bool isInitialized = false;

    void Awake()
    {
        InitializeScale();
    }

    void Start()
    {
        InitializeScale();
    }

    void OnEnable()
    {
        InitializeScale();
        transform.localScale = originalScale;
    }

    private void InitializeScale()
    {
        if (isInitialized) return;
        originalScale = transform.localScale;
        if (originalScale == Vector3.zero)
        {
            originalScale = Vector3.one;
        }
        isInitialized = true;
    }

    public void OnSelect(BaseEventData eventData)
    {
        InitializeScale();
        AnimateScale(originalScale * selectedScaleMultiplier);
        ScrollToSelected(gameObject);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        InitializeScale();
        AnimateScale(originalScale);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        BodylinkUIInteractor interactor = FindAnyObjectByType<BodylinkUIInteractor>();
        if (interactor != null && interactor.IsControllerActive())
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        BodylinkUIInteractor interactor = FindAnyObjectByType<BodylinkUIInteractor>();
        if (interactor != null && interactor.IsControllerActive())
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void AnimateScale(Vector3 targetScale)
    {
        transform.DOKill();
        transform.DOScale(targetScale, animationDuration).SetUpdate(true); // Works even when Time.timeScale is 0
    }

    private void ScrollToSelected(GameObject selectedObj)
    {
        ScrollRect scrollRect = selectedObj.GetComponentInParent<ScrollRect>();
        if (scrollRect == null) return;

        RectTransform scrollTransform = scrollRect.transform as RectTransform;
        RectTransform viewportTransform = scrollRect.viewport != null ? scrollRect.viewport : scrollTransform;
        RectTransform contentTransform = scrollRect.content;
        RectTransform targetTransform = selectedObj.transform as RectTransform;

        if (scrollTransform == null || contentTransform == null || targetTransform == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform);

        float padding = 60f; // Safe margin from viewport edges

        // Convert target position to scroll viewport local space
        Vector3 targetPosInViewport = viewportTransform.InverseTransformPoint(targetTransform.position);
        float itemHeight = targetTransform.rect.height;
        float itemWidth = targetTransform.rect.width;

        float viewportHeight = viewportTransform.rect.height;
        float viewportWidth = viewportTransform.rect.width;

        // Viewport bounds in local space
        float viewportMinY = -viewportHeight / 2f;
        float viewportMaxY = viewportHeight / 2f;
        float viewportMinX = -viewportWidth / 2f;
        float viewportMaxX = viewportWidth / 2f;

        // Item bounds in viewport space
        float itemMinY = targetPosInViewport.y - (itemHeight * targetTransform.pivot.y);
        float itemMaxY = targetPosInViewport.y + (itemHeight * (1f - targetTransform.pivot.y));
        float itemMinX = targetPosInViewport.x - (itemWidth * targetTransform.pivot.x);
        float itemMaxX = targetPosInViewport.x + (itemWidth * (1f - targetTransform.pivot.x));

        // Vertical Scroll adjustment
        if (contentTransform.rect.height > viewportHeight && scrollRect.vertical)
        {
            float scrollOffset = 0f;
            if (itemMinY < viewportMinY + padding)
            {
                // Item is too low or going off bottom, scroll down to bring it up
                scrollOffset = itemMinY - (viewportMinY + padding);
            }
            else if (itemMaxY > viewportMaxY - padding)
            {
                // Item is too high or going off top, scroll up to bring it down
                scrollOffset = itemMaxY - (viewportMaxY - padding);
            }

            if (Mathf.Abs(scrollOffset) > 0.01f)
            {
                float contentHeight = contentTransform.rect.height;
                float maxScroll = contentHeight - viewportHeight;
                if (maxScroll > 0f)
                {
                    float normDelta = scrollOffset / maxScroll;
                    float newNormPos = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + normDelta);
                    
                    scrollRect.DOKill();
                    DOTween.To(() => scrollRect.verticalNormalizedPosition, x => scrollRect.verticalNormalizedPosition = x, newNormPos, 0.2f).SetUpdate(true);
                }
            }
        }

        // Horizontal Scroll adjustment
        if (contentTransform.rect.width > viewportWidth && scrollRect.horizontal)
        {
            float scrollOffset = 0f;
            if (itemMinX < viewportMinX + padding)
            {
                scrollOffset = itemMinX - (viewportMinX + padding);
            }
            else if (itemMaxX > viewportMaxX - padding)
            {
                scrollOffset = itemMaxX - (viewportMaxX - padding);
            }

            if (Mathf.Abs(scrollOffset) > 0.01f)
            {
                float contentWidth = contentTransform.rect.width;
                float maxScroll = contentWidth - viewportWidth;
                if (maxScroll > 0f)
                {
                    float normDelta = scrollOffset / maxScroll;
                    float newNormPos = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition + normDelta);
                    
                    scrollRect.DOKill();
                    DOTween.To(() => scrollRect.horizontalNormalizedPosition, x => scrollRect.horizontalNormalizedPosition = x, newNormPos, 0.2f).SetUpdate(true);
                }
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeSceneLoadedCallback()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        
        AttachToAllSelectables();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        AttachToAllSelectables();
    }

    public static void AttachToAllSelectables()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            if (root == null) continue;
            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true); // true = include inactive!
            foreach (Selectable selectable in selectables)
            {
                if (selectable != null && selectable.gameObject.GetComponent<UISelectedVisualizer>() == null)
                {
                    selectable.gameObject.AddComponent<UISelectedVisualizer>();
                }
            }
        }
    }
}
