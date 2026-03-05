using BodylinkSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GrabPullThrow2DExample : MonoBehaviour
{
    [Header("References")]
    public bool isCompleted;
    public RawImage miniCamera;     // Mini camera view
    private bool levelCompleted;
    public GameObject successPanel;
    public CollectPoint[] collectPoints;


    void Start()
    {
        miniCamera.gameObject.SetActive(false);
        Bodylink.Instance.OnInitialized += () =>
        {
            miniCamera.gameObject.SetActive(true);
            miniCamera.texture = Bodylink.Instance.cameraScreen.texture;
        };
    }

    void Update()
    {
        if (levelCompleted) return;
        if (collectPoints == null) return;
        for (int i = 0; i < collectPoints.Length; i++)
        {
            if (collectPoints[i].completed == false)
            {
                isCompleted = false;
                break;
            }
            else
            {
                isCompleted = true;
            }
        }

        if (isCompleted)
        {
            levelCompleted = true;
            successPanel.SetActive(true);
        }
    }

    public void RestartButton()
    {
        DestroyAllDontDestroyOnLoadObjects();
    }

    private void DestroyAllDontDestroyOnLoadObjects()
    {
        var bodylinkPrefab = FindAnyObjectByType<Bodylink>();
        if (bodylinkPrefab != null)
        {
            Destroy(bodylinkPrefab);
            Destroy(bodylinkPrefab.gameObject);
        }

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        Resources.UnloadUnusedAssets();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);


    }

}