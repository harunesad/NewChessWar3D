using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUIManager : MonoBehaviour
{
    [SerializeField] Button playBtn, towerBtn, shopBtn, multiplayerBtn, twoPlayersBtn, whiteBtn, blackBtn, backBtn,
        exitBtn, soundOnOffBtn, coinAdsBtn, closeCoinAdsPanelBtn, infoBtn, closeInfoPanelBtn;
    [SerializeField] CanvasGroup difficultMenu, mainMenu, shopMenu, coinAdsMenu, infoMenu;
    [SerializeField] List<Button> difficultsBtn;
    [SerializeField] RectTransform select;
    [SerializeField] TextMeshProUGUI warning;
    [SerializeField] AudioSource music, click;
    [SerializeField] Sprite soundOn, soundOff;
    Difficulty difficulty;
    CanvasGroup currentMenu;
    void Start()
    {
        PlayerPrefs.SetString("Type", "White");

        MusicState(music);

        coinAdsBtn.onClick.AddListener(CoinAdsMenuOpen);
        infoBtn.onClick.AddListener(InfoMenuOpen);
        playBtn.onClick.AddListener(delegate { MenuOpen(difficultMenu); });
        shopBtn.onClick.AddListener(delegate { MenuOpen(shopMenu); });
        towerBtn.onClick.AddListener(delegate { MessageShow("Coming Soon"); });
        multiplayerBtn.onClick.AddListener(delegate { MessageShow("Coming Soon"); });
        twoPlayersBtn.onClick.AddListener(delegate { MessageShow("Coming Soon"); });

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
        warning.transform.parent = mainMenu.transform;
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
        warning.transform.parent = mainMenu.transform;
        coinAdsMenu.alpha = 0;
        coinAdsMenu.blocksRaycasts = false;
        coinAdsMenu.interactable = false;
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
    void WhiteSelect()
    {
        MusicState(click);
        difficulty = FindAnyObjectByType<Difficulty>();
        PlayerPrefs.SetString("Type", "White");
        select.anchoredPosition = new Vector3(-25, 0, 0);
    }
    void BlackSelect()
    {
        MusicState(click);
        difficulty = FindAnyObjectByType<Difficulty>();
        PlayerPrefs.SetString("Type", "Black");
        select.anchoredPosition = new Vector3(25, 0, 0);
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
}
