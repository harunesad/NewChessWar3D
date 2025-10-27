using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class JsonSave : MonoBehaviour
{
    [SerializeField] List<Stars> stars;
    [SerializeField] List<int> newStarCounts;
    [SerializeField] List<bool> newUnlock;
    [SerializeField] Text coinText;
    ShopManager shopManager;
    public static JsonSave jsonSave;
    public SaveVariables sv;
    private void Awake()
    {
        jsonSave = this;
        shopManager = FindAnyObjectByType<ShopManager>();
    }
    void Start()
    {
        sv = SaveManager.Load();
        if (sv.save)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                for (int j = 0; j < sv.starCounts[i]; j++)
                {
                    stars[i].star[j].color = new Color(1, 1, 1, 1);
                }
            }
            shopManager.items = sv.items;
        }
        else
        {
            sv.items = shopManager.items;
            sv.starCounts = newStarCounts;
            sv.unlock = newUnlock;
            sv.save = true;
            SaveManager.Save(sv);
        }
        for (int i = 0; i < 10; i++)
        {
            if (!sv.unlock[i])
            {
                Button difficult = stars[i].star[0].transform.parent.GetComponent<Button>();
                var colors = difficult.colors;
                colors.normalColor = colors.disabledColor;
                colors.selectedColor = colors.disabledColor;
                difficult.colors = colors;
            }
        }
        CoinUpdate();
    }
    public void CoinUpdate()
    {
        CultureInfo turkceKultur = new CultureInfo("tr-TR");
        string formatToCoin = sv.coin.ToString("N0", turkceKultur);
        coinText.text = formatToCoin;
    }
}
[Serializable]
public class Stars
{
    public List<Image> star;
}