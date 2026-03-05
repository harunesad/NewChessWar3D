using System.Collections;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class JumpExample : MonoBehaviour
{
    public Transform[] players;
    public Text[] playerNamesText;
    private Animator[] playerAnims;
    private Coroutine[] moveRoutines;
    [SerializeField] private float verticalDistance = 1.5f;
    [SerializeField] private float moveDuration = 0.25f;
    private bool[] isGrounded;

    void OnEnable()
    {
        PreparePlayers();
        foreach (var p in players)
        {
            if (p == null) continue;
            p.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        Bodylink.Instance.OnInitialized += () =>
        {

            Bodylink.Instance.DisplayCameraFeed(true);
            Bodylink.Instance.SetCameraFeedSize(0.75f);

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                players[i].gameObject.SetActive(true);
                playerNamesText[i].text = "Player : " + i.ToString();
                players[i].gameObject.GetComponent<MeshRenderer>().material.color = i == 0 ? Color.green : Color.red;
                isGrounded[i] = true;
                if (i != 0 && Bodylink.Instance.isMultiplayerEnabled == false)
                {
                    players[i].gameObject.SetActive(false);
                    players[i - 1].position += new Vector3(2, 0, 0);
                }
            }
        };
        Bodylink.Instance.inputEvents.OnGestureDetection += HandleGestureDetection;
    }

    void OnDisable()
    {
        if (Bodylink.Instance == null) return;
        Bodylink.Instance.inputEvents.OnGestureDetection -= HandleGestureDetection;
        if (moveRoutines != null)
        {
            for (int i = 0; i < moveRoutines.Length; i++)
            {
                if (moveRoutines[i] != null) StopCoroutine(moveRoutines[i]);
                moveRoutines[i] = null;
            }
        }
        if (isGrounded != null)
        {
            for (int i = 0; i < isGrounded.Length; i++) isGrounded[i] = true;
        }
    }

    private void HandleGestureDetection(int playerIndex, string gestureName, object value)
    {
        if (!IsValidPlayer(playerIndex)) return;
        if (gestureName == "Jump")
        {
            if (!isGrounded[playerIndex]) return;
            StartJump(playerIndex);
        }
    }

    private void StartJump(int playerIndex)
    {
        if (moveRoutines[playerIndex] != null) StopCoroutine(moveRoutines[playerIndex]);
        playerAnims[playerIndex].SetTrigger("move");
        moveRoutines[playerIndex] = StartCoroutine(JumpRoutine(playerIndex));
    }

    private IEnumerator JumpRoutine(int playerIndex)
    {
        isGrounded[playerIndex] = false;
        var groundPos = players[playerIndex].position;
        var peakPos = groundPos + Vector3.up * verticalDistance;

        yield return MovePlayer(playerIndex, groundPos, peakPos);
        yield return MovePlayer(playerIndex, peakPos, groundPos);

        isGrounded[playerIndex] = true;
        moveRoutines[playerIndex] = null;
    }

    private IEnumerator MovePlayer(int playerIndex, Vector3 startPos, Vector3 targetPos)
    {
        var elapsed = 0f;
        while (elapsed < moveDuration)
        {
            players[playerIndex].position = Vector3.Lerp(startPos, targetPos, elapsed / moveDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        players[playerIndex].position = targetPos;
    }

    private void PreparePlayers()
    {
        if (players == null) players = new Transform[0];
        var count = players.Length;
        playerAnims = new Animator[count];
        moveRoutines = new Coroutine[count];
        isGrounded = new bool[count];
        for (int i = 0; i < count; i++)
        {
            if (players[i] == null) continue;
            playerAnims[i] = players[i].GetComponent<Animator>();
            isGrounded[i] = true;
        }
    }

    private bool IsValidPlayer(int index)
    {
        return index >= 0 && players != null && index < players.Length && players[index] != null;
    }
}