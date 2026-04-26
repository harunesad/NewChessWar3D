using System;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodyPoints3DSmoothed
    {
        private const int TotalBodyPoints = 33;

        private readonly BodylinkPlayerAvatar bodylinkPlayer;
        private readonly Landmark[] smoothedPoints = new Landmark[TotalBodyPoints];
        private readonly bool[] hasSmoothedPoint = new bool[TotalBodyPoints];
        private readonly Mediapipe.Landmark smoothingProto = new Mediapipe.Landmark();
        private readonly Mediapipe.Landmark conversionProto = new Mediapipe.Landmark();
        private readonly Landmark defaultLandmark;

        // Shared smoothing settings for ALL joints
        private float positionSmoothing = 0.15f; // 0 = very smooth, 1 = raw
        private float deadZone = 0.002f;

        public BodyPoints3DSmoothed(BodylinkPlayerAvatar player)
        {
            bodylinkPlayer = player;
            defaultLandmark = CreateDefaultLandmark();
            ClearSmoothedPoints();
            InitializeFromRawPoints();
        }

        private Landmark SmoothLandmark(Landmark previous, Landmark raw)
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

            return Landmark.CreateFrom(smoothingProto);
        }

        private Landmark ConvertToUnityCoordinates(Landmark point)
        {
            conversionProto.X = point.x;
            conversionProto.Y = -point.y;
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

            return Landmark.CreateFrom(conversionProto);
        }

        private void InitializeFromRawPoints()
        {
            var rawPoints = bodylinkPlayer.bodyRawPoints3D;
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

        private static Landmark CreateDefaultLandmark()
        {
            var proto = new Mediapipe.Landmark
            {
                X = 0f,
                Y = 0f,
                Z = 0f,
                Visibility = 0f,
                Presence = 0f
            };
            return Landmark.CreateFrom(proto);
        }

        public void Update()
        {
            try
            {
                var rawPoints = bodylinkPlayer.bodyRawPoints3D;
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
                Debug.LogWarning($"[Bodylink] Failed to update smoothed 3D body points: {e}");
                ClearSmoothedPoints();
            }
        }

        /// <summary>
        /// Get smoothed landmark by Mediapipe index (0-32)
        /// </summary>
        private Landmark GetNormalizedPoint(int index)
        {
            if (index < 0 || index >= TotalBodyPoints || !hasSmoothedPoint[index])
            {
                return defaultLandmark;
            }

            return smoothedPoints[index];
        }

        public Landmark this[int index] => GetNormalizedPoint(index);

        public Landmark GetPoint(int index)
        {
            return GetNormalizedPoint(index);
        }

        // Convenience accessors (optional, mirrors BodyPoints2D style)
        public Landmark head { get { return GetNormalizedPoint(0); } }
        public Landmark nose { get { return GetNormalizedPoint(0); } }

        public Landmark leftEyeInner { get { return GetNormalizedPoint(4); } }
        public Landmark leftEye { get { return GetNormalizedPoint(5); } }
        public Landmark leftEyeOuter { get { return GetNormalizedPoint(6); } }
        public Landmark rightEyeInner { get { return GetNormalizedPoint(1); } }
        public Landmark rightEye { get { return GetNormalizedPoint(2); } }
        public Landmark rightEyeOuter { get { return GetNormalizedPoint(3); } }
        public Landmark leftEar { get { return GetNormalizedPoint(8); } }
        public Landmark rightEar { get { return GetNormalizedPoint(7); } }
        public Landmark mouthLeft { get { return GetNormalizedPoint(10); } }
        public Landmark mouthRight { get { return GetNormalizedPoint(9); } }
        // --- Torso Landmarks (4 points) ---
        public Landmark leftShoulder { get { return GetNormalizedPoint(12); } }
        public Landmark rightShoulder { get { return GetNormalizedPoint(11); } }
        public Landmark leftHip { get { return GetNormalizedPoint(24); } }
        public Landmark rightHip { get { return GetNormalizedPoint(23); } }

        // --- Arm Landmarks (10 points) ---
        public Landmark leftElbow { get { return GetNormalizedPoint(14); } }
        public Landmark rightElbow { get { return GetNormalizedPoint(13); } }
        public Landmark leftWrist { get { return GetNormalizedPoint(16); } }
        public Landmark rightWrist { get { return GetNormalizedPoint(15); } }
        public Landmark leftPinky { get { return GetNormalizedPoint(18); } }
        public Landmark rightPinky { get { return GetNormalizedPoint(17); } }
        public Landmark leftIndex { get { return GetNormalizedPoint(20); } }
        public Landmark rightIndex { get { return GetNormalizedPoint(19); } }
        public Landmark leftThumb { get { return GetNormalizedPoint(22); } }
        public Landmark rightThumb { get { return GetNormalizedPoint(21); } }

        // --- Leg Landmarks (8 points) ---
        public Landmark leftKnee { get { return GetNormalizedPoint(26); } }
        public Landmark rightKnee { get { return GetNormalizedPoint(25); } }
        public Landmark leftAnkle { get { return GetNormalizedPoint(28); } }
        public Landmark rightAnkle { get { return GetNormalizedPoint(27); } }
        public Landmark leftHeel { get { return GetNormalizedPoint(30); } }
        public Landmark rightHeel { get { return GetNormalizedPoint(29); } }
        public Landmark leftFootIndex { get { return GetNormalizedPoint(32); } }
        public Landmark rightFootIndex { get { return GetNormalizedPoint(31); } }
    }
}
