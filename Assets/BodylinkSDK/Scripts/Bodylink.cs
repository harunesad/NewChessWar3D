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

namespace BodylinkSDK
{
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
        private const float referenceHeight = 1.65f; // average height for normalization

        [Range(1, 2)]
        [FormerlySerializedAs("numberOfPlayers")]
        [SerializeField] private int _numberOfPlayers = 1;

        public bool isCalibrating { get; set; }


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
            DontDestroyOnLoad(gameObject);

            // Subscribe to scene change events
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Start()
        {
            if (initializeOnStart) Initialize(() =>
            {
                if (calibrateOnStart) Calibrate();
            });

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

        }

        public void Calibrate(Action callback = null)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[Bodylink] Cannot calibrate before initialization.");
                return;
            }
            IsCalibrated = false;
            bodylinkAvatar.Calibrate(calibrationType, () =>
            {
                IsCalibrated = true;
                //Debug.Log("[Bodylink] Calibrated.");

                // 🔹 Notify subscribers
                OnCalibrated?.Invoke();

                // 🔹 Optional per-call callback
                callback?.Invoke();

            }, calibrationWaitTime);

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
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (resumeOnSceneChange && IsInitialized && IsCalibrated && poseLandmarkerRunnerPrefab != null)
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

        public float GetPlayerCurrentHeight(int playerIndex = 0)
        {
            if (!IsInitialized) return 0;
            if (bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData == null) return 0;
            float height = bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData.height;
            return height;
        }

        public float GetPlayerHeightRatio(int playerIndex = 0)
        {
            float currentHeight = GetPlayerCurrentHeight(playerIndex);
            return referenceHeight / currentHeight;
        }

        public float GetPlayerArmRatio(int playerIndex = 0)
        {
            if (bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData == null) return 0;
            return bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData.armRatio;
        }

        public float GetPlayerLegRatio(int playerIndex = 0)
        {
            if (bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData == null) return 0;
            return bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData.legRatio;
        }

        public float GetPlayerTorsoRatio(int playerIndex = 0)
        {
            if (bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData == null) return 0;
            return bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData.torsoRatio;
        }

        public float GetPlayerArmLength(int playerIndex = 0)
        {
            if (bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData == null) return 0;
            return bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData.armLength;
        }

        public float GetPlayerLegLength(int playerIndex = 0)
        {
            if (bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData == null) return 0;
            return bodylinkAvatar.players[playerIndex].bodyLimbCalibration2D.calibrationData.legLength;
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
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }
    }

}
