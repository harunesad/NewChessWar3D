using System;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodyPoints2D
    {
        private const int TotalBodyPoints = 33;

        private readonly BodylinkPlayerAvatar bodylinkPlayer;
        private readonly NormalizedLandmark[] cachedPoints = new NormalizedLandmark[TotalBodyPoints];
        private readonly bool[] hasCachedPoint = new bool[TotalBodyPoints];
        private readonly Mediapipe.NormalizedLandmark reusableProto = new Mediapipe.NormalizedLandmark();
        private readonly NormalizedLandmark defaultLandmark;
        private int cacheFrame = -1;

        public BodyPoints2D(BodylinkPlayerAvatar player)
        {
            bodylinkPlayer = player;
            defaultLandmark = CreateDefaultLandmark();
            ClearCache();
        }

        public BodyPoints2D fullbody { get { return bodylinkPlayer.body2D; } }

        // --- Face / Head Landmarks (11 points) ---
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

        // Generic access by Mediapipe index
        public NormalizedLandmark this[int index] => NormalizePoint(index);

        public NormalizedLandmark GetNormalizedPoint(int index)
        {
            return NormalizePoint(index);
        }

        private NormalizedLandmark NormalizePoint(int index)
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
                var rawPoints = bodylinkPlayer.bodyRawPoints2D;
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

        private NormalizedLandmark ConvertToUnityCoordinates(NormalizedLandmark landmark)
        {
            reusableProto.X = landmark.x;
            reusableProto.Y = 1f - landmark.y;
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

            return NormalizedLandmark.CreateFrom(reusableProto);
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
    }
}
