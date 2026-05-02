using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ChessEngine.Game.UI;
using ChessEngine.Game;
using ChessEngine;
using DG.Tweening;
using GameAnalyticsSDK;

public class GameUIManager : MonoBehaviour
{
    [SerializeField] Text timeText;
    [SerializeField] TextMeshProUGUI warning;
    [SerializeField] Button pauseBtn, menuBtn, restartBtn, closeBtn, soundOnOffBtn,
    undoBtn, healthBtn;
    [SerializeField] GameObject resumePanel, gameoverPanel, game;
    [SerializeField] ChessPoints chessPoints;
    [SerializeField] Sprite soundOn, soundOff, resume, pause;
    [SerializeField] AudioSource click;
    [SerializeField] GameSave gameSave;
    [SerializeField] TurnIndicatorUI turnIndicatorUI;
    [SerializeField] string turn;
    public float time;
    public bool gameFinish = false;
    ChessUndoManager chessUndoManager;
    Difficulty difficulty;
    ChessGameManager chessGameManager;
    string pendingGameOverMessage = "";
    public int activeAnimations = 0; // Hareket eden taş sayısını takip eder

    void Start()
    {
        Debug.Log("GameUIManager Start Triggered!");
        chessUndoManager = FindAnyObjectByType<ChessUndoManager>();
        difficulty = FindAnyObjectByType<Difficulty>();
        chessGameManager = FindAnyObjectByType<ChessGameManager>();
        
        // Setup Tutorial
        int tutorialVal = PlayerPrefs.GetInt("TutorialCompleted", 0);
        Debug.Log("Tutorial Status in Prefs: " + tutorialVal);

        if (tutorialVal == 0)
        {
            Debug.Log("Attempting to add TutorialManager...");
            TutorialManager tutorial = gameObject.AddComponent<TutorialManager>();
            tutorial.interactor = FindAnyObjectByType<TPSChessInteractor>();
            tutorial.uiManager = this;
            tutorial.gameManager = chessGameManager;
            Debug.Log("TutorialManager component added!");
        }

        // Oyuncunun rengini PlayerPrefs'ten al
        turn = PlayerPrefs.GetString("Type", "White");

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
        soundOnOffBtn.onClick.AddListener(SoundOnOff);

        gameoverPanel.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(MenuOpen);
        gameoverPanel.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(RestartGame);

        // Start Health Button Pulse
        StartPulse(healthBtn.transform.parent);
    }
    void Update()
    {
        if (gameFinish) return;

        if (chessGameManager.ChessInstance.turn.ToString() == turn)
        {
            if (!undoBtn.gameObject.activeSelf)
            {
                undoBtn.gameObject.SetActive(true);
                //StartPulse(undoBtn.transform); // Start pulse when it becomes active
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
            string winnerColor = "";
            string resultMessage = "";
            
            if (chessPoints.whitePoints < chessPoints.blackPoints)
            {
                winnerColor = "Black";
                if (SceneManager.GetActiveScene().buildIndex != 3)
                {
                    resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                    PlayerPrefs.SetString("WinType", winnerColor);
                }
                else
                {
                    resultMessage = "Black Win!";
                }
                GameoverMenuOpen(resultMessage);
            }
            else if (chessPoints.whitePoints == chessPoints.blackPoints)
            {
                if (SceneManager.GetActiveScene().buildIndex != 3)
                {
                    PlayerPrefs.SetString("WinType", "Draw");
                }
                GameoverMenuOpen("Draw!");
            }
            else
            {
                winnerColor = "White";
                if (SceneManager.GetActiveScene().buildIndex != 3)
                {
                    resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                    PlayerPrefs.SetString("WinType", winnerColor);
                }
                else
                {
                    resultMessage = "White Win!";
                }
                GameoverMenuOpen(resultMessage);
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
        if (gameFinish) return; // Prevent health reward during Game Over

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
        if (gameFinish) return; // Prevent pause during Game Over

        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            click.Play();
        }
        resumePanel.SetActive(!resumePanel.gameObject.activeSelf);
        game.SetActive(!game.activeSelf);
        if (Time.timeScale == 1)
        {
            Time.timeScale = 0;
            pauseBtn.GetComponent<Image>().sprite = resume;
        }
        else
        {
            Time.timeScale = 1;
            pauseBtn.GetComponent<Image>().sprite = pause;
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
    void SoundOnOff()
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
        pauseBtn.GetComponent<Image>().sprite = pause;
    }
    public void GameFinish()
    {
        gameFinish = true;
    }

    void OnEnable()
    {
        if (chessGameManager == null)
            chessGameManager = FindAnyObjectByType<ChessGameManager>();

        if (chessGameManager != null)
            chessGameManager.GameOver.AddListener(OnChessGameOver);
    }

    void OnDisable()
    {
        if (chessGameManager != null)
            chessGameManager.GameOver.RemoveListener(OnChessGameOver);
    }

    void OnDestroy()
    {
        // Critical: Clean up ad event subscriptions to prevent memory leaks
        OnAdClosedCleanup();
    }

    private void OnChessGameOver(ChessColor pTeam, GameOverReason pReason)
    {
        // Eğer zaten bir mesaj bekliyorsa (mesela süre bittiyse), ikinci bir mesaj almayalım
        if (!string.IsNullOrEmpty(pendingGameOverMessage)) 
        {
            return;
        }

        string resultMessage = "Game Over";
        string winnerColor = "";

        switch (pReason)
        {
            case GameOverReason.Won:
                // Mat durumu: pTeam kazandı
                winnerColor = pTeam.ToString();
                if (SceneManager.GetActiveScene().buildIndex != 3)
                {
                    resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                    PlayerPrefs.SetString("WinType", winnerColor);
                }
                else
                {
                    resultMessage = winnerColor + " Win!";
                }
                break;
            case GameOverReason.Draw:
                // Beraberlik (Pat)
                resultMessage = "Stalemate - Draw";
                if (SceneManager.GetActiveScene().buildIndex != 3)
                {
                    PlayerPrefs.SetString("WinType", "Draw");
                }
                break;
            case GameOverReason.Forfeit:
                // Terk: pTeam terk etti, karşı taraf kazandı
                winnerColor = (pTeam == ChessColor.Black) ? "White" : "Black";
                if (SceneManager.GetActiveScene().buildIndex != 3)
                {
                    resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                    PlayerPrefs.SetString("WinType", winnerColor);
                }
                else
                {
                    resultMessage = winnerColor + " Win!";
                }
                break;
        }

        gameFinish = true;
        pendingGameOverMessage = resultMessage;

        // Eğer o an hareket eden taş yoksa (Süre bittiğinde veya Pat durumunda), paneli hemen aç.
        if (activeAnimations <= 0)
        {
            CheckGameOverState();
        }
    }

    public void RegisterAnimation()
    {
        activeAnimations++;
    }

    public void UnregisterAnimation()
    {
        activeAnimations--;
        if (activeAnimations < 0) activeAnimations = 0;
        
        CheckGameOverState();
    }

    public void CheckGameOverState()
    {
        if (gameFinish && !string.IsNullOrEmpty(pendingGameOverMessage) && activeAnimations <= 0)
        {
            GameoverMenuOpen(pendingGameOverMessage);
            pendingGameOverMessage = ""; // Reset after showing
        }
    }

    [SerializeField] GameObject wheelObj, arrowObj, claimObj;
    public void GameoverMenuOpen(string result)
    {
        if (SceneManager.GetActiveScene().buildIndex != 3)
        {
            gameSave.ChessSave();

            // DEFAULT: Hide the wheel components first
            if (wheelObj != null) wheelObj.SetActive(false);
            if (arrowObj != null) arrowObj.SetActive(false);
            if (claimObj != null) claimObj.SetActive(false);

            // Automatic In-App Review Trigger
            if (result == "You Win!")
            {
                GameAnalytics.NewDesignEvent("GameOutcome:Win:" + SceneManager.GetActiveScene().name);
                int wins = PlayerPrefs.GetInt("TotalWins", 0) + 1;
                PlayerPrefs.SetInt("TotalWins", wins);
                                
                // Only request review on real devices, not in the Editor
                #if !UNITY_EDITOR
                if (wins == 3)
                {
                    GooglePlayReview review = FindObjectOfType<GooglePlayReview>();
                    if (review != null)
                    {
                        review.RequestReview();
                    }
                }
                #endif

                // SHOW WHEEL ONLY ON WIN:
                if (RewardedInterstitialManager.Instance != null && RewardedInterstitialManager.Instance.IsAdReady())
                {
                    if (wheelObj != null) wheelObj.SetActive(true);
                    if (arrowObj != null) arrowObj.SetActive(true);
                    if (claimObj != null) claimObj.SetActive(true);
                }
            }
            else if (result == "You Lose!")
            {
                GameAnalytics.NewDesignEvent("GameOutcome:Loss:" + SceneManager.GetActiveScene().name);
            }
        }
        if (pauseBtn != null) pauseBtn.interactable = false;
        if (healthBtn != null) healthBtn.interactable = false;
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
        warning.GetComponent<CanvasGroup>().DOKill();
        warning.GetComponent<CanvasGroup>().alpha = 0;
        warning.GetComponent<CanvasGroup>().DOFade(1, .75f).SetEase(Ease.Linear).OnComplete(() =>
        {
            warning.GetComponent<CanvasGroup>().DOFade(1, 1.5f).SetEase(Ease.Linear).OnComplete(() =>
            {
                warning.GetComponent<CanvasGroup>().DOFade(0, .75f).SetEase(Ease.Linear);
            });
        });
    }

    public void ShowTutorialMessage(string message)
    {
        warning.GetComponent<TextMeshProUGUI>().text = message;
        warning.GetComponent<CanvasGroup>().DOKill();
        warning.GetComponent<CanvasGroup>().DOFade(1, 0.5f);
    }

    public void HideTutorialMessage()
    {
        warning.GetComponent<CanvasGroup>().DOKill();
        warning.GetComponent<CanvasGroup>().DOFade(0, 0.5f);
    }

    [ContextMenu("Reset Tutorial Progress")]
    public void ResetTutorialProgress()
    {
        PlayerPrefs.SetInt("TutorialCompleted", 0);
        PlayerPrefs.Save();
        Debug.Log("Tutorial Progress Reset! Restart the game to see the tutorial.");
    }
}
