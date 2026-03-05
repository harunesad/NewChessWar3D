using UnityEngine;
using BodylinkSDK;
using System;
using UnityEngine.UI;
public class CustomGestureEventExample : BodylinkBaseGestureDetector
{
    private Transform handTransform;

    [Header("Wave Settings")]
    public float minWaveSpeed = 0.15f;
    public float minWaveAmplitude = 0.04f;
    public int directionChangeThreshold = 3;
    public float detectionWindow = 0.6f;

    [Header("Output (Read Only)")]
    public bool IsWaving;
    public float WaveStrength;
    public float WaveSpanNormalized;

    private Vector3 lastPosition;
    private float lastXVelocity;

    private float timer;
    private int directionChanges;
    private float accumulatedAmplitude;
    private float minX = float.MaxValue;
    private float maxX = float.MinValue;
    private float referenceBodyHeight = -1f;
    private float lastHeightRatio = 1f;
    private bool hasLastPosition;

    public Slider waveSlider;

    void Start()
    {
        Bodylink.Instance.inputEvents.OnGestureDetection += (playerIndex, name, values) =>
        {
            waveSlider.value = (float)values[0] / 100f;
        };
    }
    void Update()
    {
        try
        {
            handTransform = Bodylink.Instance.bodyPoints2DGameObject.rightWrist[0].transform;
            if (!hasLastPosition && handTransform != null)
            {
                lastPosition = handTransform.position;
                hasLastPosition = true;
            }
            CheckHandWave();
        }
        catch (Exception e) { }
    }

    public void CheckHandWave()
    {
        if (handTransform == null) return;
        if (!hasLastPosition) return;

        timer += Time.deltaTime;

        lastHeightRatio = GetHeightRatioFromBodyPoints();

        Vector3 currentPos = handTransform.position;
        float deltaX = currentPos.x - lastPosition.x;
        float velocityX = deltaX / Mathf.Max(Time.deltaTime, 0.0001f);

        minX = Mathf.Min(minX, currentPos.x);
        maxX = Mathf.Max(maxX, currentPos.x);

        // Check direction flip
        if (Mathf.Sign(velocityX) != Mathf.Sign(lastXVelocity) &&
            Mathf.Abs(velocityX) > minWaveSpeed)
        {
            directionChanges++;
        }

        accumulatedAmplitude += Mathf.Abs(deltaX);

        // Evaluate window
        if (timer >= detectionWindow)
        {
            float waveSpan = Mathf.Max(0f, maxX - minX);
            float normalizedAmplitude = accumulatedAmplitude * lastHeightRatio;
            float normalizedSpan = waveSpan * lastHeightRatio;

            bool waveDetected = directionChanges >= directionChangeThreshold &&
                                normalizedSpan >= minWaveAmplitude;

            IsWaving = waveDetected;
            WaveSpanNormalized = normalizedSpan;

            // Normalize strength
            WaveStrength = waveDetected
                ? Mathf.Clamp01((normalizedSpan / detectionWindow) * directionChanges * 2f)
                : 0f;

            if (waveDetected)
            {
                // playerIndex 0 is used for this sample; extend as needed for multiplayer
                OnGestureDetect(0, "HandWave", normalizedSpan);
            }

            // Reset window
            timer = 0f;
            directionChanges = 0;
            accumulatedAmplitude = 0f;
            minX = float.MaxValue;
            maxX = float.MinValue;
        }

        lastXVelocity = velocityX;
        lastPosition = currentPos;
    }

    public override void ProcessGesture()
    {
    }

    private float GetHeightRatioFromBodyPoints()
    {
        try
        {
            var body = Bodylink.Instance.bodyPoints2DGameObject;
            var head = body.head[0];
            var leftFoot = body.leftFoot[0];
            var rightFoot = body.rightFoot[0];

            Transform headT = head != null ? head.transform : null;
            Transform footT = leftFoot != null ? leftFoot.transform : (rightFoot != null ? rightFoot.transform : null);
            if (headT == null || footT == null) return lastHeightRatio;

            float currentHeight = Vector3.Distance(headT.position, footT.position);
            if (currentHeight <= 0.0001f) return lastHeightRatio;

            if (referenceBodyHeight < 0f)
                referenceBodyHeight = currentHeight;

            return referenceBodyHeight / currentHeight;
        }
        catch (Exception)
        {
            return lastHeightRatio;
        }
    }
}
