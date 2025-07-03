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
    [SerializeField] Button chessPlayBtn, chessTowerBtn, chessMarketBtn, chessMultiplayerBtn, whiteBtn, blackBtn, backBtn, exitBtn;
    [SerializeField] CanvasGroup difficultMenu, mainMenu;
    [SerializeField] List<Button> difficultsBtn;
    [SerializeField] RectTransform select;
    [SerializeField] TextMeshProUGUI warning;
    void Start()
    {
        chessPlayBtn.onClick.AddListener(Difficultopen);
        chessTowerBtn.onClick.AddListener(ComingSoon);
        chessMarketBtn.onClick.AddListener(ComingSoon);
        chessMultiplayerBtn.onClick.AddListener(ComingSoon);

        whiteBtn.onClick.AddListener(WhiteSelect);
        blackBtn.onClick.AddListener(BlackSelect);

        for (int i = 0; i < difficultsBtn.Count; i++)
        {
            int j = i;
            difficultsBtn[i].onClick.AddListener(delegate { DifficultSelect(j); });
        }

        backBtn.onClick.AddListener(Backmenu);
        exitBtn.onClick.AddListener(ExitGame);
    }
    void Difficultopen()
    {
        mainMenu.interactable = false;
        mainMenu.blocksRaycasts = false;
        mainMenu.DOFade(0, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            backBtn.transform.parent = difficultMenu.transform;
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
        difficulty.type = Difficulty.Type.White;
        select.anchoredPosition = new Vector3(-25, 0, 0);
    }
    void BlackSelect()
    {
        difficulty.type = Difficulty.Type.Black;
        select.anchoredPosition = new Vector3(25, 0, 0);
    }
    void DifficultSelect(int difficult)
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
    void Backmenu()
    {
        difficultMenu.interactable = false;
        difficultMenu.blocksRaycasts = false;
        difficultMenu.DOFade(0, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            mainMenu.DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                mainMenu.interactable = true;
                mainMenu.blocksRaycasts = true;
            });
        });
    }
    void ExitGame()
    {
        Application.Quit();
    }
    void ComingSoon()
    {
        warning.GetComponent<CanvasGroup>().DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            warning.GetComponent<CanvasGroup>().DOFade(1, 2).SetEase(Ease.Linear).OnComplete(() =>
            {
                warning.GetComponent<CanvasGroup>().DOFade(0, 2).SetEase(Ease.Linear);
            });
        });
    }
}
