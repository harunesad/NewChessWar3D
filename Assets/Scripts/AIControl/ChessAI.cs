using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessAI : MonoBehaviour
{
    private const int MaxDepth = 3; // Algoritmanýn kaç seviye derinlikte arama yapacaðý
    [SerializeField]
    ChessBoard chessBoard;

    // Diðer kodlar ve fonksiyonlar...

    public void MakeBestMove()
    {
        ChessMove bestMove = Minimax(MaxDepth, false);
        // En iyi hamleyi yap
        chessBoard.MakeMove(bestMove);
    }

    private ChessMove Minimax(int depth, bool isMaximizingPlayer)
    {
        List<ChessMove> legalMoves = chessBoard.GenerateLegalMoves(); // Mevcut durumda geçerli hamleleri oluþtur

        if (depth == 0 || legalMoves.Count == 0) // Derinlik sýfýra ulaþtýðýnda veya geçerli hamle kalmadýðýnda dur
        {
            return chessBoard.EvaluateBoard(); // Tahtayý deðerlendir ve en iyi hamleyi döndür
        }

        ChessMove bestMove = new ChessMove();
        int bestValue = isMaximizingPlayer ? int.MinValue : int.MaxValue;

        foreach (ChessMove move in legalMoves)
        {
            chessBoard.MakeMove(move);

            int value;
            if (isMaximizingPlayer)
            {
                value = Minimax(depth - 1, false).value;
            }
            else
            {
                // Sadece siyahýn hamlelerini deðerlendir
                value = Minimax(depth - 1, true).value;
            }

            if ((isMaximizingPlayer && value > bestValue) || (!isMaximizingPlayer && value < bestValue))
            {
                bestValue = value;
                bestMove = move;
            }

            chessBoard.UndoMove(move); // Hamleyi geri al
        }

        return bestMove;
    }

    // Diðer yardýmcý fonksiyonlar...}
}
