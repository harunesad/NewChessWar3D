using System;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodyPoints2DSmoothed
    {
        private const int TotalBodyPoints = 33;

        private readonly BodylinkPlayerAvatar bodylinkPlayer;
        private readonly NormalizedLandmark[] smoothedPoints = new NormalizedLandmark[TotalBodyPoints];
        private readonly bool[] hasSmoothedPoint = new bool[TotalBodyPoints];
        private readonly Mediapipe.NormalizedLandmark smoothingProto = new Mediapipe.NormalizedLandmark();
        private readonly Mediapipe.NormalizedLandmark conversionProto = new Mediapipe.NormalizedLandmark();
        private readonly NormalizedLandmark defaultLandmark;

        // Shared smoothing settings for ALL joints
        private float positionSmoothing = 0.15f; // 0 = very smooth, 1 = raw
        private float deadZone = 0.002f;

        public BodyPoints2DSmoothed(BodylinkPlayerAvatar player)
        {
            bodylinkPlayer = player;
            defaultLandmark = CreateDefaultLandmark();
            ClearSmoothedPoints();
            InitializeFromRawPoints();
        }

        private NormalizedLandmark SmoothLandmark(NormalizedLandmark previous, NormalizedLandmark raw)
        {
            float dx = raw.x - previous.x;
            float dy = raw.y - previous.y;
            float sqrMag = dx * dx + dy * dy;

            // Dead-zone filtering.
            if (sqrMag < deadZone * deadZone)
            {
                return previous;
            }

            smoothingProto.X = Mathf.Lerp(previous.x, raw.x, positionSmoothing);
            smoothingProto.Y = Mathf.Lerp(previous.y, raw.y, positionSmoothing);
            smoothingProto.Z = Mathf.Lerp(previous.z, raw.z, positionSmoothing);

            if (raw.visibility.HasValue)
            {
                smoothingProto.Visibility = raw.visibility.Value;
            }
            else
            {
                smoothingProto.ClearVisibility();
            }

            if (raw.presence.HasValue)
            {
                smoothingProto.Presence = raw.presence.Value;
            }
            else
            {
                smoothingProto.ClearPresence();
            }

            return NormalizedLandmark.CreateFrom(smoothingProto);
        }

        private NormalizedLandmark ConvertToUnityCoordinates(NormalizedLandmark point)
        {
            conversionProto.X = point.x;
            conversionProto.Y = 1f - point.y;
            conversionProto.Z = point.z;

            if (point.visibility.HasValue)
            {
                conversionProto.Visibility = point.visibility.Value;
            }
            else
            {
                conversionProto.ClearVisibility();
            }

            if (point.presence.HasValue)
            {
                conversionProto.Presence = point.presence.Value;
            }
            else
            {
                conversionProto.ClearPresence();
            }

            return NormalizedLandmark.CreateFrom(conversionProto);
        }

        private void InitializeFromRawPoints()
        {
            var rawPoints = bodylinkPlayer.bodyRawPoints2D;
            int rawCount = rawPoints == null ? 0 : Mathf.Min(rawPoints.Count, TotalBodyPoints);

            for (int i = 0; i < TotalBodyPoints; i++)
            {
                if (i < rawCount && rawPoints[i] != null)
                {
                    smoothedPoints[i] = ConvertToUnityCoordinates(rawPoints[i]);
                    hasSmoothedPoint[i] = true;
                }
                else
                {
                    smoothedPoints[i] = defaultLandmark;
                    hasSmoothedPoint[i] = false;
                }
            }
        }

        private void ClearSmoothedPoints()
        {
            for (int i = 0; i < TotalBodyPoints; i++)
            {
                smoothedPoints[i] = defaultLandmark;
                hasSmoothedPoint[i] = false;
            }
        }

        private static NormalizedLandmark CreateDefaultLandmark()
        {
            var proto = new Mediapipe.NormalizedLandmark
            {
                X = 0f,
                Y = 0f,
                Z = 0f,
                Visibility = 0f,
                Presence = 0f
            };
            return NormalizedLandmark.CreateFrom(proto);
        }

        public void Update()
        {
            try
            {
                var rawPoints = bodylinkPlayer.bodyRawPoints2D;
                int rawCount = rawPoints == null ? 0 : Mathf.Min(rawPoints.Count, TotalBodyPoints);

                for (int i = 0; i < TotalBodyPoints; i++)
                {
                    if (i < rawCount && rawPoints[i] != null)
                    {
                        var raw = ConvertToUnityCoordinates(rawPoints[i]);
                        if (hasSmoothedPoint[i])
                        {
                            smoothedPoints[i] = SmoothLandmark(smoothedPoints[i], raw);
                        }
                        else
                        {
                            smoothedPoints[i] = raw;
                            hasSmoothedPoint[i] = true;
                        }
                    }
                    else
                    {
                        smoothedPoints[i] = defaultLandmark;
                        hasSmoothedPoint[i] = false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bodylink] Failed to update smoothed 2D body points: {e}");
                ClearSmoothedPoints();
            }
        }

        /// <summary>
        /// Get smoothed landmark by Mediapipe index (0-32)
        /// </summary>
        private NormalizedLandmark GetNormalizedPoint(int index)
        {
            if (index < 0 || index >= TotalBodyPoints || !hasSmoothedPoint[index])
            {
                return defaultLandmark;
            }

            return smoothedPoints[index];
        }

        // Convenience accessors (optional, mirrors BodyPoints2D style)
        public NormalizedLandmark head { get { return GetNormalizedPoint(0); } }
        public NormalizedLandmark nose { get { return GetNormalizedPoint(0); } }

        public NormalizedLandmark leftEyeInner { get { return GetNormalizedPoint(4); } }
        public NormalizedLandmark leftEye { get { return GetNormalizedPoint(5); } }
        public NormalizedLandmark leftEyeOuter { get { return GetNormalizedPoint(6); } }
        public NormalizedLandmark rightEyeInner { get { return GetNormalizedPoint(1); } }
        public NormalizedLandmark rightEye { get { return GetNormalizedPoint(2); } }
        public NormalizedLandmark rightEyeOuter { get { return GetNormalizedPoint(3); } }
        public NormalizedLandmark leftEar { get { return GetNormalizedPoint(8); } }
        public NormalizedLandmark rightEar { get { return GetNormalizedPoint(7); } }
        public NormalizedLandmark mouthLeft { get { return GetNormalizedPoint(10); } }
        public NormalizedLandmark mouthRight { get { return GetNormalizedPoint(9); } }
        // --- Torso Landmarks (4 points) ---
        public NormalizedLandmark leftShoulder { get { return GetNormalizedPoint(12); } }
        public NormalizedLandmark rightShoulder { get { return GetNormalizedPoint(11); } }
        public NormalizedLandmark leftHip { get { return GetNormalizedPoint(24); } }
        public NormalizedLandmark rightHip { get { return GetNormalizedPoint(23); } }

        // --- Arm Landmarks (10 points) ---
        public NormalizedLandmark leftElbow { get { return GetNormalizedPoint(14); } }
        public NormalizedLandmark rightElbow { get { return GetNormalizedPoint(13); } }
        public NormalizedLandmark leftWrist { get { return GetNormalizedPoint(16); } }
        public NormalizedLandmark rightWrist { get { return GetNormalizedPoint(15); } }
        public NormalizedLandmark leftPinky { get { return GetNormalizedPoint(18); } }
        public NormalizedLandmark rightPinky { get { return GetNormalizedPoint(17); } }
        public NormalizedLandmark leftIndex { get { return GetNormalizedPoint(20); } }
        public NormalizedLandmark rightIndex { get { return GetNormalizedPoint(19); } }
        public NormalizedLandmark leftThumb { get { return GetNormalizedPoint(22); } }
        public NormalizedLandmark rightThumb { get { return GetNormalizedPoint(21); } }

        // --- Leg Landmarks (8 points) ---
        public NormalizedLandmark leftKnee { get { return GetNormalizedPoint(26); } }
        public NormalizedLandmark rightKnee { get { return GetNormalizedPoint(25); } }
        public NormalizedLandmark leftAnkle { get { return GetNormalizedPoint(28); } }
        public NormalizedLandmark rightAnkle { get { return GetNormalizedPoint(27); } }
        public NormalizedLandmark leftHeel { get { return GetNormalizedPoint(30); } }
        public NormalizedLandmark rightHeel { get { return GetNormalizedPoint(29); } }
        public NormalizedLandmark leftFootIndex { get { return GetNormalizedPoint(32); } }
        public NormalizedLandmark rightFootIndex { get { return GetNormalizedPoint(31); } }
    }
}
