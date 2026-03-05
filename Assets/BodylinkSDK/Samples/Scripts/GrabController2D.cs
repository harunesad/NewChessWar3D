using System.Collections;
using BodylinkSDK;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

public class GrabController2D : MonoBehaviour
{
    [Header("Smooth Follow")]
    [SerializeField] private bool useSmoothFollow = false;
    [Header("Movement")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float smoothSpeed = 15f;
    [SerializeField] private float pullSpeed = 12f;

    [Header("Throw")]
    [SerializeField] private bool canThrow = true;
    [SerializeField] private float throwStrength = 1.2f;
    [SerializeField] private float maxThrowDistance = 2.5f;
    [SerializeField] private float throwDuration = 0.25f;

    [Header("Colors")]
    [SerializeField] private bool changeColorOnGrab = true;
    private Color hoverColor = Color.green;
    private Color grabColor = Color.red;
    private Color normalColor = Color.white;

    [Header("Grabber Tag")]
    public string grabberTag = "Grabber";

    // State
    private bool isGrabbed;
    private bool isThrowing;
    private bool isTriggered;

    private Side lockedHand;
    private bool handLocked;

    // Objects
    private GameObject grabbedObject;
    private GameObject throwingObject;
    private Collectable collectable;
    private SpriteRenderer targetRenderer;

    // Velocity
    private Vector2 lastHandPos;
    private Vector2 handVelocity;
    private bool hasLastHandPos;

    // Throw
    private Vector2 throwStartPos;
    private Vector2 throwVelocity;
    private float throwTimer;

    private Bodylink bodylink;

    IEnumerator Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        yield return new WaitForEndOfFrame();

        bodylink = Bodylink.Instance;
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
        // Ignore other hand if locked
        if (handLocked && side != lockedHand)
            return;

        if (pose == HandPose.Closed_Fist)
        {
            if (isTriggered && !isGrabbed && grabbedObject)
            {
                lockedHand = side;
                handLocked = true;
                BeginGrab();
            }
        }
        else if (pose == HandPose.Open_Palm)
        {
            if (isGrabbed && side == lockedHand)
                BeginThrow();
        }
    }

    // ================= GRAB =================

    private void BeginGrab()
    {
        isGrabbed = true;
        isThrowing = false;

        if (collectable)
            collectable.isGrabbed = true;

        if (changeColorOnGrab && targetRenderer)
            targetRenderer.color = grabColor;

        hasLastHandPos = false;
    }

    // ================= THROW =================

    private void BeginThrow()
    {
        isGrabbed = false;
        isThrowing = true;

        if (collectable)
        {
            collectable.isGrabbed = false;
            collectable = null;
        }

        if (changeColorOnGrab && targetRenderer)
            targetRenderer.color = normalColor;

        throwingObject = grabbedObject;
        grabbedObject = null;
        targetRenderer = null;

        throwStartPos = throwingObject.transform.position;
        throwVelocity = handVelocity * throwStrength;
        throwTimer = 0f;

        handLocked = false;
    }

    // ================= TRIGGERS =================

    void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag(grabberTag) || isGrabbed) return;

        isTriggered = true;
        grabbedObject = col.gameObject;
        collectable = grabbedObject.GetComponent<Collectable>();
        targetRenderer = grabbedObject.GetComponent<SpriteRenderer>();

        if (changeColorOnGrab && targetRenderer)
            targetRenderer.color = hoverColor;
    }

    void OnTriggerExit2D(Collider2D col)
    {
        if (!col.CompareTag(grabberTag) || isGrabbed) return;

        isTriggered = false;

        if (changeColorOnGrab && targetRenderer)
            targetRenderer.color = normalColor;

        grabbedObject = null;
        collectable = null;
        targetRenderer = null;
    }

    // ================= UPDATE =================

    void Update()
    {
        UpdateHandPosition();

        Vector2 handPos = transform.position;

        // Velocity
        if (!hasLastHandPos)
        {
            lastHandPos = handPos;
            hasLastHandPos = true;
        }

        handVelocity = (handPos - lastHandPos) / Mathf.Max(Time.deltaTime, 0.001f);
        lastHandPos = handPos;

        // Pull
        if (isGrabbed && grabbedObject)
        {
            grabbedObject.transform.position = Vector2.Lerp(
                grabbedObject.transform.position,
                handPos,
                Time.deltaTime * pullSpeed
            );
        }

        // Throw
        if (canThrow && isThrowing && throwingObject)
        {
            throwTimer += Time.deltaTime;
            Vector2 targetPos = throwStartPos + throwVelocity * (throwTimer / throwDuration);

            throwingObject.transform.position = Vector2.Lerp(
                throwingObject.transform.position,
                targetPos,
                Time.deltaTime * 12f
            );

            if (throwTimer >= throwDuration ||
                Vector2.Distance(throwStartPos, throwingObject.transform.position) > maxThrowDistance)
            {
                isThrowing = false;
                throwingObject = null;
            }
        }
    }

    // ================= HAND FOLLOW =================

    private void UpdateHandPosition()
    {
        if (bodylink == null || !bodylink.IsInitialized) return;

        NormalizedLandmark wrist;

        if (handLocked)
        {
            wrist = lockedHand == Side.Left
            ? (useSmoothFollow ? bodylink.players[0].body2DSmoothed.leftWrist : bodylink.players[0].body2D.leftWrist)
            : (useSmoothFollow ? bodylink.players[0].body2DSmoothed.rightWrist : bodylink.players[0].body2D.rightWrist);
        }
        else
        {
            // Follow any visible hand (prefer right)
            var right = useSmoothFollow ? bodylink.players[0].body2DSmoothed.rightWrist : bodylink.players[0].body2D.rightWrist;
            var left = useSmoothFollow ? bodylink.players[0].body2DSmoothed.leftWrist : bodylink.players[0].body2D.leftWrist;
            wrist = right.visibility > 0.5f ? right : left;
        }

        Vector2 normalized = new Vector2(wrist.x, wrist.y);
        Vector2 screenPos = new Vector2(
            normalized.x * Screen.width,
            normalized.y * Screen.height
        );

        float depth = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, depth)
        );

        worldPos.z = 0;

        transform.position = Vector3.Lerp(
            transform.position,
            worldPos,
            Time.deltaTime * smoothSpeed
        );
    }
}
