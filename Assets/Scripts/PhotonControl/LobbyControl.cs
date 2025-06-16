using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class LobbyControl : MonoBehaviour
{
    PhotonView myPhotonView;
    int joinRoom;
    bool join;
    public Player player;
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
        ServerControl.Instance.players.Add(gameObject);
        ServerControl.Instance.playerNames.Add(myPhotonView.Owner.NickName);
        InvokeRepeating("ConnectionControl", 1, 1);
    }
    void Update()
    {

    }
    void ConnectionControl()
    {
        if (myPhotonView.IsMine)
        {
            //ServerControl.Instance.playerNames.Clear();
            //for (int i = 0; i < PhotonNetwork.PlayerList.Count(); i++)
            //{
            //    ServerControl.Instance.playerNames.Add(PhotonNetwork.PlayerList[i].NickName);
            //}
            //for (int i = 0; i < UIManager.Instance.nicknames.Count; i++)
            //{
            //    if (!ServerControl.Instance.playerNames.Contains(UIManager.Instance.nicknames[i].text))
            //    {
            //        UIManager.Instance.nicknames[i].text = "";
            //        UIManager.Instance.playerTypes[i].text = "";
            //    }
            //}
            if (UIManager.Instance.roomId > 0)
            {
                bool connect = false;
                RoomInfo roomInfo = UIManager.Instance.chessRoom.transform.GetChild(UIManager.Instance.roomId - 1).GetComponent<RoomInfo>();
                TextMeshProUGUI nickname1 = roomInfo.player1Nickname;
                TextMeshProUGUI nickname2 = roomInfo.player2Nickname;
                if (nickname1.text.Length == 0)
                {
                    connect = true;
                }
                if (nickname2.text.Length == 0)
                {
                    connect = true;
                }
                if (!connect)
                {
                    StartCoroutine(WaitLeaveRoom());
                }
            }
        }
    }
    IEnumerator WaitLeaveRoom()
    {
        yield return new WaitForSeconds(1.1f);
        UIManager.Instance.environment.SetActive(false);
        UIManager.Instance.LoadingStart(UIManager.Instance.gameLoading.gameObject);
        PhotonNetwork.LeaveRoom();
        ServerControl.Instance.step++;
        UIManager.Instance.chessRoom.SetActive(false);
        UIManager.Instance.back.gameObject.SetActive(false);
        ServerControl.Instance.players.Clear();
        ServerControl.Instance.playerNames.Clear();
    }
    public void JoinRoomPun(int room)
    {
        if (myPhotonView.IsMine && !join)
        {
            join = true;
            UIManager.Instance.roomId = room;
            myPhotonView.RPC("JoinRoomAll", RpcTarget.AllBuffered, room, UIManager.Instance.nickname);
            if (player == Player.White)
            {
                ServerControl.Instance.myPlayer = ServerControl.Player.White;
            }
            else
            {
                ServerControl.Instance.myPlayer = ServerControl.Player.Black;
            }
        }
    }
    [PunRPC]
    void JoinRoomAll(int room, string nickname)
    {
        joinRoom = room;
        RoomInfo roomInfo = UIManager.Instance.chessRoom.transform.GetChild(joinRoom - 1).GetComponent<RoomInfo>();
        TextMeshProUGUI nickname1 = roomInfo.player1Nickname;
        TextMeshProUGUI nickname2 = roomInfo.player2Nickname;
        TextMeshProUGUI type1 = roomInfo.player1Type;
        TextMeshProUGUI type2 = roomInfo.player2Type;
        Debug.Log(nickname);
        if (nickname1.text == nickname || nickname2.text == nickname)
        {
            return;
        }
        if (nickname1.text.Length == 0)
        {
            nickname1.text = myPhotonView.Owner.NickName;
            type1.text = "White";
            player = Player.White;
        }
        else if (nickname2.text.Length == 0)
        {
            nickname2.text = myPhotonView.Owner.NickName;
            type2.text = "Black";
            player = Player.Black;
        }
    }
    public void LeaveRoomPun(int room)
    {
        if (myPhotonView.IsMine && join && joinRoom == room)
        {
            myPhotonView.RPC("LeaveRoomAll", RpcTarget.AllBuffered, room);
            join = false;
        }
    }
    [PunRPC]
    void LeaveRoomAll(int room)
    {
        joinRoom = room;
        RoomInfo roomInfo = UIManager.Instance.chessRoom.transform.GetChild(joinRoom - 1).GetComponent<RoomInfo>();
        TextMeshProUGUI nickname1 = roomInfo.player1Nickname;
        TextMeshProUGUI nickname2 = roomInfo.player2Nickname;
        TextMeshProUGUI type1 = roomInfo.player1Type;
        TextMeshProUGUI type2 = roomInfo.player2Type;
        if (nickname1.text == myPhotonView.Owner.NickName)
        {
            nickname1.text = "";
            type1.text = "";
        }
        else if (nickname2.text == myPhotonView.Owner.NickName)
        {
            nickname2.text = "";
            type2.text = "";
        }
    }
}
