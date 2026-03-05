using System;
using System.Collections;
using Mediapipe.Unity;
using UnityEngine;

namespace BodylinkSDK
{

    public class BodylinkMultiPoseList : MonoBehaviour
    {
        public static Action<int, PointListAnnotation, PointListAnnotation> onPlayerFound;
        PointListAnnotation playerOnePoints = null;
        PointListAnnotation playerTwoPoints = null;

        public void PlayerActivate()
        {
            PlayerStatus();
        }
        

        private void PlayerStatus()
        {
            int childCount = transform.childCount;
            if (childCount == 1)
            {
                if (transform.GetChild(0).gameObject !=null && transform.GetChild(0).gameObject.activeSelf)
                {
                    playerOnePoints = transform.GetChild(0).gameObject.GetComponentInChildren<PointListAnnotation>();
                }
            }
            else if (childCount == 2)
            {

                if (transform.GetChild(0).gameObject.activeSelf)
                {
                    playerOnePoints = transform.GetChild(0).gameObject.GetComponentInChildren<PointListAnnotation>();
                }
                if (transform.GetChild(1).gameObject.activeSelf)
                {
                    playerTwoPoints = transform.GetChild(1).gameObject.GetComponentInChildren<PointListAnnotation>();
                }
                else
                {
                    playerTwoPoints = null;
                }

            }

            onPlayerFound?.Invoke(childCount, playerOnePoints, playerTwoPoints);
        }

    }
}
