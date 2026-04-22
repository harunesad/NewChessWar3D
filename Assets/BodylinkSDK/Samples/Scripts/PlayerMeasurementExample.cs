using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMeasurementExample : MonoBehaviour
{
    [Header("Player One UI Elements")]
    public Text playerOneNameText;
    public Text playerOneHeightText;
    public Text playerOneArmRatioText;
    public Text playerOneLegRatioText;
    public Text playerOneTorsoRatioText;

    [Header("Player Two UI Elements")]
    public Text playerTwoNameText;
    public Text playerTwoHeightText;
    public Text playerTwoArmRatioText;
    public Text playerTwoLegRatioText;
    public Text playerTwoTorsoRatioText;



    public RawImage rawImage;
    public GameObject leftContainer, rightContainer;
    private bool isInitialized = false;


    void Start()
    {
        leftContainer.SetActive(false);
        rightContainer.SetActive(false);
        rawImage.gameObject.SetActive(false);

        Bodylink.Instance.OnInitialized += () =>
        {
            leftContainer.SetActive(true);
            rightContainer.SetActive(Bodylink.Instance.isMultiplayerEnabled);
            rawImage.gameObject.SetActive(true);
            rawImage.texture = Bodylink.Instance.cameraScreen.texture;


            playerOneNameText.text = "Player 1";

            if (Bodylink.Instance.isMultiplayerEnabled)
            {
                rightContainer.SetActive(true);
                playerTwoNameText.text = "Player 2";
            }
            isInitialized = true;

        };

    }

    void Update()
    {
        if (!isInitialized) return;

        UpdatePlayerMeasurement(
            0,
            playerOneHeightText,
            playerOneArmRatioText,
            playerOneLegRatioText,
            playerOneTorsoRatioText);

        if (Bodylink.Instance.isMultiplayerEnabled)
        {
            UpdatePlayerMeasurement(
                1,
                playerTwoHeightText,
                playerTwoArmRatioText,
                playerTwoLegRatioText,
                playerTwoTorsoRatioText);
        }
    }

    private void UpdatePlayerMeasurement(
        int playerIndex,
        Text heightText,
        Text armRatioText,
        Text legRatioText,
        Text torsoRatioText)
    {
        if (Bodylink.Instance != null &&
            Bodylink.Instance.TryGetPlayerCurrentData(playerIndex, out BodyCalibrationData2D data))
        {
            SetText(heightText, "Height : " + data.height.ToString("0.00"));
            SetText(armRatioText, "Arm Ratio : " + data.armRatio.ToString("0.00"));
            SetText(legRatioText, "Leg Ratio : " + data.legRatio.ToString("0.00"));
            SetText(torsoRatioText, "Torso Ratio : " + data.torsoRatio.ToString("0.00"));
            return;
        }

        SetText(heightText, "Height : N/A");
        SetText(armRatioText, "Arm Ratio : N/A");
        SetText(legRatioText, "Leg Ratio : N/A");
        SetText(torsoRatioText, "Torso Ratio : N/A");
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }


}
