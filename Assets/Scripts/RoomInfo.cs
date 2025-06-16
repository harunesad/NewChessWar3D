using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RoomInfo : MonoBehaviour
{
    public TextMeshProUGUI player1Nickname, player2Nickname, player1Type, player2Type;
    public Button join, leave;
    void Start()
    {
        join.onClick.AddListener(delegate { Join(int.Parse(name.Substring(name.Length - 1, 1))); });
        leave.onClick.AddListener(delegate { Leave(int.Parse(name.Substring(name.Length - 1, 1))); });
    }
    void Update()
    {
        //if (player1Nickname.text.Length > 0 && player2Nickname.text.Length > 0)
        //{

        //}
    }
    void Join(int room)
    {
        ServerControl.Instance.lobby.GetComponent<LobbyControl>().JoinRoomPun(room);
    }
    void Leave(int room)
    {
        ServerControl.Instance.lobby.GetComponent<LobbyControl>().LeaveRoomPun(room);
    }
}
