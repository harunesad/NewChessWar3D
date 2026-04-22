using UnityEngine;
using System;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using System.Collections;
using UnityEngine.Events;
using Mediapipe.Unity.Sample;
using System.Collections.Generic;
using Mediapipe.Unity;
using UnityEngine.SceneManagement;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using UnityEngine.Serialization;
using System.IO;
using System.Linq;

namespace BodylinkSDK
{
    public enum BodylinkInputStreamType
    {
        WebCam = 0,
        Video = 1
    }

    public class Bodylink : MonoBehaviour
    {
        // === Singleton Instance ===
        public static Bodylink Instance { get; private set; }

        [Header("Start Settings")]
        [Tooltip("If enabled, Bodylink will initialize on Start().")]
        [SerializeField] private bool initializeOnStart = true;

        [Tooltip("If enabled, Bodylink will calibrate on Start() (after initialization).")]
        [SerializeField] private bool calibrateOnStart = true;
        [Tooltip("If enabled, Bodylink will calibrate and show UI when user is out of screen")]
        [FormerlySerializedAs("autoRecalibrate")]
        [SerializeField] private bool _autoRecalibrate = true;
        [SerializeField] private bool resumeOnSceneChange = true;
        [SerializeField] private float calibrationWaitTime = 2;
        [FormerlySerializedAs("visibilityThreshold")]
        [SerializeField] private float _visibilityThreshold = 0.5f;
        [FormerlySerializedAs("visibilityWaitingTime")]
        [SerializeField] private float _visibilityWaitingTime = 1f; // seconds
        [FormerlySerializedAs("calibrationDistanceThreshold")]
        [SerializeField] private float _calibrationDistanceThreshold = 2;
        [SerializeField] private bool showSkeleton = false;
        [FormerlySerializedAs("showCameraFeed")]
        [SerializeField] private bool _showCameraFeed = false;
        [FormerlySerializedAs("cam")]
        [SerializeField] private Camera _cam;
        [Header("Input Stream")]
        [SerializeField] private BodylinkInputStreamType _inputStream = BodylinkInputStreamType.WebCam;
        [SerializeField] private UnityEngine.Object _selectedVideoAsset;
        [SerializeField, HideInInspector] private string _selectedVideoFile = string.Empty;
        [SerializeField] private bool _playVideoInLoop = true;
        private const float referenceHeight = 1.65f; // average height for normalization
        private static readonly string[] SupportedVideoExtensions = { ".mp4", ".mov", ".m4v", ".avi", ".mpeg", ".mpg", ".webm" };

        [Range(1, 2)]
        [FormerlySerializedAs("numberOfPlayers")]
        [SerializeField] private int _numberOfPlayers = 1;

        public bool isCalibrating { get; set; }

        [FormerlySerializedAs("Calibration Mode")]
        [SerializeField] private BodylinkCalibrationMode calibrationMode;


        [FormerlySerializedAs("calibrationType")]
        [SerializeField] private BodylinkCalibrationType _calibrationType;

        [Min(1)]
        [FormerlySerializedAs("matchPointLimit")]
        [SerializeField] private int _matchPointLimit = 1;

        public bool autoRecalibrate
        {
            get => _autoRecalibrate;
            set => _autoRecalibrate = value;
        }

        public float visibilityThreshold
        {
            get => _visibilityThreshold;
            set => _visibilityThreshold = value;
        }

        public float visibilityWaitingTime
        {
            get => _visibilityWaitingTime;
            set => _visibilityWaitingTime = value;
        }

        public float calibrationDistanceThreshold
        {
            get => _calibrationDistanceThreshold;
            set => _calibrationDistanceThreshold = value;
        }

        public bool showCameraFeed
        {
            get => _showCameraFeed;
            set => _showCameraFeed = value;
        }

        public Camera cam
        {
            get => _cam;
            set => _cam = value;
        }

        public BodylinkInputStreamType inputStream
        {
            get => _inputStream;
            set => _inputStream = value;
        }

        public string selectedVideoFile
        {
            get => _selectedVideoFile;
            set => _selectedVideoFile = value;
        }

        public UnityEngine.Object selectedVideoAsset
        {
            get => _selectedVideoAsset;
            set => _selectedVideoAsset = value;
        }

        public bool playVideoInLoop
        {
            get => _playVideoInLoop;
            set => _playVideoInLoop = value;
        }

        public string videoRecordingsFolderPath => Path.Combine(Application.dataPath, "Videos");

        public int numberOfPlayers
        {
            get => _numberOfPlayers;
            set => _numberOfPlayers = value;
        }

        public BodylinkCalibrationType calibrationType
        {
            get => _calibrationType;
            set => _calibrationType = value;
        }

        public BodylinkCalibrationMode selectedCalibrationMode
        {
            get => calibrationMode;
            set => calibrationMode = value;
        }

        public int matchPointLimit
        {
            get => _matchPointLimit;
            set => _matchPointLimit = value;
        }

        // === Events ===
        /// <summary> Fired when Bodylink has been initialized. </summary>
        public Action OnInitialized;

        /// <summary> Fired when Bodylink has been calibrated. </summary>
        public Action OnCalibrated;
        public Action OnDisposed;
        public Action OnPlayerOutOfScreen;



        // === Public state flags ===
        public bool IsInitialized { get; private set; } = false;
        public bool IsCalibrated { get; private set; } = false;
        public string[] imageSourcesNames
        {
            get
            {
                return ImageSourceProvider.ImageSource.sourceCandidateNames;
            }
        }
        public ImageSource imageSource
        {
            get
            {
                return ImageSourceProvider.ImageSource;
            }
        }

        // Runtime references
        public PoseLandmarkerRunner PoseLandmarkerRunnerInstance { get; private set; }
        public HandGestureRunner handGestureRunnerInstance { get; private set; }
        public Mediapipe.Unity.Screen cameraScreen { get; private set; }
        public BodylinkAvatar bodylinkAvatar { get; private set; }
        public BodylinkPlayerAvatar[] players { get; private set; }
        public BodylinkSkeletonVisualizer[] skeletonVisualizers { get; private set; }
        private BodylinkEvents cachedInputEvents;

        private PoseLandmarkDetectionConfig poseConfig; //used to change no of players
        private GestureRecognizerConfig handConfig;
        private Coroutine inputStreamApplyRoutine;
        private Coroutine startupCalibrationRoutine;
        private BodylinkCalibrationMode startupCalibrationMode;
        private BodylinkCalibrationType startupCalibrationType;
        private int startupMatchPointLimit;
        private float startupCalibrationWaitTime;

        public BodyPoints3DGameObject bodyPoints3DGameObject { get; private set; }
        public BodyPoints2DGameObject bodyPoints2DGameObject { get; private set; }

        public bool isMultiplayerEnabled
        {
            get
            {
                return numberOfPlayers >= 2;
            }
        }


        public BodylinkEvents inputEvents
        {
            get
            {
                if (cachedInputEvents == null)
                {
                    cachedInputEvents = GetComponent<BodylinkEvents>();
                }

                return cachedInputEvents;
            }
        }

        // === Prefab References ===
        [SerializeField]
        private PoseLandmarkerRunner poseLandmarkerRunnerPrefab;

        void OnValidate()
        {
            // Clamp the selected limit to the available tracked points for the chosen calibration type
            int maxAllowed = GetMaxMatchPoints(calibrationType);
            matchPointLimit = Mathf.Clamp(matchPointLimit, 1, maxAllowed);
#if UNITY_EDITOR
            SyncSelectedVideoPathFromAsset();
#endif
            EnsureSelectedVideoIsValid();
        }

        private int GetMaxMatchPoints(BodylinkCalibrationType type)
        {
            switch (type)
            {
                case BodylinkCalibrationType.FullBody:
                    return 5; // head + 4 extremities currently used
                case BodylinkCalibrationType.UpperBody:
                    return 3; // head + both hands
                case BodylinkCalibrationType.Feet:
                    return 3; // head + both feet
                case BodylinkCalibrationType.Head:
                default:
                    return 1; // head only
            }
        }


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // prevent duplicates
                return;
            }


            Instance = this;
            cachedInputEvents = GetComponent<BodylinkEvents>();
            startupCalibrationMode = calibrationMode;
            startupCalibrationType = _calibrationType;
            startupMatchPointLimit = _matchPointLimit;
            startupCalibrationWaitTime = calibrationWaitTime;
            DontDestroyOnLoad(gameObject);

            // Subscribe to scene change events
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Start()
        {
            if (!initializeOnStart)
            {
                return;
            }

            Initialize();
            if (calibrateOnStart)
            {
                StartStartupCalibration();
            }
        }

        // === Core Functions ===
        public void Initialize(Action callback = null)
        {
            if (IsInitialized) return;

            if (poseLandmarkerRunnerPrefab == null)
            {
                Debug.LogError("[Bodylink] PoseLandmarkerRunner prefab reference is missing.");
                return;
            }
            PoseLandmarkerRunnerInstance = Instantiate(poseLandmarkerRunnerPrefab, transform);

            poseConfig = PoseLandmarkerRunnerInstance.config;
            poseConfig.NumPoses = numberOfPlayers;

            handConfig = PoseLandmarkerRunnerInstance.GetComponent<HandGestureRunner>().config;
            handConfig.NumHands = numberOfPlayers * 2;

            handGestureRunnerInstance = PoseLandmarkerRunnerInstance.GetComponent<HandGestureRunner>();
            cameraScreen = PoseLandmarkerRunnerInstance.GetComponentInChildren<Mediapipe.Unity.Screen>();
            if (cameraScreen == null)
            {
                Debug.LogError("[Bodylink] Screen component is missing.");
                return;
            }
            bodylinkAvatar = PoseLandmarkerRunnerInstance.GetComponentInChildren<BodylinkAvatar>();
            if (bodylinkAvatar == null)
            {
                Debug.LogError("[Bodylink] Avatar component is missing.");
                return;
            }

            players = bodylinkAvatar.players;

            skeletonVisualizers = PoseLandmarkerRunnerInstance.GetComponentsInChildren<BodylinkSkeletonVisualizer>();
            if (skeletonVisualizers == null)
            {
                Debug.LogError("[Bodylink] SkeletonVisualizer component is missing.");
                return;
            }



            MultiPoseLandmarkListWithMaskAnnotation multiPoseLandmarkListWithMaskAnnotation = PoseLandmarkerRunnerInstance.GetComponentInChildren<MultiPoseLandmarkListWithMaskAnnotation>();
            MultiHandLandmarkListAnnotation multiHandLandmarkListAnnotation = PoseLandmarkerRunnerInstance.GetComponentInChildren<MultiHandLandmarkListAnnotation>();

            if (showSkeleton)
            {
                multiPoseLandmarkListWithMaskAnnotation.SetLandmarkRadius(15);
                multiPoseLandmarkListWithMaskAnnotation.SetConnectionWidth(1);

                multiHandLandmarkListAnnotation.SetLandmarkRadius(15);
                multiHandLandmarkListAnnotation.SetConnectionWidth(1);
            }
            else
            {
                multiPoseLandmarkListWithMaskAnnotation.SetLandmarkRadius(0);
                multiPoseLandmarkListWithMaskAnnotation.SetConnectionWidth(0);

                multiHandLandmarkListAnnotation.SetLandmarkRadius(0);
                multiHandLandmarkListAnnotation.SetConnectionWidth(0);
            }

            BodylinkMultiPoseList.onPlayerFound = (playerCount, player_1_pointListAnnotation, player_2_pointListAnnotation) =>
            {
                if (IsCalibrated) return;
                bodylinkAvatar.SetPlayerPointAnnotations(player_1_pointListAnnotation, player_2_pointListAnnotation);

                bodyPoints2DGameObject?.SetPointListAnnotations(player_1_pointListAnnotation, player_2_pointListAnnotation);
                if (IsInitialized) return;

                bodyPoints3DGameObject = new BodyPoints3DGameObject(skeletonVisualizers);
                bodyPoints2DGameObject = new BodyPoints2DGameObject(player_1_pointListAnnotation, player_2_pointListAnnotation);

                if (showCameraFeed)
                {
                    DisplayCameraFeed(true);
                }

                IsInitialized = true;
                OnInitialized?.Invoke();
                callback?.Invoke();
            };

            if (inputStreamApplyRoutine != null)
            {
                StopCoroutine(inputStreamApplyRoutine);
            }
            inputStreamApplyRoutine = StartCoroutine(ApplyInputStreamWhenReady());

        }

        public void Calibrate(Action callback = null)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[Bodylink] Cannot calibrate before initialization.");
                return;
            }

            StartCalibrationWithCurrentSettings(callback);
        }

        private void StartCalibrationWithCurrentSettings(Action callback = null)
        {
            if (bodylinkAvatar == null)
            {
                Debug.LogWarning("[Bodylink] Cannot calibrate before avatar components are ready.");
                return;
            }

            IsCalibrated = false;
            bodylinkAvatar.Calibrate(selectedCalibrationMode, calibrationType, () =>
            {
                IsCalibrated = true;
                //Debug.Log("[Bodylink] Calibrated.");

                // 🔹 Notify subscribers
                OnCalibrated?.Invoke();

                // 🔹 Optional per-call callback
                callback?.Invoke();

            }, calibrationWaitTime);

        }

        private void StartCalibrationWithSettings(
            BodylinkCalibrationMode calibrationModeToUse,
            BodylinkCalibrationType calibrationTypeToUse,
            int matchPointLimitToUse,
            float calibrationWaitTimeToUse,
            Action callback = null)
        {
            if (bodylinkAvatar == null)
            {
                Debug.LogWarning("[Bodylink] Cannot calibrate before avatar components are ready.");
                return;
            }

            matchPointLimit = matchPointLimitToUse;
            IsCalibrated = false;
            bodylinkAvatar.Calibrate(calibrationModeToUse, calibrationTypeToUse, () =>
            {
                IsCalibrated = true;
                OnCalibrated?.Invoke();
                callback?.Invoke();
            }, calibrationWaitTimeToUse);
        }

        private void StartStartupCalibration()
        {
            if (startupCalibrationRoutine != null)
            {
                StopCoroutine(startupCalibrationRoutine);
            }

            startupCalibrationRoutine = StartCoroutine(CalibrateOnStartWhenReady());
        }

        private IEnumerator CalibrateOnStartWhenReady()
        {
            yield return new WaitUntil(() =>
                bodylinkAvatar != null &&
                players != null &&
                players.Length > 0 &&
                players[0] != null &&
                players[0].bodyLimbCalibration2D != null);

            startupCalibrationRoutine = null;

            if (!calibrateOnStart || isCalibrating)
            {
                yield break;
            }

            // Startup calibration uses the inspector values captured in Awake().
            StartCalibrationWithSettings(
                startupCalibrationMode,
                startupCalibrationType,
                startupMatchPointLimit,
                startupCalibrationWaitTime);
        }

        public void DisableCalibration()
        {
            IsCalibrated = true;
            bodylinkAvatar.DisableCalibration();
        }

        public void Dispose()
        {
            IsInitialized = false;
            IsCalibrated = false;
            if (inputStreamApplyRoutine != null)
            {
                StopCoroutine(inputStreamApplyRoutine);
                inputStreamApplyRoutine = null;
            }
            if (PoseLandmarkerRunnerInstance != null)
            {
                PoseLandmarkerRunnerInstance.Stop();
                Destroy(PoseLandmarkerRunnerInstance.gameObject);
            }

            if (handGestureRunnerInstance != null)
            {
                handGestureRunnerInstance.Stop();
                Destroy(handGestureRunnerInstance.gameObject);
            }

            OnDisposed?.Invoke();
        }

        private IEnumerator ApplyInputStreamWhenReady()
        {
            const float timeoutSeconds = 10f;
            float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;

#if UNITY_ANDROID && !UNITY_EDITOR
            // --- HARUN ANDROID FRONT CAMERA FIX: PERMISSION WAIT ---
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            while (!Application.HasUserAuthorization(UserAuthorization.WebCam) && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }
#endif

            while (ImageSourceProvider.ImageSource == null && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            // Boot ayarlarını yükle ama izleme motorlarını HENTÜZ ÇALIŞTIRMA
            ApplyInputStreamSettings(false);
            
            // --- HARUN ANDROID FRONT CAMERA FIX: SELECTION ---
#if UNITY_ANDROID && !UNITY_EDITOR
            int frontCamIndex = 0;
            for (int i = 0; i < WebCamTexture.devices.Length; i++)
            {
                if (WebCamTexture.devices[i].isFrontFacing)
                {
                    frontCamIndex = i;
                    break;
                }
            }
            if (WebCamTexture.devices.Length > 0 && imageSource != null)
            {
                imageSource.SelectSource(frontCamIndex); // Sadece kaynağı değiştir
            }
#endif

            // Kamera kaynağı düzgünce seçildikten sonra HER ŞEYİ BAŞLAT
            RestartRunnersForImageSource();

            inputStreamApplyRoutine = null;
        }

        public void SelectSource(int index)
        {
            imageSource.SelectSource(index);
            if (PoseLandmarkerRunnerInstance.isActiveAndEnabled)
            {
                PoseLandmarkerRunnerInstance.Resume();
            }
            else
            {
                PoseLandmarkerRunnerInstance.Play();
            }
            
            // --- HARUN HAND GESTURE FREEZE FIX ---
            if (handGestureRunnerInstance != null)
            {
                if (handGestureRunnerInstance.isActiveAndEnabled)
                    handGestureRunnerInstance.Resume();
                else
                    handGestureRunnerInstance.Play();
            }
        }

        public string[] GetRecordedVideoFileNames()
        {
            string selectedVideoPath = GetSelectedVideoAbsolutePath();
            if (!File.Exists(selectedVideoPath))
            {
                return Array.Empty<string>();
            }

            return new[] { Path.GetFileName(selectedVideoPath) };
        }

        public void ApplyInputStreamSettings(bool restartRunners = true)
        {
            bool configured = TryConfigureImageSource();
            if (!configured)
            {
                return;
            }

            if (restartRunners && Application.isPlaying)
            {
                RestartRunnersForImageSource();
            }
        }

        private bool TryConfigureImageSource()
        {
            ImageSourceType desiredType = _inputStream == BodylinkInputStreamType.Video
                ? ImageSourceType.Video
                : ImageSourceType.WebCamera;

            ImageSourceProvider.Switch(desiredType);
            ImageSource activeSource = ImageSourceProvider.ImageSource;
            if (activeSource == null)
            {
                return false;
            }

            // Video inputs are mirrored before MediaPipe processing so prerecorded clips
            // behave like front-facing camera feeds.
            activeSource.isHorizontallyFlipped = _inputStream == BodylinkInputStreamType.Video;

            if (_inputStream != BodylinkInputStreamType.Video)
            {
                return true;
            }

            if (!(activeSource is VideoSource videoSource))
            {
                Debug.LogWarning("[Bodylink] Active image source is not a VideoSource.");
                return false;
            }

            return TryConfigureVideoSource(videoSource);
        }

        private bool TryConfigureVideoSource(VideoSource videoSource)
        {
            string selectedVideoPath = GetSelectedVideoAbsolutePath();
            if (!IsSupportedVideoPath(selectedVideoPath) || !File.Exists(selectedVideoPath))
            {
                Debug.LogWarning("[Bodylink] No valid video selected. Drag and drop a video file in the Bodylink inspector. Falling back to WebCam.");
                _inputStream = BodylinkInputStreamType.WebCam;
                ImageSourceProvider.Switch(ImageSourceType.WebCamera);
                if (ImageSourceProvider.ImageSource != null)
                {
                    ImageSourceProvider.ImageSource.isHorizontallyFlipped = false;
                }
                return ImageSourceProvider.ImageSource != null;
            }

            videoSource.SetExternalSourcePaths(new[] { selectedVideoPath }, true);
            videoSource.loop = _playVideoInLoop;
            videoSource.SelectSource(0);
            return true;
        }

        private void RestartRunnersForImageSource()
        {
            if (PoseLandmarkerRunnerInstance != null && PoseLandmarkerRunnerInstance.isActiveAndEnabled)
            {
                PoseLandmarkerRunnerInstance.Play();
            }

            if (handGestureRunnerInstance != null && handGestureRunnerInstance.isActiveAndEnabled)
            {
                handGestureRunnerInstance.Play();
            }
        }

        private void EnsureSelectedVideoIsValid()
        {
            if (_inputStream != BodylinkInputStreamType.Video)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedVideoFile))
            {
                return;
            }

            string selectedVideoPath = GetSelectedVideoAbsolutePath();
            if (!IsSupportedVideoPath(selectedVideoPath))
            {
                _selectedVideoFile = string.Empty;
            }
        }

        private string GetSelectedVideoAbsolutePath()
        {
            if (string.IsNullOrWhiteSpace(_selectedVideoFile))
            {
                return string.Empty;
            }

            string configuredPath = _selectedVideoFile.Trim();
            if (Path.IsPathRooted(configuredPath))
            {
                return Path.GetFullPath(configuredPath);
            }

            string normalizedPath = configuredPath.Replace('\\', '/');
            if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = normalizedPath.Substring("Assets/".Length);
                return Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
            }

            // Backward compatibility: older setup stored file name only.
            return Path.GetFullPath(Path.Combine(videoRecordingsFolderPath, configuredPath));
        }

        private bool IsSupportedVideoPath(string videoPath)
        {
            if (string.IsNullOrWhiteSpace(videoPath))
            {
                return false;
            }

            string extension = Path.GetExtension(videoPath);
            return SupportedVideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

#if UNITY_EDITOR
        private void SyncSelectedVideoPathFromAsset()
        {
            if (_selectedVideoAsset == null)
            {
                _selectedVideoFile = string.Empty;
                return;
            }

            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(_selectedVideoAsset);
            _selectedVideoFile = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
        }
#endif

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (resumeOnSceneChange && IsInitialized && poseLandmarkerRunnerPrefab != null)
            {
                PoseLandmarkerRunnerInstance.Resume();
                handGestureRunnerInstance.Resume();
            }
        }



        public BodylinkSkeletonVisualizer GetSkeleton(int playerIndex = 0)
        {
            // logicalPlayerIndex → 0 = left player, 1 = right player
            int realMpIndex = bodylinkAvatar.stablePlayerIndex[playerIndex];
            return skeletonVisualizers[realMpIndex];
        }

        public bool TryGetPlayerCurrentData(int playerIndex, out BodyCalibrationData2D data)
        {
            if (!TryGetPlayerMeasurementData(playerIndex, out BodyCalibrationData2D sourceData))
            {
                data = null;
                return false;
            }

            data = BodyLimbCalibration2D.Clone(sourceData);
            return data != null;
        }

        private bool TryGetPlayerMeasurementData(int playerIndex, out BodyCalibrationData2D data)
        {
            data = null;
            if (!IsInitialized || bodylinkAvatar == null || bodylinkAvatar.players == null)
            {
                return false;
            }

            if (playerIndex < 0 || playerIndex >= bodylinkAvatar.players.Length || bodylinkAvatar.players[playerIndex] == null)
            {
                return false;
            }

            BodyLimbCalibration2D calibration = bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D;
            if (calibration == null)
            {
                return false;
            }

            data = calibration.currentFrameData ?? calibration.calibrationData;
            return data != null;
        }

        public float GetPlayerCurrentHeight(int playerIndex = 0)
        {
            return TryGetPlayerMeasurementData(playerIndex, out BodyCalibrationData2D data)
                ? data.height
                : 0f;
        }

        public float GetPlayerHeightRatio(int playerIndex = 0)
        {
            float currentHeight = GetPlayerCurrentHeight(playerIndex);
            return currentHeight > Mathf.Epsilon ? referenceHeight / currentHeight : 0f;
        }

        public float GetPlayerArmRatio(int playerIndex = 0)
        {
            return TryGetPlayerMeasurementData(playerIndex, out BodyCalibrationData2D data)
                ? data.armRatio
                : 0f;
        }

        public float GetPlayerLegRatio(int playerIndex = 0)
        {
            return TryGetPlayerMeasurementData(playerIndex, out BodyCalibrationData2D data)
                ? data.legRatio
                : 0f;
        }

        public float GetPlayerTorsoRatio(int playerIndex = 0)
        {
            return TryGetPlayerMeasurementData(playerIndex, out BodyCalibrationData2D data)
                ? data.torsoRatio
                : 0f;
        }

        public float GetPlayerArmLength(int playerIndex = 0)
        {
            return TryGetPlayerMeasurementData(playerIndex, out BodyCalibrationData2D data)
                ? data.armLength
                : 0f;
        }

        public float GetPlayerLegLength(int playerIndex = 0)
        {
            return TryGetPlayerMeasurementData(playerIndex, out BodyCalibrationData2D data)
                ? data.legLength
                : 0f;
        }

        public void Show3DSkeleton(bool show, int playerIndex = 0, float size = .1f)
        {
            skeletonVisualizers[playerIndex].ShowSkelton(show, size);
        }

        public void DisplayCameraFeed(bool show)
        {
            showCameraFeed = show;
            players[0].SetMiniCameraScreen();
            players[0].ShowMiniCamera(show);
            
            // --- HARUN UI LAYOUT FIX ---
            if (show && cameraScreen != null)
            {
                var screenRect = cameraScreen.GetComponent<RectTransform>();
                if (screenRect != null)
                {
                    // Bottom-Right mini camera override
                    screenRect.anchorMin = new Vector2(1, 0);
                    screenRect.anchorMax = new Vector2(1, 0);
                    screenRect.pivot = new Vector2(1, 0);
                    screenRect.sizeDelta = new Vector2(400, 225);
                    screenRect.anchoredPosition = new Vector2(-20, 20);
                }
                
                Canvas parentCanvas = cameraScreen.GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    parentCanvas.sortingOrder = 99;
                }
            }
        }

        public void SetCameraFeedSize(float size)
        {
            players[0].SetMiniCameraScale(size);
        }

        void OnDisable()
        {
        }

        private void OnDestroy()
        {
            if (inputStreamApplyRoutine != null)
            {
                StopCoroutine(inputStreamApplyRoutine);
                inputStreamApplyRoutine = null;
            }
            if (startupCalibrationRoutine != null)
            {
                StopCoroutine(startupCalibrationRoutine);
                startupCalibrationRoutine = null;
            }
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }
    }

}
