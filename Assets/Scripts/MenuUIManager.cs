using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUIManager : MonoBehaviour
{
    [SerializeField] Difficulty difficulty;
    [SerializeField] Button chessPlayBtn, chessTowerBtn, chessMarketBtn, chessMultiplayerBtn, whiteBtn, blackBtn, backBtn,
        exitBtn, soundOnOffBtn;
    [SerializeField] CanvasGroup difficultMenu, mainMenu;
    [SerializeField] List<Button> difficultsBtn;
    [SerializeField] RectTransform select;
    [SerializeField] TextMeshProUGUI warning;
    [SerializeField] AudioSource music, click;
    [SerializeField] Sprite soundOn, soundOff;
    void Start()
    {
        MusicState(music);
        chessPlayBtn.onClick.AddListener(Difficultopen);
        chessTowerBtn.onClick.AddListener(delegate { MessageShow("Coming Soon"); });
        chessMarketBtn.onClick.AddListener(delegate { MessageShow("Coming Soon"); });
        chessMultiplayerBtn.onClick.AddListener(delegate { MessageShow("Coming Soon"); });

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
    void Difficultopen()
    {
        MusicState(click);
        mainMenu.interactable = false;
        mainMenu.blocksRaycasts = false;
        mainMenu.DOFade(0, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            backBtn.transform.parent = difficultMenu.transform;
            warning.transform.parent = difficultMenu.transform;
            backBtn.gameObject.SetActive(true);
            difficultMenu.DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                difficultMenu.interactable = true;
                difficultMenu.blocksRaycasts = true;
            });
        });
    }
    void WhiteSelect()
    {
        MusicState(click);
        difficulty.type = Difficulty.Type.White;
        select.anchoredPosition = new Vector3(-25, 0, 0);
    }
    void BlackSelect()
    {
        MusicState(click);
        difficulty.type = Difficulty.Type.Black;
        select.anchoredPosition = new Vector3(25, 0, 0);
    }
    void DifficultSelect(int difficult)
    {
        MusicState(click);
        if (JsonSave.jsonSave.sv.unlock[difficult])
        {
            difficulty.difficult = (difficult + 1) * 2;
            if (difficulty.type == Difficulty.Type.White)
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
        MusicState(click);
        difficultMenu.interactable = false;
        difficultMenu.blocksRaycasts = false;
        difficultMenu.DOFade(0, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            warning.transform.parent = mainMenu.transform;
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
    void MessageShow(string message)
    {
        MusicState(click);
        warning.GetComponent<TextMeshProUGUI>().text = message;
        warning.GetComponent<CanvasGroup>().DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            warning.GetComponent<CanvasGroup>().DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                warning.GetComponent<CanvasGroup>().DOFade(0, 1).SetEase(Ease.Linear);
            });
        });
    }
}
