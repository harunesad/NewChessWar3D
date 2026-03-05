using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class PoseAnalyzerDisplay : MonoBehaviour
{
    public PoseConfidenceAnalyzer poseConfidenceAnalyzer;
    public Text leftPlayerBestPoseText, rightPlayerBestPoseText;
    public GameObject playerOneUI, playerTwoUI;
    public RawImage miniCamera;
    public PoseSliderObject[] leftPlayerSliders;
    public PoseSliderObject[] rightPlayerSliders;

    void Start()
    {
        Bodylink.Instance.OnInitialized += () =>
        {
            playerTwoUI.SetActive(Bodylink.Instance.isMultiplayerEnabled);
            miniCamera.texture = Bodylink.Instance.cameraScreen.texture;
        };
    }



    void Update()
    {
        if (Bodylink.Instance.isMultiplayerEnabled)
        {
            rightPlayerSliders[0].UpdatePoseInfo("Idle Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.Idle, 1));
            rightPlayerSliders[1].UpdatePoseInfo("T-Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.TPose, 1));
            rightPlayerSliders[2].UpdatePoseInfo("A-Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.APose, 1));
            rightPlayerSliders[3].UpdatePoseInfo("Both Arms Up", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.BothArmsUp, 1));
            rightPlayerSliders[4].UpdatePoseInfo("Crouch Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.Crouch, 1));
            rightPlayerSliders[5].UpdatePoseInfo("OneLegBalance", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.OneLegBalance, 1));
            rightPlayerSliders[6].UpdatePoseInfo("CrossArm", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.CrossArm, 1));
            rightPlayerSliders[7].UpdatePoseInfo("ArmFlex", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.ArmFlex, 1));
            rightPlayerSliders[8].UpdatePoseInfo("HandsTogether", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.HandsTogether, 1));
            rightPlayerSliders[9].UpdatePoseInfo("Hero", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.HeroPose, 1));
        }
        leftPlayerSliders[0].UpdatePoseInfo("Idle Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.Idle));
        leftPlayerSliders[1].UpdatePoseInfo("T-Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.TPose));
        leftPlayerSliders[2].UpdatePoseInfo("A-Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.APose));
        leftPlayerSliders[3].UpdatePoseInfo("Both Arms Up", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.BothArmsUp));
        leftPlayerSliders[4].UpdatePoseInfo("Crouch Pose", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.Crouch));
        leftPlayerSliders[5].UpdatePoseInfo("OneLegBalance", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.OneLegBalance));
        leftPlayerSliders[6].UpdatePoseInfo("CrossArm", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.CrossArm));
        leftPlayerSliders[7].UpdatePoseInfo("ArmFlex", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.ArmFlex));
        leftPlayerSliders[8].UpdatePoseInfo("HandsTogether", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.HandsTogether));
        leftPlayerSliders[9].UpdatePoseInfo("Hero", poseConfidenceAnalyzer.GetPoseConfidence(PoseType.HeroPose));
    }


}
