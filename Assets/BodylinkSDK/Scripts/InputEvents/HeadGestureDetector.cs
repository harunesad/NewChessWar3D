using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace BodylinkSDK
{
    public class HeadGestureDetector : BodylinkBaseGestureDetector
    {
        private const int MaxPlayers = 2;
        private const int MaxSamplesPerPlayer = 20;
        private const float MinHeadScale = 0.02f;
        private const float GestureTimeout = 1f;

        [Header("Gesture Settings")]
        public float movementThreshold = 0.08f;
        public float lookHoldTime = 0.5f;
        public float nodCompletionThreshold = 0.08f;
        public float shakeCompletionThreshold = 0.06f;

        public bool EnableHeadNod = true;
        public bool EnableHeadShake = true;
        public bool EnableLookLeft = true;
        public bool EnableLookRight = true;

        private enum NodState { None, Down, Up, Complete }
        private enum ShakeState { None, Left, Right, Complete }

        private readonly float[] lookTimers = new float[MaxPlayers];
        private readonly string[] currentLookDirections = new string[MaxPlayers];
        private readonly List<(Vector2 pos, float time)>[] headPositions =
        {
            new List<(Vector2 pos, float time)>(MaxSamplesPerPlayer),
            new List<(Vector2 pos, float time)>(MaxSamplesPerPlayer)
        };

        private readonly NodState[] nodStates = new NodState[MaxPlayers];
        private readonly ShakeState[] shakeStates = new ShakeState[MaxPlayers];
        private readonly float[] nodOriginY = new float[MaxPlayers];
        private readonly float[] nodLowestY = new float[MaxPlayers];
        private readonly float[] nodStartTimes = new float[MaxPlayers];
        private readonly float[] shakeOriginX = new float[MaxPlayers];
        private readonly float[] shakeExtremeX = new float[MaxPlayers];
        private readonly float[] shakeStartTimes = new float[MaxPlayers];

        private bool IsInitialized;

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

            if (IsInitialized)
            {
                ResetAllPlayerStates();
            }
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
            if (!IsInitialized || poseLandMarkerResult.poseLandmarks == null || poseLandMarkerResult.poseLandmarks.Count == 0)
            {
                return;
            }

            ProcessPlayer(0);

            if (isMultiplayerEnabled && poseLandMarkerResult.poseLandmarks.Count >= 2)
            {
                ProcessPlayer(1);
            }
        }

        private void ProcessPlayer(int playerIndex)
        {
            if (!TryGetHeadLandmarks(playerIndex, out NormalizedLandmark nose, out NormalizedLandmark leftEar, out NormalizedLandmark rightEar))
            {
                ResetPlayerState(playerIndex);
                return;
            }

            Vector2 currentPos = new Vector2(nose.x, nose.y);
            float currentTime = Time.time;

            List<(Vector2 pos, float time)> positions = headPositions[playerIndex];
            positions.Add((currentPos, currentTime));
            TrimOldSamples(positions, currentTime);

            if (positions.Count < 2)
            {
                return;
            }

            Vector2 previousPos = positions[positions.Count - 2].pos;
            float deltaX = currentPos.x - previousPos.x;
            float deltaY = currentPos.y - previousPos.y;

            float headScale = Mathf.Max(Mathf.Abs(rightEar.x - leftEar.x), MinHeadScale);
            float motionThreshold = GetMotionThreshold(headScale);
            float nodReturnThreshold = GetNodReturnThreshold(headScale);
            float shakeReturnThreshold = GetShakeReturnThreshold(headScale);

            if (EnableHeadNod)
            {
                DetectHeadNod(playerIndex, currentPos.y, deltaY, currentTime, motionThreshold, nodReturnThreshold);
            }

            if (EnableHeadShake)
            {
                DetectHeadShake(playerIndex, currentPos.x, deltaX, currentTime, motionThreshold, shakeReturnThreshold);
            }

            if (EnableLookLeft || EnableLookRight)
            {
                DetectLookDirection(playerIndex, currentPos, deltaX, leftEar, rightEar, motionThreshold);
            }
        }

        private bool TryGetHeadLandmarks(int playerIndex, out NormalizedLandmark nose, out NormalizedLandmark leftEar, out NormalizedLandmark rightEar)
        {
            nose = new NormalizedLandmark();
            leftEar = new NormalizedLandmark();
            rightEar = new NormalizedLandmark();

            if (players == null || playerIndex < 0 || playerIndex >= players.Length || players[playerIndex] == null)
            {
                return false;
            }

            if (poseLandMarkerResult.poseLandmarks == null || poseLandMarkerResult.poseLandmarks.Count <= playerIndex)
            {
                return false;
            }

            BodyPoints2D body = players[playerIndex].body2D;
            if (body == null)
            {
                return false;
            }

            nose = body.nose;
            leftEar = body.leftEar;
            rightEar = body.rightEar;

            return nose != null && leftEar != null && rightEar != null;
        }

        private void DetectHeadNod(int playerIndex, float currentY, float deltaY, float currentTime, float motionThreshold, float returnThreshold)
        {
            switch (nodStates[playerIndex])
            {
                case NodState.None:
                    if (deltaY < -motionThreshold)
                    {
                        nodStates[playerIndex] = NodState.Down;
                        nodOriginY[playerIndex] = headPositions[playerIndex][headPositions[playerIndex].Count - 2].pos.y;
                        nodLowestY[playerIndex] = currentY;
                        nodStartTimes[playerIndex] = currentTime;
                    }
                    break;

                case NodState.Down:
                    if (currentY < nodLowestY[playerIndex])
                    {
                        nodLowestY[playerIndex] = currentY;
                    }

                    if (currentY > nodLowestY[playerIndex] + returnThreshold)
                    {
                        nodStates[playerIndex] = NodState.Up;
                    }
                    else if (currentTime - nodStartTimes[playerIndex] > GestureTimeout)
                    {
                        nodStates[playerIndex] = NodState.None;
                    }
                    break;

                case NodState.Up:
                    {
                        float nodTravel = nodOriginY[playerIndex] - nodLowestY[playerIndex];
                        if (nodTravel < motionThreshold)
                        {
                            if (currentTime - nodStartTimes[playerIndex] > GestureTimeout)
                            {
                                nodStates[playerIndex] = NodState.None;
                            }
                            break;
                        }

                        if (currentY >= nodOriginY[playerIndex] - returnThreshold)
                        {
                            OnGestureDetect(playerIndex, "HeadNod", Mathf.Clamp01(nodTravel / motionThreshold));
                            nodStates[playerIndex] = NodState.Complete;
                        }
                        else if (currentY < nodLowestY[playerIndex])
                        {
                            nodStates[playerIndex] = NodState.Down;
                            nodLowestY[playerIndex] = currentY;
                        }
                        else if (currentTime - nodStartTimes[playerIndex] > GestureTimeout)
                        {
                            nodStates[playerIndex] = NodState.None;
                        }
                        break;
                    }

                case NodState.Complete:
                    if (Mathf.Abs(deltaY) < motionThreshold * 0.25f)
                    {
                        nodStates[playerIndex] = NodState.None;
                    }
                    break;
            }
        }

        private void DetectHeadShake(int playerIndex, float currentX, float deltaX, float currentTime, float motionThreshold, float returnThreshold)
        {
            switch (shakeStates[playerIndex])
            {
                case ShakeState.None:
                    if (deltaX > motionThreshold)
                    {
                        shakeStates[playerIndex] = ShakeState.Right;
                        shakeOriginX[playerIndex] = headPositions[playerIndex][headPositions[playerIndex].Count - 2].pos.x;
                        shakeExtremeX[playerIndex] = currentX;
                        shakeStartTimes[playerIndex] = currentTime;
                    }
                    else if (deltaX < -motionThreshold)
                    {
                        shakeStates[playerIndex] = ShakeState.Left;
                        shakeOriginX[playerIndex] = headPositions[playerIndex][headPositions[playerIndex].Count - 2].pos.x;
                        shakeExtremeX[playerIndex] = currentX;
                        shakeStartTimes[playerIndex] = currentTime;
                    }
                    break;

                case ShakeState.Right:
                    {
                        if (currentX > shakeExtremeX[playerIndex])
                        {
                            shakeExtremeX[playerIndex] = currentX;
                        }

                        float rightTravel = shakeExtremeX[playerIndex] - shakeOriginX[playerIndex];
                        if (rightTravel >= motionThreshold && currentX < shakeExtremeX[playerIndex] - returnThreshold)
                        {
                            OnGestureDetect(playerIndex, "HeadShake", Mathf.Clamp01(rightTravel / motionThreshold));
                            shakeStates[playerIndex] = ShakeState.Complete;
                        }
                        else if (currentTime - shakeStartTimes[playerIndex] > GestureTimeout)
                        {
                            shakeStates[playerIndex] = ShakeState.None;
                        }
                        break;
                    }

                case ShakeState.Left:
                    {
                        if (currentX < shakeExtremeX[playerIndex])
                        {
                            shakeExtremeX[playerIndex] = currentX;
                        }

                        float leftTravel = shakeOriginX[playerIndex] - shakeExtremeX[playerIndex];
                        if (leftTravel >= motionThreshold && currentX > shakeExtremeX[playerIndex] + returnThreshold)
                        {
                            OnGestureDetect(playerIndex, "HeadShake", Mathf.Clamp01(leftTravel / motionThreshold));
                            shakeStates[playerIndex] = ShakeState.Complete;
                        }
                        else if (currentTime - shakeStartTimes[playerIndex] > GestureTimeout)
                        {
                            shakeStates[playerIndex] = ShakeState.None;
                        }
                        break;
                    }

                case ShakeState.Complete:
                    if (Mathf.Abs(deltaX) < motionThreshold * 0.25f)
                    {
                        shakeStates[playerIndex] = ShakeState.None;
                    }
                    break;
            }
        }

        private void DetectLookDirection(int playerIndex, Vector2 currentPos, float deltaX, NormalizedLandmark leftEar, NormalizedLandmark rightEar, float motionThreshold)
        {
            float headWidth = Mathf.Abs(rightEar.x - leftEar.x);
            if (headWidth < Mathf.Epsilon)
            {
                ResetLookState(playerIndex);
                return;
            }

            float headCenter = (rightEar.x + leftEar.x) * 0.5f;
            float normalizedOffset = (currentPos.x - headCenter) / (headWidth * 0.5f);
            bool isHeadStable = Mathf.Abs(deltaX) < motionThreshold * 0.35f;

            if (isHeadStable && Mathf.Abs(normalizedOffset) > 0.3f)
            {
                lookTimers[playerIndex] += Time.deltaTime;

                if (lookTimers[playerIndex] >= lookHoldTime)
                {
                    if (EnableLookRight && normalizedOffset > 0f && currentLookDirections[playerIndex] != "right")
                    {
                        OnGestureDetect(playerIndex, "LookRight", Mathf.Abs(normalizedOffset));
                        currentLookDirections[playerIndex] = "right";
                    }
                    else if (EnableLookLeft && normalizedOffset < 0f && currentLookDirections[playerIndex] != "left")
                    {
                        OnGestureDetect(playerIndex, "LookLeft", Mathf.Abs(normalizedOffset));
                        currentLookDirections[playerIndex] = "left";
                    }
                }

                return;
            }

            ResetLookState(playerIndex);
        }

        private float GetMotionThreshold(float headScale)
        {
            return Mathf.Max(0.005f, headScale * movementThreshold);
        }

        private float GetNodReturnThreshold(float headScale)
        {
            return Mathf.Max(0.003f, nodCompletionThreshold * headScale);
        }

        private float GetShakeReturnThreshold(float headScale)
        {
            return Mathf.Max(0.003f, shakeCompletionThreshold * headScale);
        }

        private static void TrimOldSamples(List<(Vector2 pos, float time)> positions, float currentTime)
        {
            while (positions.Count > MaxSamplesPerPlayer)
            {
                positions.RemoveAt(0);
            }

            while (positions.Count > 0 && currentTime - positions[0].time > GestureTimeout)
            {
                positions.RemoveAt(0);
            }
        }

        private void HandleBodylinkReady()
        {
            SyncInitializationState();
            ResetAllPlayerStates();
        }

        private void HandlePlayerOutOfScreen()
        {
            ResetAllPlayerStates();

            if (Bodylink.Instance.autoRecalibrate == false)
            {
                return;
            }

            IsInitialized = false;
        }

        private void HandleBodylinkDisposed()
        {
            IsInitialized = false;
            ResetAllPlayerStates();
        }

        private void SyncInitializationState()
        {
            IsInitialized = Bodylink.Instance != null && Bodylink.Instance.IsInitialized;
        }

        private void ResetAllPlayerStates()
        {
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
            {
                ResetPlayerState(playerIndex);
            }
        }

        private void ResetPlayerState(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= MaxPlayers)
            {
                return;
            }

            headPositions[playerIndex].Clear();
            nodStates[playerIndex] = NodState.None;
            shakeStates[playerIndex] = ShakeState.None;
            nodOriginY[playerIndex] = 0f;
            nodLowestY[playerIndex] = 0f;
            nodStartTimes[playerIndex] = 0f;
            shakeOriginX[playerIndex] = 0f;
            shakeExtremeX[playerIndex] = 0f;
            shakeStartTimes[playerIndex] = 0f;
            ResetLookState(playerIndex);
        }

        private void ResetLookState(int playerIndex)
        {
            lookTimers[playerIndex] = 0f;
            currentLookDirections[playerIndex] = string.Empty;
        }

        public void ResetPlayerStates(int playerIndex)
        {
            ResetPlayerState(playerIndex);
        }
    }
}
