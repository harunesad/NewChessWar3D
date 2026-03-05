using System.Collections;
using System.Linq;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class ArmRaiseExample : MonoBehaviour
{
    public Transform[] players;
    public Transform[] leftPlayerArms;   // player 0 arms: [0]=left, [1]=right
    public Transform[] rightPlayerArms;  // player 1 arms: [0]=left, [1]=right
    public Text[] playerNamesText;
    public Text[] playerInfoText;
    public RawImage miniCameraView;
    //public GameObject ground;
    //private Coroutine[] moveRoutines;
    [SerializeField] private float verticalDistance = 1.5f;
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private float maxArmRotation = 180f;
    [SerializeField] private float armLerpSpeed = 6f;
    [SerializeField] private float armIdleTimeout = 0.25f;
    //private bool[] isGrounded;
    private Quaternion[] armBaseRotations;   // indexed by player*2 + side
    private float[] armTargetAngles;
    private float[] armCurrentAngles;
    private float[] armLastEventTimes;

    void OnEnable()
    {
        PreparePlayers();
        //ground.SetActive(false);
        foreach (var p in players)
        {
            if (p == null) continue;
            p.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        miniCameraView.gameObject.SetActive(false);
        playerInfoText[0].gameObject.SetActive(false);
        playerInfoText[1].gameObject.SetActive(false);
        Bodylink.Instance.OnInitialized += () =>
        {
            Bodylink.Instance.DisplayCameraFeed(false);
            miniCameraView.gameObject.SetActive(true);
            miniCameraView.texture = Bodylink.Instance.cameraScreen.texture;
            //ground.SetActive(true);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                players[i].gameObject.SetActive(true);
                playerInfoText[i].gameObject.SetActive(true);
                playerNamesText[i].text = "Player : " + i.ToString();
                players[i].gameObject.GetComponent<MeshRenderer>().material.color = i == 0 ? Color.green : Color.red;
                if (i != 0 && Bodylink.Instance.isMultiplayerEnabled == false)
                {
                    players[i].gameObject.SetActive(false);
                    //players[i - 1].position += new Vector3(2, 0, 0);
                }
            }

            if (Bodylink.Instance.isMultiplayerEnabled == false)
            {
                playerInfoText[1].gameObject.SetActive(false);
            }

        };

        Bodylink.Instance.inputEvents.OnGestureDetection += HandleGestureDetection;
    }

    void OnDisable()
    {
        if (Bodylink.Instance == null) return;
        Bodylink.Instance.inputEvents.OnGestureDetection -= HandleGestureDetection;

    }

    private void HandleGestureDetection(int playerIndex, string gestureName, object[] value)
    {
        if (!IsValidPlayer(playerIndex)) return;
        var raiseAmount = GetRaiseAmount(value);
        //Debug.Log("Player Index " + playerIndex.ToString() + " Gesture name : " + gestureName + " Value : " + raiseAmount);
        if (gestureName == "LeftArmRaise")
        {
            SetArmTarget(playerIndex, true, raiseAmount);
        }
        else if (gestureName == "RightArmRaise")
        {
            SetArmTarget(playerIndex, false, raiseAmount);
        }
        else if (gestureName == "BothArmsRaise")
        {
            SetArmTarget(playerIndex, true, raiseAmount);
            SetArmTarget(playerIndex, false, raiseAmount);
        }
        if (playerIndex == 0)
        {
            playerInfoText[0].text = "Player : " + playerIndex + "\n" + "Gesture : " + gestureName + "\nDelta : " + raiseAmount.ToString("0.00");
        }
        else if (playerIndex == 1)
        {
            playerInfoText[1].text = "Player : " + playerIndex + "\n" + "Gesture : " + gestureName + "\nDelta : " + raiseAmount.ToString("0.00");
        }
    }

    void Update()
    {
        UpdateArmRotations();
    }


    private void PreparePlayers()
    {
        if (players == null) players = new Transform[0];
        var count = players.Length;
        var totalArms = count * 2;
        armBaseRotations = new Quaternion[totalArms];
        armTargetAngles = new float[totalArms];
        armCurrentAngles = new float[totalArms];
        armLastEventTimes = new float[totalArms];
        for (int playerIndex = 0; playerIndex < count; playerIndex++)
        {
            for (int side = 0; side < 2; side++)
            {
                int slot = ArmSlot(playerIndex, side == 0);
                armBaseRotations[slot] = Quaternion.identity;
                armLastEventTimes[slot] = float.NegativeInfinity;
                var arm = GetArmTransform(playerIndex, side == 0);
                if (arm != null)
                {
                    armBaseRotations[slot] = arm.localRotation;
                }
            }
        }
    }

    private bool IsValidPlayer(int index)
    {
        return index >= 0 && players != null && index < players.Length && players[index] != null;
    }

    private float GetRaiseAmount(object[] values)
    {
        if (values == null || values.Length == 0 || values[0] == null) return 0f;
        if (values[0] is float f) return Mathf.Clamp01(f);
        if (values[0] is double d) return Mathf.Clamp01((float)d);
        return 0f;
    }

    private void SetArmTarget(int playerIndex, bool isLeft, float raiseAmount)
    {
        if (armTargetAngles == null || armLastEventTimes == null) return;
        int slot = ArmSlot(playerIndex, isLeft);
        if (slot < 0 || slot >= armTargetAngles.Length) return;
        armTargetAngles[slot] = maxArmRotation * Mathf.Clamp01(raiseAmount);
        armLastEventTimes[slot] = Time.time;
    }

    private void UpdateArmRotations()
    {
        for (int i = 0; i < players.Length; i++)
        {
            UpdateSingleArm(i, true);
            UpdateSingleArm(i, false);
        }
    }

    private void UpdateSingleArm(int playerIndex, bool isLeft)
    {
        if (armBaseRotations == null || armTargetAngles == null || armCurrentAngles == null || armLastEventTimes == null) return;
        int slot = ArmSlot(playerIndex, isLeft);
        if (slot < 0 || slot >= armBaseRotations.Length || slot >= armTargetAngles.Length || slot >= armCurrentAngles.Length || slot >= armLastEventTimes.Length) return;
        var arm = GetArmTransform(playerIndex, isLeft);
        if (arm == null) return;

        float targetAngle = armTargetAngles[slot];
        if (Time.time - armLastEventTimes[slot] > armIdleTimeout)
        {
            targetAngle = 0f;
        }

        armCurrentAngles[slot] = Mathf.Lerp(armCurrentAngles[slot], targetAngle, Time.deltaTime * armLerpSpeed);

        var baseRotation = armBaseRotations[slot];
        arm.localRotation = baseRotation * Quaternion.Euler(-armCurrentAngles[slot], 0f, 0f);
    }

    private int ArmSlot(int playerIndex, bool isLeft)
    {
        if (!IsValidPlayer(playerIndex)) return -1;
        return playerIndex * 2 + (isLeft ? 0 : 1);
    }

    private Transform GetArmTransform(int playerIndex, bool isLeft)
    {
        // player 0 uses leftPlayerArms, player 1 uses rightPlayerArms; index 0 = left arm, 1 = right arm
        Transform[] source = null;
        if (playerIndex == 0) source = leftPlayerArms;
        else if (playerIndex == 1) source = rightPlayerArms;
        else return null;

        if (source == null) return null;
        int index = isLeft ? 0 : 1;
        if (index >= source.Length) return null;
        return source[index];
    }
}