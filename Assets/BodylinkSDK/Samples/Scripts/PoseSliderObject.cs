using UnityEngine;
using UnityEngine.UI;

public class PoseSliderObject : MonoBehaviour
{
    public Text poseNameText;
    public Text poseConfidenceText;
    public Slider poseConfidenceSlider;

    public void UpdatePoseInfo(string poseName, float confidence)
    {
        confidence *= 100;
        if (poseNameText != null)
        {
            poseNameText.text = poseName;
        }

        if (poseConfidenceText != null)
        {
            poseConfidenceText.text = $"{(int)confidence} %";
        }

        if (poseConfidenceSlider != null)
        {
            poseConfidenceSlider.value = confidence;
        }
    }

    public void ClearPoseInfo()
    {
        UpdatePoseInfo("-", 0f);
    }
}
