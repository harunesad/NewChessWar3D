using System;
using System.Collections.Generic;
using BodylinkSDK;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;

public class StaticPoseAnalyzer : MonoBehaviour
{
    [Serializable]
    public class PoseFileReference
    {
        public string poseName;
        public TextAsset poseFile;
    }

    [Serializable]
    public struct PoseScore
    {
        public string poseName;
        [Range(0f, 1f)] public float confidence;
    }

    [Serializable]
    private class PlayerRealtimeOutput
    {
        [Range(0, 1)] public int playerSlot;
        public string detectedPoseName = "Analyzing";
        [Range(0f, 1f)] public float detectedPoseConfidence;
        public List<PoseScore> poseScores = new List<PoseScore>();
    }

    [Serializable]
    private class PoseJsonData
    {
        public TrackedPosePoint[] trackedLandmarks;
        public TrackedPoseAngle[] trackedAngles;
    }

    [Serializable]
    private struct TrackedPosePoint
    {
        public int index;
        public float x;
        public float y;
    }

    [Serializable]
    private struct TrackedPoseAngle
    {
        public string name;
        public int a;
        public int b;
        public int c;
        public float angle;
    }

    private class PoseTemplate
    {
        public string poseName;
        public PosePointSample[] trackedLandmarks;
        public PoseAngleSample[] trackedAngles;
    }

    private sealed class PlayerPoseState
    {
        public readonly int PlayerSlot;
        public string DetectedPoseName = "Analyzing";
        public float DetectedPoseConfidence;
        public string LastLoggedPoseName = string.Empty;
        public float LastLoggedPoseConfidence = -1f;
        public string LastNotifiedPoseName = string.Empty;
        public float LastNotifiedPoseConfidence = -1f;
        public readonly Dictionary<string, float> CurrentScores = new Dictionary<string, float>();
        public readonly List<PoseScore> PoseScores = new List<PoseScore>();
        public readonly Vector2[] RawLandmarks = new Vector2[LandmarkCount];
        public readonly float[] LandmarkVisibility = new float[LandmarkCount];

        public PlayerPoseState(int playerSlot)
        {
            PlayerSlot = playerSlot;
        }
    }

    private readonly struct PosePointSample
    {
        public readonly int Index;
        public readonly Vector2 Position;

        public PosePointSample(int index, Vector2 position)
        {
            Index = index;
            Position = position;
        }
    }

    private readonly struct PoseAngleSample
    {
        public readonly int A;
        public readonly int B;
        public readonly int C;
        public readonly float Angle;

        public PoseAngleSample(int a, int b, int c, float angle)
        {
            A = a;
            B = b;
            C = c;
            Angle = angle;
        }
    }

    private const int LandmarkCount = 33;
    private const float MinVectorEpsilon = 0.0001f;
    private const float MinTemplateSpread = 0.0001f;
    private const float LandmarkPerfectDistance = 0.06f;
    private const float LandmarkToleranceDistance = 0.45f;
    private const float MinTrackedVisibility = 0.15f;
    private const float PoseChangeEventConfidenceDelta = 0.01f;
    private const int MaxSupportedPlayers = 2;
    public const string PosesFolderAssetPath = "Assets/StaticPoses";

    [Header("Pose Files")]
    [Tooltip("Drag JSON TextAssets from Assets/StaticPoses here.")]
    [SerializeField] private List<PoseFileReference> poseFiles = new List<PoseFileReference>();

    [Header("Detection Settings")]
    [Range(0, 1)]
    [SerializeField] private int playerSlot = 0;
    [Range(3f, 90f)]
    [SerializeField] private float angleToleranceDegrees = 35f;
    [Range(0f, 1f)]
    [SerializeField] private float detectionThreshold = 0.45f;
    [SerializeField] private bool includeMirroredComparison = true;
    [SerializeField] private bool debugEnabled;

    [Header("Realtime Output")]
    [SerializeField] private string detectedPoseName = "Analyzing";
    [SerializeField, Range(0f, 1f)] private float detectedPoseConfidence;
    [SerializeField] private List<PoseScore> poseScores = new List<PoseScore>();
    [SerializeField] private List<PlayerRealtimeOutput> playerRealtimeOutputs = new List<PlayerRealtimeOutput>();

    private PoseLandmarkerResult poseLandmarkerResult;
    private bool isSubscribedToRunner;
    private string lastNotifiedPoseName = string.Empty;
    private float lastNotifiedPoseConfidence = -1f;

    private readonly List<PoseTemplate> templates = new List<PoseTemplate>();
    private readonly Dictionary<string, float> currentScores = new Dictionary<string, float>();
    private readonly float[] currentLandmarkVisibility = new float[LandmarkCount];
    private readonly List<float> scoreBuffer = new List<float>(LandmarkCount);
    private readonly List<Vector2> templateSourcePointBuffer = new List<Vector2>(LandmarkCount);
    private readonly List<Vector2> liveSourcePointBuffer = new List<Vector2>(LandmarkCount);
    private readonly List<Vector2> templatePointBuffer = new List<Vector2>(LandmarkCount);
    private readonly List<Vector2> livePointBuffer = new List<Vector2>(LandmarkCount);
    private readonly object poseResultLock = new object();
    private readonly PlayerPoseState[] playerStates =
    {
        new PlayerPoseState(0),
        new PlayerPoseState(1)
    };

    public string DetectedPoseName => detectedPoseName;
    public float DetectedPoseConfidence => detectedPoseConfidence;
    public IReadOnlyDictionary<string, float> CurrentScores => currentScores;
    public IReadOnlyList<PoseScore> PoseScores => poseScores;
    public event Action<string, float> OnPoseChanged;
    public event Action<IReadOnlyList<PoseScore>> OnPoseScoresUpdated;
    public event Action<int, string, float> OnPlayerPoseChanged;
    public event Action<int, IReadOnlyList<PoseScore>> OnPlayerPoseScoresUpdated;

    private void OnEnable()
    {
        EnsurePlayerRealtimeOutputs();
        RebuildPoseTemplates();
        RefreshSelectedPlayerStateCache();

        if (Bodylink.Instance != null)
        {
            Bodylink.Instance.OnInitialized += OnBodylinkInitialized;
            if (Bodylink.Instance.IsInitialized)
            {
                OnBodylinkInitialized();
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromRunner();
        if (Bodylink.Instance != null)
        {
            Bodylink.Instance.OnInitialized -= OnBodylinkInitialized;
        }
    }

    private void OnValidate()
    {
        playerSlot = Mathf.Clamp(playerSlot, 0, 1);
        angleToleranceDegrees = Mathf.Clamp(angleToleranceDegrees, 1f, 180f);
        detectionThreshold = Mathf.Clamp01(detectionThreshold);
        EnsurePlayerRealtimeOutputs();
        RebuildPoseTemplates();
        RefreshSelectedPlayerStateCache();
    }

    private void OnBodylinkInitialized()
    {
        TrySubscribeToRunner();
    }

    private void TrySubscribeToRunner()
    {
        if (isSubscribedToRunner || Bodylink.Instance == null || Bodylink.Instance.PoseLandmarkerRunnerInstance == null)
        {
            return;
        }

        Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult += OnPoseLandmarkDetectionResult;
        isSubscribedToRunner = true;
    }

    private void UnsubscribeFromRunner()
    {
        if (!isSubscribedToRunner || Bodylink.Instance == null || Bodylink.Instance.PoseLandmarkerRunnerInstance == null)
        {
            return;
        }

        Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult -= OnPoseLandmarkDetectionResult;
        isSubscribedToRunner = false;
    }

    private void OnPoseLandmarkDetectionResult(PoseLandmarkerResult result, long timestamp)
    {
        lock (poseResultLock)
        {
            poseLandmarkerResult = result;
        }
    }

    private void Update()
    {
        if (!isSubscribedToRunner && Bodylink.Instance != null && Bodylink.Instance.IsInitialized)
        {
            TrySubscribeToRunner();
        }

        if (Bodylink.Instance == null || !Bodylink.Instance.IsInitialized)
        {
            SetNoMatchStateForAllPlayers("Analyzing");
            return;
        }

        if (templates.Count == 0)
        {
            SetNoMatchStateForAllPlayers("No Pose Files");
            return;
        }

        int trackedPlayerCount = Bodylink.Instance.isMultiplayerEnabled ? MaxSupportedPlayers : 1;
        for (int slot = 0; slot < MaxSupportedPlayers; slot++)
        {
            if (slot >= trackedPlayerCount)
            {
                SetNoMatchState(slot, "Analyzing");
                continue;
            }

            int mediapipeIndex = ResolveMediapipePlayerIndex(slot);
            PlayerPoseState playerState = GetPlayerState(slot);
            if (!TryReadPlayerLandmarks(mediapipeIndex, playerState.RawLandmarks, playerState.LandmarkVisibility))
            {
                SetNoMatchState(slot, "Analyzing");
                continue;
            }

            EvaluatePoseTemplates(slot, playerState.RawLandmarks, playerState.LandmarkVisibility);
        }

        RefreshSelectedPlayerStateCache();
    }

    public float GetPoseConfidence(string poseName)
    {
        return GetPoseConfidence(playerSlot, poseName);
    }

    public float GetPoseConfidence(int targetPlayerSlot, string poseName)
    {
        if (string.IsNullOrEmpty(poseName))
        {
            return 0f;
        }

        PlayerPoseState playerState = GetPlayerState(targetPlayerSlot);
        return playerState.CurrentScores.TryGetValue(poseName, out float score) ? score : 0f;
    }

    public string GetBestPose(float threshold = -1f)
    {
        return GetBestPose(playerSlot, threshold);
    }

    public string GetBestPose(int targetPlayerSlot, float threshold = -1f)
    {
        float requiredThreshold = threshold >= 0f ? threshold : detectionThreshold;
        PlayerPoseState playerState = GetPlayerState(targetPlayerSlot);
        if (playerState.DetectedPoseConfidence < requiredThreshold)
        {
            return "Analyzing";
        }

        return playerState.DetectedPoseName;
    }

    public string GetDetectedPoseName(int targetPlayerSlot)
    {
        return GetPlayerState(targetPlayerSlot).DetectedPoseName;
    }

    public float GetDetectedPoseConfidence(int targetPlayerSlot)
    {
        return GetPlayerState(targetPlayerSlot).DetectedPoseConfidence;
    }

    public IReadOnlyList<PoseScore> GetPoseScores(int targetPlayerSlot)
    {
        return GetPlayerState(targetPlayerSlot).PoseScores;
    }

    public IReadOnlyDictionary<string, float> GetCurrentScores(int targetPlayerSlot)
    {
        return GetPlayerState(targetPlayerSlot).CurrentScores;
    }

    private int ResolveMediapipePlayerIndex(int targetPlayerSlot)
    {
        if (Bodylink.Instance == null || !Bodylink.Instance.isMultiplayerEnabled || Bodylink.Instance.bodylinkAvatar == null)
        {
            return targetPlayerSlot;
        }

        int[] stablePlayerIndex = Bodylink.Instance.bodylinkAvatar.stablePlayerIndex;
        if (stablePlayerIndex == null || stablePlayerIndex.Length <= targetPlayerSlot)
        {
            return targetPlayerSlot;
        }

        return stablePlayerIndex[targetPlayerSlot];
    }

    private bool TryReadPlayerLandmarks(int mediapipeIndex, Vector2[] output, float[] visibilityOutput)
    {
        if (output == null || output.Length < LandmarkCount || visibilityOutput == null || visibilityOutput.Length < LandmarkCount)
        {
            return false;
        }

        PoseLandmarkerResult snapshot;
        lock (poseResultLock)
        {
            snapshot = poseLandmarkerResult;
        }

        if (snapshot.poseLandmarks == null ||
            mediapipeIndex < 0 ||
            mediapipeIndex >= snapshot.poseLandmarks.Count)
        {
            return false;
        }

        var landmarks = snapshot.poseLandmarks[mediapipeIndex].landmarks;
        if (landmarks == null || landmarks.Count < LandmarkCount)
        {
            return false;
        }

        try
        {
            for (int i = 0; i < LandmarkCount; i++)
            {
                output[i] = new Vector2(landmarks[i].x, landmarks[i].y);
                float visibility = landmarks[i].visibility.GetValueOrDefault(1f);
                float presence = landmarks[i].presence.GetValueOrDefault(1f);
                visibilityOutput[i] = Mathf.Max(visibility, presence);
            }
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private void EvaluatePoseTemplates(int targetPlayerSlot, Vector2[] liveLandmarks, float[] landmarkVisibility)
    {
        PlayerPoseState playerState = GetPlayerState(targetPlayerSlot);
        ApplyCurrentLandmarkVisibility(landmarkVisibility);
        playerState.CurrentScores.Clear();
        playerState.PoseScores.Clear();

        string bestPose = "Analyzing";
        float bestConfidence = 0f;

        for (int i = 0; i < templates.Count; i++)
        {
            PoseTemplate template = templates[i];
            float confidence = CalculateTemplateConfidence(template, liveLandmarks);

            playerState.CurrentScores[template.poseName] = confidence;
            playerState.PoseScores.Add(new PoseScore
            {
                poseName = template.poseName,
                confidence = confidence
            });

            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestPose = template.poseName;
            }
        }

        playerState.DetectedPoseConfidence = Mathf.Clamp01(bestConfidence);
        playerState.DetectedPoseName = playerState.DetectedPoseConfidence >= detectionThreshold ? bestPose : "Analyzing";
        SyncPlayerRealtimeOutput(playerState);
        NotifyPlayerPoseScoresUpdated(playerState);
        NotifyPlayerPoseChangedIfNeeded(playerState);

        if (debugEnabled &&
            (playerState.DetectedPoseName != playerState.LastLoggedPoseName ||
             Mathf.Abs(playerState.DetectedPoseConfidence - playerState.LastLoggedPoseConfidence) > 0.01f))
        {
            Debug.Log("[StaticPoseAnalyzer] Player " + (playerState.PlayerSlot + 1) + " Pose: " +
                      playerState.DetectedPoseName + " (" + playerState.DetectedPoseConfidence.ToString("0.000") + ")");
            playerState.LastLoggedPoseName = playerState.DetectedPoseName;
            playerState.LastLoggedPoseConfidence = playerState.DetectedPoseConfidence;
        }
    }

    private float CalculateTemplateConfidence(PoseTemplate template, Vector2[] liveLandmarks)
    {
        if (template == null || liveLandmarks == null)
        {
            return 0f;
        }

        if (!TryCalculateCombinedTemplateConfidence(template, liveLandmarks, mirror: false, out float confidence))
        {
            return 0f;
        }

        if (includeMirroredComparison && TryCalculateCombinedTemplateConfidence(template, liveLandmarks, mirror: true, out float mirroredConfidence))
        {
            confidence = Mathf.Max(confidence, mirroredConfidence);
        }

        return confidence;
    }

    private bool TryCalculateCombinedTemplateConfidence(PoseTemplate template, Vector2[] liveLandmarks, bool mirror, out float confidence)
    {
        confidence = 0f;

        bool hasAngleConfidence = TryCalculateAngleTemplateConfidence(template, liveLandmarks, mirror, out float angleConfidence);
        bool hasLandmarkConfidence = TryCalculateLandmarkTemplateConfidence(template, liveLandmarks, mirror, out float landmarkConfidence);

        if (!hasAngleConfidence && !hasLandmarkConfidence)
        {
            return false;
        }

        if (hasAngleConfidence && hasLandmarkConfidence)
        {
            float angleWeight = template.trackedAngles != null && template.trackedAngles.Length >= 4 ? 0.60f : 0.45f;
            float blendedConfidence = (angleConfidence * angleWeight) + (landmarkConfidence * (1f - angleWeight));
            confidence = Mathf.Max(angleConfidence, blendedConfidence);
            return true;
        }

        confidence = hasLandmarkConfidence ? landmarkConfidence : angleConfidence;
        return true;
    }

    private bool TryCalculateAngleTemplateConfidence(PoseTemplate template, Vector2[] liveLandmarks, bool mirror, out float confidence)
    {
        confidence = 0f;

        if (template == null || template.trackedAngles == null || template.trackedAngles.Length == 0 || liveLandmarks == null)
        {
            return false;
        }

        float perfectWindow = Mathf.Min(15f, angleToleranceDegrees * 0.5f);
        float effectiveTolerance = Mathf.Max(angleToleranceDegrees, perfectWindow + 0.001f);
        scoreBuffer.Clear();
        int lowScoreCount = 0;

        for (int i = 0; i < template.trackedAngles.Length; i++)
        {
            PoseAngleSample sample = template.trackedAngles[i];
            int a = mirror ? MirrorLandmarkIndex(sample.A) : sample.A;
            int b = mirror ? MirrorLandmarkIndex(sample.B) : sample.B;
            int c = mirror ? MirrorLandmarkIndex(sample.C) : sample.C;

            if (!HasSufficientVisibility(a) || !HasSufficientVisibility(b) || !HasSufficientVisibility(c))
            {
                continue;
            }

            if (!TryCalculateJointAngle(liveLandmarks, a, b, c, out float liveAngle))
            {
                continue;
            }

            float delta = Mathf.Abs(Mathf.DeltaAngle(liveAngle, sample.Angle));
            float score = ScoreAngleDelta(delta, perfectWindow, effectiveTolerance);
            scoreBuffer.Add(score);
            if (score < 0.35f)
            {
                lowScoreCount++;
            }
        }

        if (scoreBuffer.Count == 0)
        {
            return false;
        }

        scoreBuffer.Sort();

        int dropCount = 0;
        if (scoreBuffer.Count >= 8)
        {
            dropCount = 2;
        }
        else if (scoreBuffer.Count >= 5)
        {
            dropCount = 1;
        }

        int keptCount = scoreBuffer.Count - dropCount;
        if (keptCount <= 0)
        {
            return false;
        }

        float baseConfidence = ComputeWeightedBaseScore(scoreBuffer, dropCount);
        float lowScoreRatio = lowScoreCount / (float)scoreBuffer.Count;
        float mismatchPenalty = ComputeMismatchPenalty(lowScoreRatio, scoreBuffer.Count);
        confidence = Mathf.Clamp01(baseConfidence * mismatchPenalty);
        return true;
    }

    private bool TryCalculateLandmarkTemplateConfidence(PoseTemplate template, Vector2[] liveLandmarks, bool mirror, out float confidence)
    {
        confidence = 0f;

        if (template == null ||
            template.trackedLandmarks == null ||
            template.trackedLandmarks.Length < 2 ||
            liveLandmarks == null ||
            liveLandmarks.Length < LandmarkCount)
        {
            return false;
        }

        templateSourcePointBuffer.Clear();
        liveSourcePointBuffer.Clear();

        for (int i = 0; i < template.trackedLandmarks.Length; i++)
        {
            PosePointSample pointSample = template.trackedLandmarks[i];
            int liveIndex = mirror ? MirrorLandmarkIndex(pointSample.Index) : pointSample.Index;
            if (!IsValidLandmarkIndex(liveIndex))
            {
                continue;
            }

            if (!HasSufficientVisibility(liveIndex))
            {
                continue;
            }

            templateSourcePointBuffer.Add(pointSample.Position);
            liveSourcePointBuffer.Add(liveLandmarks[liveIndex]);
        }

        if (templateSourcePointBuffer.Count < 2 || liveSourcePointBuffer.Count < 2)
        {
            return false;
        }

        float bestConfidence = 0f;
        bool hasValidTransform = false;

        for (int transform = 0; transform < 4; transform++)
        {
            bool flipX = (transform & 1) != 0;
            bool flipY = (transform & 2) != 0;
            if (!TryCalculateLandmarkTemplateConfidenceForTransform(flipX, flipY, out float candidateConfidence))
            {
                continue;
            }

            hasValidTransform = true;
            if (candidateConfidence > bestConfidence)
            {
                bestConfidence = candidateConfidence;
            }
        }

        if (!hasValidTransform)
        {
            return false;
        }

        confidence = bestConfidence;
        return true;
    }

    private bool TryCalculateLandmarkTemplateConfidenceForTransform(bool flipX, bool flipY, out float confidence)
    {
        confidence = 0f;
        templatePointBuffer.Clear();
        livePointBuffer.Clear();

        for (int i = 0; i < templateSourcePointBuffer.Count; i++)
        {
            Vector2 templatePoint = templateSourcePointBuffer[i];
            Vector2 livePoint = liveSourcePointBuffer[i];

            if (flipX)
            {
                livePoint.x = 1f - livePoint.x;
            }

            if (flipY)
            {
                livePoint.y = 1f - livePoint.y;
            }

            templatePointBuffer.Add(templatePoint);
            livePointBuffer.Add(livePoint);
        }

        if (!TryNormalizePointSet(templatePointBuffer) || !TryNormalizePointSet(livePointBuffer))
        {
            return false;
        }

        float dot = 0f;
        float cross = 0f;
        for (int i = 0; i < templatePointBuffer.Count; i++)
        {
            Vector2 templatePoint = templatePointBuffer[i];
            Vector2 livePoint = livePointBuffer[i];
            dot += (livePoint.x * templatePoint.x) + (livePoint.y * templatePoint.y);
            cross += (livePoint.x * templatePoint.y) - (livePoint.y * templatePoint.x);
        }

        float rotation = Mathf.Atan2(cross, dot);
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);

        float totalScore = 0f;
        int lowScoreCount = 0;

        for (int i = 0; i < templatePointBuffer.Count; i++)
        {
            Vector2 templatePoint = templatePointBuffer[i];
            Vector2 livePoint = livePointBuffer[i];
            Vector2 rotatedLivePoint = new Vector2(
                (livePoint.x * cos) - (livePoint.y * sin),
                (livePoint.x * sin) + (livePoint.y * cos));

            float distance = Vector2.Distance(rotatedLivePoint, templatePoint);
            float score = ScoreDistanceDelta(distance, LandmarkPerfectDistance, LandmarkToleranceDistance);
            totalScore += score;
            if (score < 0.35f)
            {
                lowScoreCount++;
            }
        }

        float baseConfidence = totalScore / templatePointBuffer.Count;
        float lowScoreRatio = lowScoreCount / (float)templatePointBuffer.Count;
        float mismatchPenalty = ComputeMismatchPenalty(lowScoreRatio, templatePointBuffer.Count);
        confidence = Mathf.Clamp01(baseConfidence * mismatchPenalty);
        return true;
    }

    private static bool TryNormalizePointSet(List<Vector2> points)
    {
        if (points == null || points.Count < 2)
        {
            return false;
        }

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < points.Count; i++)
        {
            centroid += points[i];
        }
        centroid /= points.Count;

        float sqrMagnitudeSum = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 centered = points[i] - centroid;
            points[i] = centered;
            sqrMagnitudeSum += centered.sqrMagnitude;
        }

        float scale = Mathf.Sqrt(sqrMagnitudeSum / points.Count);
        if (scale < MinTemplateSpread)
        {
            return false;
        }

        for (int i = 0; i < points.Count; i++)
        {
            points[i] /= scale;
        }

        return true;
    }

    private static float ComputeWeightedBaseScore(List<float> sortedScores, int startIndex)
    {
        if (sortedScores == null)
        {
            return 0f;
        }

        int count = sortedScores.Count - startIndex;
        if (count <= 0)
        {
            return 0f;
        }

        if (count == 1)
        {
            return sortedScores[sortedScores.Count - 1];
        }

        if (count == 2)
        {
            float low = sortedScores[startIndex];
            float high = sortedScores[startIndex + 1];
            return (high * 0.75f) + (low * 0.25f);
        }

        if (count == 3)
        {
            float low = sortedScores[startIndex];
            float mid = sortedScores[startIndex + 1];
            float high = sortedScores[startIndex + 2];
            return (high * 0.50f) + (mid * 0.30f) + (low * 0.20f);
        }

        float total = 0f;
        for (int i = startIndex; i < sortedScores.Count; i++)
        {
            total += sortedScores[i];
        }

        return total / count;
    }

    private bool HasSufficientVisibility(int index)
    {
        if (index < 0 || index >= currentLandmarkVisibility.Length)
        {
            return false;
        }

        return currentLandmarkVisibility[index] >= MinTrackedVisibility;
    }

    private static float ComputeMismatchPenalty(float lowScoreRatio, int sampleCount)
    {
        lowScoreRatio = Mathf.Clamp01(lowScoreRatio);
        int clampedSampleCount = Mathf.Max(sampleCount, 1);
        float validRatio = 1f - lowScoreRatio;

        if (clampedSampleCount <= 2)
        {
            return Mathf.Lerp(1f, validRatio, 0.25f);
        }

        if (clampedSampleCount <= 4)
        {
            return Mathf.Pow(validRatio, 0.65f);
        }

        if (clampedSampleCount <= 7)
        {
            return Mathf.Pow(validRatio, 1.00f);
        }

        return Mathf.Pow(validRatio, 1.6f);
    }

    private static float ScoreAngleDelta(float delta, float perfectWindow, float tolerance)
    {
        if (delta <= perfectWindow)
        {
            return 1f;
        }

        if (delta >= tolerance)
        {
            return 0f;
        }

        float normalized = Mathf.InverseLerp(perfectWindow, tolerance, delta);
        return 1f - (normalized * normalized);
    }

    private static float ScoreDistanceDelta(float distance, float perfectDistance, float tolerance)
    {
        if (distance <= perfectDistance)
        {
            return 1f;
        }

        if (distance >= tolerance)
        {
            return 0f;
        }

        float normalized = Mathf.InverseLerp(perfectDistance, tolerance, distance);
        return 1f - (normalized * normalized);
    }

    private static bool TryCalculateJointAngle(Vector2[] landmarks, int a, int b, int c, out float angle)
    {
        angle = 0f;
        if (landmarks == null ||
            a < 0 || a >= landmarks.Length ||
            b < 0 || b >= landmarks.Length ||
            c < 0 || c >= landmarks.Length)
        {
            return false;
        }

        Vector2 fromPivotToA = landmarks[a] - landmarks[b];
        Vector2 fromPivotToC = landmarks[c] - landmarks[b];

        float magnitudeA = fromPivotToA.magnitude;
        float magnitudeC = fromPivotToC.magnitude;
        if (magnitudeA < MinVectorEpsilon || magnitudeC < MinVectorEpsilon)
        {
            return false;
        }

        float dot = Vector2.Dot(fromPivotToA / magnitudeA, fromPivotToC / magnitudeC);
        dot = Mathf.Clamp(dot, -1f, 1f);
        angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        return true;
    }

    private static int MirrorLandmarkIndex(int index)
    {
        switch (index)
        {
            case 1: return 4;
            case 2: return 5;
            case 3: return 6;
            case 4: return 1;
            case 5: return 2;
            case 6: return 3;
            case 7: return 8;
            case 8: return 7;
            case 9: return 10;
            case 10: return 9;
            case 11: return 12;
            case 12: return 11;
            case 13: return 14;
            case 14: return 13;
            case 15: return 16;
            case 16: return 15;
            case 17: return 18;
            case 18: return 17;
            case 19: return 20;
            case 20: return 19;
            case 21: return 22;
            case 22: return 21;
            case 23: return 24;
            case 24: return 23;
            case 25: return 26;
            case 26: return 25;
            case 27: return 28;
            case 28: return 27;
            case 29: return 30;
            case 30: return 29;
            case 31: return 32;
            case 32: return 31;
            default: return index;
        }
    }

    private void SetNoMatchStateForAllPlayers(string name)
    {
        for (int slot = 0; slot < MaxSupportedPlayers; slot++)
        {
            SetNoMatchState(slot, name);
        }

        RefreshSelectedPlayerStateCache();
    }

    private void SetNoMatchState(int targetPlayerSlot, string name)
    {
        PlayerPoseState playerState = GetPlayerState(targetPlayerSlot);
        playerState.DetectedPoseName = name;
        playerState.DetectedPoseConfidence = 0f;

        if (playerState.CurrentScores.Count > 0)
        {
            playerState.CurrentScores.Clear();
        }

        if (playerState.PoseScores.Count > 0)
        {
            playerState.PoseScores.Clear();
        }

        SyncPlayerRealtimeOutput(playerState);
        NotifyPlayerPoseScoresUpdated(playerState);
        NotifyPlayerPoseChangedIfNeeded(playerState);
    }

    private void NotifyPoseChangedIfNeeded()
    {
        bool poseNameChanged = detectedPoseName != lastNotifiedPoseName;
        bool confidenceChanged = Mathf.Abs(detectedPoseConfidence - lastNotifiedPoseConfidence) >= PoseChangeEventConfidenceDelta;
        if (!poseNameChanged && !confidenceChanged)
        {
            return;
        }

        lastNotifiedPoseName = detectedPoseName;
        lastNotifiedPoseConfidence = detectedPoseConfidence;
        OnPoseChanged?.Invoke(detectedPoseName, detectedPoseConfidence);
    }

    private void NotifyPoseScoresUpdated()
    {
        OnPoseScoresUpdated?.Invoke(poseScores);
    }

    private void NotifyPlayerPoseChangedIfNeeded(PlayerPoseState playerState)
    {
        bool poseNameChanged = playerState.DetectedPoseName != playerState.LastNotifiedPoseName;
        bool confidenceChanged = Mathf.Abs(playerState.DetectedPoseConfidence - playerState.LastNotifiedPoseConfidence) >= PoseChangeEventConfidenceDelta;
        if (!poseNameChanged && !confidenceChanged)
        {
            return;
        }

        playerState.LastNotifiedPoseName = playerState.DetectedPoseName;
        playerState.LastNotifiedPoseConfidence = playerState.DetectedPoseConfidence;
        OnPlayerPoseChanged?.Invoke(playerState.PlayerSlot, playerState.DetectedPoseName, playerState.DetectedPoseConfidence);
    }

    private void NotifyPlayerPoseScoresUpdated(PlayerPoseState playerState)
    {
        OnPlayerPoseScoresUpdated?.Invoke(playerState.PlayerSlot, playerState.PoseScores);
    }

    private void EnsurePlayerRealtimeOutputs()
    {
        if (playerRealtimeOutputs == null)
        {
            playerRealtimeOutputs = new List<PlayerRealtimeOutput>(MaxSupportedPlayers);
        }

        while (playerRealtimeOutputs.Count < MaxSupportedPlayers)
        {
            playerRealtimeOutputs.Add(new PlayerRealtimeOutput
            {
                playerSlot = playerRealtimeOutputs.Count
            });
        }

        while (playerRealtimeOutputs.Count > MaxSupportedPlayers)
        {
            playerRealtimeOutputs.RemoveAt(playerRealtimeOutputs.Count - 1);
        }

        for (int i = 0; i < playerRealtimeOutputs.Count; i++)
        {
            PlayerRealtimeOutput output = playerRealtimeOutputs[i];
            output.playerSlot = i;
            if (output.poseScores == null)
            {
                output.poseScores = new List<PoseScore>();
            }
        }
    }

    private PlayerPoseState GetPlayerState(int targetPlayerSlot)
    {
        int clampedSlot = Mathf.Clamp(targetPlayerSlot, 0, MaxSupportedPlayers - 1);
        return playerStates[clampedSlot];
    }

    private void ApplyCurrentLandmarkVisibility(float[] landmarkVisibility)
    {
        if (landmarkVisibility == null)
        {
            Array.Clear(currentLandmarkVisibility, 0, currentLandmarkVisibility.Length);
            return;
        }

        int copyLength = Mathf.Min(currentLandmarkVisibility.Length, landmarkVisibility.Length);
        Array.Copy(landmarkVisibility, currentLandmarkVisibility, copyLength);
        for (int i = copyLength; i < currentLandmarkVisibility.Length; i++)
        {
            currentLandmarkVisibility[i] = 0f;
        }
    }

    private void SyncPlayerRealtimeOutput(PlayerPoseState playerState)
    {
        EnsurePlayerRealtimeOutputs();
        PlayerRealtimeOutput output = playerRealtimeOutputs[playerState.PlayerSlot];
        output.playerSlot = playerState.PlayerSlot;
        output.detectedPoseName = playerState.DetectedPoseName;
        output.detectedPoseConfidence = playerState.DetectedPoseConfidence;
        output.poseScores.Clear();
        output.poseScores.AddRange(playerState.PoseScores);
    }

    private void RefreshSelectedPlayerStateCache()
    {
        PlayerPoseState selectedPlayerState = GetPlayerState(playerSlot);
        detectedPoseName = selectedPlayerState.DetectedPoseName;
        detectedPoseConfidence = selectedPlayerState.DetectedPoseConfidence;

        currentScores.Clear();
        foreach (KeyValuePair<string, float> pair in selectedPlayerState.CurrentScores)
        {
            currentScores[pair.Key] = pair.Value;
        }

        poseScores.Clear();
        poseScores.AddRange(selectedPlayerState.PoseScores);

        for (int i = 0; i < playerStates.Length; i++)
        {
            SyncPlayerRealtimeOutput(playerStates[i]);
        }

        NotifyPoseScoresUpdated();
        NotifyPoseChangedIfNeeded();
    }

    private void RebuildPoseTemplates()
    {
        templates.Clear();
        if (poseFiles == null)
        {
            return;
        }

        for (int i = 0; i < poseFiles.Count; i++)
        {
            PoseFileReference poseEntry = poseFiles[i];
            if (poseEntry == null || poseEntry.poseFile == null)
            {
                continue;
            }

            if (!TryParsePoseJson(poseEntry.poseFile.text, out PosePointSample[] trackedLandmarks, out PoseAngleSample[] trackedAngles))
            {
                Debug.LogWarning("[StaticPoseAnalyzer] Invalid pose JSON (trackedLandmarks and/or trackedAngles required): " + poseEntry.poseFile.name);
                continue;
            }

            PoseTemplate template = new PoseTemplate
            {
                poseName = ResolvePoseName(poseEntry),
                trackedLandmarks = trackedLandmarks,
                trackedAngles = trackedAngles
            };

            templates.Add(template);
        }
    }

    private static string ResolvePoseName(PoseFileReference poseEntry)
    {
        if (!string.IsNullOrWhiteSpace(poseEntry.poseName))
        {
            return poseEntry.poseName.Trim();
        }

        if (poseEntry.poseFile != null)
        {
            return poseEntry.poseFile.name;
        }

        return "UnnamedPose";
    }

    private static bool TryParsePoseJson(string json, out PosePointSample[] trackedLandmarks, out PoseAngleSample[] trackedAngles)
    {
        trackedLandmarks = null;
        trackedAngles = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        PoseJsonData data = JsonUtility.FromJson<PoseJsonData>(json);
        bool hasTrackedLandmarks = TryParseTrackedPosePointData(data, out trackedLandmarks);
        bool hasTrackedAngles = TryParseTrackedPoseAngleData(data, out trackedAngles);
        return hasTrackedLandmarks || hasTrackedAngles;
    }

    private static bool TryParseTrackedPosePointData(PoseJsonData data, out PosePointSample[] trackedLandmarks)
    {
        trackedLandmarks = null;
        if (data == null || data.trackedLandmarks == null || data.trackedLandmarks.Length == 0)
        {
            return false;
        }

        List<PosePointSample> points = new List<PosePointSample>(data.trackedLandmarks.Length);
        for (int i = 0; i < data.trackedLandmarks.Length; i++)
        {
            TrackedPosePoint trackedPoint = data.trackedLandmarks[i];
            if (!IsValidLandmarkIndex(trackedPoint.index))
            {
                continue;
            }

            points.Add(new PosePointSample(trackedPoint.index, new Vector2(trackedPoint.x, trackedPoint.y)));
        }

        if (points.Count < 2)
        {
            return false;
        }

        trackedLandmarks = points.ToArray();
        return true;
    }

    private static bool TryParseTrackedPoseAngleData(PoseJsonData data, out PoseAngleSample[] trackedAngles)
    {
        trackedAngles = null;
        if (data == null || data.trackedAngles == null || data.trackedAngles.Length == 0)
        {
            return false;
        }

        List<PoseAngleSample> angles = new List<PoseAngleSample>(data.trackedAngles.Length);
        for (int i = 0; i < data.trackedAngles.Length; i++)
        {
            TrackedPoseAngle trackedAngle = data.trackedAngles[i];
            if (!IsValidLandmarkIndex(trackedAngle.a) ||
                !IsValidLandmarkIndex(trackedAngle.b) ||
                !IsValidLandmarkIndex(trackedAngle.c))
            {
                continue;
            }

            float clampedAngle = Mathf.Clamp(trackedAngle.angle, 0f, 180f);
            angles.Add(new PoseAngleSample(trackedAngle.a, trackedAngle.b, trackedAngle.c, clampedAngle));
        }

        if (angles.Count == 0)
        {
            return false;
        }

        trackedAngles = angles.ToArray();
        return true;
    }

    private static bool IsValidLandmarkIndex(int index)
    {
        return index >= 0 && index < LandmarkCount;
    }
}
