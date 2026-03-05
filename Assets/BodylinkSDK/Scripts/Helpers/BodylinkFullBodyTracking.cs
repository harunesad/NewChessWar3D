using System.Collections.Generic;
using Mediapipe.Unity.CoordinateSystem;
using UnityEngine;
using BodylinkSDK;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using System;

public class BodylinkFullBodyTracking : MonoBehaviour
{
    public int playerIndex;
    private Transform _bonePrefab;
    public float scaleFactor = 1;
    [Range(0, 1)]
    public float visibilityThreshold = .6f;
    [Range(0, 1)]
    public float smoothness = .5f;
    [SerializeField]
    private bool inPlace = true;
    [SerializeField]
    private bool scaleMatch = false;
    public RectTransform skeletonParent;
    [HideInInspector]
    public List<LandmarkData> bones = new List<LandmarkData>();
    private List<BoneMapper> boneMappers = new List<BoneMapper>();
    [HideInInspector]
    private int _numberOfBones = 33;
    private Animator avatar;
    [HideInInspector]
    public LandmarkData virtualHip;
    [HideInInspector]
    public LandmarkData virtualNeck;
    private float _roiWidth = 1;
    private Transform head;
    private Transform hip;
    private Vector3 hipPosition;
    private RectTransform screen;
    private PoseLandmarkerResult poseLandmarkerResult;
    private readonly object poseResultLock = new object();
    private PoseLandmarkerResult poseWriteBuffer;
    private PoseLandmarkerResult poseReadBuffer;
    private bool hasPendingPoseResult;
    private int trackedPlayerIndex = 0;
    private bool isInitialize;
    private void Start()
    {
        Bodylink.Instance.OnInitialized += () =>
        {
            CreateSkeletonStructure();
            isInitialize = true;
            Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult += (_poseLandmarkerResult, _timeStamp) =>
            {
                lock (poseResultLock)
                {
                    _poseLandmarkerResult.CloneTo(ref poseWriteBuffer);
                    hasPendingPoseResult = true;
                }
            };

        };
    }

    private void CreateSkeletonStructure()
    {
        virtualNeck = new LandmarkData { transform = new GameObject().transform, visibility = 0 };
        virtualNeck.transform.parent = skeletonParent;
        virtualNeck.transform.localEulerAngles = Vector3.zero;
        virtualNeck.transform.name = "NECK";
        virtualHip = new LandmarkData { transform = new GameObject().transform, visibility = 0 };
        virtualHip.transform.parent = skeletonParent;
        virtualHip.transform.name = "HIP";
        virtualHip.transform.localEulerAngles = Vector3.zero;

        _bonePrefab = new GameObject().transform;
        screen = skeletonParent.GetComponent<RectTransform>();
        avatar = GetComponentInChildren<Animator>();
        for (int i = 0; i < _numberOfBones; i++)
        {
            Transform trans = Instantiate(_bonePrefab, skeletonParent);
            LandmarkData landmarkData = new LandmarkData { transform = trans, visibility = 0 };
            trans.name = "Player_" + playerIndex + "_" + ((Body)i).ToString();
            bones.Add(landmarkData);
        }
        Destroy(_bonePrefab.gameObject);
        MapBones();
    }

    private void MapBones()
    {
        head = avatar.GetBoneTransform(HumanBodyBones.Head);
        hip = avatar.GetBoneTransform(HumanBodyBones.Hips);

        //right
        BoneMapper boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.RightUpperArm), avatar.GetBoneTransform(HumanBodyBones.RightLowerArm));
        boneMapper.refParent = bones[(int)Body.RIGHT_SHOULDER];
        boneMapper.refChild = bones[(int)Body.RIGHT_ELBOW];

        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.RightLowerArm), avatar.GetBoneTransform(HumanBodyBones.RightHand));
        boneMapper.refParent = bones[(int)Body.RIGHT_ELBOW];
        boneMapper.refChild = bones[(int)Body.RIGHT_WRIST];
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.RightUpperLeg), avatar.GetBoneTransform(HumanBodyBones.RightLowerLeg));
        boneMapper.refParent = bones[(int)Body.RIGHT_HIP];
        boneMapper.refChild = bones[(int)Body.RIGHT_KNEE];
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.RightLowerLeg), avatar.GetBoneTransform(HumanBodyBones.RightFoot));
        boneMapper.refParent = bones[(int)Body.RIGHT_KNEE];
        boneMapper.refChild = bones[(int)Body.RIGHT_ANKLE];
        boneMappers.Add(boneMapper);


        //left
        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm), avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm));
        boneMapper.refParent = bones[(int)Body.LEFT_SHOULDER];
        boneMapper.refChild = bones[(int)Body.LEFT_ELBOW];
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm), avatar.GetBoneTransform(HumanBodyBones.LeftHand));
        boneMapper.refParent = bones[(int)Body.LEFT_ELBOW];
        boneMapper.refChild = bones[(int)Body.LEFT_WRIST];
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.LeftUpperLeg), avatar.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
        boneMapper.refParent = bones[(int)Body.LEFT_HIP];
        boneMapper.refChild = bones[(int)Body.LEFT_KNEE];
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.LeftLowerLeg), avatar.GetBoneTransform(HumanBodyBones.LeftFoot));
        boneMapper.refParent = bones[(int)Body.LEFT_KNEE];
        boneMapper.refChild = bones[(int)Body.LEFT_ANKLE];
        boneMappers.Add(boneMapper);

        //middle
        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.Spine), avatar.GetBoneTransform(HumanBodyBones.Neck));
        boneMapper.refParent = virtualHip;
        boneMapper.refChild = virtualNeck;
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.Neck), avatar.GetBoneTransform(HumanBodyBones.Head));
        boneMapper.refParent = virtualNeck;
        boneMapper.refChild = bones[(int)Body.NOSE];
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.LeftFoot), avatar.GetBoneTransform(HumanBodyBones.Neck));
        boneMapper.refParent = bones[(int)Body.LEFT_ANKLE]; ;
        boneMapper.refChild = bones[(int)Body.LEFT_INDEX]; ;
        boneMappers.Add(boneMapper);

        boneMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.RightFoot), avatar.GetBoneTransform(HumanBodyBones.Neck));
        boneMapper.refParent = bones[(int)Body.RIGHT_ANKLE];
        boneMapper.refChild = bones[(int)Body.RIGHT_INDEX];
        boneMappers.Add(boneMapper);


    }

    void Update()
    {
        if (!isInitialize) return;
        ConsumePendingPoseResult();
        trackedPlayerIndex = Bodylink.Instance.bodylinkAvatar.stablePlayerIndex[playerIndex];

        OnPoseUpdate();
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
        }
    }

    public void OnPoseUpdate()
    {
        if (!isTracking())
        {
            return;
        }

        try
        {
            if (poseLandmarkerResult.poseWorldLandmarks.Count <= playerIndex) return;

            for (int i = 0; i < _numberOfBones; i++)
            {
                var position = new Vector3(-poseLandmarkerResult.poseWorldLandmarks[trackedPlayerIndex].landmarks[i].x, -poseLandmarkerResult.poseWorldLandmarks[trackedPlayerIndex].landmarks[i].y, -poseLandmarkerResult.poseWorldLandmarks[trackedPlayerIndex].landmarks[i].z);
                if (i == (int)Body.NOSE) position.z *= .3f;
                else position.z *= .9f;
                bones[i].visibility = (float)poseLandmarkerResult.poseWorldLandmarks[trackedPlayerIndex].landmarks[i].visibility;
                bones[i].transform.position = position;
            }

            float _roiWidth = GetPlayerHeight(trackedPlayerIndex);
            if (_roiWidth == 0) _roiWidth = 1;

            virtualHip.transform.position = (bones[(int)Body.LEFT_HIP].transform.position + bones[(int)Body.RIGHT_HIP].transform.position) / 2;
            virtualNeck.transform.position = (bones[(int)Body.LEFT_SHOULDER].transform.position + bones[(int)Body.RIGHT_SHOULDER].transform.position) / 2;
            virtualNeck.transform.position = new Vector3(virtualNeck.transform.position.x, virtualNeck.transform.position.y, virtualNeck.transform.position.z * .0f);
            virtualHip.visibility = bones[(int)Body.LEFT_HIP].visibility;
            virtualNeck.visibility = bones[(int)Body.LEFT_SHOULDER].visibility;
        }
        catch (Exception e)
        {
            Debug.Log(playerIndex + " : " + e.ToString());
        }

    }
    private void LateUpdate()
    {
        if (!isInitialize) return;
        if (!isTracking())
        {
            return;
        }
        foreach (BoneMapper boneMapper in boneMappers)
        {
            boneMapper.parent.localRotation = boneMapper.initialRotation;
        }
        FixHipRotation();
        SolvePoseTracking();
        FixHeadRotation();
        if (!inPlace) Move();
        if (scaleMatch) FixScale();

    }
    private void SolvePoseTracking()
    {
        foreach (BoneMapper boneMapper in boneMappers)
        {
            if (boneMapper.refChild.visibility < visibilityThreshold) continue;
            Transform refParent = boneMapper.refParent.transform;       // mocap upper arm
            Transform refChild = boneMapper.refChild.transform;         // mocap hand
            Transform avatarParentBone = boneMapper.parent;   // avatar upper arm
            Transform avatarChildBone = boneMapper.child;     // avatar hand

            if (refParent == null || refChild == null || avatarParentBone == null || avatarChildBone == null)
                continue;

            // Get current avatar limb direction in local space of the parent bone
            Vector3 vOld = (avatarChildBone.position - avatarParentBone.position).normalized;
            vOld = avatarParentBone.InverseTransformDirection(vOld);

            // Get reference limb direction (from mocap) in local space of the parent bone
            Vector3 vNew = (refChild.position - refParent.position).normalized;
            vNew = avatarParentBone.InverseTransformDirection(vNew);

            if (vOld.sqrMagnitude < 0.0001f || vNew.sqrMagnitude < 0.0001f)
                continue;

            Quaternion rotOld = boneMapper.previousRotation;
            Quaternion rotNew = avatarParentBone.localRotation * Quaternion.FromToRotation(vOld, vNew).normalized;
            avatarParentBone.localRotation = Quaternion.Slerp(rotOld, rotNew, smoothness);
            boneMapper.previousRotation = avatarParentBone.localRotation;
        }

    }

    private void FixHipRotation()
    {
        try
        {
            float zDiff = bones[(int)Body.RIGHT_SHOULDER].transform.position.z - bones[(int)Body.LEFT_SHOULDER].transform.position.z;
            zDiff = Mathf.Clamp(zDiff, -20, 20);
            zDiff = Mathf.Abs(zDiff) > .02f ? zDiff - .02f * (zDiff / Mathf.Abs(zDiff)) : 0;
            float yAngle = zDiff * 300;

            hip.rotation = Quaternion.Lerp(hip.rotation, Quaternion.Euler(hip.eulerAngles.x, -yAngle, hip.eulerAngles.z), smoothness);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }

    }
    private void FixHeadRotation()
    {
        try
        {
            float zDiff = bones[(int)Body.RIGHT_EYE].transform.position.z - bones[(int)Body.LEFT_EYE].transform.position.z;
            float yAngle = zDiff * 3000;
            yAngle = Mathf.Clamp(yAngle, -90, 90);
            yAngle = Mathf.Abs(yAngle) < 5 ? 0 : yAngle;
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(head.localEulerAngles.x, -yAngle, head.localEulerAngles.z), smoothness);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }

    }
    private void Move()
    {
        try
        {
            var leftHipPosition = screen.rect.GetPoint(poseLandmarkerResult.poseLandmarks[trackedPlayerIndex].landmarks[(int)Body.LEFT_HIP], 0, false);
            var rightHipPosition = screen.rect.GetPoint(poseLandmarkerResult.poseLandmarks[trackedPlayerIndex].landmarks[(int)Body.RIGHT_HIP], 0, false);
            leftHipPosition = screen.TransformPoint(leftHipPosition);
            rightHipPosition = screen.TransformPoint(rightHipPosition);
            hipPosition = (leftHipPosition + rightHipPosition) / 2;
            hipPosition.z = hip.position.z;
            hip.position = hipPosition;
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }
    private void FixScale()
    {
        Vector3 newScale = Vector3.one * (_roiWidth * scaleFactor);
        hip.localScale = Vector3.Lerp(hip.localScale, newScale, Time.deltaTime * 10);
    }
    private bool isTracking()
    {
        if (poseLandmarkerResult.poseWorldLandmarks == null)
        {
            return false;
        }
        return true;
    }

    private float GetPlayerHeight(int playerIndex)
    {
        try
        {
            float noseY = poseLandmarkerResult.poseWorldLandmarks[playerIndex].landmarks[(int)Body.NOSE].y;
            float ankleY = (poseLandmarkerResult.poseWorldLandmarks[playerIndex].landmarks[(int)Body.LEFT_ANKLE].y + poseLandmarkerResult.poseWorldLandmarks[playerIndex].landmarks[(int)Body.RIGHT_ANKLE].y) / 2;
            return Mathf.Abs(noseY - ankleY);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return 0;
        }
    }

    public class BoneMapper
    {
        public Transform parent;
        public Transform child;
        public LandmarkData refParent;
        public LandmarkData refChild;
        public Transform hint;
        public Quaternion initialRotation;
        public Quaternion previousRotation;

        public int hip;
        public BoneMapper(Transform parent, Transform child)
        {
            this.parent = parent;
            this.child = child;
            initialRotation = parent.localRotation;
            previousRotation = initialRotation;

        }

    }
    public class LandmarkData
    {
        public Transform transform;
        public float visibility;

    }
    public enum Body
    {
        NOSE,
        LEFT_EYE_INNER,
        LEFT_EYE,
        LEFT_EYE_OUTER,
        RIGHT_EYE_INNER,
        RIGHT_EYE,
        RIGHT_EYE_OUTER,
        LEFT_EAR,
        RIGHT_EAR,
        MOUTH_LEFT,
        MOUTH_RIGHT,
        LEFT_SHOULDER,
        RIGHT_SHOULDER,
        LEFT_ELBOW,
        RIGHT_ELBOW,
        LEFT_WRIST,
        RIGHT_WRIST,
        LEFT_PINKY,
        RIGHT_PINKY,
        LEFT_INDEX,
        RIGHT_INDEX,
        LEFT_THUMB,
        RIGHT_THUMB,
        LEFT_HIP,
        RIGHT_HIP,
        LEFT_KNEE,
        RIGHT_KNEE,
        LEFT_ANKLE,
        RIGHT_ANKLE,
        LEFT_HEEL,
        RIGHT_HEEL,
        LEFT_FOOT_INDEX,
        RIGHT_FOOT_INDEX
    }
}

