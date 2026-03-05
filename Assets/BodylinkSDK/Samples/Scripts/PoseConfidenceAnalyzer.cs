using System;
using System.Collections;
using System.Collections.Generic;
using BodylinkSDK;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;

public class PoseConfidenceAnalyzer : MonoBehaviour
{
    [System.Serializable]
    public struct PoseResult
    {
        public string poseName;
        public float confidence; // 0–1
    }

    public bool idlePoseEnabled = true;
    public bool tPoseEnabled = true;
    public bool aPoseEnabled = true;
    public bool bothArmRaiseEnabled = true;
    public bool crouchEnabled = true;
    public bool oneLegBalanceEnabled = true;
    public bool crossArmEnabled = true;
    public bool armFlexEnabled = true;
    public bool handsTogetherEnabled = true;
    public bool heroEnabled = true;
    [Header("Debug")]
    public bool debugEnabled = false;


    private Dictionary<string, float> playerOneScore;
    private Dictionary<string, float> playerTwoScore;
    private PoseLandmarkerResult poseLandmarkerResult;

    void Start()
    {
        Bodylink.Instance.OnInitialized += () =>
        {
            playerOneScore = new();
            playerTwoScore = new();

            Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult += (result, timeStamp) =>
            {
                poseLandmarkerResult = result;
            };
        };

    }

    void Update()
    {
        if (!Bodylink.Instance.IsInitialized) return;
        if (poseLandmarkerResult.poseLandmarks == null || poseLandmarkerResult.poseLandmarks.Count == 0) return;

        if (Bodylink.Instance.isMultiplayerEnabled == false)
        {
            ApplyPoseConfidenceScore(0);
        }
        else
        {

            int playerOne = Bodylink.Instance.bodylinkAvatar.stablePlayerIndex[0];
            ApplyPoseConfidenceScore(playerOne);
            int playerTwo = Bodylink.Instance.bodylinkAvatar.stablePlayerIndex[1];
            if (playerTwo < 0) return;
            ApplyPoseConfidenceScore(playerTwo);
        }

    }

    private void ApplyPoseConfidenceScore(int playerIndex)
    {
        var scores = CalculateConfidences(playerIndex);
        foreach (var pose in scores)
        {
            if (playerIndex == 0)
                playerOneScore[pose.Key] = pose.Value;
            else
                playerTwoScore[pose.Key] = pose.Value;

            if (debugEnabled)
            {
                Debug.Log("Player " + playerIndex + ": " + pose.Key + ": " + pose.Value);
            }
        }
    }

    // Call this every frame after pose detection
    public Dictionary<string, float> CalculateConfidences(int index)
    {

        try
        {
            if (poseLandmarkerResult.poseLandmarks == null ||
                index < 0 ||
                index >= poseLandmarkerResult.poseLandmarks.Count)
            {
                return new Dictionary<string, float>();
            }

            var landmarks = poseLandmarkerResult.poseLandmarks[index].landmarks;
            if (landmarks == null || landmarks.Count <= 28)
            {
                return new Dictionary<string, float>();
            }

            Vector3 lWrist = new Vector3(landmarks[16].x, landmarks[16].y, 0); //landmarks[15];
            Vector3 rWrist = new Vector3(landmarks[15].x, landmarks[15].y, 0); //landmarks[16];
            Vector3 lElbow = new Vector3(landmarks[14].x, landmarks[14].y, 0); //landmarks[13];
            Vector3 rElbow = new Vector3(landmarks[13].x, landmarks[13].y, 0); //landmarks[14];
            Vector3 lShoulder = new Vector3(landmarks[12].x, landmarks[12].y, 0); //landmarks[11];
            Vector3 rShoulder = new Vector3(landmarks[11].x, landmarks[11].y, 0); //landmarks[12];
            Vector3 lHip = new Vector3(landmarks[24].x, landmarks[24].y, 0); //landmarks[23];
            Vector3 rHip = new Vector3(landmarks[23].x, landmarks[23].y, 0); //landmarks[24];
            Vector3 lKnee = new Vector3(landmarks[26].x, landmarks[26].y, 0); //landmarks[25];
            Vector3 rKnee = new Vector3(landmarks[25].x, landmarks[25].y, 0); //landmarks[26];
            Vector3 lAnkle = new Vector3(landmarks[28].x, landmarks[28].y, 0); //landmarks[27];
            Vector3 rAnkle = new Vector3(landmarks[27].x, landmarks[27].y, 0); //landmarks[28];


            float lArmAngle = CalculateAngle(lShoulder, lElbow, lWrist);
            float rArmAngle = CalculateAngle(rShoulder, rElbow, rWrist);
            float lShoulderAngle = CalculateAngle(lHip, lShoulder, lElbow);
            float rShoulderAngle = CalculateAngle(rHip, rShoulder, rElbow);
            float lKneeAngle = CalculateAngle(lHip, lKnee, lAnkle);
            float rKneeAngle = CalculateAngle(rHip, rKnee, rAnkle);

            Dictionary<string, float> confidences = new Dictionary<string, float>();


            if (idlePoseEnabled)
            {
                confidences["Idle"] =
                    (GetMatch(lShoulderAngle, 15) +
                     GetMatch(rShoulderAngle, 15)) / 2f;
            }

            if (tPoseEnabled)
            {

                confidences["T-Pose"] =
                (
                    GetMatch(lShoulderAngle, 90, 25) +
                    GetMatch(rShoulderAngle, 90, 25) +
                    GetMatch(lArmAngle, 180, 35) +
                    GetMatch(rArmAngle, 180, 35)
                ) / 4f;
            }


            if (aPoseEnabled)
            {
                // -------- Improved & Relaxed A-Pose --------
                // Arms angled down, mostly straight, wrists below shoulders

                float lWristBelowShoulder = lWrist.y > lShoulder.y ? 1f : 0f;
                float rWristBelowShoulder = rWrist.y > rShoulder.y ? 1f : 0f;

                confidences["A-Pose"] =
                (
                    GetMatch(lShoulderAngle, 45, 25) +   // was 20
                    GetMatch(rShoulderAngle, 45, 25) +
                    GetMatch(lArmAngle, 170, 30) +       // was 20
                    GetMatch(rArmAngle, 170, 30) +
                    lWristBelowShoulder +
                    rWristBelowShoulder
                ) / 6f;
            }


            if (bothArmRaiseEnabled)
            {
                // -------- Fixed Both Arm Raise (No T-Pose leakage) --------

                // Head reference (Bodylink head or midpoint of ears if needed)
                Vector3 head = new Vector3(landmarks[0].x, landmarks[0].y, 0);

                // 1. Height progress (shoulder → head)
                float lHeight = Mathf.InverseLerp(lShoulder.y, head.y - 0.03f, lWrist.y);
                float rHeight = Mathf.InverseLerp(rShoulder.y, head.y - 0.03f, rWrist.y);

                lHeight = Mathf.Clamp01(lHeight);
                rHeight = Mathf.Clamp01(rHeight);

                // 2. Arm straightness
                float lStraight = GetMatch(lArmAngle, 170, 35);
                float rStraight = GetMatch(rArmAngle, 170, 35);

                // 3. T-Pose suppression (CRITICAL FIX)
                // If shoulder angle ~90°, this becomes ~0
                float lNotTPose = 1f - GetMatch(lShoulderAngle, 90, 20);
                float rNotTPose = 1f - GetMatch(rShoulderAngle, 90, 20);

                // 4. Final confidence
                confidences["BothArmRaise"] =
                (
                    (lHeight * lStraight * lNotTPose) +
                    (rHeight * rStraight * rNotTPose)
                ) / 2f;
            }


            if (crouchEnabled)
            {
                // -------- Improved Crouch (Depth-based & Stable) --------

                // 1. Knee bend (both knees)
                float lKneeBent = GetMatch(lKneeAngle, 90, 35);
                float rKneeBent = GetMatch(rKneeAngle, 90, 35);
                float kneeScore = (lKneeBent + rKneeBent) / 2f;

                // 2. Hip lowering relative to shoulders
                float lHipDrop = Mathf.InverseLerp(
                    lShoulder.y,          // standing
                    lAnkle.y - 0.05f,     // deep crouch
                    lHip.y
                );

                float rHipDrop = Mathf.InverseLerp(
                    rShoulder.y,
                    rAnkle.y - 0.05f,
                    rHip.y
                );

                float hipDropScore = Mathf.Clamp01((lHipDrop + rHipDrop) / 2f);

                // 3. Feet height consistency (avoid one-leg balance)
                float footHeightDiff = Mathf.Abs(lAnkle.y - rAnkle.y);
                float bothFeetGrounded = 1f - Mathf.Clamp01(footHeightDiff * 5f);

                // 4. Final crouch confidence
                confidences["Crouch"] =
                (
                    kneeScore * 0.4f +
                    hipDropScore * 0.4f +
                    bothFeetGrounded * 0.2f
                );
            }


            if (oneLegBalanceEnabled)
            {
                // -------- Improved One-Leg Balance --------
                // One leg straight + other leg bent + ankle height difference

                // Check for balance - hip stability (both hips should be relatively level)
                float hipHeightDiff = Mathf.Abs(lHip.y - rHip.y);
                float hipStability = Mathf.Clamp01(1f - hipHeightDiff * 10f);

                // Leg straightness with tolerance
                float leftLegStraight = GetMatch(lKneeAngle, 180, 30);
                float rightLegStraight = GetMatch(rKneeAngle, 180, 30);

                // Leg bent (for lifted leg)
                float leftLegBent = GetMatch(lKneeAngle, 90, 60);
                float rightLegBent = GetMatch(rKneeAngle, 90, 60);

                // Ankle vertical separation (lifted leg)
                float ankleHeightDiff = Mathf.Abs(lAnkle.y - rAnkle.y);
                float liftScore = Mathf.Clamp01(ankleHeightDiff * 8f);

                // Check which leg is lifted based on ankle height
                bool leftLifted = lAnkle.y > rAnkle.y;

                if (leftLifted)
                {
                    // Left leg lifted, right leg standing
                    confidences["OneLegBalance"] = (
                        rightLegStraight * 1.5f +    // Standing leg should be straight (weighted more)
                        leftLegBent * 1.2f +         // Lifted leg should be bent
                        liftScore +
                        hipStability * 0.8f
                    ) / 4.5f;
                }
                else
                {
                    // Right leg lifted, left leg standing
                    confidences["OneLegBalance"] = (
                        leftLegStraight * 1.5f +
                        rightLegBent * 1.2f +
                        liftScore +
                        hipStability * 0.8f
                    ) / 4.5f;
                }
            }

            if (crossArmEnabled)
            {
                float lCrossDist = Vector2.Distance(lWrist, rShoulder);
                float rCrossDist = Vector2.Distance(rWrist, lShoulder);
                confidences["CrossArm"] =
                    Mathf.Min(1f, (GetMatch(lCrossDist, 0.1f, 0.2f) +
                                   GetMatch(rCrossDist, 0.1f, 0.2f)) / 2f);
            }

            if (armFlexEnabled)
            {

                confidences["ArmFlex"] =
                    (GetMatch(lShoulderAngle, 90) +
                     GetMatch(lArmAngle, 45) +
                     GetMatch(rShoulderAngle, 90) +
                     GetMatch(rArmAngle, 45)) / 4f;
            }

            if (handsTogetherEnabled)
            {

                float wristDist = Vector2.Distance(lWrist, rWrist);
                confidences["HandsTogether"] = GetMatch(wristDist, 0.05f, 0.15f);
            }

            if (heroEnabled)
            {

                float heroLeft =
                    (GetMatch(lShoulderAngle, 170) + GetMatch(rShoulderAngle, 45)) / 2f;
                float heroRight =
                    (GetMatch(rShoulderAngle, 170) + GetMatch(lShoulderAngle, 45)) / 2f;
                confidences["Hero"] = Mathf.Max(heroLeft, heroRight);
            }

            return confidences;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.ToString());
            return new Dictionary<string, float>();
        }


    }

    // =========================
    // Helper Methods
    // =========================

    float CalculateAngle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector2 ab = a - b;
        Vector2 cb = c - b;
        float angle = Vector2.Angle(ab, cb);
        return angle;
    }

    float GetMatch(float current, float target, float tolerance = 30f)
    {
        float diff = Mathf.Abs(current - target);
        if (diff > tolerance) return 0f;
        return 1f - (diff / tolerance);
    }

    // =========================
    // Best Pose Detection
    // =========================

    public string GetBestPose(Dictionary<string, float> scores, float threshold = 0.6f)
    {
        string bestPose = "Analyzing";
        float bestScore = 0f;

        foreach (var pose in scores)
        {
            if (pose.Value > bestScore && pose.Value >= threshold)
            {
                bestScore = pose.Value;
                bestPose = pose.Key;
            }
        }

        return bestPose;
    }

    public float GetPoseConfidence(PoseType poseType, int playerIndex = 0)
    {
        try
        {
            if (playerIndex == 0)
            {
                if (playerOneScore == null) return 0;
                switch (poseType)
                {
                    case PoseType.Idle:
                        return playerOneScore["Idle"];
                    case PoseType.TPose:
                        return TryGetScore(playerOneScore, "T-Pose");
                    case PoseType.APose:
                        return TryGetScore(playerOneScore, "A-Pose");
                    case PoseType.BothArmsUp:
                        return TryGetScore(playerOneScore, "BothArmRaise");
                    case PoseType.Crouch:
                        return TryGetScore(playerOneScore, "Crouch");
                    case PoseType.OneLegBalance:
                        return TryGetScore(playerOneScore, "OneLegBalance");
                    case PoseType.CrossArm:
                        return TryGetScore(playerOneScore, "CrossArm");
                    case PoseType.ArmFlex:
                        return TryGetScore(playerOneScore, "ArmFlex");
                    case PoseType.HandsTogether:
                        return TryGetScore(playerOneScore, "HandsTogether");
                    case PoseType.HeroPose:
                        return TryGetScore(playerOneScore, "Hero");
                    default:
                        return 0f;
                }
            }
            else
            {
                switch (poseType)
                {
                    case PoseType.Idle:
                        return TryGetScore(playerTwoScore, "Idle");
                    case PoseType.TPose:
                        return TryGetScore(playerTwoScore, "T-Pose");
                    case PoseType.APose:
                        return TryGetScore(playerTwoScore, "A-Pose");
                    case PoseType.BothArmsUp:
                        return TryGetScore(playerTwoScore, "BothArmRaise");
                    case PoseType.Crouch:
                        return TryGetScore(playerTwoScore, "Crouch");
                    case PoseType.OneLegBalance:
                        return TryGetScore(playerTwoScore, "OneLegBalance");
                    case PoseType.CrossArm:
                        return TryGetScore(playerTwoScore, "CrossArm");
                    case PoseType.ArmFlex:
                        return TryGetScore(playerTwoScore, "ArmFlex");
                    case PoseType.HandsTogether:
                        return TryGetScore(playerTwoScore, "HandsTogether");
                    case PoseType.HeroPose:
                        return TryGetScore(playerTwoScore, "Hero");
                    default:
                        return 0f;
                }
            }
        }
        catch (Exception ex)
        {
            //Debug.LogWarning(ex.ToString());
            return 0f;
        }
    }

    float TryGetScore(Dictionary<string, float> scores, string key)
    {
        if (scores == null) return 0f;
        return scores.TryGetValue(key, out var value) ? value : 0f;
    }
}

public enum PoseType
{
    Idle,
    TPose,
    APose,
    BothArmsUp,
    Crouch,
    OneLegBalance,
    CrossArm,
    ArmFlex,
    HandsTogether,
    HeroPose
}