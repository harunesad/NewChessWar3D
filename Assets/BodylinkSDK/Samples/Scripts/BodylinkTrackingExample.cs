using UnityEngine;
using UnityEngine.UI;
using BodylinkSDK;

public class BodylinkTrackingExample : MonoBehaviour
{
    public RawImage miniCamera;
    public GameObject playerOne, playerTwo;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        miniCamera.gameObject.SetActive(false);
        Bodylink.Instance.OnInitialized += () =>
        {
            miniCamera.gameObject.SetActive(true);
            miniCamera.texture = Bodylink.Instance.cameraScreen.texture;
            playerOne.transform.position = new Vector3(0, -1, 0);
            if (playerTwo != null)
                playerTwo.SetActive(Bodylink.Instance.isMultiplayerEnabled);
            if (Bodylink.Instance.isMultiplayerEnabled)
            {
                playerOne.transform.position = new Vector3(3f, -1, 0);
                playerTwo.transform.position = new Vector3(-3f, -1, 0);
            }
        };
    }
}
