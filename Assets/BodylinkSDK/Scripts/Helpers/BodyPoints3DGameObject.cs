using BodylinkSDK;
using Mediapipe.Unity;
using UnityEngine;

namespace BodylinkSDK
{
    public class BodyPoints3DGameObject
    {
        public BodylinkSkeletonVisualizer[] skeletonVisualizers
        {
            get
            {
                return Bodylink.Instance.skeletonVisualizers;
            }
            set { }
        }

        public BodyPoints3DGameObject(BodylinkSkeletonVisualizer[] skeletonVisualizer)
        {
            this.skeletonVisualizers = skeletonVisualizer;
        }

        public Transform GetPointTransform(int pointIndex, int playerIndex)
        {
            return skeletonVisualizers[playerIndex].PointTransforms[pointIndex];
        }

        //SkeltonVisualizer[] skeltonVisualizer { get { return Bodylink.Instance.skeltonVisualizers; } }

        // Head points
        public BodyPart3DGameObject head => new BodyPart3DGameObject(this, 0);
        public BodyPart3DGameObject nose => new BodyPart3DGameObject(this, 0);
        public BodyPart3DGameObject rightEyeInner => new BodyPart3DGameObject(this, 1);
        public BodyPart3DGameObject rightEye => new BodyPart3DGameObject(this, 2);
        public BodyPart3DGameObject rightEyeOuter => new BodyPart3DGameObject(this, 3);
        public BodyPart3DGameObject leftEyeInner => new BodyPart3DGameObject(this, 4);
        public BodyPart3DGameObject leftEye => new BodyPart3DGameObject(this, 5);
        public BodyPart3DGameObject leftEyeOuter => new BodyPart3DGameObject(this, 6);
        public BodyPart3DGameObject rightEar => new BodyPart3DGameObject(this, 7);
        public BodyPart3DGameObject leftEar => new BodyPart3DGameObject(this, 8);
        public BodyPart3DGameObject rightMouth => new BodyPart3DGameObject(this, 9);
        public BodyPart3DGameObject leftMouth => new BodyPart3DGameObject(this, 10);


        // Upper body points
        public BodyPart3DGameObject rightShoulder => new BodyPart3DGameObject(this, 11);
        public BodyPart3DGameObject leftShoulder => new BodyPart3DGameObject(this, 12);
        public BodyPart3DGameObject rightElbow => new BodyPart3DGameObject(this, 13);
        public BodyPart3DGameObject leftElbow => new BodyPart3DGameObject(this, 14);
        public BodyPart3DGameObject rightWrist => new BodyPart3DGameObject(this, 15);
        public BodyPart3DGameObject leftWrist => new BodyPart3DGameObject(this, 16);
        public BodyPart3DGameObject rightPinky => new BodyPart3DGameObject(this, 17);
        public BodyPart3DGameObject leftPinky => new BodyPart3DGameObject(this, 18);
        public BodyPart3DGameObject rightIndex => new BodyPart3DGameObject(this, 19);
        public BodyPart3DGameObject leftIndex => new BodyPart3DGameObject(this, 20);
        public BodyPart3DGameObject rightThumb => new BodyPart3DGameObject(this, 21);
        public BodyPart3DGameObject leftThumb => new BodyPart3DGameObject(this, 22);


        // Lower body points
        public BodyPart3DGameObject rightHip => new BodyPart3DGameObject(this, 23);
        public BodyPart3DGameObject leftHip => new BodyPart3DGameObject(this, 24);
        public BodyPart3DGameObject rightKnee => new BodyPart3DGameObject(this, 25);
        public BodyPart3DGameObject leftKnee => new BodyPart3DGameObject(this, 26);
        public BodyPart3DGameObject rightAnkle => new BodyPart3DGameObject(this, 27);
        public BodyPart3DGameObject leftAnkle => new BodyPart3DGameObject(this, 28);
        public BodyPart3DGameObject rightHeel => new BodyPart3DGameObject(this, 29);
        public BodyPart3DGameObject leftHeel => new BodyPart3DGameObject(this, 30);
        public BodyPart3DGameObject rightFootIndex => new BodyPart3DGameObject(this, 31);
        public BodyPart3DGameObject leftFootIndex => new BodyPart3DGameObject(this, 32);

    }


    // Wrapper for 3D skeleton Transforms, enabling both named and indexed landmark access for any player.
    public class BodyPart3DGameObject
    {
        private readonly BodyPoints3DGameObject owner;
        private readonly int pointIndex;

        public BodyPart3DGameObject(BodyPoints3DGameObject owner, int pointIndex)
        {
            this.owner = owner;
            this.pointIndex = pointIndex;
        }

        //Default player (player 0)
        private GameObject Default => owner.GetPointTransform(pointIndex, 0).gameObject;

        // Specific player indexer
        public GameObject this[int playerIndex]
            => owner.GetPointTransform(pointIndex, playerIndex).gameObject;
    }

}
