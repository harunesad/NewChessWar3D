#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace BodylinkSDK
{
    public class MotionGestureEditor : EditorWindow
    {
        private const int LandmarkCount = 33;
        private const float MinDistanceEpsilon = 0.0001f;
        private const float PickRadius = 14f;
        private const string MotionsFolderRelativeToAssetsPath = "MotionGestures";
        private const string DefaultPoseLandmarkerModelName = "pose_landmarker_full.bytes";
        private const float PoseViewBaseWidth = 560f;
        private const float PoseViewBaseHeight = 560f;
        private const float MinTrackedVisibility = 0.15f;
        private const float SidebarWidth = 470f;
        private const float BottomTimelineMinHeight = 220f;
        private const float MotionDeltaScaleFactor2D = 0.04f;
        private const float MotionDeltaScaleFactor3D = 0.05f;
        private const float MotionDeltaMin2D = 0.008f;
        private const float MotionDeltaMax2D = 0.08f;
        private const float MotionDeltaMin3D = 0.01f;
        private const float MotionDeltaMax3D = 0.18f;
        private const float MotionDeltaAccumulationMultiplier = 1.8f;
        private const float AngleMotionDeltaThreshold = 7f;
        private const float AngleMotionAccumulationThreshold = 14f;
        private const float CorePointDeltaAssistMultiplier = 0.55f;
        private const int WebcamCaptureWidth = 640;
        private const int WebcamCaptureHeight = 480;
        private const float WebcamInitTimeoutSeconds = 15f;
        private const int TimelinePreviewFrameMaxDimension = 320;
        private const float TimelineSelectionDragThreshold = 4f;
        private const float TimelineSelectionDottedLineSize = 4f;
        private const float TimelineMarkerWidth = 8f;
        private const float DefaultPointSize = 6.5f;
        private const int DefaultSolverIterations = 18;
        private const float DefaultBodyImageOpacity = 0.55f;
        private const float DefaultVideoBackgroundOpacity = 0.55f;
        private const string DefaultMotionFileName = "motion_gesture";
        private const float DefaultMotionDuration = 1f;
        private const int DefaultVideoSampleRate = 4;
        private const float DefaultVideoMaxDuration = 6f;
        private const int DefaultVideoMaxFrames = 64;
        private const float DefaultWebcamTimedStopAtSeconds = 4f;

        private struct BoneConstraint
        {
            public readonly int A;
            public readonly int B;
            public readonly float Length;

            public BoneConstraint(int a, int b, float length)
            {
                A = a;
                B = b;
                Length = length;
            }
        }

        private struct LimbChain
        {
            public readonly int Root;
            public readonly int Joint;
            public readonly int End;

            public LimbChain(int root, int joint, int end)
            {
                Root = root;
                Joint = joint;
                End = end;
            }
        }

        [Serializable]
        private class MotionJsonData
        {
            public string motionName;
            public float duration;
            public float sampleRate;
            public MotionFrameJsonData[] frames;
        }

        [Serializable]
        private class MotionFrameJsonData
        {
            public float time;
            public TrackedPosePoint[] trackedLandmarks;
            public TrackedPoseAngle[] trackedAngles;
            public TrackedPoseWorldPoint[] trackedWorldLandmarks;
        }

        [Serializable]
        private class PoseJsonData
        {
            public TrackedPosePoint[] trackedLandmarks;
            public TrackedPoseAngle[] trackedAngles;
        }

        [Serializable]
        private struct TrackedPosePoint
        {
            public int index;
            public float x;
            public float y;

            public TrackedPosePoint(int landmarkIndex, float xValue, float yValue)
            {
                index = landmarkIndex;
                x = xValue;
                y = yValue;
            }
        }

        [Serializable]
        private struct TrackedPoseAngle
        {
            public string name;
            public int a;
            public int b;
            public int c;
            public float angle;

            public TrackedPoseAngle(string angleName, int start, int pivot, int end, float value)
            {
                name = angleName;
                a = start;
                b = pivot;
                c = end;
                angle = value;
            }
        }

        [Serializable]
        private struct TrackedPoseWorldPoint
        {
            public int index;
            public float x;
            public float y;
            public float z;

            public TrackedPoseWorldPoint(int landmarkIndex, float xValue, float yValue, float zValue)
            {
                index = landmarkIndex;
                x = xValue;
                y = yValue;
                z = zValue;
            }
        }

        private readonly struct PoseAngleDefinition
        {
            public readonly string Name;
            public readonly int A;
            public readonly int B;
            public readonly int C;

            public PoseAngleDefinition(string name, int a, int b, int c)
            {
                Name = name;
                A = a;
                B = b;
                C = c;
            }
        }

        private sealed class KeyframeData
        {
            public float time;
            public Vector2[] landmarks;
            public bool[] trackedPoints;
            public Vector3[] worldLandmarks;
            public bool hasWorldLandmarks;

            public KeyframeData(float keyTime, Vector2[] points, bool[] tracked, Vector3[] worldPoints = null, bool hasWorldPoints = false)
            {
                time = keyTime;
                landmarks = points;
                trackedPoints = tracked;
                worldLandmarks = worldPoints;
                hasWorldLandmarks = hasWorldPoints && worldPoints != null && worldPoints.Length == LandmarkCount;
            }

            public KeyframeData Clone()
            {
                Vector2[] clonedLandmarks = new Vector2[LandmarkCount];
                bool[] clonedTrackedPoints = new bool[LandmarkCount];
                Array.Copy(landmarks, clonedLandmarks, LandmarkCount);
                Array.Copy(trackedPoints, clonedTrackedPoints, LandmarkCount);

                Vector3[] clonedWorldLandmarks = null;
                if (hasWorldLandmarks && worldLandmarks != null && worldLandmarks.Length == LandmarkCount)
                {
                    clonedWorldLandmarks = new Vector3[LandmarkCount];
                    Array.Copy(worldLandmarks, clonedWorldLandmarks, LandmarkCount);
                }

                return new KeyframeData(time, clonedLandmarks, clonedTrackedPoints, clonedWorldLandmarks, hasWorldLandmarks);
            }
        }

        private sealed class VideoImportContext : IDisposable
        {
            public GameObject host;
            public VideoPlayer videoPlayer;
            public RenderTexture renderTexture;
            public Texture2D readbackTexture;
            public PoseLandmarker poseLandmarker;
            public readonly List<KeyframeData> capturedFrames = new List<KeyframeData>();
            public readonly List<WebcamBackgroundFrame> sampledFrames = new List<WebcamBackgroundFrame>();

            public float duration;
            public float sampleInterval;
            public float nextSampleTime;
            public int targetFrameCount;
            public int sampledFrameCount;
            public int detectedFrameCount;
            public bool prepared;
            public bool canceled;
            public bool flipX;
            public double startedEditorTime;

            public void Dispose()
            {
                if (videoPlayer != null)
                {
                    videoPlayer.Stop();
                }

                if (poseLandmarker != null)
                {
                    if (poseLandmarker is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    poseLandmarker = null;
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                    renderTexture = null;
                }

                if (readbackTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(readbackTexture);
                    readbackTexture = null;
                }

                if (host != null)
                {
                    UnityEngine.Object.DestroyImmediate(host);
                    host = null;
                }

                for (int i = 0; i < sampledFrames.Count; i++)
                {
                    sampledFrames[i].Dispose();
                }
                sampledFrames.Clear();
            }
        }

        private sealed class WebcamBackgroundFrame : IDisposable
        {
            public float time;
            public Texture2D texture;

            public WebcamBackgroundFrame(float frameTime, Texture2D frameTexture)
            {
                time = frameTime;
                texture = frameTexture;
            }

            public void Dispose()
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                }
            }
        }

        private sealed class WebcamRecordContext : IDisposable
        {
            public WebCamTexture webcamTexture;
            public Texture2D readbackTexture;
            public Color32[] pixelBuffer;
            public PoseLandmarker poseLandmarker;
            public readonly List<KeyframeData> capturedFrames = new List<KeyframeData>();
            public readonly List<WebcamBackgroundFrame> sampledFrames = new List<WebcamBackgroundFrame>();

            public bool flipX;
            public bool autoStop;
            public float captureStartDelay;
            public float captureDuration;
            public float sampleInterval;
            public float nextSampleTime;
            public int targetFrameCount;
            public int sampledFrameCount;
            public int detectedFrameCount;
            public bool webcamReady;
            public bool recordingActive;
            public double startedEditorTime;
            public double recordingStartEditorTime;

            public void Dispose()
            {
                if (webcamTexture != null)
                {
                    webcamTexture.Stop();
                    UnityEngine.Object.DestroyImmediate(webcamTexture);
                    webcamTexture = null;
                }

                if (poseLandmarker != null)
                {
                    if (poseLandmarker is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    poseLandmarker = null;
                }

                if (readbackTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(readbackTexture);
                    readbackTexture = null;
                }

                for (int i = 0; i < sampledFrames.Count; i++)
                {
                    sampledFrames[i].Dispose();
                }
                sampledFrames.Clear();
                pixelBuffer = null;
            }
        }

        private static readonly string[] LandmarkNames =
        {
            "Nose",
            "RightEyeInner",
            "RightEye",
            "RightEyeOuter",
            "LeftEyeInner",
            "LeftEye",
            "LeftEyeOuter",
            "RightEar",
            "LeftEar",
            "MouthRight",
            "MouthLeft",
            "RightShoulder",
            "LeftShoulder",
            "RightElbow",
            "LeftElbow",
            "RightWrist",
            "LeftWrist",
            "RightPinky",
            "LeftPinky",
            "RightIndex",
            "LeftIndex",
            "RightThumb",
            "LeftThumb",
            "RightHip",
            "LeftHip",
            "RightKnee",
            "LeftKnee",
            "RightAnkle",
            "LeftAnkle",
            "RightHeel",
            "LeftHeel",
            "RightFootIndex",
            "LeftFootIndex"
        };

        // Simplified skeleton:
        // head -> nose only
        // hands -> wrist only
        // feet -> ankle only
        private static readonly int[] VisibleLandmarkIndices =
        {
            0,
            11, 12,
            13, 14,
            15, 16,
            23, 24,
            25, 26,
            27, 28
        };

        private static readonly int[,] BonePairs =
        {
            { 0, 11 }, { 0, 12 },
            { 11, 12 },
            { 11, 13 }, { 13, 15 },
            { 12, 14 }, { 14, 16 },
            { 11, 23 }, { 12, 24 }, { 23, 24 },
            { 23, 25 }, { 25, 27 },
            { 24, 26 }, { 26, 28 }
        };

        private static readonly LimbChain[] LimbChains =
        {
            new LimbChain(12, 14, 16),
            new LimbChain(11, 13, 15),
            new LimbChain(24, 26, 28),
            new LimbChain(23, 25, 27)
        };

        private static readonly PoseAngleDefinition[] TrackedAngleDefinitions =
        {
            new PoseAngleDefinition("left_elbow", 12, 14, 16),
            new PoseAngleDefinition("right_elbow", 11, 13, 15),
            new PoseAngleDefinition("left_knee", 24, 26, 28),
            new PoseAngleDefinition("right_knee", 23, 25, 27),
            new PoseAngleDefinition("left_shoulder", 24, 12, 14),
            new PoseAngleDefinition("right_shoulder", 23, 11, 13),
            new PoseAngleDefinition("left_hip", 12, 24, 26),
            new PoseAngleDefinition("right_hip", 11, 23, 25)
        };

        private readonly Vector2[] _landmarks = new Vector2[LandmarkCount];
        private readonly Vector2[] _defaultLandmarks = new Vector2[LandmarkCount];
        private readonly bool[] _trackedPoints = new bool[LandmarkCount];
        private readonly Vector3[] _worldLandmarks = new Vector3[LandmarkCount];
        private readonly List<BoneConstraint> _constraints = new List<BoneConstraint>();
        private readonly Dictionary<ulong, float> _boneLengths = new Dictionary<ulong, float>();
        private readonly Dictionary<int, LimbChain> _chainByJoint = new Dictionary<int, LimbChain>();
        private readonly List<KeyframeData> _keyframes = new List<KeyframeData>();
        private readonly HashSet<KeyframeData> _selectedKeyframes = new HashSet<KeyframeData>();

        private Rect _canvasRect;
        private Rect _normalizedSpaceRect;
        private Rect _canvasHostRect;
        private bool _showLabels;
        private bool _lockTorso = true;
        private bool _clampToCanvas = true;
        private float _pointSize = DefaultPointSize;
        private int _solverIterations = DefaultSolverIterations;
        private bool _showTrackedPoints = true;
        private Texture2D _bodyReferenceImage;
        private Texture2D _runtimeLoadedBodyImage;
        private bool _showBodyReferenceImage = true;
        private bool _flipImportedImageX;
        private float _bodyImageOpacity = DefaultBodyImageOpacity;
        private Vector2 _rightPanelScroll;

        private int _selectedJoint = -1;
        private bool _isDragging;
        private string _motionFileName = DefaultMotionFileName;
        private float _motionDuration = DefaultMotionDuration;
        private int _selectedKeyframeIndex;
        private bool _isPreviewPlaying;
        private float _previewTime;
        private double _previewStartEditorTime;
        private bool _hasWorldLandmarksForEditorPose;
        private bool _isTimelineSelectionDragging;
        private Vector2 _timelineSelectionStart;
        private Vector2 _timelineSelectionCurrent;

        private string _videoFilePath = string.Empty;
        private bool _flipImportedVideoX;
        private int _videoSampleRate = DefaultVideoSampleRate;
        private float _videoMaxDuration = DefaultVideoMaxDuration;
        private int _videoMaxFrames = DefaultVideoMaxFrames;
        private VideoImportContext _videoImport;
        private bool _videoImportStartQueued;
        private bool _showVideoBackground = true;
        private float _videoBackgroundOpacity = DefaultVideoBackgroundOpacity;
        private string _videoBackgroundLoadedPath = string.Empty;
        private double _lastVideoBackgroundTime = -1d;
        private long _lastVideoBackgroundFrame = -1L;
        private bool _videoBackgroundShowFirstFrameOnPrepare;
        private GameObject _videoBackgroundHost;
        private VideoPlayer _videoBackgroundPlayer;
        private RenderTexture _videoBackgroundRenderTexture;
        private WebcamRecordContext _webcamRecord;
        private string[] _webcamDeviceNames = Array.Empty<string>();
        private int _selectedWebcamDeviceIndex;
        private bool _useTimedWebcamRecording;
        private float _webcamTimedStartAtSeconds;
        private float _webcamTimedStopAtSeconds = DefaultWebcamTimedStopAtSeconds;
        private readonly List<WebcamBackgroundFrame> _recordedWebcamBackgroundFrames = new List<WebcamBackgroundFrame>();
        private float _recordedWebcamBackgroundDuration;
        private readonly List<WebcamBackgroundFrame> _recordedVideoBackgroundFrames = new List<WebcamBackgroundFrame>();
        private float _recordedVideoBackgroundDuration;

        [MenuItem("Bodylink/Motion Gesture Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<MotionGestureEditor>("Bodylink Motion Gesture Editor");
            window.minSize = new Vector2(1160f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureMotionsFolderExists();
            BuildDefaultPose();
            CopyPose(_defaultLandmarks, _landmarks);
            InitializeDefaultTrackedPoints();
            BuildChainMap();
            RebuildBoneConstraintsFromCurrentPose();
            BuildDefaultTimeline();
        }

        private void OnDisable()
        {
            StopPreviewPlayback();
            if (_videoImportStartQueued)
            {
                EditorApplication.delayCall -= ExecuteQueuedVideoImportStart;
                _videoImportStartQueued = false;
            }
            CancelVideoImport(showDialog: false);
            CancelWebcamRecording(showDialog: false);
            ReleaseVideoBackgroundPlayer();
            ClearRecordedVideoBackgroundFrames();
            ClearRecordedWebcamBackgroundFrames();
            ReleaseRuntimeLoadedImage();
        }

        private void OnGUI()
        {
            UpdatePreviewPlayback();
            EnsureVideoBackgroundState();

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        DrawCanvas();
                        HandleMouse(Event.current);
                        DrawSkeleton();
                    }

                    DrawRightPanel();
                }

                GUILayout.Space(4f);
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.MinHeight(BottomTimelineMinHeight)))
                {
                    DrawTimelineSection();
                }
            }
        }

        private void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true)))
            {
                _rightPanelScroll = EditorGUILayout.BeginScrollView(_rightPanelScroll);
                DrawHeaderSection();
                DrawMotionFileSection();
                DrawMotionActionsSection();
                DrawImageInputSection();
                DrawVideoInputSection();
                DrawEditorSettingsSection();
                DrawTrackedPointsSection();
                DrawSelectedJointSection();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndScrollView();
            }
        }

        private static GUIContent TooltipContent(string label, string tooltipOverride = null)
        {
            return new GUIContent(label, string.IsNullOrEmpty(tooltipOverride) ? ResolveTooltip(label) : tooltipOverride);
        }

        private static string ResolveTooltip(string label)
        {
            switch (label)
            {
                case "Mediapipe Motion Gesture Editor":
                    return "Create, preview, and export pose-based motion gestures from manually edited, image-derived, video-derived, or webcam-derived keyframes.";
                case "Motion File":
                    return "Set the exported motion file name and output location.";
                case "Name":
                    return "File name used when saving the motion JSON into Assets/MotionGestures.";
                case "Save Folder":
                    return "Project folder where exported motion JSON files are written.";
                case "Timeline":
                    return "Edit keyframes, timing, playback, and selection for the current motion.";
                case "Duration (seconds)":
                    return "Total motion duration used for timeline playback and export.";
                case "Prev":
                    return "Select the previous keyframe in the timeline.";
                case "Next":
                    return "Select the next keyframe in the timeline.";
                case "Play Preview":
                    return "Play the current timeline from the selected keyframe position.";
                case "Stop Preview":
                    return "Stop preview playback and return to the selected keyframe pose.";
                case "Add Keyframe":
                    return "Insert a new keyframe using the current editor pose.";
                case "Duplicate":
                    return "Duplicate the currently selected keyframe.";
                case "Remove":
                    return "Remove the selected keyframe or the current multi-selection.";
                case "Select All":
                    return "Select every keyframe in the current timeline.";
                case "Normalize Time":
                    return "Redistribute all keyframes evenly across the current timeline duration.";
                case "Trim Timeline":
                    return "Trim the timeline duration and recorded background frames to the selected keyframe range.";
                case "Fit Duration To Keys":
                    return "Set the motion duration to the time of the last keyframe.";
                case "Auto Fix Pose":
                    return "Automatically correct outlier joints in the primary selected keyframe using neighboring frames as reference.";
                case "Selected Keyframe Time":
                    return "Move the primary selected keyframe to a new time in the timeline.";
                case "Selected":
                    return "Shows the current keyframe selection and primary keyframe details.";
                case "Motion Actions":
                    return "Apply pose actions and save or load motion JSON files.";
                case "Reset Current Key":
                case "Reset Selected Keys":
                    return "Reset the selected keyframe pose to the default editor skeleton and tracked points.";
                case "Use Current As Baseline":
                    return "Rebuild bone constraints from the current pose for future editing adjustments.";
                case "Reset Editor":
                    return "Clear the current motion, imports, and editor state, then start again from the default setup.";
                case "Save JSON":
                    return "Write the current motion timeline to a JSON file in Assets/MotionGestures.";
                case "Load JSON":
                    return "Load a saved motion JSON file from Assets/MotionGestures into the editor.";
                case "Copy JSON":
                    return "Copy the current motion JSON to the system clipboard.";
                case "Editor Settings":
                    return "Configure display and pose-solving behavior for the editor window.";
                case "Show Labels":
                    return "Show landmark names and tracked state beside the joints on the canvas.";
                case "Lock Torso":
                    return "Keep torso joints fixed while moving non-torso joints.";
                case "Clamp To 0-1":
                    return "Keep edited landmark positions inside normalized canvas space.";
                case "Point Size":
                    return "Visual size of landmark points on the canvas.";
                case "Solver Iterations":
                    return "Number of constraint solver passes used while adjusting the pose.";
                case "Selected Joint":
                    return "Shows details about the joint currently selected on the canvas.";
                case "x":
                    return "Normalized X position of the currently selected joint.";
                case "y":
                    return "Normalized Y position of the currently selected joint.";
                case "Track All Visible":
                    return "Enable tracking for every visible landmark in the selected keyframes.";
                case "Clear Tracked":
                    return "Disable tracking for every visible landmark in the selected keyframes.";
                case "Center":
                    return "Landmarks that are centered on the body and are not left- or right-specific.";
                case "Left":
                    return "Left-side tracked landmarks.";
                case "Right":
                    return "Right-side tracked landmarks.";
                case "Image To Keyframe":
                    return "Load a still image and detect a pose to apply to the selected keyframes.";
                case "Body Image":
                    return "Reference image used for pose extraction and optional canvas overlay.";
                case "Load File":
                    return "Choose an image file from disk and assign it as the body reference image.";
                case "Show Image":
                    return "Show or hide the reference body image on the canvas.";
                case "Flip X":
                    return "Mirror the imported image horizontally before running pose detection.";
                case "Image Opacity":
                    return "Opacity of the body reference image overlay on the canvas.";
                case "Apply Pose To Selected Key":
                case "Apply Pose To Selected Keys":
                    return "Run pose detection on the current body image and apply the result to the selected keyframes.";
                case "Video To Timeline":
                    return "Import a video, sample poses over time, and build a motion timeline automatically.";
                case "Video File":
                    return "The video file currently selected for timeline generation and background preview.";
                case "Pick Video File":
                    return "Choose a video file from disk for timeline generation.";
                case "Clear":
                    return "Clear the currently selected video file.";
                case "Flip Video X":
                    return "Mirror the imported video horizontally before pose detection.";
                case "Show Video Background":
                    return "Show the selected or recorded video as a background behind the pose canvas.";
                case "Video Opacity":
                    return "Opacity of the video background shown behind the canvas.";
                case "Sample Rate (fps)":
                    return "How many frames per second are sampled from video or webcam recordings.";
                case "Max Duration (sec)":
                    return "Maximum duration to import from a video or record from webcam when auto-stopping.";
                case "Max Frames":
                    return "Maximum number of sampled frames allowed during import or recording.";
                case "Detected Frames":
                    return "How many sampled frames produced a valid pose detection.";
                case "Cancel Video Import":
                    return "Stop the current video import and discard any incomplete results.";
                case "Generate Timeline From Video":
                    return "Sample the selected video and generate a new keyframed motion timeline.";
                case "Webcam Recorder":
                    return "Record live webcam motion and convert it into a timeline.";
                case "Webcam Device":
                    return "Choose which connected webcam device to use for recording.";
                case "Use Timed Start/Stop":
                    return "Automatically start and stop webcam recording at specified times.";
                case "Start At (sec)":
                    return "Delay before timed webcam recording begins.";
                case "Stop At (sec)":
                    return "Time when timed webcam recording stops.";
                case "Stop Webcam Recording":
                    return "Finish the current webcam recording and build a timeline from captured poses.";
                case "Cancel":
                    return "Cancel the current webcam recording without keeping partial results.";
                case "Start Timed Webcam Recording":
                    return "Begin a webcam recording session that starts and stops at the configured times.";
                case "Start Webcam Recording":
                    return "Begin recording webcam poses immediately.";
                case "Recorded Webcam":
                    return "Summary of the currently stored webcam background frames.";
                case "Clear Recorded Webcam Frames":
                    return "Delete the recorded webcam background frames currently stored in the editor.";
                default:
                    return string.Empty;
            }
        }

        private static void TooltipLabelField(string label, GUIStyle style = null, string tooltipOverride = null, params GUILayoutOption[] options)
        {
            GUIContent content = TooltipContent(label, tooltipOverride);
            if (style != null)
            {
                EditorGUILayout.LabelField(content, style, options);
            }
            else
            {
                EditorGUILayout.LabelField(content, options);
            }
        }

        private static void TooltipLabelField(string label, string value, GUIStyle style = null, string tooltipOverride = null)
        {
            GUIContent labelContent = TooltipContent(label, tooltipOverride);
            GUIContent valueContent = new GUIContent(value, labelContent.tooltip);
            if (style != null)
            {
                EditorGUILayout.LabelField(labelContent, valueContent, style);
            }
            else
            {
                EditorGUILayout.LabelField(labelContent, valueContent);
            }
        }

        private static string TooltipTextField(string label, string value, params GUILayoutOption[] options)
        {
            return EditorGUILayout.TextField(TooltipContent(label), value, options);
        }

        private static float TooltipFloatField(string label, float value)
        {
            return EditorGUILayout.FloatField(TooltipContent(label), value);
        }

        private static int TooltipIntField(string label, int value)
        {
            return EditorGUILayout.IntField(TooltipContent(label), value);
        }

        private static float TooltipSlider(string label, float value, float leftValue, float rightValue)
        {
            return EditorGUILayout.Slider(TooltipContent(label), value, leftValue, rightValue);
        }

        private static int TooltipIntSlider(string label, int value, int leftValue, int rightValue)
        {
            return EditorGUILayout.IntSlider(TooltipContent(label), value, leftValue, rightValue);
        }

        private static bool TooltipToggleLeft(string label, bool value, params GUILayoutOption[] options)
        {
            return EditorGUILayout.ToggleLeft(TooltipContent(label), value, options);
        }

        private static bool TooltipFoldout(bool foldout, string label, bool toggleOnLabelClick, string tooltipOverride = null)
        {
            return EditorGUILayout.Foldout(foldout, TooltipContent(label, tooltipOverride), toggleOnLabelClick);
        }

        private static bool TooltipButton(string label, params GUILayoutOption[] options)
        {
            return GUILayout.Button(TooltipContent(label), options);
        }

        private static void TooltipPrefixLabel(string label)
        {
            EditorGUILayout.PrefixLabel(TooltipContent(label));
        }

        private static UnityEngine.Object TooltipObjectField(string label, UnityEngine.Object value, Type objectType, bool allowSceneObjects, params GUILayoutOption[] options)
        {
            return EditorGUILayout.ObjectField(TooltipContent(label), value, objectType, allowSceneObjects, options);
        }

        private static int TooltipPopup(string label, int selectedIndex, string[] displayedOptions)
        {
            return EditorGUILayout.Popup(TooltipContent(label), selectedIndex, displayedOptions);
        }

        private static void DrawHeaderSection()
        {
            TooltipLabelField("Mediapipe Motion Gesture Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use keyframes to build a motion timeline. Save creates one JSON per motion in Assets/MotionGestures.", MessageType.None);
        }

        private void DrawMotionFileSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                TooltipLabelField("Motion File", EditorStyles.boldLabel);
                _motionFileName = TooltipTextField("Name", _motionFileName);
                TooltipLabelField("Save Folder", "Assets/MotionGestures", EditorStyles.miniLabel);
            }
        }

        private void DrawTimelineSection()
        {
            int selectedKeyframeCount = GetSelectedKeyframeCount();
            bool hasMultipleKeyframesSelected = selectedKeyframeCount > 1;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                TooltipLabelField("Timeline", EditorStyles.boldLabel);

                float duration = GetTimelineDuration();
                float updatedDuration = TooltipFloatField("Duration (seconds)", duration);
                if (!Mathf.Approximately(updatedDuration, duration))
                {
                    _motionDuration = Mathf.Max(0.1f, updatedDuration);
                    ClampKeyframeTimesToDuration();
                    SortKeyframesByTime();
                }

                DrawTimelineGraph(duration);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_keyframes.Count == 0))
                    {
                        if (TooltipButton("Prev", GUILayout.Height(20f)))
                        {
                            SelectKeyframe(_selectedKeyframeIndex - 1);
                        }

                        if (TooltipButton("Next", GUILayout.Height(20f)))
                        {
                            SelectKeyframe(_selectedKeyframeIndex + 1);
                        }
                    }

                    if (TooltipButton(_isPreviewPlaying ? "Stop Preview" : "Play Preview", GUILayout.Height(20f)))
                    {
                        if (_isPreviewPlaying)
                        {
                            StopPreviewPlayback();
                        }
                        else
                        {
                            StartPreviewPlayback();
                        }
                    }

                    if (TooltipButton("Add Keyframe", GUILayout.Height(20f)))
                    {
                        AddKeyframeAtCurrentTimelineTime();
                    }

                    if (TooltipButton("Duplicate", GUILayout.Height(20f)))
                    {
                        DuplicateSelectedKeyframe();
                    }

                    using (new EditorGUI.DisabledScope(_keyframes.Count <= 2 || _keyframes.Count - Mathf.Max(1, selectedKeyframeCount) < 2))
                    {
                        if (TooltipButton("Remove", GUILayout.Height(20f)))
                        {
                            RemoveSelectedKeyframe();
                        }
                    }

                    using (new EditorGUI.DisabledScope(_keyframes.Count == 0 || selectedKeyframeCount >= _keyframes.Count))
                    {
                        if (TooltipButton("Select All", GUILayout.Height(20f)))
                        {
                            SelectAllKeyframes();
                        }
                    }

                    if (TooltipButton("Normalize Time", GUILayout.Height(20f)))
                    {
                        NormalizeKeyframeTimes();
                    }

                    if (TooltipButton("Trim Timeline", GUILayout.Height(20f)))
                    {
                        TrimTimelineToKeyframes();
                    }

                    if (TooltipButton("Fit Duration To Keys", GUILayout.Height(20f)))
                    {
                        _motionDuration = Mathf.Max(0.1f, GetLastKeyframeTime());
                    }

                    using (new EditorGUI.DisabledScope(
                        _keyframes.Count == 0 ||
                        _selectedKeyframeIndex < 0 ||
                        _selectedKeyframeIndex >= _keyframes.Count ||
                        hasMultipleKeyframesSelected))
                    {
                        if (TooltipButton("Auto Fix Pose", GUILayout.Height(20f)))
                        {
                            AutoFixSelectedKeyframePose();
                        }
                    }
                }

                if (_keyframes.Count > 0 && _selectedKeyframeIndex >= 0 && _selectedKeyframeIndex < _keyframes.Count)
                {
                    KeyframeData selectedFrame = _keyframes[_selectedKeyframeIndex];
                    if (!hasMultipleKeyframesSelected)
                    {
                        float newTime = TooltipSlider("Selected Keyframe Time", selectedFrame.time, 0f, GetTimelineDuration());
                        if (!Mathf.Approximately(newTime, selectedFrame.time))
                        {
                            selectedFrame.time = newTime;
                            SortKeyframesByTime(selectedFrame);
                            LoadSelectedKeyframeIntoEditor();
                        }

                        TooltipLabelField("Selected", "Key " + (_selectedKeyframeIndex + 1) + " / " + _keyframes.Count + " @ " + selectedFrame.time.ToString("0.000") + "s", EditorStyles.miniLabel);
                    }
                    else
                    {
                        TooltipLabelField(
                            "Selected",
                            selectedKeyframeCount + " keyframes. Primary: Key " + (_selectedKeyframeIndex + 1) + " / " + _keyframes.Count + " @ " + selectedFrame.time.ToString("0.000") + "s",
                            EditorStyles.miniLabel);
                        EditorGUILayout.HelpBox(
                            "Drag a box in the timeline to select multiple keyframes. Reset, image-apply, and tracked-point changes affect all selected frames. Time editing and Auto Fix Pose use the primary keyframe only.",
                            MessageType.None);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Timeline has no keyframes.", MessageType.Warning);
                }
            }
        }

        private void DrawTimelineGraph(float duration)
        {
            SyncSelectedKeyframesWithTimeline();

            bool hasFrameStrip = _recordedVideoBackgroundFrames.Count > 0 || _recordedWebcamBackgroundFrames.Count > 0;
            float graphHeight = hasFrameStrip ? 124f : 66f;
            Rect rect = GUILayoutUtility.GetRect(0f, graphHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f));
            Rect innerRect = new Rect(rect.x + 8f, rect.y + 8f, Mathf.Max(1f, rect.width - 16f), Mathf.Max(1f, rect.height - 16f));
            EditorGUI.DrawRect(innerRect, new Color(0.18f, 0.18f, 0.18f));
            Rect frameStripRect;
            bool drewFrameStrip = DrawTimelineFrameStrip(innerRect, duration, out frameStripRect);

            Rect markerRect = innerRect;
            if (drewFrameStrip)
            {
                float markerTop = Mathf.Min(innerRect.yMax - 12f, frameStripRect.yMax + 3f);
                markerRect = new Rect(innerRect.xMin, markerTop, innerRect.width, Mathf.Max(12f, innerRect.yMax - markerTop));
                EditorGUI.DrawRect(markerRect, new Color(0.12f, 0.12f, 0.12f, 0.95f));
            }

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.1f);
            for (int i = 1; i < 10; i++)
            {
                float t = i / 10f;
                float x = Mathf.Lerp(markerRect.xMin, markerRect.xMax, t);
                Handles.DrawLine(new Vector2(x, markerRect.yMin), new Vector2(x, markerRect.yMax));
            }

            for (int i = 0; i < _keyframes.Count; i++)
            {
                Rect marker = GetTimelineKeyframeMarkerRect(i, markerRect, duration);
                bool isSelected = IsKeyframeSelected(i);
                Color markerColor = i == _selectedKeyframeIndex
                    ? new Color(1f, 0.76f, 0.2f, 1f)
                    : isSelected
                        ? new Color(0.94f, 0.88f, 0.36f, 0.95f)
                        : new Color(0.32f, 0.78f, 1f, 0.9f);
                EditorGUI.DrawRect(marker, markerColor);
            }

            float timelineTime = _isPreviewPlaying ? _previewTime : GetSelectedTimelineTime();
            float timelineNormalized = duration > 0f ? Mathf.Clamp01(timelineTime / duration) : 0f;
            float playheadX = Mathf.Lerp(innerRect.xMin, innerRect.xMax, timelineNormalized);
            Handles.color = new Color(1f, 0.35f, 0.35f, 0.95f);
            Handles.DrawAAPolyLine(2.5f, new Vector2(playheadX, innerRect.yMin), new Vector2(playheadX, markerRect.yMax));
            Handles.EndGUI();

            if (ShouldDrawTimelineSelectionRect())
            {
                Rect selectionRect = GetTimelineSelectionRect();
                EditorGUI.DrawRect(selectionRect, new Color(0.52f, 0.77f, 1f, 0.10f));
                DrawDottedRect(selectionRect, new Color(0.82f, 0.92f, 1f, 0.95f), TimelineSelectionDottedLineSize);
            }

            string timelineLabel = _isPreviewPlaying
                ? "Preview: " + timelineTime.ToString("0.000") + "s"
                : "Current: " + timelineTime.ToString("0.000") + "s";
            string timelineTooltip = _isPreviewPlaying
                ? "Current preview playback time in the motion timeline."
                : "Current timeline time of the primary selected keyframe.";
            EditorGUI.LabelField(
                new Rect(innerRect.x, rect.yMax - 16f, innerRect.width, 14f),
                TooltipContent(timelineLabel, timelineTooltip),
                EditorStyles.miniLabel);

            Event evt = Event.current;
            if (_isPreviewPlaying || evt == null)
            {
                return;
            }

            HandleTimelineSelectionInput(evt, innerRect, markerRect, duration);
        }

        private Rect GetTimelineKeyframeMarkerRect(int index, Rect markerRect, float duration)
        {
            KeyframeData frame = _keyframes[index];
            float normalizedTime = duration > 0f ? Mathf.Clamp01(frame.time / duration) : 0f;
            float x = Mathf.Lerp(markerRect.xMin, markerRect.xMax, normalizedTime);
            return new Rect(x - (TimelineMarkerWidth * 0.5f), markerRect.y + 1f, TimelineMarkerWidth, Mathf.Max(2f, markerRect.height - 2f));
        }

        private void HandleTimelineSelectionInput(Event evt, Rect interactionRect, Rect markerRect, float duration)
        {
            if (_keyframes.Count == 0)
            {
                return;
            }

            if (!_isTimelineSelectionDragging && !interactionRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && interactionRect.Contains(evt.mousePosition))
            {
                _isTimelineSelectionDragging = true;
                _timelineSelectionStart = evt.mousePosition;
                _timelineSelectionCurrent = evt.mousePosition;
                evt.Use();
                return;
            }

            if (!_isTimelineSelectionDragging)
            {
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                _timelineSelectionCurrent = ClampPointToRect(evt.mousePosition, interactionRect);
                Repaint();
                evt.Use();
                return;
            }

            if (evt.type != EventType.MouseUp || evt.button != 0)
            {
                return;
            }

            _timelineSelectionCurrent = ClampPointToRect(evt.mousePosition, interactionRect);
            bool draggedSelectionRect = ShouldDrawTimelineSelectionRect();
            _isTimelineSelectionDragging = false;

            if (draggedSelectionRect)
            {
                SelectKeyframesInTimelineRect(GetTimelineSelectionRect(), markerRect, duration);
            }
            else
            {
                float normalized = Mathf.InverseLerp(interactionRect.xMin, interactionRect.xMax, _timelineSelectionCurrent.x);
                float clickedTime = normalized * duration;
                int nearestIndex = GetNearestKeyframeIndex(clickedTime);
                if (nearestIndex >= 0)
                {
                    SelectKeyframe(nearestIndex);
                }
            }

            Repaint();
            evt.Use();
        }

        private void SelectKeyframesInTimelineRect(Rect selectionRect, Rect markerRect, float duration)
        {
            List<KeyframeData> selection = new List<KeyframeData>();
            int primaryIndex = -1;

            for (int i = 0; i < _keyframes.Count; i++)
            {
                Rect keyframeRect = GetTimelineKeyframeMarkerRect(i, markerRect, duration);
                if (!selectionRect.Overlaps(keyframeRect, true))
                {
                    continue;
                }

                selection.Add(_keyframes[i]);
                if (primaryIndex < 0)
                {
                    primaryIndex = i;
                }
            }

            if (selection.Count == 0)
            {
                return;
            }

            CommitEditorPoseToSelectedKeyframe();
            _selectedKeyframes.Clear();
            for (int i = 0; i < selection.Count; i++)
            {
                _selectedKeyframes.Add(selection[i]);
            }

            _selectedKeyframeIndex = primaryIndex;
            LoadSelectedKeyframeIntoEditor();
        }

        private bool ShouldDrawTimelineSelectionRect()
        {
            return _isTimelineSelectionDragging &&
                (_timelineSelectionCurrent - _timelineSelectionStart).sqrMagnitude >= TimelineSelectionDragThreshold * TimelineSelectionDragThreshold;
        }

        private Rect GetTimelineSelectionRect()
        {
            return Rect.MinMaxRect(
                Mathf.Min(_timelineSelectionStart.x, _timelineSelectionCurrent.x),
                Mathf.Min(_timelineSelectionStart.y, _timelineSelectionCurrent.y),
                Mathf.Max(_timelineSelectionStart.x, _timelineSelectionCurrent.x),
                Mathf.Max(_timelineSelectionStart.y, _timelineSelectionCurrent.y));
        }

        private static Vector2 ClampPointToRect(Vector2 point, Rect rect)
        {
            return new Vector2(
                Mathf.Clamp(point.x, rect.xMin, rect.xMax),
                Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        }

        private static void DrawDottedRect(Rect rect, Color color, float dottedLineSize)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Vector3 topLeft = new Vector3(rect.xMin, rect.yMin);
            Vector3 topRight = new Vector3(rect.xMax, rect.yMin);
            Vector3 bottomRight = new Vector3(rect.xMax, rect.yMax);
            Vector3 bottomLeft = new Vector3(rect.xMin, rect.yMax);
            Handles.DrawDottedLine(topLeft, topRight, dottedLineSize);
            Handles.DrawDottedLine(topRight, bottomRight, dottedLineSize);
            Handles.DrawDottedLine(bottomRight, bottomLeft, dottedLineSize);
            Handles.DrawDottedLine(bottomLeft, topLeft, dottedLineSize);
            Handles.EndGUI();
        }

        private bool DrawTimelineFrameStrip(Rect innerRect, float timelineDuration, out Rect stripRect)
        {
            stripRect = Rect.zero;
            List<WebcamBackgroundFrame> frames = null;
            if (_recordedVideoBackgroundFrames.Count > 0)
            {
                frames = _recordedVideoBackgroundFrames;
            }
            else if (_recordedWebcamBackgroundFrames.Count > 0)
            {
                frames = _recordedWebcamBackgroundFrames;
            }

            if (frames == null || frames.Count == 0 || innerRect.width <= 10f || innerRect.height <= 14f)
            {
                return false;
            }

            float stripHeight = Mathf.Clamp(innerRect.height * 0.62f, 28f, 56f);
            stripRect = new Rect(innerRect.xMin + 1f, innerRect.yMin + 1f, innerRect.width - 2f, stripHeight);
            EditorGUI.DrawRect(stripRect, new Color(0f, 0f, 0f, 0.24f));
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.14f);
            Handles.DrawAAPolyLine(
                1.2f,
                new Vector2(stripRect.xMin, stripRect.yMax),
                new Vector2(stripRect.xMax, stripRect.yMax));
            Handles.EndGUI();

            int maxThumbs = Mathf.Clamp(Mathf.FloorToInt(stripRect.width / 62f), 5, 28);
            int step = Mathf.Max(1, frames.Count / maxThumbs);
            float durationForMapping = Mathf.Max(0.0001f, timelineDuration);
            float thumbHeight = Mathf.Max(8f, stripRect.height - 4f);
            float lastDrawnRight = float.NegativeInfinity;
            float sourceDuration = ResolveRecordedBackgroundDuration(frames);

            if (_keyframes.Count > 0)
            {
                for (int i = 0; i < _keyframes.Count; i++)
                {
                    float normalized = durationForMapping > 0f
                        ? Mathf.Clamp01(_keyframes[i].time / durationForMapping)
                        : 0f;
                    float targetTime = normalized * sourceDuration;
                    if (!TryGetRecordedBackgroundTextureAtTime(frames, targetTime, out Texture2D texture))
                    {
                        continue;
                    }

                    DrawTimelineFrameThumbnailAtPosition(texture, normalized, stripRect, thumbHeight, ref lastDrawnRight);
                }
            }
            else
            {
                for (int i = 0; i < frames.Count; i += step)
                {
                    DrawTimelineFrameThumbnail(frames[i], sourceDuration, stripRect, thumbHeight, ref lastDrawnRight);
                }

                DrawTimelineFrameThumbnail(frames[frames.Count - 1], sourceDuration, stripRect, thumbHeight, ref lastDrawnRight);
            }
            return true;
        }

        private static void DrawTimelineFrameThumbnail(
            WebcamBackgroundFrame frame,
            float durationForMapping,
            Rect stripRect,
            float thumbHeight,
            ref float lastDrawnRight)
        {
            if (frame == null || frame.texture == null)
            {
                return;
            }

            float normalized = Mathf.Clamp01(frame.time / durationForMapping);
            DrawTimelineFrameThumbnailAtPosition(frame.texture, normalized, stripRect, thumbHeight, ref lastDrawnRight);
        }

        private static void DrawTimelineFrameThumbnailAtPosition(
            Texture2D texture,
            float normalized,
            Rect stripRect,
            float thumbHeight,
            ref float lastDrawnRight)
        {
            if (texture == null)
            {
                return;
            }

            float centerX = Mathf.Lerp(stripRect.xMin, stripRect.xMax, normalized);
            float aspect = Mathf.Max(0.5f, texture.width / (float)Mathf.Max(1, texture.height));
            float thumbWidth = Mathf.Clamp(thumbHeight * aspect, thumbHeight * 0.9f, thumbHeight * 2.8f);

            float x = Mathf.Clamp(centerX - (thumbWidth * 0.5f), stripRect.xMin, stripRect.xMax - thumbWidth);
            if (x < lastDrawnRight + 3f)
            {
                return;
            }

            Rect thumbnailRect = new Rect(x, stripRect.y + 2f, thumbWidth, thumbHeight);
            GUI.DrawTexture(thumbnailRect, texture, ScaleMode.ScaleAndCrop, true);
            lastDrawnRight = thumbnailRect.xMax;
        }

        private float ResolveRecordedBackgroundDuration(List<WebcamBackgroundFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0.0001f;
            }

            float duration;
            if (ReferenceEquals(frames, _recordedVideoBackgroundFrames))
            {
                duration = _recordedVideoBackgroundDuration > 0f
                    ? _recordedVideoBackgroundDuration
                    : frames[frames.Count - 1].time;
            }
            else if (ReferenceEquals(frames, _recordedWebcamBackgroundFrames))
            {
                duration = _recordedWebcamBackgroundDuration > 0f
                    ? _recordedWebcamBackgroundDuration
                    : frames[frames.Count - 1].time;
            }
            else
            {
                duration = frames[frames.Count - 1].time;
            }

            return Mathf.Max(0.0001f, duration);
        }

        private void DrawMotionActionsSection()
        {
            int selectedKeyframeCount = GetSelectedKeyframeCount();
            string resetButtonLabel = selectedKeyframeCount > 1 ? "Reset Selected Keys" : "Reset Current Key";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                TooltipLabelField("Motion Actions", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (TooltipButton(resetButtonLabel, GUILayout.Height(22f)))
                    {
                        CopyPose(_defaultLandmarks, _landmarks);
                        InitializeDefaultTrackedPoints();
                        _hasWorldLandmarksForEditorPose = false;
                        RebuildBoneConstraintsFromCurrentPose();
                        CommitEditorPoseToSelectedKeyframes();
                        _selectedJoint = -1;
                    }

                    if (TooltipButton("Use Current As Baseline", GUILayout.Height(22f)))
                    {
                        RebuildBoneConstraintsFromCurrentPose();
                        CommitEditorPoseToSelectedKeyframe();
                    }
                }

                if (TooltipButton("Reset Editor", GUILayout.Height(22f)))
                {
                    bool resetConfirmed = EditorUtility.DisplayDialog(
                        "Reset Motion Gesture Editor",
                        "Clear the current timeline, imported media, and editor state and start from a new default motion?",
                        "Reset",
                        "Cancel");

                    if (resetConfirmed)
                    {
                        ResetEditorState();
                        ShowNotification(new GUIContent("Editor reset."));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (TooltipButton("Save JSON", GUILayout.Height(22f)))
                    {
                        SaveMotionToJson();
                    }

                    if (TooltipButton("Load JSON", GUILayout.Height(22f)))
                    {
                        LoadMotionFromJson();
                    }

                    if (TooltipButton("Copy JSON", GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = ToJsonString(pretty: true);
                    }
                }
            }
        }

        private void DrawEditorSettingsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                TooltipLabelField("Editor Settings", EditorStyles.boldLabel);
                _showLabels = TooltipToggleLeft("Show Labels", _showLabels);
                _lockTorso = TooltipToggleLeft("Lock Torso", _lockTorso);
                _clampToCanvas = TooltipToggleLeft("Clamp To 0-1", _clampToCanvas);
                _pointSize = TooltipSlider("Point Size", _pointSize, 4f, 10f);
                _solverIterations = TooltipIntSlider("Solver Iterations", _solverIterations, 8, 40);
            }
        }

        private void DrawSelectedJointSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_selectedJoint >= 0 && _selectedJoint < LandmarkCount)
                {
                    TooltipLabelField("Selected Joint", EditorStyles.boldLabel);
                    TooltipLabelField(
                        LandmarkNames[_selectedJoint] + " (" + _selectedJoint + ")",
                        tooltipOverride: "Name and index of the joint currently selected on the canvas.");
                    TooltipLabelField("x", _landmarks[_selectedJoint].x.ToString("0.000"));
                    TooltipLabelField("y", _landmarks[_selectedJoint].y.ToString("0.000"));
                }
                else
                {
                    TooltipLabelField("Selected Joint", "None");
                }
            }
        }

        private void DrawTrackedPointsSection()
        {
            int trackedCount = GetTrackedPointCount();
            int selectedKeyframeCount = GetSelectedKeyframeCount();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string title = "Tracked Points (" + trackedCount + ")";
                if (selectedKeyframeCount > 1)
                {
                    title += " for " + selectedKeyframeCount + " keys";
                }

                _showTrackedPoints = TooltipFoldout(_showTrackedPoints, title, true, "Toggle and edit which landmarks are tracked for the selected keyframes.");
                if (!_showTrackedPoints)
                {
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (TooltipButton("Track All Visible", GUILayout.Height(20f)))
                    {
                        SetTrackedPointsForVisibleLandmarks(true);
                        CommitTrackedPointsToSelectedKeyframes();
                    }

                    if (TooltipButton("Clear Tracked", GUILayout.Height(20f)))
                    {
                        SetTrackedPointsForVisibleLandmarks(false);
                        CommitTrackedPointsToSelectedKeyframes();
                    }
                }

                EditorGUI.BeginChangeCheck();
                bool drewCenterPoints = false;
                for (int i = 0; i < VisibleLandmarkIndices.Length; i++)
                {
                    int index = VisibleLandmarkIndices[i];
                    if (GetLandmarkSidePrefix(index) != null)
                    {
                        continue;
                    }

                    if (!drewCenterPoints)
                    {
                        TooltipLabelField("Center", EditorStyles.miniBoldLabel);
                        drewCenterPoints = true;
                    }

                    DrawTrackedPointToggle(index);
                }

                if (drewCenterPoints)
                {
                    EditorGUILayout.Space(4f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawTrackedPointColumn("Left", "Left");
                    DrawTrackedPointColumn("Right", "Right");
                }

                if (EditorGUI.EndChangeCheck())
                {
                    CommitTrackedPointsToSelectedKeyframes();
                }

                if (trackedCount < 2)
                {
                    EditorGUILayout.HelpBox("Track at least 2 points per keyframe for reliable motion detection.", MessageType.Warning);
                }
            }
        }

        private void DrawTrackedPointColumn(string title, string sidePrefix)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                TooltipLabelField(title, EditorStyles.miniBoldLabel);
                for (int i = 0; i < VisibleLandmarkIndices.Length; i++)
                {
                    int index = VisibleLandmarkIndices[i];
                    if (!string.Equals(GetLandmarkSidePrefix(index), sidePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    DrawTrackedPointToggle(index);
                }
            }
        }

        private void DrawTrackedPointToggle(int index)
        {
            string label = index + ": " + LandmarkNames[index];
            string tooltip = "Enable or disable tracking for " + LandmarkNames[index] + " on the selected keyframes.";
            _trackedPoints[index] = EditorGUILayout.ToggleLeft(TooltipContent(label, tooltip), _trackedPoints[index]);
        }

        private static string GetLandmarkSidePrefix(int index)
        {
            if (index < 0 || index >= LandmarkNames.Length)
            {
                return null;
            }

            string landmarkName = LandmarkNames[index];
            if (landmarkName.StartsWith("Left", StringComparison.Ordinal))
            {
                return "Left";
            }

            if (landmarkName.StartsWith("Right", StringComparison.Ordinal))
            {
                return "Right";
            }

            return null;
        }

        private void DrawCanvas()
        {
            GUILayout.Space(6f);
            Rect area = GUILayoutUtility.GetRect(10f, 10000f, 260f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _canvasHostRect = new Rect(area.x + 8f, area.y + 8f, area.width - 16f, area.height - 16f);
            _canvasRect = ResolveFixedPoseViewRect(_canvasHostRect);
            _normalizedSpaceRect = ResolveNormalizedSpaceRect();

            EditorGUI.DrawRect(_canvasHostRect, new Color(0.10f, 0.10f, 0.10f));
            EditorGUI.DrawRect(_canvasRect, new Color(0.13f, 0.13f, 0.13f));
            EditorGUI.DrawRect(_normalizedSpaceRect, new Color(0.17f, 0.17f, 0.17f));
            DrawBodyReferenceImage();
            DrawGrid(_normalizedSpaceRect, 10, new Color(1f, 1f, 1f, 0.08f));

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.14f);
            Handles.DrawAAPolyLine(2f, new Vector3(_normalizedSpaceRect.xMin, _normalizedSpaceRect.center.y), new Vector3(_normalizedSpaceRect.xMax, _normalizedSpaceRect.center.y));
            Handles.DrawAAPolyLine(2f, new Vector3(_normalizedSpaceRect.center.x, _normalizedSpaceRect.yMin), new Vector3(_normalizedSpaceRect.center.x, _normalizedSpaceRect.yMax));
            Handles.color = new Color(1f, 1f, 1f, 0.20f);
            Handles.DrawPolyLine(
                new Vector3(_normalizedSpaceRect.xMin, _normalizedSpaceRect.yMin),
                new Vector3(_normalizedSpaceRect.xMax, _normalizedSpaceRect.yMin),
                new Vector3(_normalizedSpaceRect.xMax, _normalizedSpaceRect.yMax),
                new Vector3(_normalizedSpaceRect.xMin, _normalizedSpaceRect.yMax),
                new Vector3(_normalizedSpaceRect.xMin, _normalizedSpaceRect.yMin));
            Handles.EndGUI();
        }

        private static Rect ResolveFixedPoseViewRect(Rect hostRect)
        {
            if (hostRect.width <= 0f || hostRect.height <= 0f)
            {
                return hostRect;
            }

            float scale = Mathf.Min(1f, hostRect.width / PoseViewBaseWidth, hostRect.height / PoseViewBaseHeight);
            float width = PoseViewBaseWidth * scale;
            float height = PoseViewBaseHeight * scale;
            float x = hostRect.x + (hostRect.width - width) * 0.5f;
            float y = hostRect.y + (hostRect.height - height) * 0.5f;
            return new Rect(x, y, width, height);
        }

        private void DrawImageInputSection()
        {
            string applyButtonLabel = GetSelectedKeyframeCount() > 1
                ? "Apply Pose To Selected Keys"
                : "Apply Pose To Selected Key";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                TooltipLabelField("Image To Keyframe", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Load a body image and apply the detected pose to the selected keyframe.", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D nextImage = (Texture2D)TooltipObjectField("Body Image", _bodyReferenceImage, typeof(Texture2D), false);
                    if (nextImage != _bodyReferenceImage)
                    {
                        if (_runtimeLoadedBodyImage != null && nextImage != _runtimeLoadedBodyImage)
                        {
                            ReleaseRuntimeLoadedImage();
                        }

                        _bodyReferenceImage = nextImage;
                    }

                    if (TooltipButton("Load File", GUILayout.Width(85f)))
                    {
                        LoadBodyImageFromDisk();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _showBodyReferenceImage = TooltipToggleLeft("Show Image", _showBodyReferenceImage, GUILayout.Width(95f));
                    _flipImportedImageX = TooltipToggleLeft("Flip X", _flipImportedImageX, GUILayout.Width(62f));
                }

                _bodyImageOpacity = TooltipSlider("Image Opacity", _bodyImageOpacity, 0f, 1f);

                if (TooltipButton(applyButtonLabel, GUILayout.Height(22f)))
                {
                    ApplyBodyPoseFromImageToSelectedKeyframe();
                }
            }
        }

        private void DrawVideoInputSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                TooltipLabelField("Video To Timeline", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Extract pose keyframes from a video and build a motion timeline.", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    TooltipPrefixLabel("Video File");
                    EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(_videoFilePath) ? "(none)" : _videoFilePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (TooltipButton("Pick Video File", GUILayout.Height(20f)))
                    {
                        string selectedPath = EditorUtility.OpenFilePanel("Select Motion Video", Application.dataPath, "mp4,mov,m4v,avi,mpeg,mpg,webm");
                        if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
                        {
                            SetVideoFilePath(selectedPath);
                        }
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_videoFilePath)))
                    {
                        if (TooltipButton("Clear", GUILayout.Height(20f)))
                        {
                            SetVideoFilePath(string.Empty);
                        }
                    }
                }

                _flipImportedVideoX = TooltipToggleLeft("Flip Video X", _flipImportedVideoX);
                _showVideoBackground = TooltipToggleLeft("Show Video Background", _showVideoBackground);
                using (new EditorGUI.DisabledScope(!_showVideoBackground))
                {
                    _videoBackgroundOpacity = TooltipSlider("Video Opacity", _videoBackgroundOpacity, 0f, 1f);
                }
                _videoSampleRate = TooltipIntSlider("Sample Rate (fps)", _videoSampleRate, 2, 8);
                _videoMaxDuration = TooltipFloatField("Max Duration (sec)", _videoMaxDuration);
                _videoMaxFrames = TooltipIntField("Max Frames", _videoMaxFrames);
                _videoMaxDuration = Mathf.Max(0.1f, _videoMaxDuration);
                _videoMaxFrames = Mathf.Clamp(_videoMaxFrames, 2, 240);

                if (_videoImport != null)
                {
                    float progress = _videoImport.targetFrameCount > 0
                        ? _videoImport.sampledFrameCount / (float)_videoImport.targetFrameCount
                        : 0f;
                    Rect progressRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(progressRect, Mathf.Clamp01(progress), "Sampling " + _videoImport.sampledFrameCount + " / " + _videoImport.targetFrameCount + " frames");
                    TooltipLabelField("Detected Frames", _videoImport.detectedFrameCount.ToString(), EditorStyles.miniLabel);

                    if (TooltipButton("Cancel Video Import", GUILayout.Height(22f)))
                    {
                        CancelVideoImport();
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(_webcamRecord != null))
                    {
                        if (TooltipButton("Generate Timeline From Video", GUILayout.Height(22f)))
                        {
                            QueueStartVideoImport();
                        }
                    }
                }

                EditorGUILayout.Space(8f);
                TooltipLabelField("Webcam Recorder", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Record webcam frames and generate timeline directly. You can record manually or set start/stop times.", MessageType.None);

                RefreshWebcamDeviceList();
                if (_webcamDeviceNames.Length == 0)
                {
                    EditorGUILayout.HelpBox("No webcam detected.", MessageType.Warning);
                }
                else
                {
                    _selectedWebcamDeviceIndex = TooltipPopup("Webcam Device", _selectedWebcamDeviceIndex, _webcamDeviceNames);
                }

                _useTimedWebcamRecording = TooltipToggleLeft("Use Timed Start/Stop", _useTimedWebcamRecording);
                if (_useTimedWebcamRecording)
                {
                    _webcamTimedStartAtSeconds = TooltipFloatField("Start At (sec)", _webcamTimedStartAtSeconds);
                    _webcamTimedStopAtSeconds = TooltipFloatField("Stop At (sec)", _webcamTimedStopAtSeconds);
                    _webcamTimedStartAtSeconds = Mathf.Max(0f, _webcamTimedStartAtSeconds);
                    _webcamTimedStopAtSeconds = Mathf.Max(_webcamTimedStartAtSeconds + 0.1f, _webcamTimedStopAtSeconds);
                }

                if (_webcamRecord != null)
                {
                    string status;
                    if (!_webcamRecord.webcamReady)
                    {
                        status = "Initializing webcam...";
                    }
                    else if (!_webcamRecord.recordingActive)
                    {
                        status = _webcamRecord.autoStop
                            ? "Waiting for start time..."
                            : "Preparing manual recording...";
                    }
                    else
                    {
                        status = "Recording...";
                    }

                    float progress = _webcamRecord.targetFrameCount > 0
                        ? _webcamRecord.sampledFrameCount / (float)_webcamRecord.targetFrameCount
                        : 0f;
                    Rect progressRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(
                        progressRect,
                        Mathf.Clamp01(progress),
                        status + " " + _webcamRecord.sampledFrameCount + " / " + _webcamRecord.targetFrameCount + " frames");
                    TooltipLabelField("Detected Frames", _webcamRecord.detectedFrameCount.ToString(), EditorStyles.miniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (TooltipButton("Stop Webcam Recording", GUILayout.Height(22f)))
                        {
                            StopWebcamRecording();
                        }

                        if (TooltipButton("Cancel", GUILayout.Height(22f)))
                        {
                            CancelWebcamRecording();
                        }
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(_videoImport != null || _webcamDeviceNames.Length == 0))
                    {
                        if (TooltipButton(_useTimedWebcamRecording ? "Start Timed Webcam Recording" : "Start Webcam Recording", GUILayout.Height(22f)))
                        {
                            StartWebcamRecording();
                        }
                    }
                }

                if (_recordedWebcamBackgroundFrames.Count > 0)
                {
                    float duration = _recordedWebcamBackgroundDuration > 0f
                        ? _recordedWebcamBackgroundDuration
                        : _recordedWebcamBackgroundFrames[_recordedWebcamBackgroundFrames.Count - 1].time;
                    TooltipLabelField(
                        "Recorded Webcam",
                        _recordedWebcamBackgroundFrames.Count + " frame(s), " + duration.ToString("0.00") + "s",
                        EditorStyles.miniLabel);

                    using (new EditorGUI.DisabledScope(_webcamRecord != null))
                    {
                        if (TooltipButton("Clear Recorded Webcam Frames", GUILayout.Height(20f)))
                        {
                            ClearRecordedWebcamBackgroundFrames();
                        }
                    }
                }
            }
        }

        private void QueueStartVideoImport()
        {
            if (_videoImport != null || _videoImportStartQueued || _webcamRecord != null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_videoFilePath) && File.Exists(_videoFilePath))
            {
                _showVideoBackground = true;
                if (_videoBackgroundPlayer != null && _videoBackgroundPlayer.isPrepared)
                {
                    ShowVideoBackgroundFirstFrame();
                }
                else
                {
                    _videoBackgroundShowFirstFrameOnPrepare = true;
                }

                EnsureVideoBackgroundState();
                EditorApplication.QueuePlayerLoopUpdate();
                Repaint();
            }

            _videoImportStartQueued = true;
            EditorApplication.delayCall += ExecuteQueuedVideoImportStart;
        }

        private void ExecuteQueuedVideoImportStart()
        {
            EditorApplication.delayCall -= ExecuteQueuedVideoImportStart;
            _videoImportStartQueued = false;

            if (this == null)
            {
                return;
            }

            StartVideoImport();
        }

        private void SetVideoFilePath(string path)
        {
            string normalizedPath = path ?? string.Empty;
            if (string.Equals(_videoFilePath, normalizedPath, StringComparison.Ordinal))
            {
                return;
            }

            _videoFilePath = normalizedPath;
            _lastVideoBackgroundTime = -1d;
            _lastVideoBackgroundFrame = -1L;
            _videoBackgroundShowFirstFrameOnPrepare = false;
            ClearRecordedVideoBackgroundFrames();

            if (string.IsNullOrEmpty(_videoFilePath) || !string.Equals(_videoBackgroundLoadedPath, _videoFilePath, StringComparison.Ordinal))
            {
                ReleaseVideoBackgroundPlayer();
            }

            bool hasValidVideoPath = !string.IsNullOrEmpty(_videoFilePath) && File.Exists(_videoFilePath);
            if (!hasValidVideoPath)
            {
                return;
            }

            _showVideoBackground = true;
            _videoBackgroundShowFirstFrameOnPrepare = true;
            EnsureVideoBackgroundState();
            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }

        private void RefreshWebcamDeviceList()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            int deviceCount = devices != null ? devices.Length : 0;
            if (deviceCount <= 0)
            {
                _webcamDeviceNames = Array.Empty<string>();
                _selectedWebcamDeviceIndex = 0;
                return;
            }

            bool needsRefresh = _webcamDeviceNames == null || _webcamDeviceNames.Length != deviceCount;
            if (!needsRefresh)
            {
                for (int i = 0; i < deviceCount; i++)
                {
                    if (!string.Equals(_webcamDeviceNames[i], devices[i].name, StringComparison.Ordinal))
                    {
                        needsRefresh = true;
                        break;
                    }
                }
            }

            if (!needsRefresh)
            {
                _selectedWebcamDeviceIndex = Mathf.Clamp(_selectedWebcamDeviceIndex, 0, _webcamDeviceNames.Length - 1);
                return;
            }

            _webcamDeviceNames = new string[deviceCount];
            for (int i = 0; i < deviceCount; i++)
            {
                string deviceName = devices[i].name;
                _webcamDeviceNames[i] = string.IsNullOrEmpty(deviceName) ? "Webcam " + (i + 1) : deviceName;
            }

            _selectedWebcamDeviceIndex = Mathf.Clamp(_selectedWebcamDeviceIndex, 0, _webcamDeviceNames.Length - 1);
        }

        private void StartWebcamRecording()
        {
            if (_videoImport != null)
            {
                EditorUtility.DisplayDialog("Video Import Running", "Finish or cancel video import first.", "OK");
                return;
            }

            if (_videoImportStartQueued)
            {
                EditorApplication.delayCall -= ExecuteQueuedVideoImportStart;
                _videoImportStartQueued = false;
            }

            if (_webcamRecord != null)
            {
                return;
            }

            RefreshWebcamDeviceList();
            if (_webcamDeviceNames.Length == 0)
            {
                EditorUtility.DisplayDialog("No Webcam Found", "Connect a webcam and try again.", "OK");
                return;
            }

            _selectedWebcamDeviceIndex = Mathf.Clamp(_selectedWebcamDeviceIndex, 0, _webcamDeviceNames.Length - 1);

            string modelPath = ResolvePoseLandmarkerModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                EditorUtility.DisplayDialog("Model Missing", "Could not find pose model '" + DefaultPoseLandmarkerModelName + "' in StreamingAssets.", "OK");
                return;
            }

            WebcamRecordContext context = new WebcamRecordContext();
            try
            {
                _showVideoBackground = true;
                ClearRecordedVideoBackgroundFrames();
                context.flipX = _flipImportedVideoX;
                context.sampleInterval = 1f / Mathf.Clamp(_videoSampleRate, 2, 8);
                context.captureStartDelay = _useTimedWebcamRecording ? Mathf.Max(0f, _webcamTimedStartAtSeconds) : 0f;
                context.captureDuration = Mathf.Max(0.1f, _videoMaxDuration);
                context.autoStop = _useTimedWebcamRecording;
                if (_useTimedWebcamRecording)
                {
                    float stopAt = Mathf.Max(context.captureStartDelay + 0.1f, _webcamTimedStopAtSeconds);
                    context.captureDuration = stopAt - context.captureStartDelay;
                }

                float targetDuration = context.autoStop ? context.captureDuration : Mathf.Max(0.1f, _videoMaxDuration);
                context.targetFrameCount = Mathf.Clamp(
                    Mathf.FloorToInt(targetDuration / context.sampleInterval) + 1,
                    2,
                    _videoMaxFrames);
                context.startedEditorTime = EditorApplication.timeSinceStartup;

                int requestFps = Mathf.Clamp(Mathf.Clamp(_videoSampleRate, 2, 8) * 2, 10, 30);
                context.webcamTexture = new WebCamTexture(
                    _webcamDeviceNames[_selectedWebcamDeviceIndex],
                    WebcamCaptureWidth,
                    WebcamCaptureHeight,
                    requestFps);
                context.webcamTexture.Play();

                var options = new PoseLandmarkerOptions(
                    new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: modelPath),
                    runningMode: RunningMode.IMAGE,
                    numPoses: 1,
                    minPoseDetectionConfidence: 0.5f,
                    minPosePresenceConfidence: 0.5f,
                    minTrackingConfidence: 0.5f,
                    outputSegmentationMasks: false);
                context.poseLandmarker = PoseLandmarker.CreateFromOptions(options);

                _webcamRecord = context;
                EditorApplication.update += OnWebcamRecordingUpdate;
                EditorApplication.QueuePlayerLoopUpdate();
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                context.Dispose();
                _webcamRecord = null;
                EditorApplication.update -= OnWebcamRecordingUpdate;
                EditorUtility.DisplayDialog("Webcam Recording Failed", "Could not start webcam recording:\n" + exception.Message, "OK");
            }
        }

        private void StopWebcamRecording()
        {
            if (_webcamRecord == null)
            {
                return;
            }

            if (_webcamRecord.recordingActive)
            {
                FinalizeWebcamRecording(success: true);
            }
            else
            {
                CancelWebcamRecording(showDialog: false);
            }
        }

        private void CancelWebcamRecording(bool showDialog = true)
        {
            if (_webcamRecord == null)
            {
                return;
            }

            FinalizeWebcamRecording(
                success: false,
                error: showDialog ? "Webcam recording canceled." : null,
                showFailureDialog: showDialog);
        }

        private void OnWebcamRecordingUpdate()
        {
            if (_webcamRecord == null)
            {
                EditorApplication.update -= OnWebcamRecordingUpdate;
                return;
            }

            WebcamRecordContext context = _webcamRecord;
            if (context.webcamTexture == null)
            {
                FinalizeWebcamRecording(success: false, error: "Webcam device is not available.");
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!context.webcamReady)
            {
                bool ready = context.webcamTexture.isPlaying &&
                    context.webcamTexture.width > 16 &&
                    context.webcamTexture.height > 16 &&
                    context.webcamTexture.didUpdateThisFrame;
                if (!ready)
                {
                    if (now - context.startedEditorTime > WebcamInitTimeoutSeconds)
                    {
                        FinalizeWebcamRecording(success: false, error: "Webcam initialization timed out.");
                        return;
                    }

                    EditorApplication.QueuePlayerLoopUpdate();
                    Repaint();
                    return;
                }

                context.webcamReady = true;
                context.recordingStartEditorTime = now + context.captureStartDelay;
                context.nextSampleTime = 0f;
                context.sampledFrameCount = 0;
                context.detectedFrameCount = 0;
            }

            if (!context.recordingActive)
            {
                if (now < context.recordingStartEditorTime)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    Repaint();
                    return;
                }

                context.recordingActive = true;
                context.recordingStartEditorTime = now;
            }

            float elapsed = Mathf.Max(0f, (float)(now - context.recordingStartEditorTime));
            while (context.sampledFrameCount < _videoMaxFrames && elapsed + 0.0005f >= context.nextSampleTime)
            {
                if (context.autoStop && context.nextSampleTime > context.captureDuration + 0.0005f)
                {
                    break;
                }

                TryCaptureWebcamSample(context, context.nextSampleTime);
                context.nextSampleTime += context.sampleInterval;
            }

            if (context.autoStop && elapsed >= context.captureDuration - 0.0005f)
            {
                FinalizeWebcamRecording(success: true);
                return;
            }

            if (context.sampledFrameCount >= _videoMaxFrames)
            {
                FinalizeWebcamRecording(success: true);
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }

        private void TryCaptureWebcamSample(WebcamRecordContext context, float sampleTime)
        {
            if (context == null || context.webcamTexture == null || context.poseLandmarker == null)
            {
                return;
            }

            int width = Mathf.Max(16, context.webcamTexture.width);
            int height = Mathf.Max(16, context.webcamTexture.height);
            if (context.readbackTexture == null || context.readbackTexture.width != width || context.readbackTexture.height != height)
            {
                if (context.readbackTexture != null)
                {
                    DestroyImmediate(context.readbackTexture);
                }

                context.readbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                context.pixelBuffer = null;
            }

            Color32[] pixels;
            try
            {
                pixels = context.webcamTexture.GetPixels32(context.pixelBuffer);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("MotionGestureEditor webcam sample failed: " + exception.Message);
                return;
            }

            if (pixels == null || pixels.Length == 0)
            {
                return;
            }

            context.pixelBuffer = pixels;
            context.readbackTexture.SetPixels32(pixels);
            context.readbackTexture.Apply(false, false);

            Texture2D sampledTexture = CreatePreviewFrameTexture(context.readbackTexture, TimelinePreviewFrameMaxDimension);
            if (sampledTexture != null)
            {
                context.sampledFrames.Add(new WebcamBackgroundFrame(sampleTime, sampledTexture));
            }
            context.sampledFrameCount++;

            if (!TryExtractPoseFromTexture(
                context.readbackTexture,
                context.poseLandmarker,
                context.flipX,
                out Vector2[] detectedLandmarks,
                out bool[] detectedTrackedPoints,
                out Vector3[] detectedWorldLandmarks,
                out bool hasDetectedWorldLandmarks,
                out _))
            {
                return;
            }

            context.detectedFrameCount++;
            context.capturedFrames.Add(
                new KeyframeData(
                    sampleTime,
                    detectedLandmarks,
                    detectedTrackedPoints,
                    detectedWorldLandmarks,
                    hasDetectedWorldLandmarks));
        }

        private void FinalizeWebcamRecording(bool success, string error = null, bool showFailureDialog = true)
        {
            if (_webcamRecord == null)
            {
                return;
            }

            WebcamRecordContext context = _webcamRecord;
            _webcamRecord = null;
            EditorApplication.update -= OnWebcamRecordingUpdate;

            if (success && context.capturedFrames.Count >= 2)
            {
                context.capturedFrames.Sort((a, b) => a.time.CompareTo(b.time));
                float firstPoseFrameTime = context.capturedFrames[0].time;
                for (int i = 0; i < context.capturedFrames.Count; i++)
                {
                    context.capturedFrames[i].time = Mathf.Max(0f, context.capturedFrames[i].time - firstPoseFrameTime);
                }

                _keyframes.Clear();
                for (int i = 0; i < context.capturedFrames.Count; i++)
                {
                    _keyframes.Add(context.capturedFrames[i].Clone());
                }

                _motionDuration = Mathf.Max(0.1f, _keyframes[_keyframes.Count - 1].time);
                _selectedKeyframeIndex = 0;
                LoadSelectedKeyframeIntoEditor();

                ClearRecordedVideoBackgroundFrames();
                ClearRecordedWebcamBackgroundFrames();
                if (context.sampledFrames.Count > 0)
                {
                    context.sampledFrames.Sort((a, b) => a.time.CompareTo(b.time));
                    float firstSampleTime = context.sampledFrames[0].time;
                    for (int i = 0; i < context.sampledFrames.Count; i++)
                    {
                        WebcamBackgroundFrame frame = context.sampledFrames[i];
                        frame.time = Mathf.Max(0f, frame.time - firstSampleTime);
                        _recordedWebcamBackgroundFrames.Add(frame);
                    }
                    context.sampledFrames.Clear();

                    _recordedWebcamBackgroundDuration = _recordedWebcamBackgroundFrames[_recordedWebcamBackgroundFrames.Count - 1].time;
                    if (_recordedWebcamBackgroundDuration <= 0f)
                    {
                        _recordedWebcamBackgroundDuration = _motionDuration;
                    }
                }
                else
                {
                    _recordedWebcamBackgroundDuration = 0f;
                }

                _videoFilePath = string.Empty;
                ReleaseVideoBackgroundPlayer();
                _showVideoBackground = true;

                if (string.IsNullOrWhiteSpace(_motionFileName))
                {
                    _motionFileName = "webcam_motion_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }

                EditorUtility.DisplayDialog(
                    "Webcam Recording Complete",
                    "Sampled " + context.sampledFrameCount + " frames.\nDetected pose in " + context.detectedFrameCount + " frames.\nCreated " + _keyframes.Count + " keyframes.",
                    "OK");
            }
            else if (showFailureDialog && !string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Webcam Recording Failed", error, "OK");
            }
            else if (showFailureDialog)
            {
                EditorUtility.DisplayDialog("Webcam Recording Failed", "Could not detect enough poses to build a timeline.", "OK");
            }

            context.Dispose();
            Repaint();
        }

        private void ClearRecordedWebcamBackgroundFrames()
        {
            for (int i = 0; i < _recordedWebcamBackgroundFrames.Count; i++)
            {
                _recordedWebcamBackgroundFrames[i].Dispose();
            }

            _recordedWebcamBackgroundFrames.Clear();
            _recordedWebcamBackgroundDuration = 0f;
        }

        private void ClearRecordedVideoBackgroundFrames()
        {
            for (int i = 0; i < _recordedVideoBackgroundFrames.Count; i++)
            {
                _recordedVideoBackgroundFrames[i].Dispose();
            }

            _recordedVideoBackgroundFrames.Clear();
            _recordedVideoBackgroundDuration = 0f;
        }

        private void TrimRecordedBackgroundFramesToTimelineRange(float trimStartTime, float trimEndTime)
        {
            TrimRecordedBackgroundFrames(_recordedVideoBackgroundFrames, ref _recordedVideoBackgroundDuration, trimStartTime, trimEndTime);
            TrimRecordedBackgroundFrames(_recordedWebcamBackgroundFrames, ref _recordedWebcamBackgroundDuration, trimStartTime, trimEndTime);
        }

        private static void TrimRecordedBackgroundFrames(
            List<WebcamBackgroundFrame> frames,
            ref float storedDuration,
            float trimStartTime,
            float trimEndTime)
        {
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            float start = Mathf.Max(0f, trimStartTime);
            float end = Mathf.Max(start, trimEndTime);
            float duration = Mathf.Max(0.0001f, end - start);

            int nearestStartIndex = 0;
            int nearestEndIndex = 0;
            float nearestStartDistance = float.PositiveInfinity;
            float nearestEndDistance = float.PositiveInfinity;
            for (int i = 0; i < frames.Count; i++)
            {
                WebcamBackgroundFrame frame = frames[i];
                if (frame == null)
                {
                    continue;
                }

                float startDistance = Mathf.Abs(frame.time - start);
                if (startDistance < nearestStartDistance)
                {
                    nearestStartDistance = startDistance;
                    nearestStartIndex = i;
                }

                float endDistance = Mathf.Abs(frame.time - end);
                if (endDistance < nearestEndDistance)
                {
                    nearestEndDistance = endDistance;
                    nearestEndIndex = i;
                }
            }

            List<WebcamBackgroundFrame> keptFrames = new List<WebcamBackgroundFrame>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                WebcamBackgroundFrame frame = frames[i];
                if (frame == null)
                {
                    continue;
                }

                bool inRange = frame.time >= start - 0.0005f && frame.time <= end + 0.0005f;
                bool keep = inRange || i == nearestStartIndex || i == nearestEndIndex;
                if (keep)
                {
                    keptFrames.Add(frame);
                }
                else
                {
                    frame.Dispose();
                }
            }

            frames.Clear();
            if (keptFrames.Count == 0)
            {
                storedDuration = 0f;
                return;
            }

            keptFrames.Sort((a, b) => a.time.CompareTo(b.time));
            for (int i = 0; i < keptFrames.Count; i++)
            {
                WebcamBackgroundFrame frame = keptFrames[i];
                frame.time = Mathf.Clamp(frame.time - start, 0f, duration);
                frames.Add(frame);
            }

            storedDuration = duration;
        }

        private static bool TryGetRecordedBackgroundTextureAtTime(
            List<WebcamBackgroundFrame> frames,
            float targetTime,
            out Texture2D texture)
        {
            texture = null;
            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            int nearestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < frames.Count; i++)
            {
                WebcamBackgroundFrame frame = frames[i];
                if (frame == null || frame.texture == null)
                {
                    continue;
                }

                float distance = Mathf.Abs(frame.time - targetTime);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0)
            {
                return false;
            }

            texture = frames[nearestIndex].texture;
            return texture != null;
        }

        private static bool TryGetRecordedBackgroundSize(List<WebcamBackgroundFrame> frames, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (frames == null)
            {
                return false;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                Texture2D texture = frames[i].texture;
                if (texture == null || texture.width <= 0 || texture.height <= 0)
                {
                    continue;
                }

                width = texture.width;
                height = texture.height;
                return true;
            }

            return false;
        }

        private bool TryGetActiveWebcamTexture(out WebCamTexture webcamTexture)
        {
            webcamTexture = null;
            if (_webcamRecord == null || _webcamRecord.webcamTexture == null)
            {
                return false;
            }

            WebCamTexture texture = _webcamRecord.webcamTexture;
            if (!texture.isPlaying || texture.width <= 16 || texture.height <= 16)
            {
                return false;
            }

            webcamTexture = texture;
            return true;
        }

        private bool TryGetRecordedWebcamBackgroundTextureForTimeline(out Texture2D texture)
        {
            texture = null;
            if (_recordedWebcamBackgroundFrames.Count == 0)
            {
                return false;
            }

            float timelineDuration = Mathf.Max(0.0001f, GetTimelineDuration());
            float timelineTime = Mathf.Max(0f, _isPreviewPlaying ? _previewTime : GetSelectedTimelineTime());
            float normalized = Mathf.Clamp01(timelineTime / timelineDuration);

            float sourceDuration = _recordedWebcamBackgroundDuration > 0f
                ? _recordedWebcamBackgroundDuration
                : _recordedWebcamBackgroundFrames[_recordedWebcamBackgroundFrames.Count - 1].time;
            sourceDuration = Mathf.Max(0.0001f, sourceDuration);
            float targetTime = normalized * sourceDuration;
            return TryGetRecordedBackgroundTextureAtTime(_recordedWebcamBackgroundFrames, targetTime, out texture);
        }

        private bool TryGetRecordedWebcamBackgroundSize(out int width, out int height)
        {
            return TryGetRecordedBackgroundSize(_recordedWebcamBackgroundFrames, out width, out height);
        }

        private bool TryGetRecordedVideoBackgroundTextureForTimeline(out Texture2D texture)
        {
            texture = null;
            if (_recordedVideoBackgroundFrames.Count == 0)
            {
                return false;
            }

            float timelineTime = Mathf.Max(0f, _isPreviewPlaying ? _previewTime : GetSelectedTimelineTime());
            float timelineDuration = Mathf.Max(0.0001f, GetTimelineDuration());
            float sourceDuration = _recordedVideoBackgroundDuration > 0f
                ? _recordedVideoBackgroundDuration
                : _recordedVideoBackgroundFrames[_recordedVideoBackgroundFrames.Count - 1].time;
            sourceDuration = Mathf.Max(0.0001f, sourceDuration);
            float normalized = Mathf.Clamp01(timelineTime / timelineDuration);
            float targetTime = normalized * sourceDuration;
            return TryGetRecordedBackgroundTextureAtTime(_recordedVideoBackgroundFrames, targetTime, out texture);
        }

        private bool TryGetRecordedVideoBackgroundSize(out int width, out int height)
        {
            return TryGetRecordedBackgroundSize(_recordedVideoBackgroundFrames, out width, out height);
        }

        private void DrawBodyReferenceImage()
        {
            if (_showVideoBackground)
            {
                Texture backgroundTexture = null;
                bool drawLiveWebcam = false;

                if (TryGetActiveWebcamTexture(out WebCamTexture activeWebcamTexture))
                {
                    backgroundTexture = activeWebcamTexture;
                    drawLiveWebcam = true;
                }
                else if (TryGetRecordedVideoBackgroundTextureForTimeline(out Texture2D recordedVideoTexture))
                {
                    backgroundTexture = recordedVideoTexture;
                }
                else if (_videoBackgroundRenderTexture != null)
                {
                    backgroundTexture = _videoBackgroundRenderTexture;
                }
                else if (
                    TryGetRecordedWebcamBackgroundTextureForTimeline(out Texture2D webcamBackgroundTexture))
                {
                    backgroundTexture = webcamBackgroundTexture;
                }

                if (backgroundTexture != null)
                {
                    Color previousColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_videoBackgroundOpacity));
                    if (drawLiveWebcam && _flipImportedVideoX)
                    {
                        GUI.DrawTextureWithTexCoords(_normalizedSpaceRect, backgroundTexture, new Rect(1f, 0f, -1f, 1f), true);
                    }
                    else
                    {
                        GUI.DrawTexture(_normalizedSpaceRect, backgroundTexture, ScaleMode.StretchToFill, true);
                    }
                    GUI.color = previousColor;
                }
            }

            if (!_showBodyReferenceImage || _bodyReferenceImage == null)
            {
                return;
            }

            Color imagePreviousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_bodyImageOpacity));
            GUI.DrawTexture(_normalizedSpaceRect, _bodyReferenceImage, ScaleMode.StretchToFill, true);
            GUI.color = imagePreviousColor;
        }

        private Rect ResolveNormalizedSpaceRect()
        {
            float referenceAspect = -1f;

            if (_showBodyReferenceImage && _bodyReferenceImage != null && _bodyReferenceImage.width > 0 && _bodyReferenceImage.height > 0)
            {
                referenceAspect = _bodyReferenceImage.width / (float)_bodyReferenceImage.height;
            }
            else if (_showVideoBackground)
            {
                int videoWidth = 0;
                int videoHeight = 0;
                if (_videoBackgroundRenderTexture != null)
                {
                    videoWidth = _videoBackgroundRenderTexture.width;
                    videoHeight = _videoBackgroundRenderTexture.height;
                }
                else if (_videoBackgroundPlayer != null && _videoBackgroundPlayer.isPrepared)
                {
                    videoWidth = (int)_videoBackgroundPlayer.width;
                    videoHeight = (int)_videoBackgroundPlayer.height;
                }

                if (videoWidth > 0 && videoHeight > 0)
                {
                    referenceAspect = videoWidth / (float)videoHeight;
                }
                else if (TryGetRecordedVideoBackgroundSize(out int recordedVideoWidth, out int recordedVideoHeight))
                {
                    referenceAspect = recordedVideoWidth / (float)recordedVideoHeight;
                }
                else if (TryGetActiveWebcamTexture(out WebCamTexture activeWebcamTexture))
                {
                    referenceAspect = activeWebcamTexture.width / (float)activeWebcamTexture.height;
                }
                else if (TryGetRecordedWebcamBackgroundSize(out int webcamWidth, out int webcamHeight))
                {
                    referenceAspect = webcamWidth / (float)webcamHeight;
                }
            }

            if (referenceAspect <= 0f)
            {
                return _canvasRect;
            }

            float canvasAspect = _canvasRect.width / Mathf.Max(_canvasRect.height, 0.001f);

            if (canvasAspect > referenceAspect)
            {
                float width = _canvasRect.height * referenceAspect;
                float x = _canvasRect.x + (_canvasRect.width - width) * 0.5f;
                return new Rect(x, _canvasRect.y, width, _canvasRect.height);
            }

            float height = _canvasRect.width / referenceAspect;
            float y = _canvasRect.y + (_canvasRect.height - height) * 0.5f;
            return new Rect(_canvasRect.x, y, _canvasRect.width, height);
        }

        private Rect GetInteractionRect()
        {
            if (_normalizedSpaceRect.width > 0f && _normalizedSpaceRect.height > 0f)
            {
                return _normalizedSpaceRect;
            }

            return _canvasRect;
        }

        private static void DrawGrid(Rect rect, int divisions, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            for (int i = 1; i < divisions; i++)
            {
                float t = i / (float)divisions;
                float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
                Handles.DrawLine(new Vector2(x, rect.yMin), new Vector2(x, rect.yMax));
                Handles.DrawLine(new Vector2(rect.xMin, y), new Vector2(rect.xMax, y));
            }
            Handles.EndGUI();
        }

        private void HandleMouse(Event evt)
        {
            if (_isPreviewPlaying || evt == null || _videoImport != null)
            {
                return;
            }

            Rect interactionRect = GetInteractionRect();
            if (!_isDragging && !interactionRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                _selectedJoint = GetClosestJoint(evt.mousePosition, PickRadius);
                if (_selectedJoint >= 0)
                {
                    _isDragging = true;
                    evt.Use();
                }
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && _isDragging && _selectedJoint >= 0)
            {
                Vector2 target = CanvasToNormalized(evt.mousePosition);
                if (_clampToCanvas)
                {
                    target = Clamp01(target);
                }

                MoveJoint(_selectedJoint, target);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                _isDragging = false;
                evt.Use();
            }
        }

        private void DrawSkeleton()
        {
            Handles.BeginGUI();

            Handles.color = new Color(0.28f, 0.79f, 0.98f, 0.95f);
            int pairCount = BonePairs.GetLength(0);
            for (int i = 0; i < pairCount; i++)
            {
                int a = BonePairs[i, 0];
                int b = BonePairs[i, 1];
                Handles.DrawAAPolyLine(2.0f, NormalizedToCanvas(_landmarks[a]), NormalizedToCanvas(_landmarks[b]));
            }

            for (int i = 0; i < VisibleLandmarkIndices.Length; i++)
            {
                int index = VisibleLandmarkIndices[i];
                bool selected = index == _selectedJoint;
                bool tracked = _trackedPoints[index];

                Color pointColor;
                if (selected)
                {
                    pointColor = new Color(1f, 0.7f, 0.2f, 1f);
                }
                else if (!tracked)
                {
                    pointColor = new Color(0.75f, 0.75f, 0.75f, 0.40f);
                }
                else if (IsTorsoJoint(index))
                {
                    pointColor = new Color(0.45f, 1f, 0.7f, 1f);
                }
                else
                {
                    pointColor = new Color(0.98f, 0.98f, 0.98f, 1f);
                }

                Handles.color = pointColor;
                Handles.DrawSolidDisc(NormalizedToCanvas(_landmarks[index]), Vector3.forward, _pointSize + (selected ? 1.5f : 0f));

                if (_showLabels)
                {
                    Handles.Label(
                        NormalizedToCanvas(_landmarks[index]) + new Vector2(6f, -4f),
                        index + ":" + LandmarkNames[index] + (tracked ? " [T]" : string.Empty),
                        EditorStyles.miniLabel);
                }
            }

            Handles.EndGUI();
        }

        private void MoveJoint(int index, Vector2 target)
        {
            if (index < 0 || index >= LandmarkCount)
            {
                return;
            }

            if (_chainByJoint.TryGetValue(index, out LimbChain chain))
            {
                target = ApplyLimbReachClamp(chain, index, target);
            }

            Vector2 lockedRightShoulder = _landmarks[11];
            Vector2 lockedLeftShoulder = _landmarks[12];
            Vector2 lockedRightHip = _landmarks[23];
            Vector2 lockedLeftHip = _landmarks[24];

            bool[] pinned = new bool[LandmarkCount];
            pinned[index] = true;

            if (_lockTorso && !IsTorsoJoint(index))
            {
                pinned[11] = true;
                pinned[12] = true;
                pinned[23] = true;
                pinned[24] = true;
            }

            _landmarks[index] = target;

            for (int i = 0; i < _solverIterations; i++)
            {
                for (int c = 0; c < _constraints.Count; c++)
                {
                    BoneConstraint constraint = _constraints[c];
                    SatisfyDistanceConstraint(constraint.A, constraint.B, constraint.Length, pinned);
                }

                ApplyJointAngleLimits(pinned);

                if (_clampToCanvas)
                {
                    ClampPose(pinned);
                }

                if (_lockTorso && !IsTorsoJoint(index))
                {
                    _landmarks[11] = lockedRightShoulder;
                    _landmarks[12] = lockedLeftShoulder;
                    _landmarks[23] = lockedRightHip;
                    _landmarks[24] = lockedLeftHip;
                }

                _landmarks[index] = target;
            }

            _hasWorldLandmarksForEditorPose = false;
            CommitEditorPoseToSelectedKeyframe();
        }

        private void SatisfyDistanceConstraint(int a, int b, float targetDistance, bool[] pinned)
        {
            Vector2 pointA = _landmarks[a];
            Vector2 pointB = _landmarks[b];
            Vector2 delta = pointB - pointA;
            float distance = delta.magnitude;
            if (distance < MinDistanceEpsilon)
            {
                distance = MinDistanceEpsilon;
                delta = Vector2.right * distance;
            }

            float difference = (distance - targetDistance) / distance;
            bool aPinned = pinned[a];
            bool bPinned = pinned[b];

            if (aPinned && bPinned)
            {
                return;
            }

            if (aPinned)
            {
                pointB -= delta * difference;
            }
            else if (bPinned)
            {
                pointA += delta * difference;
            }
            else
            {
                Vector2 adjust = delta * (0.5f * difference);
                pointA += adjust;
                pointB -= adjust;
            }

            _landmarks[a] = pointA;
            _landmarks[b] = pointB;
        }

        private void ApplyJointAngleLimits(bool[] pinned)
        {
            ConstrainAngle(12, 14, 16, 10f, 175f, pinned);
            ConstrainAngle(11, 13, 15, 10f, 175f, pinned);
            ConstrainAngle(24, 26, 28, 10f, 175f, pinned);
            ConstrainAngle(23, 25, 27, 10f, 175f, pinned);
            ConstrainAngle(24, 12, 14, 8f, 178f, pinned);
            ConstrainAngle(23, 11, 13, 8f, 178f, pinned);
            ConstrainAngle(12, 24, 26, 8f, 178f, pinned);
            ConstrainAngle(11, 23, 25, 8f, 178f, pinned);
        }

        private void ConstrainAngle(int parent, int joint, int child, float minAngle, float maxAngle, bool[] pinned)
        {
            Vector2 jp = _landmarks[joint];
            Vector2 vParent = _landmarks[parent] - jp;
            Vector2 vChild = _landmarks[child] - jp;

            if (vParent.sqrMagnitude < MinDistanceEpsilon || vChild.sqrMagnitude < MinDistanceEpsilon)
            {
                return;
            }

            float signed = Vector2.SignedAngle(vParent, vChild);
            float current = Mathf.Abs(signed);
            float target = Mathf.Clamp(current, minAngle, maxAngle);

            if (Mathf.Abs(target - current) < 0.01f)
            {
                return;
            }

            float sign = Mathf.Sign(signed);
            if (Mathf.Abs(sign) < 0.001f)
            {
                sign = 1f;
            }

            float delta = (target - current) * sign;

            bool parentPinned = pinned[parent];
            bool childPinned = pinned[child];

            if (parentPinned && childPinned)
            {
                return;
            }

            if (!parentPinned && !childPinned)
            {
                _landmarks[parent] = RotateAroundPoint(_landmarks[parent], jp, -delta * 0.5f);
                _landmarks[child] = RotateAroundPoint(_landmarks[child], jp, delta * 0.5f);
                return;
            }

            if (!parentPinned)
            {
                _landmarks[parent] = RotateAroundPoint(_landmarks[parent], jp, -delta);
                return;
            }

            if (!childPinned)
            {
                _landmarks[child] = RotateAroundPoint(_landmarks[child], jp, delta);
            }
        }

        private Vector2 ApplyLimbReachClamp(LimbChain chain, int selectedJoint, Vector2 target)
        {
            float upperLength = GetBoneLength(chain.Root, chain.Joint);
            float lowerLength = GetBoneLength(chain.Joint, chain.End);

            if (selectedJoint == chain.End)
            {
                Vector2 root = _landmarks[chain.Root];
                float minReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
                float maxReach = upperLength + lowerLength - 0.001f;
                return ClampDistance(root, target, minReach, maxReach);
            }

            if (selectedJoint == chain.Joint)
            {
                return ProjectToTwoCircleIntersection(
                    _landmarks[chain.Root], upperLength,
                    _landmarks[chain.End], lowerLength,
                    target);
            }

            return target;
        }

        private static Vector2 ClampDistance(Vector2 center, Vector2 point, float minDistance, float maxDistance)
        {
            Vector2 delta = point - center;
            float distance = delta.magnitude;
            if (distance < MinDistanceEpsilon)
            {
                return center + (Vector2.right * Mathf.Max(minDistance, MinDistanceEpsilon));
            }

            float clampedDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            return center + (delta / distance) * clampedDistance;
        }

        private static Vector2 ProjectToTwoCircleIntersection(
            Vector2 centerA,
            float radiusA,
            Vector2 centerB,
            float radiusB,
            Vector2 hint)
        {
            Vector2 axis = centerB - centerA;
            float distance = axis.magnitude;
            if (distance < MinDistanceEpsilon)
            {
                return centerA + (hint - centerA).normalized * radiusA;
            }

            Vector2 direction = axis / distance;

            if (distance > radiusA + radiusB)
            {
                return centerA + direction * radiusA;
            }

            if (distance < Mathf.Abs(radiusA - radiusB))
            {
                Vector2 insideDirection = radiusA >= radiusB ? direction : -direction;
                return centerA + insideDirection * radiusA;
            }

            float a = (radiusA * radiusA - radiusB * radiusB + distance * distance) / (2f * distance);
            float hSquared = Mathf.Max((radiusA * radiusA) - (a * a), 0f);
            float h = Mathf.Sqrt(hSquared);

            Vector2 mid = centerA + direction * a;
            Vector2 perp = new Vector2(-direction.y, direction.x);

            Vector2 candidateA = mid + perp * h;
            Vector2 candidateB = mid - perp * h;

            float da = (candidateA - hint).sqrMagnitude;
            float db = (candidateB - hint).sqrMagnitude;
            return da <= db ? candidateA : candidateB;
        }

        private void ClampPose(bool[] pinned)
        {
            for (int i = 0; i < LandmarkCount; i++)
            {
                if (pinned[i])
                {
                    continue;
                }

                _landmarks[i] = Clamp01(_landmarks[i]);
            }
        }

        private static Vector2 RotateAroundPoint(Vector2 point, Vector2 pivot, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 dir = point - pivot;

            return new Vector2(
                pivot.x + (dir.x * cos - dir.y * sin),
                pivot.y + (dir.x * sin + dir.y * cos));
        }

        private int GetClosestJoint(Vector2 mousePosition, float radius)
        {
            float bestDistance = radius * radius;
            int bestIndex = -1;
            for (int i = 0; i < VisibleLandmarkIndices.Length; i++)
            {
                int index = VisibleLandmarkIndices[i];
                Vector2 point = NormalizedToCanvas(_landmarks[index]);
                float distance = (point - mousePosition).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = index;
                }
            }
            return bestIndex;
        }

        private Vector2 CanvasToNormalized(Vector2 canvasPosition)
        {
            Rect interactionRect = GetInteractionRect();
            float x = Mathf.InverseLerp(interactionRect.xMin, interactionRect.xMax, canvasPosition.x);
            float y = 1f - Mathf.InverseLerp(interactionRect.yMin, interactionRect.yMax, canvasPosition.y);
            return new Vector2(x, y);
        }

        private Vector2 NormalizedToCanvas(Vector2 normalized)
        {
            Rect interactionRect = GetInteractionRect();
            float x = Mathf.Lerp(interactionRect.xMin, interactionRect.xMax, normalized.x);
            float y = Mathf.Lerp(interactionRect.yMax, interactionRect.yMin, normalized.y);
            return new Vector2(x, y);
        }

        private static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        private static bool IsTorsoJoint(int index)
        {
            return index == 11 || index == 12 || index == 23 || index == 24;
        }

        private void BuildChainMap()
        {
            _chainByJoint.Clear();
            for (int i = 0; i < LimbChains.Length; i++)
            {
                LimbChain chain = LimbChains[i];
                _chainByJoint[chain.Root] = chain;
                _chainByJoint[chain.Joint] = chain;
                _chainByJoint[chain.End] = chain;
            }
        }

        private void RebuildBoneConstraintsFromCurrentPose()
        {
            _constraints.Clear();
            _boneLengths.Clear();

            int pairCount = BonePairs.GetLength(0);
            for (int i = 0; i < pairCount; i++)
            {
                int a = BonePairs[i, 0];
                int b = BonePairs[i, 1];
                float length = Vector2.Distance(_landmarks[a], _landmarks[b]);
                _constraints.Add(new BoneConstraint(a, b, length));
                _boneLengths[MakeEdgeKey(a, b)] = length;
            }
        }

        private float GetBoneLength(int a, int b)
        {
            ulong key = MakeEdgeKey(a, b);
            if (_boneLengths.TryGetValue(key, out float length))
            {
                return length;
            }
            return Vector2.Distance(_landmarks[a], _landmarks[b]);
        }

        private static ulong MakeEdgeKey(int a, int b)
        {
            uint min = (uint)Mathf.Min(a, b);
            uint max = (uint)Mathf.Max(a, b);
            return ((ulong)min << 32) | max;
        }

        private static void CopyPose(Vector2[] source, Vector2[] destination)
        {
            for (int i = 0; i < LandmarkCount; i++)
            {
                destination[i] = source[i];
            }
        }

        private void InitializeDefaultTrackedPoints()
        {
            Array.Clear(_trackedPoints, 0, _trackedPoints.Length);
            SetTrackedPointsForVisibleLandmarks(true);
        }

        private void SetTrackedPointsForVisibleLandmarks(bool isTracked)
        {
            for (int i = 0; i < VisibleLandmarkIndices.Length; i++)
            {
                _trackedPoints[VisibleLandmarkIndices[i]] = isTracked;
            }
        }

        private int GetTrackedPointCount()
        {
            int count = 0;
            for (int i = 0; i < _trackedPoints.Length; i++)
            {
                if (_trackedPoints[i])
                {
                    count++;
                }
            }

            return count;
        }

        private void SyncSelectedKeyframesWithTimeline()
        {
            if (_keyframes.Count == 0)
            {
                _selectedKeyframes.Clear();
                _selectedKeyframeIndex = -1;
                return;
            }

            List<KeyframeData> staleSelections = null;
            foreach (KeyframeData keyframe in _selectedKeyframes)
            {
                if (keyframe != null && _keyframes.Contains(keyframe))
                {
                    continue;
                }

                if (staleSelections == null)
                {
                    staleSelections = new List<KeyframeData>();
                }

                staleSelections.Add(keyframe);
            }

            if (staleSelections != null)
            {
                for (int i = 0; i < staleSelections.Count; i++)
                {
                    _selectedKeyframes.Remove(staleSelections[i]);
                }
            }

            if (_selectedKeyframeIndex < 0 || _selectedKeyframeIndex >= _keyframes.Count)
            {
                _selectedKeyframeIndex = 0;
            }

            if (_selectedKeyframes.Count == 0)
            {
                _selectedKeyframes.Add(_keyframes[_selectedKeyframeIndex]);
                return;
            }

            if (!_selectedKeyframes.Contains(_keyframes[_selectedKeyframeIndex]))
            {
                _selectedKeyframeIndex = GetFirstSelectedKeyframeIndex();
            }
        }

        private int GetFirstSelectedKeyframeIndex()
        {
            for (int i = 0; i < _keyframes.Count; i++)
            {
                if (_selectedKeyframes.Contains(_keyframes[i]))
                {
                    return i;
                }
            }

            return _keyframes.Count > 0 ? 0 : -1;
        }

        private int GetSelectedKeyframeCount()
        {
            SyncSelectedKeyframesWithTimeline();
            return _selectedKeyframes.Count;
        }

        private bool IsKeyframeSelected(int index)
        {
            return index >= 0 &&
                index < _keyframes.Count &&
                _selectedKeyframes.Contains(_keyframes[index]);
        }

        private void ResetEditorState()
        {
            StopPreviewPlayback();

            if (_videoImportStartQueued)
            {
                EditorApplication.delayCall -= ExecuteQueuedVideoImportStart;
                _videoImportStartQueued = false;
            }

            CancelVideoImport(showDialog: false);
            CancelWebcamRecording(showDialog: false);
            ReleaseVideoBackgroundPlayer();
            ClearRecordedVideoBackgroundFrames();
            ClearRecordedWebcamBackgroundFrames();
            ReleaseRuntimeLoadedImage();

            _bodyReferenceImage = null;
            _showBodyReferenceImage = true;
            _flipImportedImageX = false;
            _bodyImageOpacity = DefaultBodyImageOpacity;

            _videoFilePath = string.Empty;
            _flipImportedVideoX = false;
            _videoSampleRate = DefaultVideoSampleRate;
            _videoMaxDuration = DefaultVideoMaxDuration;
            _videoMaxFrames = DefaultVideoMaxFrames;
            _showVideoBackground = true;
            _videoBackgroundOpacity = DefaultVideoBackgroundOpacity;
            _videoBackgroundLoadedPath = string.Empty;
            _videoBackgroundShowFirstFrameOnPrepare = false;
            _lastVideoBackgroundTime = -1d;
            _lastVideoBackgroundFrame = -1L;

            _selectedWebcamDeviceIndex = 0;
            _useTimedWebcamRecording = false;
            _webcamTimedStartAtSeconds = 0f;
            _webcamTimedStopAtSeconds = DefaultWebcamTimedStopAtSeconds;

            _showLabels = false;
            _lockTorso = true;
            _clampToCanvas = true;
            _pointSize = DefaultPointSize;
            _solverIterations = DefaultSolverIterations;
            _showTrackedPoints = true;
            _rightPanelScroll = Vector2.zero;

            _motionFileName = DefaultMotionFileName;
            _motionDuration = DefaultMotionDuration;
            _selectedJoint = -1;
            _selectedKeyframeIndex = 0;
            _selectedKeyframes.Clear();
            _isDragging = false;
            _isTimelineSelectionDragging = false;
            _previewTime = 0f;
            _hasWorldLandmarksForEditorPose = false;
            Array.Clear(_worldLandmarks, 0, _worldLandmarks.Length);

            BuildDefaultPose();
            CopyPose(_defaultLandmarks, _landmarks);
            InitializeDefaultTrackedPoints();
            RebuildBoneConstraintsFromCurrentPose();
            BuildDefaultTimeline();
            Repaint();
        }

        private void LoadBodyImageFromDisk()
        {
            string path = EditorUtility.OpenFilePanel("Select Human Body Image", Application.dataPath, "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            byte[] imageBytes;
            try
            {
                imageBytes = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Failed To Read Image", "Could not read file:\n" + exception.Message, "OK");
                return;
            }

            Texture2D loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!loadedTexture.LoadImage(imageBytes, false))
            {
                DestroyImmediate(loadedTexture);
                EditorUtility.DisplayDialog("Invalid Image", "Could not decode the selected image file.", "OK");
                return;
            }

            loadedTexture.name = Path.GetFileNameWithoutExtension(path);
            ReleaseRuntimeLoadedImage();
            _runtimeLoadedBodyImage = loadedTexture;
            _bodyReferenceImage = loadedTexture;
        }

        private void ApplyBodyPoseFromImageToSelectedKeyframe()
        {
            if (!TryExtractPoseFromImage(
                _bodyReferenceImage,
                _flipImportedImageX,
                out Vector2[] detectedLandmarks,
                out bool[] detectedTrackedPoints,
                out Vector3[] detectedWorldLandmarks,
                out bool hasDetectedWorldLandmarks,
                out string errorMessage))
            {
                EditorUtility.DisplayDialog("Pose Extraction Failed", errorMessage, "OK");
                return;
            }

            for (int i = 0; i < LandmarkCount; i++)
            {
                _landmarks[i] = detectedLandmarks[i];
                _trackedPoints[i] = detectedTrackedPoints[i];
                if (hasDetectedWorldLandmarks)
                {
                    _worldLandmarks[i] = detectedWorldLandmarks[i];
                }
            }

            _hasWorldLandmarksForEditorPose = hasDetectedWorldLandmarks;

            if (_clampToCanvas)
            {
                for (int i = 0; i < LandmarkCount; i++)
                {
                    _landmarks[i] = Clamp01(_landmarks[i]);
                }

                // 2D clamping can desynchronize world-space points from edited normalized points.
                _hasWorldLandmarksForEditorPose = false;
            }

            RebuildBoneConstraintsFromCurrentPose();
            CommitEditorPoseToSelectedKeyframes();
            _selectedJoint = -1;
            Repaint();
        }

        private bool TryExtractPoseFromImage(
            Texture2D sourceImage,
            bool flipX,
            out Vector2[] detectedLandmarks,
            out bool[] trackedPoints,
            out Vector3[] detectedWorldLandmarks,
            out bool hasWorldLandmarks,
            out string errorMessage)
        {
            detectedLandmarks = null;
            trackedPoints = null;
            detectedWorldLandmarks = null;
            hasWorldLandmarks = false;
            errorMessage = string.Empty;

            if (sourceImage == null)
            {
                errorMessage = "Assign a body image first.";
                return false;
            }

            string modelPath = ResolvePoseLandmarkerModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                errorMessage = "Could not find model file '" + DefaultPoseLandmarkerModelName + "' in Assets/StreamingAssets.";
                return false;
            }

            Texture2D readableTexture = null;
            try
            {
                readableTexture = CreateReadableTexture(sourceImage);
                if (readableTexture == null)
                {
                    errorMessage = "Failed to convert image to a readable texture.";
                    return false;
                }

                var options = new PoseLandmarkerOptions(
                    new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: modelPath),
                    runningMode: RunningMode.IMAGE,
                    numPoses: 1,
                    minPoseDetectionConfidence: 0.5f,
                    minPosePresenceConfidence: 0.5f,
                    minTrackingConfidence: 0.5f,
                    outputSegmentationMasks: false);

                using (var poseLandmarker = PoseLandmarker.CreateFromOptions(options))
                {
                    if (!TryExtractPoseFromTexture(
                        readableTexture,
                        poseLandmarker,
                        flipX,
                        out detectedLandmarks,
                        out trackedPoints,
                        out detectedWorldLandmarks,
                        out hasWorldLandmarks,
                        out errorMessage))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                errorMessage = "Pose detection failed:\n" + exception.Message;
                return false;
            }
            finally
            {
                if (readableTexture != null)
                {
                    DestroyImmediate(readableTexture);
                }
            }
        }

        private bool TryExtractPoseFromTexture(
            Texture2D texture,
            PoseLandmarker poseLandmarker,
            bool flipX,
            out Vector2[] detectedLandmarks,
            out bool[] trackedPoints,
            out Vector3[] detectedWorldLandmarks,
            out bool hasWorldLandmarks,
            out string errorMessage)
        {
            detectedLandmarks = null;
            trackedPoints = null;
            detectedWorldLandmarks = null;
            hasWorldLandmarks = false;
            errorMessage = string.Empty;

            if (texture == null)
            {
                errorMessage = "Invalid source texture.";
                return false;
            }

            if (poseLandmarker == null)
            {
                errorMessage = "Pose landmarker is not initialized.";
                return false;
            }

            using (var image = new Mediapipe.Image(texture))
            {
                PoseLandmarkerResult result = default;
                bool poseDetected = poseLandmarker.TryDetect(image, new ImageProcessingOptions(rotationDegrees: 0), ref result);
                if (!poseDetected || result.poseLandmarks == null || result.poseLandmarks.Count == 0)
                {
                    errorMessage = "No body pose detected. Use a clear, full-body frame.";
                    return false;
                }

                var normalizedLandmarks = result.poseLandmarks[0].landmarks;
                if (normalizedLandmarks == null || normalizedLandmarks.Count < LandmarkCount)
                {
                    errorMessage = "Pose detection returned incomplete landmark data.";
                    return false;
                }

                var worldLandmarks = result.poseWorldLandmarks != null && result.poseWorldLandmarks.Count > 0
                    ? result.poseWorldLandmarks[0].landmarks
                    : null;
                hasWorldLandmarks = worldLandmarks != null && worldLandmarks.Count >= LandmarkCount;

                detectedLandmarks = new Vector2[LandmarkCount];
                trackedPoints = new bool[LandmarkCount];
                detectedWorldLandmarks = new Vector3[LandmarkCount];

                for (int i = 0; i < LandmarkCount; i++)
                {
                    float x = normalizedLandmarks[i].x;
                    float y = normalizedLandmarks[i].y;
                    float visibility = Mathf.Max(normalizedLandmarks[i].visibility.GetValueOrDefault(1f), normalizedLandmarks[i].presence.GetValueOrDefault(1f));

                    if (flipX)
                    {
                        x = 1f - x;
                    }

                    detectedLandmarks[i] = new Vector2(x, y);
                    if (hasWorldLandmarks)
                    {
                        detectedWorldLandmarks[i] = new Vector3(worldLandmarks[i].x, worldLandmarks[i].y, worldLandmarks[i].z);
                    }
                    else
                    {
                        detectedWorldLandmarks[i] = Vector3.zero;
                    }
                    trackedPoints[i] = visibility >= MinTrackedVisibility;
                }
            }

            int trackedCount = 0;
            for (int i = 0; i < trackedPoints.Length; i++)
            {
                if (trackedPoints[i])
                {
                    trackedCount++;
                }
            }

            if (trackedCount < 2)
            {
                for (int i = 0; i < VisibleLandmarkIndices.Length; i++)
                {
                    trackedPoints[VisibleLandmarkIndices[i]] = true;
                }
            }

            return true;
        }

        private static string ResolvePoseLandmarkerModelPath()
        {
            string modelPath = Path.Combine(Application.streamingAssetsPath, DefaultPoseLandmarkerModelName);
            if (File.Exists(modelPath))
            {
                return modelPath;
            }

            string fallback = Path.Combine(Application.dataPath, "StreamingAssets", DefaultPoseLandmarkerModelName);
            return File.Exists(fallback) ? fallback : null;
        }

        private static Texture2D CreateReadableTexture(Texture2D sourceTexture)
        {
            if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
            {
                return null;
            }

            RenderTexture temporaryTexture = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previousRenderTexture = RenderTexture.active;

            try
            {
                Graphics.Blit(sourceTexture, temporaryTexture);
                RenderTexture.active = temporaryTexture;

                Texture2D readableTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false, false);
                readableTexture.ReadPixels(new Rect(0f, 0f, temporaryTexture.width, temporaryTexture.height), 0, 0);
                readableTexture.Apply(false, false);
                return readableTexture;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(temporaryTexture);
            }
        }

        private static Texture2D CreatePreviewFrameTexture(Texture sourceTexture, int maxDimension)
        {
            if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
            {
                return null;
            }

            int sourceWidth = sourceTexture.width;
            int sourceHeight = sourceTexture.height;
            float scale = 1f;
            int maxSourceDimension = Mathf.Max(sourceWidth, sourceHeight);
            if (maxSourceDimension > maxDimension && maxDimension > 0)
            {
                scale = maxDimension / (float)maxSourceDimension;
            }

            int targetWidth = Mathf.Max(2, Mathf.RoundToInt(sourceWidth * scale));
            int targetHeight = Mathf.Max(2, Mathf.RoundToInt(sourceHeight * scale));

            RenderTextureReadWrite readWrite = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? RenderTextureReadWrite.sRGB
                : RenderTextureReadWrite.Default;
            RenderTexture temporaryTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, readWrite);
            RenderTexture previousRenderTexture = RenderTexture.active;

            try
            {
                Graphics.Blit(sourceTexture, temporaryTexture);
                RenderTexture.active = temporaryTexture;

                Texture2D previewTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                previewTexture.ReadPixels(new Rect(0f, 0f, targetWidth, targetHeight), 0, 0, false);
                previewTexture.Apply(false, false);
                return previewTexture;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(temporaryTexture);
            }
        }

        private void ReleaseRuntimeLoadedImage()
        {
            if (_runtimeLoadedBodyImage == null)
            {
                return;
            }

            if (_bodyReferenceImage == _runtimeLoadedBodyImage)
            {
                _bodyReferenceImage = null;
            }

            if (!AssetDatabase.Contains(_runtimeLoadedBodyImage))
            {
                DestroyImmediate(_runtimeLoadedBodyImage);
            }

            _runtimeLoadedBodyImage = null;
        }

        private void BuildDefaultTimeline()
        {
            _keyframes.Clear();
            AddKeyframe(0f, _defaultLandmarks, _trackedPoints);
            AddKeyframe(1f, _defaultLandmarks, _trackedPoints);
            _motionDuration = 1f;
            SelectKeyframe(0, commitCurrent: false);
        }

        private void AddKeyframe(float keyTime, Vector2[] sourceLandmarks, bool[] sourceTracked, Vector3[] sourceWorldLandmarks = null, bool hasWorldLandmarks = false)
        {
            Vector2[] landmarksCopy = new Vector2[LandmarkCount];
            bool[] trackedCopy = new bool[LandmarkCount];
            Array.Copy(sourceLandmarks, landmarksCopy, LandmarkCount);
            Array.Copy(sourceTracked, trackedCopy, LandmarkCount);

            Vector3[] worldLandmarksCopy = null;
            if (hasWorldLandmarks && sourceWorldLandmarks != null && sourceWorldLandmarks.Length == LandmarkCount)
            {
                worldLandmarksCopy = new Vector3[LandmarkCount];
                Array.Copy(sourceWorldLandmarks, worldLandmarksCopy, LandmarkCount);
            }

            _keyframes.Add(new KeyframeData(keyTime, landmarksCopy, trackedCopy, worldLandmarksCopy, hasWorldLandmarks));
        }

        private void AddKeyframeAtCurrentTimelineTime()
        {
            EnsureTimelineHasKeyframes();
            CommitEditorPoseToSelectedKeyframe();

            float insertTime = _selectedKeyframeIndex >= 0 && _selectedKeyframeIndex < _keyframes.Count
                ? Mathf.Min(GetTimelineDuration(), _keyframes[_selectedKeyframeIndex].time + 0.1f)
                : 0f;

            AddKeyframe(insertTime, _landmarks, _trackedPoints, _worldLandmarks, _hasWorldLandmarksForEditorPose);
            SortKeyframesByTime();
            int index = GetNearestKeyframeIndex(insertTime);
            SelectKeyframe(index);
        }

        private void DuplicateSelectedKeyframe()
        {
            if (_selectedKeyframeIndex < 0 || _selectedKeyframeIndex >= _keyframes.Count)
            {
                return;
            }

            CommitEditorPoseToSelectedKeyframe();
            KeyframeData source = _keyframes[_selectedKeyframeIndex];
            KeyframeData clone = source.Clone();
            clone.time = Mathf.Min(GetTimelineDuration(), source.time + 0.05f);
            _keyframes.Add(clone);
            SortKeyframesByTime(clone);
            SelectKeyframe(_keyframes.IndexOf(clone), commitCurrent: false);
        }

        private void RemoveSelectedKeyframe()
        {
            SyncSelectedKeyframesWithTimeline();
            int selectedCount = Mathf.Max(1, _selectedKeyframes.Count);
            if (_selectedKeyframeIndex < 0 ||
                _selectedKeyframeIndex >= _keyframes.Count ||
                _keyframes.Count <= 2 ||
                _keyframes.Count - selectedCount < 2)
            {
                return;
            }

            float fallbackTime = _keyframes[_selectedKeyframeIndex].time;
            for (int i = _keyframes.Count - 1; i >= 0; i--)
            {
                if (_selectedKeyframes.Contains(_keyframes[i]))
                {
                    _keyframes.RemoveAt(i);
                }
            }

            _selectedKeyframes.Clear();
            _selectedKeyframeIndex = GetNearestKeyframeIndex(fallbackTime);
            if (_selectedKeyframeIndex < 0)
            {
                _selectedKeyframeIndex = 0;
            }

            if (_keyframes.Count > 0)
            {
                _selectedKeyframes.Add(_keyframes[_selectedKeyframeIndex]);
            }
            LoadSelectedKeyframeIntoEditor();
        }

        private void NormalizeKeyframeTimes()
        {
            if (_keyframes.Count <= 1)
            {
                return;
            }

            float duration = GetTimelineDuration();
            for (int i = 0; i < _keyframes.Count; i++)
            {
                float t = i / (float)(_keyframes.Count - 1);
                _keyframes[i].time = duration * t;
            }

            SortKeyframesByTime();
            LoadSelectedKeyframeIntoEditor();
        }

        private void TrimTimelineToKeyframes()
        {
            if (_keyframes.Count == 0)
            {
                return;
            }

            CommitEditorPoseToSelectedKeyframe();
            SortKeyframesByTime();

            float firstTime = _keyframes[0].time;
            float lastTime = _keyframes[_keyframes.Count - 1].time;
            float trimmedDuration = Mathf.Max(0.1f, lastTime - firstTime);
            TrimRecordedBackgroundFramesToTimelineRange(firstTime, lastTime);

            for (int i = 0; i < _keyframes.Count; i++)
            {
                _keyframes[i].time = Mathf.Max(0f, _keyframes[i].time - firstTime);
            }

            _motionDuration = trimmedDuration;

            if (_isPreviewPlaying)
            {
                _previewTime = Mathf.Clamp(_previewTime - firstTime, 0f, _motionDuration);
            }

            LoadSelectedKeyframeIntoEditor();
        }

        private void AutoFixSelectedKeyframePose()
        {
            if (_selectedKeyframeIndex < 0 || _selectedKeyframeIndex >= _keyframes.Count)
            {
                return;
            }

            CommitEditorPoseToSelectedKeyframe();

            KeyframeData currentFrame = _keyframes[_selectedKeyframeIndex];
            KeyframeData previousFrame = _selectedKeyframeIndex > 0 ? _keyframes[_selectedKeyframeIndex - 1] : null;
            KeyframeData nextFrame = _selectedKeyframeIndex + 1 < _keyframes.Count ? _keyframes[_selectedKeyframeIndex + 1] : null;
            if (previousFrame == null && nextFrame == null)
            {
                ShowNotification(new GUIContent("Auto Fix Pose needs a neighboring keyframe."));
                return;
            }

            Array.Copy(currentFrame.landmarks, _landmarks, LandmarkCount);
            Array.Copy(currentFrame.trackedPoints, _trackedPoints, LandmarkCount);
            if (currentFrame.hasWorldLandmarks && currentFrame.worldLandmarks != null && currentFrame.worldLandmarks.Length == LandmarkCount)
            {
                Array.Copy(currentFrame.worldLandmarks, _worldLandmarks, LandmarkCount);
                _hasWorldLandmarksForEditorPose = true;
            }
            else
            {
                Array.Clear(_worldLandmarks, 0, _worldLandmarks.Length);
                _hasWorldLandmarksForEditorPose = false;
            }

            Vector2[] predictedLandmarks = new Vector2[LandmarkCount];
            bool[] hasPredictedLandmarks = new bool[LandmarkCount];
            Vector3[] predictedWorldLandmarks = new Vector3[LandmarkCount];
            bool[] hasPredictedWorldLandmarks = new bool[LandmarkCount];
            float[] landmarkDeltas = new float[LandmarkCount];
            float[] worldDeltas = new float[LandmarkCount];
            List<float> landmarkDeltaSamples = new List<float>(LandmarkCount);
            List<float> worldDeltaSamples = new List<float>(LandmarkCount);

            for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
            {
                if (!currentFrame.trackedPoints[pointIndex])
                {
                    continue;
                }

                if (TryPredictLandmarkFromNeighbors(previousFrame, currentFrame, nextFrame, pointIndex, out Vector2 predicted))
                {
                    hasPredictedLandmarks[pointIndex] = true;
                    predictedLandmarks[pointIndex] = predicted;
                    float delta = Vector2.Distance(currentFrame.landmarks[pointIndex], predicted);
                    landmarkDeltas[pointIndex] = delta;
                    landmarkDeltaSamples.Add(delta);
                }

                if (_hasWorldLandmarksForEditorPose &&
                    TryPredictWorldLandmarkFromNeighbors(previousFrame, currentFrame, nextFrame, pointIndex, out Vector3 predictedWorld))
                {
                    hasPredictedWorldLandmarks[pointIndex] = true;
                    predictedWorldLandmarks[pointIndex] = predictedWorld;
                    float worldDelta = Vector3.Distance(currentFrame.worldLandmarks[pointIndex], predictedWorld);
                    worldDeltas[pointIndex] = worldDelta;
                    worldDeltaSamples.Add(worldDelta);
                }
            }

            if (landmarkDeltaSamples.Count == 0)
            {
                ShowNotification(new GUIContent("Auto Fix Pose found no reference points."));
                return;
            }

            float scale2D = EstimateFrameScale2D(currentFrame);
            float landmarkBaseThreshold = Mathf.Clamp(scale2D * 0.16f, 0.02f, 0.16f);
            float landmarkOutlierThreshold = ComputeOutlierThreshold(landmarkDeltaSamples, landmarkBaseThreshold);

            bool useWorldOutlierCheck = _hasWorldLandmarksForEditorPose && worldDeltaSamples.Count > 0;
            float worldOutlierThreshold = 0f;
            if (useWorldOutlierCheck)
            {
                float scale3D = EstimateFrameScale3D(currentFrame);
                float worldBaseThreshold = Mathf.Clamp(scale3D * 0.14f, 0.02f, 0.25f);
                worldOutlierThreshold = ComputeOutlierThreshold(worldDeltaSamples, worldBaseThreshold);
            }

            bool[] correctedPoints = new bool[LandmarkCount];
            bool worldPoseStillAligned = _hasWorldLandmarksForEditorPose;
            int correctedCount = 0;

            for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
            {
                if (!hasPredictedLandmarks[pointIndex])
                {
                    continue;
                }

                float delta = landmarkDeltas[pointIndex];
                bool shouldCorrect = delta >= landmarkOutlierThreshold;
                if (!shouldCorrect &&
                    useWorldOutlierCheck &&
                    hasPredictedWorldLandmarks[pointIndex] &&
                    worldDeltas[pointIndex] >= worldOutlierThreshold)
                {
                    shouldCorrect = true;
                }

                if (!shouldCorrect)
                {
                    continue;
                }

                float blend = Mathf.Lerp(0.65f, 1f, Mathf.InverseLerp(landmarkOutlierThreshold, landmarkOutlierThreshold * 2.5f, delta));
                _landmarks[pointIndex] = Vector2.Lerp(_landmarks[pointIndex], predictedLandmarks[pointIndex], blend);
                correctedPoints[pointIndex] = true;
                correctedCount++;

                if (worldPoseStillAligned)
                {
                    if (hasPredictedWorldLandmarks[pointIndex])
                    {
                        float worldDelta = worldDeltas[pointIndex];
                        float worldBlend = worldOutlierThreshold > MinDistanceEpsilon
                            ? Mathf.Lerp(0.65f, 1f, Mathf.InverseLerp(worldOutlierThreshold, worldOutlierThreshold * 2.5f, worldDelta))
                            : blend;
                        _worldLandmarks[pointIndex] = Vector3.Lerp(_worldLandmarks[pointIndex], predictedWorldLandmarks[pointIndex], worldBlend);
                    }
                    else
                    {
                        worldPoseStillAligned = false;
                    }
                }
            }

            if (correctedCount <= 0)
            {
                ShowNotification(new GUIContent("Auto Fix Pose found no outlier points."));
                return;
            }

            if (!worldPoseStillAligned)
            {
                Array.Clear(_worldLandmarks, 0, _worldLandmarks.Length);
                _hasWorldLandmarksForEditorPose = false;
            }

            float[] referenceBoneLengths = BuildReferenceBoneLengths(previousFrame, currentFrame, nextFrame);
            RelaxAutoFixedPose(correctedPoints, referenceBoneLengths);
            CommitEditorPoseToSelectedKeyframe();
            RebuildBoneConstraintsFromCurrentPose();
            Repaint();

            ShowNotification(new GUIContent("Auto fixed " + correctedCount + " point" + (correctedCount == 1 ? string.Empty : "s") + "."));
        }

        private void RelaxAutoFixedPose(bool[] correctedPoints, float[] referenceBoneLengths)
        {
            if (correctedPoints == null)
            {
                return;
            }

            bool[] pinned = new bool[LandmarkCount];
            bool hasAdjustablePoints = false;
            for (int i = 0; i < LandmarkCount; i++)
            {
                pinned[i] = !correctedPoints[i];
                if (correctedPoints[i])
                {
                    hasAdjustablePoints = true;
                }
            }

            if (!hasAdjustablePoints)
            {
                return;
            }

            int pairCount = BonePairs.GetLength(0);
            int iterations = Mathf.Clamp(_solverIterations / 2, 6, 20);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
                {
                    int a = BonePairs[pairIndex, 0];
                    int b = BonePairs[pairIndex, 1];
                    float targetLength = referenceBoneLengths != null &&
                        pairIndex < referenceBoneLengths.Length
                        ? referenceBoneLengths[pairIndex]
                        : 0f;

                    if (targetLength <= MinDistanceEpsilon)
                    {
                        continue;
                    }

                    SatisfyDistanceConstraint(a, b, targetLength, pinned);
                }

                ApplyJointAngleLimits(pinned);
                if (_clampToCanvas)
                {
                    ClampPose(pinned);
                }
            }
        }

        private static float[] BuildReferenceBoneLengths(KeyframeData previousFrame, KeyframeData currentFrame, KeyframeData nextFrame)
        {
            int pairCount = BonePairs.GetLength(0);
            float[] lengths = new float[pairCount];
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                int a = BonePairs[pairIndex, 0];
                int b = BonePairs[pairIndex, 1];
                float total = 0f;
                int count = 0;

                if (IsTrackedLandmarkAvailable(previousFrame, a) && IsTrackedLandmarkAvailable(previousFrame, b))
                {
                    total += Vector2.Distance(previousFrame.landmarks[a], previousFrame.landmarks[b]);
                    count++;
                }

                if (IsTrackedLandmarkAvailable(nextFrame, a) && IsTrackedLandmarkAvailable(nextFrame, b))
                {
                    total += Vector2.Distance(nextFrame.landmarks[a], nextFrame.landmarks[b]);
                    count++;
                }

                if (count == 0 && IsTrackedLandmarkAvailable(currentFrame, a) && IsTrackedLandmarkAvailable(currentFrame, b))
                {
                    total += Vector2.Distance(currentFrame.landmarks[a], currentFrame.landmarks[b]);
                    count++;
                }

                lengths[pairIndex] = count > 0 ? total / count : 0f;
            }

            return lengths;
        }

        private static bool TryPredictLandmarkFromNeighbors(
            KeyframeData previousFrame,
            KeyframeData currentFrame,
            KeyframeData nextFrame,
            int pointIndex,
            out Vector2 predicted)
        {
            predicted = Vector2.zero;
            bool hasPrevious = IsTrackedLandmarkAvailable(previousFrame, pointIndex);
            bool hasNext = IsTrackedLandmarkAvailable(nextFrame, pointIndex);
            if (!hasPrevious && !hasNext)
            {
                return false;
            }

            if (hasPrevious && hasNext)
            {
                float blend = 0.5f;
                float span = nextFrame.time - previousFrame.time;
                if (span > 0.0001f)
                {
                    blend = Mathf.Clamp01((currentFrame.time - previousFrame.time) / span);
                }

                predicted = Vector2.Lerp(previousFrame.landmarks[pointIndex], nextFrame.landmarks[pointIndex], blend);
                return true;
            }

            predicted = hasPrevious ? previousFrame.landmarks[pointIndex] : nextFrame.landmarks[pointIndex];
            return true;
        }

        private static bool TryPredictWorldLandmarkFromNeighbors(
            KeyframeData previousFrame,
            KeyframeData currentFrame,
            KeyframeData nextFrame,
            int pointIndex,
            out Vector3 predicted)
        {
            predicted = Vector3.zero;
            bool hasPrevious = IsWorldLandmarkAvailable(previousFrame, pointIndex);
            bool hasNext = IsWorldLandmarkAvailable(nextFrame, pointIndex);
            if (!hasPrevious && !hasNext)
            {
                return false;
            }

            if (hasPrevious && hasNext)
            {
                float blend = 0.5f;
                float span = nextFrame.time - previousFrame.time;
                if (span > 0.0001f)
                {
                    blend = Mathf.Clamp01((currentFrame.time - previousFrame.time) / span);
                }

                predicted = Vector3.Lerp(previousFrame.worldLandmarks[pointIndex], nextFrame.worldLandmarks[pointIndex], blend);
                return true;
            }

            predicted = hasPrevious ? previousFrame.worldLandmarks[pointIndex] : nextFrame.worldLandmarks[pointIndex];
            return true;
        }

        private static bool IsTrackedLandmarkAvailable(KeyframeData frame, int pointIndex)
        {
            return frame != null &&
                frame.landmarks != null &&
                frame.landmarks.Length == LandmarkCount &&
                frame.trackedPoints != null &&
                frame.trackedPoints.Length == LandmarkCount &&
                pointIndex >= 0 &&
                pointIndex < LandmarkCount &&
                frame.trackedPoints[pointIndex];
        }

        private static bool IsWorldLandmarkAvailable(KeyframeData frame, int pointIndex)
        {
            return IsTrackedLandmarkAvailable(frame, pointIndex) &&
                frame.hasWorldLandmarks &&
                frame.worldLandmarks != null &&
                frame.worldLandmarks.Length == LandmarkCount;
        }

        private static float EstimateFrameScale2D(KeyframeData frame)
        {
            if (frame == null || frame.landmarks == null || frame.trackedPoints == null)
            {
                return 0.5f;
            }

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            int count = 0;
            for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
            {
                if (!frame.trackedPoints[pointIndex])
                {
                    continue;
                }

                Vector2 point = frame.landmarks[pointIndex];
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                count++;
            }

            if (count < 2)
            {
                return 0.5f;
            }

            float diagonal = Vector2.Distance(min, max);
            return diagonal > MinDistanceEpsilon ? diagonal : 0.5f;
        }

        private static float EstimateFrameScale3D(KeyframeData frame)
        {
            if (frame == null ||
                !frame.hasWorldLandmarks ||
                frame.worldLandmarks == null ||
                frame.worldLandmarks.Length != LandmarkCount ||
                frame.trackedPoints == null)
            {
                return 1f;
            }

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int count = 0;
            for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
            {
                if (!frame.trackedPoints[pointIndex])
                {
                    continue;
                }

                Vector3 point = frame.worldLandmarks[pointIndex];
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
                count++;
            }

            if (count < 3)
            {
                return 1f;
            }

            float diagonal = Vector3.Distance(min, max);
            return diagonal > MinDistanceEpsilon ? diagonal : 1f;
        }

        private static float ComputeOutlierThreshold(List<float> deltaSamples, float baseThreshold)
        {
            if (deltaSamples == null || deltaSamples.Count == 0)
            {
                return baseThreshold;
            }

            List<float> sorted = new List<float>(deltaSamples);
            float median = ComputeMedian(sorted);

            List<float> deviations = new List<float>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                deviations.Add(Mathf.Abs(sorted[i] - median));
            }

            float mad = ComputeMedian(deviations);
            float robustThreshold = mad > 0.0001f
                ? median + (mad * 3.5f)
                : median * 2.25f;

            return Mathf.Max(baseThreshold, robustThreshold);
        }

        private static float ComputeMedian(List<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0f;
            }

            values.Sort();
            int middle = values.Count / 2;
            if ((values.Count & 1) == 1)
            {
                return values[middle];
            }

            return (values[middle - 1] + values[middle]) * 0.5f;
        }

        private void ClampKeyframeTimesToDuration()
        {
            float duration = Mathf.Max(0.1f, _motionDuration);
            for (int i = 0; i < _keyframes.Count; i++)
            {
                _keyframes[i].time = Mathf.Clamp(_keyframes[i].time, 0f, duration);
            }
        }

        private float GetLastKeyframeTime()
        {
            float last = 0f;
            for (int i = 0; i < _keyframes.Count; i++)
            {
                if (_keyframes[i].time > last)
                {
                    last = _keyframes[i].time;
                }
            }

            return last;
        }

        private float GetTimelineDuration()
        {
            return Mathf.Max(0.1f, _motionDuration, GetLastKeyframeTime());
        }

        private float GetSelectedTimelineTime()
        {
            if (_selectedKeyframeIndex < 0 || _selectedKeyframeIndex >= _keyframes.Count)
            {
                return 0f;
            }

            return _keyframes[_selectedKeyframeIndex].time;
        }

        private int GetNearestKeyframeIndex(float time)
        {
            if (_keyframes.Count == 0)
            {
                return -1;
            }

            int nearestIndex = 0;
            float bestDistance = Mathf.Abs(_keyframes[0].time - time);

            for (int i = 1; i < _keyframes.Count; i++)
            {
                float distance = Mathf.Abs(_keyframes[i].time - time);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private void EnsureTimelineHasKeyframes()
        {
            if (_keyframes.Count >= 2)
            {
                return;
            }

            BuildDefaultTimeline();
        }

        private void SortKeyframesByTime(KeyframeData keepSelection = null)
        {
            if (_keyframes.Count <= 1)
            {
                SyncSelectedKeyframesWithTimeline();
                return;
            }

            KeyframeData selectedRef = keepSelection;
            if (selectedRef == null && _selectedKeyframeIndex >= 0 && _selectedKeyframeIndex < _keyframes.Count)
            {
                selectedRef = _keyframes[_selectedKeyframeIndex];
            }

            _keyframes.Sort((left, right) => left.time.CompareTo(right.time));

            if (selectedRef != null)
            {
                _selectedKeyframeIndex = _keyframes.IndexOf(selectedRef);
            }

            if (_selectedKeyframeIndex < 0 && _keyframes.Count > 0)
            {
                _selectedKeyframeIndex = 0;
            }

            SyncSelectedKeyframesWithTimeline();
        }

        private void SelectKeyframe(int index, bool commitCurrent = true)
        {
            if (_keyframes.Count == 0)
            {
                return;
            }

            if (commitCurrent)
            {
                CommitEditorPoseToSelectedKeyframe();
            }

            _selectedKeyframeIndex = Mathf.Clamp(index, 0, _keyframes.Count - 1);
            _selectedKeyframes.Clear();
            _selectedKeyframes.Add(_keyframes[_selectedKeyframeIndex]);
            LoadSelectedKeyframeIntoEditor();
        }

        private void SelectAllKeyframes(bool commitCurrent = true)
        {
            if (_keyframes.Count == 0)
            {
                return;
            }

            if (commitCurrent)
            {
                CommitEditorPoseToSelectedKeyframe();
            }

            _selectedKeyframeIndex = Mathf.Clamp(_selectedKeyframeIndex, 0, _keyframes.Count - 1);
            _selectedKeyframes.Clear();
            for (int i = 0; i < _keyframes.Count; i++)
            {
                _selectedKeyframes.Add(_keyframes[i]);
            }

            LoadSelectedKeyframeIntoEditor();
        }

        private void LoadSelectedKeyframeIntoEditor()
        {
            SyncSelectedKeyframesWithTimeline();
            if (_selectedKeyframeIndex < 0 || _selectedKeyframeIndex >= _keyframes.Count)
            {
                return;
            }

            KeyframeData keyframe = _keyframes[_selectedKeyframeIndex];
            Array.Copy(keyframe.landmarks, _landmarks, LandmarkCount);
            Array.Copy(keyframe.trackedPoints, _trackedPoints, LandmarkCount);
            if (keyframe.hasWorldLandmarks && keyframe.worldLandmarks != null && keyframe.worldLandmarks.Length == LandmarkCount)
            {
                Array.Copy(keyframe.worldLandmarks, _worldLandmarks, LandmarkCount);
                _hasWorldLandmarksForEditorPose = true;
            }
            else
            {
                Array.Clear(_worldLandmarks, 0, _worldLandmarks.Length);
                _hasWorldLandmarksForEditorPose = false;
            }
            RebuildBoneConstraintsFromCurrentPose();
            Repaint();
        }

        private void CommitEditorPoseToSelectedKeyframe()
        {
            SyncSelectedKeyframesWithTimeline();
            if (_selectedKeyframeIndex < 0 || _selectedKeyframeIndex >= _keyframes.Count)
            {
                return;
            }

            CopyEditorPoseToKeyframe(_keyframes[_selectedKeyframeIndex]);
        }

        private void CommitEditorPoseToSelectedKeyframes()
        {
            SyncSelectedKeyframesWithTimeline();
            for (int i = 0; i < _keyframes.Count; i++)
            {
                KeyframeData keyframe = _keyframes[i];
                if (!_selectedKeyframes.Contains(keyframe))
                {
                    continue;
                }

                CopyEditorPoseToKeyframe(keyframe);
            }
        }

        private void CommitTrackedPointsToSelectedKeyframes()
        {
            SyncSelectedKeyframesWithTimeline();
            for (int i = 0; i < _keyframes.Count; i++)
            {
                KeyframeData keyframe = _keyframes[i];
                if (!_selectedKeyframes.Contains(keyframe))
                {
                    continue;
                }

                Array.Copy(_trackedPoints, keyframe.trackedPoints, LandmarkCount);
            }
        }

        private void CopyEditorPoseToKeyframe(KeyframeData keyframe)
        {
            if (keyframe == null)
            {
                return;
            }

            Array.Copy(_landmarks, keyframe.landmarks, LandmarkCount);
            Array.Copy(_trackedPoints, keyframe.trackedPoints, LandmarkCount);

            if (_hasWorldLandmarksForEditorPose)
            {
                if (keyframe.worldLandmarks == null || keyframe.worldLandmarks.Length != LandmarkCount)
                {
                    keyframe.worldLandmarks = new Vector3[LandmarkCount];
                }

                Array.Copy(_worldLandmarks, keyframe.worldLandmarks, LandmarkCount);
                keyframe.hasWorldLandmarks = true;
            }
            else
            {
                keyframe.worldLandmarks = null;
                keyframe.hasWorldLandmarks = false;
            }
        }

        private void StartPreviewPlayback()
        {
            if (_keyframes.Count < 2)
            {
                return;
            }

            CommitEditorPoseToSelectedKeyframe();
            _isPreviewPlaying = true;
            _previewStartEditorTime = EditorApplication.timeSinceStartup;
            _previewTime = GetSelectedTimelineTime();
            Repaint();
        }

        private void StopPreviewPlayback()
        {
            if (!_isPreviewPlaying)
            {
                return;
            }

            _isPreviewPlaying = false;
            LoadSelectedKeyframeIntoEditor();
        }

        private void UpdatePreviewPlayback()
        {
            if (!_isPreviewPlaying)
            {
                return;
            }

            float duration = GetTimelineDuration();
            if (duration <= 0.0001f || _keyframes.Count < 2)
            {
                StopPreviewPlayback();
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - _previewStartEditorTime;
            float wrappedTime = Mathf.Repeat((float)elapsed + GetSelectedTimelineTime(), duration);
            _previewTime = wrappedTime;

            SampleTimelineAtTime(_previewTime, _landmarks, _trackedPoints);
            RebuildBoneConstraintsFromCurrentPose();
            Repaint();
        }

        private void SampleTimelineAtTime(float time, Vector2[] outputLandmarks, bool[] outputTrackedPoints)
        {
            if (_keyframes.Count == 0)
            {
                return;
            }

            if (_keyframes.Count == 1)
            {
                Array.Copy(_keyframes[0].landmarks, outputLandmarks, LandmarkCount);
                Array.Copy(_keyframes[0].trackedPoints, outputTrackedPoints, LandmarkCount);
                return;
            }

            SortKeyframesByTime();

            if (time <= _keyframes[0].time)
            {
                Array.Copy(_keyframes[0].landmarks, outputLandmarks, LandmarkCount);
                Array.Copy(_keyframes[0].trackedPoints, outputTrackedPoints, LandmarkCount);
                return;
            }

            int lastIndex = _keyframes.Count - 1;
            if (time >= _keyframes[lastIndex].time)
            {
                Array.Copy(_keyframes[lastIndex].landmarks, outputLandmarks, LandmarkCount);
                Array.Copy(_keyframes[lastIndex].trackedPoints, outputTrackedPoints, LandmarkCount);
                return;
            }

            int rightIndex = 1;
            while (rightIndex < _keyframes.Count && _keyframes[rightIndex].time < time)
            {
                rightIndex++;
            }

            int leftIndex = Mathf.Max(0, rightIndex - 1);
            KeyframeData left = _keyframes[leftIndex];
            KeyframeData right = _keyframes[rightIndex];
            float t = Mathf.InverseLerp(left.time, right.time, time);

            for (int i = 0; i < LandmarkCount; i++)
            {
                outputLandmarks[i] = Vector2.Lerp(left.landmarks[i], right.landmarks[i], t);
                outputTrackedPoints[i] = left.trackedPoints[i] || right.trackedPoints[i];
            }
        }

        private void SaveMotionToJson()
        {
            CommitEditorPoseToSelectedKeyframe();

            if (_keyframes.Count < 2)
            {
                EditorUtility.DisplayDialog("Not Enough Keyframes", "Create at least 2 keyframes before saving motion JSON.", "OK");
                return;
            }

            EnsureMotionsFolderExists();
            string fileName = SanitizeFileName(_motionFileName);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "motion_gesture";
            }
            _motionFileName = fileName;

            string absolutePath = Path.Combine(GetMotionsFolderAbsolutePath(), fileName + ".json");
            if (File.Exists(absolutePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Overwrite Motion File",
                    "A motion file named '" + fileName + ".json' already exists in Assets/MotionGestures.\nDo you want to overwrite it?",
                    "Overwrite",
                    "Cancel");

                if (!overwrite)
                {
                    return;
                }
            }

            File.WriteAllText(absolutePath, ToJsonString(pretty: true));
            string assetPath = AbsolutePathToAssetPath(absolutePath);
            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath);
            }

            AssetDatabase.Refresh();
            string savedLocation = !string.IsNullOrEmpty(assetPath)
                ? assetPath
                : absolutePath;
            ShowNotification(new GUIContent("Saved " + fileName + ".json"));
            EditorUtility.DisplayDialog("Motion Saved", "Saved motion JSON to:\n" + savedLocation, "OK");
        }

        private void LoadMotionFromJson()
        {
            EnsureMotionsFolderExists();
            string motionsFolderPath = GetMotionsFolderAbsolutePath();
            string path = EditorUtility.OpenFilePanel("Load Motion JSON", motionsFolderPath, "json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            if (!IsPathInsideDirectory(path, motionsFolderPath))
            {
                EditorUtility.DisplayDialog("Invalid Motion File", "Select motion JSON from Assets/MotionGestures folder only.", "OK");
                return;
            }

            string json = File.ReadAllText(path);
            if (!TryApplyMotionJson(json))
            {
                EditorUtility.DisplayDialog("Invalid Motion JSON", "Expected motion JSON with frames[].trackedLandmarks (optionally trackedAngles/trackedWorldLandmarks) or compatible static pose JSON.", "OK");
                return;
            }

            ClearRecordedVideoBackgroundFrames();
            ClearRecordedWebcamBackgroundFrames();
            _motionFileName = Path.GetFileNameWithoutExtension(path);
            RebuildBoneConstraintsFromCurrentPose();
        }

        private bool TryApplyMotionJson(string json)
        {
            MotionJsonData motionData = JsonUtility.FromJson<MotionJsonData>(json);
            if (TryApplyMotionData(motionData))
            {
                return true;
            }

            PoseJsonData staticPoseData = JsonUtility.FromJson<PoseJsonData>(json);
            if (staticPoseData == null || staticPoseData.trackedLandmarks == null || staticPoseData.trackedLandmarks.Length < 2)
            {
                return false;
            }

            Vector2[] reconstructedLandmarks = new Vector2[LandmarkCount];
            bool[] reconstructedTracked = new bool[LandmarkCount];
            CopyPose(_defaultLandmarks, reconstructedLandmarks);
            for (int i = 0; i < staticPoseData.trackedLandmarks.Length; i++)
            {
                TrackedPosePoint point = staticPoseData.trackedLandmarks[i];
                if (point.index < 0 || point.index >= LandmarkCount)
                {
                    continue;
                }

                reconstructedLandmarks[point.index] = new Vector2(point.x, point.y);
                reconstructedTracked[point.index] = true;
            }

            _keyframes.Clear();
            AddKeyframe(0f, reconstructedLandmarks, reconstructedTracked);
            AddKeyframe(1f, reconstructedLandmarks, reconstructedTracked);
            _motionDuration = 1f;
            SelectKeyframe(0, commitCurrent: false);
            return true;
        }

        private bool TryApplyMotionData(MotionJsonData motionData)
        {
            if (motionData == null || motionData.frames == null || motionData.frames.Length == 0)
            {
                return false;
            }

            List<KeyframeData> loadedFrames = new List<KeyframeData>(motionData.frames.Length);
            for (int i = 0; i < motionData.frames.Length; i++)
            {
                MotionFrameJsonData frame = motionData.frames[i];
                if (frame == null || frame.trackedLandmarks == null || frame.trackedLandmarks.Length < 2)
                {
                    continue;
                }

                Vector2[] landmarks = new Vector2[LandmarkCount];
                bool[] tracked = new bool[LandmarkCount];
                Vector3[] worldLandmarks = null;
                CopyPose(_defaultLandmarks, landmarks);

                int trackedCount = 0;
                for (int p = 0; p < frame.trackedLandmarks.Length; p++)
                {
                    TrackedPosePoint point = frame.trackedLandmarks[p];
                    if (point.index < 0 || point.index >= LandmarkCount)
                    {
                        continue;
                    }

                    landmarks[point.index] = new Vector2(point.x, point.y);
                    if (!tracked[point.index])
                    {
                        tracked[point.index] = true;
                        trackedCount++;
                    }
                }

                if (trackedCount < 2)
                {
                    continue;
                }

                bool hasWorldLandmarks = frame.trackedWorldLandmarks != null && frame.trackedWorldLandmarks.Length >= 2;
                if (hasWorldLandmarks)
                {
                    worldLandmarks = new Vector3[LandmarkCount];
                    int validWorldCount = 0;
                    for (int w = 0; w < frame.trackedWorldLandmarks.Length; w++)
                    {
                        TrackedPoseWorldPoint point = frame.trackedWorldLandmarks[w];
                        if (point.index < 0 || point.index >= LandmarkCount)
                        {
                            continue;
                        }

                        worldLandmarks[point.index] = new Vector3(point.x, point.y, point.z);
                        validWorldCount++;
                    }

                    hasWorldLandmarks = validWorldCount >= 2;
                    if (!hasWorldLandmarks)
                    {
                        worldLandmarks = null;
                    }
                }

                loadedFrames.Add(new KeyframeData(Mathf.Max(0f, frame.time), landmarks, tracked, worldLandmarks, hasWorldLandmarks));
            }

            if (loadedFrames.Count < 2)
            {
                return false;
            }

            loadedFrames.Sort((a, b) => a.time.CompareTo(b.time));
            _keyframes.Clear();
            _keyframes.AddRange(loadedFrames);

            float loadedDuration = motionData.duration > 0f ? motionData.duration : loadedFrames[loadedFrames.Count - 1].time;
            _motionDuration = Mathf.Max(0.1f, loadedDuration);

            _selectedKeyframeIndex = 0;
            LoadSelectedKeyframeIntoEditor();
            return true;
        }

        private static string GetMotionsFolderAbsolutePath()
        {
            return Path.Combine(Application.dataPath, MotionsFolderRelativeToAssetsPath);
        }

        private static void EnsureMotionsFolderExists()
        {
            string folderPath = GetMotionsFolderAbsolutePath();
            if (Directory.Exists(folderPath))
            {
                return;
            }

            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        private static bool IsPathInsideDirectory(string filePath, string directoryPath)
        {
            string fullFilePath = Path.GetFullPath(filePath).Replace('\\', '/');
            string fullDirectoryPath = Path.GetFullPath(directoryPath).Replace('\\', '/').TrimEnd('/') + "/";
            return fullFilePath.StartsWith(fullDirectoryPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string AbsolutePathToAssetPath(string absolutePath)
        {
            string fullPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string assetsPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (!fullPath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "Assets" + fullPath.Substring(assetsPath.Length);
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            string sanitized = fileName.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                sanitized = sanitized.Replace(invalid[i], '_');
            }

            sanitized = sanitized.Replace(' ', '_');
            return sanitized;
        }

        private string ToJsonString(bool pretty)
        {
            CommitEditorPoseToSelectedKeyframe();
            SortKeyframesByTime();

            MotionJsonData data = new MotionJsonData();
            data.motionName = SanitizeFileName(_motionFileName);
            data.duration = GetTimelineDuration();
            data.sampleRate = EstimateTimelineSampleRate();
            BuildDeltaBasedTrackingFilters(
                out bool[] movingLandmarkMask,
                out bool[] movingWorldLandmarkMask,
                out bool[] movingAngleMask,
                out float[] landmarkMotionScore,
                out float[] worldMotionScore,
                out float[] angleMotionScore);

            List<MotionFrameJsonData> frames = new List<MotionFrameJsonData>(_keyframes.Count);
            for (int i = 0; i < _keyframes.Count; i++)
            {
                KeyframeData keyframe = _keyframes[i];
                List<TrackedPosePoint> trackedLandmarks = new List<TrackedPosePoint>();
                List<TrackedPoseAngle> trackedAngles = new List<TrackedPoseAngle>();
                List<TrackedPoseWorldPoint> trackedWorldLandmarks = null;

                for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
                {
                    if (!keyframe.trackedPoints[pointIndex] || !movingLandmarkMask[pointIndex])
                    {
                        continue;
                    }

                    trackedLandmarks.Add(new TrackedPosePoint(pointIndex, keyframe.landmarks[pointIndex].x, keyframe.landmarks[pointIndex].y));
                }
                EnsureMinimumTrackedLandmarksForFrame(keyframe, trackedLandmarks, landmarkMotionScore, minimumRequired: 2);

                if (keyframe.hasWorldLandmarks && keyframe.worldLandmarks != null && keyframe.worldLandmarks.Length == LandmarkCount)
                {
                    trackedWorldLandmarks = new List<TrackedPoseWorldPoint>(trackedLandmarks.Count);
                    for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
                    {
                        if (!keyframe.trackedPoints[pointIndex] || !movingWorldLandmarkMask[pointIndex])
                        {
                            continue;
                        }

                        Vector3 world = keyframe.worldLandmarks[pointIndex];
                        trackedWorldLandmarks.Add(new TrackedPoseWorldPoint(pointIndex, world.x, world.y, world.z));
                    }

                    EnsureMinimumTrackedWorldLandmarksForFrame(keyframe, trackedWorldLandmarks, worldMotionScore, minimumRequired: 3);
                }

                for (int angleIndex = 0; angleIndex < TrackedAngleDefinitions.Length; angleIndex++)
                {
                    PoseAngleDefinition definition = TrackedAngleDefinitions[angleIndex];
                    if (!movingAngleMask[angleIndex])
                    {
                        continue;
                    }

                    if (!keyframe.trackedPoints[definition.A] || !keyframe.trackedPoints[definition.B] || !keyframe.trackedPoints[definition.C])
                    {
                        continue;
                    }

                    if (!TryCalculateJointAngle(keyframe.landmarks, definition.A, definition.B, definition.C, out float angle))
                    {
                        continue;
                    }

                    trackedAngles.Add(new TrackedPoseAngle(definition.Name, definition.A, definition.B, definition.C, angle));
                }
                EnsureMinimumTrackedAnglesForFrame(keyframe, trackedAngles, angleMotionScore, minimumRequired: 1);

                MotionFrameJsonData frame = new MotionFrameJsonData
                {
                    time = keyframe.time,
                    trackedLandmarks = trackedLandmarks.ToArray(),
                    trackedAngles = trackedAngles.ToArray(),
                    trackedWorldLandmarks = trackedWorldLandmarks != null ? trackedWorldLandmarks.ToArray() : null
                };
                frames.Add(frame);
            }

            data.frames = frames.ToArray();
            return JsonUtility.ToJson(data, pretty);
        }

        private void BuildDeltaBasedTrackingFilters(
            out bool[] movingLandmarkMask,
            out bool[] movingWorldLandmarkMask,
            out bool[] movingAngleMask,
            out float[] landmarkMotionScore,
            out float[] worldMotionScore,
            out float[] angleMotionScore)
        {
            movingLandmarkMask = new bool[LandmarkCount];
            movingWorldLandmarkMask = new bool[LandmarkCount];
            movingAngleMask = new bool[TrackedAngleDefinitions.Length];
            landmarkMotionScore = new float[LandmarkCount];
            worldMotionScore = new float[LandmarkCount];
            angleMotionScore = new float[TrackedAngleDefinitions.Length];

            if (_keyframes == null || _keyframes.Count == 0)
            {
                return;
            }

            float[] landmarkMaxDelta = new float[LandmarkCount];
            float[] landmarkTotalDelta = new float[LandmarkCount];
            bool[] landmarkSeen = new bool[LandmarkCount];
            Vector2[] previousLandmarks = new Vector2[LandmarkCount];
            bool[] hasPreviousLandmarks = new bool[LandmarkCount];

            float[] worldMaxDelta = new float[LandmarkCount];
            float[] worldTotalDelta = new float[LandmarkCount];
            bool[] worldSeen = new bool[LandmarkCount];
            Vector3[] previousWorldLandmarks = new Vector3[LandmarkCount];
            bool[] hasPreviousWorldLandmarks = new bool[LandmarkCount];

            for (int frameIndex = 0; frameIndex < _keyframes.Count; frameIndex++)
            {
                KeyframeData frame = _keyframes[frameIndex];
                if (frame == null || frame.trackedPoints == null)
                {
                    continue;
                }

                for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
                {
                    if (!frame.trackedPoints[pointIndex])
                    {
                        continue;
                    }

                    Vector2 point = frame.landmarks[pointIndex];
                    landmarkSeen[pointIndex] = true;
                    if (hasPreviousLandmarks[pointIndex])
                    {
                        float delta = Vector2.Distance(previousLandmarks[pointIndex], point);
                        landmarkMaxDelta[pointIndex] = Mathf.Max(landmarkMaxDelta[pointIndex], delta);
                        landmarkTotalDelta[pointIndex] += delta;
                    }

                    previousLandmarks[pointIndex] = point;
                    hasPreviousLandmarks[pointIndex] = true;

                    if (!frame.hasWorldLandmarks || frame.worldLandmarks == null || frame.worldLandmarks.Length != LandmarkCount)
                    {
                        continue;
                    }

                    Vector3 worldPoint = frame.worldLandmarks[pointIndex];
                    worldSeen[pointIndex] = true;
                    if (hasPreviousWorldLandmarks[pointIndex])
                    {
                        float deltaWorld = Vector3.Distance(previousWorldLandmarks[pointIndex], worldPoint);
                        worldMaxDelta[pointIndex] = Mathf.Max(worldMaxDelta[pointIndex], deltaWorld);
                        worldTotalDelta[pointIndex] += deltaWorld;
                    }

                    previousWorldLandmarks[pointIndex] = worldPoint;
                    hasPreviousWorldLandmarks[pointIndex] = true;
                }
            }

            float scale2D = EstimateTrackedLandmarkScale2D();
            float landmarkPeakThreshold = Mathf.Clamp(scale2D * MotionDeltaScaleFactor2D, MotionDeltaMin2D, MotionDeltaMax2D);
            float landmarkAccumThreshold = landmarkPeakThreshold * MotionDeltaAccumulationMultiplier;
            for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
            {
                landmarkMotionScore[pointIndex] = landmarkMaxDelta[pointIndex] + (landmarkTotalDelta[pointIndex] * 0.25f);

                bool isMoving = landmarkSeen[pointIndex] &&
                    (landmarkMaxDelta[pointIndex] >= landmarkPeakThreshold ||
                     landmarkTotalDelta[pointIndex] >= landmarkAccumThreshold);

                if (!isMoving &&
                    landmarkSeen[pointIndex] &&
                    IsCoreLandmarkIndex(pointIndex) &&
                    landmarkMaxDelta[pointIndex] >= landmarkPeakThreshold * CorePointDeltaAssistMultiplier)
                {
                    isMoving = true;
                }

                movingLandmarkMask[pointIndex] = isMoving;
            }

            EnsureMinimumMovingIndices(movingLandmarkMask, landmarkSeen, landmarkMotionScore, minimumRequired: 2);

            float scale3D = EstimateTrackedLandmarkScale3D();
            float worldPeakThreshold = Mathf.Clamp(scale3D * MotionDeltaScaleFactor3D, MotionDeltaMin3D, MotionDeltaMax3D);
            float worldAccumThreshold = worldPeakThreshold * 1.6f;
            for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
            {
                worldMotionScore[pointIndex] = worldMaxDelta[pointIndex] + (worldTotalDelta[pointIndex] * 0.25f);

                bool isMoving = worldSeen[pointIndex] &&
                    (worldMaxDelta[pointIndex] >= worldPeakThreshold ||
                     worldTotalDelta[pointIndex] >= worldAccumThreshold);

                if (!isMoving &&
                    worldSeen[pointIndex] &&
                    movingLandmarkMask[pointIndex] &&
                    worldMaxDelta[pointIndex] >= worldPeakThreshold * 0.6f)
                {
                    isMoving = true;
                }

                movingWorldLandmarkMask[pointIndex] = isMoving;
            }

            int worldSeenCount = 0;
            for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
            {
                if (worldSeen[pointIndex])
                {
                    worldSeenCount++;
                }
            }
            if (worldSeenCount >= 3)
            {
                EnsureMinimumMovingIndices(movingWorldLandmarkMask, worldSeen, worldMotionScore, minimumRequired: 3);
            }

            float[] angleMaxDelta = new float[TrackedAngleDefinitions.Length];
            float[] angleTotalDelta = new float[TrackedAngleDefinitions.Length];
            float[] previousAngles = new float[TrackedAngleDefinitions.Length];
            bool[] angleSeen = new bool[TrackedAngleDefinitions.Length];
            bool[] hasPreviousAngles = new bool[TrackedAngleDefinitions.Length];

            for (int frameIndex = 0; frameIndex < _keyframes.Count; frameIndex++)
            {
                KeyframeData frame = _keyframes[frameIndex];
                if (frame == null || frame.trackedPoints == null)
                {
                    continue;
                }

                for (int angleIndex = 0; angleIndex < TrackedAngleDefinitions.Length; angleIndex++)
                {
                    PoseAngleDefinition definition = TrackedAngleDefinitions[angleIndex];
                    if (!frame.trackedPoints[definition.A] || !frame.trackedPoints[definition.B] || !frame.trackedPoints[definition.C])
                    {
                        continue;
                    }

                    if (!TryCalculateJointAngle(frame.landmarks, definition.A, definition.B, definition.C, out float angle))
                    {
                        continue;
                    }

                    angleSeen[angleIndex] = true;
                    if (hasPreviousAngles[angleIndex])
                    {
                        float delta = Mathf.Abs(Mathf.DeltaAngle(previousAngles[angleIndex], angle));
                        angleMaxDelta[angleIndex] = Mathf.Max(angleMaxDelta[angleIndex], delta);
                        angleTotalDelta[angleIndex] += delta;
                    }

                    previousAngles[angleIndex] = angle;
                    hasPreviousAngles[angleIndex] = true;
                }
            }

            for (int angleIndex = 0; angleIndex < TrackedAngleDefinitions.Length; angleIndex++)
            {
                angleMotionScore[angleIndex] = angleMaxDelta[angleIndex] + (angleTotalDelta[angleIndex] * 0.2f);
                if (!angleSeen[angleIndex])
                {
                    continue;
                }

                PoseAngleDefinition definition = TrackedAngleDefinitions[angleIndex];
                bool isMoving = angleMaxDelta[angleIndex] >= AngleMotionDeltaThreshold ||
                    angleTotalDelta[angleIndex] >= AngleMotionAccumulationThreshold;

                if (!isMoving &&
                    (movingLandmarkMask[definition.A] || movingLandmarkMask[definition.B] || movingLandmarkMask[definition.C]) &&
                    angleMaxDelta[angleIndex] >= 4f)
                {
                    isMoving = true;
                }

                movingAngleMask[angleIndex] = isMoving;
            }

            EnsureMinimumMovingIndices(movingAngleMask, angleSeen, angleMotionScore, minimumRequired: 1);
        }

        private float EstimateTrackedLandmarkScale2D()
        {
            float total = 0f;
            int count = 0;

            for (int frameIndex = 0; frameIndex < _keyframes.Count; frameIndex++)
            {
                KeyframeData frame = _keyframes[frameIndex];
                if (frame == null || frame.landmarks == null || frame.trackedPoints == null)
                {
                    continue;
                }

                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                int included = 0;

                for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
                {
                    if (!frame.trackedPoints[pointIndex])
                    {
                        continue;
                    }

                    Vector2 point = frame.landmarks[pointIndex];
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                    included++;
                }

                if (included < 2)
                {
                    continue;
                }

                float diagonal = Vector2.Distance(min, max);
                if (diagonal <= MinDistanceEpsilon)
                {
                    continue;
                }

                total += diagonal;
                count++;
            }

            if (count <= 0)
            {
                return 0.5f;
            }

            return total / count;
        }

        private float EstimateTrackedLandmarkScale3D()
        {
            float total = 0f;
            int count = 0;

            for (int frameIndex = 0; frameIndex < _keyframes.Count; frameIndex++)
            {
                KeyframeData frame = _keyframes[frameIndex];
                if (frame == null ||
                    !frame.hasWorldLandmarks ||
                    frame.worldLandmarks == null ||
                    frame.worldLandmarks.Length != LandmarkCount ||
                    frame.trackedPoints == null)
                {
                    continue;
                }

                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                int included = 0;

                for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
                {
                    if (!frame.trackedPoints[pointIndex])
                    {
                        continue;
                    }

                    Vector3 point = frame.worldLandmarks[pointIndex];
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                    included++;
                }

                if (included < 3)
                {
                    continue;
                }

                float diagonal = Vector3.Distance(min, max);
                if (diagonal <= MinDistanceEpsilon)
                {
                    continue;
                }

                total += diagonal;
                count++;
            }

            if (count <= 0)
            {
                return 1f;
            }

            return total / count;
        }

        private static void EnsureMinimumMovingIndices(bool[] movingMask, bool[] seenMask, float[] motionScore, int minimumRequired)
        {
            if (movingMask == null || seenMask == null || motionScore == null || minimumRequired <= 0)
            {
                return;
            }

            int movingCount = 0;
            int seenCount = 0;
            for (int i = 0; i < movingMask.Length && i < seenMask.Length; i++)
            {
                if (seenMask[i])
                {
                    seenCount++;
                }

                if (movingMask[i])
                {
                    movingCount++;
                }
            }

            int targetCount = Mathf.Min(minimumRequired, seenCount);
            while (movingCount < targetCount)
            {
                int bestIndex = -1;
                float bestScore = float.NegativeInfinity;
                for (int i = 0; i < movingMask.Length && i < seenMask.Length && i < motionScore.Length; i++)
                {
                    if (!seenMask[i] || movingMask[i])
                    {
                        continue;
                    }

                    if (motionScore[i] > bestScore)
                    {
                        bestScore = motionScore[i];
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                movingMask[bestIndex] = true;
                movingCount++;
            }
        }

        private static void EnsureMinimumTrackedLandmarksForFrame(
            KeyframeData keyframe,
            List<TrackedPosePoint> trackedLandmarks,
            float[] landmarkMotionScore,
            int minimumRequired)
        {
            if (keyframe == null || trackedLandmarks == null || keyframe.trackedPoints == null || minimumRequired <= 0)
            {
                return;
            }

            bool[] added = new bool[LandmarkCount];
            for (int i = 0; i < trackedLandmarks.Count; i++)
            {
                int index = trackedLandmarks[i].index;
                if (index >= 0 && index < LandmarkCount)
                {
                    added[index] = true;
                }
            }

            while (trackedLandmarks.Count < minimumRequired)
            {
                int bestIndex = -1;
                float bestScore = float.NegativeInfinity;
                for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
                {
                    if (!keyframe.trackedPoints[pointIndex] || added[pointIndex])
                    {
                        continue;
                    }

                    float score = landmarkMotionScore != null && pointIndex < landmarkMotionScore.Length
                        ? landmarkMotionScore[pointIndex]
                        : 0f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = pointIndex;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                trackedLandmarks.Add(new TrackedPosePoint(bestIndex, keyframe.landmarks[bestIndex].x, keyframe.landmarks[bestIndex].y));
                added[bestIndex] = true;
            }
        }

        private static void EnsureMinimumTrackedWorldLandmarksForFrame(
            KeyframeData keyframe,
            List<TrackedPoseWorldPoint> trackedWorldLandmarks,
            float[] worldMotionScore,
            int minimumRequired)
        {
            if (keyframe == null ||
                trackedWorldLandmarks == null ||
                !keyframe.hasWorldLandmarks ||
                keyframe.worldLandmarks == null ||
                keyframe.worldLandmarks.Length != LandmarkCount ||
                keyframe.trackedPoints == null ||
                minimumRequired <= 0)
            {
                return;
            }

            bool[] added = new bool[LandmarkCount];
            for (int i = 0; i < trackedWorldLandmarks.Count; i++)
            {
                int index = trackedWorldLandmarks[i].index;
                if (index >= 0 && index < LandmarkCount)
                {
                    added[index] = true;
                }
            }

            while (trackedWorldLandmarks.Count < minimumRequired)
            {
                int bestIndex = -1;
                float bestScore = float.NegativeInfinity;
                for (int pointIndex = 0; pointIndex < LandmarkCount; pointIndex++)
                {
                    if (!keyframe.trackedPoints[pointIndex] || added[pointIndex])
                    {
                        continue;
                    }

                    float score = worldMotionScore != null && pointIndex < worldMotionScore.Length
                        ? worldMotionScore[pointIndex]
                        : 0f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = pointIndex;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                Vector3 worldPoint = keyframe.worldLandmarks[bestIndex];
                trackedWorldLandmarks.Add(new TrackedPoseWorldPoint(bestIndex, worldPoint.x, worldPoint.y, worldPoint.z));
                added[bestIndex] = true;
            }
        }

        private static void EnsureMinimumTrackedAnglesForFrame(
            KeyframeData keyframe,
            List<TrackedPoseAngle> trackedAngles,
            float[] angleMotionScore,
            int minimumRequired)
        {
            if (keyframe == null || trackedAngles == null || keyframe.trackedPoints == null || minimumRequired <= 0)
            {
                return;
            }

            bool[] addedDefinitions = new bool[TrackedAngleDefinitions.Length];
            for (int i = 0; i < trackedAngles.Count; i++)
            {
                TrackedPoseAngle existing = trackedAngles[i];
                for (int definitionIndex = 0; definitionIndex < TrackedAngleDefinitions.Length; definitionIndex++)
                {
                    PoseAngleDefinition definition = TrackedAngleDefinitions[definitionIndex];
                    if (existing.a == definition.A && existing.b == definition.B && existing.c == definition.C)
                    {
                        addedDefinitions[definitionIndex] = true;
                        break;
                    }
                }
            }

            while (trackedAngles.Count < minimumRequired)
            {
                int bestDefinition = -1;
                float bestScore = float.NegativeInfinity;
                float bestAngle = 0f;

                for (int definitionIndex = 0; definitionIndex < TrackedAngleDefinitions.Length; definitionIndex++)
                {
                    if (addedDefinitions[definitionIndex])
                    {
                        continue;
                    }

                    PoseAngleDefinition definition = TrackedAngleDefinitions[definitionIndex];
                    if (!keyframe.trackedPoints[definition.A] ||
                        !keyframe.trackedPoints[definition.B] ||
                        !keyframe.trackedPoints[definition.C])
                    {
                        continue;
                    }

                    if (!TryCalculateJointAngle(keyframe.landmarks, definition.A, definition.B, definition.C, out float angle))
                    {
                        continue;
                    }

                    float score = angleMotionScore != null && definitionIndex < angleMotionScore.Length
                        ? angleMotionScore[definitionIndex]
                        : 0f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDefinition = definitionIndex;
                        bestAngle = angle;
                    }
                }

                if (bestDefinition < 0)
                {
                    break;
                }

                PoseAngleDefinition selected = TrackedAngleDefinitions[bestDefinition];
                trackedAngles.Add(new TrackedPoseAngle(selected.Name, selected.A, selected.B, selected.C, bestAngle));
                addedDefinitions[bestDefinition] = true;
            }
        }

        private static bool IsCoreLandmarkIndex(int index)
        {
            switch (index)
            {
                case 11:
                case 12:
                case 23:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                case 30:
                case 31:
                case 32:
                    return true;
                default:
                    return false;
            }
        }

        private float EstimateTimelineSampleRate()
        {
            if (_keyframes.Count < 2)
            {
                return 1f;
            }

            float totalDelta = 0f;
            int pairCount = 0;
            for (int i = 1; i < _keyframes.Count; i++)
            {
                float delta = Mathf.Abs(_keyframes[i].time - _keyframes[i - 1].time);
                if (delta <= 0.0001f)
                {
                    continue;
                }

                totalDelta += delta;
                pairCount++;
            }

            if (pairCount == 0)
            {
                return 1f;
            }

            float averageDelta = totalDelta / pairCount;
            return Mathf.Clamp(1f / Mathf.Max(averageDelta, 0.0001f), 1f, 120f);
        }

        private static bool TryCalculateJointAngle(Vector2[] landmarks, int a, int b, int c, out float angle)
        {
            angle = 0f;
            if (landmarks == null ||
                a < 0 || a >= landmarks.Length ||
                b < 0 || b >= landmarks.Length ||
                c < 0 || c >= landmarks.Length)
            {
                return false;
            }

            Vector2 fromPivotToA = landmarks[a] - landmarks[b];
            Vector2 fromPivotToC = landmarks[c] - landmarks[b];

            float magnitudeA = fromPivotToA.magnitude;
            float magnitudeC = fromPivotToC.magnitude;
            if (magnitudeA < MinDistanceEpsilon || magnitudeC < MinDistanceEpsilon)
            {
                return false;
            }

            float dot = Vector2.Dot(fromPivotToA / magnitudeA, fromPivotToC / magnitudeC);
            dot = Mathf.Clamp(dot, -1f, 1f);
            angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return true;
        }

        private void StartVideoImport()
        {
            if (_videoImport != null)
            {
                return;
            }

            if (_webcamRecord != null)
            {
                EditorUtility.DisplayDialog("Webcam Recording Running", "Stop or cancel webcam recording first.", "OK");
                return;
            }

            bool hasVideoPath = !string.IsNullOrEmpty(_videoFilePath) && File.Exists(_videoFilePath);
            if (!hasVideoPath)
            {
                EditorUtility.DisplayDialog("Video Required", "Pick a video file first.", "OK");
                return;
            }

            string modelPath = ResolvePoseLandmarkerModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                EditorUtility.DisplayDialog("Model Missing", "Could not find pose model '" + DefaultPoseLandmarkerModelName + "' in StreamingAssets.", "OK");
                return;
            }

            ClearRecordedWebcamBackgroundFrames();

            VideoImportContext context = new VideoImportContext();
            context.flipX = _flipImportedVideoX;
            context.startedEditorTime = EditorApplication.timeSinceStartup;

            try
            {
                context.host = new GameObject("MotionGestureVideoImportTemp");
                context.host.hideFlags = HideFlags.HideAndDontSave;

                context.videoPlayer = context.host.AddComponent<VideoPlayer>();
                context.videoPlayer.playOnAwake = false;
                context.videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                context.videoPlayer.waitForFirstFrame = true;
                context.videoPlayer.skipOnDrop = false;
                context.videoPlayer.isLooping = false;
                context.videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                context.videoPlayer.sendFrameReadyEvents = true;

                context.videoPlayer.source = VideoSource.Url;
                context.videoPlayer.url = _videoFilePath;

                var options = new PoseLandmarkerOptions(
                    new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: modelPath),
                    runningMode: RunningMode.IMAGE,
                    numPoses: 1,
                    minPoseDetectionConfidence: 0.5f,
                    minPosePresenceConfidence: 0.5f,
                    minTrackingConfidence: 0.5f,
                    outputSegmentationMasks: false);

                context.poseLandmarker = PoseLandmarker.CreateFromOptions(options);

                context.videoPlayer.frameReady += OnVideoFrameReady;
                context.videoPlayer.loopPointReached += OnVideoLoopPointReached;

                _videoImport = context;
                EditorApplication.update += OnVideoImportUpdate;
                context.videoPlayer.Prepare();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                context.Dispose();
                _videoImport = null;
                EditorApplication.update -= OnVideoImportUpdate;
                EditorUtility.DisplayDialog("Video Import Failed", "Could not start video import:\n" + exception.Message, "OK");
            }
        }

        private void OnVideoImportUpdate()
        {
            if (_videoImport == null)
            {
                EditorApplication.update -= OnVideoImportUpdate;
                return;
            }

            if (_videoImport.canceled)
            {
                FinalizeVideoImport(success: false, "Video import canceled.");
                return;
            }

            if (!_videoImport.prepared)
            {
                if (_videoImport.videoPlayer != null && _videoImport.videoPlayer.isPrepared)
                {
                    PrepareVideoImportPlayback();
                }
                else
                {
                    double elapsed = EditorApplication.timeSinceStartup - _videoImport.startedEditorTime;
                    if (elapsed > 20.0)
                    {
                        FinalizeVideoImport(success: false, "Video preparation timed out.");
                    }
                }
            }
            else
            {
                if (_videoImport.sampledFrameCount >= _videoImport.targetFrameCount)
                {
                    FinalizeVideoImport(success: true);
                }
                else if (_videoImport.videoPlayer != null &&
                         !_videoImport.videoPlayer.isPlaying &&
                         _videoImport.videoPlayer.frame > 0)
                {
                    FinalizeVideoImport(success: true);
                }
            }

            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }

        private void PrepareVideoImportPlayback()
        {
            if (_videoImport == null || _videoImport.videoPlayer == null)
            {
                return;
            }

            VideoPlayer player = _videoImport.videoPlayer;
            int width = Mathf.Max(16, (int)player.width);
            int height = Mathf.Max(16, (int)player.height);
            _videoImport.renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _videoImport.renderTexture.Create();
            _videoImport.readbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            player.targetTexture = _videoImport.renderTexture;

            float sourceDuration = (float)player.length;
            if (float.IsNaN(sourceDuration) || float.IsInfinity(sourceDuration) || sourceDuration <= 0f)
            {
                float frameRate = (float)player.frameRate;
                if (frameRate > 0f && player.frameCount > 0)
                {
                    sourceDuration = (float)(player.frameCount / frameRate);
                }
                else
                {
                    sourceDuration = _videoMaxDuration;
                }
            }

            _videoImport.duration = Mathf.Max(0.1f, Mathf.Min(_videoMaxDuration, sourceDuration));
            _videoImport.sampleInterval = 1f / Mathf.Clamp(_videoSampleRate, 2, 8);
            _videoImport.targetFrameCount = Mathf.Clamp(
                Mathf.FloorToInt(_videoImport.duration / _videoImport.sampleInterval) + 1,
                2,
                _videoMaxFrames);
            _videoImport.nextSampleTime = 0f;
            _videoImport.sampledFrameCount = 0;
            _videoImport.detectedFrameCount = 0;
            _videoImport.prepared = true;

            player.Play();
        }

        private void OnVideoFrameReady(VideoPlayer source, long frameIdx)
        {
            if (_videoImport == null || !_videoImport.prepared || source != _videoImport.videoPlayer)
            {
                return;
            }

            float currentTime = (float)source.time;
            if (currentTime + 0.0005f < _videoImport.nextSampleTime)
            {
                return;
            }

            TryCaptureVideoFrame(currentTime);

            while (_videoImport.nextSampleTime <= currentTime + 0.0005f)
            {
                _videoImport.nextSampleTime += _videoImport.sampleInterval;
            }

            if (_videoImport.sampledFrameCount >= _videoImport.targetFrameCount)
            {
                FinalizeVideoImport(success: true);
            }
        }

        private void TryCaptureVideoFrame(float frameTime)
        {
            if (_videoImport == null || _videoImport.renderTexture == null || _videoImport.readbackTexture == null)
            {
                return;
            }

            float keyframeTime = Mathf.Clamp(_videoImport.nextSampleTime, 0f, _videoImport.duration);
            _videoImport.sampledFrameCount++;

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _videoImport.renderTexture;
                _videoImport.readbackTexture.ReadPixels(new Rect(0f, 0f, _videoImport.renderTexture.width, _videoImport.renderTexture.height), 0, 0, false);
                _videoImport.readbackTexture.Apply(false, false);
                Texture2D previewFrame = CreatePreviewFrameTexture(_videoImport.readbackTexture, TimelinePreviewFrameMaxDimension);
                if (previewFrame != null)
                {
                    _videoImport.sampledFrames.Add(new WebcamBackgroundFrame(keyframeTime, previewFrame));
                }

                if (!TryExtractPoseFromTexture(
                    _videoImport.readbackTexture,
                    _videoImport.poseLandmarker,
                    _videoImport.flipX,
                    out Vector2[] detectedLandmarks,
                    out bool[] detectedTracked,
                    out Vector3[] detectedWorldLandmarks,
                    out bool hasDetectedWorldLandmarks,
                    out _))
                {
                    return;
                }

                _videoImport.detectedFrameCount++;
                _videoImport.capturedFrames.Add(new KeyframeData(keyframeTime, detectedLandmarks, detectedTracked, detectedWorldLandmarks, hasDetectedWorldLandmarks));
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void OnVideoLoopPointReached(VideoPlayer source)
        {
            if (_videoImport != null && source == _videoImport.videoPlayer)
            {
                FinalizeVideoImport(success: true);
            }
        }

        private void CancelVideoImport(bool showDialog = true)
        {
            if (_videoImport == null)
            {
                return;
            }

            _videoImport.canceled = true;
            FinalizeVideoImport(success: false, showFailureDialog: showDialog, error: showDialog ? "Video import canceled." : null);
        }

        private void FinalizeVideoImport(bool success, string error = null, bool showFailureDialog = true)
        {
            if (_videoImport == null)
            {
                return;
            }

            VideoImportContext context = _videoImport;
            _videoImport = null;
            EditorApplication.update -= OnVideoImportUpdate;

            if (context.videoPlayer != null)
            {
                context.videoPlayer.frameReady -= OnVideoFrameReady;
                context.videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            }

            if (success && context.capturedFrames.Count >= 2)
            {
                context.capturedFrames.Sort((a, b) => a.time.CompareTo(b.time));
                float firstTime = context.capturedFrames[0].time;
                for (int i = 0; i < context.capturedFrames.Count; i++)
                {
                    context.capturedFrames[i].time -= firstTime;
                }

                _keyframes.Clear();
                for (int i = 0; i < context.capturedFrames.Count; i++)
                {
                    _keyframes.Add(context.capturedFrames[i].Clone());
                }

                _motionDuration = Mathf.Max(0.1f, _keyframes[_keyframes.Count - 1].time);
                _selectedKeyframeIndex = 0;
                LoadSelectedKeyframeIntoEditor();
                ClearRecordedVideoBackgroundFrames();
                if (context.sampledFrames.Count > 0)
                {
                    context.sampledFrames.Sort((a, b) => a.time.CompareTo(b.time));
                    float firstSampleTime = context.sampledFrames[0].time;
                    for (int i = 0; i < context.sampledFrames.Count; i++)
                    {
                        WebcamBackgroundFrame frame = context.sampledFrames[i];
                        frame.time = Mathf.Max(0f, frame.time - firstSampleTime);
                        _recordedVideoBackgroundFrames.Add(frame);
                    }

                    context.sampledFrames.Clear();
                    _recordedVideoBackgroundDuration = _recordedVideoBackgroundFrames[_recordedVideoBackgroundFrames.Count - 1].time;
                    if (_recordedVideoBackgroundDuration <= 0f)
                    {
                        _recordedVideoBackgroundDuration = _motionDuration;
                    }
                }
                else
                {
                    _recordedVideoBackgroundDuration = 0f;
                }

                ReleaseVideoBackgroundPlayer();

                if (string.IsNullOrWhiteSpace(_motionFileName))
                {
                    if (!string.IsNullOrEmpty(_videoFilePath))
                    {
                        _motionFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(_videoFilePath));
                    }
                }

                EditorUtility.DisplayDialog(
                    "Video Import Complete",
                    "Sampled " + context.sampledFrameCount + " frames.\nDetected pose in " + context.detectedFrameCount + " frames.\nCreated " + _keyframes.Count + " keyframes.",
                    "OK");
            }
            else if (showFailureDialog && !string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Video Import Failed", error, "OK");
            }
            else if (showFailureDialog)
            {
                EditorUtility.DisplayDialog("Video Import Failed", "Could not detect enough poses to build a timeline.", "OK");
            }

            context.Dispose();
            Repaint();
        }

        private void EnsureVideoBackgroundState()
        {
            if (_showVideoBackground && _recordedVideoBackgroundFrames.Count > 0 && _videoImport == null)
            {
                if (_videoBackgroundPlayer != null)
                {
                    ReleaseVideoBackgroundPlayer();
                }

                return;
            }

            bool hasValidVideoPath = !string.IsNullOrEmpty(_videoFilePath) && File.Exists(_videoFilePath);
            if (!hasValidVideoPath || !_showVideoBackground || _videoImport != null)
            {
                if (_videoBackgroundPlayer != null)
                {
                    ReleaseVideoBackgroundPlayer();
                }
                return;
            }

            if (_videoBackgroundPlayer == null || !string.Equals(_videoBackgroundLoadedPath, _videoFilePath, StringComparison.Ordinal))
            {
                ReleaseVideoBackgroundPlayer();
                CreateVideoBackgroundPlayer(_videoFilePath);
                return;
            }

            if (!_videoBackgroundPlayer.isPrepared)
            {
                return;
            }

            EnsureVideoBackgroundRenderTexture();
            UpdateVideoBackgroundFrameFromTimeline();
        }

        private void CreateVideoBackgroundPlayer(string videoPath)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                return;
            }

            try
            {
                _videoBackgroundHost = new GameObject("MotionGestureVideoBackgroundTemp");
                _videoBackgroundHost.hideFlags = HideFlags.HideAndDontSave;

                _videoBackgroundPlayer = _videoBackgroundHost.AddComponent<VideoPlayer>();
                _videoBackgroundPlayer.playOnAwake = false;
                _videoBackgroundPlayer.audioOutputMode = VideoAudioOutputMode.None;
                _videoBackgroundPlayer.waitForFirstFrame = true;
                _videoBackgroundPlayer.skipOnDrop = false;
                _videoBackgroundPlayer.isLooping = false;
                _videoBackgroundPlayer.renderMode = VideoRenderMode.RenderTexture;
                _videoBackgroundPlayer.sendFrameReadyEvents = true;
                _videoBackgroundPlayer.source = VideoSource.Url;
                _videoBackgroundPlayer.url = videoPath;
                _videoBackgroundPlayer.prepareCompleted += OnVideoBackgroundPrepared;
                _videoBackgroundPlayer.errorReceived += OnVideoBackgroundError;
                _videoBackgroundPlayer.frameReady += OnVideoBackgroundFrameReady;

                _videoBackgroundLoadedPath = videoPath;
                _lastVideoBackgroundTime = -1d;
                _lastVideoBackgroundFrame = -1L;
                _videoBackgroundPlayer.Prepare();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReleaseVideoBackgroundPlayer();
            }
        }

        private void EnsureVideoBackgroundRenderTexture()
        {
            if (_videoBackgroundPlayer == null || !_videoBackgroundPlayer.isPrepared)
            {
                return;
            }

            int width = Mathf.Max(16, (int)_videoBackgroundPlayer.width);
            int height = Mathf.Max(16, (int)_videoBackgroundPlayer.height);
            if (_videoBackgroundRenderTexture != null &&
                _videoBackgroundRenderTexture.width == width &&
                _videoBackgroundRenderTexture.height == height)
            {
                if (_videoBackgroundPlayer.targetTexture != _videoBackgroundRenderTexture)
                {
                    _videoBackgroundPlayer.targetTexture = _videoBackgroundRenderTexture;
                }

                return;
            }

            if (_videoBackgroundRenderTexture != null)
            {
                _videoBackgroundRenderTexture.Release();
                DestroyImmediate(_videoBackgroundRenderTexture);
                _videoBackgroundRenderTexture = null;
            }

            _videoBackgroundRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _videoBackgroundRenderTexture.Create();
            _videoBackgroundPlayer.targetTexture = _videoBackgroundRenderTexture;
        }

        private void UpdateVideoBackgroundFrameFromTimeline(bool force = false)
        {
            if (_videoBackgroundPlayer == null || !_videoBackgroundPlayer.isPrepared || _videoBackgroundRenderTexture == null)
            {
                return;
            }

            double timelineDuration = Math.Max(0.0001d, GetTimelineDuration());
            double timelineTime = Math.Max(0d, _isPreviewPlaying ? _previewTime : GetSelectedTimelineTime());
            double timelineNormalizedTime = Math.Min(1d, timelineTime / timelineDuration);

            double length = _videoBackgroundPlayer.length;
            bool hasKnownLength = !double.IsNaN(length) && !double.IsInfinity(length) && length > 0.0001d;
            double targetTime = hasKnownLength
                ? timelineNormalizedTime * Math.Max(0d, length - 0.001d)
                : timelineTime;

            if (hasKnownLength)
            {
                targetTime = Math.Min(targetTime, Math.Max(0d, length - 0.001d));
            }

            ulong frameCount = _videoBackgroundPlayer.frameCount;
            long targetFrame = -1L;
            if (frameCount > 0)
            {
                if (_videoBackgroundPlayer.frameRate > 0f)
                {
                    targetFrame = (long)Math.Round(targetTime * _videoBackgroundPlayer.frameRate);
                }
                else
                {
                    targetFrame = (long)Math.Round(timelineNormalizedTime * Math.Max(0L, (long)frameCount - 1L));
                }

                if (targetFrame < 0L)
                {
                    targetFrame = 0L;
                }
                else if ((ulong)targetFrame >= frameCount)
                {
                    targetFrame = (long)frameCount - 1L;
                }
            }

            bool unchangedFrame = targetFrame >= 0L && targetFrame == _lastVideoBackgroundFrame;
            bool unchangedTime = Math.Abs(targetTime - _lastVideoBackgroundTime) < 0.002d;
            if (!force && (unchangedFrame || (targetFrame < 0L && unchangedTime)))
            {
                return;
            }

            _lastVideoBackgroundFrame = targetFrame;
            _lastVideoBackgroundTime = targetTime;

            try
            {
                if (targetFrame >= 0L)
                {
                    _videoBackgroundPlayer.frame = targetFrame;
                }
                else if (_videoBackgroundPlayer.canSetTime)
                {
                    _videoBackgroundPlayer.time = targetTime;
                }

                if (_videoBackgroundPlayer.isPlaying)
                {
                    _videoBackgroundPlayer.Pause();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("MotionGestureEditor failed to seek background video frame: " + exception.Message);
            }
        }

        private void ShowVideoBackgroundFirstFrame()
        {
            if (_videoBackgroundPlayer == null || !_videoBackgroundPlayer.isPrepared)
            {
                return;
            }

            _lastVideoBackgroundTime = -1d;
            _lastVideoBackgroundFrame = -1L;

            try
            {
                if (_videoBackgroundPlayer.frameCount > 0)
                {
                    _videoBackgroundPlayer.frame = 0L;
                    _lastVideoBackgroundFrame = 0L;
                    _lastVideoBackgroundTime = 0d;
                    return;
                }

                if (_videoBackgroundPlayer.canSetTime)
                {
                    _videoBackgroundPlayer.time = 0d;
                    _lastVideoBackgroundTime = 0d;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("MotionGestureEditor failed to seek first video frame: " + exception.Message);
            }
        }

        private void OnVideoBackgroundPrepared(VideoPlayer source)
        {
            if (source == null || source != _videoBackgroundPlayer)
            {
                return;
            }

            EnsureVideoBackgroundRenderTexture();
            if (_videoBackgroundShowFirstFrameOnPrepare)
            {
                ShowVideoBackgroundFirstFrame();
                _videoBackgroundShowFirstFrameOnPrepare = false;
            }
            else
            {
                UpdateVideoBackgroundFrameFromTimeline(force: true);
            }
            source.Pause();
            Repaint();
        }

        private void OnVideoBackgroundFrameReady(VideoPlayer source, long frameIndex)
        {
            if (source == _videoBackgroundPlayer)
            {
                Repaint();
            }
        }

        private void OnVideoBackgroundError(VideoPlayer source, string message)
        {
            if (source != _videoBackgroundPlayer)
            {
                return;
            }

            Debug.LogWarning("MotionGestureEditor video background error: " + message);
            ReleaseVideoBackgroundPlayer();
        }

        private void ReleaseVideoBackgroundPlayer()
        {
            if (_videoBackgroundPlayer != null)
            {
                _videoBackgroundPlayer.prepareCompleted -= OnVideoBackgroundPrepared;
                _videoBackgroundPlayer.errorReceived -= OnVideoBackgroundError;
                _videoBackgroundPlayer.frameReady -= OnVideoBackgroundFrameReady;
                _videoBackgroundPlayer.Stop();
                _videoBackgroundPlayer.targetTexture = null;
                _videoBackgroundPlayer = null;
            }

            if (_videoBackgroundRenderTexture != null)
            {
                _videoBackgroundRenderTexture.Release();
                DestroyImmediate(_videoBackgroundRenderTexture);
                _videoBackgroundRenderTexture = null;
            }

            if (_videoBackgroundHost != null)
            {
                DestroyImmediate(_videoBackgroundHost);
                _videoBackgroundHost = null;
            }

            _videoBackgroundLoadedPath = string.Empty;
            _lastVideoBackgroundTime = -1d;
            _lastVideoBackgroundFrame = -1L;
            _videoBackgroundShowFirstFrameOnPrepare = false;
        }

        private void BuildDefaultPose()
        {
            Array.Clear(_defaultLandmarks, 0, _defaultLandmarks.Length);
            _defaultLandmarks[0] = new Vector2(0.500f, 0.880f);
            _defaultLandmarks[1] = new Vector2(0.492f, 0.892f);
            _defaultLandmarks[2] = new Vector2(0.478f, 0.892f);
            _defaultLandmarks[3] = new Vector2(0.462f, 0.886f);
            _defaultLandmarks[4] = new Vector2(0.508f, 0.892f);
            _defaultLandmarks[5] = new Vector2(0.522f, 0.892f);
            _defaultLandmarks[6] = new Vector2(0.538f, 0.886f);
            _defaultLandmarks[7] = new Vector2(0.445f, 0.880f);
            _defaultLandmarks[8] = new Vector2(0.555f, 0.880f);
            _defaultLandmarks[9] = new Vector2(0.486f, 0.864f);
            _defaultLandmarks[10] = new Vector2(0.514f, 0.864f);

            _defaultLandmarks[11] = new Vector2(0.420f, 0.750f);
            _defaultLandmarks[12] = new Vector2(0.580f, 0.750f);
            _defaultLandmarks[23] = new Vector2(0.450f, 0.520f);
            _defaultLandmarks[24] = new Vector2(0.550f, 0.520f);

            _defaultLandmarks[13] = new Vector2(0.345f, 0.620f);
            _defaultLandmarks[14] = new Vector2(0.655f, 0.620f);
            _defaultLandmarks[15] = new Vector2(0.305f, 0.480f);
            _defaultLandmarks[16] = new Vector2(0.695f, 0.480f);
            _defaultLandmarks[17] = new Vector2(0.286f, 0.460f);
            _defaultLandmarks[18] = new Vector2(0.714f, 0.460f);
            _defaultLandmarks[19] = new Vector2(0.295f, 0.448f);
            _defaultLandmarks[20] = new Vector2(0.705f, 0.448f);
            _defaultLandmarks[21] = new Vector2(0.304f, 0.468f);
            _defaultLandmarks[22] = new Vector2(0.696f, 0.468f);

            _defaultLandmarks[25] = new Vector2(0.442f, 0.320f);
            _defaultLandmarks[26] = new Vector2(0.558f, 0.320f);
            _defaultLandmarks[27] = new Vector2(0.432f, 0.130f);
            _defaultLandmarks[28] = new Vector2(0.568f, 0.130f);
            _defaultLandmarks[29] = new Vector2(0.420f, 0.104f);
            _defaultLandmarks[30] = new Vector2(0.580f, 0.104f);
            _defaultLandmarks[31] = new Vector2(0.452f, 0.094f);
            _defaultLandmarks[32] = new Vector2(0.548f, 0.094f);
        }
    }
}
#endif
