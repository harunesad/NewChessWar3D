using System;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodyPoints3D
    {
        private const int TotalBodyPoints = 33;

        private readonly BodylinkPlayerAvatar bodylinkPlayer;
        private readonly Landmark[] cachedPoints = new Landmark[TotalBodyPoints];
        private readonly bool[] hasCachedPoint = new bool[TotalBodyPoints];
        private readonly Mediapipe.Landmark reusableProto = new Mediapipe.Landmark();
        private readonly Landmark defaultLandmark;
        private int cacheFrame = -1;

        public BodyPoints3D(BodylinkPlayerAvatar player)
        {
            bodylinkPlayer = player;
            defaultLandmark = CreateDefaultLandmark();
            ClearCache();
        }

        // --- Face / Head Landmarks (11 points) ---
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

        // Generic access by Mediapipe index
        public Landmark this[int index] => NormalizePoint(index);

        public Landmark GetNormalizedPoint(int index)
        {
            return NormalizePoint(index);
        }

        private Landmark NormalizePoint(int index)
        {
            if (index < 0 || index >= TotalBodyPoints)
            {
                return defaultLandmark;
            }

            RefreshCacheIfNeeded();
            return hasCachedPoint[index] ? cachedPoints[index] : defaultLandmark;
        }

        private void RefreshCacheIfNeeded()
        {
            int currentFrame = Time.frameCount;
            if (cacheFrame == currentFrame)
            {
                return;
            }

            cacheFrame = currentFrame;

            try
            {
                var rawPoints = bodylinkPlayer.bodyRawPoints3D;
                int rawCount = rawPoints == null ? 0 : Mathf.Min(rawPoints.Count, TotalBodyPoints);

                for (int i = 0; i < TotalBodyPoints; i++)
                {
                    if (i < rawCount && rawPoints[i] != null)
                    {
                        cachedPoints[i] = ConvertToUnityCoordinates(rawPoints[i]);
                        hasCachedPoint[i] = true;
                    }
                    else
                    {
                        cachedPoints[i] = defaultLandmark;
                        hasCachedPoint[i] = false;
                    }
                }
            }
            catch (Exception)
            {
                ClearCache();
            }
        }

        private void ClearCache()
        {
            for (int i = 0; i < TotalBodyPoints; i++)
            {
                cachedPoints[i] = defaultLandmark;
                hasCachedPoint[i] = false;
            }
        }

        private Landmark ConvertToUnityCoordinates(Landmark landmark)
        {
            reusableProto.X = landmark.x;
            reusableProto.Y = -landmark.y;
            reusableProto.Z = landmark.z;

            if (landmark.visibility.HasValue)
            {
                reusableProto.Visibility = landmark.visibility.Value;
            }
            else
            {
                reusableProto.ClearVisibility();
            }

            if (landmark.presence.HasValue)
            {
                reusableProto.Presence = landmark.presence.Value;
            }
            else
            {
                reusableProto.ClearPresence();
            }

            return Landmark.CreateFrom(reusableProto);
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
    }
}
