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
using ChessEngine;
using DG.Tweening;

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
        chessUndoManager = FindAnyObjectByType<ChessUndoManager>();
        difficulty = FindAnyObjectByType<Difficulty>();
        chessGameManager = FindAnyObjectByType<ChessGameManager>();
        
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
        soundOnOffBtn.onClick.AddListener(SoundfOnOff);

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
            string winnerColor = "";
            string resultMessage = "";
            
            if (chessPoints.whitePoints < chessPoints.blackPoints)
            {
                winnerColor = "Black";
                resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                PlayerPrefs.SetString("WinType", winnerColor);
                gameSave.ChessSave();
                GameoverMenuOpen(resultMessage);
            }
            else if (chessPoints.whitePoints == chessPoints.blackPoints)
            {
                PlayerPrefs.SetString("WinType", "Draw");
                gameSave.ChessSave();
                GameoverMenuOpen("Draw");
            }
            else
            {
                winnerColor = "White";
                resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                PlayerPrefs.SetString("WinType", winnerColor);
                gameSave.ChessSave();
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
                resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                PlayerPrefs.SetString("WinType", winnerColor);
                break;
            case GameOverReason.Draw:
                // Beraberlik (Pat)
                resultMessage = "Stalemate - Draw";
                PlayerPrefs.SetString("WinType", "Draw");
                break;
            case GameOverReason.Forfeit:
                // Terk: pTeam terk etti, karşı taraf kazandı
                winnerColor = (pTeam == ChessColor.Black) ? "White" : "Black";
                resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                PlayerPrefs.SetString("WinType", winnerColor);
                break;
            case GameOverReason.TimeExpired:
                // Süre bitti: pTeam'in süresi bitti, karşı taraf kazandı
                winnerColor = (pTeam == ChessColor.Black) ? "White" : "Black";
                resultMessage = (winnerColor == turn) ? "You Win!" : "You Lose!";
                PlayerPrefs.SetString("WinType", winnerColor);
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

    public void GameoverMenuOpen(string result)
    {
        gameSave.ChessSave();
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
