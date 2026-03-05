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
        poseNameText.text = poseName;
        poseConfidenceText.text = $"{(int)confidence} %";
        poseConfidenceSlider.value = confidence;
    }
}
