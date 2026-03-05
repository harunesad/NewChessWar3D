using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMeasurementExample : MonoBehaviour
{
    public Text playerOneNameText;
    public Text playerOneHeightText;
    public Text playerOneArmRatioText;
    public Text playerOneLegRatioText;
    public Text playerOneTorsoRatioText;


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
        playerOneHeightText.text = "Height : " + Bodylink.Instance.GetPlayerCurrentHeight(0).ToString("0.00");
        playerOneArmRatioText.text = "Arm Ratio : " + Bodylink.Instance.GetPlayerArmRatio(0).ToString("0.00");
        playerOneLegRatioText.text = "Leg Ratio : " + Bodylink.Instance.GetPlayerLegRatio(0).ToString("0.00");
        playerOneTorsoRatioText.text = "Torso Ratio : " + Bodylink.Instance.GetPlayerTorsoRatio(0).ToString("0.00");
        if (Bodylink.Instance.isMultiplayerEnabled)
        {
            playerTwoHeightText.text = "Height : " + Bodylink.Instance.GetPlayerCurrentHeight(1).ToString("0.00");
            playerTwoArmRatioText.text = "Arm Ratio : " + Bodylink.Instance.GetPlayerArmRatio(1).ToString("0.00");
            playerTwoLegRatioText.text = "Leg Ratio : " + Bodylink.Instance.GetPlayerLegRatio(1).ToString("0.00");
            playerTwoTorsoRatioText.text = "Torso Ratio : " + Bodylink.Instance.GetPlayerTorsoRatio(1).ToString("0.00");
        }
    }


}