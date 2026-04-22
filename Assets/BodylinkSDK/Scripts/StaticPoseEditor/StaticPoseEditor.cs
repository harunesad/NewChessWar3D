#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEditor;
using UnityEngine;

namespace BodylinkSDK
{
    public class StaticPoseEditor : EditorWindow
    {
        private const int LandmarkCount = 33;
        private const float MinDistanceEpsilon = 0.0001f;
        private const float PickRadius = 14f;
        private const string PosesFolderRelativeToAssetsPath = "StaticPoses";
        private const string DefaultPoseLandmarkerModelName = "pose_landmarker_full.bytes";
        private const float PoseViewBaseWidth = 560f;
        private const float PoseViewBaseHeight = 560f;

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
        private class PoseJsonData
        {
            public TrackedPosePoint[] trackedLandmarks;
            public TrackedPoseAngle[] trackedAngles;
        }

        [Serializable]
        private class LegacyPoseJsonData
        {
            public PosePoint[] landmarks = new PosePoint[LandmarkCount];
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

        [Serializable]
        private struct PosePoint
        {
            public float x;
            public float y;

            public PosePoint(float xValue, float yValue)
            {
                x = xValue;
                y = yValue;
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
            0,   // nose
            11, 12, // shoulders
            13, 14, // elbows
            15, 16, // wrists
            23, 24, // hips
            25, 26, // knees
            27, 28  // ankles
        };

        private static readonly int[] CenterTrackedPointIndices = { 0 };
        private static readonly int[] LeftTrackedPointIndices = { 12, 14, 16, 24, 26, 28 };
        private static readonly int[] RightTrackedPointIndices = { 11, 13, 15, 23, 25, 27 };

        // Simplified bone connections based on visible landmarks only.
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
            new LimbChain(12, 14, 16), // left arm
            new LimbChain(11, 13, 15), // right arm
            new LimbChain(24, 26, 28), // left leg
            new LimbChain(23, 25, 27)  // right leg
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
        private readonly List<BoneConstraint> _constraints = new List<BoneConstraint>();
        private readonly Dictionary<ulong, float> _boneLengths = new Dictionary<ulong, float>();
        private readonly Dictionary<int, LimbChain> _chainByJoint = new Dictionary<int, LimbChain>();

        private const float SidebarWidth = 332f;
        private const float TrackedPointColumnSpacing = 6f;

        private static readonly GUIContent PoseFileNameFieldContent = new GUIContent(
            "Name",
            "Name used for the pose JSON file when saving to Assets/StaticPoses.");

        private static readonly GUIContent SaveFolderLabelContent = new GUIContent(
            "Save Folder",
            "Pose JSON files created by this editor are saved in this Assets subfolder.");

        private static readonly GUIContent SaveFolderPathContent = new GUIContent(
            "Assets/StaticPoses",
            "Pose JSON files created by this editor are saved in this Assets subfolder.");

        private static readonly GUIContent ResetPoseButtonContent = new GUIContent(
            "Reset Pose",
            "Restore the default landmark positions and reset tracked points to the default visible set.");

        private static readonly GUIContent UseCurrentAsBaselineButtonContent = new GUIContent(
            "Set Baseline",
            "Rebuild bone-length constraints from the current pose so future edits preserve these proportions.");

        private static readonly GUIContent SaveJsonButtonContent = new GUIContent(
            "Save JSON",
            "Save the current tracked landmarks and tracked angles as a pose JSON file.");

        private static readonly GUIContent LoadJsonButtonContent = new GUIContent(
            "Load JSON",
            "Load a saved pose JSON file and apply its tracked landmarks to the editor pose.");

        private static readonly GUIContent CopyJsonButtonContent = new GUIContent(
            "Copy JSON",
            "Copy the current pose JSON to the system clipboard without writing a file.");

        private static readonly GUIContent BodyImageFieldContent = new GUIContent(
            "Body Image",
            "Assign a project texture to use as the pose reference image behind the canvas.");

        private static readonly GUIContent LoadBodyImageButtonContent = new GUIContent(
            "Load File",
            "Load a body reference image from disk for the current editor session.");

        private static readonly GUIContent ShowImageToggleContent = new GUIContent(
            "Show Image",
            "Show or hide the reference image on the pose canvas.");

        private static readonly GUIContent FlipImageToggleContent = new GUIContent(
            "Flip X",
            "Mirror the reference image horizontally on the canvas.");

        private static readonly GUIContent BodyImageOpacitySliderContent = new GUIContent(
            "Image Opacity",
            "Adjust how transparent the reference image appears behind the landmarks.");

        private static readonly GUIContent ApplyBodyPoseButtonContent = new GUIContent(
            "Apply Body Pose From Image",
            "Run pose detection on the current image and apply the detected landmarks to the editor pose.");

        private static readonly GUIContent ShowLabelsToggleContent = new GUIContent(
            "Show Labels",
            "Display landmark names and indices next to the joints on the canvas.");

        private static readonly GUIContent LockTorsoToggleContent = new GUIContent(
            "Lock Torso",
            "Keep the shoulders and hips fixed while dragging arm or leg joints.");

        private static readonly GUIContent ClampToCanvasToggleContent = new GUIContent(
            "Clamp To 0-1",
            "Keep edited landmark positions inside the normalized pose area.");

        private static readonly GUIContent PointSizeSliderContent = new GUIContent(
            "Point Size",
            "Set the visual size of the joint handles on the canvas.");

        private static readonly GUIContent SolverIterationsSliderContent = new GUIContent(
            "Solver Iterations",
            "Control how many constraint-solving passes run while moving a joint.");

        private static readonly GUIContent TrackAllVisibleButtonContent = new GUIContent(
            "Track Visible",
            "Mark every visible landmark in this simplified pose as tracked.");

        private static readonly GUIContent ClearTrackedButtonContent = new GUIContent(
            "Clear Tracked",
            "Unmark all currently visible landmarks from the tracked pose set.");

        private static readonly GUIContent CenterTrackedPointsLabelContent = new GUIContent(
            "Center",
            "Tracked landmarks that sit on the body's center line.");

        private static readonly GUIContent LeftTrackedPointsLabelContent = new GUIContent(
            "Left",
            "Tracked landmarks on the body's left side.");

        private static readonly GUIContent RightTrackedPointsLabelContent = new GUIContent(
            "Right",
            "Tracked landmarks on the body's right side.");

        private Rect _canvasRect;
        private Rect _normalizedSpaceRect;
        private Rect _canvasHostRect;
        private bool _showLabels;
        private bool _lockTorso = true;
        private bool _clampToCanvas = true;
        private float _pointSize = 6.5f;
        private int _solverIterations = 18;
        private string _poseFileName = "static_pose";
        private bool _showTrackedPoints = true;
        private Texture2D _bodyReferenceImage;
        private Texture2D _runtimeLoadedBodyImage;
        private bool _showBodyReferenceImage = true;
        private bool _flipImportedImageX;
        private float _bodyImageOpacity = 0.55f;
        private Vector2 _rightPanelScroll;

        private int _selectedJoint = -1;
        private bool _isDragging;

        [MenuItem("Bodylink/Static Pose Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<StaticPoseEditor>("Bodylink Static Pose Editor");
            window.minSize = new Vector2(920f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsurePosesFolderExists();
            BuildDefaultPose();
            CopyPose(_defaultLandmarks, _landmarks);
            InitializeDefaultTrackedPoints();
            BuildChainMap();
            RebuildBoneConstraintsFromCurrentPose();
        }

        private void OnDisable()
        {
            ReleaseRuntimeLoadedImage();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    DrawCanvas();
                    HandleMouse(Event.current);
                    DrawSkeleton();
                }

                DrawRightPanel();
            }
        }

        private void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true)))
            {
                _rightPanelScroll = EditorGUILayout.BeginScrollView(_rightPanelScroll, false, false);
                DrawHeaderSection();
                DrawPoseFileSection();
                DrawPoseActionsSection();
                DrawImageInputSection();
                DrawEditorSettingsSection();
                DrawTrackedPointsSection();
                DrawSelectedJointSection();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawHeaderSection()
        {
            EditorGUILayout.LabelField("Mediapipe Static Pose Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Canvas is on the left. Use the right panel to manage image import, pose files, and tracked points.", MessageType.None);
        }

        private void DrawPoseFileSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Pose File", EditorStyles.boldLabel);
                _poseFileName = EditorGUILayout.TextField(PoseFileNameFieldContent, _poseFileName);
                EditorGUILayout.LabelField(SaveFolderLabelContent, SaveFolderPathContent, EditorStyles.miniLabel);
            }
        }

        private void DrawPoseActionsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Pose Actions", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(ResetPoseButtonContent, GUILayout.Height(22f)))
                    {
                        CopyPose(_defaultLandmarks, _landmarks);
                        InitializeDefaultTrackedPoints();
                        RebuildBoneConstraintsFromCurrentPose();
                        _selectedJoint = -1;
                    }

                    if (GUILayout.Button(UseCurrentAsBaselineButtonContent, GUILayout.Height(22f)))
                    {
                        RebuildBoneConstraintsFromCurrentPose();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(SaveJsonButtonContent, GUILayout.Height(22f)))
                    {
                        SavePoseToJson();
                    }

                    if (GUILayout.Button(LoadJsonButtonContent, GUILayout.Height(22f)))
                    {
                        LoadPoseFromJson();
                    }

                    if (GUILayout.Button(CopyJsonButtonContent, GUILayout.Height(22f)))
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
                EditorGUILayout.LabelField("Editor Settings", EditorStyles.boldLabel);
                _showLabels = EditorGUILayout.ToggleLeft(ShowLabelsToggleContent, _showLabels);
                _lockTorso = EditorGUILayout.ToggleLeft(LockTorsoToggleContent, _lockTorso);
                _clampToCanvas = EditorGUILayout.ToggleLeft(ClampToCanvasToggleContent, _clampToCanvas);
                _pointSize = EditorGUILayout.Slider(PointSizeSliderContent, _pointSize, 4f, 10f);
                _solverIterations = EditorGUILayout.IntSlider(SolverIterationsSliderContent, _solverIterations, 8, 40);
            }
        }

        private void DrawSelectedJointSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_selectedJoint >= 0 && _selectedJoint < LandmarkCount)
                {
                    EditorGUILayout.LabelField("Selected Joint", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(LandmarkNames[_selectedJoint] + " (" + _selectedJoint + ")");
                    EditorGUILayout.LabelField("x", _landmarks[_selectedJoint].x.ToString("0.000"));
                    EditorGUILayout.LabelField("y", _landmarks[_selectedJoint].y.ToString("0.000"));
                }
                else
                {
                    EditorGUILayout.LabelField("Selected Joint", "None");
                }
            }
        }

        private void DrawTrackedPointsSection()
        {
            int trackedCount = GetTrackedPointCount();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _showTrackedPoints = EditorGUILayout.Foldout(_showTrackedPoints, GetTrackedPointsFoldoutContent(trackedCount), true);
                if (!_showTrackedPoints)
                {
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(TrackAllVisibleButtonContent, GUILayout.Height(20f)))
                    {
                        SetTrackedPointsForVisibleLandmarks(true);
                    }

                    if (GUILayout.Button(ClearTrackedButtonContent, GUILayout.Height(20f)))
                    {
                        SetTrackedPointsForVisibleLandmarks(false);
                    }
                }

                if (CenterTrackedPointIndices.Length > 0)
                {
                    EditorGUILayout.Space(4f);
                    DrawTrackedPointGroup(CenterTrackedPointsLabelContent, CenterTrackedPointIndices);
                }

                EditorGUILayout.Space(4f);
                DrawTrackedPointColumns();

                if (trackedCount < 2)
                {
                    EditorGUILayout.HelpBox("Track at least 2 points for reliable pose detection.", MessageType.Warning);
                }
            }
        }

        private void DrawTrackedPointColumns()
        {
            float height = GetTrackedPointColumnHeight(Mathf.Max(LeftTrackedPointIndices.Length, RightTrackedPointIndices.Length));
            Rect rowRect = EditorGUILayout.GetControlRect(false, height);
            float columnWidth = (rowRect.width - TrackedPointColumnSpacing) * 0.5f;

            Rect leftRect = new Rect(rowRect.x, rowRect.y, columnWidth, height);
            Rect rightRect = new Rect(leftRect.xMax + TrackedPointColumnSpacing, rowRect.y, columnWidth, height);

            DrawTrackedPointColumn(leftRect, LeftTrackedPointsLabelContent, LeftTrackedPointIndices);
            DrawTrackedPointColumn(rightRect, RightTrackedPointsLabelContent, RightTrackedPointIndices);
        }

        private void DrawTrackedPointColumn(Rect rect, GUIContent label, int[] indices)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            Rect innerRect = new Rect(
                rect.x + EditorStyles.helpBox.padding.left,
                rect.y + EditorStyles.helpBox.padding.top,
                rect.width - EditorStyles.helpBox.padding.horizontal,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(innerRect, label, EditorStyles.miniBoldLabel);

            float y = innerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                Rect toggleRect = new Rect(innerRect.x, y, innerRect.width, EditorGUIUtility.singleLineHeight);
                _trackedPoints[index] = EditorGUI.ToggleLeft(toggleRect, GetTrackedPointToggleContent(index), _trackedPoints[index]);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private void DrawTrackedPointGroup(GUIContent label, int[] indices)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                _trackedPoints[index] = EditorGUILayout.ToggleLeft(GetTrackedPointToggleContent(index), _trackedPoints[index]);
            }
        }

        private static GUIContent GetTrackedPointsFoldoutContent(int trackedCount)
        {
            return new GUIContent(
                "Tracked Points (" + trackedCount + ")",
                "Choose which landmarks are saved with this pose and used to build tracked pose angles.");
        }

        private static float GetTrackedPointColumnHeight(int rowCount)
        {
            int totalLines = rowCount + 1;
            return EditorStyles.helpBox.padding.vertical +
                (EditorGUIUtility.singleLineHeight * totalLines) +
                (EditorGUIUtility.standardVerticalSpacing * Mathf.Max(0, totalLines - 1));
        }

        private static GUIContent GetTrackedPointToggleContent(int index)
        {
            string landmarkName = ObjectNames.NicifyVariableName(LandmarkNames[index]);
            string displayName = GetTrackedPointDisplayName(landmarkName);
            return new GUIContent(
                index + ": " + displayName,
                "Include the " + landmarkName + " landmark when saving and matching this pose.");
        }

        private static string GetTrackedPointDisplayName(string landmarkName)
        {
            const string LeftPrefix = "Left ";
            const string RightPrefix = "Right ";

            if (landmarkName.StartsWith(LeftPrefix, StringComparison.Ordinal))
            {
                return landmarkName.Substring(LeftPrefix.Length);
            }

            if (landmarkName.StartsWith(RightPrefix, StringComparison.Ordinal))
            {
                return landmarkName.Substring(RightPrefix.Length);
            }

            return landmarkName;
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
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Body Image Input", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Load a full-body image and apply detected landmarks to update points.", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D nextImage = (Texture2D)EditorGUILayout.ObjectField(BodyImageFieldContent, _bodyReferenceImage, typeof(Texture2D), false);
                    if (nextImage != _bodyReferenceImage)
                    {
                        if (_runtimeLoadedBodyImage != null && nextImage != _runtimeLoadedBodyImage)
                        {
                            ReleaseRuntimeLoadedImage();
                        }

                        _bodyReferenceImage = nextImage;
                    }

                    if (GUILayout.Button(LoadBodyImageButtonContent, GUILayout.Width(85f)))
                    {
                        LoadBodyImageFromDisk();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _showBodyReferenceImage = EditorGUILayout.ToggleLeft(ShowImageToggleContent, _showBodyReferenceImage, GUILayout.Width(95f));
                    _flipImportedImageX = EditorGUILayout.ToggleLeft(FlipImageToggleContent, _flipImportedImageX, GUILayout.Width(62f));
                }

                _bodyImageOpacity = EditorGUILayout.Slider(BodyImageOpacitySliderContent, _bodyImageOpacity, 0f, 1f);

                if (GUILayout.Button(ApplyBodyPoseButtonContent, GUILayout.Height(22f)))
                {
                    ApplyBodyPoseFromImage();
                }
            }
        }

        private void DrawBodyReferenceImage()
        {
            if (!_showBodyReferenceImage || _bodyReferenceImage == null)
            {
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_bodyImageOpacity));
            GUI.DrawTexture(_normalizedSpaceRect, _bodyReferenceImage, ScaleMode.StretchToFill, true);
            GUI.color = previousColor;
        }

        private Rect ResolveNormalizedSpaceRect()
        {
            if (_bodyReferenceImage == null || _bodyReferenceImage.width <= 0 || _bodyReferenceImage.height <= 0)
            {
                return _canvasRect;
            }

            float imageAspect = _bodyReferenceImage.width / (float)_bodyReferenceImage.height;
            float canvasAspect = _canvasRect.width / Mathf.Max(_canvasRect.height, 0.001f);

            if (canvasAspect > imageAspect)
            {
                float width = _canvasRect.height * imageAspect;
                float x = _canvasRect.x + (_canvasRect.width - width) * 0.5f;
                return new Rect(x, _canvasRect.y, width, _canvasRect.height);
            }

            float height = _canvasRect.width / imageAspect;
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
            if (evt == null)
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

            // Draw bones
            Handles.color = new Color(0.28f, 0.79f, 0.98f, 0.95f);
            int pairCount = BonePairs.GetLength(0);
            for (int i = 0; i < pairCount; i++)
            {
                int a = BonePairs[i, 0];
                int b = BonePairs[i, 1];
                Handles.DrawAAPolyLine(2.0f, NormalizedToCanvas(_landmarks[a]), NormalizedToCanvas(_landmarks[b]));
            }

            // Draw points
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
            ConstrainAngle(12, 14, 16, 10f, 175f, pinned); // left elbow
            ConstrainAngle(11, 13, 15, 10f, 175f, pinned); // right elbow
            ConstrainAngle(24, 26, 28, 10f, 175f, pinned); // left knee
            ConstrainAngle(23, 25, 27, 10f, 175f, pinned); // right knee
            ConstrainAngle(24, 12, 14, 8f, 178f, pinned);  // left shoulder
            ConstrainAngle(23, 11, 13, 8f, 178f, pinned);  // right shoulder
            ConstrainAngle(12, 24, 26, 8f, 178f, pinned);  // left hip
            ConstrainAngle(11, 23, 25, 8f, 178f, pinned);  // right hip
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

            // No intersection (far apart): use the closest feasible point from A toward B.
            if (distance > radiusA + radiusB)
            {
                return centerA + direction * radiusA;
            }

            // One circle inside another: project on A in the direction away/toward B.
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

        private void ApplyBodyPoseFromImage()
        {
            if (!TryExtractPoseFromImage(_bodyReferenceImage, out Vector2[] detectedLandmarks, out string errorMessage))
            {
                EditorUtility.DisplayDialog("Pose Extraction Failed", errorMessage, "OK");
                return;
            }

            for (int i = 0; i < LandmarkCount; i++)
            {
                _landmarks[i] = detectedLandmarks[i];
            }

            if (_clampToCanvas)
            {
                for (int i = 0; i < LandmarkCount; i++)
                {
                    _landmarks[i] = Clamp01(_landmarks[i]);
                }
            }

            InitializeDefaultTrackedPoints();
            RebuildBoneConstraintsFromCurrentPose();
            _selectedJoint = -1;
            Repaint();
        }

        private bool TryExtractPoseFromImage(Texture2D sourceImage, out Vector2[] detectedLandmarks, out string errorMessage)
        {
            detectedLandmarks = null;
            errorMessage = string.Empty;

            if (sourceImage == null)
            {
                errorMessage = "Assign a human body image first.";
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
                using (var image = new Mediapipe.Image(readableTexture))
                {
                    PoseLandmarkerResult result = default;
                    bool poseDetected = poseLandmarker.TryDetect(image, new ImageProcessingOptions(rotationDegrees: 0), ref result);
                    if (!poseDetected || result.poseLandmarks == null || result.poseLandmarks.Count == 0)
                    {
                        errorMessage = "No body pose detected. Use a clear, full-body image.";
                        return false;
                    }

                    var normalizedLandmarks = result.poseLandmarks[0].landmarks;
                    if (normalizedLandmarks == null || normalizedLandmarks.Count < LandmarkCount)
                    {
                        errorMessage = "Pose detection returned incomplete landmark data.";
                        return false;
                    }

                    detectedLandmarks = new Vector2[LandmarkCount];
                    for (int i = 0; i < LandmarkCount; i++)
                    {
                        float x = normalizedLandmarks[i].x;
                        float y = normalizedLandmarks[i].y;

                        if (_flipImportedImageX)
                        {
                            x = 1f - x;
                        }

                        detectedLandmarks[i] = new Vector2(x, y);
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

        private void SavePoseToJson()
        {
            int trackedCount = GetTrackedPointCount();
            if (trackedCount < 2)
            {
                EditorUtility.DisplayDialog("Not Enough Tracked Points", "Mark at least 2 points before saving pose JSON.", "OK");
                return;
            }

            EnsurePosesFolderExists();
            string fileName = SanitizeFileName(_poseFileName);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "static_pose";
            }
            _poseFileName = fileName;

            string absolutePath = Path.Combine(GetPosesFolderAbsolutePath(), fileName + ".json");
            if (File.Exists(absolutePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Overwrite Pose File",
                    "A pose file named '" + fileName + ".json' already exists in Assets/StaticPoses.\nDo you want to overwrite it?",
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
        }

        private void LoadPoseFromJson()
        {
            EnsurePosesFolderExists();
            string posesFolderPath = GetPosesFolderAbsolutePath();
            string path = EditorUtility.OpenFilePanel("Load Pose JSON", posesFolderPath, "json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            if (!IsPathInsideDirectory(path, posesFolderPath))
            {
                EditorUtility.DisplayDialog("Invalid Pose File", "Select pose JSON from Assets/StaticPoses folder only.", "OK");
                return;
            }

            string json = File.ReadAllText(path);
            if (!TryApplyPoseJson(json))
            {
                EditorUtility.DisplayDialog("Invalid Pose JSON", "Expected JSON with trackedLandmarks (and optional trackedAngles) or 33 landmarks (legacy format).", "OK");
                return;
            }

            _poseFileName = Path.GetFileNameWithoutExtension(path);
            RebuildBoneConstraintsFromCurrentPose();
        }

        private bool TryApplyPoseJson(string json)
        {
            PoseJsonData data = JsonUtility.FromJson<PoseJsonData>(json);
            if (TryApplyTrackedPoseData(data))
            {
                return true;
            }

            LegacyPoseJsonData legacyData = JsonUtility.FromJson<LegacyPoseJsonData>(json);
            if (legacyData != null && legacyData.landmarks != null && legacyData.landmarks.Length == LandmarkCount)
            {
                for (int i = 0; i < LandmarkCount; i++)
                {
                    _landmarks[i] = new Vector2(legacyData.landmarks[i].x, legacyData.landmarks[i].y);
                }

                if (_clampToCanvas)
                {
                    for (int i = 0; i < LandmarkCount; i++)
                    {
                        _landmarks[i] = Clamp01(_landmarks[i]);
                    }
                }

                InitializeDefaultTrackedPoints();
                return true;
            }

            return false;
        }

        private bool TryApplyTrackedPoseData(PoseJsonData data)
        {
            if (data == null || data.trackedLandmarks == null || data.trackedLandmarks.Length == 0)
            {
                return false;
            }

            CopyPose(_defaultLandmarks, _landmarks);
            Array.Clear(_trackedPoints, 0, _trackedPoints.Length);

            int appliedCount = 0;
            for (int i = 0; i < data.trackedLandmarks.Length; i++)
            {
                TrackedPosePoint point = data.trackedLandmarks[i];
                if (point.index < 0 || point.index >= LandmarkCount)
                {
                    continue;
                }

                _landmarks[point.index] = new Vector2(point.x, point.y);
                _trackedPoints[point.index] = true;
                appliedCount++;
            }

            if (appliedCount < 2)
            {
                return false;
            }

            if (_clampToCanvas)
            {
                for (int i = 0; i < LandmarkCount; i++)
                {
                    _landmarks[i] = Clamp01(_landmarks[i]);
                }
            }

            return true;
        }

        private static string GetPosesFolderAbsolutePath()
        {
            return Path.Combine(Application.dataPath, PosesFolderRelativeToAssetsPath);
        }

        private static void EnsurePosesFolderExists()
        {
            string folderPath = GetPosesFolderAbsolutePath();
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
            PoseJsonData data = new PoseJsonData();
            List<TrackedPosePoint> trackedLandmarks = new List<TrackedPosePoint>();
            List<TrackedPoseAngle> trackedAngles = new List<TrackedPoseAngle>();

            for (int i = 0; i < LandmarkCount; i++)
            {
                if (_trackedPoints[i])
                {
                    trackedLandmarks.Add(new TrackedPosePoint(i, _landmarks[i].x, _landmarks[i].y));
                }
            }

            for (int i = 0; i < TrackedAngleDefinitions.Length; i++)
            {
                PoseAngleDefinition definition = TrackedAngleDefinitions[i];
                if (!_trackedPoints[definition.A] || !_trackedPoints[definition.B] || !_trackedPoints[definition.C])
                {
                    continue;
                }

                if (!TryCalculateJointAngle(_landmarks, definition.A, definition.B, definition.C, out float angle))
                {
                    continue;
                }

                trackedAngles.Add(new TrackedPoseAngle(definition.Name, definition.A, definition.B, definition.C, angle));
            }

            data.trackedLandmarks = trackedLandmarks.ToArray();
            data.trackedAngles = trackedAngles.ToArray();
            return JsonUtility.ToJson(data, pretty);
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

        private void BuildDefaultPose()
        {
            // Head
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

            // Torso
            _defaultLandmarks[11] = new Vector2(0.420f, 0.750f);
            _defaultLandmarks[12] = new Vector2(0.580f, 0.750f);
            _defaultLandmarks[23] = new Vector2(0.450f, 0.520f);
            _defaultLandmarks[24] = new Vector2(0.550f, 0.520f);

            // Arms
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

            // Legs
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
