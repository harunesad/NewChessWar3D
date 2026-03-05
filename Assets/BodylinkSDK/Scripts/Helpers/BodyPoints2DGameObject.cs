using Mediapipe.Unity;
using UnityEngine;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace BodylinkSDK
{
    public class BodyPoints2DGameObject
    {
        private PointListAnnotation[] pointListAnnotation = new PointListAnnotation[2];
        public BodyPoints2DGameObject(PointListAnnotation _playerOnePointListAnnotation,PointListAnnotation _playerTwoPointListAnnotation)
        {
            SetPointListAnnotations(_playerOnePointListAnnotation,_playerTwoPointListAnnotation);
        }

        public void SetPointListAnnotations(PointListAnnotation _playerOnePointListAnnotation,PointListAnnotation _playerTwoPointListAnnotation)
        {
            if(_playerTwoPointListAnnotation && _playerOnePointListAnnotation)
                SetLeftRightPlayer(_playerOnePointListAnnotation,_playerTwoPointListAnnotation);
            else
            {
                pointListAnnotation[0] = _playerOnePointListAnnotation;
                pointListAnnotation[1] = _playerTwoPointListAnnotation;
            }

        }

        private void SetLeftRightPlayer(PointListAnnotation _playerOnePointListAnnotation,PointListAnnotation _playerTwoPointListAnnotation)
        {
            try
            {
                //check distance between avatar and player
                float player1Dist = Vector2.Distance(_playerOnePointListAnnotation[0].transform.position ,Vector2.zero);
                float player2Dist = Vector2.Distance(_playerOnePointListAnnotation[0].transform.position , Vector2.zero);

                if(player1Dist < player2Dist)
                {
                    //Debug.LogWarning("Player one is left");
                    pointListAnnotation[0] = _playerOnePointListAnnotation;
                    pointListAnnotation[1] = _playerTwoPointListAnnotation;
                }
                else
                {
                    //Debug.LogWarning("Player two is left");
                    pointListAnnotation[0] = _playerOnePointListAnnotation;
                    pointListAnnotation[1] = _playerTwoPointListAnnotation;
                }
            }
            catch(System.Exception e)
            {
                Debug.LogWarning(e.Message);
                pointListAnnotation[0] = _playerOnePointListAnnotation;
                pointListAnnotation[1] = _playerTwoPointListAnnotation;
            }
        }

        public Transform GetChildOrNull(int index,int playerIndex = 0)
        {
            if (pointListAnnotation == null || pointListAnnotation[playerIndex].transform == null)
                return null;

            if (index < 0 || index >= pointListAnnotation[playerIndex].transform.childCount)
                return null;

            Transform t = pointListAnnotation[playerIndex].transform.GetChild(index);

            // ✅ Flip Y for Unity UI space (do NOT modify transform!)
            Vector3 pos = t.localPosition;
            pos.y = 1f - pos.y; 
            t.localPosition = pos;

            return t;
        }

        // Individual points (common Mediapipe-style indexing — adjust indices if your children order differs)
        // Head / face
        public BodyPart2DGameObject head          =>    new BodyPart2DGameObject(this,0);
        public BodyPart2DGameObject nose          =>    new BodyPart2DGameObject(this,0);
        public BodyPart2DGameObject rightEyeInner =>    new BodyPart2DGameObject(this,1);
        public BodyPart2DGameObject rightEye      =>    new BodyPart2DGameObject(this,2);
        public BodyPart2DGameObject rightEyeOuter =>    new BodyPart2DGameObject(this,3);
        public BodyPart2DGameObject leftEyeInner  =>    new BodyPart2DGameObject(this,4);
        public BodyPart2DGameObject leftEye       =>    new BodyPart2DGameObject(this,5);
        public BodyPart2DGameObject leftEyeOuter  =>    new BodyPart2DGameObject(this,6);
        public BodyPart2DGameObject rightEar      =>    new BodyPart2DGameObject(this,7);
        public BodyPart2DGameObject leftEar       =>    new BodyPart2DGameObject(this,8);
        public BodyPart2DGameObject mouthRight    =>    new BodyPart2DGameObject(this,9);
        public BodyPart2DGameObject mouthLeft     =>    new BodyPart2DGameObject(this,10);


        // Upper body
        public BodyPart2DGameObject rightShoulder =>    new BodyPart2DGameObject(this,11);
        public BodyPart2DGameObject leftShoulder  =>    new BodyPart2DGameObject(this,12);
        public BodyPart2DGameObject rightElbow    =>    new BodyPart2DGameObject(this,13);
        public BodyPart2DGameObject leftElbow     =>    new BodyPart2DGameObject(this,14);
        public BodyPart2DGameObject rightWrist    =>    new BodyPart2DGameObject(this,15);
        public BodyPart2DGameObject leftWrist     =>    new BodyPart2DGameObject(this,16);
    

        // (optional torso / mid points if present)
        public BodyPart2DGameObject rightIndexShoulderMid => new BodyPart2DGameObject(this,17);
        public BodyPart2DGameObject leftIndexShoulderMid => new BodyPart2DGameObject(this,18);
        public BodyPart2DGameObject chest         =>    new BodyPart2DGameObject(this,19);
        public BodyPart2DGameObject pelvis        =>    new BodyPart2DGameObject(this,20);
    

        // Lower body / legs (common indices — adjust as needed)
        public BodyPart2DGameObject rightHip      =>    new BodyPart2DGameObject(this,23);
        public BodyPart2DGameObject leftHip       =>    new BodyPart2DGameObject(this,24);
        public BodyPart2DGameObject rightKnee     =>    new BodyPart2DGameObject(this,25);
        public BodyPart2DGameObject leftKnee      =>    new BodyPart2DGameObject(this,26);
        public BodyPart2DGameObject rightAnkle    =>    new BodyPart2DGameObject(this,27);
        public BodyPart2DGameObject leftAnkle     =>    new BodyPart2DGameObject(this,28);
        public BodyPart2DGameObject rightHeel     =>    new BodyPart2DGameObject(this,29);
        public BodyPart2DGameObject leftHeel      =>    new BodyPart2DGameObject(this,30);
        public BodyPart2DGameObject rightFoot     =>    new BodyPart2DGameObject(this,31);
        public BodyPart2DGameObject leftFoot      =>    new BodyPart2DGameObject(this,32);
    }

    // Wrapper that exposes 2D body points as Unity transforms, allowing easy access through named properties or indexes for any player.
    public class BodyPart2DGameObject
    {
        private readonly BodyPoints2DGameObject owner;
        private readonly int pointIndex;

        public BodyPart2DGameObject(BodyPoints2DGameObject owner, int pointIndex)
        {
            this.owner = owner;
            this.pointIndex = pointIndex;
        }

        //Default player (player 0)
        private GameObject Default => owner.GetChildOrNull(pointIndex, 0).gameObject;

        // Specific player indexer
        public GameObject this[int playerIndex] 
            => owner.GetChildOrNull(pointIndex, playerIndex).gameObject;
    }

}
