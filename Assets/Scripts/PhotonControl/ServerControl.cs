using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ServerControl : MonoBehaviourPunCallbacks
{
    private static ServerControl _instance;

    public static ServerControl Instance
    {
        get
        {
            if (_instance == null)
            {
                // Eðer örnek oluþturulmamýþsa, yeni bir örnek oluþtur
                GameObject singletonObject = new GameObject("ServerControl");
                _instance = singletonObject.AddComponent<ServerControl>();
            }

            return _instance;
        }
    }
    public GameObject lobbyControl, lobby, chessControlWhite, chessControlBlack, chess;
    public List<GameObject> players, chessPlayers;
    public List<string> playerNames;
    public int step;
    public Player playerTurn, myPlayer;
    [SerializeField] LayerMask white, black;
    [SerializeField] ChessBoard chessBoard;
    [SerializeField] Transform whitePos, blackPos;
    public enum Player
    {
        White,
        Black
    }
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
    private void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
    }
    public override void OnConnectedToMaster()
    {
        //Servera baðlanýnca çalýþan callback fonksiyon
        Debug.Log("Servera baðlandý");
        if (step == 1)
        {
            PhotonNetwork.JoinLobby();
        }
    }
    public override void OnJoinedLobby()
    {
        Debug.Log("Lobiye baðlandý");
        if (step == 0)
        {
            RoomOptions roomOptions = new RoomOptions() { MaxPlayers = 18, IsOpen = true, IsVisible = true };
            PhotonNetwork.JoinOrCreateRoom("Lobby" + UIManager.Instance.levelId, roomOptions, TypedLobby.Default);
        }
        else if (step == 1)
        {
            RoomOptions roomOptions = new RoomOptions() { MaxPlayers = 2, IsOpen = true, IsVisible = true };
            PhotonNetwork.JoinOrCreateRoom("ChessRoom" + UIManager.Instance.levelId + UIManager.Instance.roomId, roomOptions, TypedLobby.Default);
        }
    }
    public override void OnJoinedRoom()
    {
        Debug.Log("Odaya baðlandý");
        if (step == 0)
        {
            UIManager.Instance.LoadingFinish(UIManager.Instance.levelLoading.gameObject);
            lobby = PhotonNetwork.Instantiate(lobbyControl.name, Vector3.zero, Quaternion.identity, (byte)lobbyControl.GetPhotonView().ViewID, null);
            lobby.GetPhotonView().Owner.NickName = UIManager.Instance.nickname;
        }
        else if (step == 1)
        {
            UIManager.Instance.LoadingFinish(UIManager.Instance.gameLoading.gameObject);
            if (myPlayer == Player.White)
            {
                chess = PhotonNetwork.Instantiate(chessControlWhite.name, Vector3.zero, Quaternion.identity, (byte)chessControlWhite.GetPhotonView().ViewID, null);
                Camera.main.transform.position = whitePos.position;
                Camera.main.transform.rotation = whitePos.rotation;
            }
            else
            {
                chess = PhotonNetwork.Instantiate(chessControlBlack.name, Vector3.zero, Quaternion.identity, (byte)chessControlBlack.GetPhotonView().ViewID, null);
                Camera.main.transform.position = blackPos.position;
                Camera.main.transform.rotation = blackPos.rotation;
                Camera.main.GetComponent<CamSwerve>().camPosIndex = 2;
            }
            chess.GetPhotonView().Owner.NickName = UIManager.Instance.nickname;
            StartCoroutine(ChessWait());
        }
    }
    IEnumerator ChessWait()
    {
        yield return new WaitForSeconds(2);
        UIManager.Instance.myPoint.text = "1210";
        UIManager.Instance.enemyPoint.text = "1210";
        chessBoard.gameObject.SetActive(true);
        UIManager.Instance.playerTurn.text = "White";
        Camera.main.GetComponent<CamSwerve>().enabled = true;
    }
    public override void OnLeftRoom()
    {
        //Odadan ayrýlýnca çalýþan callback fonksiyon
        Debug.Log("Odadan ayrýldý");
        if (chessBoard.gameObject.activeSelf)
        {
            step = 0;
            chessBoard.gameObject.SetActive(false);
            chessPlayers.Clear();
            for (int i = 0; i < chessBoard.squareParent.transform.childCount; i++)
            {
                Destroy(chessBoard.squareParent.transform.GetChild(i).gameObject);
            }
            for (int i = 0; i < chessBoard.piecesParent.transform.childCount; i++)
            {
                Destroy(chessBoard.piecesParent.transform.GetChild(i).gameObject);
            }
            UIManager.Instance.LoadingFinish(UIManager.Instance.levelLoading.gameObject);
        }
    }
    public override void OnLeftLobby()
    {
        //Lobiden ayrýlýnca çalýþan callback fonksiyon
        Debug.Log("Lobiden ayrýldý");
    }
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        //Bir odaya girmeye çalýþýnca hata oluþursa çalýþan callback fonksiyon
        Debug.Log("Herhangi bir odaya girilemedi");
    }
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        //Rastgele bir odaya girmeye çalýþýnca hata oluþursa çalýþan callback fonksiyon
        Debug.Log("Rastgele bir odaya girilemedi");
    }
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        //Bir odaya yaratmaya esnasýnda hata oluþursa çalýþan callback fonksiyon
        Debug.Log("Oda oluþturulurken hata oluþtu");
    }
}
