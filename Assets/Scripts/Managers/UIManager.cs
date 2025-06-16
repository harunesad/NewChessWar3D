using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using Photon.Pun;

public class UIManager : MonoBehaviour
{
    private static UIManager _instance;

    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Eðer örnek oluþturulmamýþsa, yeni bir örnek oluþtur
                GameObject singletonObject = new GameObject("UIManager");
                _instance = singletonObject.AddComponent<UIManager>();
            }

            return _instance;
        }
    }
    [SerializeField]
    TextMeshProUGUI win, lose, point, level, warningMessage, myNickname;
    [SerializeField]
    Button gameLevel;
    public Button back;
    public Image levelLoading, gameLoading;
    [SerializeField]
    TMP_InputField nicknameText;
    public TextMeshProUGUI enemyNickname, myPoint, enemyPoint, playerTurn, turnTime;
    public GameObject chessBoard, chessRoom, chessLevel, environment;
    public string nickname;
    public int levelId, roomId;
    public float time = 60;
    public List<TextMeshProUGUI> nicknames, playerTypes;
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
    void Start()
    {
        gameLevel.onClick.AddListener(LevelActive);
        back.onClick.AddListener(Back);
        for (int i = 0; i < chessLevel.transform.childCount; i++)
        {
            int j = i;
            chessLevel.transform.GetChild(j).GetComponent<Button>().onClick.AddListener(delegate { LevelSelect(j); });
        }
    }
    public void TimeReduce()
    {
        if (chessBoard.activeSelf)
        {
            time -= Time.deltaTime;
        }
        turnTime.text = "Time : " + ((int)time).ToString();
        if (time <= 0)
        {
            time = 60;
            if (ServerControl.Instance.playerTurn == ServerControl.Player.White)
            {
                ServerControl.Instance.playerTurn = ServerControl.Player.Black;
                playerTurn.text = "Black";
            }
            else
            {
                ServerControl.Instance.playerTurn = ServerControl.Player.White;
                playerTurn.text = "White";
            }
        }
    }
    void LevelActive()
    {
        if (nicknameText.text.Length < 10 && nicknameText.text.Length > 4 && !nicknameText.text.Contains(" "))
        {
            nickname = nicknameText.text;
            gameLevel.gameObject.SetActive(false);
            chessLevel.SetActive(true);
            nicknameText.gameObject.SetActive(false);
            WinLose(true);
            myNickname.text = nickname;
            myNickname.gameObject.SetActive(true);
            for (int i = 0; i < environment.transform.childCount - 2; i++)
            {
                environment.transform.GetChild(i).gameObject.SetActive(false);
            }
        }
        else
        {
            WarningText("Nickname en az 5 en fazla 10 harfli olmalý ve boþluk içermemelidir.");
        }

    }
    void LevelSelect(int index)
    {
        if (index < JsonSave.json.save.level + 1)
        {
            LoadingStart(levelLoading.gameObject);
            levelId = index;
            chessRoom.SetActive(true);
            back.gameObject.SetActive(true);
            WinLose(false);
            PhotonNetwork.JoinLobby();
            chessLevel.SetActive(false);
        }
        else
        {
            WarningText("Henüz bu levellere giriþ yapacak levelde deðilsiniz.");
        }
    }
    void Back()
    {
        if (chessRoom.activeSelf)
        {
            ServerControl.Instance.lobby.GetComponent<LobbyControl>().LeaveRoomPun(roomId);
            LoadingStart(levelLoading.gameObject);
            back.gameObject.SetActive(false);
            chessLevel.SetActive(true);
            chessRoom.SetActive(false);
            WinLose(true);
            StartCoroutine(LeaveWait());
        }
        else
        {
            environment.SetActive(true);
            LoadingStart(levelLoading.gameObject);
            chessLevel.SetActive(true);
            WinLose(true);
            StartCoroutine(LeaveWait());
            myPoint.text = "";
            enemyPoint.text = "";
            enemyNickname.text = "";
            playerTurn.text = "";
            turnTime.text = "";
            Camera.main.GetComponent<CamSwerve>().enabled = false;
        }
    }
    IEnumerator LeaveWait()
    {
        yield return new WaitForSeconds(.5f);
        PhotonNetwork.LeaveRoom();
    }
    void WinLose(bool state)
    {
        win.gameObject.SetActive(state);
        lose.gameObject.SetActive(state);
        point.gameObject.SetActive(state);
        level.gameObject.SetActive(state);
        if (win.gameObject.activeSelf)
        {
            win.text = "Win: " + JsonSave.json.save.win;
            lose.text = "Lose: " + JsonSave.json.save.lose;
            point.text = "Point: " + (JsonSave.json.save.win - JsonSave.json.save.lose < 0 ? 0 : JsonSave.json.save.win - JsonSave.json.save.lose);
            level.text = "Level: " + (JsonSave.json.save.level + 1);
        }
    }
    public void WarningText(string message)
    {
        warningMessage.text = message;
        warningMessage.GetComponent<CanvasGroup>().DOFade(1, .25f).SetEase(Ease.Linear).OnComplete(() =>
        {
            StartCoroutine(LoadingComplete());
        });
    }
    IEnumerator LoadingComplete()
    {
        yield return new WaitForSeconds(.75f);
        warningMessage.GetComponent<CanvasGroup>().DOFade(0, .25f).SetEase(Ease.Linear);
    }
    public void LoadingStart(GameObject obj)
    {
        obj.GetComponent<CanvasGroup>().DOFade(1, .15f).SetEase(Ease.Linear);
    }
    public void LoadingFinish(GameObject obj)
    {
        obj.GetComponent<CanvasGroup>().DOFade(0, .15f).SetEase(Ease.Linear);
    }
}
