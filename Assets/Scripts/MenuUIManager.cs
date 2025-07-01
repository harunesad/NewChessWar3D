using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUIManager : MonoBehaviour
{
    [SerializeField] Difficulty difficulty;
    [SerializeField] Button chessPlayBtn, whiteBtn, blackBtn;
    [SerializeField] GameObject difficultMenu, mainMenu;
    [SerializeField] List<Button> difficultsBtn;
    void Start()
    {
        chessPlayBtn.onClick.AddListener(Difficultopen);

        whiteBtn.onClick.AddListener(WhiteSelect);
        blackBtn.onClick.AddListener(BlackSelect);

        for (int i = 0; i < difficultsBtn.Count; i++)
        {
            int j = i;
            difficultsBtn[i].onClick.AddListener(delegate { DifficultSelect(j); });
        }
    }
    void Difficultopen()
    {
        mainMenu.SetActive(false);
        difficultMenu.SetActive(true);
    }
    void WhiteSelect()
    {
        difficulty.type = Difficulty.Type.White;
    }
    void BlackSelect()
    {
        difficulty.type = Difficulty.Type.Black;
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
}
