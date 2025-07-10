using ChessEngine.Game.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameChanger : MonoBehaviour
{
    [SerializeField] ChessAIGameManager chessAIGameManager;
    Difficulty difficulty;
    void Start()
    {
        difficulty = FindAnyObjectByType<Difficulty>();
        if (PlayerPrefs.GetString("Type") == "White")
        {
            chessAIGameManager.blackAIThinkDepth = difficulty.difficult;
            chessAIGameManager.blackAIThinkTime = difficulty.difficult / 2;
        }
        else
        {
            chessAIGameManager.whiteAIThinkDepth = difficulty.difficult;
            chessAIGameManager.whiteAIThinkTime = difficulty.difficult / 2;
        }
        Debug.Log(chessAIGameManager.whiteAIThinkDepth + " " + chessAIGameManager.blackAIThinkDepth);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
