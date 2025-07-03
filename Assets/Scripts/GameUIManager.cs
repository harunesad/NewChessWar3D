using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [SerializeField] Text timeText;
    [SerializeField] Button pauseBtn, menuBtn, restartBtn, closeBtn;
    [SerializeField] GameObject resumePanel, gameoverPanel, game;
    [SerializeField] float time;
    public bool gameFinish = false;
    void Start()
    {
        if (time % 60 < 10)
        {
            timeText.text = (int)(time / 60) + " : 0" + (int)(time % 60);
        }
        else
        {
            timeText.text = (int)(time / 60) + " : " + (int)(time % 60);
        }

        pauseBtn.onClick.AddListener(ResumePanelOnOff);
        menuBtn.onClick.AddListener(MenuOpen);
        restartBtn.onClick.AddListener(RestartGame);
        closeBtn.onClick.AddListener(ResumePanelOff);

        gameoverPanel.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(MenuOpen);
        gameoverPanel.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(RestartGame);
    }
    void Update()
    {
        time -= Time.deltaTime;
        if (time % 60 < 10)
        {
            timeText.text = (int)(time / 60) + " : 0" + (int)(time % 60);
        }
        else
        {
            timeText.text = (int)(time / 60) + " : " + (int)(time % 60);
        }
    }
    void ResumePanelOnOff()
    {
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
        Time.timeScale = 1;
        SceneManager.LoadScene(0);
    }
    void RestartGame()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    void ResumePanelOff()
    {
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
