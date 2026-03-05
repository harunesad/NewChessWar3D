using System.Collections;
using System.Collections.Generic;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class Skeleton2DExample : MonoBehaviour
{
    [Header("UI")]
    public Text visibilityText;          // Template text to clone per landmark
    public Canvas canvas;                // Canvas that holds the texts

    private RectTransform canvasRect;
    private readonly Text[] playerOnelandmarkLabels = new Text[33];
    private readonly Text[] playerTwolandmarkLabels = new Text[33];

    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();

        Bodylink.Instance.OnInitialized += () =>
        {
            GenerateLabels(0);
            if (Bodylink.Instance.isMultiplayerEnabled)
            {
                GenerateLabels(1);
            }

        };

        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        // Keep the template hidden; we only show the clones.
        if (visibilityText != null)
        {
            visibilityText.gameObject.SetActive(false);
        }


    }

    void Update()
    {
        if (!Bodylink.Instance.IsInitialized) return;
        if (visibilityText == null || canvasRect == null) return;


        PlayerOneVisibilityTracking(0);
        if (Bodylink.Instance.isMultiplayerEnabled)
        {
            PlayerOneVisibilityTracking(1);
        }
    }

    void PlayerOneVisibilityTracking(int index)
    {
        var player = Bodylink.Instance.players[index];
        var points2D = player.body2D;

        for (int i = 0; i < 33; i++)
        {
            float visibility = points2D[i].visibility.HasValue ? points2D[i].visibility.Value : 0f;
            if (index == 0)
                playerOnelandmarkLabels[i].text = visibility.ToString("0.0");
            else
                playerTwolandmarkLabels[i].text = visibility.ToString("0.0");


            Vector2 screenPos = new Vector2(points2D[i].x * Screen.width, points2D[i].y * Screen.height);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out var localPos);

            Vector2 clampedLocal = new Vector2(
            Mathf.Clamp(localPos.x, canvasRect.rect.xMin, canvasRect.rect.xMax),
            Mathf.Clamp(localPos.y, canvasRect.rect.yMin, canvasRect.rect.yMax)
            );

            if (index == 0)
            {
                var labelRect = playerOnelandmarkLabels[i].rectTransform;
                labelRect.anchoredPosition = clampedLocal;
            }
            else
            {
                var labelRect = playerTwolandmarkLabels[i].rectTransform;
                labelRect.anchoredPosition = clampedLocal;
            }
        }
    }

    private void GenerateLabels(int index)
    {
        if (index == 0)
        {

            for (int i = 0; i < 33; i++)
            {
                var clone = Instantiate(visibilityText, visibilityText.transform.parent);
                clone.gameObject.SetActive(true);
                clone.name = $"VisibilityLabel_{i}";
                clone.text = "";
                playerOnelandmarkLabels[i] = clone;
            }
        }
        else
        {

            for (int i = 0; i < 33; i++)
            {
                var clone = Instantiate(visibilityText, visibilityText.transform.parent);
                clone.gameObject.SetActive(true);
                clone.name = $"VisibilityLabel_{i}";
                clone.text = "";
                playerTwolandmarkLabels[i] = clone;
            }
        }


    }
}