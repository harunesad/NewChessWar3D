using BodylinkSDK;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WebCamVideoRecording))]
public class WebCamVideoRecordingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var recorder = (WebCamVideoRecording)target;
        bool isPlaying = Application.isPlaying;
        bool isRecording = isPlaying && recorder.IsRecording;

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Recording Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!isPlaying))
        {
            if (!isRecording && GUILayout.Button("Start Recording"))
            {
                recorder.StartRecording();
            }

            if (isRecording && GUILayout.Button("Stop Recording"))
            {
                recorder.StopRecording();
            }
        }

        if (!isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use recording buttons.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Camera Running", recorder.IsCameraRunning ? "Yes" : "No");
            EditorGUILayout.LabelField("Recording", recorder.IsRecording ? "Yes" : "No");
        }

        EditorGUILayout.EndVertical();

        if (isPlaying)
        {
            Repaint();
        }
    }
}
