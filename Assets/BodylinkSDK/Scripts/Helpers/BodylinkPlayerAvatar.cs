using System;
using System.Collections;
using System.Collections.Generic;
using BodylinkSDK;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace BodylinkSDK
{
    public class BodylinkPlayerAvatar : MonoBehaviour
    {
        public int playerIndex;
        public Image avatarImage;
        public Slider calibrationProgressSlider;
        public RawImage miniCameraView;
        public List<Image> OnScreenStaticBodyPointsList; // 0 head, 1 left hand, 2 right hand, 3 left foot, 4 right foot
        public List<NormalizedLandmark> bodyRawPoints2D { get; set; }
        public List<Landmark> bodyRawPoints3D { get; set; }

        public BodyPoints2D body2D { get; private set; }
        public BodyPoints3D body3D { get; private set; }

        public BodyPoints2DSmoothed body2DSmoothed { get; private set; }
        public BodyPoints3DSmoothed body3DSmoothed { get; private set; }

        public BodylinkHandPoints[] handPoints { get; private set; }

        public PointListAnnotation PointListAnnotation { get; private set; }
        public List<PointAnnotation> TrackingPointAnnotation { get; private set; }

        public BodyLimbCalibration2D bodyLimbCalibration2D { get; private set; }

        public int[] bodyPointsIndexes { get; private set; }


        private float waitingTime
        {
            get { return Bodylink.Instance.visibilityWaitingTime; }
        }

        private float visibilityThreshold
        {
            get { return Bodylink.Instance.visibilityThreshold; }
        }

        private float lastUpdateTime;
        private float lowVisibilityStartTime;

        private float calibrationProgress = 0f;

        private volatile bool lastFrameLowVisibility;
        private volatile bool newFrameReceived; // flag set from worker thread

        private BodylinkCalibrationType bodylinkCalibrationType;
        private bool hasCalibrated;

        private Action<int> onOutOfScreen;
        private Action<int> onCalibration;


        public void Init(Action<int> _onCalibration, Action<int> _onOutOfScreen)
        {
            onOutOfScreen = _onOutOfScreen;
            onCalibration = _onCalibration;

            bodyRawPoints2D = new();
            bodyRawPoints3D = new();


            handPoints = new BodylinkHandPoints[2];
            handPoints[0] = new BodylinkHandPoints();
            handPoints[1] = new BodylinkHandPoints();



            body2D = new BodyPoints2D(this);
            body3D = new BodyPoints3D(this);

            body2DSmoothed = new BodyPoints2DSmoothed(this);
            body3DSmoothed = new BodyPoints3DSmoothed(this);



            bodyLimbCalibration2D = new(body2D);

            miniCameraView.gameObject.SetActive(false);

        }


        public void SetBodyPoints(PoseLandmarkerResult result, int index = 0)
        {
            try
            {
                playerIndex = index;
                bodyRawPoints2D = result.poseLandmarks[playerIndex].landmarks;
                bodyRawPoints3D = result.poseWorldLandmarks[playerIndex].landmarks;


                bodyLimbCalibration2D.CalculateLimbRatio();

                if (bodyPointsIndexes == null || bodyPointsIndexes.Length == 0) return;
                bool anyLow = false;

                foreach (int idx in bodyPointsIndexes)
                {
                    if (idx < 0 || idx >= bodyRawPoints2D.Count) continue;

                    if (bodyRawPoints2D[idx].visibility < visibilityThreshold)
                    {
                        anyLow = true;
                        break;
                    }
                }

                lastFrameLowVisibility = anyLow;
                newFrameReceived = true; // ✅ mark new frame, but don’t touch Unity API
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to set body points for player {index}: {e}");
            }
        }

        public void SetHandPoints(GestureRecognizerResult result, int leftIndex = -1, int rightIndex = -1)
        {
            //Debug.Log("player Index: " + playerIndex + " Left Index: " + leftIndex + " right Index: " + rightIndex);
            if (leftIndex != -1)
            {
                handPoints[0].handLandmark = result.handLandmarks[leftIndex].landmarks;
                handPoints[0].handSide = Side.Left;
                handPoints[0].gestures = result.gestures[leftIndex];
            }
            else if (rightIndex != -1)
            {
                handPoints[1].handLandmark = result.handLandmarks[rightIndex].landmarks;
                handPoints[1].handSide = Side.Right;
                handPoints[1].gestures = result.gestures[rightIndex];
            }
            if (leftIndex == -1)
            {
                //Debug.Log("Left handMark is null");
                handPoints[0].handLandmark = null;
            }
            if (rightIndex == -1)
            {
                //Debug.Log("Right handMark is null");
                handPoints[1].handLandmark = null;
            }
        }

        private void Update()
        {
            if (!Bodylink.Instance.IsInitialized) return;
            body2DSmoothed.Update();
            body3DSmoothed.Update();
            // Don't check visibility during calibration OR auto-recalibration
            if (!Bodylink.Instance.IsCalibrated || Bodylink.Instance.isCalibrating)
                return;

            CheckCalibration();

        }

        void CheckCalibration()
        {
            float now = Time.realtimeSinceStartup;

            // If worker told us a new frame arrived → record Unity time here
            if (newFrameReceived)
            {
                lastUpdateTime = now;
                newFrameReceived = false;
            }

            // === Case 1: Low visibility sustained ===
            if (lastFrameLowVisibility)
            {
                if (lowVisibilityStartTime <= 0f)
                    lowVisibilityStartTime = now;

                if (now - lowVisibilityStartTime >= waitingTime)
                {
                    print("[Bodylink] SyncOut: landmarks visibility too low.");
                    lowVisibilityStartTime = 0f;
                    //onOutOfScreen?.Invoke(playerIndex);
                    if (Bodylink.Instance.autoRecalibrate)
                    {
                        //Debug.LogError("OnCalibration Called..."+playerIndex);
                        onCalibration?.Invoke(playerIndex);
                    }
                }
            }
            else
            {
                lowVisibilityStartTime = 0f;
            }

            // === Case 2: No updates received ===
            if (lastUpdateTime > 0f && (now - lastUpdateTime >= waitingTime))
            {
                print("[Bodylink] SyncOut: no landmark updates received.");
                lastUpdateTime = 0f;
                onOutOfScreen?.Invoke(playerIndex);
                if (Bodylink.Instance.autoRecalibrate)
                {
                    onCalibration?.Invoke(playerIndex);
                }
            }
        }
        public void ShowImages(bool state)
        {
            avatarImage.gameObject.SetActive(state);
            calibrationProgressSlider.gameObject.SetActive(state);
        }


        public void SetPointListAnnotation(PointListAnnotation _pointListAnnotation)
        {
            if (_pointListAnnotation == null && PointListAnnotation == null)
                return;

            this.PointListAnnotation = _pointListAnnotation;
        }

        public void Calibrate(BodylinkCalibrationType calibrationType, Action onCalibration, float waitTime = 0)
        {
            bodylinkCalibrationType = calibrationType;

            calibrationProgressSlider.value = 0;

            hasCalibrated = false;
            lastUpdateTime = 0;

            if (calibrationType == BodylinkCalibrationType.None)
            {
                onCalibration?.Invoke();
                return;
            }
            //ShowImages(true);
            SetAvatarPositionAndScale(calibrationType);
            StartCoroutine(Calibrating(calibrationType, () =>
            {
                onCalibration?.Invoke();
            }, waitTime));

        }


        public void RecalculateTrackPoints(PointListAnnotation _pointListAnnotation)
        {
            if (hasCalibrated) return;
            if (_pointListAnnotation == null) return;
            if (this.PointListAnnotation == _pointListAnnotation) return;
            this.PointListAnnotation = _pointListAnnotation;
            TrackingPointAnnotation = GetPointsFromAnnotation(bodylinkCalibrationType);
        }

        public void DisableCalibration()
        {
            ShowImages(false);
            //SetMiniCameraScreen();
        }

        IEnumerator Calibrating(BodylinkCalibrationType calibrationType, Action onCalibration, float waitTime)
        {
            float maxDistance = Bodylink.Instance.calibrationDistanceThreshold;
            int pointLimit = Bodylink.Instance.matchPointLimit;

            yield return new WaitForEndOfFrame();

            if (PointListAnnotation == null)
            {
                //will wait until PointlistAnnotation is found
                yield return new WaitUntil(() => PointListAnnotation != null);
            }

            TrackingPointAnnotation = GetPointsFromAnnotation(calibrationType);
            if (TrackingPointAnnotation != null && TrackingPointAnnotation.Count > 0)
            {
                while (true)
                {
                    int greenCount = 0;
                    int activePoints = 0;
                    bool anyMismatch = false;

                    // Loop over tracked points
                    for (int i = 0; i < TrackingPointAnnotation.Count; i++)
                    {
                        if (i >= OnScreenStaticBodyPointsList.Count) break;

                        if (TrackingPointAnnotation[i] != null)
                        {
                            activePoints++;
                            OnScreenStaticBodyPointsList[i].gameObject.SetActive(true);

                            float dist = Vector2.Distance(
                                OnScreenStaticBodyPointsList[i].rectTransform.position,
                                TrackingPointAnnotation[i].transform.position
                            );

                            if (dist < maxDistance)
                            {
                                OnScreenStaticBodyPointsList[i].color = Color.green;
                                greenCount++;
                            }
                            else
                            {
                                OnScreenStaticBodyPointsList[i].color = Color.white;
                                anyMismatch = true;
                            }
                        }
                        else
                        {
                            OnScreenStaticBodyPointsList[i].gameObject.SetActive(false);
                        }
                    }

                    bool allCalibrated;
                    if (calibrationType != BodylinkCalibrationType.Head && pointLimit > 0 && TrackingPointAnnotation.Count > pointLimit)
                    {
                        // Only require a limited number of points to be matched.
                        allCalibrated = greenCount >= pointLimit;
                    }
                    else
                    {
                        // Fall back to requiring every active point to match.
                        allCalibrated = !anyMismatch && activePoints > 0 && greenCount == activePoints;
                    }

                    // Update progress based on calibration state
                    if (allCalibrated)
                    {
                        // increase progress toward 1
                        calibrationProgress += Time.deltaTime / waitTime;
                        calibrationProgress = Mathf.Clamp01(calibrationProgress);
                    }
                    else
                    {
                        // decrease progress toward 0
                        calibrationProgress -= Time.deltaTime / waitTime;
                        calibrationProgress = Mathf.Clamp01(calibrationProgress);
                    }

                    calibrationProgressSlider.value = calibrationProgress;

                    // Calibration complete
                    if (Mathf.Approximately(calibrationProgress, 1f))
                    {
                        //Debug.Log("[Bodylink] Calibration successful! "+playerIndex);
                        hasCalibrated = true;
                        onCalibration?.Invoke();
                        yield break;
                    }

                    yield return null; // next frame
                }
            }
            else
            {
                Debug.LogError("[Bodylink] No point tracked ");
            }
        }

        List<PointAnnotation> GetPointsFromAnnotation(BodylinkCalibrationType calibrationType)
        {
            List<PointAnnotation> points = new List<PointAnnotation>();

            if (calibrationType == BodylinkCalibrationType.FullBody)
            {
                points.Add(PointListAnnotation[0]);  // Head
                points.Add(PointListAnnotation[15]); // right Hand
                points.Add(PointListAnnotation[16]); // left Hand
                points.Add(PointListAnnotation[27]); // right Foot
                points.Add(PointListAnnotation[28]); // left Foot
                bodyPointsIndexes = new int[] { 0, 15, 16, 27, 28 };
            }
            else if (calibrationType == BodylinkCalibrationType.Head)
            {
                points.Add(PointListAnnotation[0]);  // Head
                points.Add(null); // no right Hand
                points.Add(null); // no left Hand
                points.Add(null); // no right Foot
                points.Add(null); // no left Foot
                bodyPointsIndexes = new int[] { 0 };

            }
            else if (calibrationType == BodylinkCalibrationType.UpperBody)
            {
                points.Add(PointListAnnotation[0]);  // Head
                points.Add(PointListAnnotation[15]); // right Hand
                points.Add(PointListAnnotation[16]); // left Hand
                points.Add(null); // right Foot
                points.Add(null); // left Foot
                bodyPointsIndexes = new int[] { 0, 15, 16 };

            }
            else if (calibrationType == BodylinkCalibrationType.Feet)
            {
                points.Add(PointListAnnotation[0]);  // Head
                points.Add(null); // no right Hand
                points.Add(null); // no left Hand
                points.Add(PointListAnnotation[27]); // right Foot
                points.Add(PointListAnnotation[28]); // left Foot
                bodyPointsIndexes = new int[] { 0, 27, 28 };

            }

            return points;

        }
        void SetAvatarPositionAndScale(BodylinkCalibrationType calibrationType)
        {
            if (calibrationType == BodylinkCalibrationType.Head)
            {
                avatarImage.transform.localPosition = new Vector3(0, -2274, 0);
                avatarImage.transform.localScale = new Vector3(4.5f, 4.5f, 1);

                foreach (Image g in OnScreenStaticBodyPointsList)
                {
                    g.transform.localScale = new Vector3(0.4f, 0.4f, 1);
                }
            }
            else if (calibrationType == BodylinkCalibrationType.UpperBody)
            {
                avatarImage.transform.localPosition = new Vector3(0, -1035, 0);
                avatarImage.transform.localScale = new Vector3(2.4f, 2.4f, 1);

                foreach (Image g in OnScreenStaticBodyPointsList)
                {
                    g.transform.localScale = new Vector3(0.6f, 0.6f, 1);
                }
            }
            else
            {
                avatarImage.transform.localPosition = new Vector3(0, 0, 0);
                avatarImage.transform.localScale = new Vector3(1f, 1f, 1);

                foreach (Image g in OnScreenStaticBodyPointsList)
                {
                    g.transform.localScale = new Vector3(1, 1, 1);
                }

            }
        }

        public void SetMiniCameraScreen()
        {
            // Assign texture
            miniCameraView.texture = Bodylink.Instance.cameraScreen.texture;
            miniCameraView.SetNativeSize();

        }
        public void ShowMiniCamera(bool show)
        {
            miniCameraView.gameObject.SetActive(show);
        }
        public void SetMiniCameraScale(float scale)
        {
            miniCameraView.transform.localScale = new Vector3(-scale, scale, 1);
        }

    }

    public class BodylinkHandPoints
    {
        public Side handSide;
        public List<NormalizedLandmark> handLandmark;
        public List<Landmark> handWorldLandmark;
        //public Classifications handedness;
        public Classifications gestures;

        public BodylinkHandPoints()
        {
            handLandmark = new();
            handWorldLandmark = new();
            //handedness = new();
            gestures = new();
        }
    }
}
