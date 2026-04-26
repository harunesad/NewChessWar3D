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
        JumpingJack = 2,
        Lunges = 3,
        Plank = 4,
        HighKnees = 5
    }

    public enum TrackingPhase
    {
        WaitingForStartPose = 0,
        MovingToTargetPose = 1,
        ReturningToStartPose = 2
    }

    private enum ExerciseLeadSide
    {
        Left = 0,
        Right = 1
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

    [Serializable]
    private sealed class SquatSettings
    {
        [Min(0f)] public float KneeProgressStandingAngle = 170f;
        [Min(0f)] public float KneeProgressBottomAngle = 90f;
        [Min(0f)] public float HipDepthStandingRatio = 1.00f;
        [Min(0f)] public float HipDepthBottomRatio = 0.40f;
        [Min(0f)] public float KneeSymmetryTolerance = 30f;
        [Min(0f)] public float StartKneeAngle = 160f;
        [Min(0f)] public float TargetKneeAngle = 105f;
        [Range(0f, 1f)] public float TargetHipDepthProgress = 0.55f;
    }

    [Serializable]
    private sealed class ExerciseRuntimeSettings
    {
        [Min(1)] public int TargetRepetitions = 10;
        [Min(1)] public int RepsPerSet = 10;
        [Range(0f, 1f)] public float MediumThreshold = 0.55f;
        [Range(0f, 1f)] public float HighThreshold = 0.75f;
        [Min(0.25f)] public float HoldDuration = 3f;
    }

    [Serializable]
    private sealed class PushUpSettings
    {
        [Min(0f)] public float ElbowProgressStandingAngle = 170f;
        [Min(0f)] public float ElbowProgressBottomAngle = 80f;
        [Min(0f)] public float BodyFormMinAngle = 135f;
        [Min(0f)] public float BodyFormMaxAngle = 170f;
        [Min(0f)] public float ElbowSymmetryTolerance = 35f;
        [Min(0f)] public float StartElbowAngle = 158f;
        [Min(0f)] public float TargetElbowAngle = 95f;
        [Range(0f, 1f)] public float MinimumFormScore = 0.30f;
    }

    [Serializable]
    private sealed class JumpingJackSettings
    {
        [Min(0f)] public float ArmTargetHeightMultiplier = 0.90f;
        [Min(0f)] public float LegSpreadStandingRatio = 1.00f;
        [Min(0f)] public float LegSpreadOpenRatio = 1.90f;
        [Range(0f, 1f)] public float StartArmProgressMax = 0.30f;
        [Min(0f)] public float StartFeetSpreadMax = 1.45f;
        [Range(0f, 1f)] public float TargetArmProgressMin = 0.72f;
        [Range(0f, 1f)] public float TargetLegProgressMin = 0.55f;
        [Min(0f)] public float ArmSymmetryTolerance = 0.35f;
        [Min(0f)] public float LegSymmetryToleranceWidthMultiplier = 0.75f;
    }

    [Serializable]
    private sealed class LungeSettings
    {
        [Range(0f, 1f)] public float VisibilityThreshold = 0.15f;
        [Min(0f)] public float FrontKneeProgressStandingAngle = 170f;
        [Min(0f)] public float FrontKneeProgressBottomAngle = 105f;
        [Min(0f)] public float StanceProgressStandingRatio = 0.20f;
        [Min(0f)] public float StanceProgressOpenRatio = 0.75f;
        [Min(0f)] public float BackLegBentAngle = 115f;
        [Min(0f)] public float BackLegStraightAngle = 170f;
        [Min(0f)] public float StartFrontKneeAngle = 150f;
        [Min(0f)] public float StartMaxStanceRatio = 0.32f;
        [Range(0f, 1f)] public float StartMinBackLegExtension = 0.40f;
        [Min(0f)] public float TargetFrontKneeAngle = 130f;
        [Min(0f)] public float TargetMinStanceRatio = 0.55f;
        [Range(0f, 1f)] public float TargetMinBackLegExtension = 0.40f;
        [Min(0f)] public float LevelToleranceScale = 0.35f;
    }

    [Serializable]
    private sealed class PlankSettings
    {
        [Range(0f, 1f)] public float VisibilityThreshold = 0.20f;
        [Min(0f)] public float TorsoAlignmentMinAngle = 132f;
        [Min(0f)] public float TorsoAlignmentMaxAngle = 175f;
        [Min(0f)] public float LegAlignmentMinAngle = 140f;
        [Min(0f)] public float LegAlignmentMaxAngle = 180f;
        [Min(0f)] public float SideLevelToleranceScale = 0.45f;
        [Min(0f)] public float AngleConsistencyTolerance = 35f;
        [Range(0f, 1f)] public float StartFormMax = 0.45f;
        [Range(0f, 1f)] public float TargetFormMin = 0.62f;
        [Range(0f, 1f)] public float TargetTorsoAlignmentMin = 0.60f;
        [Range(0f, 1f)] public float TargetLegAlignmentMin = 0.60f;
        [Range(0f, 1f)] public float TargetStabilityMin = 0.35f;
    }

    [Serializable]
    private sealed class HighKneesSettings
    {
        [Range(0f, 1f)] public float VisibilityThreshold = 0.20f;
        public float LiftStartOffset = -0.10f;
        public float LiftEndOffset = 0.45f;
        [Min(0f)] public float ActiveBendStandingAngle = 175f;
        [Min(0f)] public float ActiveBendRaisedAngle = 95f;
        [Min(0f)] public float SupportExtensionBentAngle = 100f;
        [Min(0f)] public float SupportExtensionStraightAngle = 175f;
        [Range(0f, 1f)] public float StartActiveLiftMax = 0.24f;
        [Range(0f, 1f)] public float StartSupportLiftMax = 0.50f;
        [Range(0f, 1f)] public float TargetActiveLiftMin = 0.38f;
        [Range(0f, 1f)] public float TargetActiveBendMin = 0.18f;
        [Range(0f, 1f)] public float TargetSupportLiftMax = 0.60f;
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
        public float CurrentHoldTime;
        public int RepSampleCount;
        public int RepetitionCount;
        public int CompletedSets;
        public ExerciseLeadSide ExpectedLungeSide = ExerciseLeadSide.Left;
        public ExerciseLeadSide ExpectedHighKneeSide = ExerciseLeadSide.Left;
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

    [SerializeField, HideInInspector] private int targetRepetitions = 10;
    [SerializeField, HideInInspector] private int repsPerSet = 10;
    [SerializeField, HideInInspector] private float mediumThreshold = 0.55f;
    [SerializeField, HideInInspector] private float highThreshold = 0.75f;
    [SerializeField, HideInInspector] private float plankHoldDuration = 3f;
    [SerializeField, HideInInspector] private bool runtimeSettingsMigrated;

    [Header("Tracking")]
    [SerializeField] private float progressLerpSpeed = 10f;
    [SerializeField] private bool evaluateEveryFrame = true;
    [SerializeField] private bool debugLogs;

    [Header("Exercise Settings")]
    [SerializeField] private ExerciseRuntimeSettings squatRuntimeSettings = new ExerciseRuntimeSettings();
    [SerializeField] private ExerciseRuntimeSettings pushUpRuntimeSettings = new ExerciseRuntimeSettings();
    [SerializeField] private ExerciseRuntimeSettings jumpingJackRuntimeSettings = new ExerciseRuntimeSettings();
    [SerializeField] private ExerciseRuntimeSettings lungeRuntimeSettings = new ExerciseRuntimeSettings();
    [SerializeField] private ExerciseRuntimeSettings plankRuntimeSettings = new ExerciseRuntimeSettings();
    [SerializeField] private ExerciseRuntimeSettings highKneesRuntimeSettings = new ExerciseRuntimeSettings();

    [SerializeField, HideInInspector] private SquatSettings squatSettings = new SquatSettings();
    [SerializeField, HideInInspector] private PushUpSettings pushUpSettings = new PushUpSettings();
    [SerializeField, HideInInspector] private JumpingJackSettings jumpingJackSettings = new JumpingJackSettings();
    [SerializeField, HideInInspector] private LungeSettings lungeSettings = new LungeSettings();
    [SerializeField, HideInInspector] private PlankSettings plankSettings = new PlankSettings();
    [SerializeField, HideInInspector] private HighKneesSettings highKneesSettings = new HighKneesSettings();

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
    public int TargetRepetitions => GetActiveRuntimeSettings().TargetRepetitions;
    public int RepsPerSet => GetActiveRuntimeSettings().RepsPerSet;
    public float LiveProgress => GetLiveProgress(playerSlot);
    public float WorkoutProgress => GetWorkoutProgress(playerSlot);
    public float LiveAccuracy => GetLiveAccuracy(playerSlot);
    public float AverageRepAccuracy => GetAverageRepAccuracy(playerSlot);
    public float LastRepAccuracy => GetLastRepAccuracy(playerSlot);
    public float ConsistencyScore => GetConsistencyScore(playerSlot);
    public float FinalScore => GetFinalScore(playerSlot);
    public float MediumThreshold => GetActiveRuntimeSettings().MediumThreshold;
    public float HighThreshold => GetActiveRuntimeSettings().HighThreshold;
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
        EnsureExerciseSettings();
        MigrateLegacyRuntimeSettings();
        ResetWorkout();
    }

    private void OnValidate()
    {
        EnsureExerciseSettings();
        MigrateLegacyRuntimeSettings();
        playerSlot = Mathf.Clamp(playerSlot, 0, MaxSupportedPlayers - 1);
        minimumVisibility = Mathf.Clamp01(minimumVisibility);
        progressLerpSpeed = Mathf.Max(0f, progressLerpSpeed);
        ClampRuntimeSettings();
        ClampExerciseSettings();
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

    private void ClampExerciseSettings()
    {
        squatSettings.KneeProgressStandingAngle = Mathf.Max(squatSettings.KneeProgressBottomAngle, squatSettings.KneeProgressStandingAngle);
        squatSettings.StartKneeAngle = Mathf.Max(squatSettings.TargetKneeAngle, squatSettings.StartKneeAngle);
        squatSettings.HipDepthStandingRatio = Mathf.Max(squatSettings.HipDepthBottomRatio, squatSettings.HipDepthStandingRatio);

        pushUpSettings.ElbowProgressStandingAngle = Mathf.Max(pushUpSettings.ElbowProgressBottomAngle, pushUpSettings.ElbowProgressStandingAngle);
        pushUpSettings.BodyFormMaxAngle = Mathf.Max(pushUpSettings.BodyFormMinAngle, pushUpSettings.BodyFormMaxAngle);
        pushUpSettings.StartElbowAngle = Mathf.Max(pushUpSettings.TargetElbowAngle, pushUpSettings.StartElbowAngle);

        jumpingJackSettings.LegSpreadOpenRatio = Mathf.Max(jumpingJackSettings.LegSpreadStandingRatio, jumpingJackSettings.LegSpreadOpenRatio);
        jumpingJackSettings.TargetArmProgressMin = Mathf.Max(jumpingJackSettings.StartArmProgressMax, jumpingJackSettings.TargetArmProgressMin);

        lungeSettings.FrontKneeProgressStandingAngle = Mathf.Max(lungeSettings.FrontKneeProgressBottomAngle, lungeSettings.FrontKneeProgressStandingAngle);
        lungeSettings.StanceProgressOpenRatio = Mathf.Max(lungeSettings.StanceProgressStandingRatio, lungeSettings.StanceProgressOpenRatio);
        lungeSettings.BackLegStraightAngle = Mathf.Max(lungeSettings.BackLegBentAngle, lungeSettings.BackLegStraightAngle);
        lungeSettings.StartFrontKneeAngle = Mathf.Max(lungeSettings.TargetFrontKneeAngle, lungeSettings.StartFrontKneeAngle);
        lungeSettings.TargetMinStanceRatio = Mathf.Max(lungeSettings.StartMaxStanceRatio, lungeSettings.TargetMinStanceRatio);

        plankSettings.TorsoAlignmentMaxAngle = Mathf.Max(plankSettings.TorsoAlignmentMinAngle, plankSettings.TorsoAlignmentMaxAngle);
        plankSettings.LegAlignmentMaxAngle = Mathf.Max(plankSettings.LegAlignmentMinAngle, plankSettings.LegAlignmentMaxAngle);
        plankSettings.TargetFormMin = Mathf.Max(plankSettings.StartFormMax, plankSettings.TargetFormMin);
        highKneesSettings.LiftEndOffset = Mathf.Max(highKneesSettings.LiftStartOffset + 0.01f, highKneesSettings.LiftEndOffset);
        highKneesSettings.ActiveBendStandingAngle = Mathf.Max(highKneesSettings.ActiveBendRaisedAngle, highKneesSettings.ActiveBendStandingAngle);
        highKneesSettings.SupportExtensionStraightAngle = Mathf.Max(highKneesSettings.SupportExtensionBentAngle, highKneesSettings.SupportExtensionStraightAngle);
        highKneesSettings.TargetActiveLiftMin = Mathf.Max(highKneesSettings.StartActiveLiftMax, highKneesSettings.TargetActiveLiftMin);
    }

    private void ClampRuntimeSettings()
    {
        ClampRuntimeSettings(squatRuntimeSettings);
        ClampRuntimeSettings(pushUpRuntimeSettings);
        ClampRuntimeSettings(jumpingJackRuntimeSettings);
        ClampRuntimeSettings(lungeRuntimeSettings);
        ClampRuntimeSettings(plankRuntimeSettings);
        ClampRuntimeSettings(highKneesRuntimeSettings);
    }

    private static void ClampRuntimeSettings(ExerciseRuntimeSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.TargetRepetitions = Mathf.Max(1, settings.TargetRepetitions);
        settings.RepsPerSet = Mathf.Max(1, settings.RepsPerSet);
        settings.MediumThreshold = Mathf.Clamp01(settings.MediumThreshold);
        settings.HighThreshold = Mathf.Max(settings.MediumThreshold, Mathf.Clamp01(settings.HighThreshold));
        settings.HoldDuration = Mathf.Max(0.25f, settings.HoldDuration);
    }

    private void EnsureExerciseSettings()
    {
        squatRuntimeSettings ??= new ExerciseRuntimeSettings();
        pushUpRuntimeSettings ??= new ExerciseRuntimeSettings();
        jumpingJackRuntimeSettings ??= new ExerciseRuntimeSettings();
        lungeRuntimeSettings ??= new ExerciseRuntimeSettings();
        plankRuntimeSettings ??= new ExerciseRuntimeSettings();
        highKneesRuntimeSettings ??= new ExerciseRuntimeSettings();

        squatSettings ??= new SquatSettings();
        pushUpSettings ??= new PushUpSettings();
        jumpingJackSettings ??= new JumpingJackSettings();
        lungeSettings ??= new LungeSettings();
        plankSettings ??= new PlankSettings();
        highKneesSettings ??= new HighKneesSettings();
    }

    private void MigrateLegacyRuntimeSettings()
    {
        if (runtimeSettingsMigrated)
        {
            return;
        }

        ExerciseRuntimeSettings[] runtimeSettings =
        {
            squatRuntimeSettings,
            pushUpRuntimeSettings,
            jumpingJackRuntimeSettings,
            lungeRuntimeSettings,
            plankRuntimeSettings,
            highKneesRuntimeSettings
        };

        for (int i = 0; i < runtimeSettings.Length; i++)
        {
            ExerciseRuntimeSettings settings = runtimeSettings[i];
            if (settings == null)
            {
                continue;
            }

            settings.TargetRepetitions = Mathf.Max(1, targetRepetitions);
            settings.RepsPerSet = Mathf.Max(1, repsPerSet);
            settings.MediumThreshold = Mathf.Clamp01(mediumThreshold);
            settings.HighThreshold = Mathf.Max(settings.MediumThreshold, Mathf.Clamp01(highThreshold));
        }

        plankRuntimeSettings.HoldDuration = Mathf.Max(0.25f, plankHoldDuration);
        runtimeSettingsMigrated = true;
    }

    private ExerciseRuntimeSettings GetActiveRuntimeSettings()
    {
        return GetRuntimeSettings(exercise);
    }

    private ExerciseRuntimeSettings GetRuntimeSettings(ExerciseType exerciseType)
    {
        return exerciseType switch
        {
            ExerciseType.Squat => squatRuntimeSettings,
            ExerciseType.PushUp => pushUpRuntimeSettings,
            ExerciseType.JumpingJack => jumpingJackRuntimeSettings,
            ExerciseType.Lunges => lungeRuntimeSettings,
            ExerciseType.Plank => plankRuntimeSettings,
            ExerciseType.HighKnees => highKneesRuntimeSettings,
            _ => squatRuntimeSettings
        };
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
            playerState.CurrentHoldTime = 0f;
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
    public bool IsPlayerWorkoutComplete(int targetPlayerSlot) => GetRepetitionCount(targetPlayerSlot) >= GetActiveRuntimeSettings().TargetRepetitions;

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
        playerState.CurrentHoldTime = 0f;
        playerState.RepetitionCount = 0;
        playerState.CompletedSets = 0;
        playerState.ExpectedLungeSide = ExerciseLeadSide.Left;
        playerState.ExpectedHighKneeSide = ExerciseLeadSide.Left;
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
        if (!TryEvaluateCurrentExercise(playerState, out ExerciseEvaluation evaluation))
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
            if (exercise == ExerciseType.Plank)
            {
                ResetPlankHold(playerState);
            }

            playerState.CurrentRepCycleProgress = playerState.TrackingPhase == TrackingPhase.ReturningToStartPose ? 0.5f : 0f;
            FadeLivePose(playerState, "Move fully into frame");
            UpdateWorkoutProgress(playerState);
            return;
        }

        if (exercise == ExerciseType.Plank)
        {
            EvaluatePlankHold(playerState, evaluation);
            UpdateWorkoutProgress(playerState);
            return;
        }

        playerState.CurrentProgress = evaluation.Progress;
        switch (playerState.TrackingPhase)
        {
            case TrackingPhase.WaitingForStartPose:
                playerState.CurrentRepCycleProgress = 0f;
                playerState.StatusMessage = GetStartPoseStatus(playerState);

                if (evaluation.IsStartPose)
                {
                    playerState.TrackingPhase = TrackingPhase.MovingToTargetPose;
                    ResetRepCapture(playerState);
                    playerState.StatusMessage = GetTargetPoseStatus(playerState, evaluation);
                }
                break;

            case TrackingPhase.MovingToTargetPose:
                if (evaluation.Progress <= 0.05f && evaluation.IsStartPose)
                {
                    playerState.CurrentRepCycleProgress = 0f;
                    playerState.StatusMessage = GetStartPoseStatus(playerState);
                    ResetRepCapture(playerState);
                    break;
                }

                CaptureRepSample(playerState, evaluation);
                playerState.CurrentRepCycleProgress = Mathf.Clamp01(evaluation.Progress * 0.5f);
                playerState.StatusMessage = GetTargetPoseStatus(playerState, evaluation);

                if (evaluation.IsTargetPose)
                {
                    playerState.TrackingPhase = TrackingPhase.ReturningToStartPose;
                    playerState.StatusMessage = GetReturnPoseStatus(playerState, evaluation);
                }
                break;

            case TrackingPhase.ReturningToStartPose:
                CaptureRepSample(playerState, evaluation);
                playerState.CurrentRepCycleProgress = Mathf.Clamp01(0.5f + ((1f - evaluation.Progress) * 0.5f));
                playerState.StatusMessage = GetReturnPoseStatus(playerState, evaluation);

                if (evaluation.IsStartPose)
                {
                    CompleteRepetition(playerState);
                    playerState.TrackingPhase = IsPlayerWorkoutComplete(playerState.PlayerSlot)
                        ? TrackingPhase.WaitingForStartPose
                        : TrackingPhase.MovingToTargetPose;
                    playerState.CurrentRepCycleProgress = 0f;
                    playerState.StatusMessage = IsPlayerWorkoutComplete(playerState.PlayerSlot)
                        ? $"{GetExerciseDisplayName()} workout complete"
                        : GetReadyForNextStatus(playerState);
                    ResetRepCapture(playerState);
                }
                break;
        }

        UpdateWorkoutProgress(playerState);
    }

    private void CompleteRepetition(PlayerTrackingState playerState)
    {
        playerState.RepetitionCount++;

        if (exercise == ExerciseType.Lunges)
        {
            playerState.ExpectedLungeSide = GetOppositeLungeSide(playerState.ExpectedLungeSide);
        }
        else if (exercise == ExerciseType.HighKnees)
        {
            playerState.ExpectedHighKneeSide = GetOppositeLungeSide(playerState.ExpectedHighKneeSide);
        }

        float averageCycleAccuracy = playerState.RepSampleCount > 0
            ? playerState.RepAccumulatedAccuracy / playerState.RepSampleCount
            : playerState.LastEvaluation.Accuracy;

        playerState.LastRepAccuracy = Mathf.Clamp01((playerState.RepPeakAccuracy * 0.65f) + (averageCycleAccuracy * 0.35f));
        playerState.RepetitionScores.Add(playerState.LastRepAccuracy);
        playerState.CurrentSetScores.Add(playerState.LastRepAccuracy);

        if (playerState.CurrentSetScores.Count >= GetActiveRuntimeSettings().RepsPerSet)
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

    private void EvaluatePlankHold(PlayerTrackingState playerState, ExerciseEvaluation evaluation)
    {
        float holdDuration = Mathf.Max(0.25f, plankRuntimeSettings.HoldDuration);

        if (debugLogs)
        {
            Debug.Log(
                $"[Bodylink] Plank hold | player={playerState.PlayerSlot + 1} | isPlank={evaluation.IsTargetPose} | accuracy={(evaluation.Accuracy * 100f):F0}% | hold={playerState.CurrentHoldTime:F1}/{holdDuration:F1}s");
        }

        if (!evaluation.IsTargetPose)
        {
            ResetPlankHold(playerState);
            playerState.StatusMessage = evaluation.Accuracy < GetActiveRuntimeSettings().MediumThreshold
                ? GetTargetPoseStatus(playerState, evaluation)
                : GetStartPoseStatus(playerState);
            return;
        }

        CaptureRepSample(playerState, evaluation);
        playerState.CurrentHoldTime += Time.deltaTime;
        float holdProgress = Mathf.Clamp01(playerState.CurrentHoldTime / holdDuration);
        float remainingSeconds = Mathf.Max(0f, holdDuration - playerState.CurrentHoldTime);

        playerState.CurrentProgress = holdProgress;
        playerState.CurrentRepCycleProgress = holdProgress;
        playerState.StatusMessage = remainingSeconds > 0f
            ? $"Hold plank {remainingSeconds:F1}s"
            : "Plank hold complete";

        while (playerState.CurrentHoldTime >= holdDuration && !IsPlayerWorkoutComplete(playerState.PlayerSlot))
        {
            playerState.CurrentHoldTime -= holdDuration;
            CompleteRepetition(playerState);
            ResetRepCapture(playerState);
        }

        if (IsPlayerWorkoutComplete(playerState.PlayerSlot))
        {
            playerState.CurrentHoldTime = 0f;
            playerState.CurrentProgress = 0f;
            playerState.CurrentRepCycleProgress = 0f;
            playerState.StatusMessage = $"{GetExerciseDisplayName()} workout complete";
            return;
        }

        float nextProgress = Mathf.Clamp01(playerState.CurrentHoldTime / holdDuration);
        playerState.CurrentProgress = nextProgress;
        playerState.CurrentRepCycleProgress = nextProgress;
    }

    private void ResetRepCapture(PlayerTrackingState playerState)
    {
        playerState.RepPeakAccuracy = 0f;
        playerState.RepAccumulatedAccuracy = 0f;
        playerState.RepSampleCount = 0;
    }

    private void ResetPlankHold(PlayerTrackingState playerState)
    {
        playerState.CurrentHoldTime = 0f;
        playerState.CurrentProgress = 0f;
        playerState.CurrentRepCycleProgress = 0f;
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

        float completion = Mathf.Clamp01((float)playerState.RepetitionCount / GetActiveRuntimeSettings().TargetRepetitions);
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
        playerState.WorkoutProgress = Mathf.Clamp01((playerState.RepetitionCount + playerState.CurrentRepCycleProgress) / GetActiveRuntimeSettings().TargetRepetitions);
    }

    private void RaiseMetricsUpdated()
    {
        MetricsUpdated?.Invoke(this);
    }

    private PlayerTrackingState GetPlayerState(int targetPlayerSlot)
    {
        return playerStates[Mathf.Clamp(targetPlayerSlot, 0, MaxSupportedPlayers - 1)];
    }

    private bool TryEvaluateCurrentExercise(PlayerTrackingState playerState, out ExerciseEvaluation evaluation)
    {
        evaluation = CreateInvalidEvaluation(exercise);
        int targetPlayerSlot = playerState.PlayerSlot;

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
            case ExerciseType.Lunges:
                evaluation = EvaluateLunges(playerAvatar, playerState.ExpectedLungeSide);
                return true;
            case ExerciseType.Plank:
                evaluation = EvaluatePlank(playerAvatar);
                return true;
            case ExerciseType.HighKnees:
                evaluation = EvaluateHighKnees(playerAvatar, playerState.ExpectedHighKneeSide);
                return true;
            default:
                return false;
        }
    }

    private ExerciseEvaluation EvaluateSquat(BodylinkPlayerAvatar playerAvatar)
    {
        SquatSettings settings = squatSettings;

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

        float kneeProgress = Mathf.InverseLerp(settings.KneeProgressStandingAngle, settings.KneeProgressBottomAngle, averageKneeAngle);
        float hipDepthProgress = Mathf.InverseLerp(settings.HipDepthStandingRatio, settings.HipDepthBottomRatio, hipToKneeRatio);
        float symmetry = GetSymmetryScore(leftKneeAngle, rightKneeAngle, settings.KneeSymmetryTolerance);
        float formScore = Mathf.Clamp01((kneeProgress * 0.55f) + (hipDepthProgress * 0.45f));
        float accuracy = Mathf.Clamp01((kneeProgress * 0.65f) + (hipDepthProgress * 0.20f) + (symmetry * 0.15f));
        bool isStartPose = averageKneeAngle >= settings.StartKneeAngle;
        bool isTargetPose = averageKneeAngle <= settings.TargetKneeAngle && hipDepthProgress >= settings.TargetHipDepthProgress;
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
        PushUpSettings settings = pushUpSettings;

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
        float bendProgress = Mathf.InverseLerp(settings.ElbowProgressStandingAngle, settings.ElbowProgressBottomAngle, averageElbowAngle);
        float symmetry = GetSymmetryScore(leftElbowAngle, rightElbowAngle, settings.ElbowSymmetryTolerance);

        float formScore = 0.75f;
        float formAccumulator = 0f;
        int formSampleCount = 0;

        if (TryGetPoint(playerAvatar, 24, out Vector2 leftHip) &&
            TryGetPoint(playerAvatar, 26, out Vector2 leftKnee))
        {
            formAccumulator += Mathf.InverseLerp(settings.BodyFormMinAngle, settings.BodyFormMaxAngle, CalculateAngle(leftShoulder, leftHip, leftKnee));
            formSampleCount++;
        }

        if (TryGetPoint(playerAvatar, 23, out Vector2 rightHip) &&
            TryGetPoint(playerAvatar, 25, out Vector2 rightKnee))
        {
            formAccumulator += Mathf.InverseLerp(settings.BodyFormMinAngle, settings.BodyFormMaxAngle, CalculateAngle(rightShoulder, rightHip, rightKnee));
            formSampleCount++;
        }

        if (formSampleCount > 0)
        {
            formScore = formAccumulator / formSampleCount;
        }

        float accuracy = Mathf.Clamp01((bendProgress * 0.70f) + (symmetry * 0.20f) + (formScore * 0.10f));
        bool isStartPose = averageElbowAngle >= settings.StartElbowAngle && formScore >= settings.MinimumFormScore;
        bool isTargetPose = averageElbowAngle <= settings.TargetElbowAngle && formScore >= settings.MinimumFormScore;

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
        JumpingJackSettings settings = jumpingJackSettings;

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
        float armTargetY = shoulderMidY + (shoulderWidth * settings.ArmTargetHeightMultiplier);
        float leftArmProgress = Mathf.InverseLerp(leftShoulder.y, armTargetY, leftHand.y);
        float rightArmProgress = Mathf.InverseLerp(rightShoulder.y, armTargetY, rightHand.y);
        float armProgress = Average(leftArmProgress, rightArmProgress);
        float feetSpreadRatio = Vector2.Distance(leftFoot, rightFoot) / shoulderWidth;
        float legProgress = Mathf.InverseLerp(settings.LegSpreadStandingRatio, settings.LegSpreadOpenRatio, feetSpreadRatio);

        float centerX = Average(leftShoulder.x, rightShoulder.x);
        if (TryGetPoint(playerAvatar, 24, out Vector2 leftHip) &&
            TryGetPoint(playerAvatar, 23, out Vector2 rightHip))
        {
            centerX = Average(leftHip.x, rightHip.x);
        }

        float leftLegOffset = Mathf.Abs(leftFoot.x - centerX);
        float rightLegOffset = Mathf.Abs(rightFoot.x - centerX);
        float armSymmetry = GetSymmetryScore(leftArmProgress, rightArmProgress, settings.ArmSymmetryTolerance);
        float legSymmetry = GetSymmetryScore(leftLegOffset, rightLegOffset, shoulderWidth * settings.LegSymmetryToleranceWidthMultiplier);
        float symmetry = Average(armSymmetry, legSymmetry);
        float formScore = Mathf.Clamp01((armProgress * 0.55f) + (legProgress * 0.45f));
        float accuracy = Mathf.Clamp01((armProgress * 0.45f) + (legProgress * 0.35f) + (symmetry * 0.20f));
        bool isStartPose = armProgress <= settings.StartArmProgressMax && feetSpreadRatio <= settings.StartFeetSpreadMax;
        bool isTargetPose = armProgress >= settings.TargetArmProgressMin && legProgress >= settings.TargetLegProgressMin;
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

    private ExerciseEvaluation EvaluateLunges(BodylinkPlayerAvatar playerAvatar, ExerciseLeadSide expectedSide)
    {
        LungeSettings settings = lungeSettings;
        float lungeVisibility = Mathf.Min(minimumVisibility, settings.VisibilityThreshold);

        if (!TryGetPoint3D(playerAvatar, 12, out Vector3 leftShoulder, lungeVisibility) ||
            !TryGetPoint3D(playerAvatar, 11, out Vector3 rightShoulder, lungeVisibility) ||
            !TryGetPoint3D(playerAvatar, 24, out Vector3 leftHip, lungeVisibility) ||
            !TryGetPoint3D(playerAvatar, 23, out Vector3 rightHip, lungeVisibility))
        {
            return CreateInvalidEvaluation(ExerciseType.Lunges);
        }

        Vector3 leftKnee = Vector3.zero;
        Vector3 leftAnkle = Vector3.zero;
        Vector3 rightKnee = Vector3.zero;
        Vector3 rightAnkle = Vector3.zero;

        bool hasLeftLeg =
            TryGetPoint3D(playerAvatar, 26, out leftKnee, lungeVisibility) &&
            TryGetPoint3D(playerAvatar, 28, out leftAnkle, lungeVisibility);
        bool hasRightLeg =
            TryGetPoint3D(playerAvatar, 25, out rightKnee, lungeVisibility) &&
            TryGetPoint3D(playerAvatar, 27, out rightAnkle, lungeVisibility);

        bool expectLeft = expectedSide == ExerciseLeadSide.Left;
        bool hasFrontLeg = expectLeft ? hasLeftLeg : hasRightLeg;
        bool hasBackLeg = expectLeft ? hasRightLeg : hasLeftLeg;

        if (!hasFrontLeg)
        {
            return CreateInvalidEvaluation(ExerciseType.Lunges);
        }

        Vector3 frontHip = expectLeft ? leftHip : rightHip;
        Vector3 frontKnee = expectLeft ? leftKnee : rightKnee;
        Vector3 frontAnkle = expectLeft ? leftAnkle : rightAnkle;
        Vector3 backHip = expectLeft ? rightHip : leftHip;
        Vector3 backKnee = expectLeft ? rightKnee : leftKnee;
        Vector3 backAnkle = expectLeft ? rightAnkle : leftAnkle;

        float frontKneeAngle = expectLeft
            ? CalculateAngle(leftHip, leftKnee, leftAnkle)
            : CalculateAngle(rightHip, rightKnee, rightAnkle);
        float backKneeAngle = hasBackLeg
            ? (expectLeft
                ? CalculateAngle(rightHip, rightKnee, rightAnkle)
                : CalculateAngle(leftHip, leftKnee, leftAnkle))
            : 180f;

        float frontLegLength = Mathf.Max(
            MinimumSegmentLength,
            Vector3.Distance(frontHip, frontKnee) + Vector3.Distance(frontKnee, frontAnkle));
        float backLegLength = hasBackLeg
            ? Mathf.Max(
                MinimumSegmentLength,
                Vector3.Distance(backHip, backKnee) + Vector3.Distance(backKnee, backAnkle))
            : frontLegLength;
        float bodyScale = Mathf.Max(MinimumSegmentLength, Average(frontLegLength, backLegLength));

        // ✅ Stronger progress
        Vector2 frontGround = new Vector2(frontAnkle.x, frontAnkle.z);
        Vector2 backGround = hasBackLeg
            ? new Vector2(backAnkle.x, backAnkle.z)
            : frontGround;
        float stanceDistanceRatio = Vector2.Distance(frontGround, backGround) / bodyScale;
        float kneeProgress = Mathf.InverseLerp(settings.FrontKneeProgressStandingAngle, settings.FrontKneeProgressBottomAngle, frontKneeAngle);
        float stanceProgress = Mathf.InverseLerp(settings.StanceProgressStandingRatio, settings.StanceProgressOpenRatio, stanceDistanceRatio);

        float progress = Mathf.Clamp01((kneeProgress * 0.55f) + (stanceProgress * 0.45f));

        // ✅ FIXED thresholds (clear separation)
        float backLegExtension = hasBackLeg ? Mathf.InverseLerp(settings.BackLegBentAngle, settings.BackLegStraightAngle, backKneeAngle) : 1f;
        bool isStartPose = frontKneeAngle >= settings.StartFrontKneeAngle &&
            stanceDistanceRatio <= settings.StartMaxStanceRatio &&
            backLegExtension >= settings.StartMinBackLegExtension;
        bool isTargetPose = frontKneeAngle <= settings.TargetFrontKneeAngle &&
            stanceDistanceRatio >= settings.TargetMinStanceRatio &&
            backLegExtension >= settings.TargetMinBackLegExtension;

        float levelTolerance = Mathf.Max(MinimumSegmentLength, bodyScale * settings.LevelToleranceScale);
        float hipLevel = 1f - Mathf.Clamp01(Mathf.Abs(leftHip.y - rightHip.y) / levelTolerance);
        float shoulderLevel = 1f - Mathf.Clamp01(Mathf.Abs(leftShoulder.y - rightShoulder.y) / levelTolerance);
        float accuracy = Mathf.Clamp01((progress * 0.60f) + (backLegExtension * 0.20f) + (hipLevel * 0.10f) + (shoulderLevel * 0.10f));
        float symmetry = Mathf.Clamp01((backLegExtension * 0.4f) + (hipLevel * 0.4f) + (shoulderLevel * 0.2f));

        if (debugLogs)
        {
            Debug.Log(
                $"[Bodylink] Lunges {GetLungeSideLabel(expectedSide)} | frontKnee={frontKneeAngle:F1} | backKnee={backKneeAngle:F1} | stance={stanceDistanceRatio:F2} | backExt={backLegExtension:F2} | start={isStartPose} | target={isTargetPose}");
        }

        return new ExerciseEvaluation(
            ExerciseType.Lunges,
            true,
            progress,
            accuracy,
            isStartPose,
            isTargetPose,
            symmetry,
            progress);
    }

    private ExerciseEvaluation EvaluatePlank(BodylinkPlayerAvatar playerAvatar)
    {
        PlankSettings settings = plankSettings;
        float plankVisibility = Mathf.Min(minimumVisibility, settings.VisibilityThreshold);

        if (!TryGetPoint(playerAvatar, 12, out Vector2 leftShoulder, plankVisibility) ||
            !TryGetPoint(playerAvatar, 11, out Vector2 rightShoulder, plankVisibility) ||
            !TryGetPoint(playerAvatar, 24, out Vector2 leftHip, plankVisibility) ||
            !TryGetPoint(playerAvatar, 23, out Vector2 rightHip, plankVisibility) ||
            !TryGetPoint(playerAvatar, 26, out Vector2 leftKnee, plankVisibility) ||
            !TryGetPoint(playerAvatar, 25, out Vector2 rightKnee, plankVisibility) ||
            !TryGetPoint(playerAvatar, 28, out Vector2 leftAnkle, plankVisibility) ||
            !TryGetPoint(playerAvatar, 27, out Vector2 rightAnkle, plankVisibility))
        {
            return CreateInvalidEvaluation(ExerciseType.Plank);
        }

        float leftTorsoAngle = CalculateAngle(leftShoulder, leftHip, leftKnee);
        float rightTorsoAngle = CalculateAngle(rightShoulder, rightHip, rightKnee);
        float leftLegAngle = CalculateAngle(leftHip, leftKnee, leftAnkle);
        float rightLegAngle = CalculateAngle(rightHip, rightKnee, rightAnkle);

        float torsoAlignment = Average(
            Mathf.InverseLerp(settings.TorsoAlignmentMinAngle, settings.TorsoAlignmentMaxAngle, leftTorsoAngle),
            Mathf.InverseLerp(settings.TorsoAlignmentMinAngle, settings.TorsoAlignmentMaxAngle, rightTorsoAngle));

        float legAlignment = Average(
            Mathf.InverseLerp(settings.LegAlignmentMinAngle, settings.LegAlignmentMaxAngle, leftLegAngle),
            Mathf.InverseLerp(settings.LegAlignmentMinAngle, settings.LegAlignmentMaxAngle, rightLegAngle));

        float upperBodyScale = Average(
            Vector2.Distance(leftShoulder, leftHip),
            Vector2.Distance(rightShoulder, rightHip));
        float lowerBodyScale = Average(
            Vector2.Distance(leftHip, leftKnee),
            Vector2.Distance(rightHip, rightKnee));
        float bodyScale = Mathf.Max(MinimumSegmentLength, Average(upperBodyScale, lowerBodyScale));

        float shoulderLevel = 1f - Mathf.Clamp01(
            Mathf.Abs(leftShoulder.y - rightShoulder.y) /
            Mathf.Max(MinimumSegmentLength, bodyScale * settings.SideLevelToleranceScale));
        float hipLevel = 1f - Mathf.Clamp01(
            Mathf.Abs(leftHip.y - rightHip.y) /
            Mathf.Max(MinimumSegmentLength, bodyScale * settings.SideLevelToleranceScale));
        float torsoConsistency = 1f - Mathf.Clamp01(Mathf.Abs(leftTorsoAngle - rightTorsoAngle) / settings.AngleConsistencyTolerance);
        float legConsistency = 1f - Mathf.Clamp01(Mathf.Abs(leftLegAngle - rightLegAngle) / settings.AngleConsistencyTolerance);

        float sideLevelStability = Average(shoulderLevel, hipLevel);
        float angleConsistency = Average(torsoConsistency, legConsistency);
        float stability = Average(sideLevelStability, angleConsistency);
        float plankForm = Mathf.Clamp01((torsoAlignment * 0.55f) + (legAlignment * 0.30f) + (stability * 0.15f));
        float accuracy = Mathf.Clamp01((plankForm * 0.75f) + (stability * 0.25f));
        bool isStartPose = plankForm <= settings.StartFormMax;
        bool isTargetPose = plankForm >= settings.TargetFormMin &&
            torsoAlignment >= settings.TargetTorsoAlignmentMin &&
            legAlignment >= settings.TargetLegAlignmentMin &&
            stability >= settings.TargetStabilityMin;
        float progress = plankForm;
        float symmetry = stability;

        if (debugLogs)
        {
            Debug.Log(
                $"[Bodylink] Plank detect | isPlank={isTargetPose} | accuracy={(accuracy * 100f):F0}% | form={plankForm:F2} | torso={torsoAlignment:F2} | legs={legAlignment:F2} | stability={stability:F2}");
        }

        return new ExerciseEvaluation(
            ExerciseType.Plank,
            true,
            progress,
            accuracy,
            isStartPose,
            isTargetPose,
            symmetry,
            plankForm);
    }

    private ExerciseEvaluation EvaluateHighKnees(BodylinkPlayerAvatar playerAvatar, ExerciseLeadSide expectedSide)
    {
        HighKneesSettings settings = highKneesSettings;
        float highKneeVisibility = Mathf.Min(minimumVisibility, settings.VisibilityThreshold);

        if (!TryGetPoint(playerAvatar, 12, out Vector2 leftShoulder, highKneeVisibility) ||
            !TryGetPoint(playerAvatar, 11, out Vector2 rightShoulder, highKneeVisibility) ||
            !TryGetPoint(playerAvatar, 24, out Vector2 leftHip, highKneeVisibility) ||
            !TryGetPoint(playerAvatar, 23, out Vector2 rightHip, highKneeVisibility) ||
            !TryGetPoint(playerAvatar, 26, out Vector2 leftKnee, highKneeVisibility) ||
            !TryGetPoint(playerAvatar, 25, out Vector2 rightKnee, highKneeVisibility) ||
            !TryGetPoint(playerAvatar, 28, out Vector2 leftAnkle, highKneeVisibility) ||
            !TryGetPoint(playerAvatar, 27, out Vector2 rightAnkle, highKneeVisibility))
        {
            return CreateInvalidEvaluation(ExerciseType.HighKnees);
        }

        float shoulderMidY = Average(leftShoulder.y, rightShoulder.y);
        float hipMidY = Average(leftHip.y, rightHip.y);
        float torsoHeight = Mathf.Max(MinimumSegmentLength, shoulderMidY - hipMidY);

        float liftStart = hipMidY + (torsoHeight * settings.LiftStartOffset);
        float liftEnd = hipMidY + (torsoHeight * settings.LiftEndOffset);
        float leftKneeLift = Mathf.InverseLerp(liftStart, liftEnd, leftKnee.y);
        float rightKneeLift = Mathf.InverseLerp(liftStart, liftEnd, rightKnee.y);
        float leftKneeAngle = CalculateAngle(leftHip, leftKnee, leftAnkle);
        float rightKneeAngle = CalculateAngle(rightHip, rightKnee, rightAnkle);

        bool expectLeft = expectedSide == ExerciseLeadSide.Left;
        float activeLift = expectLeft ? leftKneeLift : rightKneeLift;
        float supportLift = expectLeft ? rightKneeLift : leftKneeLift;
        float activeKneeAngle = expectLeft ? leftKneeAngle : rightKneeAngle;
        float supportKneeAngle = expectLeft ? rightKneeAngle : leftKneeAngle;

        float shoulderWidth = Mathf.Max(MinimumBodyWidth, Vector2.Distance(leftShoulder, rightShoulder));
        float torsoCenterOffset = Mathf.Abs(Average(leftShoulder.x, rightShoulder.x) - Average(leftHip.x, rightHip.x));
        float uprightness = 1f - Mathf.Clamp01(torsoCenterOffset / Mathf.Max(MinimumSegmentLength, shoulderWidth));
        float activeBend = Mathf.InverseLerp(settings.ActiveBendStandingAngle, settings.ActiveBendRaisedAngle, activeKneeAngle);
        float supportExtension = Mathf.InverseLerp(settings.SupportExtensionBentAngle, settings.SupportExtensionStraightAngle, supportKneeAngle);
        float separation = Mathf.Clamp01(activeLift - (supportLift * 0.35f));

        float formScore = Mathf.Clamp01((activeLift * 0.50f) + (uprightness * 0.20f) + (activeBend * 0.20f) + (supportExtension * 0.10f));
        float accuracy = Mathf.Clamp01((activeLift * 0.45f) + (uprightness * 0.20f) + (activeBend * 0.20f) + (supportExtension * 0.15f));
        bool isStartPose = activeLift <= settings.StartActiveLiftMax && supportLift <= settings.StartSupportLiftMax;
        bool isTargetPose = activeLift >= settings.TargetActiveLiftMin &&
            activeBend >= settings.TargetActiveBendMin &&
            supportLift <= settings.TargetSupportLiftMax;
        float progress = Mathf.Clamp01((activeLift * 0.65f) + (separation * 0.20f) + (activeBend * 0.15f));
        float symmetry = Mathf.Clamp01((supportExtension * 0.5f) + (uprightness * 0.5f));

        if (debugLogs)
        {
            Debug.Log(
                $"[Bodylink] High knees {GetLungeSideLabel(expectedSide)} | lift={activeLift:F2} | support={supportLift:F2} | bend={activeBend:F2} | start={isStartPose} | target={isTargetPose}");
        }

        return new ExerciseEvaluation(
            ExerciseType.HighKnees,
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
        return TryGetPoint(playerAvatar, landmarkIndex, out point, minimumVisibility);
    }

    private bool TryGetPoint3D(BodylinkPlayerAvatar playerAvatar, int landmarkIndex, out Vector3 point, float visibilityThreshold)
    {
        point = Vector3.zero;
        if (playerAvatar == null)
        {
            return false;
        }

        Landmark landmark = useSmoothedPoints
            ? playerAvatar.body3DSmoothed[landmarkIndex]
            : playerAvatar.body3D[landmarkIndex];

        float visibility = landmark.visibility.HasValue || landmark.presence.HasValue
            ? Mathf.Max(
                landmark.visibility.GetValueOrDefault(),
                landmark.presence.GetValueOrDefault())
            : 1f;

        if (visibility < Mathf.Clamp01(visibilityThreshold))
        {
            return false;
        }

        point = new Vector3(landmark.x, landmark.y, landmark.z);
        return true;
    }

    private bool TryGetPoint(BodylinkPlayerAvatar playerAvatar, int landmarkIndex, out Vector2 point, float visibilityThreshold)
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

        if (visibility < Mathf.Clamp01(visibilityThreshold))
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

    private string GetStartPoseStatus(PlayerTrackingState playerState)
    {
        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "Get into push-up start pose";
            case ExerciseType.JumpingJack:
                return "Stand with hands down to start";
            case ExerciseType.Lunges:
                return $"Stand tall to start {GetLungeSideLabel(playerState.ExpectedLungeSide)} lunge";
            case ExerciseType.Plank:
                return "Get into plank start pose";
            case ExerciseType.HighKnees:
                return $"Stand ready for {GetLungeSideLabel(playerState.ExpectedHighKneeSide)} high knee";
            default:
                return "Stand tall to start squat";
        }
    }

    private string GetTargetPoseStatus(PlayerTrackingState playerState, ExerciseEvaluation evaluation)
    {
        if (evaluation.Accuracy < GetActiveRuntimeSettings().MediumThreshold)
        {
            switch (exercise)
            {
                case ExerciseType.PushUp:
                    return "Fix push-up form";
                case ExerciseType.JumpingJack:
                    return "Fix jumping jack form";
                case ExerciseType.Lunges:
                    return $"Fix {GetLungeSideLabel(playerState.ExpectedLungeSide)} lunge form";
                case ExerciseType.Plank:
                    return "Fix plank form";
                case ExerciseType.HighKnees:
                    return $"Fix {GetLungeSideLabel(playerState.ExpectedHighKneeSide)} high knee form";
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
            case ExerciseType.Lunges:
                return $"Step into {GetLungeSideLabel(playerState.ExpectedLungeSide)} lunge";
            case ExerciseType.Plank:
                return "Hold plank";
            case ExerciseType.HighKnees:
                return $"Drive {GetLungeSideLabel(playerState.ExpectedHighKneeSide)} knee higher";
            default:
                return "Go a bit lower";
        }
    }

    private string GetReturnPoseStatus(PlayerTrackingState playerState, ExerciseEvaluation evaluation)
    {
        if (evaluation.Accuracy < GetActiveRuntimeSettings().MediumThreshold)
        {
            switch (exercise)
            {
                case ExerciseType.PushUp:
                    return "Keep push-up form as you reset";
                case ExerciseType.JumpingJack:
                    return "Keep jumping jack form as you reset";
                case ExerciseType.Lunges:
                    return $"Keep {GetLungeSideLabel(playerState.ExpectedLungeSide)} lunge balance as you reset";
                case ExerciseType.Plank:
                    return "Keep plank form as you reset";
                case ExerciseType.HighKnees:
                    return $"Keep {GetLungeSideLabel(playerState.ExpectedHighKneeSide)} high knee form as you reset";
                default:
                    return "Keep form as you reset";
            }
        }

        switch (exercise)
        {
            case ExerciseType.PushUp:
                return "Push back to start";
            case ExerciseType.JumpingJack:
                return "Return arms and legs";
            case ExerciseType.Lunges:
                return $"Return from {GetLungeSideLabel(playerState.ExpectedLungeSide)} lunge";
            case ExerciseType.Plank:
                return "Release plank";
            case ExerciseType.HighKnees:
                return $"Lower {GetLungeSideLabel(playerState.ExpectedHighKneeSide)} knee back down";
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
            case ExerciseType.Lunges:
                return "Lunges";
            case ExerciseType.Plank:
                return "Plank";
            case ExerciseType.HighKnees:
                return "High knees";
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
            case ExerciseType.Lunges:
                return "lunges";
            case ExerciseType.Plank:
                return "plank";
            case ExerciseType.HighKnees:
                return "high knees";
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
            case ExerciseType.Lunges:
                return "Lunges";
            case ExerciseType.Plank:
                return "Plank holds";
            case ExerciseType.HighKnees:
                return "High knees";
            default:
                return "Squats";
        }
    }

    private string GetReadyForNextStatus(PlayerTrackingState playerState)
    {
        if (exercise == ExerciseType.Lunges)
        {
            return $"Ready for {GetLungeSideLabel(playerState.ExpectedLungeSide)} lunge";
        }

        if (exercise == ExerciseType.HighKnees)
        {
            return $"Ready for {GetLungeSideLabel(playerState.ExpectedHighKneeSide)} high knee";
        }

        return $"Ready for next {GetExerciseShortLabel()}";
    }

    private static string GetLungeSideLabel(ExerciseLeadSide side)
    {
        return side == ExerciseLeadSide.Left ? "left" : "right";
    }

    private static ExerciseLeadSide GetOppositeLungeSide(ExerciseLeadSide side)
    {
        return side == ExerciseLeadSide.Left ? ExerciseLeadSide.Right : ExerciseLeadSide.Left;
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

    private static float CalculateAngle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ba = a - b;
        Vector3 bc = c - b;
        if (ba.sqrMagnitude <= MinimumSegmentLength || bc.sqrMagnitude <= MinimumSegmentLength)
        {
            return 180f;
        }

        return Vector3.Angle(ba, bc);
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
