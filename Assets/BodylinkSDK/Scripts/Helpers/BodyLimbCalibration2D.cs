using UnityEngine;
using Mediapipe.Unity;

namespace BodylinkSDK
{

    [System.Serializable]
    public class BodyCalibrationData2D
    {
        public float height;
        public float armLength;
        public float legLength;
        public float armRatio;
        public float legRatio;
        public float torsoRatio;
    }

    public class BodyLimbCalibration2D
    {
        public BodyCalibrationData2D calibrationData { get; private set; }

        public BodyLimbCalibration2D(BodyPoints2D bodyPoints2D)
        {
            body2D = bodyPoints2D;
        }


        // Get from SDK
        private BodyPoints2D body2D;

        public void CalculateLimbRatio()
        {
            if (body2D == null) return;

            // 🧠 Get landmarks
            var nose = body2D.nose;

            var leftAnkle = body2D.leftAnkle;
            var rightAnkle = body2D.rightAnkle;

            var leftShoulder = body2D.leftShoulder;
            var rightShoulder = body2D.rightShoulder;

            var leftElbow = body2D.leftElbow;
            var rightElbow = body2D.rightElbow;

            var leftWrist = body2D.leftWrist;
            var rightWrist = body2D.rightWrist;

            var leftHip = body2D.leftHip;
            var rightHip = body2D.rightHip;

            var leftKnee = body2D.leftKnee;
            var rightKnee = body2D.rightKnee;

            // 🔹 Step 1: Calculate height (nose → mid-ankle)
            Vector2 midAnkle = new Vector2(
                (leftAnkle.x + rightAnkle.x) * 0.5f,
                (leftAnkle.y + rightAnkle.y) * 0.5f
            );

            if (calibrationData == null)
                calibrationData = new BodyCalibrationData2D();

            calibrationData.height = Mathf.Abs(nose.y - midAnkle.y);


            // 🔹 Step 2: Arm length using (Shoulder → Elbow) + (Elbow → Wrist)
            float leftArm =
                Vector2.Distance(new Vector2(leftShoulder.x, leftShoulder.y),
                                new Vector2(leftElbow.x, leftElbow.y))
            + Vector2.Distance(new Vector2(leftElbow.x, leftElbow.y),
                                new Vector2(leftWrist.x, leftWrist.y));

            float rightArm =
                Vector2.Distance(new Vector2(rightShoulder.x, rightShoulder.y),
                                new Vector2(rightElbow.x, rightElbow.y))
            + Vector2.Distance(new Vector2(rightElbow.x, rightElbow.y),
                                new Vector2(rightWrist.x, rightWrist.y));

            float avgArm = (leftArm + rightArm) * 0.5f;


            // 🔹 Step 3: Leg length using (Hip → Knee) + (Knee → Ankle)
            float leftLeg =
                Vector2.Distance(new Vector2(leftHip.x, leftHip.y),
                                new Vector2(leftKnee.x, leftKnee.y))
            + Vector2.Distance(new Vector2(leftKnee.x, leftKnee.y),
                                new Vector2(leftAnkle.x, leftAnkle.y));

            float rightLeg =
                Vector2.Distance(new Vector2(rightHip.x, rightHip.y),
                                new Vector2(rightKnee.x, rightKnee.y))
            + Vector2.Distance(new Vector2(rightKnee.x, rightKnee.y),
                                new Vector2(rightAnkle.x, rightAnkle.y));

            float avgLeg = (leftLeg + rightLeg) * 0.5f;


            // 🔹 Step 4: Torso length (mid-shoulder → mid-hip)
            Vector2 midShoulder = new Vector2(
                (leftShoulder.x + rightShoulder.x) * 0.5f,
                (leftShoulder.y + rightShoulder.y) * 0.5f
            );

            Vector2 midHip = new Vector2(
                (leftHip.x + rightHip.x) * 0.5f,
                (leftHip.y + rightHip.y) * 0.5f
            );

            float torsoLength = Vector2.Distance(midShoulder, midHip);


            // 🔹 Step 5: Normalized Ratios
            calibrationData.armLength = avgArm;
            calibrationData.legLength = avgLeg;
            calibrationData.armRatio = avgArm / calibrationData.height;
            calibrationData.legRatio = avgLeg / calibrationData.height;
            calibrationData.torsoRatio = torsoLength / calibrationData.height;
        }
    }
}