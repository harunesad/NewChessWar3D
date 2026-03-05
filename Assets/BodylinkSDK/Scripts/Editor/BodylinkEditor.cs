using UnityEngine;
using UnityEditor;
using BodylinkSDK;
using System.Collections.Generic;

[CustomEditor(typeof(Bodylink))]
public class BodylinkEditor : Editor
{
    SerializedProperty initializeOnStart;
    SerializedProperty calibrateOnStart;
    SerializedProperty autoCalibrate;
    SerializedProperty resumeOnSceneChange;
    SerializedProperty calibrationWaitTime;
    SerializedProperty visibilityThreshold;
    SerializedProperty visibilityWaitingTime;
    SerializedProperty calibrationDistanceThreshold;
    SerializedProperty numberOfPlayers;
    SerializedProperty calibrationType;
    SerializedProperty showSkeleton;
    SerializedProperty matchPointLimit;
    SerializedProperty poseLandmarkerRunnerPrefab;
    SerializedProperty camera;
    SerializedProperty showCameraFeed;

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
        calibrationType = serializedObject.FindProperty("_calibrationType");
        showSkeleton = serializedObject.FindProperty("showSkeleton");
        matchPointLimit = serializedObject.FindProperty("_matchPointLimit");
        poseLandmarkerRunnerPrefab = serializedObject.FindProperty("poseLandmarkerRunnerPrefab");
        camera = serializedObject.FindProperty("_cam");
        showCameraFeed = serializedObject.FindProperty("_showCameraFeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

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
        EditorGUILayout.EndVertical();

        EditorGUILayout.PropertyField(calibrationWaitTime, new GUIContent("Calibration WaitTime"));
        EditorGUILayout.PropertyField(visibilityThreshold, new GUIContent("VisibilityThreshold"));
        EditorGUILayout.PropertyField(visibilityWaitingTime, new GUIContent("Visibility WaitingTime"));
        EditorGUILayout.PropertyField(calibrationDistanceThreshold, new GUIContent("Calibration DistanceThreshold"));
        EditorGUILayout.PropertyField(numberOfPlayers, new GUIContent("NumberOfPlayers"));
        EditorGUILayout.PropertyField(calibrationType, new GUIContent("CalibrationType"));

        // Draw match point limit with a dynamic max based on calibration type
        var type = (BodylinkCalibrationType)calibrationType.enumValueIndex;
        int maxMatchPoints = GetMaxMatchPoints(type);
        matchPointLimit.intValue = EditorGUILayout.IntSlider(new GUIContent("Match Point Limit"), matchPointLimit.intValue, 1, maxMatchPoints);

        EditorGUILayout.PropertyField(poseLandmarkerRunnerPrefab, new GUIContent("Pose Landmarker RunnerPrefab"));
        EditorGUILayout.PropertyField(camera, new GUIContent("Camera"));

        serializedObject.ApplyModifiedProperties();
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
