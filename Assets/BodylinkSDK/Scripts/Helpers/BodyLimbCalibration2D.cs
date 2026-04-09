using UnityEngine;

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
        private readonly BodyPoints2D body2D;

        public BodyCalibrationData2D calibrationData { get; private set; }
        public BodyCalibrationData2D currentFrameData { get; private set; }

        public BodyLimbCalibration2D(BodyPoints2D bodyPoints2D)
        {
            body2D = bodyPoints2D;
        }

        public void CalculateLimbRatio()
        {
            if (TryBuildCalibrationData(out BodyCalibrationData2D measurement))
            {
                currentFrameData = measurement;
            }
            else
            {
                currentFrameData = null;
            }
        }

        public bool TryGetCurrentMeasurement(out BodyCalibrationData2D measurement)
        {
            if (!TryBuildCalibrationData(out BodyCalibrationData2D data))
            {
                measurement = null;
                return false;
            }

            currentFrameData = Clone(data);
            measurement = Clone(data);
            return true;
        }

        public bool CaptureCurrentMeasurement()
        {
            if (!TryGetCurrentMeasurement(out BodyCalibrationData2D measurement))
            {
                return false;
            }

            calibrationData = measurement;
            return true;
        }

        public void SetCalibrationData(BodyCalibrationData2D data)
        {
            calibrationData = Clone(data);
        }

        public void ResetCalibration()
        {
            calibrationData = null;
        }

        public static BodyCalibrationData2D Clone(BodyCalibrationData2D source)
        {
            if (source == null)
            {
                return null;
            }

            return new BodyCalibrationData2D
            {
                height = source.height,
                armLength = source.armLength,
                legLength = source.legLength,
                armRatio = source.armRatio,
                legRatio = source.legRatio,
                torsoRatio = source.torsoRatio
            };
        }

        private bool TryBuildCalibrationData(out BodyCalibrationData2D data)
        {
            data = null;
            if (body2D == null)
            {
                return false;
            }

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

            Vector2 midAnkle = new Vector2(
                (leftAnkle.x + rightAnkle.x) * 0.5f,
                (leftAnkle.y + rightAnkle.y) * 0.5f
            );

            data = new BodyCalibrationData2D
            {
                height = Mathf.Abs(nose.y - midAnkle.y)
            };

            float leftArm =
                Vector2.Distance(new Vector2(leftShoulder.x, leftShoulder.y), new Vector2(leftElbow.x, leftElbow.y)) +
                Vector2.Distance(new Vector2(leftElbow.x, leftElbow.y), new Vector2(leftWrist.x, leftWrist.y));

            float rightArm =
                Vector2.Distance(new Vector2(rightShoulder.x, rightShoulder.y), new Vector2(rightElbow.x, rightElbow.y)) +
                Vector2.Distance(new Vector2(rightElbow.x, rightElbow.y), new Vector2(rightWrist.x, rightWrist.y));

            float avgArm = (leftArm + rightArm) * 0.5f;

            float leftLeg =
                Vector2.Distance(new Vector2(leftHip.x, leftHip.y), new Vector2(leftKnee.x, leftKnee.y)) +
                Vector2.Distance(new Vector2(leftKnee.x, leftKnee.y), new Vector2(leftAnkle.x, leftAnkle.y));

            float rightLeg =
                Vector2.Distance(new Vector2(rightHip.x, rightHip.y), new Vector2(rightKnee.x, rightKnee.y)) +
                Vector2.Distance(new Vector2(rightKnee.x, rightKnee.y), new Vector2(rightAnkle.x, rightAnkle.y));

            float avgLeg = (leftLeg + rightLeg) * 0.5f;

            Vector2 midShoulder = new Vector2(
                (leftShoulder.x + rightShoulder.x) * 0.5f,
                (leftShoulder.y + rightShoulder.y) * 0.5f
            );

            Vector2 midHip = new Vector2(
                (leftHip.x + rightHip.x) * 0.5f,
                (leftHip.y + rightHip.y) * 0.5f
            );

            float torsoLength = Vector2.Distance(midShoulder, midHip);

            if (!IsFiniteAndPositive(data.height) ||
                !IsFiniteAndPositive(avgArm) ||
                !IsFiniteAndPositive(avgLeg) ||
                !IsFiniteAndPositive(torsoLength))
            {
                data = null;
                return false;
            }

            data.armLength = avgArm;
            data.legLength = avgLeg;
            data.armRatio = avgArm / data.height;
            data.legRatio = avgLeg / data.height;
            data.torsoRatio = torsoLength / data.height;

            if (!IsFiniteAndPositive(data.armRatio) ||
                !IsFiniteAndPositive(data.legRatio) ||
                !IsFiniteAndPositive(data.torsoRatio))
            {
                data = null;
                return false;
            }

            return true;
        }

        private static bool IsFiniteAndPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > Mathf.Epsilon;
        }
    }
}
