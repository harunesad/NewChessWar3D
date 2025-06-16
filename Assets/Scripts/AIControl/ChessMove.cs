using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessMove
{
    public int value; // Hamlenin deðeri
    public int startX; // Hamlenin baþlangýç pozisyonu
    public int startY;
    public int endX;   // Hamlenin bitiþ pozisyonu
    public int endY;

    // Varsayýlan yapýcý metod
    public ChessMove()
    {
        value = 0;
        startX = 0;
        startY = 0;
        endX = 0;
        endY = 0;
    }
}
