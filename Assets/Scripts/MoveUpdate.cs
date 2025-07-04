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
    public Piecetype piecetype;
    public enum Piecetype
    {
        White,
        Black
    }
    void Start()
    {
        parent = transform.parent;
        transform.parent = null;

        chessPoints = FindAnyObjectByType<ChessPoints>();
        audioManager = FindAnyObjectByType<AudioManager>();
        if (character.GetComponent<MeshRenderer>().materials[0].color == Color.white)
        {
            piecetype = Piecetype.White;
            chessPoints.whitePoints += piecePoint;
        }
        else
        {
            piecetype = Piecetype.Black;
            chessPoints.blackPoints += piecePoint;
        }
        chessPoints.whitePointText.text = "White Points: " + chessPoints.whitePoints;
        chessPoints.blackPointText.text = "Black Points: " + chessPoints.blackPoints;
    }

    // Update is called once per frame
    void Update()
    {   
        
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
                        if (piecetype == Piecetype.White)
                        {
                            FindAnyObjectByType<GameUIManager>().GameoverMenuOpen("White Win");
                        }
                        else
                        {
                            FindAnyObjectByType<GameUIManager>().GameoverMenuOpen("Black Win");
                        }
                    }
                });
            });
        });
    }
    public void PieceDestroy()
    {
        audioManager.Hit();
        if (piecetype == Piecetype.White)
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
