using UnityEngine;

public enum ChessPieceType
{
    King,
    Queen,
    Bishop,
    Knight,
    Rook,
    Pawn
}

[CreateAssetMenu(fileName = "NewSkin", menuName = "Chess/Skin")]
public class ChessSkinData : ScriptableObject
{
    public string skinName;
    public int price;
    public Sprite icon;

    [Header("Piece Prefabs")]
    public GameObject kingPrefab;
    public GameObject queenPrefab;
    public GameObject bishopPrefab;
    public GameObject knightPrefab;
    public GameObject rookPrefab;
    public GameObject pawnPrefab;

    public GameObject GetPrefab(ChessPieceType type)
    {
        switch (type)
        {
            case ChessPieceType.King: return kingPrefab;
            case ChessPieceType.Queen: return queenPrefab;
            case ChessPieceType.Bishop: return bishopPrefab;
            case ChessPieceType.Knight: return knightPrefab;
            case ChessPieceType.Rook: return rookPrefab;
            case ChessPieceType.Pawn: return pawnPrefab;
            default: return null;
        }
    }
}
