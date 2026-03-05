using UnityEngine;
using Mediapipe.Unity;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using System.Collections;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.GestureRecognizer;

namespace BodylinkSDK
{
    public class BodylinkAvatar : MonoBehaviour
    {

        public PoseLandmarkerResult poseLandmarkerResult { get; private set; }
        public GestureRecognizerResult handLandmarkerResult { get; private set; }

        public BodylinkPlayerAvatar[] players;

        [SerializeField] private Image background;

        private BodylinkPlayerIdentityTracker identityTracker = new BodylinkPlayerIdentityTracker();
        PointListAnnotation _playerOnePointListAnnotation, _playerTwoPointListAnnotation;
        public int[] stablePlayerIndex = new int[2];


        private bool isMultiplayerEnabled = false;
        private int calibrationCount = 0;
        private bool isCalibratedCalled;
        private float setLeftRightPlayerTimeoutSeconds = 30f;
        private Coroutine setLeftRightPlayerRoutine;

        List<List<NormalizedLandmark>> mpPlayers = new();
        private readonly object resultBufferLock = new object();
        private PoseLandmarkerResult poseWriteBuffer;
        private PoseLandmarkerResult poseReadBuffer;
        private bool hasPendingPoseResult;
        private GestureRecognizerResult handWriteBuffer;
        private GestureRecognizerResult handReadBuffer;
        private bool hasPendingHandResult;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            isMultiplayerEnabled = Bodylink.Instance.numberOfPlayers >= 2;
            ShowImages(false);
            Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult += OnPoseLandmarkDetectionOutput;
            Bodylink.Instance.handGestureRunnerInstance.OnHandLandmarkDetectionOutputAction += OnHandLandmarkDetectionOutput;
            players[0].gameObject.SetActive(true);
            players[0].Init(CheckCalibration, OnPlayerOutOfScreen);
            players[1].gameObject.SetActive(isMultiplayerEnabled);
            players[1].Init(CheckCalibration, OnPlayerOutOfScreen);

        }

        void Update()
        {
            PoseLandmarkerResult pendingPoseResult = default;
            GestureRecognizerResult pendingHandResult = default;
            bool processPose = false;
            bool processHand = false;

            lock (resultBufferLock)
            {
                if (hasPendingPoseResult)
                {
                    var poseSwap = poseReadBuffer;
                    poseReadBuffer = poseWriteBuffer;
                    poseWriteBuffer = poseSwap;
                    hasPendingPoseResult = false;

                    pendingPoseResult = poseReadBuffer;
                    processPose = pendingPoseResult.poseLandmarks != null;
                }

                if (hasPendingHandResult)
                {
                    var handSwap = handReadBuffer;
                    handReadBuffer = handWriteBuffer;
                    handWriteBuffer = handSwap;
                    hasPendingHandResult = false;

                    pendingHandResult = handReadBuffer;
                    processHand = pendingHandResult.handLandmarks != null;
                }
            }

            if (processPose)
            {
                ProcessPoseLandmarkDetectionOutput(pendingPoseResult);
            }

            if (processHand)
            {
                ProcessHandLandmarkDetectionOutput(pendingHandResult);
            }
        }


        private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, long timestamp)
        {
            if (result.poseLandmarks == null || result.poseLandmarks.Count < 1) return;

            lock (resultBufferLock)
            {
                result.CloneTo(ref poseWriteBuffer);
                hasPendingPoseResult = true;
            }
        }

        private void ProcessPoseLandmarkDetectionOutput(PoseLandmarkerResult result)
        {
            poseLandmarkerResult = result;
            //if (!Bodylink.Instance.IsCalibrated) return;

            // === 1) Build list for identity tracker ===
            mpPlayers.Clear();
            foreach (var p in poseLandmarkerResult.poseLandmarks)
                mpPlayers.Add(p.landmarks);

            // === 2) Get stable identity mapping ===
            identityTracker.GetStableIdentity(mpPlayers, stablePlayerIndex);
            // stablePlayerIndex[0] = Player A
            // stablePlayerIndex[1] = Player B


            if (isMultiplayerEnabled && poseLandmarkerResult.poseLandmarks.Count >= 2)
            {
                int idA = stablePlayerIndex[0];
                int idB = stablePlayerIndex[1];

                // === 3) Compare their X positions (nose = 0) ===
                float xA = poseLandmarkerResult.poseLandmarks[idA].landmarks[0].x;
                float xB = poseLandmarkerResult.poseLandmarks[idB].landmarks[0].x;

                // === 4) Lower X = left side ===
                if (xA < xB)
                {
                    // A is left, B is right
                    players[0].SetBodyPoints(poseLandmarkerResult, idA);
                    players[1].SetBodyPoints(poseLandmarkerResult, idB);
                }
                else
                {
                    stablePlayerIndex[0] = idB;
                    stablePlayerIndex[1] = idA;
                    // B is left, A is right
                    players[0].SetBodyPoints(poseLandmarkerResult, idB);
                    players[1].SetBodyPoints(poseLandmarkerResult, idA);
                }
            }
            else
            {
                // Single player
                players[0].SetBodyPoints(poseLandmarkerResult, 0);
            }

        }


        private void OnHandLandmarkDetectionOutput(GestureRecognizerResult result, long timestamp)
        {

            bool hasHandData = result.handLandmarks != null &&
            result.handLandmarks.Count > 0;

            if (!hasHandData) return;

            lock (resultBufferLock)
            {
                result.CloneTo(ref handWriteBuffer);
                hasPendingHandResult = true;
            }
        }

        private void ProcessHandLandmarkDetectionOutput(GestureRecognizerResult result)
        {
            try
            {

                handLandmarkerResult = result;

                if (isMultiplayerEnabled == false && handLandmarkerResult.handLandmarks.Count <= 2)
                {
                    if (handLandmarkerResult.handLandmarks.Count == 1)
                    {
                        if (handLandmarkerResult.handedness[0].categories[0].categoryName == "Right")
                        {
                            players[0].SetHandPoints(handLandmarkerResult, 0, -1);
                        }
                        else if (handLandmarkerResult.handedness[0].categories[0].categoryName == "Left")
                        {
                            players[0].SetHandPoints(handLandmarkerResult, -1, 0);
                        }
                    }
                    else
                    {
                        if (handLandmarkerResult.handedness[0].categories[0].categoryName == "Right")
                        {
                            players[0].SetHandPoints(handLandmarkerResult, 1, 0);
                        }
                        else if (handLandmarkerResult.handedness[0].categories[0].categoryName == "Left")
                        {
                            players[0].SetHandPoints(handLandmarkerResult, 0, 1);
                        }
                    }
                }
                else if (isMultiplayerEnabled)
                {
                    // Ensure pose data exists before matching hands to wrists
                    if (poseLandmarkerResult.poseLandmarks == null ||
                    poseLandmarkerResult.poseLandmarks.Count == 0)
                    {
                        return;
                    }

                    int p0Left = -1, p0Right = -1;
                    int p1Left = -1, p1Right = -1;

                    for (int i = 0; i < handLandmarkerResult.handLandmarks.Count; i++)
                    {
                        // --- Hand wrist ---
                        var handWrist = handLandmarkerResult.handLandmarks[i].landmarks[0];
                        Vector2 handPos = new Vector2(handWrist.x, handWrist.y);

                        // --- Handedness ---
                        // Screen is mirrored, so "Right" maps to the visual left side and vice versa
                        bool isLeftHand =
                        handLandmarkerResult.handedness != null &&
                        handLandmarkerResult.handedness.Count > i &&
                        handLandmarkerResult.handedness[i].categories[0].categoryName == "Right";

                        float minDist = float.MaxValue;
                        int targetPlayer = -1;

                        if (isLeftHand)
                        {
                            // Compare ONLY with left wrists
                            CompareHandToWrist(players[0].body2D.leftWrist, handPos, 0, ref minDist, ref targetPlayer);
                            if (poseLandmarkerResult.poseLandmarks.Count > 1)
                            {
                                CompareHandToWrist(players[1].body2D.leftWrist, handPos, 1, ref minDist, ref targetPlayer);
                            }


                            if (targetPlayer == 0) p0Left = i;
                            else if (targetPlayer == 1) p1Left = i;
                        }
                        else
                        {
                            // Compare ONLY with right wrists
                            CompareHandToWrist(players[0].body2D.rightWrist, handPos, 0, ref minDist, ref targetPlayer);
                            if (poseLandmarkerResult.poseLandmarks.Count > 1)
                            {
                                CompareHandToWrist(players[1].body2D.rightWrist, handPos, 1, ref minDist, ref targetPlayer);
                            }

                            if (targetPlayer == 0) p0Right = i;
                            else if (targetPlayer == 1) p1Right = i;
                        }
                    }

                    // Apply results
                    players[0].SetHandPoints(handLandmarkerResult, p0Left, p0Right);
                    if (poseLandmarkerResult.poseLandmarks.Count > 1)
                        players[1].SetHandPoints(handLandmarkerResult, p1Left, p1Right);
                    else
                        players[1].SetHandPoints(handLandmarkerResult, -1, -1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to process hand landmark output: {e}");
            }

        }

        private void CompareHandToWrist(NormalizedLandmark wristPoint, Vector2 handPos, int playerIndex, ref float minDist, ref int targetPlayer)
        {
            if (wristPoint.visibility < 0.5f) return;

            Vector2 bodyPos = new Vector2(wristPoint.x, wristPoint.y);
            float dist = Vector2.Distance(bodyPos, handPos);

            if (dist < minDist)
            {
                minDist = dist;
                targetPlayer = playerIndex;
            }
        }


        private void CheckCalibration(int playerIndex)
        {
            if (Bodylink.Instance.autoRecalibrate)
            {
                Bodylink.Instance.Calibrate();
            }
        }

        private void OnPlayerOutOfScreen(int playerIndex)
        {
            Bodylink.Instance.OnPlayerOutOfScreen?.Invoke();
        }

        private void ShowImages(bool state)
        {

            background.gameObject.SetActive(state);
            ShowCamerainFullScreen(state);

            players[0].ShowImages(state);
            if (isMultiplayerEnabled)
            {
                players[1].ShowImages(state);
            }

        }

        public void SetPlayerPointAnnotations(PointListAnnotation _firstPlayerAnnotations, PointListAnnotation _secondPlayerAnnotations)
        {
            if (_firstPlayerAnnotations != null)
            {
                _playerOnePointListAnnotation = _firstPlayerAnnotations;
                players[0].SetPointListAnnotation(_firstPlayerAnnotations);
            }
            if (_secondPlayerAnnotations != null && isMultiplayerEnabled)
            {
                _playerTwoPointListAnnotation = _secondPlayerAnnotations;
                players[1].SetPointListAnnotation(_secondPlayerAnnotations);
            }

            if (isMultiplayerEnabled && isCalibratedCalled == false)
            {
                if (setLeftRightPlayerRoutine != null)
                {
                    StopCoroutine(setLeftRightPlayerRoutine);
                }

                setLeftRightPlayerRoutine = StartCoroutine(SetLeftRightPlayer());
            }

        }

        IEnumerator SetLeftRightPlayer()
        {
            float startTime = Time.realtimeSinceStartup;

            while (!isCalibratedCalled)
            {
                if (Time.realtimeSinceStartup - startTime >= setLeftRightPlayerTimeoutSeconds)
                {
                    Debug.LogWarning("[Bodylink] SetLeftRightPlayer timed out waiting for calibration to start.");
                    setLeftRightPlayerRoutine = null;
                    yield break;
                }

                yield return null;
            }

            yield return new WaitForSeconds(.1f);
            //check distance between avatar and player
            while (true)
            {
                if (Time.realtimeSinceStartup - startTime >= setLeftRightPlayerTimeoutSeconds)
                {
                    Debug.LogWarning("[Bodylink] SetLeftRightPlayer timed out before calibration completed.");
                    setLeftRightPlayerRoutine = null;
                    yield break;
                }

                float player1Dist = Vector2.Distance(_playerOnePointListAnnotation[0].transform.position, players[0].avatarImage.rectTransform.position);
                float player2Dist = Vector2.Distance(_playerOnePointListAnnotation[0].transform.position, players[1].avatarImage.rectTransform.position);

                if (player1Dist < player2Dist)
                {
                    players[0].RecalculateTrackPoints(_playerOnePointListAnnotation);
                    players[1].RecalculateTrackPoints(_playerTwoPointListAnnotation);
                }
                else
                {
                    players[0].RecalculateTrackPoints(_playerTwoPointListAnnotation);
                    players[1].RecalculateTrackPoints(_playerOnePointListAnnotation);

                }

                if (Bodylink.Instance.IsCalibrated)
                {
                    setLeftRightPlayerRoutine = null;
                    yield break;
                }

                yield return null;
            }
        }

        public void Calibrate(BodylinkCalibrationType calibrationType, Action onCalibration, float waitTime = 0)
        {
            isCalibratedCalled = true;
            ShowImages(true);
            ResetPlayerIndex();
            Bodylink.Instance.isCalibrating = true;
            // Reset UI properly before calibration
            players[0].ShowMiniCamera(false);

            players[0].Calibrate(calibrationType, () =>
            {
                CalibrationCompleted(onCalibration);

            }, waitTime);

            if (isMultiplayerEnabled)
            {
                //playerAvatars[1].ShowImages(true);
                players[1].Calibrate(calibrationType, () =>
                {
                    CalibrationCompleted(onCalibration);

                }, waitTime);
            }
        }

        private void CalibrationCompleted(Action onCalibration)
        {
            calibrationCount++;
            if (calibrationCount == Bodylink.Instance.numberOfPlayers)
            {

                ShowImages(false);
                players[0].ShowImages(false);
                if (Bodylink.Instance.showCameraFeed)
                {
                    players[0].ShowMiniCamera(true);
                }

                players[0].SetMiniCameraScreen();
                if (isMultiplayerEnabled)
                {
                    players[1].ShowImages(false);
                }
                Bodylink.Instance.isCalibrating = false;
                calibrationCount = 0;
                isCalibratedCalled = false;
                onCalibration?.Invoke();
            }
        }

        public void DisableCalibration()
        {
            players[0].DisableCalibration();
            if (isMultiplayerEnabled)
                players[1].DisableCalibration();
        }

        void ShowCamerainFullScreen(bool status)
        {
            Bodylink.Instance.cameraScreen.GetComponent<RawImage>().enabled = status;
        }

        private void ResetPlayerIndex()
        {
            if (isMultiplayerEnabled && this.gameObject.transform.GetChild(0).GetChild(0).gameObject.GetComponent<BodylinkPlayerAvatar>().playerIndex != 0)
            {
                SwitchPlayer();
            }
        }

        private void SwitchPlayer()
        {
            //Debug.Log("Player Switched");
            var temp = players[0];
            players[0] = players[1];
            players[1] = temp;
        }
    }
}
