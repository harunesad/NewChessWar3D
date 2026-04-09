using UnityEngine;
using UnityEditor;
using BodylinkSDK;
using System;
using System.IO;

[CustomEditor(typeof(Bodylink))]
public class BodylinkEditor : Editor
{
    private static readonly string[] SupportedVideoExtensions = { ".mp4", ".mov", ".m4v", ".avi", ".mpeg", ".mpg", ".webm" };

    SerializedProperty initializeOnStart;
    SerializedProperty calibrateOnStart;
    SerializedProperty autoCalibrate;
    SerializedProperty resumeOnSceneChange;
    SerializedProperty calibrationWaitTime;
    SerializedProperty visibilityThreshold;
    SerializedProperty visibilityWaitingTime;
    SerializedProperty calibrationDistanceThreshold;
    SerializedProperty numberOfPlayers;
    SerializedProperty calibrationMode;
    SerializedProperty calibrationType;
    SerializedProperty showSkeleton;
    SerializedProperty matchPointLimit;
    SerializedProperty poseLandmarkerRunnerPrefab;
    SerializedProperty camera;
    SerializedProperty showCameraFeed;
    SerializedProperty inputStream;
    SerializedProperty selectedVideoAsset;
    SerializedProperty selectedVideoFile;
    SerializedProperty playVideoInLoop;

    void OnEnable()
    {
        initializeOnStart = serializedObject.FindProperty("initializeOnStart");
        calibrateOnStart = serializedObject.FindProperty("calibrateOnStart");
        autoCalibrate = serializedObject.FindProperty("_autoRecalibrate");
        resumeOnSceneChange = serializedObject.FindProperty("resumeOnSceneChange");
        calibrationWaitTime = serializedObject.FindProperty("calibrationWaitTime");
        visibilityThreshold = serializedObject.FindProperty("_visibilityThreshold");
        visibilityWaitingTime = serializedObject.FindProperty("_visibilityWaitingTime");
        calibrationDistanceThreshold = serializedObject.FindProperty("_calibrationDistanceThreshold");
        numberOfPlayers = serializedObject.FindProperty("_numberOfPlayers");
        calibrationMode = serializedObject.FindProperty("calibrationMode");
        calibrationType = serializedObject.FindProperty("_calibrationType");
        showSkeleton = serializedObject.FindProperty("showSkeleton");
        matchPointLimit = serializedObject.FindProperty("_matchPointLimit");
        poseLandmarkerRunnerPrefab = serializedObject.FindProperty("poseLandmarkerRunnerPrefab");
        camera = serializedObject.FindProperty("_cam");
        showCameraFeed = serializedObject.FindProperty("_showCameraFeed");
        inputStream = serializedObject.FindProperty("_inputStream");
        selectedVideoAsset = serializedObject.FindProperty("_selectedVideoAsset");
        selectedVideoFile = serializedObject.FindProperty("_selectedVideoFile");
        playVideoInLoop = serializedObject.FindProperty("_playVideoInLoop");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Bodylink bodylink = (Bodylink)target;
        bool inputSettingsChanged = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        // Show first toggle
        EditorGUILayout.PropertyField(initializeOnStart, new GUIContent("Initialize OnStart"));

        if (initializeOnStart.boolValue)
        {
            // Only show second toggle if first is ON
            EditorGUILayout.PropertyField(calibrateOnStart, new GUIContent("Calibrate OnStart"));
        }
        EditorGUILayout.PropertyField(autoCalibrate, new GUIContent("Auto Recalibrate"));

        EditorGUILayout.PropertyField(resumeOnSceneChange, new GUIContent("Resume OnSceneChange"));
        EditorGUILayout.PropertyField(showSkeleton, new GUIContent("Show Skeleton"));
        EditorGUILayout.PropertyField(showCameraFeed, new GUIContent("Show Camera Feed"));
        inputSettingsChanged = DrawInputSourceSettings();
        EditorGUILayout.EndVertical();

        EditorGUILayout.PropertyField(calibrationWaitTime, new GUIContent("Calibration WaitTime"));
        EditorGUILayout.PropertyField(visibilityThreshold, new GUIContent("VisibilityThreshold"));
        EditorGUILayout.PropertyField(visibilityWaitingTime, new GUIContent("Visibility WaitingTime"));
        EditorGUILayout.PropertyField(calibrationDistanceThreshold, new GUIContent("Calibration DistanceThreshold"));
        EditorGUILayout.PropertyField(numberOfPlayers, new GUIContent("NumberOfPlayers"));
        EditorGUILayout.PropertyField(calibrationMode, new GUIContent("Calibration Mode"));

        BodylinkCalibrationMode selectedMode = (BodylinkCalibrationMode)calibrationMode.enumValueIndex;
        if (selectedMode == BodylinkCalibrationMode.Target_Points_Match ||
            selectedMode == BodylinkCalibrationMode.Free_Points_Position)
        {
            EditorGUILayout.PropertyField(calibrationType, new GUIContent("Calibration Type"));

            // Clamp the slider to the tracked points available for the selected calibration type.
            var type = (BodylinkCalibrationType)calibrationType.enumValueIndex;
            int maxMatchPoints = GetMaxMatchPoints(type);
            matchPointLimit.intValue = EditorGUILayout.IntSlider(new GUIContent("Match Point Limit"), matchPointLimit.intValue, 1, maxMatchPoints);
        }

        EditorGUILayout.PropertyField(poseLandmarkerRunnerPrefab, new GUIContent("Pose Landmarker RunnerPrefab"));
        EditorGUILayout.PropertyField(camera, new GUIContent("Camera"));

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying && inputSettingsChanged)
        {
            bodylink.ApplyInputStreamSettings(true);
        }
    }

    private bool DrawInputSourceSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Input Source", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(inputStream, new GUIContent("Input Stream"));
        bool videoPathChanged = false;

        if ((BodylinkInputStreamType)inputStream.enumValueIndex == BodylinkInputStreamType.Video)
        {
            EditorGUILayout.PropertyField(playVideoInLoop, new GUIContent("Play Video In Loop"));
            selectedVideoAsset.objectReferenceValue = EditorGUILayout.ObjectField(
                new GUIContent("Video File"),
                selectedVideoAsset.objectReferenceValue,
                typeof(UnityEngine.Object),
                false);

            string assetPath = GetAssetPath(selectedVideoAsset.objectReferenceValue);
            if (!string.Equals(selectedVideoFile.stringValue, assetPath, StringComparison.Ordinal))
            {
                selectedVideoFile.stringValue = assetPath;
                videoPathChanged = true;
            }

            if (selectedVideoAsset.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Drag and drop a video file from the Project window.", MessageType.Info);
            }
            else if (!IsSupportedVideoAssetPath(assetPath))
            {
                EditorGUILayout.HelpBox("Unsupported file type. Use: .mp4, .mov, .m4v, .avi, .mpeg, .mpg, .webm", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Resolved Path", ResolveAbsolutePath(assetPath).Replace('\\', '/'));
            }
        }

        return EditorGUI.EndChangeCheck() || videoPathChanged;
    }

    private static string GetAssetPath(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

        string assetPath = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
    }

    private static bool IsSupportedVideoAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assetPath);
        return Array.Exists(
            SupportedVideoExtensions,
            supportedExtension => string.Equals(supportedExtension, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveAbsolutePath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(assetPath))
        {
            return Path.GetFullPath(assetPath);
        }

        string normalizedPath = assetPath.Replace('\\', '/');
        if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = normalizedPath.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
        }

        return assetPath;
    }

    private int GetMaxMatchPoints(BodylinkCalibrationType type)
    {
        switch (type)
        {
            case BodylinkCalibrationType.FullBody:
                return 5; // head + 4 extremities
            case BodylinkCalibrationType.UpperBody:
                return 3; // head + both hands
            case BodylinkCalibrationType.Feet:
                return 3; // head + both feet
            case BodylinkCalibrationType.Head:
            default:
                return 1; // head only
        }
    }
}
