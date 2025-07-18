using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChessPoints : MonoBehaviour
{
    public int blackPoints, whitePoints;
    public Text whitePointText, blackPointText;
    public MoveUpdate rookWhite1, rookWhite2, rookBlack1, rookBlack2;
    void Start()
    {
        //whitePointText.text = "White Points: " + whitePoints;
        //blackPointText.text = "Black Points: " + blackPoints;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
