using System.Collections;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class HeadGestureExample : MonoBehaviour
{
    public GameObject canvasObject, confirmationPanel, resultPanel;
    public Text responseText, timerText;
    private string msg;
    private bool gestureDetected, activateTimer;
    float timer = 5;
    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        //canvasObject.SetActive(false);
        Bodylink.Instance.inputEvents.OnGestureDetection += HandleGestureDetection;
        Bodylink.Instance.OnInitialized += () =>
        {
            canvasObject.SetActive(true);
            Bodylink.Instance.SetCameraFeedSize(.75f);
        };
    }

    void OnDisable()
    {
        if (Bodylink.Instance == null) return;
        Bodylink.Instance.inputEvents.OnGestureDetection -= HandleGestureDetection;
    }

    private void HandleGestureDetection(int playerIndex, string gestureName, object value)
    {
        if (gestureName == "HeadNod")
        {
            gestureDetected = true;
            msg = "Head Nod Detected.";
        }
        else if (gestureName == "HeadShake")
        {
            gestureDetected = true;
            msg = "Head Shake Detected.";
        }

    }

    void Update()
    {
        if (gestureDetected)
        {
            gestureDetected = false;
            confirmationPanel.SetActive(false);
            resultPanel.SetActive(true);
            activateTimer = true;
            responseText.text = msg + "\nResetting scene in : ";
        }

        if (activateTimer)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                confirmationPanel.SetActive(true);
                resultPanel.SetActive(false);
                activateTimer = false;
                timer = 4;
            }
            timerText.text = timer.ToString("F0");
        }
    }

}