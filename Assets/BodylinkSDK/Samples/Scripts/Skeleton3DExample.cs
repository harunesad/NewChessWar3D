using System.Collections;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Skeleton3DExample : MonoBehaviour
{
    public Transform skeleton_1_pos, skeleton_2_pos;
    public RawImage rawImage;


    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();

        Bodylink.Instance.OnInitialized += () =>
        {
            Bodylink.Instance.Show3DSkeleton(true, 0, .1f);
            Bodylink.Instance.skeletonVisualizers[0].gameObject.transform.position = skeleton_1_pos.position;
            Bodylink.Instance.skeletonVisualizers[0].gameObject.transform.rotation = Quaternion.Euler(0, 180, 0);
            if (Bodylink.Instance.isMultiplayerEnabled)
            {
                Bodylink.Instance.Show3DSkeleton(true, 1, .1f);
                Bodylink.Instance.skeletonVisualizers[1].gameObject.transform.position = skeleton_2_pos.position;
                Bodylink.Instance.skeletonVisualizers[1].gameObject.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            rawImage.texture = Bodylink.Instance.cameraScreen.texture;
        };

    }

    public void MainButton()
    {
        SceneManager.LoadScene(0);
    }
}