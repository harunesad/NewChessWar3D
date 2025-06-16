using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JsonSave : MonoBehaviour
{
    public static JsonSave json;
    public SaveObject save;
    private void Awake()
    {
        json = this;
    }
    void Start()
    {
        save = SaveManager.Load();
    }
}
