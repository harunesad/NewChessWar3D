using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using BodylinkSDK;
public class ScreenRecordingTest : MonoBehaviour
{
    public RawImage qrCodeImage;
    public Text shareUrlText;
    public GameObject qrCodePanel;
    private BodylinkScreenRecording screenRecording;

    [SerializeField] private Button recordBtn;
    [SerializeField] private int autoStopAfterSeconds = 0;

    public UnityAction<Texture2D, string> onVideoShareReady;

    private Texture2D lastQrTexture;
    private string lastShareUrl;

    public Texture2D LastQrTexture => lastQrTexture;
    public string LastShareUrl => lastShareUrl;
    bool isRecording = false;

    private void Start()
    {
        screenRecording = FindFirstObjectByType<BodylinkScreenRecording>();
        if (screenRecording == null)
        {
            Debug.LogError("BodylinkScreenRecording component not found.");
            return;
        }

        screenRecording.onStartRecord += OnStartRecord;
        screenRecording.onStopRecording += OnStopRecording;

        if (recordBtn != null)
        {
            recordBtn.onClick.AddListener(OnClickRecord);
        }
    }



    private void OnDestroy()
    {
        if (screenRecording == null)
            return;

        screenRecording.onStartRecord -= OnStartRecord;
        screenRecording.onStopRecording -= OnStopRecording;
    }

    public void OnClickRecord()
    {
        if (isRecording)
        {
            screenRecording.StopRecording();
            return;
        }
        else
        {
            screenRecording.StartRecording(autoStopAfterSeconds);
        }
    }



    public void OnStartRecord()
    {
        recordBtn.GetComponentInChildren<Text>().text = "Stop";
        isRecording = true;
    }

    private void OnStopRecording(string savedVideoPath)
    {
        recordBtn.GetComponentInChildren<Text>().text = "Start";
        isRecording = false;

        screenRecording.ShareRecording(savedVideoPath, OnShareReady);
    }

    private void OnShareReady(Texture2D qrTexture, string shareUrl)
    {
        lastQrTexture = qrTexture;
        lastShareUrl = shareUrl;

        if (qrCodeImage != null)
        {
            qrCodePanel.SetActive(true);
            qrCodeImage.texture = qrTexture;
            shareUrlText.text = shareUrl;
        }
        onVideoShareReady?.Invoke(qrTexture, shareUrl);
    }
}
