using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

public class TowerMenuUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject levelCardPrefab;
    public RectTransform contentPanel; // Butonların spawn olduğu dikey panel
    public List<Sprite> themeSprites = new List<Sprite>(); // 10 adet tema görseli
    
    [HideInInspector]
    public List<RectTransform> spawnedCards = new List<RectTransform>();

    [Header("Layout & Animation")]
    public float cardHeight = 350f;
    public float spacing = 150f;
    public float transitionDuration = 0.25f;

    private int currentCenteredIndex = -1;
    private Navigation savedOriginalBackNav;
    private bool hasSavedOriginalBackNav = false;
    private Navigation savedOriginalPlayNav;
    private bool hasSavedOriginalPlayNav = false;

    void Awake()
    {
        // Basılı tutma tekrarlamasını devre dışı bırak
        var inputModule = EventSystem.current?.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (inputModule != null)
        {
            inputModule.inputActionsPerSecond = 1; // Saniyede max 1 hareket
            inputModule.repeatDelay = 999f; // Tekrarlama yok
        }
    }

    void OnDisable()
    {
        // Geri (Back) butonunun orijinal navigasyonunu geri yükle
        MenuUIManager menuUI = FindFirstObjectByType<MenuUIManager>(FindObjectsInactive.Include);
        if (menuUI != null && menuUI.BackButton != null && hasSavedOriginalBackNav)
        {
            menuUI.BackButton.navigation = savedOriginalBackNav;
            Debug.Log("[TowerMenuUI] Back butonu navigasyonu orijinal haline geri yüklendi.");
        }

        // Play butonunun orijinal navigasyonunu geri yükle
        if (contentPanel != null && contentPanel.parent != null && hasSavedOriginalPlayNav)
        {
            foreach (var b in contentPanel.parent.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "Play")
                {
                    b.navigation = savedOriginalPlayNav;
                    Debug.Log("[TowerMenuUI] Play butonu navigasyonu orijinal haline geri yüklendi.");
                    break;
                }
            }
        }
    }

    public void UpdateUI()
    {
        TowerManager tm = TowerManager.Instance;
        if (tm == null) return;

        // Eski kartları temizle
        foreach (var card in spawnedCards) 
        {
            if (card != null) Destroy(card.gameObject);
        }
        spawnedCards.Clear();

        int reachedLevel = tm.GetReachedLevel();
        float step = cardHeight + spacing;

        // Content panelin anchor ve pivot ayarlarını absolute Y koordinatı için yukarı-esne (Top-Stretch) olarak zorla set et
        if (contentPanel != null)
        {
            contentPanel.anchorMin = new Vector2(0f, 1f);
            contentPanel.anchorMax = new Vector2(1f, 1f);
            contentPanel.pivot = new Vector2(0.5f, 1f);
            contentPanel.anchoredPosition = new Vector2(0f, 0f);
        }

        // Kartları dikey olarak sıralı oluştur
        for (int i = 0; i < tm.levels.Count; i++)
        {
            TowerLevelData data = tm.levels[i];
            GameObject cardObj = Instantiate(levelCardPrefab, contentPanel);
            RectTransform cardRT = cardObj.GetComponent<RectTransform>();
            spawnedCards.Add(cardRT);

            // Kartın kendi anchor ayarlarını en üstten dizilmesi için Top-Center (Y=1) olarak zorla
            cardRT.anchorMin = new Vector2(0.5f, 1f);
            cardRT.anchorMax = new Vector2(0.5f, 1f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);

            // Pozisyonlarını dikey olarak sırayla ata (X konumu 0f, Y başlangıcı -650f)
            cardRT.anchoredPosition = new Vector2(0f, -675f - i * step);
            SetupCardVisuals(cardObj, data, reachedLevel, i);
        }

        // İkinci geçiş: Tüm kartlar spawn edildikten sonra navigation bağlantılarını kur
        SetupAllNavigation(reachedLevel);

        // Content panelinin toplam boyutu (Başlangıç ofseti dahil)
        if (contentPanel != null)
        {
            contentPanel.sizeDelta = new Vector2(contentPanel.sizeDelta.x, 650f + tm.levels.Count * step);
        }

        Canvas.ForceUpdateCanvases();
        
        // Son ulaşılan seviyeye otomatik odaklan
        int focusIdx = Mathf.Clamp(reachedLevel - 1, 0, tm.levels.Count - 1);
        FocusOnIndex(focusIdx, true);
    }

    private void SetupCardVisuals(GameObject cardObj, TowerLevelData data, int reachedLevel, int index)
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
                int themeIndex = (data.levelNumber - 1) / 10;
                if (themeSprites != null && themeIndex >= 0 && themeIndex < themeSprites.Count)
                {
                    img.sprite = themeSprites[themeIndex];
                }
            }
        }

        bool isUnlocked = (data.levelNumber <= reachedLevel);
        CanvasGroup cg = cardObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
        cg.alpha = isUnlocked ? 1.0f : 0.4f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        // Selectable veya Button bileşenini yönet
        Selectable selectable = cardObj.GetComponent<Selectable>();
        if (selectable == null) selectable = cardObj.GetComponentInChildren<Selectable>();

        if (selectable != null)
        {
            selectable.interactable = isUnlocked;

            // Eğer nesne doğrudan Button ise tıklama olayını ata
            Button btn = selectable as Button;
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    if (isUnlocked)
                    {
                        TowerManager.Instance.StartTowerLevel(index);
                    }
                });
            }

            // Yön tuşlarıyla odaklandığında (OnSelect) paneli o karta doğru kaydır
            EventTrigger trigger = selectable.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = selectable.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            EventTrigger.Entry selectEntry = new EventTrigger.Entry();
            selectEntry.eventID = EventTriggerType.Select;
            selectEntry.callback.AddListener((eventData) => {
                FocusOnIndex(index);
            });
            trigger.triggers.Add(selectEntry);
        }
    }

    /// <summary>
    /// Tüm kartlar spawn edildikten sonra navigation bağlantılarını kurar.
    /// </summary>
    private void SetupAllNavigation(int reachedLevel)
    {
        // Back butonunu bul
        Button backBtn = null;
        MenuUIManager menuUI = FindFirstObjectByType<MenuUIManager>(FindObjectsInactive.Include);
        if (menuUI != null)
        {
            backBtn = menuUI.BackButton;
        }

        // Play butonunu TowerPanelBg (contentPanel.parent) altından bul
        Button playBtn = null;
        if (contentPanel != null && contentPanel.parent != null)
        {
            foreach (var b in contentPanel.parent.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "Play")
                {
                    playBtn = b;
                    break;
                }
            }
        }

        // İlk kez açıldığında orijinal navigasyonları yedekle
        if (backBtn != null && !hasSavedOriginalBackNav)
        {
            savedOriginalBackNav = backBtn.navigation;
            hasSavedOriginalBackNav = true;
        }
        if (playBtn != null && !hasSavedOriginalPlayNav)
        {
            savedOriginalPlayNav = playBtn.navigation;
            hasSavedOriginalPlayNav = true;
        }

        Debug.Log($"[TowerMenuUI] SetupAllNavigation başladı. Kart sayısı: {spawnedCards.Count}, Back buton: {backBtn != null}, Play buton: {playBtn != null}");

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            Selectable selectable = spawnedCards[i].GetComponent<Selectable>()
                ?? spawnedCards[i].GetComponentInChildren<Selectable>();
            if (selectable == null) continue;

            Navigation nav = selectable.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = backBtn;
            nav.selectOnRight = playBtn; // Sağ yönü Play butonuna bağla

            // Yukarı = önceki kart (daha küçük index)
            if (i > 0)
            {
                nav.selectOnUp = spawnedCards[i - 1].GetComponent<Selectable>()
                    ?? spawnedCards[i - 1].GetComponentInChildren<Selectable>();
            }
            else
            {
                nav.selectOnUp = null; // Level 1'in üstünde bir şey yok
            }

            // Aşağı = sonraki kart (daha büyük index)
            if (i < spawnedCards.Count - 1)
            {
                nav.selectOnDown = spawnedCards[i + 1].GetComponent<Selectable>()
                    ?? spawnedCards[i + 1].GetComponentInChildren<Selectable>();
            }
            else
            {
                nav.selectOnDown = null; // Son level'ın altında bir şey yok
            }

            selectable.navigation = nav;

            Debug.Log($"[TowerMenuUI] Kart {i} navigasyon: Up={nav.selectOnUp?.name}, Down={nav.selectOnDown?.name}, Left={nav.selectOnLeft?.name}, Right={nav.selectOnRight?.name}");
        }

        // Back butonunun yön tuşu bağlantılarını dinamik olarak kur
        if (backBtn != null && spawnedCards.Count > 0)
        {
            Selectable firstCard = spawnedCards[0].GetComponent<Selectable>()
                ?? spawnedCards[0].GetComponentInChildren<Selectable>();
            
            Navigation backNav = backBtn.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            backNav.selectOnRight = firstCard;
            backNav.selectOnLeft = null;

            // Sahnedeki diğer aktif Selectable bileşenlerini tara (Back hariç, level kartları hariç)
            Selectable findUp = null;
            Selectable findDown = null;
            float closestUpDist = float.MaxValue;
            float closestDownDist = float.MaxValue;
            Vector3 backPos = backBtn.transform.position;

            foreach (var sel in Selectable.allSelectablesArray)
            {
                if (sel == null || !sel.isActiveAndEnabled || sel == backBtn || (playBtn != null && sel == playBtn)) continue;
                if (sel.transform.IsChildOf(contentPanel)) continue;

                Vector3 selPos = sel.transform.position;
                float diffY = selPos.y - backPos.y;

                if (diffY > 0.05f) // Fiziksel olarak Back butonunun yukarısında
                {
                    float dist = Vector3.Distance(backPos, selPos);
                    if (dist < closestUpDist)
                    {
                        closestUpDist = dist;
                        findUp = sel;
                    }
                }
                else if (diffY < -0.05f) // Fiziksel olarak Back butonunun aşağısında
                {
                    float dist = Vector3.Distance(backPos, selPos);
                    if (dist < closestDownDist)
                    {
                        closestDownDist = dist;
                        findDown = sel;
                    }
                }
            }

            backNav.selectOnUp = findUp;
            backNav.selectOnDown = findDown;
            backBtn.navigation = backNav;

            Debug.Log($"[TowerMenuUI] Back butonu navigasyonu ayarlandı. Up: {findUp?.name}, Down: {findDown?.name}");
        }

        // Play butonunun yön tuşu bağlantılarını dinamik olarak kur
        if (playBtn != null && spawnedCards.Count > 0)
        {
            Selectable firstCard = spawnedCards[0].GetComponent<Selectable>()
                ?? spawnedCards[0].GetComponentInChildren<Selectable>();
            
            Navigation playNav = playBtn.navigation;
            playNav.mode = Navigation.Mode.Explicit;
            playNav.selectOnLeft = firstCard;
            playNav.selectOnRight = null;

            // Sahnedeki diğer aktif Selectable bileşenlerini tara (Play hariç, level kartları hariç)
            Selectable findUp = null;
            Selectable findDown = null;
            float closestUpDist = float.MaxValue;
            float closestDownDist = float.MaxValue;
            Vector3 playPos = playBtn.transform.position;

            foreach (var sel in Selectable.allSelectablesArray)
            {
                if (sel == null || !sel.isActiveAndEnabled || sel == playBtn || (backBtn != null && sel == backBtn)) continue;
                if (sel.transform.IsChildOf(contentPanel)) continue;

                Vector3 selPos = sel.transform.position;
                float diffY = selPos.y - playPos.y;

                if (diffY > 0.05f) // Fiziksel olarak Play butonunun yukarısında
                {
                    float dist = Vector3.Distance(playPos, selPos);
                    if (dist < closestUpDist)
                    {
                        closestUpDist = dist;
                        findUp = sel;
                    }
                }
                else if (diffY < -0.05f) // Fiziksel olarak Play butonunun aşağısında
                {
                    float dist = Vector3.Distance(playPos, selPos);
                    if (dist < closestDownDist)
                    {
                        closestDownDist = dist;
                        findDown = sel;
                    }
                }
            }

            playNav.selectOnUp = findUp;
            playNav.selectOnDown = findDown;
            playBtn.navigation = playNav;

            Debug.Log($"[TowerMenuUI] Play butonu navigasyonu ayarlandı. Up: {findUp?.name}, Down: {findDown?.name}");
        }
    }

    private void UpdatePlayButtonState(int index)
    {
        // Play butonunu TowerPanelBg (contentPanel.parent) altından bul
        Button playBtn = null;
        if (contentPanel != null && contentPanel.parent != null)
        {
            foreach (var b in contentPanel.parent.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "Play")
                {
                    playBtn = b;
                    break;
                }
            }
        }

        if (playBtn != null)
        {
            TowerManager tm = TowerManager.Instance;
            if (tm != null && index >= 0 && index < tm.levels.Count)
            {
                int reachedLevel = tm.GetReachedLevel();
                bool isUnlocked = (tm.levels[index].levelNumber <= reachedLevel);
                
                playBtn.interactable = isUnlocked;

                // Play butonu üzerindeki Text'i güncelle (kilitli ise "LOCKED", açık ise "PLAY" veya "BUTTON")
                TextMeshProUGUI btnText = playBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = isUnlocked ? "PLAY" : "LOCKED";
                }

                // Tıklama olayını güncelle
                playBtn.onClick.RemoveAllListeners();
                if (isUnlocked)
                {
                    playBtn.onClick.AddListener(() => {
                        tm.StartTowerLevel(index);
                    });
                }
                
                Debug.Log($"[TowerMenuUI] Play butonu güncellendi. Index: {index}, Kilitli mi: {!isUnlocked}, Text: {(btnText != null ? btnText.text : "null")}");
            }
        }
    }

    private void FocusOnIndex(int index, bool immediate = false)
    {
        Debug.Log($"[TowerMenuUI] FocusOnIndex tetiklendi. Index: {index}, currentCenteredIndex: {currentCenteredIndex}, Immediate: {immediate}");

        if (index < 0 || index >= spawnedCards.Count) return;

        // Her durumda Play butonunu seçilen level indexine göre güncelle (kilit kontrolü ve başlatma işlevi)
        UpdatePlayButtonState(index);

        if (index == currentCenteredIndex) return;

        currentCenteredIndex = index;
        float step = cardHeight + spacing;
        
        // Kartlar aşağı doğru ekside dizildiği için paneli yukarı (+) kaydırmalıyız
        float targetY = index * step;

        Debug.Log($"[TowerMenuUI] FocusOnIndex - Hedef Y konumu: {targetY} (step: {step})");

        if (contentPanel != null)
        {
            contentPanel.DOKill();
            if (immediate)
            {
                contentPanel.anchoredPosition = new Vector2(contentPanel.anchoredPosition.x, targetY);
            }
            else
            {
                contentPanel.DOAnchorPosY(targetY, transitionDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        // EventSystem odağını seçilen nesneye çek
        if (spawnedCards[index] != null)
        {
            Selectable activeCardSelectable = spawnedCards[index].GetComponent<Selectable>()
                ?? spawnedCards[index].GetComponentInChildren<Selectable>();

            if (activeCardSelectable != null)
            {
                // Back butonunun sağ yönünü bu aktif karta bağla
                Button backBtn = null;
                MenuUIManager menuUI = FindFirstObjectByType<MenuUIManager>(FindObjectsInactive.Include);
                if (menuUI != null)
                {
                    backBtn = menuUI.BackButton;
                }

                if (backBtn != null)
                {
                    Navigation backNav = backBtn.navigation;
                    backNav.mode = Navigation.Mode.Explicit;
                    backNav.selectOnRight = activeCardSelectable;
                    backBtn.navigation = backNav;
                    Debug.Log($"[TowerMenuUI] Back butonunun sağ yönü aktif kart olan index {index} ({activeCardSelectable.gameObject.name}) olarak güncellendi. Up: {backNav.selectOnUp?.name}, Down: {backNav.selectOnDown?.name}");
                }

                // Play butonunun sol yönünü bu aktif karta bağla
                Button playBtn = null;
                if (contentPanel != null && contentPanel.parent != null)
                {
                    foreach (var b in contentPanel.parent.GetComponentsInChildren<Button>(true))
                    {
                        if (b.name == "Play")
                        {
                            playBtn = b;
                            break;
                        }
                    }
                }

                if (playBtn != null)
                {
                    Navigation playNav = playBtn.navigation;
                    playNav.mode = Navigation.Mode.Explicit;
                    playNav.selectOnLeft = activeCardSelectable;
                    playBtn.navigation = playNav;
                    Debug.Log($"[TowerMenuUI] Play butonunun sol yönü aktif kart olan index {index} ({activeCardSelectable.gameObject.name}) olarak güncellendi.");
                }
            }

            if (EventSystem.current != null)
            {
                GameObject targetObj = spawnedCards[index].gameObject;
                if (EventSystem.current.currentSelectedGameObject != targetObj)
                {
                    Debug.Log($"[TowerMenuUI] EventSystem odağı {targetObj.name} objesine taşınıyor.");
                    EventSystem.current.SetSelectedGameObject(targetObj);
                }
            }
        }
    }
}
