using DG.Tweening;
using GameAnalyticsSDK;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUIManager : MonoBehaviour
{
    [SerializeField] Button playBtn, towerBtn, shopBtn, multiplayerBtn, twoPlayersBtn, whiteBtn, blackBtn, backBtn,
        exitBtn, soundOnOffBtn, coinAdsBtn, closeCoinAdsPanelBtn, infoBtn, closeInfoPanelBtn, healthBtn, dailyRewardBtn, tutorialBtn;
    [SerializeField] CanvasGroup difficultMenu, mainMenu, shopMenu, coinAdsMenu, infoMenu, dailyRewardMenu;
    [SerializeField] GameObject tutorialOverlay, loading; // İlk başta görünmesi gereken siyah ekran
    [SerializeField] Sprite whiteSelected, whiteUnselected, blackSelected, blackUnselected;
    [SerializeField] List<Button> difficultsBtn;
    [SerializeField] TextMeshProUGUI warning, loadingText;
    [SerializeField] AudioSource music, click;
    [SerializeField] Sprite soundOn, soundOff;
    Difficulty difficulty;
    CanvasGroup currentMenu;
    void Start()
    {
        StartCoroutine(LoadingDelay());
        // Initialize GameAnalytics explicitly
        GameAnalytics.Initialize();

        PlayerPrefs.SetString("Type", "White");
        whiteBtn.image.sprite = whiteSelected;
        blackBtn.image.sprite = blackUnselected;

        MusicState(music);

        coinAdsBtn.onClick.AddListener(CoinAdsMenuOpen);
        infoBtn.onClick.AddListener(delegate { 
            if (IsTutorialDone()) InfoMenuOpen(); 
            else MessageShow("Please complete the tutorial first!"); 
        });
        healthBtn.onClick.AddListener(HealthUpdate);
        
        playBtn.onClick.AddListener(delegate { 
            if (IsTutorialDone()) MenuOpen(difficultMenu); 
            else MessageShow("Please complete the tutorial first!"); 
        });
        
        shopBtn.onClick.AddListener(delegate { 
            if (IsTutorialDone()) MenuOpen(shopMenu); 
            else MessageShow("Please complete the tutorial first!"); 
        });
        
        towerBtn.onClick.AddListener(delegate { 
            if (IsTutorialDone()) MessageShow("Coming Soon"); 
            else MessageShow("Please complete the tutorial first!"); 
        });
        
        multiplayerBtn.onClick.AddListener(delegate { 
            if (IsTutorialDone()) MessageShow("Coming Soon"); 
            else MessageShow("Please complete the tutorial first!"); 
        });
        
        twoPlayersBtn.onClick.AddListener(delegate { 
            if (IsTutorialDone()) EnterTwoPlayers(); 
            else MessageShow("Please complete the tutorial first!"); 
        });

        whiteBtn.onClick.AddListener(WhiteSelect);
        blackBtn.onClick.AddListener(BlackSelect);

        for (int i = 0; i < difficultsBtn.Count; i++)
        {
            int j = i;
            difficultsBtn[i].onClick.AddListener(delegate { DifficultSelect(j); });
        }

        backBtn.onClick.AddListener(Backmenu);
        soundOnOffBtn.onClick.AddListener(SoundfOnOff);
        exitBtn.onClick.AddListener(ExitGame);
        closeCoinAdsPanelBtn.onClick.AddListener(CoinAdsMenuClose);
        closeInfoPanelBtn.onClick.AddListener(InfoMenuClose);
        dailyRewardBtn.onClick.AddListener(DailyRewardMenuOpen);
        tutorialBtn.onClick.AddListener(EnterTutorial);

        // İlk kez mi açılıyor kontrol et
        bool isTutorialDone = PlayerPrefs.GetInt("TutorialDone", 0) == 1;
        if (tutorialOverlay != null)
        {
            tutorialOverlay.SetActive(!isTutorialDone);
        }

        // Start Health Button and CoinAds Button Pulse
        StartPulse(healthBtn.transform.parent);
        StartPulse(coinAdsBtn.transform.parent);

        // Deactivate all sub-panels so EventSystem ignores them at start
        if (difficultMenu != null) difficultMenu.gameObject.SetActive(false);
        if (shopMenu != null) shopMenu.gameObject.SetActive(false);
        if (coinAdsMenu != null) coinAdsMenu.gameObject.SetActive(false);
        if (infoMenu != null) infoMenu.gameObject.SetActive(false);
        if (dailyRewardMenu != null) dailyRewardMenu.gameObject.SetActive(false);
    }
    IEnumerator LoadingDelay()
    {
        loadingText.text = "Loading";
        yield return new WaitForSeconds(.5f);
        loadingText.text = "Loading .";
        yield return new WaitForSeconds(.5f);
        loadingText.text = "Loading ..";
        yield return new WaitForSeconds(.5f);
        loadingText.text = "Loading ...";
        yield return new WaitForSeconds(.5f);
        loading.SetActive(false);
        if (UnityEngine.EventSystems.EventSystem.current != null && playBtn != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playBtn.gameObject);
        }
    }
    void StartPulse(Transform target)
    {
        target.DOKill();
        Vector3 baseScale = target.localScale;
        target.DOScale(baseScale * 1.1f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }
    void HealthUpdate()
    {
        if (RewardedAdsManager.Instance != null && RewardedAdsManager.Instance.IsRewardedAdReady())
        {
            // Subscribe to events
            RewardedAdsManager.Instance.OnRewardEarned += GiveHealthReward;
            RewardedAdsManager.Instance.OnAdFailedToShow += OnHealthAdFailed;
            RewardedAdsManager.Instance.OnAdClosed += OnAdClosedCleanup;
            
            RewardedAdsManager.Instance.ShowRewardedAd();
        }
        else
        {
            MessageShow("Ad Not Ready");
            // If ad is not ready, try to load one for next time
            if (RewardedAdsManager.Instance != null)
            {
                RewardedAdsManager.Instance.LoadRewardedAd();
            }
        }
    }

    void GiveHealthReward()
    {
        JsonSave.jsonSave.sv.health++;
        SaveManager.Save(JsonSave.jsonSave.sv);
        JsonSave.jsonSave.HealthUpdate();
        
        GameAnalytics.NewDesignEvent("Ads:HealthReward:Success");
    }

    void OnHealthAdFailed()
    {
        MessageShow("Ad Failed");
        Debug.LogWarning("Health ad failed to show");
    }

    void OnAdClosedCleanup()
    {
        if (RewardedAdsManager.Instance != null)
        {
            RewardedAdsManager.Instance.OnRewardEarned -= GiveHealthReward;
            RewardedAdsManager.Instance.OnAdFailedToShow -= OnHealthAdFailed;
            RewardedAdsManager.Instance.OnAdClosed -= OnAdClosedCleanup;
        }
    }
    void MusicState(AudioSource source)
    {
        if (PlayerPrefs.HasKey("Audio"))
        {
            if (PlayerPrefs.GetInt("Audio") == 1)
            {
                source.Play();
                soundOnOffBtn.GetComponent<Image>().sprite = soundOff;
            }
            else
            {
                source.Stop();
                soundOnOffBtn.GetComponent<Image>().sprite = soundOn;
            }
        }
        else
        {
            if (source.isPlaying)
            {
                PlayerPrefs.SetInt("Audio", 0);
                source.Stop();
            }
            else
            {
                PlayerPrefs.SetInt("Audio", 1);
                source.Play();
            }
        }
    }
    void InfoMenuOpen()
    {
        MusicState(click);
        infoMenu.gameObject.SetActive(true);
        warning.transform.parent = infoMenu.transform;
        infoMenu.alpha = 1;
        infoMenu.blocksRaycasts = true;
        infoMenu.interactable = true;
        if (currentMenu != null)
        {
            backBtn.gameObject.SetActive(false);
            currentMenu.alpha = 0;
            currentMenu.blocksRaycasts = false;
            currentMenu.interactable = false;
            currentMenu.gameObject.SetActive(false);
        }
        else
        {
            mainMenu.alpha = 0;
            mainMenu.blocksRaycasts = false;
            mainMenu.interactable = false;
            mainMenu.gameObject.SetActive(false);
        }
        if (coinAdsMenu.alpha == 1)
        {
            coinAdsMenu.alpha = 0;
            coinAdsMenu.blocksRaycasts = false;
            coinAdsMenu.interactable = false;
            coinAdsMenu.gameObject.SetActive(false);
        }
        if (UnityEngine.EventSystems.EventSystem.current != null && closeInfoPanelBtn != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeInfoPanelBtn.gameObject);
        }
    }
    void InfoMenuClose()
    {
        MusicState(click);
        infoMenu.gameObject.SetActive(false);
        if (currentMenu != null)
        {
            backBtn.gameObject.SetActive(true);
            currentMenu.gameObject.SetActive(true);
            currentMenu.alpha = 1;
            currentMenu.blocksRaycasts = true;
            currentMenu.interactable = true;
            warning.transform.parent = currentMenu.transform;
        }
        else
        {
            mainMenu.gameObject.SetActive(true);
            mainMenu.alpha = 1;
            mainMenu.blocksRaycasts = true;
            mainMenu.interactable = true;
            warning.transform.parent = mainMenu.transform;
        }
        infoMenu.alpha = 0;
        infoMenu.blocksRaycasts = false;
        infoMenu.interactable = false;
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            if (currentMenu != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
            }
            else
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playBtn.gameObject);
            }
        }
    }
    void CoinAdsMenuOpen()
    {
        MusicState(click);
        coinAdsMenu.gameObject.SetActive(true);
        warning.transform.parent = coinAdsMenu.transform;
        coinAdsMenu.alpha = 1;
        coinAdsMenu.blocksRaycasts = true;
        coinAdsMenu.interactable = true;
        if (currentMenu != null)
        {
            backBtn.gameObject.SetActive(false);
            currentMenu.alpha = 0;
            currentMenu.blocksRaycasts = false;
            currentMenu.interactable = false;
            currentMenu.gameObject.SetActive(false);
        }
        else
        {
            mainMenu.alpha = 0;
            mainMenu.blocksRaycasts = false;
            mainMenu.interactable = false;
            mainMenu.gameObject.SetActive(false);
        }
        if (infoMenu.alpha == 1)
        {
            infoMenu.alpha = 0;
            infoMenu.blocksRaycasts = false;
            infoMenu.interactable = false;
            infoMenu.gameObject.SetActive(false);
        }
        if (UnityEngine.EventSystems.EventSystem.current != null && closeCoinAdsPanelBtn != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeCoinAdsPanelBtn.gameObject);
        }
    }
    void CoinAdsMenuClose()
    {
        MusicState(click);
        coinAdsMenu.gameObject.SetActive(false);
        if (currentMenu != null)
        {
            backBtn.gameObject.SetActive(true);
            currentMenu.gameObject.SetActive(true);
            currentMenu.alpha = 1;
            currentMenu.blocksRaycasts = true;
            currentMenu.interactable = true;
            warning.transform.parent = currentMenu.transform;
        }
        else
        {
            mainMenu.gameObject.SetActive(true);
            mainMenu.alpha = 1;
            mainMenu.blocksRaycasts = true;
            mainMenu.interactable = true;
            warning.transform.parent = mainMenu.transform;
        }
        coinAdsMenu.alpha = 0;
        coinAdsMenu.blocksRaycasts = false;
        coinAdsMenu.interactable = false;
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            if (currentMenu != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
            }
            else
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playBtn.gameObject);
            }
        }
    }
    public void DailyRewardMenuOpen()
    {
        if (dailyRewardMenu.alpha == 1) return; // Zaten açıksa tekrar açma
        
        DailyRewardManager drm = FindObjectOfType<DailyRewardManager>();
        if (drm != null) drm.UpdateUI();
        
        MenuOpen(dailyRewardMenu);
    }
    void MenuOpen(CanvasGroup openToMenu)
    {
        MusicState(click);
        if (infoMenu.alpha == 1)
        {
            infoMenu.alpha = 0;
            infoMenu.blocksRaycasts = false;
            infoMenu.interactable = false;
            infoMenu.gameObject.SetActive(false);
        }
        if (coinAdsMenu.alpha == 1)
        {
            coinAdsMenu.alpha = 0;
            coinAdsMenu.blocksRaycasts = false;
            coinAdsMenu.interactable = false;
            coinAdsMenu.gameObject.SetActive(false);
        }
        mainMenu.interactable = false;
        mainMenu.blocksRaycasts = false;
        mainMenu.DOFade(0, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            mainMenu.gameObject.SetActive(false);
            backBtn.transform.parent = openToMenu.transform;
            warning.transform.parent = openToMenu.transform;
            backBtn.gameObject.SetActive(true);
            openToMenu.gameObject.SetActive(true);
            openToMenu.DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                currentMenu = openToMenu;
                openToMenu.interactable = true;
                openToMenu.blocksRaycasts = true;
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    if (openToMenu == difficultMenu && difficultsBtn != null && difficultsBtn.Count > 0)
                    {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(difficultsBtn[0].gameObject);
                    }
                    else
                    {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
                    }
                }
            });
        });
    }
    void EnterTwoPlayers()
    {
        SceneManager.LoadScene(3);
    }
    public void EnterTutorial()
    {
        BodylinkTutorialManager.IsTutorialActive = true;
        // Beyaz oyuncu sahnesine yönlendir (Scene 2)
        SceneManager.LoadScene(2);
    }
    void WhiteSelect()
    {
        MusicState(click);
        difficulty = FindAnyObjectByType<Difficulty>();
        PlayerPrefs.SetString("Type", "White");
        whiteBtn.image.sprite = whiteSelected;
        blackBtn.image.sprite = blackUnselected;
    }
    void BlackSelect()
    {
        MusicState(click);
        difficulty = FindAnyObjectByType<Difficulty>();
        PlayerPrefs.SetString("Type", "Black");
        blackBtn.image.sprite = blackSelected;
        whiteBtn.image.sprite = whiteUnselected;
    }
    void DifficultSelect(int difficult)
    {
        MusicState(click);
        difficulty = FindAnyObjectByType<Difficulty>();
        if (JsonSave.jsonSave.sv.unlock[difficult])
        {
            difficulty.difficult = (difficult + 1) * 2;
            if (PlayerPrefs.GetString("Type") == "White")
            {
                SceneManager.LoadScene(2);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
        }
        else
        {
            MessageShow("Unlock");
        }
    }
    void Backmenu()
    {
        CanvasGroup closeToMenu = currentMenu;
        MusicState(click);
        closeToMenu.interactable = false;
        closeToMenu.blocksRaycasts = false;
        closeToMenu.DOFade(0, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            closeToMenu.gameObject.SetActive(false);
            backBtn.transform.parent = mainMenu.transform;
            warning.transform.parent = mainMenu.transform;
            backBtn.gameObject.SetActive(false);
            mainMenu.gameObject.SetActive(true);
            mainMenu.DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                mainMenu.interactable = true;
                mainMenu.blocksRaycasts = true;
                if (UnityEngine.EventSystems.EventSystem.current != null && playBtn != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playBtn.gameObject);
                }
            });
        });
    }

    void SoundfOnOff()
    {
        MusicState(click);
        if (soundOnOffBtn.GetComponent<Image>().sprite == soundOn)
        {
            PlayerPrefs.SetInt("Audio", 1);
            music.Play();
            soundOnOffBtn.GetComponent<Image>().sprite = soundOff;
        }
        else
        {
            PlayerPrefs.SetInt("Audio", 0);
            music.Stop();
            soundOnOffBtn.GetComponent<Image>().sprite = soundOn;
        }
    }
    void ExitGame()
    {
        MusicState(click);
        Application.Quit();
    }
    public void MessageShow(string message)
    {
        MusicState(click);
        warning.GetComponent<TextMeshProUGUI>().text = message;
        warning.GetComponent<CanvasGroup>().DOFade(1, .75f).SetEase(Ease.Linear).OnComplete(() =>
        {
            warning.GetComponent<CanvasGroup>().DOFade(1, .5f).SetEase(Ease.Linear).OnComplete(() =>
            {
                warning.GetComponent<CanvasGroup>().DOFade(0, .75f).SetEase(Ease.Linear);
            });
        });
    }

    void OnDestroy()
    {
        // Critical: Clean up ad event subscriptions to prevent memory leaks
        OnAdClosedCleanup();
    }

    bool IsTutorialDone()
    {
        return PlayerPrefs.GetInt("TutorialDone", 0) == 1;
    }

    /// <summary>Returns the default button to focus on when switching to controller mode in this scene.</summary>
    public GameObject GetDefaultSelectedButton()
    {
        if (difficultMenu != null && difficultMenu.gameObject.activeSelf)
        {
            if (difficultsBtn != null && difficultsBtn.Count > 0)
                return difficultsBtn[0].gameObject;
        }
        else if (shopMenu != null && shopMenu.gameObject.activeSelf)
        {
            if (backBtn != null) return backBtn.gameObject;
        }
        else if (coinAdsMenu != null && coinAdsMenu.gameObject.activeSelf)
        {
            if (closeCoinAdsPanelBtn != null) return closeCoinAdsPanelBtn.gameObject;
        }
        else if (infoMenu != null && infoMenu.gameObject.activeSelf)
        {
            if (closeInfoPanelBtn != null) return closeInfoPanelBtn.gameObject;
        }
        else if (dailyRewardMenu != null && dailyRewardMenu.gameObject.activeSelf)
        {
            if (backBtn != null) return backBtn.gameObject;
        }

        if (mainMenu != null && mainMenu.gameObject.activeSelf)
        {
            if (playBtn != null) return playBtn.gameObject;
        }

        return null;
    }
}
