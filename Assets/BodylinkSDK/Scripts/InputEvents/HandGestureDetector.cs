using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace BodylinkSDK
{
    public class HandGestureDetector : BodylinkBaseGestureDetector
    {
        [Header("Settings")]
        [Tooltip("Number of frames to track for detecting a swipe")]
        [SerializeField] private int bufferSize = 8;

        [Tooltip("How far the hand must move (normalized 0-1) to trigger a swipe")]
        public float swipeThreshold = 0.15f;

        [Tooltip("Max time in seconds for swipe gesture")]
        public float maxSwipeTime = 0.5f;


        private Queue<(float x, float y, float time)> playerOneHandPositions = new Queue<(float, float, float)>();
        private Queue<(float x, float y, float time)> playerTwoHandPositions = new Queue<(float, float, float)>();
        private float playerOneCurrentX, playerOneCurrentY, playerOneCurrentTime;
        private float playerTwoCurrentX, playerTwoCurrentY, playerTwoCurrentTime;
        private bool IsInitialized;


        // 🔹 Toggles
        public bool EnableSwipeLeft;
        public bool EnableSwipeRight;
        public bool EnableSwipeUp;
        public bool EnableSwipeDown;
        public bool EnablePoseDetect;

        void Start()
        {
            Bodylink.Instance.OnInitialized += () =>
            {
                IsInitialized = true;
                ClearBuffers();
            };
            Bodylink.Instance.OnPlayerOutOfScreen += () =>
            {
                if (Bodylink.Instance.autoRecalibrate == false) return;
                IsInitialized = false;
                ClearBuffers();
            };
        }


        void Update()
        {
            ProcessGesture();
        }

        public override void ProcessGesture()
        {
            if (IsInitialized == false || handLandMarkerResult.handLandmarks == null) return;

            if (handLandMarkerResult.handLandmarks.Count == 0 || handLandMarkerResult.handLandmarks[0].landmarks.Count <= 0)
                return;

            bool playerOneUpdated = TryUpdateHandBuffer(0, ref playerOneCurrentX, ref playerOneCurrentY, ref playerOneCurrentTime, playerOneHandPositions);
            bool playerTwoUpdated = isMultiplayerEnabled &&
                                    TryUpdateHandBuffer(1, ref playerTwoCurrentX, ref playerTwoCurrentY, ref playerTwoCurrentTime, playerTwoHandPositions);

            if (playerOneUpdated) PlayerOneDetectHandSwipe();
            if (playerTwoUpdated) PlayerTwoDetectHandSwipe();
            HandPoseDetection();
        }

        private bool TryUpdateHandBuffer(int playerIndex, ref float currentX, ref float currentY, ref float currentTime, Queue<(float x, float y, float time)> buffer)
        {
            try
            {
                if (players.Length <= playerIndex || players[playerIndex].handPoints == null) return false;

                var handPoints = players[playerIndex].handPoints;
                if (handPoints == null || handPoints.Length == 0) return false;

                List<NormalizedLandmark> landmarks = null;
                for (int i = 0; i < handPoints.Length; i++)
                {
                    if (handPoints[i] == null) continue;
                    var candidate = handPoints[i].handLandmark;
                    if (candidate != null && candidate.Count > 0)
                    {
                        landmarks = candidate;
                        break;
                    }
                }

                if (landmarks == null || landmarks.Count == 0) return false;

                var landmark = landmarks[0];
                currentX = landmark.x;
                currentY = landmark.y;
                currentTime = Time.time;

                buffer.Enqueue((currentX, currentY, currentTime));
                if (buffer.Count > bufferSize)
                    buffer.Dequeue();

                TrimOldSamples(buffer, currentTime);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to update hand buffer for player {playerIndex}: {e}");
                return false;
            }
        }


        private void PlayerOneDetectHandSwipe()
        {
            DetectHandSwipeForPlayer(0, playerOneHandPositions, playerOneCurrentX, playerOneCurrentY, playerOneCurrentTime);
        }

        private void PlayerTwoDetectHandSwipe()
        {
            if (!isMultiplayerEnabled) return;
            DetectHandSwipeForPlayer(1, playerTwoHandPositions, playerTwoCurrentX, playerTwoCurrentY, playerTwoCurrentTime);
        }

        private void DetectHandSwipeForPlayer(int playerIndex, Queue<(float x, float y, float time)> buffer, float currentX, float currentY, float currentTime)
        {
            TrimOldSamples(buffer, currentTime);

            if (buffer.Count < 2) return;

            var first = buffer.Peek();
            var last = (x: currentX, y: currentY, time: currentTime);

            float deltaX = last.x - first.x;
            float deltaY = last.y - first.y;
            float timeDelta = last.time - first.time;

            if (timeDelta <= 0f || timeDelta > maxSwipeTime) return;

            float heightRatio = Bodylink.Instance.GetPlayerHeightRatio(playerIndex);
            float adaptiveThreshold = Mathf.Max(0.01f, swipeThreshold * heightRatio);

            bool isHorizontal = Mathf.Abs(deltaX) > Mathf.Abs(deltaY);

            if (isHorizontal)
            {
                if (EnableSwipeRight && deltaX > adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeRight", deltaX * heightRatio);
                    buffer.Clear();
                }
                else if (EnableSwipeLeft && deltaX < -adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeLeft", -deltaX * heightRatio);
                    buffer.Clear();
                }
            }
            else
            {
                if (EnableSwipeUp && deltaY < -adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeUp", -deltaY * heightRatio);
                    buffer.Clear();
                }
                else if (EnableSwipeDown && deltaY > adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeDown", deltaY * heightRatio);
                    buffer.Clear();
                }
            }
        }

        private void HandPoseDetection()
        {

            if (!EnablePoseDetect) return;
            //if (poseLandMarkerResult.poseLandmarks == null) return;

            if (isMultiplayerEnabled)
            {

                DetectAndTriggerHandPose(0);

                DetectAndTriggerHandPose(1);

            }
            else
            {
                DetectAndTriggerHandPose(0);
            }
        }

        private void DetectAndTriggerHandPose(int playerIndex)
        {
            try
            {
                var handPoints = players[playerIndex].handPoints;
                int handSide = handPoints[0].handLandmark == null ? (handPoints[1].handLandmark == null ? -1 : 1) : 0;
                if (handSide < 0) return;

                var landmark = handPoints[handSide].handLandmark[0];
                if (landmark == null) return;

                var gestures = handPoints[handSide].gestures;
                if (gestures.categories == null || gestures.categories.Count == 0) return;

                Side hand = handPoints[handSide].handSide;
                HandPose handPose = (HandPose)Enum.Parse(typeof(HandPose), gestures.categories[0].categoryName, true);

                OnPoseDetected(playerIndex, "HandPoseDetect", hand, handPose);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to detect hand pose for player {playerIndex}: {e}");
            }
        }

        private void TrimOldSamples(Queue<(float x, float y, float time)> buffer, float now)
        {
            while (buffer.Count > 0 && now - buffer.Peek().time > maxSwipeTime)
                buffer.Dequeue();
        }

        private void ClearBuffers()
        {
            playerOneHandPositions.Clear();
            playerTwoHandPositions.Clear();
        }
    }
}
