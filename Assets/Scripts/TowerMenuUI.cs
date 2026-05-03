using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

public class TowerMenuUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("UI References")]
    public GameObject levelCardPrefab;
    public ScrollRect scrollRect;
    public RectTransform content;
    public List<Sprite> themeSprites = new List<Sprite>(); // 10 adet tema görseli buraya gelecek
    
    [Header("Fixed Controls")]
    public Button mainPlayButton;
    public TextMeshProUGUI mainPlayButtonText;

    [Header("Manual Layout Settings")]
    public float cardPosX = 600f;
    public float cardHeight = 400f;
    public float spacing = 100f;
    public float snapSpeed = 0.3f; // Biraz daha hızlandırdık
    
    // NOT: Swipe'ın her yerde çalışması için bu scriptin olduğu objede 
    // Tüm ekranı kaplayan şeffaf bir IMAGE (Raycast Target: ON) olmalıdır.
    private float swipeThreshold; 
    private List<RectTransform> spawnedCards = new List<RectTransform>();
    private bool isSnapping = false;
    private int currentCenteredIndex = 0;
    private Vector2 pointerDownPosition;

    void Awake()
    {
        if (mainPlayButton != null)
            mainPlayButton.onClick.AddListener(OnMainPlayClicked);
            
        if (scrollRect != null)
        {
            scrollRect.inertia = false;
            scrollRect.vertical = false;
        }

        // Ekranın %5'ini eşik yapıyoruz (Örn: 1080p ekranda ~50px)
        swipeThreshold = Screen.height * 0.05f;
    }

    public void UpdateUI()
    {
        TowerManager tm = TowerManager.Instance;
        if (tm == null) return;

        foreach (var card in spawnedCards) 
        {
            if(card != null) Destroy(card.gameObject);
        }
        spawnedCards.Clear();

        int reachedLevel = tm.GetReachedLevel();
        float totalItemStep = cardHeight + spacing;

        for (int i = 0; i < tm.levels.Count; i++)
        {
            TowerLevelData data = tm.levels[i];
            GameObject cardObj = Instantiate(levelCardPrefab, content);
            RectTransform cardRT = cardObj.GetComponent<RectTransform>();
            spawnedCards.Add(cardRT);

            cardRT.anchoredPosition = new Vector2(cardPosX, i * totalItemStep);
            SetupCardVisuals(cardObj, data, reachedLevel);
        }

        content.sizeDelta = new Vector2(content.sizeDelta.x, tm.levels.Count * totalItemStep);
        Canvas.ForceUpdateCanvases();
        
        int focusIdx = Mathf.Clamp(reachedLevel - 1, 0, tm.levels.Count - 1);
        SnapToIndex(focusIdx, true);
    }

    private void SetupCardVisuals(GameObject cardObj, TowerLevelData data, int reachedLevel)
    {
        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach(var t in texts)
        {
            if(t.name.Contains("Name") || t.name.Contains("Number")) t.text = "LEVEL " + data.levelNumber;
            if(t.name.Contains("Description")) t.text = data.levelDescription;
            if(t.name.Contains("Reward")) t.text = "Reward: " + data.coinReward + " Coins";
        }

        Image[] images = cardObj.GetComponentsInChildren<Image>();
        foreach(var img in images)
        {
            if(img.name.Contains("Preview") || img.name == "Image")
            {
                // Tema indeksini hesapla (1-10 -> 0, 11-20 -> 1...)
                int themeIndex = (data.levelNumber - 1) / 10;
                if (themeSprites != null && themeIndex >= 0 && themeIndex < themeSprites.Count)
                {
                    img.sprite = themeSprites[themeIndex];
                }
            }
        }

        CanvasGroup cg = cardObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
        cg.alpha = (data.levelNumber <= reachedLevel) ? 1.0f : 0.4f;
        
        // KRİTİK DÜZELTME: Kartlar tıklamayı engellemesin ki arka plan her zaman swipe'ı algılasın
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        content.DOKill();
        isSnapping = false;
        pointerDownPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapping) return;

        float totalItemStep = cardHeight + spacing;
        float baseSnapY = -currentCenteredIndex * totalItemStep;
        
        // Sürüklemeyi 1 level mesafesiyle kısıtla (Örn: %60 esneme payı)
        float newY = content.anchoredPosition.y + eventData.delta.y;
        float clampedY = Mathf.Clamp(newY, baseSnapY - totalItemStep * 0.6f, baseSnapY + totalItemStep * 0.6f);
        
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, clampedY);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        float dragDistance = eventData.position.y - pointerDownPosition.y;

        if (Mathf.Abs(dragDistance) > swipeThreshold)
        {
            if (dragDistance < 0) // Swipe Down -> Yukarı çık (Index +1)
            {
                int nextIdx = Mathf.Clamp(currentCenteredIndex + 1, 0, spawnedCards.Count - 1);
                SnapToIndex(nextIdx);
            }
            else // Swipe Up -> Aşağı in (Index -1)
            {
                int prevIdx = Mathf.Clamp(currentCenteredIndex - 1, 0, spawnedCards.Count - 1);
                SnapToIndex(prevIdx);
            }
        }
        else
        {
            SnapToIndex(currentCenteredIndex);
        }
    }

    private void SnapToIndex(int index, bool immediate = false)
    {
        if (spawnedCards.Count == 0 || index < 0 || index >= spawnedCards.Count) return;

        float totalItemStep = cardHeight + spacing;
        float targetY = -index * totalItemStep;
        currentCenteredIndex = index;

        if (immediate)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
            UpdateMainButtonStatus(index);
        }
        else
        {
            isSnapping = true;
            content.DOAnchorPosY(targetY, snapSpeed).SetEase(Ease.OutQuint).OnComplete(() => {
                isSnapping = false;
                UpdateMainButtonStatus(index);
            });
        }
    }

    private void UpdateMainButtonStatus(int index)
    {
        if (mainPlayButton == null) return;
        TowerManager tm = TowerManager.Instance;
        if (tm == null || index < 0 || index >= tm.levels.Count) return;

        TowerLevelData data = tm.GetLevelData(index);
        int reachedLevel = tm.GetReachedLevel();
        bool isUnlocked = (data.levelNumber <= reachedLevel);

        mainPlayButton.interactable = isUnlocked;
        if (mainPlayButtonText != null)
            mainPlayButtonText.text = isUnlocked ? "PLAY LEVEL " + data.levelNumber : "LOCKED";
    }

    private void OnMainPlayClicked()
    {
        if (isSnapping || currentCenteredIndex < 0) return;
        TowerManager.Instance.StartTowerLevel(currentCenteredIndex);
    }
}
