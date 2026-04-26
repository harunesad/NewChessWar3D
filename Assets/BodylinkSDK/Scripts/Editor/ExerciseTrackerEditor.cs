using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExerciseTracker))]
public class ExerciseTrackerEditor : Editor
{
    private SerializedProperty exerciseProp;
    private SerializedProperty playerSlotProp;
    private SerializedProperty useSmoothedPointsProp;
    private SerializedProperty minimumVisibilityProp;
    private SerializedProperty progressLerpSpeedProp;
    private SerializedProperty evaluateEveryFrameProp;
    private SerializedProperty debugLogsProp;

    private SerializedProperty squatRuntimeSettingsProp;
    private SerializedProperty pushUpRuntimeSettingsProp;
    private SerializedProperty jumpingJackRuntimeSettingsProp;
    private SerializedProperty lungeRuntimeSettingsProp;
    private SerializedProperty plankRuntimeSettingsProp;
    private SerializedProperty highKneesRuntimeSettingsProp;

    private void OnEnable()
    {
        exerciseProp = serializedObject.FindProperty("exercise");
        playerSlotProp = serializedObject.FindProperty("playerSlot");
        useSmoothedPointsProp = serializedObject.FindProperty("useSmoothedPoints");
        minimumVisibilityProp = serializedObject.FindProperty("minimumVisibility");
        progressLerpSpeedProp = serializedObject.FindProperty("progressLerpSpeed");
        evaluateEveryFrameProp = serializedObject.FindProperty("evaluateEveryFrame");
        debugLogsProp = serializedObject.FindProperty("debugLogs");

        squatRuntimeSettingsProp = serializedObject.FindProperty("squatRuntimeSettings");
        pushUpRuntimeSettingsProp = serializedObject.FindProperty("pushUpRuntimeSettings");
        jumpingJackRuntimeSettingsProp = serializedObject.FindProperty("jumpingJackRuntimeSettings");
        lungeRuntimeSettingsProp = serializedObject.FindProperty("lungeRuntimeSettings");
        plankRuntimeSettingsProp = serializedObject.FindProperty("plankRuntimeSettings");
        highKneesRuntimeSettingsProp = serializedObject.FindProperty("highKneesRuntimeSettings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawExerciseSection();
        EditorGUILayout.Space();
        DrawTrackingSection();
        EditorGUILayout.Space();
        DrawSelectedExerciseSettings();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawExerciseSection()
    {
        EditorGUILayout.LabelField("Exercise", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(exerciseProp);
            EditorGUILayout.PropertyField(playerSlotProp);
            EditorGUILayout.PropertyField(useSmoothedPointsProp);
            EditorGUILayout.PropertyField(minimumVisibilityProp);
        }
    }

    private void DrawTrackingSection()
    {
        EditorGUILayout.LabelField("Tracking", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(progressLerpSpeedProp);
            EditorGUILayout.PropertyField(evaluateEveryFrameProp);
            EditorGUILayout.PropertyField(debugLogsProp);
        }
    }

    private void DrawSelectedExerciseSettings()
    {
        SerializedProperty selectedSettings = GetSelectedSettingsProperty();
        string label = GetSelectedSettingsLabel();

        if (selectedSettings == null)
        {
            return;
        }

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox("These are the developer-facing settings for the selected exercise.", MessageType.Info);
            DrawRuntimeSettings(selectedSettings);
        }
    }

    private SerializedProperty GetSelectedSettingsProperty()
    {
        ExerciseTracker.ExerciseType selectedExercise = (ExerciseTracker.ExerciseType)exerciseProp.enumValueIndex;
        switch (selectedExercise)
        {
            case ExerciseTracker.ExerciseType.Squat:
                return squatRuntimeSettingsProp;
            case ExerciseTracker.ExerciseType.PushUp:
                return pushUpRuntimeSettingsProp;
            case ExerciseTracker.ExerciseType.JumpingJack:
                return jumpingJackRuntimeSettingsProp;
            case ExerciseTracker.ExerciseType.Lunges:
                return lungeRuntimeSettingsProp;
            case ExerciseTracker.ExerciseType.Plank:
                return plankRuntimeSettingsProp;
            case ExerciseTracker.ExerciseType.HighKnees:
                return highKneesRuntimeSettingsProp;
            default:
                return null;
        }
    }

    private string GetSelectedSettingsLabel()
    {
        ExerciseTracker.ExerciseType selectedExercise = (ExerciseTracker.ExerciseType)exerciseProp.enumValueIndex;
        return selectedExercise + " Settings";
    }

    private void DrawRuntimeSettings(SerializedProperty runtimeSettingsProperty)
    {
        EditorGUILayout.PropertyField(runtimeSettingsProperty.FindPropertyRelative("TargetRepetitions"));
        EditorGUILayout.PropertyField(runtimeSettingsProperty.FindPropertyRelative("RepsPerSet"));
        EditorGUILayout.PropertyField(runtimeSettingsProperty.FindPropertyRelative("MediumThreshold"));
        EditorGUILayout.PropertyField(runtimeSettingsProperty.FindPropertyRelative("HighThreshold"));

        ExerciseTracker.ExerciseType selectedExercise = (ExerciseTracker.ExerciseType)exerciseProp.enumValueIndex;
        if (selectedExercise == ExerciseTracker.ExerciseType.Plank)
        {
            EditorGUILayout.PropertyField(runtimeSettingsProperty.FindPropertyRelative("HoldDuration"));
        }
    }
}
