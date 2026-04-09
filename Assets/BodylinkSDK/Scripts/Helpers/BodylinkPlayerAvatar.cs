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
        private const float MinimumCalibrationHoldTime = 0.25f;
        private const float ProgressLossMultiplier = 1.5f;
        private const float StabilityAbsoluteTolerance = 0.02f;
        private const float StabilityRelativeTolerance = 0.08f;
        private const float TPoseHandHeightTolerance = 0.12f;
        private const float TPoseElbowHeightTolerance = 0.14f;
        private const float TPoseArmExtensionThreshold = 0.12f;
        private const float DefaultPoseArmDropThreshold = 0.05f;
        private const float DefaultPoseHandHorizontalTolerance = 0.20f;
        private const float UprightPoseThreshold = 0.05f;
        private static readonly int[] AutoPoseMovingPointIndexes = { 0, 15, 16, 27, 28 };

        private static readonly int[] HeadVisibilityIndexes = { 0 };
        private static readonly int[] FullBodyVisibilityIndexes = { 0, 11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28 };
        private static readonly int[] AutoPoseVisibilityIndexes = { 0, 15, 16, 27, 28, 11, 12, 13, 14, 23, 24, 25, 26 };

        public int playerIndex;
        public Image avatarImage;
        public Slider calibrationProgressSlider;
        public Text calibrationInstructionText;
        public RawImage miniCameraView;
        public Image tPoseImage;
        public List<Image> OnScreenStaticBodyPointsList; // 0 head, 1 left hand, 2 right hand, 3 left foot, 4 right foot
        public List<Image> OnScreenMovingBodyPointsList; // 0 head, 1 right hand, 2 left hand, 3 right foot, 4 left foot
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
        private BodylinkCalibrationMode currentCalibrationMode;
        private bool hasCalibrated;
        private Coroutine calibrationRoutine;

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
            EnsureCalibrationInstructionText();
            HideCalibrationTargets();

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

            if (rightIndex != -1)
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
                    //print("[Bodylink] SyncOut: landmarks visibility too low.");
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
        public void ShowImages(bool state, bool showAvatarOverlay = true)
        {
            avatarImage.gameObject.SetActive(state && showAvatarOverlay);
            SetTPoseImageVisible(state && currentCalibrationMode == BodylinkCalibrationMode.T_or_Idle_Pose_Detection);
            calibrationProgressSlider.gameObject.SetActive(state);
            SetCalibrationInstructionVisible(state);

            if (!state)
            {
                HideCalibrationTargets();
            }
        }


        public void SetPointListAnnotation(PointListAnnotation _pointListAnnotation)
        {
            if (_pointListAnnotation == null && PointListAnnotation == null)
                return;

            this.PointListAnnotation = _pointListAnnotation;
        }

        public void Calibrate(BodylinkCalibrationMode calibrationMode, BodylinkCalibrationType calibrationType, Action onCalibration, float waitTime = 0)
        {
            currentCalibrationMode = calibrationMode;
            bodylinkCalibrationType = GetEffectiveCalibrationType(calibrationMode, calibrationType);

            calibrationProgress = 0f;
            calibrationProgressSlider.value = 0f;

            hasCalibrated = false;
            lastUpdateTime = 0f;
            lowVisibilityStartTime = 0f;
            bodyLimbCalibration2D.ResetCalibration();

            if (calibrationRoutine != null)
            {
                StopCoroutine(calibrationRoutine);
                calibrationRoutine = null;
            }

            TrackingPointAnnotation = null;
            HideCalibrationTargets();
            ShowImages(true, calibrationMode == BodylinkCalibrationMode.Target_Points_Match);
            ConfigureTrackingIndexes(calibrationMode, bodylinkCalibrationType);

            if (calibrationMode == BodylinkCalibrationMode.Target_Points_Match &&
                bodylinkCalibrationType == BodylinkCalibrationType.None)
            {
                CompleteCalibration(onCalibration, null, true);
                return;
            }

            if (calibrationMode == BodylinkCalibrationMode.Target_Points_Match)
            {
                SetAvatarPositionAndScale(bodylinkCalibrationType);
            }

            calibrationRoutine = StartCoroutine(Calibrating(calibrationMode, bodylinkCalibrationType, onCalibration, waitTime));
        }


        public void RecalculateTrackPoints(PointListAnnotation _pointListAnnotation)
        {
            if (currentCalibrationMode != BodylinkCalibrationMode.Target_Points_Match) return;
            if (hasCalibrated) return;
            if (_pointListAnnotation == null) return;
            if (this.PointListAnnotation == _pointListAnnotation) return;
            this.PointListAnnotation = _pointListAnnotation;
            TrackingPointAnnotation = GetPointsFromAnnotation(bodylinkCalibrationType);
        }

        public void DisableCalibration()
        {
            if (calibrationRoutine != null)
            {
                StopCoroutine(calibrationRoutine);
                calibrationRoutine = null;
            }

            HideCalibrationTargets();
            ShowImages(false, false);
        }

        IEnumerator Calibrating(BodylinkCalibrationMode calibrationMode, BodylinkCalibrationType calibrationType, Action onCalibration, float waitTime)
        {
            switch (calibrationMode)
            {
                case BodylinkCalibrationMode.Free_Points_Position:
                    yield return AutoPoseCalibrating(onCalibration, waitTime);
                    break;
                case BodylinkCalibrationMode.T_or_Idle_Pose_Detection:
                    yield return TPoseCalibrating(onCalibration, waitTime);
                    break;
                case BodylinkCalibrationMode.Continuous_Auto:
                    yield return ContinuousCalibrating(onCalibration, waitTime);
                    break;
                case BodylinkCalibrationMode.Target_Points_Match:
                default:
                    yield return MultiPoseCalibrating(calibrationType, onCalibration, waitTime);
                    break;
            }
        }

        IEnumerator MultiPoseCalibrating(BodylinkCalibrationType calibrationType, Action onCalibration, float waitTime)
        {
            float maxDistance = Bodylink.Instance.calibrationDistanceThreshold;
            int pointLimit = Bodylink.Instance.matchPointLimit;
            float duration = GetCalibrationDuration(waitTime);

            yield return new WaitForEndOfFrame();

            if (PointListAnnotation == null)
            {
                yield return new WaitUntil(() => PointListAnnotation != null);
            }

            TrackingPointAnnotation = GetPointsFromAnnotation(calibrationType);
            if (TrackingPointAnnotation == null || TrackingPointAnnotation.Count == 0)
            {
                Debug.LogError("[Bodylink] No points tracked for multipose calibration.");
                calibrationRoutine = null;
                yield break;
            }

            while (true)
            {
                int greenCount = 0;
                int activePoints = 0;
                bool anyMismatch = false;

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
                    allCalibrated = greenCount >= pointLimit;
                }
                else
                {
                    allCalibrated = !anyMismatch && activePoints > 0 && greenCount == activePoints;
                }

                UpdateCalibrationProgress(allCalibrated, duration);

                if (Mathf.Approximately(calibrationProgress, 1f) && CompleteCalibration(onCalibration))
                {
                    yield break;
                }

                yield return null;
            }
        }

        IEnumerator AutoPoseCalibrating(Action onCalibration, float waitTime)
        {
            float duration = GetCalibrationDuration(waitTime);
            int requiredVisiblePoints = GetRequiredVisiblePointCount(bodylinkCalibrationType, Bodylink.Instance.matchPointLimit);

            while (true)
            {
                int visiblePoints = UpdateAutoPoseCalibrationTargets(bodylinkCalibrationType);
                bool ready = requiredVisiblePoints == 0 || visiblePoints >= requiredVisiblePoints;

                UpdateCalibrationProgress(ready, duration);

                if (ready && Mathf.Approximately(calibrationProgress, 1f))
                {
                    if (TryGetAutoPoseMeasurement(out BodyCalibrationData2D measurement))
                    {
                        if (CompleteCalibration(onCalibration, measurement))
                        {
                            yield break;
                        }
                    }
                    else if (CompleteCalibration(onCalibration, null, true))
                    {
                        yield break;
                    }
                }

                yield return null;
            }
        }

        IEnumerator TPoseCalibrating(Action onCalibration, float waitTime)
        {
            float duration = GetCalibrationDuration(waitTime);
            BodyCalibrationData2D previousMeasurement = null;

            while (true)
            {
                bool validMeasurement = TryGetTPoseMeasurement(out BodyCalibrationData2D measurement);
                bool ready = validMeasurement &&
                             (previousMeasurement == null || IsMeasurementStable(previousMeasurement, measurement));

                UpdateCalibrationProgress(ready, duration);

                if (ready)
                {
                    previousMeasurement = measurement;
                    if (Mathf.Approximately(calibrationProgress, 1f) &&
                        CompleteCalibration(onCalibration, measurement))
                    {
                        yield break;
                    }
                }
                else if (validMeasurement)
                {
                    previousMeasurement = measurement;
                }

                yield return null;
            }
        }

        IEnumerator ContinuousCalibrating(Action onCalibration, float waitTime)
        {
            float duration = GetCalibrationDuration(waitTime);

            while (true)
            {
                bool headDetected = IsHeadDetected();
                UpdateCalibrationProgress(headDetected, duration);

                if (headDetected && Mathf.Approximately(calibrationProgress, 1f))
                {
                    if (bodyLimbCalibration2D.TryGetCurrentMeasurement(out BodyCalibrationData2D measurement))
                    {
                        if (CompleteCalibration(onCalibration, measurement))
                        {
                            yield break;
                        }
                    }
                    else if (CompleteCalibration(onCalibration, null, true))
                    {
                        yield break;
                    }
                }

                yield return null;
            }
        }

        bool IsHeadDetected()
        {
            if (bodyRawPoints2D == null || bodyRawPoints2D.Count <= 0)
            {
                return false;
            }

            NormalizedLandmark head = bodyRawPoints2D[HeadVisibilityIndexes[0]];
            if (head == null)
            {
                return false;
            }

            return head.visibility.GetValueOrDefault() > 0f ||
                   head.presence.GetValueOrDefault() > 0f;
        }

        bool TryGetAutoPoseMeasurement(out BodyCalibrationData2D measurement)
        {
            measurement = null;
            if (!AreLandmarksVisible(AutoPoseVisibilityIndexes))
            {
                return false;
            }

            return bodyLimbCalibration2D.TryGetCurrentMeasurement(out measurement);
        }

        bool TryGetTPoseMeasurement(out BodyCalibrationData2D measurement)
        {
            measurement = null;
            if (!TryGetFullBodyMeasurement(out BodyCalibrationData2D fullBodyMeasurement))
            {
                return false;
            }

            measurement = fullBodyMeasurement;
            return IsTPose() || IsDefaultPose();
        }

        bool TryGetFullBodyMeasurement(out BodyCalibrationData2D measurement)
        {
            measurement = null;
            if (!AreLandmarksVisible(FullBodyVisibilityIndexes))
            {
                return false;
            }

            return bodyLimbCalibration2D.TryGetCurrentMeasurement(out measurement);
        }

        bool AreLandmarksVisible(IReadOnlyList<int> indexes)
        {
            if (bodyRawPoints2D == null || bodyRawPoints2D.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < indexes.Count; i++)
            {
                int index = indexes[i];
                if (index < 0 || index >= bodyRawPoints2D.Count)
                {
                    return false;
                }

                NormalizedLandmark point = bodyRawPoints2D[index];
                if (point == null || point.visibility.GetValueOrDefault() < visibilityThreshold)
                {
                    return false;
                }
            }

            return true;
        }

        int UpdateAutoPoseCalibrationTargets(BodylinkCalibrationType calibrationType)
        {
            if (OnScreenMovingBodyPointsList == null || OnScreenMovingBodyPointsList.Count == 0)
            {
                return 0;
            }

            int visiblePoints = 0;
            int[] activeIndexes = GetCalibrationPointIndexes(calibrationType);

            for (int i = 0; i < OnScreenMovingBodyPointsList.Count; i++)
            {
                Image pointImage = OnScreenMovingBodyPointsList[i];
                if (pointImage == null)
                {
                    continue;
                }

                int landmarkIndex = i < AutoPoseMovingPointIndexes.Length
                    ? AutoPoseMovingPointIndexes[i]
                    : -1;

                if (landmarkIndex < 0 ||
                    Array.IndexOf(activeIndexes, landmarkIndex) < 0 ||
                    !TryGetVisibleLandmarkScreenPoint(AutoPoseMovingPointIndexes[i], out Vector2 screenPoint))
                {
                    pointImage.gameObject.SetActive(false);
                    continue;
                }

                visiblePoints++;
                pointImage.color = Color.green;
                pointImage.gameObject.SetActive(true);
                SetImagePosition(pointImage.rectTransform, screenPoint);
            }

            return visiblePoints;
        }

        bool TryGetVisibleLandmarkScreenPoint(int landmarkIndex, out Vector2 screenPoint)
        {
            screenPoint = Vector2.zero;
            if (bodyRawPoints2D == null || landmarkIndex < 0 || landmarkIndex >= bodyRawPoints2D.Count)
            {
                return false;
            }

            NormalizedLandmark point = body2D[landmarkIndex];
            if (point.visibility.GetValueOrDefault() < visibilityThreshold)
            {
                return false;
            }

            screenPoint = new Vector2(point.x * UnityEngine.Screen.width, point.y * UnityEngine.Screen.height);
            return true;
        }

        void SetImagePosition(RectTransform targetRect, Vector2 screenPoint)
        {
            if (targetRect == null)
            {
                return;
            }

            RectTransform parentRect = targetRect.parent as RectTransform;
            if (parentRect == null)
            {
                targetRect.position = screenPoint;
                return;
            }

            Canvas canvas = targetRect.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, canvasCamera, out Vector2 localPoint))
            {
                targetRect.anchoredPosition = localPoint;
                return;
            }

            targetRect.position = screenPoint;
        }

        bool IsTPose()
        {
            float shoulderMidY = (body2D.leftShoulder.y + body2D.rightShoulder.y) * 0.5f;

            bool wristsNearShoulderHeight =
                Mathf.Abs(body2D.leftWrist.y - shoulderMidY) <= TPoseHandHeightTolerance &&
                Mathf.Abs(body2D.rightWrist.y - shoulderMidY) <= TPoseHandHeightTolerance;

            bool elbowsNearShoulderHeight =
                Mathf.Abs(body2D.leftElbow.y - shoulderMidY) <= TPoseElbowHeightTolerance &&
                Mathf.Abs(body2D.rightElbow.y - shoulderMidY) <= TPoseElbowHeightTolerance;

            bool armsExtended =
                Mathf.Abs(body2D.leftWrist.x - body2D.leftShoulder.x) >= TPoseArmExtensionThreshold &&
                Mathf.Abs(body2D.rightWrist.x - body2D.rightShoulder.x) >= TPoseArmExtensionThreshold;

            return wristsNearShoulderHeight && elbowsNearShoulderHeight && armsExtended;
        }

        bool IsDefaultPose()
        {
            float shoulderMidY = (body2D.leftShoulder.y + body2D.rightShoulder.y) * 0.5f;
            float hipMidY = (body2D.leftHip.y + body2D.rightHip.y) * 0.5f;

            bool upright = shoulderMidY > hipMidY + UprightPoseThreshold;
            bool armsDown =
                body2D.leftWrist.y < body2D.leftShoulder.y - DefaultPoseArmDropThreshold &&
                body2D.rightWrist.y < body2D.rightShoulder.y - DefaultPoseArmDropThreshold;

            bool handsNearTorso =
                Mathf.Abs(body2D.leftWrist.x - body2D.leftHip.x) <= DefaultPoseHandHorizontalTolerance &&
                Mathf.Abs(body2D.rightWrist.x - body2D.rightHip.x) <= DefaultPoseHandHorizontalTolerance;

            return upright && armsDown && handsNearTorso;
        }

        bool IsMeasurementStable(BodyCalibrationData2D previous, BodyCalibrationData2D current)
        {
            if (previous == null || current == null)
            {
                return false;
            }

            return IsWithinTolerance(previous.height, current.height) &&
                   IsWithinTolerance(previous.armLength, current.armLength) &&
                   IsWithinTolerance(previous.legLength, current.legLength) &&
                   IsWithinTolerance(previous.torsoRatio, current.torsoRatio);
        }

        bool IsWithinTolerance(float previous, float current)
        {
            float allowedDelta = Mathf.Max(StabilityAbsoluteTolerance, Mathf.Abs(previous) * StabilityRelativeTolerance);
            return Mathf.Abs(previous - current) <= allowedDelta;
        }

        void UpdateCalibrationProgress(bool progressForward, float duration)
        {
            float delta = Time.deltaTime / duration;
            if (progressForward)
            {
                calibrationProgress = Mathf.Clamp01(calibrationProgress + delta);
            }
            else
            {
                calibrationProgress = Mathf.Clamp01(calibrationProgress - (delta * ProgressLossMultiplier));
            }

            calibrationProgressSlider.value = calibrationProgress;
        }

        bool CompleteCalibration(Action onCalibration, BodyCalibrationData2D measurement = null, bool allowMissingMeasurement = false)
        {
            if (measurement != null)
            {
                bodyLimbCalibration2D.SetCalibrationData(measurement);
            }
            else if (!allowMissingMeasurement && !bodyLimbCalibration2D.CaptureCurrentMeasurement())
            {
                return false;
            }

            calibrationProgress = 1f;
            calibrationProgressSlider.value = 1f;
            hasCalibrated = true;
            HideCalibrationTargets();
            calibrationRoutine = null;
            onCalibration?.Invoke();
            return true;
        }

        void ConfigureTrackingIndexes(BodylinkCalibrationMode calibrationMode, BodylinkCalibrationType calibrationType)
        {
            if (calibrationMode == BodylinkCalibrationMode.Target_Points_Match ||
                calibrationMode == BodylinkCalibrationMode.Free_Points_Position)
            {
                bodyPointsIndexes = GetCalibrationPointIndexes(calibrationType);
                return;
            }

            bodyPointsIndexes = new int[] { 0, 15, 16, 27, 28 };
        }

        void HideCalibrationTargets()
        {
            SetTPoseImageVisible(false);

            if (OnScreenStaticBodyPointsList == null)
            {
                HideMovingCalibrationTargets();
                return;
            }

            foreach (Image point in OnScreenStaticBodyPointsList)
            {
                if (point == null)
                {
                    continue;
                }

                point.color = Color.white;
                point.gameObject.SetActive(false);
            }

            HideMovingCalibrationTargets();
        }

        void SetTPoseImageVisible(bool visible)
        {
            if (tPoseImage == null)
            {
                return;
            }

            tPoseImage.gameObject.SetActive(visible);
        }

        void HideMovingCalibrationTargets()
        {
            if (OnScreenMovingBodyPointsList == null)
            {
                return;
            }

            foreach (Image point in OnScreenMovingBodyPointsList)
            {
                if (point == null)
                {
                    continue;
                }

                point.color = Color.white;
                point.gameObject.SetActive(false);
            }
        }

        void EnsureCalibrationInstructionText()
        {
            if (calibrationInstructionText != null)
            {
                return;
            }

            Transform parent = calibrationProgressSlider != null
                ? calibrationProgressSlider.transform.parent
                : transform;

            GameObject textObject = new GameObject("CalibrationInstructionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 140f);
            rectTransform.sizeDelta = new Vector2(480f, 60f);

            calibrationInstructionText = textObject.GetComponent<Text>();
            calibrationInstructionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            calibrationInstructionText.fontSize = 30;
            calibrationInstructionText.alignment = TextAnchor.MiddleCenter;
            calibrationInstructionText.color = Color.white;
            calibrationInstructionText.raycastTarget = false;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);

            textObject.SetActive(false);
        }

        void SetCalibrationInstructionVisible(bool visible)
        {
            if (!visible && calibrationInstructionText == null)
            {
                return;
            }

            EnsureCalibrationInstructionText();
            if (calibrationInstructionText == null)
            {
                return;
            }

            calibrationInstructionText.text = visible ? GetCalibrationInstruction(currentCalibrationMode) : string.Empty;
            calibrationInstructionText.gameObject.SetActive(visible);
        }

        static string GetCalibrationInstruction(BodylinkCalibrationMode calibrationMode)
        {
            switch (calibrationMode)
            {
                case BodylinkCalibrationMode.Free_Points_Position:
                    return "Stand in view";
                case BodylinkCalibrationMode.T_or_Idle_Pose_Detection:
                    return "Stand T pose or Default state";
                case BodylinkCalibrationMode.Continuous_Auto:
                    return "Keep head visible";
                case BodylinkCalibrationMode.Target_Points_Match:
                default:
                    return "Align with dots";
            }
        }

        static float GetCalibrationDuration(float waitTime)
        {
            return Mathf.Max(waitTime, MinimumCalibrationHoldTime);
        }

        static BodylinkCalibrationType GetEffectiveCalibrationType(BodylinkCalibrationMode calibrationMode, BodylinkCalibrationType requestedType)
        {
            return calibrationMode == BodylinkCalibrationMode.Target_Points_Match ||
                   calibrationMode == BodylinkCalibrationMode.Free_Points_Position
                ? requestedType
                : BodylinkCalibrationType.FullBody;
        }

        static int[] GetCalibrationPointIndexes(BodylinkCalibrationType calibrationType)
        {
            switch (calibrationType)
            {
                case BodylinkCalibrationType.FullBody:
                    return new int[] { 0, 15, 16, 27, 28 };
                case BodylinkCalibrationType.Head:
                    return new int[] { 0 };
                case BodylinkCalibrationType.UpperBody:
                    return new int[] { 0, 15, 16 };
                case BodylinkCalibrationType.Feet:
                    return new int[] { 0, 27, 28 };
                case BodylinkCalibrationType.None:
                default:
                    return Array.Empty<int>();
            }
        }

        static int GetRequiredVisiblePointCount(BodylinkCalibrationType calibrationType, int matchPointLimit)
        {
            int trackedPointCount = GetCalibrationPointIndexes(calibrationType).Length;
            if (trackedPointCount <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(matchPointLimit, 1, trackedPointCount);
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
