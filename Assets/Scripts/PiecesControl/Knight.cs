using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Knight : ChessPiece
{
    public bool hasMoved; // Atýn daha önce hareket edip etmediðini gösteren bir özellik
    
    public Knight(Player player, Vector2Int position) : base(PieceType.Knight, player, position)
    {
        hasMoved = false; // Baþlangýçta atlar henüz hareket etmemiþtir
    }

    public override bool Highlited(bool active)
    {
        Transform highleted = ChessBoard.boardObject[x, z].transform.GetChild(0);
        highleted.GetComponent<Renderer>().material.color = Color.blue;
        highleted.gameObject.SetActive(active);

        if (getValidMoves.Count > 0)
        {
            getValidMoves.Clear();
        }
        for (int i = 0; i < move.Count; i++)
        {
            if (z + move[i].z <= 7 && z + move[i].z >= 0 && x + move[i].x <= 7 && x + move[i].x >= 0)
            {
                if (ChessBoard.board[z + move[i].z, x + move[i].x] == null)
                {
                    getValidMoves.Add(new Vector2Int(z + move[i].z, x + move[i].x));
                    Transform zHighleted = ChessBoard.boardObject[x + move[i].x, z + move[i].z].transform.GetChild(0);
                    zHighleted.GetComponent<Renderer>().material.color = Color.green;
                    zHighleted.gameObject.SetActive(active);
                }
                else if(ChessBoard.board[z + move[i].z, x + move[i].x] != null && ChessBoard.board[z + move[i].z, x + move[i].x].player != player)
                {
                    getValidMoves.Add(new Vector2Int(z + move[i].z, x + move[i].x));
                    Transform zHighleted = ChessBoard.boardObject[x + move[i].x, z + move[i].z].transform.GetChild(0);
                    zHighleted.GetComponent<Renderer>().material.color = Color.red;
                    zHighleted.gameObject.SetActive(active);
                }
            }
        }

        return active;
    }

    public override void Move(Vector2Int destination)
    {
        Highlited(false);

        base.Move(destination);

        transform.DOMove(new Vector3(destination.x, transform.position.y, destination.y), 1).SetEase(Ease.Linear).OnComplete(() =>
        {
            Algorithm(destination);
            if (ChessBoard.board[z, x] != null && ChessBoard.board[z, x].player != player)
            {
                destroyPiece = ChessBoard.board[z, x];
                Kill();
            }
            else
            {
                ChessBoard.board[z, x] = GetComponent<ChessPiece>();
                destroyPiece = null;
            }
            ChangePlayer();
        });
    }

    // Atýn hareket kurallarý
    public override bool Algorithm(Vector2Int destination)
    {
        ChessBoard.board[z, x] = null;

        // At, L þeklinde hareket eder
        int dx = Mathf.Abs(destination.x - position.x);
        int dy = Mathf.Abs(destination.y - position.y);

        if ((dx == 2 && dy == 1) || (dx == 1 && dy == 2))
        {
            position = destination;
            z = position.y;
            x = position.x;
            hasMoved = true;
            return true;
        }

        return false;
    }

    public override void Kill()
    {
        base.Kill();
        ChessBoard.board[z, x] = GetComponent<ChessPiece>();
    }
    public override void ChangePlayer()
    {
        base.ChangePlayer();
    }
}