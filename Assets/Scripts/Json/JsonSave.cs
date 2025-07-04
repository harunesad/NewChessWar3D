using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class JsonSave : MonoBehaviour
{
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

        }
        else
        {
            sv.save = true;
            SaveManager.Save(sv);
        }
    }
}
