using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.AI;

public class CamSwerve : MonoBehaviour
{
    float lastFrameFingerPositionX;
    public float moveFactorX;
    [SerializeField]
    List<Transform> camPos;
    public int camPosIndex;
    void Update()
    {
        System();
        transform.LookAt(new Vector3(-.725f, 0, 0));
    }
    public void System()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastFrameFingerPositionX = Input.mousePosition.x;
        }
        else if (Input.GetMouseButton(0))
        {
            moveFactorX = Input.mousePosition.x - lastFrameFingerPositionX;
            //lastFrameFingerPositionX = Input.mousePosition.x;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            Move();
            moveFactorX = 0f;
        }
    }
    public void Move()
    {
        if (moveFactorX < -500)
        {
            Debug.Log(moveFactorX + " " + camPosIndex);
            if (camPosIndex == 0)
            {
                camPosIndex = 3;
            }
            else
            {
                camPosIndex--;
            }
            transform.DOMove(camPos[camPosIndex].position, 0.4f);
        }
        else if (moveFactorX > 500)
        {
            Debug.Log(moveFactorX + " " + camPosIndex);
            if (camPosIndex == 3)
            {
                camPosIndex = 0;
            }
            else
            {
                camPosIndex++;
            }
            transform.DOMove(camPos[camPosIndex].position, 0.4f);
        }
    }
}
