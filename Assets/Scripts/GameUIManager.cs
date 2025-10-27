using Clickables;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Unity.VisualScripting.Member;

public class GameUIManager : MonoBehaviour
{
    Difficulty difficulty;
    [SerializeField] Text timeText;
    [SerializeField] Button pauseBtn, menuBtn, restartBtn, closeBtn, soundOnOffBtn;
    [SerializeField] GameObject resumePanel, gameoverPanel, game;
    [SerializeField] ChessPoints chessPoints;
    [SerializeField] Sprite soundOn, soundOff;
    [SerializeField] AudioSource click;
    [SerializeField] GameSave gameSave;
    public float time;
    public bool gameFinish = false;
    void Start()
    {
        difficulty = FindAnyObjectByType<Difficulty>();

        if (PlayerPrefs.HasKey("Audio"))
        {
            if (PlayerPrefs.GetInt("Audio") == 1)
            {
                soundOnOffBtn.GetComponent<Image>().sprite = soundOff;
            }
            else
            {
                soundOnOffBtn.GetComponent<Image>().sprite = soundOn;
            }
        }

        string first = ((int)(time / 60)) < 10 ? "0" + ((int)(time / 60)) : ((int)(time / 60)).ToString();
        string second = ((int)(time % 60)) < 10 ? "0" + ((int)(time % 60)) : ((int)(time % 60)).ToString();
        timeText.text = first + " : " + second;

        pauseBtn.onClick.AddListener(ResumePanelOnOff);
        menuBtn.onClick.AddListener(MenuOpen);
        restartBtn.onClick.AddListener(RestartGame);
        closeBtn.onClick.AddListener(ResumePanelOff);
        soundOnOffBtn.onClick.AddListener(SoundfOnOff);

        gameoverPanel.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(MenuOpen);
        gameoverPanel.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(RestartGame);
    }
    void Update()
    {
        time -= Time.deltaTime;
        if (time < 0)
        {
            time = 0;
            if (chessPoints.whitePoints < chessPoints.blackPoints)
            {
                PlayerPrefs.SetString("WinType", "Black");
                gameSave.ChessSave();
                GameoverMenuOpen("Black Win");
            }
            else if (chessPoints.whitePoints == chessPoints.blackPoints)
            {
                PlayerPrefs.SetString("WinType", "Draw");
                gameSave.ChessSave();
                GameoverMenuOpen("Draw");
            }
            else
            {
                PlayerPrefs.SetString("WinType", "White");
                gameSave.ChessSave();
                GameoverMenuOpen("White Win");
            }
            return;
        }
        string first = ((int)(time / 60)) < 10 ? "0" + ((int)(time / 60)) : ((int)(time / 60)).ToString();
        string second = ((int)(time % 60)) < 10 ? "0" + ((int)(time % 60)) : ((int)(time % 60)).ToString();
        timeText.text = first + " : " + second;
    }
    void ResumePanelOnOff()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
        resumePanel.SetActive(!resumePanel.gameObject.activeSelf);
        game.SetActive(!game.activeSelf);
        if (Time.timeScale == 1)
        {
            Time.timeScale = 0;
        }
        else
        {
            Time.timeScale = 1;
        }
    }
    void MenuOpen()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
        Time.timeScale = 1;
        PlayerPrefs.SetInt("Scene", 0);
        //AdsManager adsManager = FindAnyObjectByType<AdsManager>();
        //adsManager.ShowInterstitialAd();
        SceneManager.LoadScene(0);
    }
    void RestartGame()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
        Time.timeScale = 1;
        PlayerPrefs.SetInt("Scene", 1);
        //AdsManager adsManager = FindAnyObjectByType<AdsManager>();
        //adsManager.ShowInterstitialAd();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    void SoundfOnOff()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
        if (soundOnOffBtn.GetComponent<Image>().sprite == soundOn)
        {
            PlayerPrefs.SetInt("Audio", 1);
            soundOnOffBtn.GetComponent<Image>().sprite = soundOff;
        }
        else
        {
            PlayerPrefs.SetInt("Audio", 0);
            soundOnOffBtn.GetComponent<Image>().sprite = soundOn;
        }
    }
    void ResumePanelOff()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
        resumePanel.SetActive(false);
        game.SetActive(true);
        Time.timeScale = 1;
    }
    public void GameFinish()
    {
        Debug.Log("a");
        gameFinish = true;
    }
    public void GameoverMenuOpen(string result)
    {
        pauseBtn.gameObject.SetActive(false);
        gameoverPanel.GetComponentInChildren<TextMeshProUGUI>().text = result;
        gameoverPanel.SetActive(true);
        game.SetActive(false);
        Time.timeScale = 0;
    }
}
