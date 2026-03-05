using BodylinkSDK;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

public class GrabController3D : MonoBehaviour
{
    [Header("Smooth Follow")]
    [SerializeField] private bool useSmoothFollow = false;
    [Header("References")]
    [SerializeField] private Camera mainCamera;

    [Header("Raycast")]
    [SerializeField] private LayerMask surfaceLayers;
    [SerializeField] private float maxDistance = 1000f;

    [Header("Movement")]
    [SerializeField] private bool xyMovement;
    [SerializeField] private int maxHeight = 5;
    [SerializeField] private float followSpeed = 15f;

    [Header("Throw")]
    [SerializeField] private bool canThrow = true;
    [SerializeField] private float throwMultiplier = 1.2f;
    [SerializeField] private float upwardBias = 0.3f;

    // State
    private bool isGrabbed;
    private bool handLocked;
    private Side lockedHand;

    // Objects
    private GameObject grabbedObject;
    private Rigidbody grabbedRB;
    private Collectable collectable;

    // Hand tracking
    private Vector3 lastHandWorldPos;
    private Vector3 handVelocity;
    private bool hasLastHandPos;

    private RaycastHit raycastHit;
    private Bodylink bodylink;
    private Vector3 initialPos;

    void Start()
    {
        bodylink = Bodylink.Instance;
        initialPos = transform.position;

        bodylink.inputEvents.OnPoseDetection += HandleHandPose;
    }

    void OnDisable()
    {
        if (Bodylink.Instance != null)
            Bodylink.Instance.inputEvents.OnPoseDetection -= HandleHandPose;
    }

    // ================= HAND POSES =================

    private void HandleHandPose(int player, string poseName, Side side, HandPose pose)
    {
        // Ignore other hand once locked
        if (handLocked && side != lockedHand)
            return;

        if (pose == HandPose.Closed_Fist && !isGrabbed)
        {
            lockedHand = side;
            handLocked = true;
            TryGrab();
        }
        else if (pose == HandPose.Open_Palm && isGrabbed && side == lockedHand)
        {
            Release();
        }
    }

    // ================= UPDATE =================

    void Update()
    {
        if (!bodylink || !bodylink.IsInitialized)
            return;

        UpdateHandWorldPosition();

        // Velocity (stable)
        if (!hasLastHandPos)
        {
            lastHandWorldPos = transform.position;
            hasLastHandPos = true;
        }

        handVelocity = (transform.position - lastHandWorldPos) / Mathf.Max(Time.deltaTime, 0.001f);
        lastHandWorldPos = transform.position;

        // Move grabbed object
        if (isGrabbed && grabbedObject)
        {
            Vector3 target = transform.position;
            if (xyMovement)
                target.z = grabbedObject.transform.position.z;

            grabbedObject.transform.position =
                Vector3.Lerp(grabbedObject.transform.position, target, followSpeed * Time.deltaTime);
        }
    }

    // ================= HAND POSITION =================

    private void UpdateHandWorldPosition()
    {
        NormalizedLandmark wrist;

        if (handLocked)
        {
            wrist = lockedHand == Side.Left
            ? (useSmoothFollow ? bodylink.players[0].body2DSmoothed.leftWrist : bodylink.players[0].body2D.leftWrist)
            : (useSmoothFollow ? bodylink.players[0].body2DSmoothed.rightWrist : bodylink.players[0].body2D.rightWrist);
        }
        else
        {
            // Prefer right hand, fallback left
            var right = useSmoothFollow ? bodylink.players[0].body2DSmoothed.rightWrist : bodylink.players[0].body2D.rightWrist;
            var left = useSmoothFollow ? bodylink.players[0].body2DSmoothed.leftWrist : bodylink.players[0].body2D.leftWrist;
            wrist = right.visibility > 0.5f ? right : left;
        }

        Vector2 screenPos = new Vector2(wrist.x * Screen.width, wrist.y * Screen.height);
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out raycastHit, maxDistance, surfaceLayers))
        {
            Vector3 pos = raycastHit.point;

            if (xyMovement)
            {
                pos.z = initialPos.z;
                pos.y = Mathf.Clamp(pos.y, initialPos.y, initialPos.y + maxHeight);
            }
            else
            {
                pos += Vector3.up * (isGrabbed ? 5f : 0.15f);
            }

            transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime * followSpeed);
        }
    }

    // ================= GRAB =================

    private void TryGrab()
    {
        if (raycastHit.transform == null || isGrabbed)
            return;

        if (raycastHit.transform.gameObject.name == "Ground")
            return;

        collectable = raycastHit.transform.GetComponent<Collectable>();
        if (!collectable)
            return;

        grabbedObject = raycastHit.transform.gameObject;
        grabbedRB = grabbedObject.GetComponent<Rigidbody>();

        grabbedObject.GetComponent<Collider>().enabled = false;
        grabbedRB.isKinematic = true;

        collectable.isGrabbed = true;
        isGrabbed = true;

        hasLastHandPos = false;
    }

    // ================= RELEASE =================

    private void Release()
    {
        if (!grabbedObject)
            return;

        grabbedObject.GetComponent<Collider>().enabled = true;
        grabbedRB.isKinematic = false;

        if (canThrow)
        {
            Vector3 throwDir = handVelocity + Vector3.up * upwardBias;
            grabbedRB.AddForce(throwDir * throwMultiplier, ForceMode.Impulse);
        }

        collectable.isGrabbed = false;
        collectable = null;
        grabbedObject = null;

        isGrabbed = false;
        handLocked = false;
    }
}
