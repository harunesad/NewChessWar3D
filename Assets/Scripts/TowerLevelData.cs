using UnityEngine;

[CreateAssetMenu(fileName = "TowerLevel", menuName = "ChessWar/Tower Level")]
public class TowerLevelData : ScriptableObject
{
    public int levelNumber;
    public string fenString;
    public Sprite levelPreviewImage;
    [TextArea(3, 5)]
    public string levelDescription = "Checkmate in 1 move!";
    public int coinReward = 100;
    public int difficulty = 1; // 1 to 5
    public bool isBlackLevel = false; // If true, loads HarunChessBlackAI scene
}
