using System;
using System.Collections;
using Mediapipe.Unity;
using UnityEngine;
namespace BodylinkSDK
{

    public class BodylinkPoseLandmarkListAnnotation : MonoBehaviour
    {
        public static Action<PointListAnnotation> OnPointListAnnotation;
        private BodylinkMultiPoseList bodylinkMultiposeList;

        private void Awake()
        {
            bodylinkMultiposeList = GetComponentInParent<BodylinkMultiPoseList>();
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            PointListAnnotation pointListAnnotation = GetComponentInChildren<PointListAnnotation>();
            if (pointListAnnotation == null)
            {
                Debug.LogError("[Bodylink] PointListAnnotation component is missing.");
            }
            else
            {
                OnPointListAnnotation?.Invoke(pointListAnnotation);

            }

        }

        void OnEnable()
        {
            bodylinkMultiposeList.PlayerActivate();
        }

    }
}