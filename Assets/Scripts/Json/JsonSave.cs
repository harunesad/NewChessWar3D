using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JsonSave : MonoBehaviour
{
    [SerializeField] List<Stars> stars;
    [SerializeField] List<int> newStarCounts;
    [SerializeField] List<bool> newUnlock;
    public static JsonSave jsonSave;
    public SaveVariables sv;
    private void Awake()
    {
        jsonSave = this;
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
        }
        else
        {
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
    }
}
[Serializable]
public class Stars
{
    public List<Image> star;
}