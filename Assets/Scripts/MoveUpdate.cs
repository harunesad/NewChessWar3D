using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class MoveUpdate : MonoBehaviour
{
    [SerializeField] List<MaterialChange> materialChange;
    [SerializeField] GameObject character;
    [SerializeField] int piecePoint;
    [SerializeField] bool white;
    [SerializeField] Material whiteMat, blackMat;
    Transform parent;
    ChessPoints chessPoints;
    AudioManager audioManager;
    GameSave gameSave;
    void Start()
    {
        chessPoints = FindAnyObjectByType<ChessPoints>();
        audioManager = FindAnyObjectByType<AudioManager>();
        gameSave = FindAnyObjectByType<GameSave>();

        if (character.GetComponent<MeshRenderer>().materials[0].color == whiteMat.color)
        {
            white = true;
            chessPoints.whitePoints += piecePoint;
            for (int i = 0; i < gameSave.sv.items.Count; i++)
            {
                if (gameSave.sv.items[i].white && gameSave.sv.items[i].selected)
                {
                    character.GetComponent<MeshRenderer>().materials[0].color = chessPoints.piecesColors[i];
                }
            }
        }
        else
        {
            white = false;
            chessPoints.blackPoints += piecePoint;
            for (int i = 0; i < gameSave.sv.items.Count; i++)
            {
                if (!gameSave.sv.items[i].white && gameSave.sv.items[i].selected)
                {
                    character.GetComponent<MeshRenderer>().materials[0].color = chessPoints.piecesColors[i];
                }
            }
        }
        chessPoints.whitePointText.text = "White Points: " + chessPoints.whitePoints;
        chessPoints.blackPointText.text = "Black Points: " + chessPoints.blackPoints;
        for (int i = 0; i < materialChange.Count; i++)
        {
            materialChange[i].Change();
        }
    }
}
