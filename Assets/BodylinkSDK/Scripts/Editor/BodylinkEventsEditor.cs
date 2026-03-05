using UnityEngine;
using UnityEditor;
using BodylinkSDK;
using System.Collections.Generic;

[CustomEditor(typeof(BodylinkEvents))]
public class BodylinkEventsEditor : Editor
{
    SerializedProperty enableHandEventProp;
    SerializedProperty enableBodyEventProp;
    SerializedProperty enableHeadEventProp;
    BodylinkEvents controller;

    // foldout states
    bool handFoldout;
    bool bodyFoldout;
    bool headFoldout;

    private readonly Dictionary<string, string[]> eventDescriptions = new Dictionary<string, string[]>
    {
        {
            "HandGestureDetector", new string[]
            {
                "👈 Swipe Left",
                "👉 Swipe Right",
                "👆 Swipe Up",
                "👇 Swipe Down",
                "👋 HandPoseDetect"
            }
        },
        {
            "BodyMovementDetector", new string[]
            {
                "⬅️ Body MoveLeft",
                "➡️ Body MoveRight",
                "⬆️ Jump",
                "🙌 Arm Raise"
            }
        },
        {
            "HeadGestureDetector", new string[]
            {
                "👍 Head Nod",
                "👎 Head Shake",
                "👉 OnLook Right",
                "👈 OnLook Left"
            }
        }
    };

    private readonly string[] handPoses = new string[]
    {
        "🚫 None",
        "✊ Closed_Fist",
        "✋ Open_Palm",
        "☝️ Pointing_Up",
        "👎 Thumb_Down",
        "👍 Thumb_Up",
        "✌️ Victory",
        "🤟 ILoveYou"
    };

    void OnEnable()
    {
        controller = (BodylinkEvents)target;
        enableHandEventProp = serializedObject.FindProperty("EnableHandEvents");
        enableBodyEventProp = serializedObject.FindProperty("EnableBodyEvents");
        enableHeadEventProp = serializedObject.FindProperty("EnableHeadEvents");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField(
            "⚠️ Using too many events together may cause performance issues."
        );

        // 🔹 Head Event section
        DrawEventSection(enableHeadEventProp, "🧑 Head Event", "HeadGestureDetector", ref controller.headGestureDetector, ref headFoldout);

        // 🔹 Hand Event section
        DrawEventSection(enableHandEventProp, "🤚 Hand Event", "HandGestureDetector", ref controller.handGestureDetector, ref handFoldout);

        // 🔹 Body Event section
        DrawEventSection(enableBodyEventProp, "🧍 Body Event", "BodyMovementDetector", ref controller.bodyMovementDetector, ref bodyFoldout);

        DrawCustomEventDetectors();


        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEventSection<T>(SerializedProperty boolProp, string label, string childName, ref T handler, ref bool foldout) where T : Component
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Checkbox toggle
        EditorGUILayout.PropertyField(boolProp, new GUIContent(label));

        if (boolProp.boolValue)
        {
            if (handler == null)
            {
                // Check if child exists
                var existingChild = controller.transform.Find(childName);
                if (existingChild != null)
                {
                    handler = existingChild.GetComponent<T>();
                    if (handler == null)
                        handler = existingChild.gameObject.AddComponent<T>();
                }
                else
                {
                    GameObject child = new GameObject(childName);
                    child.transform.SetParent(controller.transform);
                    handler = child.AddComponent<T>();
                }
            }

            // 🔹 Foldout for child inspector
            foldout = EditorGUILayout.Foldout(foldout, $"{label} Settings", true);
            if (foldout && handler != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    Editor editor = CreateEditor(handler);
                    if (editor != null)
                        editor.OnInspectorGUI();
                }

                // 🔹 Event Details
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Available Events:", EditorStyles.boldLabel);

                if (eventDescriptions.TryGetValue(typeof(T).Name, out var events))
                {
                    foreach (var evt in events)
                    {
                        EditorGUILayout.LabelField("• " + evt);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No events documented yet.");
                }

                // 🔹 Show extra gestures ONLY for Hand
                if (typeof(T).Name == "HandGestureDetector")
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Available Hand Poses:", EditorStyles.boldLabel);

                    foreach (var pose in handPoses)
                    {
                        EditorGUILayout.LabelField("    • " + pose);
                    }
                }
            }
        }
        else
        {
            // Auto-destroy if unchecked
            if (handler != null)
            {
                GameObject toDestroy = handler.gameObject;
                handler = null;
                if (toDestroy != null)
                    DestroyImmediate(toDestroy);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCustomEventDetectors()
    {
        EditorGUILayout.Space();
        if (controller.transform.childCount > 0)
        {
            foreach (Transform child in controller.transform)
            {
                var detector = child.GetComponent<MonoBehaviour>();
                if (detector == null)
                    continue;

                var type = detector.GetType();

                // Skip the known built-in ones
                if (type.Name == "HandGestureDetector" || type.Name == "HeadGestureDetector" || type.Name == "BodyMovementDetector")
                    continue;
                EditorGUILayout.LabelField("🧩 Custom Gesture Detectors", EditorStyles.boldLabel);
                break;
            }
        }

        foreach (Transform child in controller.transform)
        {
            var detector = child.GetComponent<MonoBehaviour>();
            if (detector == null)
                continue;

            var type = detector.GetType();

            // Skip the known built-in ones
            if (type.Name == "HandGestureDetector" ||
                type.Name == "HeadGestureDetector" ||
                type.Name == "BodyMovementDetector")
                continue;

            // ✅ Only include scripts that inherit from BaseGestureDetector
            if (!typeof(BodylinkBaseGestureDetector).IsAssignableFrom(type))
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"🧠 {type.Name}", EditorStyles.boldLabel);

            // Try to show "name" string field and "enabled" bool field
            var nameField = type.GetField("name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var boolField = type.GetField("enabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (nameField != null)
            {
                string currentName = (string)nameField.GetValue(detector);
                string newName = EditorGUILayout.TextField("Name", currentName);
                if (newName != currentName)
                    nameField.SetValue(detector, newName);
            }

            if (boolField != null)
            {
                bool currentEnabled = (bool)boolField.GetValue(detector);
                bool newEnabled = EditorGUILayout.Toggle("Enabled", currentEnabled);
                if (newEnabled != currentEnabled)
                    boolField.SetValue(detector, newEnabled);
            }

            // Show inspector for the detector itself
            EditorGUILayout.Space();
            Editor editor = CreateEditor(detector);
            if (editor != null)
                editor.OnInspectorGUI();

            EditorGUILayout.EndVertical();
        }
    }


}
