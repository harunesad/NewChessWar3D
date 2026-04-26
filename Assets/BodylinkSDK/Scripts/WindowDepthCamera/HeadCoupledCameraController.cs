using BodylinkSDK;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

[DisallowMultipleComponent]
public class HeadCoupledCameraController : MonoBehaviour
{
    private struct TrackingSample
    {
        public Vector2 eyeCenter;
        public float bodyHeight;
        public float eyeSpan;
        public float earSpan;
        public float shoulderSpan;
        public Vector2 noseOffsetNormalized;
        public bool hasLookDirection;
    }

    [Header("Scene Setup")]
    [SerializeField] private Camera renderCamera;
    [SerializeField] private int playerIndex;

    [Header("Virtual Screen")]
    [SerializeField] private bool useDeviceDpiForScreenSize = true;
    [SerializeField] private Vector2 manualScreenSizeMeters = new Vector2(0.30f, 0.17f);
    [SerializeField] private float defaultViewerDistanceMeters = 0.55f;
    [SerializeField] private float minimumViewerDistanceMeters = 0.35f;
    [SerializeField] private float maximumViewerDistanceMeters = 1.10f;

    [Header("Tracking Camera Estimate")]
    [SerializeField] private float trackingCameraVerticalFov = 60f;
    [SerializeField] private float minimumLandmarkConfidence = 0.35f;
    [SerializeField] private bool invertHorizontal;
    [SerializeField] private bool invertVertical;

    [Header("Motion Response")]
    [SerializeField] private float horizontalSensitivity = 1f;
    [SerializeField] private float verticalSensitivity = 1f;
    [SerializeField] private float depthSensitivity = 1f;
    [SerializeField] private float maximumCameraZ = 8f;
    [SerializeField] private float fullZEffectDistanceRatio = 0.9f;
    [SerializeField] private float trackingSmoothing = 14f;
    [SerializeField] private float depthSmoothing = 12f;
    [SerializeField] private bool recenterOnFirstValidFrame = true;
    [SerializeField] private bool holdLastPoseWhenTrackingLost = true;
    [SerializeField] private bool recenterOnBodylinkCalibration = true;

    [Header("Look Rotation")]
    [SerializeField] private float lookYawSensitivity = 18f;
    [SerializeField] private float lookYawEffectMultiplier = 1.75f;
    [SerializeField] private float maximumLookYaw = 24f;
    [SerializeField] private float lookRotationSmoothing = 16f;
    [SerializeField] private bool invertLookYaw;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;
    [SerializeField] private bool logHeightDistanceRatio;

    private const float MinimumSpan = 0.0001f;

    private Vector3 currentEyeLocal;
    private Vector3 currentLookEuler;
    private float currentCameraZProgress;
    private TrackingSample neutralSample;
    private bool hasNeutralSample;
    private Vector2 lastScreenSizeMeters;
    private Vector3 originalCameraLocalPosition;
    private Quaternion originalCameraLocalRotation;
    private float originalCameraFieldOfView;
    private bool cachedOriginalPose;
    private bool bodylinkEventsHooked;

    public void Recenter()
    {
        hasNeutralSample = false;
        currentLookEuler = Vector3.zero;
        currentCameraZProgress = 0f;
    }

    private void Reset()
    {
        renderCamera = GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        EnsureCameraReference();
        CacheOriginalCameraPose();
        lastScreenSizeMeters = GetVirtualScreenSizeMeters();
        currentEyeLocal = GetDefaultEyeLocal();
        currentLookEuler = Vector3.zero;
        currentCameraZProgress = 0f;
        ApplyCameraState(currentEyeLocal, currentLookEuler, currentCameraZProgress);
    }

    private void OnEnable()
    {
        EnsureCameraReference();
        CacheOriginalCameraPose();
        lastScreenSizeMeters = GetVirtualScreenSizeMeters();
        currentEyeLocal = GetDefaultEyeLocal();
        currentLookEuler = Vector3.zero;
        currentCameraZProgress = 0f;

        if (recenterOnFirstValidFrame)
        {
            hasNeutralSample = false;
        }
    }

    private void LateUpdate()
    {
        TryHookBodylinkEvents();

        if (!EnsureCameraReference())
        {
            return;
        }

        lastScreenSizeMeters = GetVirtualScreenSizeMeters();

        Vector3 targetEyeLocal = holdLastPoseWhenTrackingLost
            ? currentEyeLocal
            : GetDefaultEyeLocal();
        Vector3 targetLookEuler = holdLastPoseWhenTrackingLost
            ? currentLookEuler
            : Vector3.zero;
        float targetCameraZProgress = holdLastPoseWhenTrackingLost
            ? currentCameraZProgress
            : 0f;

        if (TryBuildDesiredView(
            out Vector3 trackedEyeLocal,
            out Vector3 trackedLookEuler,
            out float trackedCameraZProgress))
        {
            targetEyeLocal = trackedEyeLocal;
            targetLookEuler = trackedLookEuler;
            targetCameraZProgress = trackedCameraZProgress;
        }

        currentEyeLocal = SmoothEyeLocal(currentEyeLocal, targetEyeLocal);
        currentLookEuler = SmoothLookEuler(currentLookEuler, targetLookEuler);
        currentCameraZProgress = SmoothScalar(currentCameraZProgress, targetCameraZProgress);
        ApplyCameraState(currentEyeLocal, currentLookEuler, currentCameraZProgress);
    }

    private void OnDisable()
    {
        UnhookBodylinkEvents();

        if (renderCamera == null)
        {
            return;
        }

        renderCamera.ResetProjectionMatrix();

        if (cachedOriginalPose)
        {
            renderCamera.transform.localPosition = originalCameraLocalPosition;
            renderCamera.transform.localRotation = originalCameraLocalRotation;
            renderCamera.fieldOfView = originalCameraFieldOfView;
        }
    }

    private void TryHookBodylinkEvents()
    {
        if (bodylinkEventsHooked)
        {
            return;
        }

        if (Bodylink.Instance == null)
        {
            return;
        }

        Bodylink.Instance.OnCalibrated += HandleBodylinkCalibrated;
        Bodylink.Instance.OnDisposed += HandleBodylinkDisposed;
        bodylinkEventsHooked = true;
    }

    private void UnhookBodylinkEvents()
    {
        if (!bodylinkEventsHooked)
        {
            return;
        }

        if (Bodylink.Instance != null)
        {
            Bodylink.Instance.OnCalibrated -= HandleBodylinkCalibrated;
            Bodylink.Instance.OnDisposed -= HandleBodylinkDisposed;
        }

        bodylinkEventsHooked = false;
    }

    private void HandleBodylinkCalibrated()
    {
        if (recenterOnBodylinkCalibration)
        {
            Recenter();
        }
    }

    private void HandleBodylinkDisposed()
    {
        hasNeutralSample = false;
        UnhookBodylinkEvents();
    }

    private bool EnsureCameraReference()
    {
        if (renderCamera != null)
        {
            return true;
        }

        renderCamera = GetComponentInChildren<Camera>();
        return renderCamera != null;
    }

    private void CacheOriginalCameraPose()
    {
        if (renderCamera == null || cachedOriginalPose)
        {
            return;
        }

        originalCameraLocalPosition = renderCamera.transform.localPosition;
        originalCameraLocalRotation = renderCamera.transform.localRotation;
        originalCameraFieldOfView = renderCamera.fieldOfView;
        cachedOriginalPose = true;
    }

    private Vector3 GetDefaultEyeLocal()
    {
        float nearClip = renderCamera != null ? renderCamera.nearClipPlane : 0.3f;
        float clampedDistance = Mathf.Clamp(
            defaultViewerDistanceMeters,
            Mathf.Max(minimumViewerDistanceMeters, nearClip + 0.05f),
            maximumViewerDistanceMeters);

        return new Vector3(0f, 0f, -clampedDistance);
    }

    private Vector3 SmoothEyeLocal(Vector3 currentValue, Vector3 targetValue)
    {
        if (!Application.isPlaying)
        {
            return targetValue;
        }

        float blend = GetSmoothingBlend(trackingSmoothing);
        return Vector3.Lerp(currentValue, targetValue, blend);
    }

    private Vector3 SmoothLookEuler(Vector3 currentValue, Vector3 targetValue)
    {
        if (!Application.isPlaying)
        {
            return targetValue;
        }

        float blend = GetSmoothingBlend(lookRotationSmoothing);
        return new Vector3(
            Mathf.LerpAngle(currentValue.x, targetValue.x, blend),
            Mathf.LerpAngle(currentValue.y, targetValue.y, blend),
            Mathf.LerpAngle(currentValue.z, targetValue.z, blend));
    }

    private float SmoothScalar(float currentValue, float targetValue)
    {
        if (!Application.isPlaying)
        {
            return targetValue;
        }

        float blend = GetSmoothingBlend(depthSmoothing);
        return Mathf.Lerp(currentValue, targetValue, blend);
    }

    private static float GetSmoothingBlend(float smoothing)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, smoothing) * Time.deltaTime);
    }

    private bool TryBuildDesiredView(
        out Vector3 desiredEyeLocal,
        out Vector3 desiredLookEuler,
        out float desiredCameraZProgress)
    {
        desiredEyeLocal = GetDefaultEyeLocal();
        desiredLookEuler = Vector3.zero;
        desiredCameraZProgress = 0f;

        if (!TryGetTrackingSample(out TrackingSample currentSample))
        {
            return false;
        }

        if (!hasNeutralSample)
        {
            neutralSample = currentSample;
            hasNeutralSample = true;
        }

        float viewerDistance = EstimateViewerDistance(currentSample, neutralSample, out desiredCameraZProgress);
        float aspect = GetTrackingAspect();
        float verticalFovRadians = trackingCameraVerticalFov * Mathf.Deg2Rad;
        float halfFrustumHeight = Mathf.Tan(verticalFovRadians * 0.5f) * viewerDistance;
        float halfFrustumWidth = halfFrustumHeight * aspect;

        float normalizedOffsetX = currentSample.eyeCenter.x - neutralSample.eyeCenter.x;
        float normalizedOffsetY = currentSample.eyeCenter.y - neutralSample.eyeCenter.y;

        if (invertHorizontal)
        {
            normalizedOffsetX = -normalizedOffsetX;
        }

        if (invertVertical)
        {
            normalizedOffsetY = -normalizedOffsetY;
        }

        float localX = normalizedOffsetX * (halfFrustumWidth * 2f) * horizontalSensitivity;
        float localY = normalizedOffsetY * (halfFrustumHeight * 2f) * verticalSensitivity;

        desiredEyeLocal = new Vector3(localX, localY, -viewerDistance);
        desiredLookEuler = BuildLookEuler(currentSample, desiredCameraZProgress);
        return true;
    }

    private Vector3 BuildLookEuler(TrackingSample currentSample, float cameraZProgress)
    {
        if (!currentSample.hasLookDirection || !neutralSample.hasLookDirection)
        {
            return Vector3.zero;
        }

        float yaw = (currentSample.noseOffsetNormalized.x - neutralSample.noseOffsetNormalized.x) *
            lookYawSensitivity *
            Mathf.Max(0f, lookYawEffectMultiplier);

        if (invertLookYaw)
        {
            yaw = -yaw;
        }

        yaw *= Mathf.Clamp01(cameraZProgress);
        float yawLimit = Mathf.Max(0f, maximumLookYaw);
        yaw = Mathf.Clamp(yaw, -yawLimit, yawLimit);
        return new Vector3(0f, yaw, 0f);
    }

    private float EstimateViewerDistance(
        TrackingSample currentSample,
        TrackingSample referenceSample,
        out float cameraZProgress)
    {
        cameraZProgress = 0f;
        float heightRatio = GetHeightBasedDistanceRatio(currentSample, referenceSample);
        float scaleRatio = GetCombinedScaleDistanceRatio(currentSample, referenceSample);

        if (logHeightDistanceRatio)
        {
            Debug.Log($"Depth ratios - Height: {heightRatio:F3}, Combined: {scaleRatio:F3}");
        }

        if (scaleRatio <= 0f)
        {
            scaleRatio = 1f;
        }

        float depthOffset = (scaleRatio - 1f) * defaultViewerDistanceMeters * depthSensitivity;
        float fallbackViewerDistance = ClampViewerDistance(defaultViewerDistanceMeters + depthOffset);
        cameraZProgress = GetCameraZProgressFromDistanceRatio(scaleRatio);
        return fallbackViewerDistance;
    }

    private float GetCombinedScaleDistanceRatio(TrackingSample currentSample, TrackingSample referenceSample)
    {
        float ratio = 0f;
        float totalWeight = 0f;

        AddDistanceRatio(referenceSample.bodyHeight, currentSample.bodyHeight, 0.30f, ref ratio, ref totalWeight);
        AddDistanceRatio(referenceSample.shoulderSpan, currentSample.shoulderSpan, 0.35f, ref ratio, ref totalWeight);
        AddDistanceRatio(referenceSample.earSpan, currentSample.earSpan, 0.20f, ref ratio, ref totalWeight);
        AddDistanceRatio(referenceSample.eyeSpan, currentSample.eyeSpan, 0.15f, ref ratio, ref totalWeight);

        return totalWeight > 0f ? ratio / totalWeight : 1f;
    }

    private float GetCameraZProgressFromDistanceRatio(float distanceRatio)
    {
        float fullEffectRatio = Mathf.Clamp(fullZEffectDistanceRatio, 0.01f, 0.99f);

        if (Mathf.Approximately(fullEffectRatio, 1f))
        {
            return 0f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(1f, fullEffectRatio, distanceRatio));
    }

    private static void AddDistanceRatio(float referenceSpan, float currentSpan, float weight, ref float weightedRatioSum, ref float totalWeight)
    {
        if (referenceSpan <= MinimumSpan || currentSpan <= MinimumSpan || weight <= 0f)
        {
            return;
        }

        weightedRatioSum += (referenceSpan / currentSpan) * weight;
        totalWeight += weight;
    }

    private bool TryGetTrackingSample(out TrackingSample sample)
    {
        sample = default;

        Bodylink bodylink = Bodylink.Instance;
        if (bodylink == null || !bodylink.IsInitialized || bodylink.players == null)
        {
            return false;
        }

        if (playerIndex < 0 || playerIndex >= bodylink.players.Length)
        {
            return false;
        }

        BodylinkPlayerAvatar player = bodylink.players[playerIndex];
        if (player == null || player.body2DSmoothed == null)
        {
            return false;
        }

        BodyPoints2DSmoothed body = player.body2DSmoothed;

        bool hasLeftEye = TryGetAveragePoint(body.leftEyeInner, body.leftEye, body.leftEyeOuter, out Vector2 leftEye);
        bool hasRightEye = TryGetAveragePoint(body.rightEyeInner, body.rightEye, body.rightEyeOuter, out Vector2 rightEye);
        bool hasNose = TryGetPoint(body.nose, out Vector2 nose);
        bool hasLeftEar = TryGetPoint(body.leftEar, out Vector2 leftEar);
        bool hasRightEar = TryGetPoint(body.rightEar, out Vector2 rightEar);
        bool hasLeftShoulder = TryGetPoint(body.leftShoulder, out Vector2 leftShoulder);
        bool hasRightShoulder = TryGetPoint(body.rightShoulder, out Vector2 rightShoulder);

        if (hasLeftEye && hasRightEye)
        {
            sample.eyeCenter = (leftEye + rightEye) * 0.5f;
        }
        else if (hasNose)
        {
            sample.eyeCenter = nose;
        }
        else if (hasLeftEar && hasRightEar)
        {
            sample.eyeCenter = (leftEar + rightEar) * 0.5f;
        }
        else
        {
            return false;
        }

        sample.eyeSpan = hasLeftEye && hasRightEye ? Mathf.Abs(rightEye.x - leftEye.x) : 0f;
        sample.earSpan = hasLeftEar && hasRightEar ? Mathf.Abs(rightEar.x - leftEar.x) : 0f;
        sample.shoulderSpan = hasLeftShoulder && hasRightShoulder ? Mathf.Abs(rightShoulder.x - leftShoulder.x) : 0f;
        sample.bodyHeight = bodylink.GetPlayerCurrentHeight(playerIndex);

        float faceSpan = Mathf.Max(sample.eyeSpan, sample.earSpan);
        if (hasNose && faceSpan > MinimumSpan)
        {
            sample.noseOffsetNormalized = (nose - sample.eyeCenter) / faceSpan;
            sample.hasLookDirection = true;
        }

        return true;
    }

    private bool TryGetAveragePoint(
        NormalizedLandmark first,
        NormalizedLandmark second,
        NormalizedLandmark third,
        out Vector2 averagePoint)
    {
        Vector2 sum = Vector2.zero;
        float totalWeight = 0f;

        AccumulatePoint(first, ref sum, ref totalWeight);
        AccumulatePoint(second, ref sum, ref totalWeight);
        AccumulatePoint(third, ref sum, ref totalWeight);

        if (totalWeight <= 0f)
        {
            averagePoint = Vector2.zero;
            return false;
        }

        averagePoint = sum / totalWeight;
        return true;
    }

    private void AccumulatePoint(NormalizedLandmark landmark, ref Vector2 sum, ref float totalWeight)
    {
        float confidence = GetConfidence(landmark);
        if (confidence < minimumLandmarkConfidence)
        {
            return;
        }

        sum += new Vector2(landmark.x, landmark.y) * confidence;
        totalWeight += confidence;
    }

    private bool TryGetPoint(NormalizedLandmark landmark, out Vector2 point)
    {
        float confidence = GetConfidence(landmark);
        if (confidence < minimumLandmarkConfidence)
        {
            point = Vector2.zero;
            return false;
        }

        point = new Vector2(landmark.x, landmark.y);
        return true;
    }

    private static float GetConfidence(NormalizedLandmark landmark)
    {
        float visibility = landmark.visibility.GetValueOrDefault(0f);
        float presence = landmark.presence.GetValueOrDefault(0f);
        return Mathf.Max(visibility, presence);
    }

    private float GetHeightBasedDistanceRatio(TrackingSample currentSample, TrackingSample referenceSample)
    {
        if (referenceSample.bodyHeight <= MinimumSpan || currentSample.bodyHeight <= MinimumSpan)
        {
            return -1f;
        }

        return referenceSample.bodyHeight / currentSample.bodyHeight;
    }

    private float ClampViewerDistance(float distance)
    {
        float nearClip = renderCamera != null ? renderCamera.nearClipPlane : 0.3f;
        return Mathf.Clamp(
            distance,
            Mathf.Max(minimumViewerDistanceMeters, nearClip + 0.05f),
            maximumViewerDistanceMeters);
    }

    private Vector2 GetVirtualScreenSizeMeters()
    {
        if (useDeviceDpiForScreenSize && Screen.dpi > 0f)
        {
            float widthMeters = (Screen.width / Screen.dpi) * 0.0254f;
            float heightMeters = (Screen.height / Screen.dpi) * 0.0254f;

            if (widthMeters > 0f && heightMeters > 0f)
            {
                return new Vector2(widthMeters, heightMeters);
            }
        }

        float width = Mathf.Max(0.05f, manualScreenSizeMeters.x);
        float height = Mathf.Max(0.05f, manualScreenSizeMeters.y);
        return new Vector2(width, height);
    }

    private float GetTrackingAspect()
    {
        Bodylink bodylink = Bodylink.Instance;
        Texture trackingTexture = bodylink != null && bodylink.cameraScreen != null
            ? bodylink.cameraScreen.texture
            : null;

        if (trackingTexture != null && trackingTexture.width > 0 && trackingTexture.height > 0)
        {
            return trackingTexture.width / (float)trackingTexture.height;
        }

        return Screen.height > 0 ? Screen.width / (float)Screen.height : (16f / 9f);
    }

    private void ApplyCameraState(Vector3 eyeLocal, Vector3 lookEuler, float cameraZProgress)
    {
        if (renderCamera == null)
        {
            return;
        }

        float targetFieldOfView = originalCameraFieldOfView > 0f
            ? originalCameraFieldOfView
            : renderCamera.fieldOfView;
        Vector3 cameraLocalPosition = originalCameraLocalPosition + new Vector3(eyeLocal.x, eyeLocal.y, 0f);
        float cappedMaximumCameraZ = Mathf.Max(originalCameraLocalPosition.z, maximumCameraZ);
        cameraLocalPosition.z = Mathf.Lerp(
            originalCameraLocalPosition.z,
            cappedMaximumCameraZ,
            Mathf.Clamp01(cameraZProgress));

        renderCamera.transform.localPosition = cameraLocalPosition;
        renderCamera.transform.localRotation = originalCameraLocalRotation * Quaternion.Euler(lookEuler);
        renderCamera.fieldOfView = targetFieldOfView;

        renderCamera.projectionMatrix = BuildOffAxisProjection(eyeLocal, targetFieldOfView);
    }

    private Matrix4x4 BuildOffAxisProjection(Vector3 eyeLocal, float fieldOfView)
    {
        float near = renderCamera.nearClipPlane;
        float far = renderCamera.farClipPlane;
        float distanceToViewer = Mathf.Max(near + 0.001f, -eyeLocal.z);
        float aspect = renderCamera.aspect > 0f ? renderCamera.aspect : GetTrackingAspect();
        float halfHeight = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * near;
        float halfWidth = halfHeight * aspect;

        float horizontalShift = eyeLocal.x * near / distanceToViewer;
        float verticalShift = eyeLocal.y * near / distanceToViewer;

        float left = -halfWidth - horizontalShift;
        float right = halfWidth - horizontalShift;
        float bottom = -halfHeight - verticalShift;
        float top = halfHeight - verticalShift;

        return PerspectiveOffCenter(left, right, bottom, top, near, far);
    }

    private static Matrix4x4 PerspectiveOffCenter(
        float left,
        float right,
        float bottom,
        float top,
        float near,
        float far)
    {
        float x = (2f * near) / (right - left);
        float y = (2f * near) / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(far + near) / (far - near);
        float d = -(2f * far * near) / (far - near);
        float e = -1f;

        Matrix4x4 matrix = new Matrix4x4();
        matrix[0, 0] = x;
        matrix[0, 2] = a;
        matrix[1, 1] = y;
        matrix[1, 2] = b;
        matrix[2, 2] = c;
        matrix[2, 3] = d;
        matrix[3, 2] = e;
        return matrix;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Vector2 screenSize = Application.isPlaying ? lastScreenSizeMeters : GetVirtualScreenSizeMeters();
        Vector3 center = transform.position;
        Vector3 right = transform.right * (screenSize.x * 0.5f);
        Vector3 up = transform.up * (screenSize.y * 0.5f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center - right - up, center + right - up);
        Gizmos.DrawLine(center + right - up, center + right + up);
        Gizmos.DrawLine(center + right + up, center - right + up);
        Gizmos.DrawLine(center - right + up, center - right - up);

        if (renderCamera == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center, renderCamera.transform.position);
        Gizmos.DrawSphere(renderCamera.transform.position, 0.01f);
    }
}
