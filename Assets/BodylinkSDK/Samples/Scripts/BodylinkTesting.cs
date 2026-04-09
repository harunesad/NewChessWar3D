using System.Collections;
using System.Collections.Generic;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class BodylinkTesting : MonoBehaviour
{
    public Dropdown camerasDropdown;
    public Dropdown calibrationModeDropdown;
    public Dropdown calibrationDropdown;
    public Dropdown playerDropDown;
    public Button initButton, calibrateButton, resetButton;

    public RawImage rawImage;
    [Header("PlayerOne")]
    public Text playerOneEventsText;
    public Text playerOneHandPosText;
    public Text playerOneHeightText;
    public Text playerOneHeightRatioText;
    public Text playerOneArmLengthText;
    public Text playerOneArmRatioText;
    public Text playerOneTorsoRatioText;
    public Text playerOneLegLengthText;
    public Text playerOneLegRatioText;


    [Header("PlayerTwo")]
    public Text playerTwoEventsText;
    public Text playerTwoHandPosText;
    public Text playerTwoHeightText;
    public Text playerTwoHeightRatioText;
    public Text playerTwoArmLengthText;
    public Text playerTwoArmRatioText;
    public Text playerTwoTorsoRatioText;
    public Text playerTwoLegLengthText;
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

        calibrationModeDropdown.onValueChanged.AddListener((val) =>
        {
            Bodylink.Instance.selectedCalibrationMode = (BodylinkCalibrationMode)val;
            UpdateCalibrationDropdownState();
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

        Bodylink.Instance.selectedCalibrationMode = (BodylinkCalibrationMode)calibrationModeDropdown.value;
        UpdateCalibrationDropdownState();

    }


    // Update is called once per frame
    void Update()
    {
        initButton.interactable = !bodylinkInstance.IsInitialized;
        calibrateButton.interactable = bodylinkInstance.IsInitialized && !bodylinkInstance.IsCalibrated;
        resetButton.interactable = bodylinkInstance.PoseLandmarkerRunnerInstance != null && bodylinkInstance.IsCalibrated && bodylinkInstance.IsInitialized;

        //        rawImage.texture = Bodylink.Instance.cameraScreen.texture;

        playerTwoParent.SetActive(bodylinkInstance.isMultiplayerEnabled);
        if (bodylinkInstance.IsInitialized == false)
        {
            UpdatePlayerStats(
                -1,
                playerOneHeightText,
                playerOneHeightRatioText,
                playerOneArmLengthText,
                playerOneArmRatioText,
                playerOneTorsoRatioText,
                playerOneLegLengthText,
                playerOneLegRatioText);

            UpdatePlayerStats(
                -1,
                playerTwoHeightText,
                playerTwoHeightRatioText,
                playerTwoArmLengthText,
                playerTwoArmRatioText,
                playerTwoTorsoRatioText,
                playerTwoLegLengthText,
                playerTwoLegRatioText);
            return;
        }

        UpdatePlayerStats(
            0,
            playerOneHeightText,
            playerOneHeightRatioText,
            playerOneArmLengthText,
            playerOneArmRatioText,
            playerOneTorsoRatioText,
            playerOneLegLengthText,
            playerOneLegRatioText);

        UpdatePlayerStats(
            bodylinkInstance.isMultiplayerEnabled ? 1 : -1,
            playerTwoHeightText,
            playerTwoHeightRatioText,
            playerTwoArmLengthText,
            playerTwoArmRatioText,
            playerTwoTorsoRatioText,
            playerTwoLegLengthText,
            playerTwoLegRatioText);

    }

    private void UpdatePlayerStats(
        int playerIndex,
        Text heightText,
        Text heightRatioText,
        Text armLengthText,
        Text armRatioText,
        Text torsoRatioText,
        Text legLengthText,
        Text legRatioText)
    {
        BodyCalibrationData2D data = null;
        bool hasPlayerData = playerIndex >= 0 && bodylinkInstance.TryGetPlayerCurrentData(playerIndex, out data);

        SetStatText(heightText, "Height", hasPlayerData ? FormatStat(data.height) : "N/A");
        SetStatText(heightRatioText, "Height Ratio", hasPlayerData ? FormatStat(GetHeightRatio(data.height)) : "N/A");
        SetStatText(armLengthText, "Arm Length", hasPlayerData ? FormatStat(data.armLength) : "N/A");
        SetStatText(armRatioText, "Arm Ratio", hasPlayerData ? FormatStat(data.armRatio) : "N/A");
        SetStatText(torsoRatioText, "Torso Ratio", hasPlayerData ? FormatStat(data.torsoRatio) : "N/A");
        SetStatText(legLengthText, "Leg Length", hasPlayerData ? FormatStat(data.legLength) : "N/A");
        SetStatText(legRatioText, "Leg Ratio", hasPlayerData ? FormatStat(data.legRatio) : "N/A");
    }

    private static void SetStatText(Text target, string label, string value)
    {
        if (target == null)
        {
            return;
        }

        target.text = label + " : " + value;
    }

    private static string FormatStat(float value)
    {
        return value.ToString("F3");
    }

    private static float GetHeightRatio(float currentHeight)
    {
        if (currentHeight <= Mathf.Epsilon)
        {
            return 0f;
        }

        return 1.65f / currentHeight;
    }

    private void UpdateCalibrationDropdownState()
    {
        if (calibrationDropdown == null)
        {
            return;
        }

        calibrationDropdown.interactable =
            Bodylink.Instance.selectedCalibrationMode == BodylinkCalibrationMode.Target_Points_Match ||
            Bodylink.Instance.selectedCalibrationMode == BodylinkCalibrationMode.Free_Points_Position;
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
