using System;
using System.Collections.Generic;
using BodylinkSDK;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

[AddComponentMenu("Bodylink/Exercise Tracker")]
public class ExerciseTracker : BodylinkBaseGestureDetector
{
    public enum ExerciseType
    {
        Squat = 0,
        PushUp = 1,
        JumpingJack = 2
    }

    public enum TrackingPhase
    {
        WaitingForStartPose = 0,
        MovingToTargetPose = 1,
        ReturningToStartPose = 2
    }

    public readonly struct ExerciseEvaluation
    {
        public ExerciseType Exercise { get; }
        public bool HasValidPose { get; }
        public float Progress { get; }
        public float Accuracy { get; }
        public bool IsStartPose { get; }
        public bool IsTargetPose { get; }
        public float Symmetry { get; }
        public float FormScore { get; }

        public ExerciseEvaluation(
            ExerciseType exercise,
            bool hasValidPose,
            float progress,
            float accuracy,
            bool isStartPose,
            bool isTargetPose,
            float symmetry,
            float formScore)
        {
            Exercise = exercise;
            HasValidPose = hasValidPose;
            Progress = Mathf.Clamp01(progress);
            Accuracy = Mathf.Clamp01(accuracy);
            IsStartPose = isStartPose;
            IsTargetPose = isTargetPose;
            Symmetry = Mathf.Clamp01(symmetry);
            FormScore = Mathf.Clamp01(formScore);
        }
    }

    private sealed class PlayerTrackingState
    {
        public readonly int PlayerSlot;
        public readonly List<float> RepetitionScores = new List<float>();
        public readonly List<float> CurrentSetScores = new List<float>();
        public readonly List<float> CompletedSetScores = new List<float>();
        public TrackingPhase TrackingPhase = TrackingPhase.WaitingForStartPose;
        public ExerciseEvaluation LastEvaluation;
        public float CurrentProgress;
        public float CurrentRepCycleProgress;
        public float DisplayedProgress;
        public float CurrentAccuracy;
        public float AverageRepAccuracy;
        public float LastRepAccuracy;
        public float ConsistencyScore = 1f;
        public float WorkoutProgress;
        public float RepPeakAccuracy;
        public float RepAccumulatedAccuracy;
        public float FinalScore;
        public int RepSampleCount;
        public int RepetitionCount;
        public int CompletedSets;
        public string StatusMessage = "Waiting for Bodylink";

        public PlayerTrackingState(int playerSlot)
        {
            PlayerSlot = playerSlot;
        }
    }

    private const int MaxSupportedPlayers = 2;
    private const float MinimumBodyWidth = 0.05f;
    private const float MinimumSegmentLength = 0.0001f;

    [Header("Exercise")]
    [SerializeField] private ExerciseType exercise = ExerciseType.Squat;
    [SerializeField, Min(0)] private int playerSlot;
    [SerializeField] private bool useSmoothedPoints = true;
    [SerializeField, Range(0f, 1f)] private float minimumVisibility = 0.5f;
    [SerializeField, Min(1)] private int targetRepetitions = 10;

    [Header("Tracking")]
    [SerializeField, Min(1)] private int repsPerSet = 10;
    [SerializeField, Range(0f, 1f)] private float mediumThreshold = 0.55f;
    [SerializeField, Range(0f, 1f)] private float highThreshold = 0.75f;
    [SerializeField] private float progressLerpSpeed = 10f;
    [SerializeField] private bool evaluateEveryFrame = true;
    [SerializeField] private bool debugLogs;

    private readonly PlayerTrackingState[] playerStates =
    {
        new PlayerTrackingState(0),
        new PlayerTrackingState(1)
    };

    private bool isTrackingPaused;
    private string pausedStatusMessage;

    public event Action<ExerciseTracker> MetricsUpdated;
    public event Action<ExerciseTracker, int, float, int> RepetitionCompleted;
    public event Action<ExerciseTracker, int, int, float, int> PlayerRepetitionCompleted;

    public ExerciseType CurrentExercise => exercise;
    public int SelectedPlayerSlot => playerSlot;
    public int RepetitionCount => GetRepetitionCount(playerSlot);
    public int CompletedSets => GetCompletedSets(playerSlot);
    public int CurrentSetRepCount => GetCurrentSetRepCount(playerSlot);
    public int CurrentSetNumber => GetCurrentSetNumber(playerSlot);
    public int TargetRepetitions => targetRepetitions;
    public int RepsPerSet => repsPerSet;
    public float LiveProgress => GetLiveProgress(playerSlot);
    public float WorkoutProgress => GetWorkoutProgress(playerSlot);
    public float LiveAccuracy => GetLiveAccuracy(playerSlot);
    public float AverageRepAccuracy => GetAverageRepAccuracy(playerSlot);
    public float LastRepAccuracy => GetLastRepAccuracy(playerSlot);
    public float ConsistencyScore => GetConsistencyScore(playerSlot);
    public float FinalScore => GetFinalScore(playerSlot);
    public float MediumThreshold => mediumThreshold;
    public float HighThreshold => highThreshold;
    public TrackingPhase CurrentTrackingPhase => GetTrackingPhase(playerSlot);
    public ExerciseEvaluation LastEvaluation => GetLastEvaluation(playerSlot);
    public bool HasValidPose => HasValidPoseForPlayer(playerSlot);
    public bool IsWorkoutComplete => IsPlayerWorkoutComplete(playerSlot);
    public bool IsTrackingPaused => isTrackingPaused;
    public string ExerciseDisplayName => GetExerciseDisplayName();
    public string ExerciseCountLabel => GetExerciseCountLabel();
    public string StatusMessage => GetStatusMessage(playerSlot);

    private void Awake()
    {
        ResetWorkout();
    }

    private void OnValidate()
    {
        playerSlot = Mathf.Clamp(playerSlot, 0, MaxSupportedPlayers - 1);
        minimumVisibility = Mathf.Clamp01(minimumVisibility);
        targetRepetitions = Mathf.Max(1, targetRepetitions);
        repsPerSet = Mathf.Max(1, repsPerSet);
        progressLerpSpeed = Mathf.Max(0f, progressLerpSpeed);
        highThreshold = Mathf.Max(mediumThreshold, highThreshold);
    }

    private void Update()
    {
        if (evaluateEveryFrame && !isTrackingPaused)
        {
            ProcessGesture();
        }

        for (int i = 0; i < playerStates.Length; i++)
        {
            PlayerTrackingState playerState = playerStates[i];
            playerState.DisplayedProgress = Mathf.Lerp(
                playerState.DisplayedProgress,
                playerState.CurrentProgress,
                1f - Mathf.Exp(-progressLerpSpeed * Time.deltaTime));
        }
    }

    public override void ProcessGesture()
    {
        EvaluateNow();
    }

    [ContextMenu("Reset Workout")]
    public void ResetWorkout()
    {
        for (int i = 0; i < playerStates.Length; i++)
        {
            ResetPlayerState(playerStates[i]);
        }

        RaiseMetricsUpdated();
    }

    public void SetExercise(ExerciseType nextExercise)
    {
        bool alreadyReset = exercise == nextExercise;
        for (int i = 0; i < playerStates.Length; i++)
        {
            if (playerStates[i].RepetitionCount > 0 || playerStates[i].CompletedSets > 0)
            {
                alreadyReset = false;
                break;
            }
        }

        if (alreadyReset)
        {
            return;
        }

        exercise = nextExercise;
        ResetWorkout();
    }

    public void PauseTracking(string pauseMessage = null)
    {
        isTrackingPaused = true;
        pausedStatusMessage = pauseMessage;

        for (int i = 0; i < playerStates.Length; i++)
        {
            PlayerTrackingState playerState = playerStates[i];
            playerState.CurrentProgress = 0f;
            playerState.CurrentRepCycleProgress = 0f;
            playerState.CurrentAccuracy = 0f;
        }

        RaiseMetricsUpdated();
    }

    public void ResumeTracking()
    {
        isTrackingPaused = false;
        pausedStatusMessage = null;
        RaiseMetricsUpdated();
    }

    public void EvaluateNow()
    {
        if (isTrackingPaused)
        {
            return;
        }

        int trackedPlayerCount = GetTrackedPlayerCount();
        for (int i = 0; i < playerStates.Length; i++)
        {
            if (i >= trackedPlayerCount)
            {
                SetInactivePlayerState(playerStates[i], i);
                continue;
            }

            EvaluatePlayer(playerStates[i]);
        }

        RaiseMetricsUpdated();
    }

    public int GetTrackedPlayerCount()
    {
        return Bodylink.Instance != null &&
               Bodylink.Instance.IsInitialized &&
               Bodylink.Instance.isMultiplayerEnabled
            ? MaxSupportedPlayers
            : 1;
    }

    public bool IsPlayerActive(int targetPlayerSlot)
    {
        return targetPlayerSlot >= 0 && targetPlayerSlot < GetTrackedPlayerCount();
    }

    public int GetRepetitionCount(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).RepetitionCount;
    public int GetCompletedSets(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).CompletedSets;
    public int GetCurrentSetRepCount(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).CurrentSetScores.Count;
    public int GetCurrentSetNumber(int targetPlayerSlot) => GetCompletedSets(targetPlayerSlot) + 1;
    public float GetLiveProgress(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).DisplayedProgress;
    public float GetWorkoutProgress(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).WorkoutProgress;
    public float GetLiveAccuracy(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).CurrentAccuracy * 100f;
    public float GetAverageRepAccuracy(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).AverageRepAccuracy * 100f;
    public float GetLastRepAccuracy(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).LastRepAccuracy * 100f;
    public float GetConsistencyScore(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).ConsistencyScore * 100f;
    public float GetFinalScore(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).FinalScore * 100f;
    public TrackingPhase GetTrackingPhase(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).TrackingPhase;
    public ExerciseEvaluation GetLastEvaluation(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).LastEvaluation;
    public bool HasValidPoseForPlayer(int targetPlayerSlot) => GetPlayerState(targetPlayerSlot).LastEvaluation.HasValidPose;
    public bool IsPlayerWorkoutComplete(int targetPlayerSlot) => GetRepetitionCount(targetPlayerSlot) >= targetRepetitions;

    public string GetStatusMessage(int targetPlayerSlot)
    {
        if (!string.IsNullOrEmpty(pausedStatusMessage))
        {
            return pausedStatusMessage;
        }

        return GetPlayerState(targetPlayerSlot).StatusMessage;
    }

    private void ResetPlayerState(PlayerTrackingState playerState)
    {
        playerState.TrackingPhase = TrackingPhase.WaitingForStartPose;
        playerState.LastEvaluation = default;
        playerState.CurrentProgress = 0f;
        playerState.CurrentRepCycleProgress = 0f;
        playerState.DisplayedProgress = 0f;
        playerState.CurrentAccuracy = 0f;
        playerState.AverageRepAccuracy = 0f;
        playerState.LastRepAccuracy = 0f;
        playerState.ConsistencyScore = 1f;
        playerState.WorkoutProgress = 0f;
        playerState.FinalScore = 0f;
        playerState.RepetitionCount = 0;
        playerState.CompletedSets = 0;
        playerState.RepetitionScores.Clear();
        playerState.CurrentSetScores.Clear();
        playerState.CompletedSetScores.Clear();
        ResetRepCapture(playerState);
        playerState.StatusMessage = GetUnavailableStatus();
    }

    private void SetInactivePlayerState(PlayerTrackingState playerState, int targetPlayerSlot)
    {
        string inactiveStatus = GetInactivePlayerStatus(targetPlayerSlot);
        bool alreadyInactive =
            playerState.RepetitionCount == 0 &&
            playerState.CompletedSets == 0 &&
            playerState.DisplayedProgress <= 0f &&
            playerState.CurrentAccuracy <= 0f &&
            playerState.StatusMessage == inactiveStatus &&
            !playerState.LastEvaluation.HasValidPose;

        if (alreadyInactive)
        {
            return;
        }

        ResetPlayerState(playerState);
        playerState.StatusMessage = inactiveStatus;
    }

    private void EvaluatePlayer(PlayerTrackingState playerState)
    {
        if (!TryEvaluateCurrentExercise(playerState.PlayerSlot, out ExerciseEvaluation evaluation))
        {
            playerState.LastEvaluation = CreateInvalidEvaluation(exercise);
            playerState.CurrentRepCycleProgress = playerState.TrackingPhase == TrackingPhase.ReturningToStartPose ? 0.5f : 0f;
            FadeLivePose(playerState, GetUnavailableStatus());
            UpdateWorkoutProgress(playerState);
            return;
        }

        playerState.LastEvaluation = evaluation;
        playerState.CurrentAccuracy = evaluation.Accuracy;

        if (!evaluation.HasValidPose)
        {
            playerState.CurrentRepCycleProgress = playerState.TrackingPhase == TrackingPhase.ReturningToStartPose ? 0.5f : 0f;
            FadeLivePose(playerState, "Move fully into frame");
            UpdateWorkoutProgress(playerState);
            return;
        }

        playerState.CurrentProgress = evaluation.Progress;

        switch (playerState.TrackingPhase)
        {
            case TrackingPhase.WaitingForStartPose:
                playerState.CurrentRepCycleProgress = 0f;
                playerState.StatusMessage = GetStartPoseStatus();

                if (evaluation.IsStartPose)
                {
                    playerState.TrackingPhase = TrackingPhase.MovingToTargetPose;
                    ResetRepCapture(playerState);
                    playerState.StatusMessage = GetTargetPoseStatus(evaluation);
                }
                break;

            case TrackingPhase.MovingToTargetPose:
                if (evaluation.Progress <= 0.05f && evaluation.IsStartPose)
                {
                    playerState.CurrentRepCycleProgress = 0f;
                    playerState.StatusMessage = GetStartPoseStatus();
                    ResetRepCapture(playerState);
                    break;
                }

                CaptureRepSample(playerState, evaluation);
                playerState.CurrentRepCycleProgress = Mathf.Clamp01(evaluation.Progress * 0.5f);
                playerState.StatusMessage = GetTargetPoseStatus(evaluation);

                if (evaluation.IsTargetPose)
                {
                    playerState.TrackingPhase = TrackingPhase.ReturningToStartPose;
                    playerState.StatusMessage = GetReturnPoseStatus(evaluation);
                }
                break;

            case TrackingPhase.ReturningToStartPose:
                CaptureRepSample(playerState, evaluation);
                playerState.CurrentRepCycleProgress = Mathf.Clamp01(0.5f + ((1f - evaluation.Progress) * 0.5f));
                playerState.StatusMessage = GetReturnPoseStatus(evaluation);

                if (evaluation.IsStartPose)
                {
                    CompleteRepetition(playerState);
                    playerState.TrackingPhase = IsPlayerWorkoutComplete(playerState.PlayerSlot)
                        ? TrackingPhase.WaitingForStartPose
                        : TrackingPhase.MovingToTargetPose;
                    playerState.CurrentRepCycleProgress = 0f;
                    playerState.StatusMessage = IsPlayerWorkoutComplete(playerState.PlayerSlot)
                        ? $"{GetExerciseDisplayName()} workout complete"
                        : $"Ready for next {GetExerciseShortLabel()}";
                    ResetRepCapture(playerState);
                }
                break;
        }

        UpdateWorkoutProgress(playerState);
    }

    private void CompleteRepetition(PlayerTrackingState playerState)
    {
        playerState.RepetitionCount++;

        float averageCycleAccuracy = playerState.RepSampleCount > 0
            ? playerState.RepAccumulatedAccuracy / playerState.RepSampleCount
            : playerState.LastEvaluation.Accuracy;

        playerState.LastRepAccuracy = Mathf.Clamp01((playerState.RepPeakAccuracy * 0.65f) + (averageCycleAccuracy * 0.35f));
        playerState.RepetitionScores.Add(playerState.LastRepAccuracy);
        playerState.CurrentSetScores.Add(playerState.LastRepAccuracy);

        if (playerState.CurrentSetScores.Count >= repsPerSet)
        {
            playerState.CompletedSets++;
            playerState.CompletedSetScores.Add(GetAverage(playerState.CurrentSetScores));
            playerState.CurrentSetScores.Clear();
        }

        playerState.AverageRepAccuracy = GetAverage(playerState.RepetitionScores);
        playerState.ConsistencyScore = CalculateConsistencyScore(playerState.RepetitionScores, playerState.AverageRepAccuracy);
        RecalculateFinalScore(playerState);

        if (debugLogs)
        {
            Debug.Log(
                $"[Bodylink] Player {playerState.PlayerSlot + 1} {GetExerciseDisplayName()} rep {playerState.RepetitionCount} completed. " +
                $"Accuracy {(playerState.LastRepAccuracy * 100f):F0}% Final score {(playerState.FinalScore * 100f):F0}%");
        }

        int finalScorePercent = Mathf.RoundToInt(playerState.FinalScore * 100f);
        OnGestureDetect(playerState.PlayerSlot, $"{exercise}RepCompleted", playerState.RepetitionCount, playerState.LastRepAccuracy, finalScorePercent);

        if (IsPlayerWorkoutComplete(playerState.PlayerSlot))
        {
            OnGestureDetect(
                playerState.PlayerSlot,
                $"{exercise}WorkoutCompleted",
                playerState.RepetitionCount,
                playerState.AverageRepAccuracy,
                finalScorePercent);
        }

        PlayerRepetitionCompleted?.Invoke(
            this,
            playerState.PlayerSlot,
            playerState.RepetitionCount,
            playerState.LastRepAccuracy,
            finalScorePercent);

        if (playerState.PlayerSlot == playerSlot)
        {
            RepetitionCompleted?.Invoke(this, playerState.RepetitionCount, playerState.LastRepAccuracy, finalScorePercent);
        }
    }

    private void CaptureRepSample(PlayerTrackingState playerState, ExerciseEvaluation evaluation)
    {
        playerState.RepPeakAccuracy = Mathf.Max(playerState.RepPeakAccuracy, evaluation.Accuracy);
        playerState.RepAccumulatedAccuracy += evaluation.Accuracy;
        playerState.RepSampleCount++;
    }

    private void ResetRepCapture(PlayerTrackingState playerState)
    {
        playerState.RepPeakAccuracy = 0f;
        playerState.RepAccumulatedAccuracy = 0f;
        playerState.RepSampleCount = 0;
    }

    private void RecalculateFinalScore(PlayerTrackingState playerState)
    {
        if (playerState.RepetitionScores.Count == 0)
        {
            playerState.FinalScore = 0f;
            return;
        }

        float setScore = playerState.CompletedSetScores.Count > 0
            ? GetAverage(playerState.CompletedSetScores)
            : GetAverage(playerState.CurrentSetScores);

        if (setScore <= 0f)
        {
            setScore = playerState.AverageRepAccuracy;
        }

        float completion = Mathf.Clamp01((float)playerState.RepetitionCount / targetRepetitions);
        float weightedScore =
            (playerState.AverageRepAccuracy * 0.55f) +
            (playerState.ConsistencyScore * 0.25f) +
            (setScore * 0.20f);

        playerState.FinalScore = Mathf.Clamp01(weightedScore * completion);
    }

    private static float CalculateConsistencyScore(IReadOnlyList<float> repetitionScores, float averageAccuracy)
    {
        if (repetitionScores == null || repetitionScores.Count <= 1)
        {
            return repetitionScores == null || repetitionScores.Count == 0 ? 1f : repetitionScores[0];
        }

        float deviationSum = 0f;
        for (int i = 0; i < repetitionScores.Count; i++)
        {
            deviationSum += Mathf.Abs(repetitionScores[i] - averageAccuracy);
        }

        float averageDeviation = deviationSum / repetitionScores.Count;
        return 1f - Mathf.Clamp01(averageDeviation / 0.25f);
    }

    private void FadeLivePose(PlayerTrackingState playerState, string newStatus)
    {
        playerState.CurrentProgress = Mathf.MoveTowards(playerState.CurrentProgress, 0f, Time.deltaTime * 2f);
        playerState.CurrentAccuracy = Mathf.MoveTowards(playerState.CurrentAccuracy, 0f, Time.deltaTime * 2f);
        playerState.StatusMessage = newStatus;
    }

    private void UpdateWorkoutProgress(PlayerTrackingState playerState)
    {
        playerState.WorkoutProgress = Mathf.Clamp01((playerState.RepetitionCount + playerState.CurrentRepCycleProgress) / targetRepetitions);
    }

    private void RaiseMetricsUpdated()
    {
        MetricsUpdated?.Invoke(this);
    }

    private PlayerTrackingState GetPlayerState(int targetPlayerSlot)
    {
        return playerStates[Mathf.Clamp(targetPlayerSlot, 0, MaxSupportedPlayers - 1)];
    }

    private bool TryEvaluateCurrentExercise(int targetPlayerSlot, out ExerciseEvaluation evaluation)
    {
        evaluation = CreateInvalidEvaluation(exercise);

        if (!TryGetTrackedPlayerAvatar(targetPlayerSlot, out BodylinkPlayerAvatar playerAvatar))
        {
            return false;
        }

        switch (exercise)
        {
            case ExerciseType.Squat:
                evaluation = EvaluateSquat(playerAvatar);
                return true;
            case ExerciseType.PushUp:
                evaluation = EvaluatePushUp(playerAvatar);
                return true;
            case ExerciseType.JumpingJack:
                evaluation = EvaluateJumpingJack(playerAvatar);
                return true;
            default:
                return false;
        }
    }

    private ExerciseEvaluation EvaluateSquat(BodylinkPlayerAvatar playerAvatar)
    {
        if (!TryGetPoint(playerAvatar, 24, out Vector2 leftHip) ||
            !TryGetPoint(playerAvatar, 26, out Vector2 leftKnee) ||
            !TryGetPoint(playerAvatar, 28, out Vector2 leftAnkle) ||
            !TryGetPoint(playerAvatar, 23, out Vector2 rightHip) ||
            !TryGetPoint(playerAvatar, 25, out Vector2 rightKnee) ||
            !TryGetPoint(playerAvatar, 27, out Vector2 rightAnkle))
        {
            return CreateInvalidEvaluation(ExerciseType.Squat);
        }

        float leftKneeAngle = CalculateAngle(leftHip, leftKnee, leftAnkle);
        float rightKneeAngle = CalculateAngle(rightHip, rightKnee, rightAnkle);
        float averageKneeAngle = Average(leftKneeAngle, rightKneeAngle);

        float lowerLegLength = Average(
            Vector2.Distance(leftKnee, leftAnkle),
            Vector2.Distance(rightKnee, rightAnkle));

        float hipToKneeRatio = lowerLegLength > MinimumSegmentLength
            ? Average(leftHip.y - leftKnee.y, rightHip.y - rightKnee.y) / lowerLegLength
            : 1f;

        float kneeProgress = Mathf.InverseLerp(170f, 90f, averageKneeAngle);
        float hipDepthProgress = Mathf.InverseLerp(1.00f, 0.40f, hipToKneeRatio);
        float symmetry = GetSymmetryScore(leftKneeAngle, rightKneeAngle, 30f);
        float formScore = Mathf.Clamp01((kneeProgress * 0.55f) + (hipDepthProgress * 0.45f));
        float accuracy = Mathf.Clamp01((kneeProgress * 0.65f) + (hipDepthProgress * 0.20f) + (symmetry * 0.15f));
        bool isStartPose = averageKneeAngle >= 160f;
        bool isTargetPose = averageKneeAngle <= 105f && hipDepthProgress >= 0.55f;
        float progress = Mathf.Clamp01((kneeProgress * 0.75f) + (hipDepthProgress * 0.25f));

        return new ExerciseEvaluation(
            ExerciseType.Squat,
            true,
            progress,
            accuracy,
            isStartPose,
            isTargetPose,
            symmetry,
            formScore);
    }

    private ExerciseEvaluation EvaluatePushUp(BodylinkPlayerAvatar playerAvatar)
    {
        if (!TryGetPoint(playerAvatar, 12, out Vector2 leftShoulder) ||
            !TryGetPoint(playerAvatar, 14, out Vector2 leftElbow) ||
            !TryGetPoint(playerAvatar, 16, out Vector2 leftWrist) ||
            !TryGetPoint(playerAvatar, 11, out Vector2 rightShoulder) ||
            !TryGetPoint(playerAvatar, 13, out Vector2 rightElbow) ||
            !TryGetPoint(playerAvatar, 15, out Vector2 rightWrist))
        {
            return CreateInvalidEvaluation(ExerciseType.PushUp);
        }

        float leftElbowAngle = CalculateAngle(leftShoulder, leftElbow, leftWrist);
        float rightElbowAngle = CalculateAngle(rightShoulder, rightElbow, rightWrist);
        float averageElbowAngle = Average(leftElbowAngle, rightElbowAngle);
        float bendProgress = Mathf.InverseLerp(170f, 80f, averageElbowAngle);
        float symmetry = GetSymmetryScore(leftElbowAngle, rightElbowAngle, 35f);

        float formScore = 0.75f;
        float formAccumulator = 0f;
        int formSampleCount = 0;

        if (TryGetPoint(playerAvatar, 24, out Vector2 leftHip) &&
            TryGetPoint(playerAvatar, 26, out Vector2 leftKnee))
        {
            formAccumulator += Mathf.InverseLerp(135f, 170f, CalculateAngle(leftShoulder, leftHip, leftKnee));
            formSampleCount++;
        }

        if (TryGetPoint(playerAvatar, 23, out Vector2 rightHip) &&
            TryGetPoint(playerAvatar, 25, out Vector2 rightKnee))
        {
            formAccumulator += Mathf.InverseLerp(135f, 170f, CalculateAngle(rightShoulder, rightHip, rightKnee));
            formSampleCount++;
        }

        if (formSampleCount > 0)
        {
            formScore = formAccumulator / formSampleCount;
        }

        float accuracy = Mathf.Clamp01((bendProgress * 0.70f) + (symmetry * 0.20f) + (formScore * 0.10f));
        bool isStartPose = averageElbowAngle >= 158f && formScore >= 0.30f;
        bool isTargetPose = averageElbowAngle <= 95f && formScore >= 0.30f;

        return new ExerciseEvaluation(
            ExerciseType.PushUp,
            true,
            bendProgress,
            accuracy,
            isStartPose,
            isTargetPose,
            symmetry,
            formScore);
    }

    private ExerciseEvaluation EvaluateJumpingJack(BodylinkPlayerAvatar playerAvatar)
    {
        if (!TryGetPoint(playerAvatar, 12, out Vector2 leftShoulder) ||
            !TryGetAveragePoint(playerAvatar, out Vector2 leftHand, 16, 18, 20, 22) ||
            !TryGetPoint(playerAvatar, 11, out Vector2 rightShoulder) ||
            !TryGetAveragePoint(playerAvatar, out Vector2 rightHand, 15, 17, 19, 21) ||
            !TryGetAveragePoint(playerAvatar, out Vector2 leftFoot, 28, 30, 32) ||
            !TryGetAveragePoint(playerAvatar, out Vector2 rightFoot, 27, 29, 31))
        {
            return CreateInvalidEvaluation(ExerciseType.JumpingJack);
        }

        float shoulderWidth = Mathf.Max(MinimumBodyWidth, Vector2.Distance(leftShoulder, rightShoulder));
        float shoulderMidY = Average(leftShoulder.y, rightShoulder.y);
        float armTargetY = shoulderMidY + (shoulderWidth * 0.90f);
        float leftArmProgress = Mathf.InverseLerp(leftShoulder.y, armTargetY, leftHand.y);
        float rightArmProgress = Mathf.InverseLerp(rightShoulder.y, armTargetY, rightHand.y);
        float armProgress = Average(leftArmProgress, rightArmProgress);
        float feetSpreadRatio = Vector2.Distance(leftFoot, rightFoot) / shoulderWidth;
        float legProgress = Mathf.InverseLerp(1.00f, 1.90f, feetSpreadRatio);

        float centerX = Average(leftShoulder.x, rightShoulder.x);
        if (TryGetPoint(playerAvatar, 24, out Vector2 leftHip) &&
            TryGetPoint(playerAvatar, 23, out Vector2 rightHip))
        {
            centerX = Average(leftHip.x, rightHip.x);
        }

        float leftLegOffset = Mathf.Abs(leftFoot.x - centerX);
        float rightLegOffset = Mathf.Abs(rightFoot.x - centerX);
        float armSymmetry = GetSymmetryScore(leftArmProgress, rightArmProgress, 0.35f);
        float legSymmetry = GetSymmetryScore(leftLegOffset, rightLegOffset, shoulderWidth * 0.75f);
        float symmetry = Average(armSymmetry, legSymmetry);
        float formScore = Mathf.Clamp01((armProgress * 0.55f) + (legProgress * 0.45f));
        float accuracy = Mathf.Clamp01((armProgress * 0.45f) + (legProgress * 0.35f) + (symmetry * 0.20f));
        bool isStartPose = armProgress <= 0.30f && feetSpreadRatio <= 1.45f;
        bool isTargetPose = armProgress >= 0.72f && legProgress >= 0.55f;
        float progress = Average(armProgress, legProgress);

        return new ExerciseEvaluation(
            ExerciseType.JumpingJack,
            true,
            progress,
            accuracy,
            isStartPose,
            isTargetPose,
            symmetry,
            formScore);
    }

    private bool TryGetTrackedPlayerAvatar(int targetPlayerSlot, out BodylinkPlayerAvatar playerAvatar)
    {
        playerAvatar = null;

        Bodylink bodylink = Bodylink.Instance;
        if (bodylink == null ||
            !bodylink.IsInitialized ||
            bodylink.bodylinkAvatar == null ||
            bodylink.bodylinkAvatar.players == null)
        {
            return false;
        }

        // BodylinkAvatar already keeps players[0] = left slot and players[1] = right slot.
        // stablePlayerIndex maps those slots back to raw Mediapipe indices and should only be
        // used when reading poseLandmarkerResult directly. Using it here swaps slot ownership.
        if (targetPlayerSlot < 0 || targetPlayerSlot >= bodylink.bodylinkAvatar.players.Length)
        {
            return false;
        }

        playerAvatar = bodylink.bodylinkAvatar.players[targetPlayerSlot];
        return playerAvatar != null;
    }

    private bool TryGetPoint(BodylinkPlayerAvatar playerAvatar, int landmarkIndex, out Vector2 point)
    {
        point = Vector2.zero;
        if (playerAvatar == null)
        {
            return false;
        }

        NormalizedLandmark landmark = useSmoothedPoints
            ? playerAvatar.body2DSmoothed[landmarkIndex]
            : playerAvatar.body2D[landmarkIndex];

        float visibility = Mathf.Max(
            landmark.visibility.GetValueOrDefault(),
            landmark.presence.GetValueOrDefault());

        if (visibility < minimumVisibility)
        {
            return false;
        }

        point = new Vector2(landmark.x, landmark.y);
        return true;
    }

    private bool TryGetAveragePoint(BodylinkPlayerAvatar playerAvatar, out Vector2 point, params int[] landmarkIndexes)
    {
        point = Vector2.zero;
        if (playerAvatar == null || landmarkIndexes == null || landmarkIndexes.Length == 0)
        {
            return false;
        }

        Vector2 accumulated = Vector2.zero;
        int visibleCount = 0;

        for (int i = 0; i < landmarkIndexes.Length; i++)
        {
            if (!TryGetPoint(playerAvatar, landmarkIndexes[i], out Vector2 currentPoint))
            {
                continue;
            }

            accumulated += currentPoint;
            visibleCount++;
        }

        if (visibleCount == 0)
        {
            return false;
        }

        point = accumulated / visibleCount;
        return true;
    }

    private ExerciseEvaluation CreateInvalidEvaluation(ExerciseType exerciseType)
    {
        return new ExerciseEvaluation(exerciseType, false, 0f, 0f, false, false, 0f, 0f);
    }

    private string GetUnavailableStatus()
    {
        if (Bodylink.Instance == null)
        {
            return "Waiting for Bodylink";
        }

        if (!Bodylink.Instance.IsInitialized)
        {
            return "Initializing tracker";
        }

        return "Waiting for pose";
    }

    private static string GetInactivePlayerStatus(int targetPlayerSlot)
    {
        return targetPlayerSlot == 0 ? "Waiting for player 1" : "Waiting for player 2";
    }

    private string GetStartPoseStatus()
    {
        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "Get into push-up start pose";
            case ExerciseType.JumpingJack:
                return "Stand with hands down to start";
            default:
                return "Stand tall to start squat";
        }
    }

    private string GetTargetPoseStatus(ExerciseEvaluation evaluation)
    {
        if (evaluation.Accuracy < mediumThreshold)
        {
            switch (exercise)
            {
                case ExerciseType.PushUp:
                    return "Fix push-up form";
                case ExerciseType.JumpingJack:
                    return "Fix jumping jack form";
                default:
                    return "Fix squat form";
            }
        }

        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "Lower your chest";
            case ExerciseType.JumpingJack:
                return "Open arms and legs more";
            default:
                return "Go a bit lower";
        }
    }

    private string GetReturnPoseStatus(ExerciseEvaluation evaluation)
    {
        if (evaluation.Accuracy < mediumThreshold)
        {
            return "Keep form as you reset";
        }

        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "Push back to start";
            case ExerciseType.JumpingJack:
                return "Return arms and legs";
            default:
                return "Stand up to reset";
        }
    }

    private string GetExerciseDisplayName()
    {
        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "Push-up";
            case ExerciseType.JumpingJack:
                return "Jumping jack";
            default:
                return "Squat";
        }
    }

    private string GetExerciseShortLabel()
    {
        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "push-up";
            case ExerciseType.JumpingJack:
                return "jumping jack";
            default:
                return "squat";
        }
    }

    private string GetExerciseCountLabel()
    {
        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "Push-ups";
            case ExerciseType.JumpingJack:
                return "Jumping jacks";
            default:
                return "Squats";
        }
    }

    private static float CalculateAngle(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ba = a - b;
        Vector2 bc = c - b;
        if (ba.sqrMagnitude <= MinimumSegmentLength || bc.sqrMagnitude <= MinimumSegmentLength)
        {
            return 180f;
        }

        return Vector2.Angle(ba, bc);
    }

    private static float GetSymmetryScore(float leftValue, float rightValue, float tolerance)
    {
        return 1f - Mathf.Clamp01(Mathf.Abs(leftValue - rightValue) / Mathf.Max(MinimumSegmentLength, tolerance));
    }

    private static float Average(float firstValue, float secondValue)
    {
        return (firstValue + secondValue) * 0.5f;
    }

    private static float GetAverage(IReadOnlyList<float> values)
    {
        if (values == null || values.Count == 0)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < values.Count; i++)
        {
            total += values[i];
        }

        return total / values.Count;
    }
}
