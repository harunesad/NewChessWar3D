using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameSave : MonoBehaviour
{
    [SerializeField] GameUIManager gameUIManager;
    [SerializeField] SaveVariables sv;
    [SerializeField] List<Image> stars;
    Difficulty difficulty;
    int starCount;
    void Start()
    {
        sv = SaveManager.Load();
        difficulty = FindAnyObjectByType<Difficulty>();
    }
    public void ChessSave()
    {
        if (PlayerPrefs.GetString("WinType") == PlayerPrefs.GetString("Type"))
        {
            if (difficulty.difficult / 2 < 10)
            {
                sv.unlock[difficulty.difficult / 2] = true;
            }
            if (gameUIManager.time > 0 &&gameUIManager.time <= 300)
            {
                if (sv.starCounts[(difficulty.difficult / 2) - 1] < 1)
                {
                    sv.starCounts[(difficulty.difficult / 2) - 1] = 1;
                }
                starCount = 1;
            }
            else if (gameUIManager.time > 300 && gameUIManager.time <= 600)
            {
                if (sv.starCounts[(difficulty.difficult / 2) - 1] < 2)
                {
                    sv.starCounts[(difficulty.difficult / 2) - 1] = 2;
                }
                starCount = 2;
            }
            else if (gameUIManager.time > 600 && gameUIManager.time <= 900)
            {
                if (sv.starCounts[(difficulty.difficult / 2) - 1] < 3)
                {
                    sv.starCounts[(difficulty.difficult / 2) - 1] = 3;
                }
                starCount = 3;
            }
            Debug.Log(starCount);
            for (int i = 0; i < starCount; i++)
            {
                stars[i].color= new Color(1, 1, 1, 1); ;
            }
            SaveManager.Save(sv);
        }
    }
}
