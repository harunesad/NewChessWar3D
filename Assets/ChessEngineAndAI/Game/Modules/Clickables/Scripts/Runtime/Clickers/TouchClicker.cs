using UnityEngine;
using Clickables.TouchScreens;

namespace Clickables
{
    /// <summary>
    /// An implementation of a Clicker that uses touch inputs to click on Clickables.
    /// NOTE: This component does not implement the 'hover over' functionality.
    /// NOTE: Touch holds will fire secondary click events after TouchInvoker.touchHoldDelay seconds.
    /// </summary>
    /// Author: Intuitive Gaming Solutions
    [RequireComponent(typeof(InvokeEventOnTouch))]
    public class TouchClicker : Clicker
    {
        #region Editor Serialized Settings
        [Header("Settings - Touch Clicker")]
        [Tooltip("Controls the mode with which the clicker interacts with triggers.")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [Tooltip("The clicker mode that controls what kind of colliders are targetted by the clicker.\n\nBoth3D - clicks both 2D and 3D colliders but favors 3D colliders.\n3D - clicks only 3D colliders.\n2D - clicks only 2D colliders.\nBoth2D - clicks both 2D and 3D colliders but favors 2D colliders.")]
        public ClickerMode clickerMode = ClickerMode.Both3D;
        [Tooltip("(Optional) A reference to the Camera to use when raycasting. If not set CameraReference will be used.")]
        [SerializeField] Camera m_CameraOverride;
        [Tooltip("Should Clickables with their 'enabled' field set to false still be clickable?")]
        public bool clickDisabledClickables;
        [Tooltip("A layer mask of layers to be ignored when looking for layers raycasting from the mouse pointer.")]
        public LayerMask ignorePointerRaycastLayers;
        [Tooltip("The maximum distance this Clicker may click Clickables at.")]
        public float maxClickDistance = 100f;
        #endregion
        #region Public Properties
        /// <summary>A reference to the InvokeEventOnTouch component that drives this clicker.</summary>
        public InvokeEventOnTouch TouchInvoker { get; private set; }
        /// <summary>A reference to the Camera that is used in raycasting done by this component.</summary>
        public Camera CameraReference { get { return m_CameraOverride != null ? m_CameraOverride : Camera.main; } }
        #endregion

        // Unity callback(s).
        #region Unity Callbacks
        protected override void Awake()
        {
            // Find 'TouchInvoker' reference.
            TouchInvoker = GetComponent<InvokeEventOnTouch>();

            // Invoke the base class 'Awake' method.
            base.Awake();
        }

        protected override void OnEnable()
        {
            // Subscribe to 'TouchInvoker' events.
            TouchInvoker.Touched.AddListener(OnTouched);
            TouchInvoker.TouchHeld.AddListener(OnTouchHeld);

            // Invoke the base class 'OnEnable' method.
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            // Invoke the base type 'OnDisable()' callback.
            base.OnDisable();

            // Unsubscribe from 'TouchInvoker' events.
            if (TouchInvoker != null)
            {
                TouchInvoker.Touched.RemoveListener(OnTouched);
                TouchInvoker.TouchHeld.RemoveListener(OnTouchHeld);
            }

            // Unhighlight on disable if set.
            if (unhoverOnDisable)
                ForceStopHover();
        }
        #endregion

        // Public method(s).
        #region Public Setting Override Methods
        /// <summary>Nullifies the 'camera override' setting for this component.</summary>
        public void NullifyCameraOverride() { m_CameraOverride = null; }
        /// <summary>Sets the 'camera override' setting for this component.</summary>
        /// <param name="pCamera"></param>
        public void SetCameraOverride(Camera pCamera) { m_CameraOverride = pCamera; }
        /// <summary>A public method that sets the maxClickDistance field of this component. Useful for use with Unity editor events.</summary>
        /// <param name="pDistance"></param>
        public void SetMaxClickDistance(float pDistance) { maxClickDistance = pDistance; }
        /// <summary>Sets 'clickerMode' to 'ClickerMode.Both3D'. This mode clicks both 2D and 3D colliders but favors 3D colliders.</summary>
        public void SetClickerModeBoth3D() { clickerMode = ClickerMode.Both3D; }
        /// <summary>Sets 'clickerMode' to 'ClickerMode.Both3D'. This mode clicks both 2D and 3D colliders but favors 2D colliders.</summary>
        public void SetClickerModeBoth2D() { clickerMode = ClickerMode.Both2D; }
        /// <summary>Sets 'clickerMode' to 'ClickerMode.Collider'. This mode clicks only 3D colliders.</summary>
        public void SetClickerMode3D() { clickerMode = ClickerMode.Collider; }
        /// <summary>Sets 'clickerMode' to 'ClickerMode.Collider2D'. This mode clicks only 2D colliders.</summary>
        public void SetClickerMode2D() { clickerMode = ClickerMode.Collider2D; }
        #endregion

        // Private callback(s).
        #region Protected Touch Callbacks
        /// <summary>Invoked whenever a new touch is detected.</summary>
        /// <param name="pTouchPos"></param>
        protected void OnTouched(Vector2 pTouchPos)
        {
            // Ensure there is a valid camera reference.
            if (CameraReference != null)
            {
                // Check if something was hit in a non-ignored layer.
                Ray ray = CameraReference.ScreenPointToRay(pTouchPos);

                // MODE: Both (Favor 3D)
                if (clickerMode == ClickerMode.Both3D)
                {
                    // 3D click detection.
                    bool rayHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxClickDistance, ~ignorePointerRaycastLayers, triggerInteraction);
                    if (rayHit)
                    {
                        // Check for Clickable component on hitinfo collider.
                        Clickable clickable = Clickable.Find(hitInfo.collider);

                        // Fire primary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            PrimaryClickClickable(clickable, pTouchPos);
                    }
                    else
                    {
                        // Fallback to 2D click detection.
                        RaycastHit2D rayHit2D = Physics2D.Raycast(ray.origin, ray.direction, maxClickDistance, ~ignorePointerRaycastLayers);
                        if (rayHit2D)
                        {
                            // Check for Clickable component on rayHit2D collider.
                            Clickable clickable = Clickable.Find(rayHit2D.collider);

                            // Fire primary clicked event.
                            if (clickable != null && (clickable.enabled || clickDisabledClickables))
                                PrimaryClickClickable(clickable, pTouchPos);
                        }
                    }
                }
                // MODE: 3D.
                else if (clickerMode == ClickerMode.Collider)
                {
                    // 3D click detection.
                    bool rayHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxClickDistance, ~ignorePointerRaycastLayers, triggerInteraction);
                    if (rayHit)
                    {
                        // Check for Clickable component on hitinfo collider.
                        Clickable clickable = Clickable.Find(hitInfo.collider);

                        // Fire primary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            PrimaryClickClickable(clickable, pTouchPos);
                    }
                }
                // MODE: 2D.
                else if (clickerMode == ClickerMode.Collider2D)
                {
                    // 2D click detection.
                    RaycastHit2D rayHit = Physics2D.Raycast(ray.origin, ray.direction, maxClickDistance, ~ignorePointerRaycastLayers);
                    if (rayHit)
                    {
                        // Check for Clickable component on rayHit collider.
                        Clickable clickable = Clickable.Find(rayHit.collider);

                        // Fire primary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            PrimaryClickClickable(clickable, pTouchPos);
                    }
                }
                // MODE: Both (Favor 2D).
                else if (clickerMode == ClickerMode.Both2D)
                {
                    // 2D click detection.
                    RaycastHit2D rayHit2D = Physics2D.Raycast(ray.origin, ray.direction, maxClickDistance, ~ignorePointerRaycastLayers);
                    if (rayHit2D)
                    {
                        // Check for Clickable component on rayHit2D collider.
                        Clickable clickable = Clickable.Find(rayHit2D.collider);

                        // Fire primary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            PrimaryClickClickable(clickable, pTouchPos);
                    }
                    else
                    {
                        // Fallback to 3D click detection.
                        bool rayHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxClickDistance, ~ignorePointerRaycastLayers, triggerInteraction);
                        if (rayHit)
                        {
                            // Check for Clickable component on hitinfo collider.
                            Clickable clickable = Clickable.Find(hitInfo.collider);

                            // Fire primary clicked event.
                            if (clickable != null && (clickable.enabled || clickDisabledClickables))
                                PrimaryClickClickable(clickable, pTouchPos);
                        }
                    }
                }
                else { Debug.LogWarning($"Unhandled 'clickerMode' set for {nameof(MouseClicker)} component: {clickerMode}", gameObject); }
            }
        }

        /// <summary>Invoked whenever a touch is held for TouchInvoker.touchHoldDelay seconds.</summary>
        /// <param name="pTouchPos"></param>
        protected void OnTouchHeld(Vector2 pTouchPos)
        {
            // Ensure there is a valid camera reference.
            if (CameraReference != null)
            {
                // Check if something was hit in a non-ignored layer.
                Ray ray = CameraReference.ScreenPointToRay(pTouchPos);

                // MODE: Both (Favor 3D)
                if (clickerMode == ClickerMode.Both3D)
                {
                    // 3D click detection.
                    bool rayHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxClickDistance, ~ignorePointerRaycastLayers, triggerInteraction);
                    if (rayHit)
                    {
                        // Check for Clickable component on hitinfo collider.
                        Clickable clickable = Clickable.Find(hitInfo.collider);

                        // Fire secondary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            SecondaryClickClickable(clickable, pTouchPos);
                    }
                    else
                    {
                        // Fallback to 2D click detection.
                        RaycastHit2D rayHit2D = Physics2D.Raycast(ray.origin, ray.direction, maxClickDistance, ~ignorePointerRaycastLayers);
                        if (rayHit2D)
                        {
                            // Check for Clickable component on rayHit2D collider.
                            Clickable clickable = Clickable.Find(rayHit2D.collider);

                            // Fire secondary clicked event.
                            if (clickable != null && (clickable.enabled || clickDisabledClickables))
                                SecondaryClickClickable(clickable, pTouchPos);
                        }
                    }
                }
                // MODE: 3D.
                else if (clickerMode == ClickerMode.Collider)
                {
                    // 3D click detection.
                    bool rayHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxClickDistance, ~ignorePointerRaycastLayers, triggerInteraction);
                    if (rayHit)
                    {
                        // Check for Clickable component on hitinfo collider.
                        Clickable clickable = Clickable.Find(hitInfo.collider);

                        // Fire secondary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            SecondaryClickClickable(clickable, pTouchPos);
                    }
                }
                // MODE: 2D.
                else if (clickerMode == ClickerMode.Collider2D)
                {
                    // 2D click detection.
                    RaycastHit2D rayHit = Physics2D.Raycast(ray.origin, ray.direction, maxClickDistance, ~ignorePointerRaycastLayers);
                    if (rayHit)
                    {
                        // Check for Clickable component on rayHit collider.
                        Clickable clickable = Clickable.Find(rayHit.collider);

                        // Fire secondary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            SecondaryClickClickable(clickable, pTouchPos);
                    }
                }
                // MODE: Both (Favor 2D).
                else if (clickerMode == ClickerMode.Both2D)
                {
                    // 2D click detection.
                    RaycastHit2D rayHit2D = Physics2D.Raycast(ray.origin, ray.direction, maxClickDistance, ~ignorePointerRaycastLayers);
                    if (rayHit2D)
                    {
                        // Check for Clickable component on rayHit2D collider.
                        Clickable clickable = Clickable.Find(rayHit2D.collider);

                        // Fire secondary clicked event.
                        if (clickable != null && (clickable.enabled || clickDisabledClickables))
                            SecondaryClickClickable(clickable, pTouchPos);
                    }
                    else
                    {
                        // Fallback to 3D click detection.
                        bool rayHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxClickDistance, ~ignorePointerRaycastLayers, triggerInteraction);
                        if (rayHit)
                        {
                            // Check for Clickable component on hitinfo collider.
                            Clickable clickable = Clickable.Find(hitInfo.collider);

                            // Fire secondary clicked event.
                            if (clickable != null && (clickable.enabled || clickDisabledClickables))
                                SecondaryClickClickable(clickable, pTouchPos);
                        }
                    }
                }
                else { Debug.LogWarning($"Unhandled 'clickerMode' set for {nameof(MouseClicker)} component: {clickerMode}", gameObject); }
            }
        }
        #endregion
    }
}
