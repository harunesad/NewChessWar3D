using System;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodylinkSkeletonVisualizer : MonoBehaviour
    {
        public int playerIndex;
        [Header("Dependencies")]
        [SerializeField] public PointListAnnotation pointListAnnotation;
        [SerializeField] public ConnectionListAnnotation connectionListAnnotation;
        [Header("Transform Settings")]
        [SerializeField] private float _hipHeightMeter = 0.9f;
        [SerializeField] public float scale = 5f;   // increase to spread points more
        [SerializeField] public Vector3 offset = Vector3.zero;  // shift skeleton if needed


        [Header("Smoothing Settings")]
        [Range(1, 20)] public int smoothingWindow = 5;

        private PoseLandmarkerResult poseLandmarkerResult;
        private readonly object poseResultLock = new object();
        private PoseLandmarkerResult poseWriteBuffer;
        private PoseLandmarkerResult poseReadBuffer;
        private bool hasPendingPoseResult;
        private bool hasResult;

        private Transform[] _pointTransforms;
        public Transform[] PointTransforms { get { return _pointTransforms; } }
        private Transform[] connectionTransforms;

        // One buffer (queue) per point index
        private Queue<Vector3>[] smoothingBuffers;

        void Start()
        {

            Bodylink.Instance.OnInitialized += Init;
            Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult += ResultFunction;
        }

        void Init()
        {

            // Cache child transforms
            int pointCount = pointListAnnotation.transform.childCount;
            _pointTransforms = new Transform[pointCount];
            smoothingBuffers = new Queue<Vector3>[pointCount];

            for (int i = 0; i < pointCount; i++)
            {
                _pointTransforms[i] = pointListAnnotation.transform.GetChild(i);
                _pointTransforms[i].gameObject.SetActive(false);
                smoothingBuffers[i] = new Queue<Vector3>();
            }

            int connectionCount = connectionListAnnotation.transform.childCount;
            connectionTransforms = new Transform[connectionCount];
            for (int i = 0; i < connectionCount; i++)
            {
                connectionTransforms[i] = connectionListAnnotation.transform.GetChild(i);
                connectionTransforms[i].gameObject.SetActive(false);
            }
        }

        private void ResultFunction(PoseLandmarkerResult result, long timeElapse)
        {
            lock (poseResultLock)
            {
                result.CloneTo(ref poseWriteBuffer);
                hasPendingPoseResult = true;
            }

            // Activate annotations only once when we have the first result
        }

        void Update()
        {
            if (!Bodylink.Instance.IsInitialized) return;
            ConsumePendingPoseResult();
            ActivateAnnotations();
            if (poseLandmarkerResult.poseWorldLandmarks == null || poseLandmarkerResult.poseWorldLandmarks.Count <= playerIndex) return;
            int mediapipeIndex;
            if (playerIndex == 0)
                mediapipeIndex = Bodylink.Instance.bodylinkAvatar.stablePlayerIndex[0];
            else
                mediapipeIndex = Bodylink.Instance.bodylinkAvatar.stablePlayerIndex[1];


            var landmarks = poseLandmarkerResult.poseWorldLandmarks[mediapipeIndex].landmarks;
            int count = Mathf.Min(landmarks.Count, _pointTransforms.Length);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    if (i >= landmarks.Count) continue;
                    Vector3 rawPos = new Vector3(
                        landmarks[i].x,
                        -landmarks[i].y,   // Flip Y-axis
                        landmarks[i].z
                    );

                    Vector3 smoothed = SmoothPoint(rawPos, smoothingBuffers[i]);
                    _pointTransforms[i].localPosition = smoothed * scale + offset;
                }
                catch (Exception e) { }
            }

            connectionListAnnotation.Redraw();
        }

        private void ConsumePendingPoseResult()
        {
            lock (poseResultLock)
            {
                if (!hasPendingPoseResult) return;

                var swap = poseReadBuffer;
                poseReadBuffer = poseWriteBuffer;
                poseWriteBuffer = swap;
                hasPendingPoseResult = false;

                poseLandmarkerResult = poseReadBuffer;
                hasResult = true;
            }
        }

        private Vector3 SmoothPoint(Vector3 newPos, Queue<Vector3> buffer)
        {
            buffer.Enqueue(newPos);
            if (buffer.Count > smoothingWindow)
                buffer.Dequeue();

            Vector3 sum = Vector3.zero;
            foreach (var pos in buffer)
                sum += pos;

            return sum / buffer.Count;
        }

        private void ActivateAnnotations()
        {
            foreach (var t in _pointTransforms)
            {
                if (t != null)
                    t.gameObject.SetActive(true);
            }

            foreach (var t in connectionTransforms)
            {
                if (t != null)
                    t.gameObject.SetActive(true);
            }
        }

        public void ShowSkelton(bool show, float size)
        {
            if (show == false)
            {
                pointListAnnotation.SetRadius(0);
                connectionListAnnotation.SetLineWidth(0);
            }
            else
            {
                pointListAnnotation.SetRadius(size);
                connectionListAnnotation.SetLineWidth(size / 2);
            }
        }
    }
}
