using System.Collections.Generic;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.EventSystems;

public class CursorPointer : MonoBehaviour
{
    private float dragStartThreshold = 1.5f; // pixels before we treat a pinch as a drag
    private float pinchDownThreshold = 0.03f;
    private float pinchUpThreshold = 0.04f;  // add hysteresis so we don't flicker in/out
    private float pinchReleaseGraceTime = 0.1f;

    // how much to expand reach when user is shorter

    private GameObject currentObject;
    private GameObject pressTarget;
    private GameObject dragTarget;
    private GameObject clickTarget;
    private RaycastResult pressRaycast;

    private Vector2 pressPosition;
    private Vector2 lastPointerPosition;
    private float pinchLastSeenTime;
    private bool isPinching;
    private bool isDragging;

    void Update()
    {
        try
        {
            var handResult = Bodylink.Instance.bodylinkAvatar.handLandmarkerResult.handLandmarks[0];
            if (handResult.landmarks == null || handResult.landmarks.Count == 0) return;



            var handLandmarks = handResult.landmarks;
            if (handLandmarks == null || handLandmarks.Count <= 8) return;

            var indexTip = handLandmarks[8];
            Vector2 normalized = new Vector2(indexTip.x, 1 - indexTip.y);
            normalized = (normalized - Vector2.one * 0.5f) + Vector2.one * 0.5f;
            normalized.x = Mathf.Clamp01(normalized.x);
            normalized.y = Mathf.Clamp01(normalized.y);

            var targetPosition = new Vector3(normalized.x * Screen.width, normalized.y * Screen.height, -2);
            transform.position = Vector3.Lerp(transform.position, targetPosition, 0.1f);

            if (EventSystem.current == null) return;

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(transform.position.x, transform.position.y),
                button = PointerEventData.InputButton.Left
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            pointerData.pointerCurrentRaycast = results.Count > 0 ? results[0] : new RaycastResult();

            GameObject newHover = results.Count > 0 ? results[0].gameObject : null;
            HandleHover(pointerData, newHover);

            bool pinched = IsPinched();
            if (pinched)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * 0.3f, 0.2f);
                if (!isPinching)
                {
                    BeginPinch(pointerData, results);
                }
                else
                {
                    ContinuePinch(pointerData, results);
                }
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, 0.2f);
                if (isPinching)
                {
                    ReleasePinch(pointerData);
                }
            }
        }
        catch (System.Exception ex)
        {
            //Debug.Log(ex);
        }
    }

    bool IsPinched()
    {
        var gestureResult = Bodylink.Instance.bodylinkAvatar.handLandmarkerResult.handLandmarks[0];
        if (gestureResult.landmarks == null || gestureResult.landmarks.Count == 0) return false;

        var landmarks = gestureResult.landmarks;
        if (landmarks == null || landmarks.Count <= 8) return false;

        var indexTip = landmarks[8];
        var thumbTip = landmarks[4];
        float distance = Vector2.Distance(new Vector2(indexTip.x, indexTip.y), new Vector2(thumbTip.x, thumbTip.y));
        bool pinchClosed = distance < pinchDownThreshold;
        bool pinchOpen = distance > pinchUpThreshold;

        if (pinchClosed)
        {
            pinchLastSeenTime = Time.unscaledTime;
            return true;
        }

        if (!pinchOpen && Time.unscaledTime - pinchLastSeenTime < pinchReleaseGraceTime)
        {
            return true;
        }

        return false;
    }

    private void HandleHover(PointerEventData pointerData, GameObject newHover)
    {
        if (newHover == currentObject) return;

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

    private void BeginPinch(PointerEventData pointerData, List<RaycastResult> results)
    {
        isPinching = true;
        isDragging = false;

        pressPosition = pointerData.position;
        lastPointerPosition = pointerData.position;
        pressRaycast = pointerData.pointerCurrentRaycast;

        GameObject target = results.Count > 0 ? results[0].gameObject : null;
        pressTarget = target != null ? ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) : null;
        dragTarget = target != null ? ExecuteEvents.GetEventHandler<IDragHandler>(target) : null;
        clickTarget = target != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) : null;

        pointerData.pressPosition = pressPosition;
        pointerData.pointerPressRaycast = pressRaycast;
        pointerData.pointerPress = pressTarget;
        pointerData.pointerDrag = dragTarget;

        if (target != null)
        {
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
    }

    private void ContinuePinch(PointerEventData pointerData, List<RaycastResult> results)
    {
        pointerData.pressPosition = pressPosition;
        pointerData.pointerPressRaycast = pressRaycast;
        pointerData.pointerPress = pressTarget;
        pointerData.pointerDrag = dragTarget;
        pointerData.pointerCurrentRaycast = results.Count > 0 ? results[0] : new RaycastResult();

        Vector2 delta = pointerData.position - lastPointerPosition;
        pointerData.delta = delta;

        if (dragTarget != null)
        {
            float threshold = Mathf.Max(EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 0, dragStartThreshold);
            if (!isDragging && delta.sqrMagnitude >= threshold * threshold)
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
        pointerData.pointerCurrentRaycast = pressRaycast;

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

        var currentClickTarget = currentObject != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentObject) : null;
        if (!isDragging && clickTarget != null && clickTarget == currentClickTarget)
        {
            ExecuteEvents.Execute(clickTarget, pointerData, ExecuteEvents.pointerClickHandler);
        }

        pressTarget = null;
        dragTarget = null;
        clickTarget = null;
        isPinching = false;
        isDragging = false;
    }
}
