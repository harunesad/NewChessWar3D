using UnityEngine;
using UnityEngine.UI;

public class FitnessExample : MonoBehaviour
{
    [Header("Tracker")]
    [SerializeField] private ExerciseTracker exerciseTracker;

    [Header("Player 1 UI")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image progressFill;
    [SerializeField] private Text exerciseText;
    [SerializeField] private Text repetitionsText;
    [SerializeField] private Text setText;
    [SerializeField] private Text accuracyText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text statusText;

    [Header("Player 2 UI")]
    [SerializeField] private GameObject playerTwoPanel;
    [SerializeField] private Slider playerTwoProgressBar;
    [SerializeField] private Image playerTwoProgressFill;
    [SerializeField] private Text playerTwoExerciseText;
    [SerializeField] private Text playerTwoRepetitionsText;
    [SerializeField] private Text playerTwoSetText;
    [SerializeField] private Text playerTwoAccuracyText;
    [SerializeField] private Text playerTwoScoreText;
    [SerializeField] private Text playerTwoStatusText;

    [Header("Progress Colors")]
    [SerializeField] private Color lowProgressColor = new Color(0.9f, 0.22f, 0.22f);
    [SerializeField] private Color midProgressColor = new Color(0.96f, 0.78f, 0.2f);
    [SerializeField] private Color highProgressColor = new Color(0.2f, 0.85f, 0.35f);

    [Header("Popup")]
    [SerializeField] private GameObject popUpPanel;
    [SerializeField] private Text popUpText;

    [Header("Mini Game Flow")]
    [SerializeField]
    private ExerciseTracker.ExerciseType[] exerciseSequence =
    {
        ExerciseTracker.ExerciseType.Squat,
        ExerciseTracker.ExerciseType.Lunges,
        ExerciseTracker.ExerciseType.PushUp,
        ExerciseTracker.ExerciseType.Plank,
        ExerciseTracker.ExerciseType.JumpingJack,
        ExerciseTracker.ExerciseType.HighKnees
    };
    [SerializeField, Min(0f)] private float readyCountdownSeconds = 3f;
    [SerializeField] private bool startSequenceOnEnable = true;
    [SerializeField] private bool allowKeyboardRestart = true;
    [SerializeField] private bool allowPointerRestart = true;
    [SerializeField] private KeyCode restartKey = KeyCode.Space;

    private int currentExerciseIndex;
    private float countdownRemaining;
    private bool isCountdownActive;
    private bool isWaitingForRestart;
    private bool hasStartedSequence;
    private bool countdownIsFirstExercise;

    private void Awake()
    {
        if (exerciseTracker == null)
        {
            exerciseTracker = GetComponent<ExerciseTracker>();
        }

        CacheProgressFills();
    }

    private void OnEnable()
    {
        CacheProgressFills();

        if (exerciseTracker != null)
        {
            exerciseTracker.MetricsUpdated += HandleMetricsUpdated;
        }

        if (startSequenceOnEnable)
        {
            StartMiniGame();
        }

        RefreshUi();
    }

    private void OnDisable()
    {
        if (exerciseTracker != null)
        {
            exerciseTracker.MetricsUpdated -= HandleMetricsUpdated;
        }
    }

    private void OnValidate()
    {
        CacheProgressFills();
    }

    private void Update()
    {
        UpdateMiniGameFlow();
        RefreshUi();
    }

    private void HandleMetricsUpdated(ExerciseTracker tracker)
    {
        RefreshUi();
    }

    private void CacheProgressFills()
    {
        CacheProgressFill(progressBar, ref progressFill);
        CacheProgressFill(playerTwoProgressBar, ref playerTwoProgressFill);
    }

    private static void CacheProgressFill(Slider progressSlider, ref Image fillImage)
    {
        if (fillImage == null && progressSlider != null && progressSlider.fillRect != null)
        {
            fillImage = progressSlider.fillRect.GetComponent<Image>();
        }
    }

    public void StartMiniGame()
    {
        if (exerciseTracker == null || exerciseSequence == null || exerciseSequence.Length == 0)
        {
            return;
        }

        hasStartedSequence = true;
        isWaitingForRestart = false;
        PrepareExercise(0, true);
    }

    public void RestartMiniGame()
    {
        StartMiniGame();
    }

    private void PrepareExercise(int exerciseIndex, bool isFirstExercise)
    {
        if (exerciseTracker == null || exerciseSequence == null || exerciseIndex < 0 || exerciseIndex >= exerciseSequence.Length)
        {
            return;
        }

        currentExerciseIndex = exerciseIndex;
        countdownIsFirstExercise = isFirstExercise;
        exerciseTracker.SetExercise(exerciseSequence[exerciseIndex]);
        exerciseTracker.PauseTracking($"Get ready for {exerciseTracker.ExerciseDisplayName}");

        if (readyCountdownSeconds <= 0f)
        {
            isCountdownActive = false;
            HidePopup();
            exerciseTracker.ResumeTracking();
            return;
        }

        countdownRemaining = readyCountdownSeconds;
        isCountdownActive = true;
        ShowPopup(BuildCountdownMessage(isFirstExercise));
    }

    private void CompleteSequence()
    {
        isCountdownActive = false;
        isWaitingForRestart = true;

        if (exerciseTracker != null)
        {
            exerciseTracker.PauseTracking("Workout complete");
        }

        ShowPopup(BuildRestartMessage());
    }

    private void UpdateMiniGameFlow()
    {
        UpdatePlayerTwoVisibility();

        if (!hasStartedSequence)
        {
            return;
        }

        if (isCountdownActive)
        {
            countdownRemaining = Mathf.Max(0f, countdownRemaining - Time.deltaTime);
            ShowPopup(BuildCountdownMessage(countdownIsFirstExercise));

            if (countdownRemaining <= 0f)
            {
                isCountdownActive = false;
                HidePopup();

                if (exerciseTracker != null)
                {
                    exerciseTracker.ResumeTracking();
                }
            }

            return;
        }

        if (!isWaitingForRestart && AreAllActivePlayersComplete())
        {
            int nextExerciseIndex = currentExerciseIndex + 1;
            if (nextExerciseIndex < exerciseSequence.Length)
            {
                PrepareExercise(nextExerciseIndex, false);
            }
            else
            {
                CompleteSequence();
            }
            return;
        }

        if (isWaitingForRestart && IsRestartRequested())
        {
            StartMiniGame();
        }
    }

    private bool AreAllActivePlayersComplete()
    {
        if (exerciseTracker == null)
        {
            return false;
        }

        int trackedPlayerCount = exerciseTracker.GetTrackedPlayerCount();
        for (int i = 0; i < trackedPlayerCount; i++)
        {
            if (!exerciseTracker.IsPlayerWorkoutComplete(i))
            {
                return false;
            }
        }

        return trackedPlayerCount > 0;
    }

    private bool IsRestartRequested()
    {
        if (allowKeyboardRestart && Input.GetKeyDown(restartKey))
        {
            return true;
        }

        if (allowPointerRestart)
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                return true;
            }
        }

        return false;
    }

    private string BuildCountdownMessage(bool isFirstExercise)
    {
        string prefix = isFirstExercise ? "First exercise" : "Next exercise";
        string exerciseName = exerciseTracker != null ? exerciseTracker.ExerciseDisplayName : "Exercise";
        int seconds = Mathf.Max(1, Mathf.CeilToInt(countdownRemaining));
        return
            $"<size=15><b>{prefix}</b></size>\n" +
            $"<size=15><b>{exerciseName.ToUpperInvariant()}</b></size>\n" +
            "<size=15><i>Get ready</i></size>\n" +
            $"<size=15>Starting in <b>{seconds}</b></size>";
    }

    private string BuildRestartMessage()
    {
        if (allowKeyboardRestart && allowPointerRestart)
        {
            return
                "<size=15><b>WORKOUT COMPLETE!</b></size>\n" +
                "<size=15><i>Great job.</i></size>\n" +
                "<size=15>Press <b>Space</b> or <b>click</b> to start again.</size>";
        }

        if (allowKeyboardRestart)
        {
            return
                "<size=15><b>WORKOUT COMPLETE!</b></size>\n" +
                "<size=15><i>Great job.</i></size>\n" +
                "<size=15>Press <b>Space</b> to start again.</size>";
        }

        if (allowPointerRestart)
        {
            return
                "<size=15><b>WORKOUT COMPLETE!</b></size>\n" +
                "<size=15><i>Great job.</i></size>\n" +
                "<size=15><b>Click</b> or <b>tap</b> to start again.</size>";
        }

        return
            "<size=15><b>WORKOUT COMPLETE!</b></size>\n" +
            "<size=15><i>Great job.</i></size>\n" +
            "<size=15>Call <b>RestartMiniGame()</b> to start again.</size>";
    }

    private void ShowPopup(string message)
    {
        if (popUpPanel == null || popUpText == null)
        {
            return;
        }

        popUpText.supportRichText = true;
        popUpText.text = message;
        if (!popUpPanel.activeSelf)
        {
            popUpPanel.SetActive(true);
        }
    }

    private void HidePopup()
    {
        if (popUpPanel != null && popUpPanel.activeSelf)
        {
            popUpPanel.SetActive(false);
        }
    }

    private void RefreshUi()
    {
        if (exerciseTracker == null)
        {
            return;
        }

        UpdatePlayerUi(
            0,
            progressBar,
            progressFill,
            exerciseText,
            repetitionsText,
            setText,
            accuracyText,
            scoreText,
            statusText);

        UpdatePlayerUi(
            1,
            playerTwoProgressBar,
            playerTwoProgressFill,
            playerTwoExerciseText,
            playerTwoRepetitionsText,
            playerTwoSetText,
            playerTwoAccuracyText,
            playerTwoScoreText,
            playerTwoStatusText);

        UpdatePlayerTwoVisibility();
    }

    private void UpdatePlayerUi(
        int playerSlot,
        Slider playerProgressBar,
        Image playerProgressFill,
        Text playerExerciseText,
        Text playerRepetitionsText,
        Text playerSetText,
        Text playerAccuracyText,
        Text playerScoreText,
        Text playerStatusText)
    {
        if (exerciseTracker == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(exerciseTracker.GetLiveProgress(playerSlot));
        Color progressColor = GetProgressColor(progress);

        if (playerProgressBar != null)
        {
            playerProgressBar.minValue = 0f;
            playerProgressBar.maxValue = 1f;
            playerProgressBar.value = progress;
        }

        if (playerProgressFill != null)
        {
            playerProgressFill.color = progressColor;
        }

        if (playerExerciseText != null)
        {
            playerExerciseText.text = exerciseTracker.ExerciseDisplayName.ToUpperInvariant();
        }

        if (playerRepetitionsText != null)
        {
            playerRepetitionsText.text =
                $"{exerciseTracker.ExerciseCountLabel}: {exerciseTracker.GetRepetitionCount(playerSlot)}/{exerciseTracker.TargetRepetitions}";
        }

        if (playerSetText != null)
        {
            playerSetText.text =
                $"Set {exerciseTracker.GetCurrentSetNumber(playerSlot)}  Rep {exerciseTracker.GetCurrentSetRepCount(playerSlot)}/{exerciseTracker.RepsPerSet}";
        }

        if (playerAccuracyText != null)
        {
            playerAccuracyText.text = $"Accuracy: {exerciseTracker.GetLiveAccuracy(playerSlot):F0}%";
        }

        if (playerScoreText != null)
        {
            playerScoreText.text = $"Final Score: {exerciseTracker.GetFinalScore(playerSlot):F0}%";
        }

        if (playerStatusText != null)
        {
            playerStatusText.text = exerciseTracker.GetStatusMessage(playerSlot);
        }
    }

    private void UpdatePlayerTwoVisibility()
    {
        if (playerTwoPanel != null)
        {
            playerTwoPanel.SetActive(exerciseTracker != null && exerciseTracker.GetTrackedPlayerCount() > 1);
        }
    }

    private Color GetProgressColor(float progress)
    {
        float yellowThreshold = exerciseTracker != null ? exerciseTracker.MediumThreshold : 0.55f;
        float greenThreshold = exerciseTracker != null ? exerciseTracker.HighThreshold : 0.75f;

        if (progress <= yellowThreshold)
        {
            return Color.Lerp(
                lowProgressColor,
                midProgressColor,
                Mathf.InverseLerp(0f, Mathf.Max(0.0001f, yellowThreshold), progress));
        }

        if (progress >= greenThreshold)
        {
            return highProgressColor;
        }

        float yellowToGreenRange = Mathf.Max(0.0001f, greenThreshold - yellowThreshold);
        float yellowToGreenProgress = Mathf.Clamp01((progress - yellowThreshold) / yellowToGreenRange);
        return Color.Lerp(midProgressColor, highProgressColor, yellowToGreenProgress);
    }
}
