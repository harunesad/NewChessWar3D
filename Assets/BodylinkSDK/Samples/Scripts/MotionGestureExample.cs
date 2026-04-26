using System.Collections.Generic;
using BodylinkSDK;
using UnityEngine;

public class MotionGestureExample : MonoBehaviour
{
    public MotionGestureDetector motionGestureDetector;
    [Header("Left Player")]
    public PoseSliderObject[] leftPlayerPoseSliderObjects;
    [Header("Right Player")]
    public GameObject rightPlayerPoseSliderParent;
    public PoseSliderObject[] rightPlayerPoseSliderObjects;


    private void Start()
    {
        if (Bodylink.Instance != null)
        {
            Bodylink.Instance.OnInitialized += OnBodylinkInitialized;
            if (Bodylink.Instance.IsInitialized)
            {
                OnBodylinkInitialized();
            }
        }

        RefreshMotionScores();
    }

    void OnDisable()
    {
        if (Bodylink.Instance != null)
        {
            Bodylink.Instance.OnInitialized -= OnBodylinkInitialized;
        }
    }

    private void Update()
    {
        RefreshMotionScores();
    }

    private void OnBodylinkInitialized()
    {
        RefreshRightPlayerVisibility();
        RefreshMotionScores();
    }

    private void RefreshMotionScores()
    {
        RefreshRightPlayerVisibility();

        if (motionGestureDetector == null)
        {
            UpdateMotionScoreSliders(leftPlayerPoseSliderObjects, null);
            UpdateMotionScoreSliders(rightPlayerPoseSliderObjects, null);
            return;
        }

        UpdateMotionScoreSliders(leftPlayerPoseSliderObjects, motionGestureDetector.GetMotionScores(0));
        UpdateMotionScoreSliders(rightPlayerPoseSliderObjects, motionGestureDetector.GetMotionScores(1));
    }

    private void RefreshRightPlayerVisibility()
    {
        if (rightPlayerPoseSliderParent == null || Bodylink.Instance == null)
        {
            return;
        }

        rightPlayerPoseSliderParent.SetActive(Bodylink.Instance.isMultiplayerEnabled);
    }

    private static void UpdateMotionScoreSliders(PoseSliderObject[] poseSliders, IReadOnlyList<MotionGestureDetector.MotionScore> scores)
    {
        if (poseSliders == null || poseSliders.Length == 0)
        {
            return;
        }

        int scoreCount = scores != null ? scores.Count : 0;
        for (int i = 0; i < poseSliders.Length; i++)
        {
            PoseSliderObject sliderObject = poseSliders[i];
            if (sliderObject == null)
            {
                continue;
            }

            if (i < scoreCount)
            {
                MotionGestureDetector.MotionScore score = scores[i];
                sliderObject.UpdatePoseInfo(score.motionName, score.confidence);
            }
            else
            {
                sliderObject.ClearPoseInfo();
            }
        }
    }
}
