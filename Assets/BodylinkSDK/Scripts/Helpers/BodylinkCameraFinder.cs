using UnityEngine;

namespace BodylinkSDK
{
    /// <summary>
    /// A utility class to find and manage camera devices.
    /// </summary>
    public class BodylinkCameraFinder : MonoBehaviour
    {
        Canvas canvas;
        void Awake()
        {
            Camera camera = Bodylink.Instance.cam;
            if (camera == null)
                camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("No main camera found. Please set a camera's tag to 'MainCamera'.");
            }
            else
            {
                canvas = GetComponent<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("No Canvas component found on this GameObject.");
                    return;
                }
                else
                {
                    canvas.worldCamera = camera;
                }
            }
        }
        void Update()
        {
            if (canvas != null && canvas.worldCamera == null)
            {
                if (Bodylink.Instance.cam != null)
                {
                    canvas.worldCamera = Bodylink.Instance.cam;
                    return;
                }
                Camera camera = Camera.main;
                if (camera != null)
                {
                    canvas.worldCamera = camera;
                }
                else
                {
                    Debug.LogError("No main camera found. Please set a camera's tag to 'MainCamera'.");
                }
            }

            //Check if main camera has changed
            if (canvas != null && canvas.worldCamera != Camera.main)
            {
                canvas.worldCamera = Camera.main;

            }


        }
    }
}
