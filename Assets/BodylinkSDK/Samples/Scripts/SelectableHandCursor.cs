using System;
using System.Collections.Generic;
using BodylinkSDK;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectableHandCursor : MonoBehaviour
{
    public enum HandSelectionMode
    {
        FirstDetected,
        LeftHand,
        RightHand
    }

    private struct HandCursorData
    {
        public List<NormalizedLandmark> Landmarks;
        public Side Side;
        public bool HasSide;
    }

    [Header("Hand Source")]
    [SerializeField] private int playerSlot;
    [SerializeField] private HandSelectionMode handSelectionMode = HandSelectionMode.FirstDetected;
    private int indexTipLandmark = 8;
    private int thumbTipLandmark = 4;

    [Header("Cursor Visual")]
    [Tooltip("Optional visual to move. If empty, this GameObject transform is moved.")]
    [SerializeField] private RectTransform cursorVisual;
    [SerializeField] private CanvasGroup cursorCanvasGroup;
    [SerializeField] private bool hideVisualWhenHandLost = true;
    private bool invertY = true;
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private Vector2 screenOffset;
    [SerializeField, Range(0f, 1f)] private float followLerp = 0.1f;
    [SerializeField] private float cursorDepth = -2f;
    [SerializeField, Min(0f)] private float idleScale = 1f;
    [SerializeField, Min(0f)] private float pinchScale = 0.3f;
    [SerializeField, Range(0f, 1f)] private float scaleLerp = 0.2f;

    [Header("Pinch")]
    [SerializeField] private bool sendUiEvents = true;
    private float pinchDownThreshold = 0.03f;
    private float pinchUpThreshold = 0.04f;
    private float pinchReleaseGraceTime = 0.1f;
    private float dragStartThreshold = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    private GameObject currentObject;
    private GameObject pressTarget;
    private GameObject dragTarget;
    private GameObject clickTarget;
    private RaycastResult pressRaycast;

    private Vector2 pressPosition;
    private Vector2 lastPointerPosition;
    private Vector2 lastScreenPosition;
    private float pinchLastSeenTime;
    private bool isPinching;
    private bool isDragging;
    private bool hasScreenPosition;
    private bool wasTracked;

    public HandSelectionMode SelectionMode
    {
        get => handSelectionMode;
        set => handSelectionMode = value;
    }

    public int PlayerSlot
    {
        get => playerSlot;
        set => playerSlot = Mathf.Max(0, value);
    }

    public bool IsTracked { get; private set; }
    public bool IsPinching => isPinching;
    public Vector2 ScreenPosition => lastScreenPosition;
    public Side TrackedSide { get; private set; }
    public bool HasTrackedSide { get; private set; }

    public event Action<SelectableHandCursor, bool> TrackingStateChanged;
    public event Action<SelectableHandCursor, bool> PinchStateChanged;

    private void Awake()
    {
        if (cursorVisual == null)
        {
            cursorVisual = transform as RectTransform;
        }

        if (cursorCanvasGroup == null && cursorVisual != null)
        {
            cursorCanvasGroup = cursorVisual.GetComponent<CanvasGroup>();
        }
    }

    private void OnValidate()
    {
        playerSlot = Mathf.Max(0, playerSlot);
        indexTipLandmark = Mathf.Max(0, indexTipLandmark);
        thumbTipLandmark = Mathf.Max(0, thumbTipLandmark);
        pinchDownThreshold = Mathf.Max(0f, pinchDownThreshold);
        pinchUpThreshold = Mathf.Max(pinchDownThreshold, pinchUpThreshold);
        pinchReleaseGraceTime = Mathf.Max(0f, pinchReleaseGraceTime);
        dragStartThreshold = Mathf.Max(0f, dragStartThreshold);
    }

    private void Update()
    {
        if (!TryGetSelectedHand(out HandCursorData handData) ||
            !TryGetScreenPosition(handData.Landmarks, out Vector2 screenPosition))
        {
            HandleTrackingLost();
            return;
        }

        IsTracked = true;
        TrackedSide = handData.Side;
        HasTrackedSide = handData.HasSide;
        hasScreenPosition = true;

        if (!wasTracked)
        {
            wasTracked = true;
            SetVisualVisible(true);
            TrackingStateChanged?.Invoke(this, true);
        }

        Vector2 cursorScreenPosition = MoveCursor(screenPosition);
        lastScreenPosition = cursorScreenPosition;

        if (sendUiEvents)
        {
            UpdateUiPointer(handData.Landmarks, cursorScreenPosition);
        }
        else
        {
            UpdatePinchFeedback(handData.Landmarks);
        }

        UpdateCursorScale();
    }

    private bool TryGetSelectedHand(out HandCursorData handData)
    {
        handData = default;

        switch (handSelectionMode)
        {
            case HandSelectionMode.RightHand:
                return TryGetDetectedHandBySide(Side.Left, out handData) || TryGetPlayerHand(Side.Left, out handData);
            case HandSelectionMode.LeftHand:
                return TryGetDetectedHandBySide(Side.Right, out handData) || TryGetPlayerHand(Side.Right, out handData);
            default:
                return TryGetFirstDetectedHand(out handData);
        }
    }

    private bool TryGetDetectedHandBySide(Side side, out HandCursorData handData)
    {
        handData = default;

        Bodylink bodylink = Bodylink.Instance;
        if (bodylink == null || !bodylink.IsInitialized || bodylink.bodylinkAvatar == null)
        {
            return false;
        }

        var result = bodylink.bodylinkAvatar.handLandmarkerResult;
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < result.handLandmarks.Count; i++)
        {
            if (!TryResolveHandSide(result.handedness, i, out Side detectedSide) || detectedSide != side)
            {
                continue;
            }

            List<NormalizedLandmark> landmarks = result.handLandmarks[i].landmarks;
            if (!HasRequiredLandmarks(landmarks))
            {
                continue;
            }

            handData = new HandCursorData
            {
                Landmarks = landmarks,
                Side = side,
                HasSide = true
            };
            return true;
        }

        return false;
    }

    private bool TryGetPlayerHand(Side side, out HandCursorData handData)
    {
        handData = default;

        Bodylink bodylink = Bodylink.Instance;
        if (bodylink == null || !bodylink.IsInitialized || bodylink.players == null)
        {
            return false;
        }

        if (playerSlot < 0 || playerSlot >= bodylink.players.Length || bodylink.players[playerSlot] == null)
        {
            return false;
        }

        BodylinkHandPoints[] handPoints = bodylink.players[playerSlot].handPoints;
        int handIndex = side == Side.Left ? 1 : 0;
        if (handPoints == null || handIndex >= handPoints.Length || handPoints[handIndex] == null)
        {
            return false;
        }

        List<NormalizedLandmark> landmarks = handPoints[handIndex].handLandmark;
        if (!HasRequiredLandmarks(landmarks))
        {
            return false;
        }

        handData = new HandCursorData
        {
            Landmarks = landmarks,
            Side = side,
            HasSide = true
        };
        return true;
    }

    private bool TryGetFirstDetectedHand(out HandCursorData handData)
    {
        handData = default;

        Bodylink bodylink = Bodylink.Instance;
        if (bodylink == null || !bodylink.IsInitialized || bodylink.bodylinkAvatar == null)
        {
            return false;
        }

        var result = bodylink.bodylinkAvatar.handLandmarkerResult;
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            return false;
        }

        List<NormalizedLandmark> landmarks = result.handLandmarks[0].landmarks;
        if (!HasRequiredLandmarks(landmarks))
        {
            return false;
        }

        handData = new HandCursorData
        {
            Landmarks = landmarks,
            HasSide = TryResolveHandSide(result.handedness, 0, out Side side),
            Side = side
        };
        return true;
    }

    private bool TryGetScreenPosition(List<NormalizedLandmark> landmarks, out Vector2 screenPosition)
    {
        screenPosition = default;
        if (!HasRequiredLandmarks(landmarks))
        {
            return false;
        }

        NormalizedLandmark indexTip = landmarks[indexTipLandmark];
        Vector2 normalized = new Vector2(indexTip.x, invertY ? 1f - indexTip.y : indexTip.y);

        if (clampToScreen)
        {
            normalized.x = Mathf.Clamp01(normalized.x);
            normalized.y = Mathf.Clamp01(normalized.y);
        }

        screenPosition = new Vector2(normalized.x * Screen.width, normalized.y * Screen.height) + screenOffset;

        if (clampToScreen)
        {
            screenPosition.x = Mathf.Clamp(screenPosition.x, 0f, Screen.width);
            screenPosition.y = Mathf.Clamp(screenPosition.y, 0f, Screen.height);
        }

        return true;
    }

    private Vector2 MoveCursor(Vector2 screenPosition)
    {
        Transform target = cursorVisual != null ? cursorVisual : transform;
        Vector3 targetPosition = new Vector3(screenPosition.x, screenPosition.y, cursorDepth);
        target.position = Vector3.Lerp(target.position, targetPosition, followLerp);

        return new Vector2(target.position.x, target.position.y);
    }

    private void UpdateCursorScale()
    {
        Transform target = cursorVisual != null ? cursorVisual : transform;
        float targetScale = isPinching ? pinchScale : idleScale;
        target.localScale = Vector3.Lerp(target.localScale, Vector3.one * targetScale, scaleLerp);
    }

    private void UpdateUiPointer(List<NormalizedLandmark> landmarks, Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            UpdatePinchFeedback(landmarks);
            return;
        }

        PointerEventData pointerData = BuildPointerData(screenPosition);
        GameObject newHover = raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
        HandleHover(pointerData, newHover);

        bool pinched = IsPinched(landmarks);
        if (pinched)
        {
            if (!isPinching)
            {
                BeginPinch(pointerData);
            }
            else
            {
                ContinuePinch(pointerData);
            }
        }
        else if (isPinching)
        {
            ReleasePinch(pointerData);
        }
    }

    private void UpdatePinchFeedback(List<NormalizedLandmark> landmarks)
    {
        bool pinched = IsPinched(landmarks);
        if (pinched == isPinching)
        {
            return;
        }

        isPinching = pinched;
        isDragging = false;
        PinchStateChanged?.Invoke(this, pinched);
    }

    private PointerEventData BuildPointerData(Vector2 screenPosition)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition,
            button = PointerEventData.InputButton.Left
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);
        pointerData.pointerCurrentRaycast = raycastResults.Count > 0 ? raycastResults[0] : new RaycastResult();
        return pointerData;
    }

    private bool IsPinched(List<NormalizedLandmark> landmarks)
    {
        if (!HasRequiredLandmarks(landmarks))
        {
            return false;
        }

        NormalizedLandmark indexTip = landmarks[indexTipLandmark];
        NormalizedLandmark thumbTip = landmarks[thumbTipLandmark];
        float distance = Vector2.Distance(new Vector2(indexTip.x, indexTip.y), new Vector2(thumbTip.x, thumbTip.y));

        if (distance < pinchDownThreshold)
        {
            pinchLastSeenTime = Time.unscaledTime;
            return true;
        }

        return distance <= pinchUpThreshold && Time.unscaledTime - pinchLastSeenTime < pinchReleaseGraceTime;
    }

    private void HandleHover(PointerEventData pointerData, GameObject newHover)
    {
        if (newHover == currentObject)
        {
            return;
        }

        if (currentObject != null)
        {
            ExecuteEvents.Execute(currentObject, pointerData, ExecuteEvents.pointerExitHandler);
        }

        if (newHover != null)
        {
            ExecuteEvents.Execute(newHover, pointerData, ExecuteEvents.pointerEnterHandler);
        }

        currentObject = newHover;
    }

    private void BeginPinch(PointerEventData pointerData)
    {
        isPinching = true;
        isDragging = false;
        PinchStateChanged?.Invoke(this, true);

        pressPosition = pointerData.position;
        lastPointerPosition = pointerData.position;
        pressRaycast = pointerData.pointerCurrentRaycast;

        GameObject target = raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
        pressTarget = target != null ? ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) : null;
        dragTarget = target != null ? ExecuteEvents.GetEventHandler<IDragHandler>(target) : null;
        clickTarget = target != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) : null;

        pointerData.pressPosition = pressPosition;
        pointerData.pointerPressRaycast = pressRaycast;
        pointerData.pointerPress = pressTarget;
        pointerData.pointerDrag = dragTarget;

        if (target == null)
        {
            return;
        }

        ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.initializePotentialDrag);
        if (pressTarget != null)
        {
            ExecuteEvents.Execute(pressTarget, pointerData, ExecuteEvents.pointerDownHandler);
        }
        else
        {
            ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
        }
    }

    private void ContinuePinch(PointerEventData pointerData)
    {
        pointerData.pressPosition = pressPosition;
        pointerData.pointerPressRaycast = pressRaycast;
        pointerData.pointerPress = pressTarget;
        pointerData.pointerDrag = dragTarget;

        Vector2 delta = pointerData.position - lastPointerPosition;
        pointerData.delta = delta;

        if (dragTarget != null)
        {
            float threshold = Mathf.Max(EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 0f, dragStartThreshold);
            if (!isDragging && Vector2.Distance(pointerData.position, pressPosition) >= threshold)
            {
                isDragging = true;
                pointerData.dragging = true;
                ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.beginDragHandler);
            }

            if (isDragging)
            {
                pointerData.dragging = true;
                ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.dragHandler);
            }
        }

        lastPointerPosition = pointerData.position;
    }

    private void ReleasePinch(PointerEventData pointerData)
    {
        pointerData.pressPosition = pressPosition;
        pointerData.pointerPressRaycast = pressRaycast;
        pointerData.pointerPress = pressTarget;
        pointerData.pointerDrag = dragTarget;

        if (isDragging && dragTarget != null)
        {
            pointerData.dragging = true;
            ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.endDragHandler);
        }

        if (pressTarget != null)
        {
            ExecuteEvents.Execute(pressTarget, pointerData, ExecuteEvents.pointerUpHandler);
        }
        else if (pressRaycast.gameObject != null)
        {
            ExecuteEvents.ExecuteHierarchy(pressRaycast.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
        }

        GameObject currentClickTarget = currentObject != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentObject) : null;
        if (!isDragging && clickTarget != null && clickTarget == currentClickTarget)
        {
            ExecuteEvents.Execute(clickTarget, pointerData, ExecuteEvents.pointerClickHandler);
        }

        pressTarget = null;
        dragTarget = null;
        clickTarget = null;
        isPinching = false;
        isDragging = false;
        PinchStateChanged?.Invoke(this, false);
    }

    private void HandleTrackingLost()
    {
        IsTracked = false;
        HasTrackedSide = false;

        if (isPinching && sendUiEvents && EventSystem.current != null && hasScreenPosition)
        {
            ReleasePinch(BuildPointerData(lastScreenPosition));
        }

        if (currentObject != null && EventSystem.current != null && hasScreenPosition)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = lastScreenPosition
            };
            ExecuteEvents.Execute(currentObject, pointerData, ExecuteEvents.pointerExitHandler);
            currentObject = null;
        }

        if (wasTracked)
        {
            wasTracked = false;
            SetVisualVisible(false);
            TrackingStateChanged?.Invoke(this, false);

            if (debugLogs)
            {
                Debug.Log("[SelectableHandCursor] Hand tracking lost.");
            }
        }
    }

    private void SetVisualVisible(bool visible)
    {
        if (!hideVisualWhenHandLost)
        {
            return;
        }

        if (cursorCanvasGroup != null)
        {
            cursorCanvasGroup.alpha = visible ? 1f : 0f;
            cursorCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (cursorVisual != null && cursorVisual.gameObject != gameObject)
        {
            cursorVisual.gameObject.SetActive(visible);
        }
    }

    private bool HasRequiredLandmarks(List<NormalizedLandmark> landmarks)
    {
        int requiredIndex = Mathf.Max(indexTipLandmark, thumbTipLandmark);
        return landmarks != null && landmarks.Count > requiredIndex;
    }

    private static bool TryResolveHandSide(IReadOnlyList<Classifications> handedness, int index, out Side side)
    {
        side = Side.Right;
        if (handedness == null ||
            index < 0 ||
            index >= handedness.Count ||
            handedness[index].categories == null ||
            handedness[index].categories.Count == 0)
        {
            return false;
        }

        string category = handedness[index].categories[0].categoryName;
        if (string.Equals(category, "Right", StringComparison.OrdinalIgnoreCase))
        {
            side = Side.Right;
            return true;
        }

        if (string.Equals(category, "Left", StringComparison.OrdinalIgnoreCase))
        {
            side = Side.Left;
            return true;
        }

        return false;
    }
}
