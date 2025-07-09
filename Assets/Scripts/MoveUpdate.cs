using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class MoveUpdate : MonoBehaviour
{
    [SerializeField] GameObject character;
    [SerializeField] int piecePoint;
    Transform parent;
    ChessPoints chessPoints;
    AudioManager audioManager;
    Difficulty difficulty;
    GameSave gameSave;
    public Difficulty.Type piecetype;
    void Start()
    {
        Invoke("ParentEmpty", 1);

        chessPoints = FindAnyObjectByType<ChessPoints>();
        audioManager = FindAnyObjectByType<AudioManager>();
        difficulty = FindAnyObjectByType<Difficulty>();
        gameSave = FindAnyObjectByType<GameSave>();

        if (character.GetComponent<MeshRenderer>().materials[0].color == Color.white)
        {
            piecetype = Difficulty.Type.White;
            chessPoints.whitePoints += piecePoint;
        }
        else
        {
            piecetype = Difficulty.Type.Black;
            chessPoints.blackPoints += piecePoint;
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
        transform.DOMoveY(transform.position.y + 1, .2f).SetEase(Ease.Linear).OnComplete(() =>
        {
            Vector3 newPos = new Vector3(parent.position.x, transform.position.y, parent.position.z);
            audioManager.Move();
            transform.DOMove(newPos, .75f).SetEase(Ease.Linear).OnComplete(() =>
            {
                transform.DOMoveY(transform.position.y - 1, .2f).SetEase(Ease.Linear).OnComplete(() =>
                {
                    if (FindAnyObjectByType<GameUIManager>().gameFinish)
                    {
                        if (piecetype == Difficulty.Type.White)
                        {
                            difficulty.winType = Difficulty.Type.White;
                            gameSave.ChessSave();
                            FindAnyObjectByType<GameUIManager>().GameoverMenuOpen("White Win");
                        }
                        else
                        {
                            difficulty.winType = Difficulty.Type.Black;
                            gameSave.ChessSave();
                            FindAnyObjectByType<GameUIManager>().GameoverMenuOpen("Black Win");
                        }
                        //if (piecetype == difficulty.type)
                        //{

                        //}
                        //else
                        //{

                        //}
                    }
                });
            });
        });
    }
    public void PieceDestroy()
    {
        audioManager.Hit();
        if (piecetype == Difficulty.Type.White)
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
