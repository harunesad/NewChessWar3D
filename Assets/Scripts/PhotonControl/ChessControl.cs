using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessControl : MonoBehaviour
{
    PhotonView myPhotonView;
    RaycastHit hit;
    public List<ChessPiece> myChessPieces;
    public bool finish;
    [SerializeField]
    LayerMask boardLayer;
    public LayerMask myLayer;
    public bool movable;
    GameObject piece, oldPiece;
    public Player player;
    public List<Vector3> destroyPieces;
    public int destroyIndex;
    public int chessPoint;
    public enum Player
    {
        White,
        Black
    }
    private void Awake()
    {
        myPhotonView = GetComponent<PhotonView>();
    }

    void Start()
    {
        if (!myPhotonView.IsMine)
        {
            UIManager.Instance.enemyNickname.text = myPhotonView.Owner.NickName;
        }
        ServerControl.Instance.chessPlayers.Add(gameObject);
    }
    void Update()
    {
        if (myPhotonView.IsMine)
        {
            if (finish)
            {
                return;
            }
            if (Input.GetMouseButtonDown(0) && ServerControl.Instance.myPlayer == ServerControl.Instance.playerTurn && !movable)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                InputPiece(ray, myLayer);
            }
            if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
            {
                UIManager.Instance.TimeReduce();
            }
        }
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
            if ((color == Color.red || color == Color.green))
            {
                int index = 0;
                for (int i = 0; i < myChessPieces.Count; i++)
                {
                    if (myChessPieces[i] != null && piece.gameObject == myChessPieces[i].gameObject)
                    {
                        index = i;
                        break;
                    }
                }
                piece = null;
                movable = true;
                myPhotonView.RPC("ChessMovement", RpcTarget.All, index, hit.transform.position.x, hit.transform.position.z);
            }
        }
    }
    [PunRPC]
    void ChessMovement(int index, float posX, float posZ)
    {
        Debug.Log(index);
        ChessPiece chessPiece = myChessPieces[index];
        chessPiece.Move(new Vector2Int((int)posX, (int)posZ));
    }
}
