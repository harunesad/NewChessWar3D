using System.Collections;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class HandSwipesExample : MonoBehaviour
{
    public Text playerOneSwipeText, playerTwoSwipeText;

    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        Bodylink.Instance.OnInitialized += () =>
        {
            playerTwoSwipeText.gameObject.SetActive(false);
            playerTwoSwipeText.gameObject.SetActive(Bodylink.Instance.isMultiplayerEnabled);
            //Bodylink.Instance.bodylinkAvatar.players[0].ShowMiniCamera(false);
        };

        Bodylink.Instance.inputEvents.OnGestureDetection += OnHandSwipeDetection;

    }

    void OnDisable()
    {
        if (Bodylink.Instance == null) return;
        Bodylink.Instance.inputEvents.OnGestureDetection -= OnHandSwipeDetection;
    }

    private void OnHandSwipeDetection(int playerIndex, string swipe, object[] value)
    {
        float delta = (float)value[0];
        if (playerIndex == 0)
        {
            playerOneSwipeText.text = "Player 1: \nSwipe" + " : " + swipe.ToString() + "\nDelta : " + delta.ToString("0.00");
        }
        else
        {
            playerTwoSwipeText.text = "Player 2: \nSwipe" + " : " + swipe.ToString() + "\nDelta : " + delta.ToString("0.00");
        }
    }
}