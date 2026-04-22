using UnityEngine;
using System.Collections.Generic;

public class StaticPoseExample : MonoBehaviour
{
    public StaticPoseAnalyzer staticPoseAnalyzer;
    public PoseSliderObject[] leftPlayerPoseSliders;
    public PoseSliderObject[] rightPlayerPoseSliders;

    private void OnEnable()
    {
        if (staticPoseAnalyzer == null)
        {
            return;
        }

        staticPoseAnalyzer.OnPlayerPoseScoresUpdated += StaticPoseAnalyzer_OnPlayerPoseScoresUpdated;
        RefreshPlayerPoseScores(0);
        RefreshPlayerPoseScores(1);
    }

    private void OnDisable()
    {
        if (staticPoseAnalyzer == null)
        {
            return;
        }

        staticPoseAnalyzer.OnPlayerPoseScoresUpdated -= StaticPoseAnalyzer_OnPlayerPoseScoresUpdated;
    }


    private void StaticPoseAnalyzer_OnPlayerPoseScoresUpdated(int playerSlot, IReadOnlyList<StaticPoseAnalyzer.PoseScore> scores)
    {
        UpdatePoseSliders(GetPoseSliders(playerSlot), scores);
    }

    private void RefreshPlayerPoseScores(int playerSlot)
    {
        if (staticPoseAnalyzer == null)
        {
            return;
        }

        UpdatePoseSliders(GetPoseSliders(playerSlot), staticPoseAnalyzer.GetPoseScores(playerSlot));
    }

    private PoseSliderObject[] GetPoseSliders(int playerSlot)
    {
        return playerSlot == 0 ? leftPlayerPoseSliders : rightPlayerPoseSliders;
    }

    private static void UpdatePoseSliders(PoseSliderObject[] poseSliders, IReadOnlyList<StaticPoseAnalyzer.PoseScore> scores)
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
                StaticPoseAnalyzer.PoseScore score = scores[i];
                sliderObject.UpdatePoseInfo(score.poseName, score.confidence);
            }
            else
            {
                sliderObject.ClearPoseInfo();
            }
        }
    }
}
