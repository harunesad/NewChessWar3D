using Mediapipe;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine.UI;
using UnityEngine;

public class HandGestureDetection : MonoBehaviour
{
    public HandGestureRunner handLandmarkerRunner;
    public Text handPoseText;
    string poseName = "";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        handLandmarkerRunner.SetAction(OnHandLandmarkDetectionOutput);
    }

    private void OnHandLandmarkDetectionOutput(GestureRecognizerResult result, long timestamp)
    {
        // Handle the detected hand landmarks here
        Debug.Log($"Detected {result.gestures[0].categories[0].categoryName}");
        poseName = result.gestures[0].categories[0].categoryName;
        //Debug.Log("Head name "+result.handedness[0].headName);

        // // You can access the landmarks and handedness like this:
        // for (int i = 0; i < result.handLandmarks.Count; i++)
        // {
        //     var landmarks = result.handLandmarks[i];
        //     var handedness = result.handedness[i];
        //     //Debug.Log($"Hand {i}: {handedness.Classification[0].Label}, Landmarks: {landmarks}");
        // }
    }

    void Update()
    {
        handPoseText.text = poseName;
    }
}
