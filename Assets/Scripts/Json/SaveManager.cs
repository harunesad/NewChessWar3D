using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public static class SaveManager
{
    public static string directory = "/SaveData/";
    public static string fileName = "SaveChess.json";
    public static void Save(SaveObject so)
    {
        string dir = Application.persistentDataPath + directory;

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string json = JsonUtility.ToJson(so);
        File.WriteAllText(dir + fileName, json);
        Debug.Log(dir);
    }
    public static SaveObject Load()
    {
        string fullPath = Application.persistentDataPath + directory + fileName;
        SaveObject so = new SaveObject();

        if (File.Exists(fullPath))
        {
            string json = File.ReadAllText(fullPath);
            so = JsonUtility.FromJson<SaveObject>(json);
        }
        else
        {
            Debug.Log("sadas");
        }
        return so;
    }
}
