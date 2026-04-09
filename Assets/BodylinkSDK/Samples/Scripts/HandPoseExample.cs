using System.Collections;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class HandPoseExample : MonoBehaviour
{
    public Text playerOneLeftPoseText, playerOneRightPoseText, playerTwoLeftPoseText, playerTwoRightPoseText;

    public Canvas canvas;

    IEnumerator Start()
    {
        canvas.gameObject.SetActive(false);
        yield return new WaitForEndOfFrame();
        Bodylink.Instance.OnInitialized += () =>
        {
            canvas.gameObject.SetActive(true);
            playerTwoLeftPoseText.gameObject.SetActive(Bodylink.Instance.isMultiplayerEnabled);
            playerTwoRightPoseText.gameObject.SetActive(Bodylink.Instance.isMultiplayerEnabled);
        };

        Bodylink.Instance.inputEvents.OnPoseDetection += OnHandPoseDetection;

    }

    void OnDisable()
    {
        if (Bodylink.Instance == null) return;
        Bodylink.Instance.inputEvents.OnPoseDetection -= OnHandPoseDetection;
    }

    private void OnHandPoseDetection(int playerIndex, string handPoseName, Side side, HandPose handPose)
    {
        if (playerIndex == 0)
        {
            if (side == Side.Left)
            {
                playerOneLeftPoseText.text = "Player 1: \n" + "Hand Side: " + side.ToString() + "\nPose" + ": " + handPose.ToString();
            }
            else
            {
                playerOneRightPoseText.text = "Player 1: \n" + "Hand Side: " + side.ToString() + "\nPose" + ": " + handPose.ToString();
            }
        }
        else
        {
            if (side == Side.Left)
            {
                playerTwoLeftPoseText.text = "Player 2: \n" + "Hand Side: " + side.ToString() + "\nPose" + ": " + handPose.ToString();
            }
            else
            {
                playerTwoRightPoseText.text = "Player 2: \n" + "Hand Side: " + side.ToString() + "\nPose" + ": " + handPose.ToString();
            }
        }
    }
}