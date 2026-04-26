#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StaticPoseAnalyzer))]
public class StaticPoseAnalyzerEditor : Editor
{
    private SerializedProperty poseFilesProperty;
    private SerializedProperty playerSlotProperty;
    private SerializedProperty angleToleranceProperty;
    private SerializedProperty detectionThresholdProperty;
    private SerializedProperty includeMirroredComparisonProperty;
    private SerializedProperty debugEnabledProperty;
    private SerializedProperty detectedPoseNameProperty;
    private SerializedProperty detectedPoseConfidenceProperty;
    private SerializedProperty poseScoresProperty;
    private SerializedProperty playerRealtimeOutputsProperty;

    private void OnEnable()
    {
        poseFilesProperty = serializedObject.FindProperty("poseFiles");
        playerSlotProperty = serializedObject.FindProperty("playerSlot");
        angleToleranceProperty = serializedObject.FindProperty("angleToleranceDegrees");
        detectionThresholdProperty = serializedObject.FindProperty("detectionThreshold");
        includeMirroredComparisonProperty = serializedObject.FindProperty("includeMirroredComparison");
        debugEnabledProperty = serializedObject.FindProperty("debugEnabled");
        detectedPoseNameProperty = serializedObject.FindProperty("detectedPoseName");
        detectedPoseConfidenceProperty = serializedObject.FindProperty("detectedPoseConfidence");
        poseScoresProperty = serializedObject.FindProperty("poseScores");
        playerRealtimeOutputsProperty = serializedObject.FindProperty("playerRealtimeOutputs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPoseFilesSection();
        EditorGUILayout.Space(8f);
        DrawDetectionSettings();
        EditorGUILayout.Space(8f);
        DrawRealtimeOutput();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDetectionSettings()
    {
        EditorGUILayout.LabelField("Detection Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Analyzer scores pose JSON files using trackedAngles and trackedLandmarks (when available). Recommended starting values: Angle Tolerance 30-45, Detection Threshold 0.35-0.55.", MessageType.None);
        EditorGUILayout.PropertyField(playerSlotProperty);
        EditorGUILayout.PropertyField(angleToleranceProperty, new GUIContent("Angle Tolerance (Degrees)"));
        EditorGUILayout.PropertyField(detectionThresholdProperty);
        EditorGUILayout.PropertyField(includeMirroredComparisonProperty);
        EditorGUILayout.PropertyField(debugEnabledProperty);
    }

    private void DrawRealtimeOutput()
    {
        EditorGUILayout.LabelField("Realtime Output", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(detectedPoseNameProperty);
            EditorGUILayout.PropertyField(detectedPoseConfidenceProperty);
            EditorGUILayout.PropertyField(poseScoresProperty, true);
            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(playerRealtimeOutputsProperty, true);
        }
    }

    private void DrawPoseFilesSection()
    {
        EditorGUILayout.LabelField("Pose Files", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag JSON TextAssets from Assets/StaticPoses into the list.", MessageType.Info);
        EditorGUILayout.PropertyField(poseFilesProperty, true);

        Rect dropArea = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drop Pose JSON Files Here");

        Event currentEvent = Event.current;
        if (!dropArea.Contains(currentEvent.mousePosition))
        {
            DrawAddAllButton();
            return;
        }

        if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
        {
            bool hasValidJson = HasValidDraggedJson();
            DragAndDrop.visualMode = hasValidJson ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (currentEvent.type == EventType.DragPerform && hasValidJson)
            {
                DragAndDrop.AcceptDrag();
                AddDraggedPoseFiles();
            }

            currentEvent.Use();
        }

        DrawAddAllButton();
    }

    private void DrawAddAllButton()
    {
        if (!GUILayout.Button("Add All JSON Poses From Default Folder"))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(StaticPoseAnalyzer.PosesFolderAssetPath))
        {
            Debug.LogWarning("[StaticPoseAnalyzerEditor] Default poses folder not found: " + StaticPoseAnalyzer.PosesFolderAssetPath);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { StaticPoseAnalyzer.PosesFolderAssetPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            TryAddPoseFile(textAsset);
        }
    }

    private bool HasValidDraggedJson()
    {
        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            TextAsset textAsset = DragAndDrop.objectReferences[i] as TextAsset;
            if (textAsset == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(textAsset);
            if (assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void AddDraggedPoseFiles()
    {
        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            TextAsset textAsset = DragAndDrop.objectReferences[i] as TextAsset;
            TryAddPoseFile(textAsset);
        }
    }

    private void TryAddPoseFile(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(textAsset);
        if (!assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (IsAlreadyAdded(textAsset))
        {
            return;
        }

        int index = poseFilesProperty.arraySize;
        poseFilesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty element = poseFilesProperty.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("poseFile").objectReferenceValue = textAsset;
        element.FindPropertyRelative("poseName").stringValue = Path.GetFileNameWithoutExtension(assetPath);
    }

    private bool IsAlreadyAdded(TextAsset textAsset)
    {
        for (int i = 0; i < poseFilesProperty.arraySize; i++)
        {
            SerializedProperty element = poseFilesProperty.GetArrayElementAtIndex(i);
            UnityEngine.Object existing = element.FindPropertyRelative("poseFile").objectReferenceValue;
            if (existing == textAsset)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
