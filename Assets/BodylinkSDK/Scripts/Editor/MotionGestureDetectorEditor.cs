#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using BodylinkSDK;

[CustomEditor(typeof(MotionGestureDetector))]
public class MotionGestureDetectorEditor : Editor
{
    private SerializedProperty motionFilesProperty;
    private SerializedProperty playerSlotProperty;
    private SerializedProperty angleToleranceProperty;
    private SerializedProperty detectionThresholdProperty;
    private SerializedProperty includeMirroredComparisonProperty;
    private SerializedProperty prioritizeCorePointsProperty;
    private SerializedProperty minTimeScaleProperty;
    private SerializedProperty maxTimeScaleProperty;
    private SerializedProperty timeScaleStepProperty;
    private SerializedProperty samplingRateProperty;
    private SerializedProperty triggerCooldownProperty;
    private SerializedProperty debugEnabledProperty;
    private SerializedProperty detectedMotionNameProperty;
    private SerializedProperty detectedMotionConfidenceProperty;
    private SerializedProperty motionScoresProperty;

    private void OnEnable()
    {
        motionFilesProperty = serializedObject.FindProperty("motionFiles");
        playerSlotProperty = serializedObject.FindProperty("playerSlot");
        angleToleranceProperty = serializedObject.FindProperty("angleToleranceDegrees");
        detectionThresholdProperty = serializedObject.FindProperty("detectionThreshold");
        includeMirroredComparisonProperty = serializedObject.FindProperty("includeMirroredComparison");
        prioritizeCorePointsProperty = serializedObject.FindProperty("prioritizeCorePoints");
        minTimeScaleProperty = serializedObject.FindProperty("minTimeScale");
        maxTimeScaleProperty = serializedObject.FindProperty("maxTimeScale");
        timeScaleStepProperty = serializedObject.FindProperty("timeScaleStep");
        samplingRateProperty = serializedObject.FindProperty("samplingRate");
        triggerCooldownProperty = serializedObject.FindProperty("triggerCooldown");
        debugEnabledProperty = serializedObject.FindProperty("debugEnabled");
        detectedMotionNameProperty = serializedObject.FindProperty("detectedMotionName");
        detectedMotionConfidenceProperty = serializedObject.FindProperty("detectedMotionConfidence");
        motionScoresProperty = serializedObject.FindProperty("motionScores");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawMotionFilesSection();
        EditorGUILayout.Space(8f);
        DrawDetectionSettings();
        EditorGUILayout.Space(8f);
        DrawRealtimeOutput();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDetectionSettings()
    {
        EditorGUILayout.LabelField("Detection Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Detector evaluates both supported players every frame. Player Slot only selects which player's realtime output is shown here and returned by the legacy single-player API. Motion score rows stay in Motion Files order.", MessageType.None);
        DrawPropertyWithTooltip(playerSlotProperty);
        DrawPropertyWithTooltip(angleToleranceProperty, "Angle Tolerance (Degrees)");
        DrawPropertyWithTooltip(detectionThresholdProperty);
        DrawPropertyWithTooltip(includeMirroredComparisonProperty);
        DrawPropertyWithTooltip(prioritizeCorePointsProperty);
        DrawPropertyWithTooltip(minTimeScaleProperty);
        DrawPropertyWithTooltip(maxTimeScaleProperty);
        DrawPropertyWithTooltip(timeScaleStepProperty);
        DrawPropertyWithTooltip(samplingRateProperty);
        DrawPropertyWithTooltip(triggerCooldownProperty);
        DrawPropertyWithTooltip(debugEnabledProperty);
    }

    private void DrawRealtimeOutput()
    {
        EditorGUILayout.LabelField("Realtime Output", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            DrawPropertyWithTooltip(detectedMotionNameProperty);
            DrawPropertyWithTooltip(detectedMotionConfidenceProperty);
            DrawPropertyWithTooltip(motionScoresProperty, includeChildren: true);
        }
    }

    private void DrawMotionFilesSection()
    {
        EditorGUILayout.LabelField("Motion Files", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag JSON TextAssets from Assets/MotionGestures into the list.", MessageType.Info);
        DrawPropertyWithTooltip(motionFilesProperty, includeChildren: true);

        Rect dropArea = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drop Motion JSON Files Here");

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
                AddDraggedMotionFiles();
            }

            currentEvent.Use();
        }

        DrawAddAllButton();
    }

    private void DrawAddAllButton()
    {
        if (!GUILayout.Button("Add All JSON Motions From Default Folder"))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(MotionGestureDetector.MotionsFolderAssetPath))
        {
            Debug.LogWarning("[MotionGestureDetectorEditor] Default motions folder not found: " + MotionGestureDetector.MotionsFolderAssetPath);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { MotionGestureDetector.MotionsFolderAssetPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            TryAddMotionFile(textAsset);
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

    private void AddDraggedMotionFiles()
    {
        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            TextAsset textAsset = DragAndDrop.objectReferences[i] as TextAsset;
            TryAddMotionFile(textAsset);
        }
    }

    private void TryAddMotionFile(TextAsset textAsset)
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

        int index = motionFilesProperty.arraySize;
        motionFilesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty element = motionFilesProperty.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("motionFile").objectReferenceValue = textAsset;
        element.FindPropertyRelative("motionName").stringValue = Path.GetFileNameWithoutExtension(assetPath);
    }

    private bool IsAlreadyAdded(TextAsset textAsset)
    {
        for (int i = 0; i < motionFilesProperty.arraySize; i++)
        {
            SerializedProperty element = motionFilesProperty.GetArrayElementAtIndex(i);
            UnityEngine.Object existing = element.FindPropertyRelative("motionFile").objectReferenceValue;
            if (existing == textAsset)
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawPropertyWithTooltip(SerializedProperty property, string labelOverride = null, bool includeChildren = false)
    {
        GUIContent label = string.IsNullOrEmpty(labelOverride)
            ? new GUIContent(property.displayName, property.tooltip)
            : new GUIContent(labelOverride, property.tooltip);
        EditorGUILayout.PropertyField(property, label, includeChildren);
    }
}
#endif
