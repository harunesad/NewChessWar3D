using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Queen : ChessPiece
{
    public bool hasMoved; // Vezirin daha önce hareket edip etmediðini gösteren bir özellik

    public Queen(Player player, Vector2Int position) : base(PieceType.Queen, player, position)
    {
        hasMoved = false; // Baþlangýçta vezir henüz hareket etmemiþtir
    }

    private void Start()
    {
        for (int i = 0; i < 28; i++)
        {
            if (i < 7)
            {
                move[i].x = i + 1;
                move[i].z = i + 1;
            }
            else if (i >= 7 && i < 14)
            {
                move[i].x = -i + 6;
                move[i].z = i - 6;
            }
            else if (i >= 14 && i < 21)
            {
                move[i].x = i - 13;
                move[i].z = -i + 13;
            }
            else if (i >= 21 && i < 28)
            {
                move[i].x = -i + 20;
                move[i].z = -i + 20;
            }
        }
        for (int i = 28; i < 56; i++)
        {
            int j = i - 28;
            if (j < 7)
            {
                move[i].z = j + 1;
            }
            else if (j >= 7 && j < 14)
            {
                move[i].z = -j + 6;
            }
            else if (j >= 14 && j < 21)
            {
                move[i].x = j - 13;
            }
            else if (j >= 21 && j < move.Count)
            {
                move[i].x = -j + 20;
            }
        }
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
        bool highlete = false;
        for (int i = 0; i < move.Count; i++)
        {
            if (z + move[i].z <= 7 && z + move[i].z >= 0 && x + move[i].x <= 7 && x + move[i].x >= 0)
            {
                if (!highlete && ChessBoard.board[z + move[i].z, x + move[i].x] == null)
                {
                    getValidMoves.Add(new Vector2Int(z + move[i].z, x + move[i].x));
                    Transform zHighleted = ChessBoard.boardObject[x + move[i].x, z + move[i].z].transform.GetChild(0);
                    zHighleted.GetComponent<Renderer>().material.color = Color.green;
                    zHighleted.gameObject.SetActive(active);
                }
                else if (!highlete && ChessBoard.board[z + move[i].z, x + move[i].x] != null)
                {
                    highlete = true;
                    if (ChessBoard.board[z + move[i].z, x + move[i].x].player != player)
                    {
                        getValidMoves.Add(new Vector2Int(z + move[i].z, x + move[i].x));
                        Transform zHighleted = ChessBoard.boardObject[x + move[i].x, z + move[i].z].transform.GetChild(0);
                        zHighleted.GetComponent<Renderer>().material.color = Color.red;
                        zHighleted.gameObject.SetActive(active);
                    }
                }
            }
            if ((i + 1) % 7 == 0)
            {
                highlete = false;
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

    // Vezir hareket kurallarý
    public override bool Algorithm(Vector2Int destination)
    {
        ChessBoard.board[z, x] = null;
        // Vezir, yatay, dikey veya çapraz yönde hareket edebilir
        if ((destination.x == position.x || destination.y == position.y || Mathf.Abs(destination.x - position.x) == Mathf.Abs(destination.y - position.y)))
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