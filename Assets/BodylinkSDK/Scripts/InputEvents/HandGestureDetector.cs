using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace BodylinkSDK
{
    public class HandGestureDetector : BodylinkBaseGestureDetector
    {
        private const int MaxHandsPerPlayer = 2;

        [Header("Settings")]
        [Tooltip("Number of frames to track for detecting a swipe")]
        [SerializeField] private int bufferSize = 8;

        [Tooltip("How far the hand must move (normalized 0-1) to trigger a swipe")]
        public float swipeThreshold = 0.15f;

        [Tooltip("Max time in seconds for swipe gesture")]
        public float maxSwipeTime = 0.5f;

        private readonly HandSwipeTrack[][] swipeTracks =
        {
            new HandSwipeTrack[] { new HandSwipeTrack(), new HandSwipeTrack() },
            new HandSwipeTrack[] { new HandSwipeTrack(), new HandSwipeTrack() }
        };

        private bool IsInitialized;


        // 🔹 Toggles
        public bool EnableSwipeLeft;
        public bool EnableSwipeRight;
        public bool EnableSwipeUp;
        public bool EnableSwipeDown;
        public bool EnablePoseDetect;

        void Start()
        {
            SyncInitializationState();

            if (Bodylink.Instance == null)
            {
                return;
            }

            Bodylink.Instance.OnInitialized += HandleBodylinkReady;
            Bodylink.Instance.OnCalibrated += HandleBodylinkReady;
            Bodylink.Instance.OnPlayerOutOfScreen += HandlePlayerOutOfScreen;
            Bodylink.Instance.OnDisposed += HandleBodylinkDisposed;
        }

        void OnDestroy()
        {
            if (Bodylink.Instance == null)
            {
                return;
            }

            Bodylink.Instance.OnInitialized -= HandleBodylinkReady;
            Bodylink.Instance.OnCalibrated -= HandleBodylinkReady;
            Bodylink.Instance.OnPlayerOutOfScreen -= HandlePlayerOutOfScreen;
            Bodylink.Instance.OnDisposed -= HandleBodylinkDisposed;
        }


        void Update()
        {
            ProcessGesture();
        }

        public override void ProcessGesture()
        {
            if (IsInitialized == false || handLandMarkerResult.handLandmarks == null)
            {
                return;
            }

            if (handLandMarkerResult.handLandmarks.Count == 0)
            {
                return;
            }

            ProcessPlayerHandSwipes(0);
            if (isMultiplayerEnabled)
            {
                ProcessPlayerHandSwipes(1);
            }

            HandPoseDetection();
        }

        private void ProcessPlayerHandSwipes(int playerIndex)
        {
            if (players == null || playerIndex < 0 || playerIndex >= players.Length || players[playerIndex] == null)
            {
                return;
            }

            for (int handIndex = 0; handIndex < MaxHandsPerPlayer; handIndex++)
            {
                HandSwipeTrack track = swipeTracks[playerIndex][handIndex];
                if (TryUpdateHandBuffer(playerIndex, handIndex, track))
                {
                    DetectHandSwipeForPlayer(playerIndex, track);
                }
                else
                {
                    track.Clear();
                }
            }
        }

        private bool TryUpdateHandBuffer(int playerIndex, int handIndex, HandSwipeTrack track)
        {
            try
            {
                if (players.Length <= playerIndex || players[playerIndex].handPoints == null) return false;

                var handPoints = players[playerIndex].handPoints;
                if (handPoints == null || handPoints.Length <= handIndex || handPoints[handIndex] == null)
                {
                    return false;
                }

                List<NormalizedLandmark> landmarks = handPoints[handIndex].handLandmark;
                if (landmarks == null || landmarks.Count == 0) return false;

                var landmark = landmarks[0];
                float currentTime = Time.time;
                int sampleLimit = Mathf.Max(2, bufferSize);

                track.CurrentX = landmark.x;
                track.CurrentY = landmark.y;
                track.CurrentTime = currentTime;
                track.Buffer.Enqueue((track.CurrentX, track.CurrentY, track.CurrentTime));

                if (track.Buffer.Count > sampleLimit)
                {
                    track.Buffer.Dequeue();
                }

                TrimOldSamples(track.Buffer, currentTime);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to update hand buffer for player {playerIndex}, hand {handIndex}: {e}");
                return false;
            }
        }

        private void DetectHandSwipeForPlayer(int playerIndex, HandSwipeTrack track)
        {
            TrimOldSamples(track.Buffer, track.CurrentTime);

            if (track.Buffer.Count < 2)
            {
                return;
            }

            var first = track.Buffer.Peek();
            var last = (x: track.CurrentX, y: track.CurrentY, time: track.CurrentTime);

            float deltaX = last.x - first.x;
            float deltaY = last.y - first.y;
            float timeDelta = last.time - first.time;

            if (timeDelta <= 0f || timeDelta > maxSwipeTime)
            {
                return;
            }

            float heightRatio = GetSafeHeightRatio(playerIndex);
            float adaptiveThreshold = Mathf.Max(0.01f, swipeThreshold * heightRatio);

            bool isHorizontal = Mathf.Abs(deltaX) > Mathf.Abs(deltaY);

            if (isHorizontal)
            {
                if (EnableSwipeRight && deltaX > adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeRight", deltaX * heightRatio);
                    track.Clear();
                }
                else if (EnableSwipeLeft && deltaX < -adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeLeft", -deltaX * heightRatio);
                    track.Clear();
                }
            }
            else
            {
                if (EnableSwipeUp && deltaY < -adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeUp", -deltaY * heightRatio);
                    track.Clear();
                }
                else if (EnableSwipeDown && deltaY > adaptiveThreshold)
                {
                    OnGestureDetect(playerIndex, "SwipeDown", deltaY * heightRatio);
                    track.Clear();
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
                if (handPoints == null || handPoints.Length == 0) return;

                for (int handIndex = 0; handIndex < handPoints.Length; handIndex++)
                {
                    if (!HasValidPoseHand(handPoints, handIndex))
                    {
                        continue;
                    }

                    var gestures = handPoints[handIndex].gestures;
                    Side hand = handPoints[handIndex].handSide;
                    HandPose handPose = (HandPose)Enum.Parse(typeof(HandPose), gestures.categories[0].categoryName, true);

                    OnPoseDetected(playerIndex, "HandPoseDetect", hand, handPose);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to detect hand pose for player {playerIndex}: {e}");
            }
        }

        private bool HasValidPoseHand(BodylinkHandPoints[] handPoints, int handIndex)
        {
            if (handPoints == null ||
                handIndex < 0 ||
                handIndex >= handPoints.Length ||
                handPoints[handIndex] == null)
            {
                return false;
            }

            var landmarks = handPoints[handIndex].handLandmark;
            if (landmarks == null || landmarks.Count == 0)
            {
                return false;
            }

            var gestures = handPoints[handIndex].gestures;
            return gestures.categories != null && gestures.categories.Count > 0;
        }

        private void TrimOldSamples(Queue<(float x, float y, float time)> buffer, float now)
        {
            while (buffer.Count > 0 && now - buffer.Peek().time > maxSwipeTime)
                buffer.Dequeue();
        }

        private void ClearBuffers()
        {
            for (int playerIndex = 0; playerIndex < swipeTracks.Length; playerIndex++)
            {
                for (int handIndex = 0; handIndex < swipeTracks[playerIndex].Length; handIndex++)
                {
                    swipeTracks[playerIndex][handIndex].Clear();
                }
            }
        }

        private void HandleBodylinkReady()
        {
            SyncInitializationState();
            ClearBuffers();
        }

        private void HandlePlayerOutOfScreen()
        {
            ClearBuffers();
            if (Bodylink.Instance.autoRecalibrate == false)
            {
                return;
            }

            IsInitialized = false;
        }

        private void HandleBodylinkDisposed()
        {
            IsInitialized = false;
            ClearBuffers();
        }

        private void SyncInitializationState()
        {
            IsInitialized = Bodylink.Instance != null && Bodylink.Instance.IsInitialized;
        }

        private float GetSafeHeightRatio(int playerIndex)
        {
            float heightRatio = Bodylink.Instance.GetPlayerHeightRatio(playerIndex);
            if (float.IsNaN(heightRatio) || float.IsInfinity(heightRatio) || heightRatio <= 0f)
            {
                return 1f;
            }

            return heightRatio;
        }

        private sealed class HandSwipeTrack
        {
            public readonly Queue<(float x, float y, float time)> Buffer = new Queue<(float x, float y, float time)>();
            public float CurrentX;
            public float CurrentY;
            public float CurrentTime;

            public void Clear()
            {
                Buffer.Clear();
                CurrentX = 0f;
                CurrentY = 0f;
                CurrentTime = 0f;
            }
        }
    }
}