using System;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;

namespace BodylinkSDK
{
    /// <summary>
    /// Base class for all gesture detectors.
    /// Handles enabling/disabling, global gesture triggering, and metadata.
    /// </summary>
    public abstract class BodylinkBaseGestureDetector : MonoBehaviour
    {
        // Fired when a gesture is detected (for modular registration)
        public event Action<int, string, object[]> OnGesture;
        public event Action<int, string, Side, HandPose> OnPose;

        public bool isMultiplayerEnabled => Bodylink.Instance.isMultiplayerEnabled;
        public BodylinkAvatar bodylinkAvatar => Bodylink.Instance.bodylinkAvatar;

        public PoseLandmarkerResult poseLandMarkerResult => Bodylink.Instance.bodylinkAvatar.poseLandmarkerResult;
        public GestureRecognizerResult handLandMarkerResult => Bodylink.Instance.bodylinkAvatar.handLandmarkerResult;

        public BodylinkPlayerAvatar[] players => Bodylink.Instance.bodylinkAvatar.players;

        public BodyPoints2DGameObject body2DGameObjects => Bodylink.Instance.bodyPoints2DGameObject;
        public BodyPoints3DGameObject body3DGameObjects => Bodylink.Instance.bodyPoints3DGameObject;

        /// <summary>
        /// Invoke a gesture event (broadcasts to EventBus and subscribers).
        /// </summary>
        protected void OnGestureDetect(int playerIndex, string name, params object[] value)
        {
            OnGesture?.Invoke(playerIndex, name, value);
        }

        protected void OnPoseDetected(int playerIndex, string name, Side side, HandPose pose)
        {
            OnPose?.Invoke(playerIndex, name, side, pose);
        }


        /// <summary>
        /// Each subclass implements this to define detection logic.
        /// </summary>
        public abstract void ProcessGesture();
    }
}