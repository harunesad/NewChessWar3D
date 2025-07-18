using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class MoveUpdate : MonoBehaviour
{
    [SerializeField] GameObject character;
    [SerializeField] int piecePoint;
    [SerializeField] bool white;
    Transform parent;
    ChessPoints chessPoints;
    AudioManager audioManager;
    GameSave gameSave;
    void Start()
    {
        Invoke("ParentEmpty", 1);

        chessPoints = FindAnyObjectByType<ChessPoints>();
        audioManager = FindAnyObjectByType<AudioManager>();
        gameSave = FindAnyObjectByType<GameSave>();

        if (character.GetComponent<MeshRenderer>().materials[0].color.r > .95f)
        {
            white = true;
            chessPoints.whitePoints += piecePoint;
            if (!chessPoints.rookWhite1 && transform.name.Contains("Rook"))
            {
                chessPoints.rookWhite1 = this;
            }
            else if (chessPoints.rookWhite1 && transform.name.Contains("Rook"))
            {
                chessPoints.rookWhite2 = this;
            }
        }
        else
        {
            white = false;
            chessPoints.blackPoints += piecePoint;
            if (!chessPoints.rookBlack1 && transform.name.Contains("Rook"))
            {
                chessPoints.rookBlack1 = this;
            }
            else if (chessPoints.rookBlack1 && transform.name.Contains("Rook"))
            {
                chessPoints.rookBlack2 = this;
            }
        }
        chessPoints.whitePointText.text = "White Points: " + chessPoints.whitePoints;
        chessPoints.blackPointText.text = "Black Points: " + chessPoints.blackPoints;
    }

    // Update is called once per frame
    void Update()
    {   
        
    }
    void ParentEmpty()
    {
        parent = transform.parent;
        transform.parent = null;
    }
    public void PieceMove()
    {
        if (Mathf.Abs(transform.position.x - parent.position.x) == 2 && transform.name.Contains("King"))
        {
            if (white)
            {
                if (chessPoints.rookWhite1.transform.position.x != chessPoints.rookWhite1.parent.position.x)
                {
                    chessPoints.rookWhite1.RookMove();
                }
                else if (chessPoints.rookWhite2.transform.position.x != chessPoints.rookWhite2.parent.position.x)
                {
                    chessPoints.rookWhite2.RookMove();
                }
            }
            else
            {
                if (chessPoints.rookBlack1.transform.position.x != chessPoints.rookBlack1.parent.position.x)
                {
                    chessPoints.rookBlack1.RookMove();
                }
                else if (chessPoints.rookBlack2.transform.position.x != chessPoints.rookBlack2.parent.position.x)
                {
                    chessPoints.rookBlack2.RookMove();
                }
            }
        }
        transform.DOMoveY(transform.position.y + 1, .15f).SetEase(Ease.Linear).OnComplete(() =>
        {
            Vector3 newPos = new Vector3(parent.position.x, transform.position.y, parent.position.z);
            audioManager.Move();
            transform.DOMove(newPos, .4f).SetEase(Ease.Linear).OnComplete(() =>
            {
                transform.DOMoveY(transform.position.y - 1, .15f).SetEase(Ease.Linear).OnComplete(() =>
                {
                    if (FindAnyObjectByType<GameUIManager>().gameFinish)
                    {
                        if (white == true)
                        {
                            PlayerPrefs.SetString("WinType", "White");
                            gameSave.ChessSave();
                            FindAnyObjectByType<GameUIManager>().GameoverMenuOpen("White Win");
                        }
                        else
                        {
                            PlayerPrefs.SetString("WinType", "Black");
                            gameSave.ChessSave();
                            FindAnyObjectByType<GameUIManager>().GameoverMenuOpen("Black Win");
                        }
                    }
                });
            });
        });
    }
    public void RookMove()
    {
        transform.DOMoveX(parent.position.x, .4f).SetEase(Ease.Linear);
    }
    public void PieceDestroy()
    {
        audioManager.Hit();
        if (white == true)
        {
            chessPoints.whitePoints -= piecePoint;
        }
        else
        {
            chessPoints.blackPoints -= piecePoint;
        }
        chessPoints.whitePointText.text = "White Points: " + chessPoints.whitePoints;
        chessPoints.blackPointText.text = "Black Points: " + chessPoints.blackPoints;
        Destroy(gameObject);
    }
}
