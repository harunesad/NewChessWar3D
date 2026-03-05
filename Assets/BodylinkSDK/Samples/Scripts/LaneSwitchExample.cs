using System.Collections;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class LaneSwitchExample : MonoBehaviour
{
    public Transform player1, player2;
    public Transform playerOneSpawnPoint, playerTwoSpawnPoint;
    public Text[] playerNameText;
    private Animator[] playerAnim = new Animator[2];
    private Coroutine[] moveRoutine = new Coroutine[2];
    private Vector3[] centerPositions = new Vector3[2];
    [SerializeField] private float moveDistance = 1.5f;
    [SerializeField] private float maxHorizontalOffset = 2f; // limit from center +/- meters
    [SerializeField] private float moveDuration = 0.25f;

    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        playerAnim[0] = player1.gameObject.GetComponent<Animator>();
        playerAnim[1] = player2.gameObject.GetComponent<Animator>();
        Bodylink.Instance.inputEvents.OnGestureDetection += HandleGestureDetection;
        Bodylink.Instance.OnInitialized += () =>
        {

            if (Bodylink.Instance.isMultiplayerEnabled)
            {
                player2.gameObject.SetActive(true);
                player2.transform.position = playerTwoSpawnPoint.position;
                centerPositions[1] = playerTwoSpawnPoint.position;
                player1.transform.position = playerOneSpawnPoint.position;
                centerPositions[0] = playerOneSpawnPoint.position;
                playerNameText[1].text = "Player 2";

            }
            else
            {
                centerPositions[0] = player1.transform.position;
                player2.gameObject.SetActive(false);
            }
            playerNameText[0].text = "Player 1";
            player1.gameObject.SetActive(true);
            Bodylink.Instance.players[0].SetMiniCameraScreen();
            Bodylink.Instance.players[0].ShowMiniCamera(true);
            Bodylink.Instance.players[0].SetMiniCameraScale(.75f);
        };
    }

    void OnDisable()
    {
        if (Bodylink.Instance == null) return;
        Bodylink.Instance.inputEvents.OnGestureDetection -= HandleGestureDetection;
    }

    private void HandleGestureDetection(int playerIndex, string gestureName, object value)
    {
        Transform target = playerIndex == 0 ? player1 : player2;
        if (target == null) return;
        if (playerIndex == 1 && !Bodylink.Instance.isMultiplayerEnabled) return;

        if (gestureName == "BodyMovesRight")
        {
            TryStartMove(playerIndex, Vector3.right);
        }
        else if (gestureName == "BodyMovesLeft")
        {
            TryStartMove(playerIndex, Vector3.left);
        }
    }

    private void TryStartMove(int playerIndex, Vector3 direction)
    {
        Transform target = playerIndex == 0 ? player1 : player2;
        Vector3 center = centerPositions[playerIndex];
        float currentOffset = target.position.x - center.x;

        float desiredOffset = currentOffset + direction.x * moveDistance;
        desiredOffset = Mathf.Clamp(desiredOffset, -maxHorizontalOffset, maxHorizontalOffset);

        // If clamped position is the same, do nothing.
        if (Mathf.Approximately(desiredOffset, currentOffset)) return;

        Vector3 start = target.position;
        Vector3 end = new Vector3(center.x + desiredOffset, start.y, start.z);

        if (moveRoutine[playerIndex] != null) StopCoroutine(moveRoutine[playerIndex]);
        if (playerAnim[playerIndex] != null) playerAnim[playerIndex].SetTrigger("move");
        moveRoutine[playerIndex] = StartCoroutine(MovePlayer(playerIndex, start, end));
    }

    private IEnumerator MovePlayer(int playerIndex, Vector3 startPos, Vector3 targetPos)
    {
        Transform target = playerIndex == 0 ? player1 : player2;
        var elapsed = 0f;
        while (elapsed < moveDuration)
        {
            target.position = Vector3.Lerp(startPos, targetPos, elapsed / moveDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.position = targetPos;
        moveRoutine[playerIndex] = null;
    }
}