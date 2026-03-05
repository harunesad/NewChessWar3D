using System;
using System.Collections.Generic;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodyMovementDetector : BodylinkBaseGestureDetector
    {
        [Header("Base Thresholds (adjusted by height)")]
        public float jumpThreshold = 0.03f;
        public float armRaiseThreshold = 0.2f;

        [Header("Body Move Settings")]
        [SerializeField] private float moveSensitivity = 0.025f;
        [SerializeField] private float sampleDuration = 0.3f;
        [SerializeField] private float gestureCooldown = 0.35f;

        private float playerOneLastMoveTime = 0f;
        private float playerTwoLastMoveTime = 0f;

        private readonly Queue<(float x, float time)> playerOneFeetPositions = new();
        private readonly Queue<(float x, float time)> playerTwoFeetPositions = new();
        private readonly Dictionary<string, float>[] lastGestureTimes = new Dictionary<string, float>[2] { new(), new() };

        private float[] hipBaseline = new float[2];
        private bool[] hipBaselineSet = new bool[2];

        [Header("Calibration Data")]
        public bool EnableMoveLeft;
        public bool EnableMoveRight;
        public bool EnableJump;
        public bool EnableArmRaise;
        public bool EnableBothArmRaise;

        private bool IsInitialized;

        void Start()
        {
            Bodylink.Instance.OnInitialized += () =>
            {
                IsInitialized = true;

                // initialize hip baseline for all active players
                int players = Bodylink.Instance.numberOfPlayers;
                for (int p = 0; p < hipBaseline.Length; p++)
                {
                    if (p < players)
                    {
                        hipBaseline[p] = (base.players[p].body2D.leftHip.y + base.players[p].body2D.rightHip.y) * 0.5f;
                        hipBaselineSet[p] = true;
                    }
                    else
                    {
                        hipBaselineSet[p] = false;
                    }
                }

            };

            Bodylink.Instance.OnPlayerOutOfScreen += () =>
            {
                if (Bodylink.Instance.autoRecalibrate == false) return;
                IsInitialized = false;
                playerOneFeetPositions.Clear();
                if (isMultiplayerEnabled)
                    playerTwoFeetPositions.Clear();
                ResetGestureCooldowns(0);
                ResetGestureCooldowns(1);
            };
        }

        void Update()
        {
            ProcessGesture();
        }

        public override void ProcessGesture()
        {
            if (!IsInitialized)
                return;
            if (poseLandMarkerResult.poseLandmarks == null) return;

            if (isMultiplayerEnabled && poseLandMarkerResult.poseLandmarks.Count > 1)
            {
                DetectPoses(0);
                DetectPoses(1);
            }
            else
            {
                DetectPoses(0);
            }
        }

        private void DetectPoses(int playerIndex)
        {
            try
            {

                // ============== ARM RAISE (FULL STRETCH ABOVE HEAD) ==============
                // Body2D shoulder position
                float shoulderY =
                    (players[playerIndex].body2D.leftShoulder.y +
                    players[playerIndex].body2D.rightShoulder.y) * 0.5f;

                // Determine REAL left/right based on screen X position
                float wristLeftY_screen;
                float wristRightY_screen;

                if (players[playerIndex].body2D.leftWrist.x < players[playerIndex].body2D.rightWrist.x)
                {
                    // correct orientation
                    wristLeftY_screen = players[playerIndex].body2D.leftWrist.y;
                    wristRightY_screen = players[playerIndex].body2D.rightWrist.y;
                }
                else
                {
                    // mirrored orientation (common problem)
                    wristLeftY_screen = players[playerIndex].body2D.rightWrist.y;
                    wristRightY_screen = players[playerIndex].body2D.leftWrist.y;
                }

                // Actual arm length
                float armLength = Bodylink.Instance.GetPlayerArmLength(playerIndex);

                // Vertical raise potential
                float maxRaise = armLength * 0.60f;

                // Deltas
                float leftArmDelta = Mathf.Clamp01((wristLeftY_screen - shoulderY) / maxRaise);
                float rightArmDelta = Mathf.Clamp01((wristRightY_screen - shoulderY) / maxRaise);

                // Raise threshold
                float raiseThreshold = armRaiseThreshold * Bodylink.Instance.GetPlayerArmRatio(playerIndex);

                // LEFT ARM
                if (EnableArmRaise && (wristLeftY_screen - shoulderY) > raiseThreshold)
                {
                    //Debug.Log("Left Arm Raise " + leftArmDelta);
                    //OnArmRaise?.Invoke(playerIndex, Side.Left, leftArmDelta);
                    TryInvokeGesture(playerIndex, "LeftArmRaise", leftArmDelta);
                }

                // RIGHT ARM
                if (EnableArmRaise && (wristRightY_screen - shoulderY) > raiseThreshold)
                {
                    //Debug.Log("Right Arm Raise " + rightArmDelta);
                    //OnArmRaise?.Invoke(playerIndex, Side.Right, rightArmDelta);
                    TryInvokeGesture(playerIndex, "RightArmRaise", rightArmDelta);
                }

                // BOTH
                if (EnableBothArmRaise &&
                    (wristLeftY_screen - shoulderY) > raiseThreshold &&
                    (wristRightY_screen - shoulderY) > raiseThreshold)
                {
                    float both = (leftArmDelta + rightArmDelta) * 0.5f;
                    //Debug.Log("Both Arm Raise " + both);
                    //OnBothArmRaise?.Invoke(playerIndex, both);
                    TryInvokeGesture(playerIndex, "BothArmsRaise", both);
                }


                // ---------- BODY2D POINTS (for movement & jump) ----------
                float hipY = (players[playerIndex].body2D.leftHip.y + players[playerIndex].body2D.rightHip.y) * 0.5f;
                float leftFootX = players[playerIndex].body2D.leftFootIndex.x;
                float rightFootX = players[playerIndex].body2D.rightFootIndex.x;
                float feetX = (leftFootX + rightFootX) * 0.5f;

                // calibration / baseline fallback
                if (!hipBaselineSet[playerIndex])
                {
                    hipBaseline[playerIndex] = hipY;
                    hipBaselineSet[playerIndex] = true;
                }

                // player ratios and thresholds
                float heightRatio = Bodylink.Instance.GetPlayerHeightRatio(playerIndex);
                float legLength = Bodylink.Instance.GetPlayerLegLength(playerIndex);
                if (legLength <= 0f) legLength = Mathf.Max(0.5f, Bodylink.Instance.GetPlayerCurrentHeight(playerIndex)); // fallback

                float adjustedMoveSensitivity = moveSensitivity * heightRatio;
                float adjustedJumpThreshold = jumpThreshold * heightRatio; // remains scaled by height

                // call movement detection (see DetectBodyMovement below)
                DetectBodyMovement(feetX, adjustedMoveSensitivity, heightRatio, playerIndex);

                // jump detection with hysteresis
                float hipDelta = hipY - hipBaseline[playerIndex];
                // small deadzone to avoid jitter:
                float jumpDeadzone = adjustedJumpThreshold * 0.5f;

                // trigger jump when hip lifts above threshold and previous hip is near baseline
                if (EnableJump)
                {
                    // Trigger jump when hip rises sufficiently above baseline
                    if (hipDelta > adjustedJumpThreshold && Time.time - playerOneLastMoveTime > 0.18f) // small cooldown so jump and move don't collide
                    {
                        float jumpStrength = Mathf.Clamp01(hipDelta / legLength);
                        //OnJump?.Invoke(playerIndex, jumpStrength);
                        TryInvokeGesture(playerIndex, "Jump", jumpStrength);

                        // After detecting a jump, update baseline slightly lower so quick small bounces don't re-trigger
                        hipBaseline[playerIndex] = hipBaseline[playerIndex] + (hipDelta * 0.5f);
                    }
                    else if (hipDelta < jumpDeadzone)
                    {
                        // restore baseline slowly when player is back down
                        // (optional smoothing to avoid resetting too fast)
                        hipBaseline[playerIndex] = Mathf.Lerp(hipBaseline[playerIndex], hipY, 0.2f);
                    }
                }

            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to detect body movement for player {playerIndex}: {e}");
            }
        }

        private void DetectBodyMovement(float feetX, float adjustedSensitivity, float heightRatio, int playerIndex)
        {
            float now = Time.time;
            Queue<(float x, float time)> feetPositions = playerIndex == 0 ? playerOneFeetPositions : playerTwoFeetPositions;
            ref float lastMoveTime = ref playerIndex == 0 ? ref playerOneLastMoveTime : ref playerTwoLastMoveTime;

            // Enqueue current sample (use median-like smoothing: clamp extreme spikes)
            float sampleX = feetX;
            feetPositions.Enqueue((sampleX, now));

            // keep samples only in sampleDuration window
            while (feetPositions.Count > 2 && now - feetPositions.Peek().time > sampleDuration)
                feetPositions.Dequeue();

            if (feetPositions.Count < 2) return;

            // compute delta between oldest and newest sample
            var first = feetPositions.Peek();
            var last = feetPositions.ToArray()[^1];

            float deltaX = last.x - first.x;
            float deltaTime = last.time - first.time;
            if (deltaTime <= 0f) return;

            // suppress repeated moves with short cooldown
            if (Time.time - lastMoveTime < 0.25f) return;

            // scale sensitivity by height ratio so small players are comparable
            float threshold = adjustedSensitivity;

            // If movement magnitude exceeds threshold => fire
            if (Mathf.Abs(deltaX) > threshold)
            {
                float strength = Mathf.Clamp01(Mathf.Abs(deltaX) / (0.5f * heightRatio)); // normalized "strength"
                if (deltaX > 0f && EnableMoveRight)
                {
                    //OnBodyMoveRight?.Invoke(playerIndex, deltaX * heightRatio);
                    TryInvokeGesture(playerIndex, "BodyMovesRight", deltaX * heightRatio);
                }
                else if (deltaX < 0f && EnableMoveLeft)
                {
                    //OnBodyMoveLeft?.Invoke(playerIndex, -deltaX * heightRatio);
                    TryInvokeGesture(playerIndex, "BodyMovesLeft", -deltaX * heightRatio);
                }

                lastMoveTime = Time.time;
            }
        }

        private bool TryInvokeGesture(int playerIndex, string gestureName, float strength)
        {
            if (playerIndex < 0 || playerIndex >= lastGestureTimes.Length) return false;

            Dictionary<string, float> gestureTimes = lastGestureTimes[playerIndex];
            if (gestureTimes.TryGetValue(gestureName, out float lastTime))
            {
                if (Time.time - lastTime < gestureCooldown)
                    return false;
            }

            OnGestureDetect(playerIndex, gestureName, strength);
            gestureTimes[gestureName] = Time.time;
            return true;
        }

        private void ResetGestureCooldowns(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= lastGestureTimes.Length) return;
            lastGestureTimes[playerIndex].Clear();
        }

    }
}
