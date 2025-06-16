using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DG.Tweening;

public class ChessPiece : MonoBehaviour
{
    public enum PieceType
    {
        None,
        Pawn,
        Rook,
        Knight,
        Bishop,
        Queen,
        King
    }

    public enum Player
    {
        None,
        White,
        Black
    }

    public PieceType type;
    public Player player;
    public Vector2Int position;
    public ChessPiece destroyPiece;
    public int x, z;
    public int chessPoint;
    public List<Moving> move;
    public List<Vector2Int> getValidMoves;
    public ChessControl chessControl, enemyChessControl;

    public ChessPiece(PieceType type, Player player, Vector2Int position)
    {
        this.type = type;
        this.player = player;
        this.position = position;
    }

    public virtual bool Highlited(bool active)
    {
        return active;
    }

    public virtual void Move(Vector2Int destination) 
    {
        //for (int i = 0; i < ServerControl.Instance.chessPlayers.Count; i++)
        //{
        //    if (ServerControl.Instance.chessPlayers[i].GetPhotonView().IsMine)
        //    {
        //        chessControl = ServerControl.Instance.chessPlayers[i].GetComponent<ChessControl>();
        //    }
        //    else
        //    {
        //        enemyChessControl = ServerControl.Instance.chessPlayers[i].GetComponent<ChessControl>();
        //    }
        //}
    }

    public virtual bool Algorithm(Vector2Int destination)
    {
        // Burada genel hareket kurallarý uygulanabilir
        // Örneðin, taþýn pozisyonunu güncelleyebilir ve true döndürebilirsiniz.
        // Ancak, her taþýn kendine özgü kurallarý olduðu için bu fonksiyon genellikle alt sýnýflar tarafýndan ezilir.
        return false;
    }

    public virtual void Kill() 
    {
        if (destroyPiece.chessPoint != 1000)
        {
            enemyChessControl.chessPoint -= destroyPiece.chessPoint;
        }
        else
        {
            enemyChessControl.chessPoint = 0;
        }
        if (enemyChessControl.GetComponent<PhotonView>().IsMine)
        {
            UIManager.Instance.myPoint.text = enemyChessControl.chessPoint.ToString();
        }
        else
        {
            UIManager.Instance.enemyPoint.text = enemyChessControl.chessPoint.ToString();
        }
        Destroy(destroyPiece.GetComponent<ChessPiece>());
        destroyPiece.transform.DOMove(chessControl.destroyPieces[chessControl.destroyIndex], .5f).SetEase(Ease.Linear);
        destroyPiece.gameObject.layer = 0;
        chessControl.destroyIndex++;
        if (destroyPiece.type == PieceType.King)
        {
            chessControl.finish = true;
            enemyChessControl.finish = true;
            if (!chessControl.GetComponent<PhotonView>().IsMine)
            {
                UIManager.Instance.WarningText("Lose");
                JsonSave.json.save.lose++;
            }
            else
            {
                UIManager.Instance.WarningText("Win");
                JsonSave.json.save.win++;
            }
            int result = JsonSave.json.save.win - JsonSave.json.save.lose - 1 <= 0 ? 0 : JsonSave.json.save.win - JsonSave.json.save.lose - 1;
            JsonSave.json.save.level = (int)result / 10;
            SaveManager.Save(JsonSave.json.save);
            UIManager.Instance.back.gameObject.SetActive(true);
        }
    }
    public virtual void PawnToQueen(ChessControl chessControl) { }
    public virtual void ChangePlayer()
    {
        if (ServerControl.Instance.playerTurn == ServerControl.Player.Black)
        {
            ServerControl.Instance.playerTurn = ServerControl.Player.White;
            UIManager.Instance.playerTurn.text = "White";
        }
        else
        {
            ServerControl.Instance.playerTurn = ServerControl.Player.Black;
            UIManager.Instance.playerTurn.text = "Black";
        }
        ServerControl.Instance.chess.GetComponent<ChessControl>().movable = false;
        UIManager.Instance.time = 60;
    }
}

[System.Serializable]
public class Moving
{
    public int x;
    public int z;
}