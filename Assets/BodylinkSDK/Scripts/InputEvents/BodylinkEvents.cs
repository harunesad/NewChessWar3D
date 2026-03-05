using System;
using System.Collections.Generic;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodylinkEvents : MonoBehaviour
    {

        [Header("Event Toggles")]
        public bool EnableHeadEvents = true;
        public bool EnableHandEvents = true;
        public bool EnableBodyEvents = true;

        [HideInInspector] public HandGestureDetector handGestureDetector;
        [HideInInspector] public BodyMovementDetector bodyMovementDetector;
        [HideInInspector] public HeadGestureDetector headGestureDetector;

        // 🧩 List of any custom detectors (plug-ins)
        private List<BodylinkBaseGestureDetector> customDetectors = new();

        public event Action<int, string, object[]> OnGestureDetection;
        public event Action<int, string, Side, HandPose> OnPoseDetection;

        void OnEnable()
        {
            RegisterDetectors();
        }

        void OnDisable()
        {
            UnregisterDetectors();
        }


        /// <summary>
        /// Automatically registers any custom gesture detector present anywhere in the scene.
        /// </summary>
        private void RegisterDetectors()
        {
            UnregisterDetectors();
            customDetectors.Clear();

            customDetectors.AddRange(FindObjectsByType<BodylinkBaseGestureDetector>(FindObjectsSortMode.None));

            foreach (var detector in customDetectors)
            {
                if (detector == null) continue;
                detector.OnGesture += HandleGesture;
                detector.OnPose += HandlePose;

            }
        }

        private void UnregisterDetectors()
        {
            foreach (var detector in customDetectors)
            {
                if (detector == null) continue;
                detector.OnGesture -= HandleGesture;
                detector.OnPose -= HandlePose;

            }
            customDetectors.Clear();
        }

        private void HandleGesture(int playerIndex, string gestureName, object[] value)
        {
            OnGestureDetection?.Invoke(playerIndex, gestureName, value);
        }

        private void HandlePose(int playerIndex, string gestureName, Side side, HandPose pose)
        {
            OnPoseDetection?.Invoke(playerIndex, gestureName, side, pose);
        }
    }
}
