using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    RaycastHit hit;
    [SerializeField]
    LayerMask whitePieceLayer, blackPieceLayer, boardLayer;
    [SerializeField]
    ChessBoard board;
    GameObject piece, oldPiece;
    public bool movable;
    [SerializeField]
    ChessAI chessAI;
    public Player players;
    public enum Player
    {
        White,
        Black
    }
    void Start()
    {

    }
    void Update()
    {
        //if (Input.GetMouseButtonDown(0) && ServerControl.Instance.myPlayer == ServerControl.Player.White && !movable)
        //{
        //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //    InputPiece(ray, whitePieceLayer);
        //}
        //else if (Input.GetMouseButtonDown(0) && ServerControl.Instance.myPlayer == ServerControl.Player.Black && !movable)
        //{
        //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //    InputPiece(ray, blackPieceLayer);
        //}
        //else if (ai)
        //{
        //    Debug.Log(ai);
        //    chessAI.MakeBestMove();
        //    ai = false;
        //}
    }
    void InputPiece(Ray ray, LayerMask pieceLayer)
    {
        if (Physics.Raycast(ray, out hit, 100, pieceLayer))
        {
            if (piece != null)
            {
                if (piece.TryGetComponent(out ChessPiece oldChessPiece))
                {
                    oldPiece = piece;
                    oldChessPiece.Highlited(false);
                }
            }
            piece = hit.transform.gameObject;
            if (piece.TryGetComponent(out ChessPiece chessPiece))
            {
                if (piece != oldPiece)
                {
                    chessPiece.Highlited(true);
                }
                else
                {
                    oldPiece = null;
                    piece = null;
                }
            }
        }
        else if (Physics.Raycast(ray, out hit, 100, boardLayer) && piece != null)
        {
            Color color = hit.transform.GetComponent<Renderer>().material.color;
            if (piece.TryGetComponent(out ChessPiece chessPiece) && (color == Color.red || color == Color.green))
            {
                chessPiece.Move(new Vector2Int((int)hit.transform.position.x, (int)hit.transform.position.z));
                piece = null;
                movable = true;
            }
        }

    }
}
