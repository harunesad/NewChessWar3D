using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

public class Watch3DExample : MonoBehaviour
{
    public RawImage camView;

    private void Start()
    {
        Bodylink.Instance.OnInitialized += () =>
        {
            camView.texture = Bodylink.Instance.cameraScreen.texture;
        };
    }
}
