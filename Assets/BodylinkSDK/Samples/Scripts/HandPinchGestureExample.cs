using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class HandPinchGestureExample : MonoBehaviour
{
    [Header("Hand Cursor Settings")]                        // Canvas containing the cursor
    public RawImage rawImage;


    void Start()
    {
        Bodylink.Instance.OnInitialized += () =>
        {
            rawImage.gameObject.SetActive(true);
            rawImage.texture = Bodylink.Instance.cameraScreen.texture;
        };
    }

}