using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public bool hasMoved; // Piyonun daha önce hareket edip etmediðini gösteren bir özellik

    public Pawn(Player player, Vector2Int position) : base(PieceType.Pawn, player, position)
    {
        hasMoved = false; // Baþlangýçta piyonlar henüz hareket etmemiþtir
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
        bool firstMove = false;
        for (int i = 0; i < move.Count; i++)
        {
            if (z + move[i].z <= 7 && z + move[i].z >= 0 && x + move[i].x <= 7 && x + move[i].x >= 0)
            {
                if (ChessBoard.board[z + move[i].z, x + move[i].x] == null && i == 0)
                {
                    getValidMoves.Add(new Vector2Int(z + move[i].z, x + move[i].x));
                    firstMove = true;
                    Transform zHighleted = ChessBoard.boardObject[x + move[i].x, z + move[i].z].transform.GetChild(0);
                    zHighleted.GetComponent<Renderer>().material.color = Color.green;
                    zHighleted.gameObject.SetActive(active);
                }
                if (firstMove && !hasMoved && ChessBoard.board[z + move[i].z, x + move[i].x] == null && i == 1)
                {
                    getValidMoves.Add(new Vector2Int(z + move[i].z, x + move[i].x));
                    Transform zHighleted = ChessBoard.boardObject[x + move[i].x, z + move[i].z].transform.GetChild(0);
                    zHighleted.GetComponent<Renderer>().material.color = Color.green;
                    zHighleted.gameObject.SetActive(active);
                }
                if (ChessBoard.board[z + move[i].z, x + move[i].x] != null && ChessBoard.board[z + move[i].z, x + move[i].x].player != player && (i == 2 || i == 3))
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
            }
            if (destination.y == 7 || destination.y == 0)
            {
                PawnToQueen(chessControl);
            }
            ChangePlayer();
        });
    }

    // Piyonun hareket kurallarý
    public override bool Algorithm(Vector2Int destination)
    {
        ChessBoard.board[z, x] = null;
        int direction = (player == Player.White) ? 1 : -1; // Beyaz piyonlar ileri, siyah piyonlar geri hareket eder

        // Ýlk hamlede iki kare ileri, sonraki hamlelerde bir kare ileri
        if (!hasMoved && Mathf.Abs(destination.y - position.y) == 2 && destination.x == position.x)
        {
            position = destination;
            z = position.y;
            hasMoved = true;
            return true;
        }

        // Diagonal olarak rakip taþý yeme (yatayda bir kare, dikeyde bir kare)
        if (Mathf.Abs(destination.x - position.x) == 1 && destination.y - position.y == direction)
        {
            position = destination;
            z = position.y;
            x = position.x;
            hasMoved = true;
            return true;
        }

        // Ýleri doðru bir kare gitme
        if (destination.x == position.x && destination.y - position.y == direction)
        {
            position = destination;
            z = position.y;
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

    public override void PawnToQueen(ChessControl chessControl)
    {
        ChessBoard chessBoard = GameObject.Find("ChessBoard").GetComponent<ChessBoard>();
        if (player == Player.White)
        {
            ChessBoard.board[z, x] = chessBoard.CreateChessPiece(new Vector3(x, 0.2f, z), Player.White, chessBoard.queenPrefabWhite, x, z);
        }
        else
        {
            ChessBoard.board[z, x] = chessBoard.CreateChessPiece(new Vector3(x, 0.2f, z), Player.Black, chessBoard.queenPrefabBlack, x, z);
        }
        Destroy(gameObject);
    }
    public override void ChangePlayer()
    {
        base.ChangePlayer();
    }
}