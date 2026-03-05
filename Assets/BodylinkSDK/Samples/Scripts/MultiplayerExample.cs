using System.Collections;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerExample : MonoBehaviour
{
    public RectTransform player_1, player_2;
    public Text player_1_name, player_2_name, playerCountText;
    public Canvas canvas;
    private RectTransform canvasRect;         // Prevent rapid double-click
    private bool canRun;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        canvasRect = canvas.GetComponent<RectTransform>();
        player_1_name.text = "";
        player_2_name.text = "";
        playerCountText.text = "";
        canRun = false;


        Bodylink.Instance.OnInitialized += () =>
        {
            canRun = true;
            Bodylink.Instance.SetCameraFeedSize(0.8f);
        };

    }

    // Update is called once per frame
    void Update()
    {
        if (!canRun) return;

        // --- 1. GET HAND POSITION ---
        var playerOneNoseLandmark = Bodylink.Instance.players[0].body2D.nose;
        Vector2 playerOneNose = new Vector2(playerOneNoseLandmark.x, playerOneNoseLandmark.y);   //normalized 0-1

        var playerTwoNoseLandmark = Bodylink.Instance.players[1].body2D.nose;
        Vector2 playerTwoNose = new Vector2(playerTwoNoseLandmark.x, playerTwoNoseLandmark.y);   //normalized 0-1


        // Convert to screen position
        Vector2 playerOnetargetPos = new Vector2(
        playerOneNose.x * Screen.width,
        playerOneNose.y * Screen.height
        );

        // Convert to screen position
        Vector2 playerTwotargetPos = new Vector2(
        playerTwoNose.x * Screen.width,
        playerTwoNose.y * Screen.height
        );

        // Convert to canvas local space and clamp to stay inside the canvas

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, playerOnetargetPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var localPos1);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, playerTwotargetPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var localPos2);


        Vector2 clampedLocalPos1 = new Vector2(
        Mathf.Clamp(localPos1.x, canvasRect.rect.xMin, canvasRect.rect.xMax),
        Mathf.Clamp(localPos1.y, canvasRect.rect.yMin, canvasRect.rect.yMax)
        );

        Vector2 clampedLocalPos2 = new Vector2(
        Mathf.Clamp(localPos2.x, canvasRect.rect.xMin, canvasRect.rect.xMax),
        Mathf.Clamp(localPos2.y, canvasRect.rect.yMin, canvasRect.rect.yMax)
        );

        // Smooth movement
        player_1.anchoredPosition = Vector2.Lerp(player_1.anchoredPosition, clampedLocalPos1, Time.deltaTime * 30);
        player_2.anchoredPosition = Vector2.Lerp(player_2.anchoredPosition, clampedLocalPos2, Time.deltaTime * 30);

        int count = Bodylink.Instance.bodylinkAvatar.poseLandmarkerResult.poseLandmarks.Count;

        player_1_name.text = "Player " + Bodylink.Instance.players[0].playerIndex.ToString();
        if (count > 1)
            player_2_name.text = "Player " + Bodylink.Instance.players[1].playerIndex.ToString();
        else
            player_2_name.text = "";

        playerCountText.text = "Detected Players: " + count.ToString();
    }
}