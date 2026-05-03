using UnityEngine;
using System.Collections.Generic;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    public const string TOWER_REACHED_KEY = "TowerLevelReached";
    public const string IS_TOWER_MODE_KEY = "IsTowerMode";
    public const string SELECTED_LEVEL_INDEX = "SelectedTowerLevel";

    public List<TowerLevelData> levels = new List<TowerLevelData>();

    [System.Serializable]
    public class TowerLevelList { public List<TowerLevelEntry> levels; }
    
    [System.Serializable]
    public class TowerLevelEntry
    {
        public int id;
        public string fen;
        public string desc;
        public int reward;
        public int diff;
        public bool isBlack;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLevelsFromJSON();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadLevelsFromJSON()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("levels");
        if (jsonFile == null)
        {
            Debug.LogError("Tower Levels JSON not found in Resources!");
            return;
        }

        TowerLevelList data = JsonUtility.FromJson<TowerLevelList>(jsonFile.text);
        levels.Clear();

        foreach (var entry in data.levels)
        {
            TowerLevelData level = ScriptableObject.CreateInstance<TowerLevelData>();
            level.levelNumber = entry.id;
            level.fenString = entry.fen;
            level.levelDescription = entry.desc;
            level.coinReward = entry.reward;
            level.difficulty = entry.diff;
            level.isBlackLevel = entry.isBlack;
            levels.Add(level);
        }
        Debug.Log("Loaded " + levels.Count + " Tower Levels from JSON.");
    }

    public int GetReachedLevel()
    {
        return PlayerPrefs.GetInt(TOWER_REACHED_KEY, 1);
    }

    public void MarkLevelCompleted(int levelNum)
    {
        int reached = GetReachedLevel();
        if (levelNum == reached)
        {
            PlayerPrefs.SetInt(TOWER_REACHED_KEY, reached + 1);
            PlayerPrefs.Save();
        }
    }

    public TowerLevelData GetLevelData(int index)
    {
        if (index >= 0 && index < levels.Count)
            return levels[index];
        return null;
    }

    public void StartTowerLevel(int index)
    {
        PlayerPrefs.SetInt(IS_TOWER_MODE_KEY, 1);
        PlayerPrefs.SetInt(SELECTED_LEVEL_INDEX, index);
        
        TowerLevelData data = GetLevelData(index);
        if (data != null)
        {
            // Seviyenin zorluk değerini Difficulty scriptine uygula
            // GameChanger bu değeri sahnede otomatik okuyacak
            Difficulty diff = FindAnyObjectByType<Difficulty>();
            if (diff != null)
            {
                // diff değeri 1-5 arası, GameChanger'ın beklediği format: (seçim+1)*2
                // Biz doğrudan levels.json'daki difficulty değerini kullanıyoruz (2-10 arası)
                diff.difficult = Mathf.Clamp(data.difficulty * 2, 2, 10);
            }

            // Load appropriate scene based on side
            UnityEngine.SceneManagement.SceneManager.LoadScene(data.isBlackLevel ? 1 : 2);
        }
    }

    public void ExitTowerMode()
    {
        PlayerPrefs.SetInt(IS_TOWER_MODE_KEY, 0);
    }
}
