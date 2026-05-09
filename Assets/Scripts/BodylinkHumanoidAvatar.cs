using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BodylinkSDK;

[RequireComponent(typeof(Animator))]
public class BodylinkHumanoidAvatar : MonoBehaviour
{
    public Animator animator;
    
    [Header("Movement Settings")]
    public float moveSensitivityX = 25f;
    public float moveSensitivityZ = 55f;
    public float positionSmoothTime = 0.15f;
    public float globalMovementScale = 1.5f; // Yeni: Tüm hareketleri oranlamak için
    public Vector3 offset = Vector3.zero;
    public bool mirrorPositionX = false;
    public bool mirrorPositionZ = false;
    public bool disableAnimatorComponent = true;

    [Header("Boundary Settings")]
    public bool useBounds = true;
    public Vector3 minBounds = new Vector3(-5, 0, -5);
    public Vector3 maxBounds = new Vector3(5, 0, 5);

    [System.Serializable]
    public struct RotationSettings {
        public bool mirrorX;
        public bool mirrorY;
        public bool mirrorZ;
        public float smoothTime;
    }

    [Header("Multiplayer Settings")]
    public int playerIndex = 0; // 0 veya 1
    public bool isTurnActive = true;
    public Transform cameraAnchor; // Kameranın takip edeceği nokta

    [Header("Mirroring & Smoothness")]
    public bool swapHands = true;
    public float rotationOffset = 0f; // Yeni: Başlangıç yönü ofseti (0 veya 180)
    public RotationSettings armSettings = new RotationSettings { mirrorX = true, mirrorY = false, mirrorZ = true, smoothTime = 0.3f };
    public RotationSettings legSettings = new RotationSettings { mirrorX = true, mirrorY = false, mirrorZ = true, smoothTime = 0.3f };

    [Header("UI Controls")]
    [SerializeField] private Button resetUIButton;

    [Header("FPP Camera Settings")]
    public Camera fppCamera;
    public Vector3 cameraOffset = new Vector3(0, 0.15f, 0.1f);
    public Vector3 cameraRotationOffset = Vector3.zero; // Yeni: Kamera açısı için

    private string debugStatus = "Bodylink Bekleniyor...";
    private Vector3 initialUserPos;
    private bool isCalibrated = false;
    private Mediapipe.Tasks.Vision.PoseLandmarker.PoseLandmarkerResult lastResult;
    
    private Dictionary<HumanBodyBones, Quaternion> initialRotations = new Dictionary<HumanBodyBones, Quaternion>();
    private Dictionary<HumanBodyBones, Vector3> initialDirs = new Dictionary<HumanBodyBones, Vector3>();
    private Vector3 currentVelocity;

    // Yumrukla Dönüş Değişkenleri
    private bool isLeftFist = false;
    private bool isRightFist = false;
    private float targetYaw = 0f;
    private float currentYaw = 0f;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        
        if (resetUIButton != null) {
            resetUIButton.onClick.AddListener(DeepReset);
        }

        DeepReset();

        if (BodylinkSDK.Bodylink.Instance != null)
        {
            BodylinkSDK.Bodylink.Instance.OnInitialized += SubscribeToData;
            if (BodylinkSDK.Bodylink.Instance.IsInitialized) SubscribeToData();
            BodylinkSDK.Bodylink.Instance.inputEvents.OnPoseDetection += HandleHandPose;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) DeepReset();
    }

    public void DeepReset() {
        isCalibrated = false;
        debugStatus = "Kalibre Ediliyor...";
        transform.position = offset;
        transform.rotation = Quaternion.identity;
        currentVelocity = Vector3.zero;
        targetYaw = rotationOffset;
        currentYaw = rotationOffset;

        if (animator != null) {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0); 
        }
        SaveInitialPose();
    }

    void SaveInitialPose()
    {
        HumanBodyBones[] bones = {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg
        };

        initialRotations.Clear();
        initialDirs.Clear();

        foreach (var bone in bones)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null)
            {
                initialRotations[bone] = t.rotation;
                Transform child = GetBoneChild(bone);
                if (child != null)
                    initialDirs[bone] = (t.InverseTransformPoint(child.position)).normalized;
                else
                    initialDirs[bone] = Vector3.forward;
            }
        }
    }

    Transform GetBoneChild(HumanBodyBones bone)
    {
        if (bone == HumanBodyBones.LeftUpperArm) return animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        if (bone == HumanBodyBones.LeftLowerArm) return animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (bone == HumanBodyBones.RightUpperArm) return animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        if (bone == HumanBodyBones.RightLowerArm) return animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (bone == HumanBodyBones.LeftUpperLeg) return animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        if (bone == HumanBodyBones.LeftLowerLeg) return animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        if (bone == HumanBodyBones.RightUpperLeg) return animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        if (bone == HumanBodyBones.RightLowerLeg) return animator.GetBoneTransform(HumanBodyBones.RightFoot);
        return null;
    }

    void HandleHandPose(int playerIndex, string gestureName, BodylinkSDK.Side side, BodylinkSDK.HandPose pose)
    {
        // Sadece kendi playerIndex'ine ait ve sırası kendisindeyken tepki ver
        if (playerIndex != this.playerIndex || !isTurnActive) return;

        bool isFist = pose == BodylinkSDK.HandPose.Closed_Fist;
        BodylinkSDK.Side actualSide = side;
        if (swapHands) actualSide = (side == BodylinkSDK.Side.Left) ? BodylinkSDK.Side.Right : BodylinkSDK.Side.Left;

        // TERSLEME: Eğer sağ el çalışmıyorsa, ayna görüntüsü yüzünden diğer tarafı kontrol ediyoruz
        if (actualSide == BodylinkSDK.Side.Left) {
            isRightFist = isFist;
            targetYaw = rotationOffset + (isRightFist ? 180f : 0f);
        }
    }

    void SubscribeToData()
    {
        if (BodylinkSDK.Bodylink.Instance.PoseLandmarkerRunnerInstance != null)
        {
            BodylinkSDK.Bodylink.Instance.PoseLandmarkerRunnerInstance.onPoseLandmarkDetectionResult += OnDataReceived;
            debugStatus = "Tracking Active ✅";
        }
    }

    void OnDataReceived(Mediapipe.Tasks.Vision.PoseLandmarker.PoseLandmarkerResult result, long timestamp)
    {
        lastResult = result;
    }

    Vector3 GetSafeWorldLandmark(int index, RotationSettings settings) {
        if (lastResult.poseWorldLandmarks == null || lastResult.poseWorldLandmarks.Count == 0) return Vector3.zero;
        var list = lastResult.poseWorldLandmarks[0].landmarks;
        if (index < 0 || index >= list.Count) return Vector3.zero;
        float mx = settings.mirrorX ? -1 : 1;
        float my = settings.mirrorY ? 1 : -1;
        float mz = settings.mirrorZ ? -1 : 1;
        return new Vector3(list[index].x * mx, list[index].y * my, list[index].z * mz);
    }

    Vector3 GetSafeScreenLandmark(int index) {
        if (lastResult.poseLandmarks == null || lastResult.poseLandmarks.Count == 0) return Vector3.zero;
        var list = lastResult.poseLandmarks[0].landmarks;
        if (index < 0 || index >= list.Count) return Vector3.zero;
        return new Vector3(list[index].x, list[index].y, list[index].z);
    }

    void LateUpdate()
    {
        var bodylink = BodylinkSDK.Bodylink.Instance;
        if (bodylink == null || !bodylink.IsInitialized || bodylink.players == null || bodylink.players.Length == 0) return;
        if (playerIndex >= bodylink.players.Length) return;
        var player = bodylink.players[playerIndex];

        if (disableAnimatorComponent && animator.enabled) animator.enabled = false;

        // 1. Pozisyon ve Dönüş (Sadece Sırası Gelen İçin)
        if (isTurnActive)
        {
            var hipL = player.body2DSmoothed.leftHip;
            var hipR = player.body2DSmoothed.rightHip;
            var sL = player.body2DSmoothed.leftShoulder;
            var sR = player.body2DSmoothed.rightShoulder;

            float userX = (hipL.x + hipR.x) / 2f;
            float torsoHeight = Vector2.Distance(new Vector2(sL.x, sL.y), new Vector2(sR.x, sR.y));

            if (!isCalibrated) {
                initialUserPos = new Vector3(userX, 0, torsoHeight);
                isCalibrated = true;
            }

            float deltaX = (userX - initialUserPos.x) * moveSensitivityX * globalMovementScale;
            float deltaZ = (torsoHeight - initialUserPos.z) * moveSensitivityZ * globalMovementScale;
            
            if (mirrorPositionX) deltaX *= -1;
            if (mirrorPositionZ) deltaZ *= -1;
            
            if (Mathf.Abs(currentYaw) > 90f) { deltaZ *= -1; deltaX *= -1; }

            Vector3 targetPos = offset + new Vector3(deltaX, 0, deltaZ);
            if (useBounds) {
                targetPos.x = Mathf.Clamp(targetPos.x, minBounds.x, maxBounds.x);
                targetPos.z = Mathf.Clamp(targetPos.z, minBounds.z, maxBounds.z);
            }

            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, positionSmoothTime);

            // --- KARAKTER DÖNÜŞÜ ---
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, 0.15f);
            transform.rotation = Quaternion.Euler(0, currentYaw, 0);
        }

        // 2. Kemik Rotasyonları (HER ZAMAN - İki oyuncu da kollarını hareket ettirebilsin)
        int L_S = swapHands ? 12 : 11; int R_S = swapHands ? 11 : 12;
        int L_E = swapHands ? 14 : 13; int R_E = swapHands ? 13 : 14;
        int L_W = swapHands ? 16 : 15; int R_W = swapHands ? 15 : 16;

        ApplyRot(HumanBodyBones.LeftUpperArm, GetSafeWorldLandmark(L_S, armSettings), GetSafeWorldLandmark(L_E, armSettings), armSettings);
        ApplyRot(HumanBodyBones.LeftLowerArm, GetSafeWorldLandmark(L_E, armSettings), GetSafeWorldLandmark(L_W, armSettings), armSettings);
        ApplyRot(HumanBodyBones.RightUpperArm, GetSafeWorldLandmark(R_S, armSettings), GetSafeWorldLandmark(R_E, armSettings), armSettings);
        ApplyRot(HumanBodyBones.RightLowerArm, GetSafeWorldLandmark(R_E, armSettings), GetSafeWorldLandmark(R_W, armSettings), armSettings);

        int L_H = swapHands ? 24 : 23; int R_H = swapHands ? 23 : 24;
        int L_K = swapHands ? 26 : 25; int R_K = swapHands ? 25 : 26;
        int L_A = swapHands ? 28 : 27; int R_A = swapHands ? 27 : 28;

        ApplyRot(HumanBodyBones.LeftUpperLeg, GetSafeWorldLandmark(L_H, legSettings), GetSafeWorldLandmark(L_K, legSettings), legSettings);
        ApplyRot(HumanBodyBones.LeftLowerLeg, GetSafeWorldLandmark(L_K, legSettings), GetSafeWorldLandmark(L_A, legSettings), legSettings);
        ApplyRot(HumanBodyBones.RightUpperLeg, GetSafeWorldLandmark(R_H, legSettings), GetSafeWorldLandmark(R_K, legSettings), legSettings);
        ApplyRot(HumanBodyBones.RightLowerLeg, GetSafeWorldLandmark(R_K, legSettings), GetSafeWorldLandmark(R_A, legSettings), legSettings);

        // 3. Kamera Takibi (Dinamik Ofsetli)
        if (fppCamera != null) {
            fppCamera.transform.position = transform.position + transform.TransformDirection(cameraOffset);
            fppCamera.transform.rotation = transform.rotation * Quaternion.Euler(cameraRotationOffset);
        }
    }

    void ApplyRot(HumanBodyBones bone, Vector3 start, Vector3 end, RotationSettings settings) {
        if (start == Vector3.zero || end == Vector3.zero) return;
        Transform t = animator.GetBoneTransform(bone);
        if (t == null || !initialDirs.ContainsKey(bone)) return;

        Vector3 dir = (end - start).normalized;

        if (Mathf.Abs(currentYaw) > 90f) {
            dir.x *= -1;
            dir.z *= -1;
        }

        Quaternion tRot = Quaternion.FromToRotation(t.TransformDirection(initialDirs[bone]), dir) * t.rotation;
        t.rotation = Quaternion.Slerp(t.rotation, tRot, settings.smoothTime);
    }

    void OnGUI() {
        GUIStyle style = new GUIStyle(); style.fontSize = 25; style.normal.textColor = Color.green;
        GUI.Label(new Rect(50, 10, 500, 50), debugStatus, style);
    }
}
