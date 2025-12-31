using Clickables;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Unity.VisualScripting.Member;
using ChessEngine.Game.UI;
using ChessEngine.Game;
using DG.Tweening;

public class GameUIManager : MonoBehaviour
{
    [SerializeField] Text timeText;
    [SerializeField] TextMeshProUGUI warning;
    [SerializeField] Button pauseBtn, menuBtn, restartBtn, closeBtn, soundOnOffBtn,
    undoBtn, healthBtn;
    [SerializeField] GameObject resumePanel, gameoverPanel, game;
    [SerializeField] ChessPoints chessPoints;
    [SerializeField] Sprite soundOn, soundOff;
    [SerializeField] AudioSource click;
    [SerializeField] GameSave gameSave;
    [SerializeField] TurnIndicatorUI turnIndicatorUI;
    [SerializeField] string turn;
    public float time;
    public bool gameFinish = false;
    ChessUndoManager chessUndoManager;
    Difficulty difficulty;
    ChessGameManager chessGameManager;

    void Start()
    {
        chessUndoManager = FindAnyObjectByType<ChessUndoManager>();
        difficulty = FindAnyObjectByType<Difficulty>();
        chessGameManager = FindAnyObjectByType<ChessGameManager>();

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

        healthBtn.onClick.AddListener(HealthUpdate);
        undoBtn.onClick.AddListener(UndoButton);
        pauseBtn.onClick.AddListener(ResumePanelOnOff);
        menuBtn.onClick.AddListener(MenuOpen);
        restartBtn.onClick.AddListener(RestartGame);
        closeBtn.onClick.AddListener(ResumePanelOff);
        soundOnOffBtn.onClick.AddListener(SoundfOnOff);

        gameoverPanel.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(MenuOpen);
        gameoverPanel.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(RestartGame);

        // Start Health Button Pulse
        StartPulse(healthBtn.transform.parent);
    }
    void Update()
    {
        if (chessGameManager.ChessInstance.turn.ToString() == turn)
        {
            if (!undoBtn.gameObject.activeSelf)
            {
                undoBtn.gameObject.SetActive(true);
                StartPulse(undoBtn.transform); // Start pulse when it becomes active
            }
        }
        else
        {
            if (undoBtn.gameObject.activeSelf)
            {
                undoBtn.transform.DOKill(); // Stop animation
                undoBtn.transform.localScale = Vector3.one; // Reset scale
                undoBtn.gameObject.SetActive(false);
            }
        }
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
            RewardedAdsManager.Instance.OnAdClosed += OnAdClosedCleanup;

            RewardedAdsManager.Instance.ShowRewardedAd();
        }
        else
        {
            Debug.LogWarning("Ad Not Ready");
            // If ad is not ready, try to load one for next time
            if (RewardedAdsManager.Instance != null)
            {
                RewardedAdsManager.Instance.LoadRewardedAd();
            }
        }
    }

    void GiveHealthReward()
    {
        GameSave.gameSave.sv.health++;
        SaveManager.Save(GameSave.gameSave.sv);
        GameSave.gameSave.HealthUpdate();
        // Note: Using the exact call found in original code, assuming GameSave.gameSave is valid static or typo
        // Original: GameSave.gameSave.HealthUpdate();
        // Checked logic: It seems GameUIManager uses this.
        // Cleanup handled by OnAdClosedCleanup usually, but for safety:
    }

    void OnAdClosedCleanup()
    {
        if (RewardedAdsManager.Instance != null)
        {
            RewardedAdsManager.Instance.OnRewardEarned -= GiveHealthReward;
            RewardedAdsManager.Instance.OnAdClosed -= OnAdClosedCleanup;
        }
    }
    void UndoButton()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
        if (GameSave.gameSave.sv.health <= 0)
        {
            MessageShow("No Health");
            return;
        }
        GameSave.gameSave.sv.health--;
        SaveManager.Save(GameSave.gameSave.sv);
        GameSave.gameSave.HealthUpdate();
        chessUndoManager.Undo();
        chessUndoManager.Undo();
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
    public void MessageShow(string message)
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
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
