using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSave : MonoBehaviour
{
    [SerializeField] GameUIManager gameUIManager;
    [SerializeField] TextMeshProUGUI coinText;
    [SerializeField] Text healthText;
    [SerializeField] List<Image> stars;
    public SaveVariables sv;
    public static GameSave gameSave;
    Difficulty difficulty;
    int starCount;
    void Awake()
    {
        gameSave = this;
    }
    void Start()
    {
        sv = SaveManager.Load();
        difficulty = FindAnyObjectByType<Difficulty>();
        HealthUpdate();
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
                int fullReward = ((difficulty.difficult / 2) * 1 * 10);
                if (sv.starCounts[(difficulty.difficult / 2) - 1] < 1)
                {
                    sv.starCounts[(difficulty.difficult / 2) - 1] = 1;
                    sv.coin += fullReward;
                }
                else
                {
                    sv.coin += fullReward / 10;
                }
                starCount = 1;
            }
            else if (gameUIManager.time > 300 && gameUIManager.time <= 600)
            {
                int fullReward = ((difficulty.difficult / 2) * 2 * 10);
                if (sv.starCounts[(difficulty.difficult / 2) - 1] < 2)
                {
                    sv.starCounts[(difficulty.difficult / 2) - 1] = 2;
                    sv.coin += fullReward;
                }
                else
                {
                    sv.coin += fullReward / 10;
                }
                starCount = 2;
            }
            else if (gameUIManager.time > 600 && gameUIManager.time <= 900)
            {
                int fullReward = ((difficulty.difficult / 2) * 3 * 10);
                if (sv.starCounts[(difficulty.difficult / 2) - 1] < 3)
                {
                    sv.starCounts[(difficulty.difficult / 2) - 1] = 3;
                    sv.coin += fullReward;
                }
                else
                {
                    sv.coin += fullReward / 10;
                }
                starCount = 3;
            }

            SaveManager.Save(sv);
            for (int i = 0; i < starCount; i++)
            {
                stars[i].color = new Color(1, 1, 1, 1); ;
            }
        }
        CoinUpdate();
    }
    void CoinUpdate()
    {
        CultureInfo turkceKultur = new CultureInfo("tr-TR");
        string formatToCoin = sv.coin.ToString("N0", turkceKultur);
        coinText.text = formatToCoin;
    }
    public void HealthUpdate()
    {
        Debug.Log(sv.health + "" + healthText.text);
        healthText.text = sv.health.ToString();
    }
}
