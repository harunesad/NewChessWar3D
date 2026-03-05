using BodylinkSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerCutOutExample : MonoBehaviour
{
    public RawImage playerOneRawImage, playerTwoRawImage;
    FullBodyCutout fullBodyCutout;
    void Start()
    {
        fullBodyCutout = GetComponent<FullBodyCutout>();
        //   Bodylink.Instance.Initialize();

        playerTwoRawImage.gameObject.SetActive(Bodylink.Instance.numberOfPlayers > 1);

    }
    void Update()
    {
        playerOneRawImage.texture = fullBodyCutout.bodyCutout[0];
        playerTwoRawImage.texture = fullBodyCutout.bodyCutout[1];
    }
}
