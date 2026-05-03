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
        exitBtn, soundOnOffBtn, coinAdsBtn, closeCoinAdsPanelBtn, infoBtn, closeInfoPanelBtn, healthBtn, dailyRewardBtn;
    [SerializeField] CanvasGroup difficultMenu, mainMenu, shopMenu, coinAdsMenu, infoMenu, dailyRewardMenu, towerMenu;
    [SerializeField] Sprite whiteSelected, whiteUnselected, blackSelected, blackUnselected;
    [SerializeField] List<Button> difficultsBtn;
    [SerializeField] TextMeshProUGUI warning;
    [SerializeField] AudioSource music, click;
    [SerializeField] Sprite soundOn, soundOff;
    Difficulty difficulty;
    CanvasGroup currentMenu;
    void Start()
    {
        // Initialize GameAnalytics explicitly
        GameAnalytics.Initialize();

        PlayerPrefs.SetString("Type", "White");
        whiteBtn.image.sprite = whiteSelected;
        blackBtn.image.sprite = blackUnselected;

        MusicState(music);

        coinAdsBtn.onClick.AddListener(CoinAdsMenuOpen);
        infoBtn.onClick.AddListener(InfoMenuOpen);
        healthBtn.onClick.AddListener(HealthUpdate);
        playBtn.onClick.AddListener(delegate { MenuOpen(difficultMenu); });
        shopBtn.onClick.AddListener(delegate { MenuOpen(shopMenu); });
        towerBtn.onClick.AddListener(TowerMenuOpen);
        multiplayerBtn.onClick.AddListener(delegate { MessageShow("Coming Soon"); });
        twoPlayersBtn.onClick.AddListener(EnterTwoPlayers);

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

        // Start Health Button and CoinAds Button Pulse
        StartPulse(healthBtn.transform.parent);
        StartPulse(coinAdsBtn.transform.parent);
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
        }
        else
        {
            mainMenu.alpha = 0;
            mainMenu.blocksRaycasts = false;
            mainMenu.interactable = false;
        }
        if (coinAdsMenu.alpha == 1)
        {
            coinAdsMenu.alpha = 0;
            coinAdsMenu.blocksRaycasts = false;
            coinAdsMenu.interactable = false;
        }
    }
    void InfoMenuClose()
    {
        MusicState(click);
        if (currentMenu != null)
        {
            backBtn.gameObject.SetActive(true);
            currentMenu.alpha = 1;
            currentMenu.blocksRaycasts = true;
            currentMenu.interactable = true;
            warning.transform.parent = currentMenu.transform;
        }
        else
        {
            mainMenu.alpha = 1;
            mainMenu.blocksRaycasts = true;
            mainMenu.interactable = true;
            warning.transform.parent = mainMenu.transform;
        }
        infoMenu.alpha = 0;
        infoMenu.blocksRaycasts = false;
        infoMenu.interactable = false;
    }
    void CoinAdsMenuOpen()
    {
        MusicState(click);
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
        }
        else
        {
            mainMenu.alpha = 0;
            mainMenu.blocksRaycasts = false;
            mainMenu.interactable = false;
        }
        if (infoMenu.alpha == 1)
        {
            infoMenu.alpha = 0;
            infoMenu.blocksRaycasts = false;
            infoMenu.interactable = false;
        }
    }
    void CoinAdsMenuClose()
    {
        MusicState(click);
        if (currentMenu != null)
        {
            backBtn.gameObject.SetActive(true);
            currentMenu.alpha = 1;
            currentMenu.blocksRaycasts = true;
            currentMenu.interactable = true;
            warning.transform.parent = currentMenu.transform;
        }
        else
        {
            mainMenu.alpha = 1;
            mainMenu.blocksRaycasts = true;
            mainMenu.interactable = true;
            warning.transform.parent = mainMenu.transform;
        }
        coinAdsMenu.alpha = 0;
        coinAdsMenu.blocksRaycasts = false;
        coinAdsMenu.interactable = false;
    }
    public void TowerMenuOpen()
    {
        if (towerMenu.alpha == 1) return;
        
        TowerMenuUI tmUI = FindAnyObjectByType<TowerMenuUI>();
        if (tmUI != null) tmUI.UpdateUI();
        
        MenuOpen(towerMenu);
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
        }
        if (coinAdsMenu.alpha == 1)
        {
            coinAdsMenu.alpha = 0;
            coinAdsMenu.blocksRaycasts = false;
            coinAdsMenu.interactable = false;
        }
        mainMenu.interactable = false;
        mainMenu.blocksRaycasts = false;
        mainMenu.DOFade(0, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            backBtn.transform.parent = openToMenu.transform;
            warning.transform.parent = openToMenu.transform;
            backBtn.gameObject.SetActive(true);
            openToMenu.DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                currentMenu = openToMenu;
                openToMenu.interactable = true;
                openToMenu.blocksRaycasts = true;
            });
        });
    }
    void EnterTwoPlayers()
    {
        PlayerPrefs.SetInt("IsTowerMode", 0);
        SceneManager.LoadScene(3);
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
        PlayerPrefs.SetInt("IsTowerMode", 0);
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
            backBtn.transform.parent = mainMenu.transform;
            warning.transform.parent = mainMenu.transform;
            backBtn.gameObject.SetActive(false);
            mainMenu.DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                mainMenu.interactable = true;
                mainMenu.blocksRaycasts = true;
                currentMenu = null;
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
}