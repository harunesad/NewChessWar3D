using System.Collections;
using System.Collections.Generic;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class BodylinkTesting : MonoBehaviour
{
    public Dropdown camerasDropdown;

    public Dropdown calibrationDropdown;
    public Dropdown playerDropDown;
    public Button initButton, calibrateButton, resetButton;

    public RawImage rawImage;
    [Header("PlayerOne")]
    public Text playerOneEventsText;
    public Text playerOneHandPosText;
    public Text playerOneHeightText;
    public Text playerOneArmRatioText;
    public Text playerOneTorsoRatioText;
    public Text playerOneLegRatioText;


    [Header("PlayerTwo")]
    public Text playerTwoEventsText;
    public Text playerTwoHandPosText;
    public Text playerTwoHeightText;
    public Text playerTwoArmRatioText;
    public Text playerTwoTorsoRatioText;
    public Text playerTwoLegRatioText;



    public GameObject playerTwoParent;

    private Bodylink bodylinkInstance;


    // Start is called before the first frame update
    void Start()
    {
        bodylinkInstance = Bodylink.Instance;

        initButton.onClick.AddListener(() =>
        {
            Bodylink.Instance.Initialize();
        });
        calibrateButton.onClick.AddListener(() =>
        {
            Bodylink.Instance.Calibrate();
        });
        resetButton.onClick.AddListener(() =>
        {
            Bodylink.Instance.Dispose();
            camerasDropdown.ClearOptions();
            camerasDropdown.onValueChanged.RemoveAllListeners();
        });
        calibrationDropdown.onValueChanged.AddListener((val) =>
        {
            Bodylink.Instance.calibrationType = (BodylinkCalibrationType)val;
        });

        playerDropDown.onValueChanged.AddListener((val) =>
        {
            Bodylink.Instance.numberOfPlayers = val + 1;
        });

        Bodylink.Instance.OnInitialized += () =>
        {
            playerOneEventsText.text = playerOneEventsText.text + "\n" + "OnInitialized Event";
            InitializeSource();

        };
        Bodylink.Instance.OnCalibrated += () =>
        {
            playerOneEventsText.text = playerOneEventsText.text + "\n" + "OnCalibrated Event";

        };
        Bodylink.Instance.OnDisposed += () =>
        {
            playerOneEventsText.text = playerOneEventsText.text + "\n" + "OnDisposed Event";

        };
        Bodylink.Instance.OnPlayerOutOfScreen += () =>
        {
            playerOneEventsText.text = playerOneEventsText.text + "\n" + "OnPlayerOutOfScreen Event";

        };


        Bodylink.Instance.inputEvents.OnGestureDetection += (playerIndex, gestureName, value) =>
        {
            if (playerIndex == 0)
                playerOneEventsText.text = gestureName + "  Value: " + value[0];
            else
                playerTwoEventsText.text = gestureName + "  Value: " + value[0];
        };

        Bodylink.Instance.inputEvents.OnPoseDetection += (playerIndex, gestureName, side, pose) =>
        {
            if (playerIndex == 0)
                playerOneHandPosText.text = "Pose Detected: " + gestureName + "  Side: " + side + " Pose: " + pose;
            else
                playerTwoHandPosText.text = "Pose Detected: " + gestureName + "  Side: " + side + " Pose: " + pose;
        };

    }


    // Update is called once per frame
    void Update()
    {
        initButton.interactable = !bodylinkInstance.IsInitialized;
        calibrateButton.interactable = bodylinkInstance.IsInitialized && !bodylinkInstance.IsCalibrated;
        resetButton.interactable = bodylinkInstance.PoseLandmarkerRunnerInstance != null && bodylinkInstance.IsCalibrated && bodylinkInstance.IsInitialized;

        //        rawImage.texture = Bodylink.Instance.cameraScreen.texture;

        if (bodylinkInstance.IsInitialized == false || bodylinkInstance.IsCalibrated == false) return;
        playerOneHeightText.text = "Height :  " + bodylinkInstance.GetPlayerCurrentHeight();
        playerOneArmRatioText.text = "Arm Ratio :  " + bodylinkInstance.GetPlayerArmRatio();
        playerOneTorsoRatioText.text = "Torso Ratio :  " + bodylinkInstance.GetPlayerTorsoRatio();
        playerOneLegRatioText.text = "Leg ratio :  " + bodylinkInstance.GetPlayerLegRatio();

        playerTwoParent.SetActive(bodylinkInstance.isMultiplayerEnabled);

        if (bodylinkInstance.isMultiplayerEnabled)
        {
            playerTwoHeightText.text = "Height :  " + bodylinkInstance.GetPlayerCurrentHeight(1);
            playerTwoArmRatioText.text = "Arm Ratio :  " + bodylinkInstance.GetPlayerArmRatio(1);
            playerTwoTorsoRatioText.text = "Torso Ratio :  " + bodylinkInstance.GetPlayerTorsoRatio(1);
            playerTwoLegRatioText.text = "Leg ratio :  " + bodylinkInstance.GetPlayerLegRatio(1);
        }

    }

    private void InitializeSource()
    {
        camerasDropdown.ClearOptions();
        camerasDropdown.onValueChanged.RemoveAllListeners();

        var sourceNames = Bodylink.Instance.imageSourcesNames;

        if (sourceNames == null)
        {
            camerasDropdown.enabled = false;
            return;
        }

        var options = new List<string>(sourceNames);
        camerasDropdown.AddOptions(options);

        var currentSourceName = Bodylink.Instance.imageSource.sourceName;
        var defaultValue = options.FindIndex(option => option == currentSourceName);

        if (defaultValue >= 0)
        {
            camerasDropdown.value = defaultValue;
        }

        camerasDropdown.onValueChanged.AddListener(delegate
        {
            Bodylink.Instance.SelectSource(camerasDropdown.value);
        });
        if (sourceNames.Length > 1)
        {
            Bodylink.Instance.SelectSource(1);
            camerasDropdown.value = 1;

        }
    }

}