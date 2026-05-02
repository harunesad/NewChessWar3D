using System.Collections;
using UnityEngine;
using ChessEngine.Game;
using ChessEngine;

public class TutorialManager : MonoBehaviour
{
    private enum TutorialStep
    {
        NONE,
        WELCOME,
        MOVE_TO_PIECE,
        CARRY_PIECE,
        MOVE_TO_TILE,
        DROP_PIECE,
        SUCCESS,
        COMPLETED
    }

    [Header("References")]
    public TPSChessInteractor interactor;
    public GameUIManager uiManager;
    public ChessGameManager gameManager;

    private TutorialStep currentStep = TutorialStep.NONE;
    private bool isTutorialActive = false;
    private string playerColor;

    void Start()
    {
        // Check if tutorial is already completed
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 1)
        {
            Destroy(this);
            return;
        }

        playerColor = PlayerPrefs.GetString("Type", "White");
        StartCoroutine(StartTutorialRoutine());
    }

    IEnumerator StartTutorialRoutine()
    {
        yield return new WaitForSeconds(1.0f); // Wait for game to initialize
        
        isTutorialActive = true;
        SetStep(TutorialStep.WELCOME);
        
        // Subscribe to interactor events
        if (interactor != null)
        {
            interactor.OnInteractionZoneEntered += OnZoneEntered;
            interactor.OnPiecePickedUp += OnPickedUp;
            interactor.OnPieceDropped += OnDropped;
        }
    }

    void OnDestroy()
    {
        if (interactor != null)
        {
            interactor.OnInteractionZoneEntered -= OnZoneEntered;
            interactor.OnPiecePickedUp -= OnPickedUp;
            interactor.OnPieceDropped -= OnDropped;
        }
    }

    private void SetStep(TutorialStep step)
    {
        currentStep = step;
        Debug.Log("Tutorial Step: " + step);

        string colorLower = playerColor.ToLower();

        switch (step)
        {
            case TutorialStep.WELCOME:
                uiManager.ShowTutorialMessage($"Welcome, Commander! Use the joystick to move towards your {colorLower} pieces.");
                StartCoroutine(NextStepAfterDelay(TutorialStep.MOVE_TO_PIECE, 4.0f));
                break;

            case TutorialStep.MOVE_TO_PIECE:
                uiManager.ShowTutorialMessage($"Move close to one of your {colorLower} pieces to interact with it.");
                break;

            case TutorialStep.CARRY_PIECE:
                uiManager.ShowTutorialMessage("Great! Now stand directly behind it and press SELECT to pick it up.");
                break;

            case TutorialStep.MOVE_TO_TILE:
                uiManager.ShowTutorialMessage("Now move to a target tile while carrying the piece.");
                break;

            case TutorialStep.DROP_PIECE:
                uiManager.ShowTutorialMessage("Press SELECT again to complete your move.");
                break;

            case TutorialStep.SUCCESS:
                uiManager.ShowTutorialMessage("Well done! You are ready for battle. Good luck!");
                PlayerPrefs.SetInt("TutorialCompleted", 1);
                PlayerPrefs.Save();
                StartCoroutine(EndTutorial());
                break;
        }
    }

    IEnumerator NextStepAfterDelay(TutorialStep next, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentStep == TutorialStep.WELCOME)
            SetStep(next);
    }

    IEnumerator EndTutorial()
    {
        yield return new WaitForSeconds(4.0f);
        uiManager.HideTutorialMessage();
        isTutorialActive = false;
        Destroy(this);
    }

    private void OnZoneEntered()
    {
        if (currentStep == TutorialStep.MOVE_TO_PIECE)
        {
            SetStep(TutorialStep.CARRY_PIECE);
        }
    }

    private void OnPickedUp()
    {
        if (currentStep == TutorialStep.CARRY_PIECE || currentStep == TutorialStep.MOVE_TO_PIECE)
        {
            SetStep(TutorialStep.MOVE_TO_TILE);
        }
    }

    private void OnDropped()
    {
        if (currentStep == TutorialStep.MOVE_TO_TILE || currentStep == TutorialStep.DROP_PIECE)
        {
            SetStep(TutorialStep.SUCCESS);
        }
    }

    // Context menu to reset tutorial for testing
    [ContextMenu("Reset Tutorial")]
    public void ResetTutorial()
    {
        PlayerPrefs.SetInt("TutorialCompleted", 0);
        PlayerPrefs.Save();
        Debug.Log("Tutorial reset. Restart the scene to see it.");
    }
}
