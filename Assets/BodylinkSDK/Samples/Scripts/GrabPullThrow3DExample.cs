using BodylinkSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GrabPullThrow3DExample : MonoBehaviour
{
    [SerializeField] private RawImage miniCamera;
    //[SerializeField] private GameObject ground;
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int maxSpawnedObjects = 10;
    [SerializeField] private GameObject successPanel;
    [SerializeField] private bool spawnCubes;
    [SerializeField] private CollectPoint[] collectPoints;
    private bool isCompleted;
    private bool levelCompleted;



    void Start()
    {
        //ground.SetActive(false);
        miniCamera.gameObject.SetActive(false);

        Bodylink.Instance.OnInitialized += () =>
        {
            //ground.SetActive(true);
            miniCamera.gameObject.SetActive(true);
            miniCamera.texture = Bodylink.Instance.cameraScreen.texture;
            if (spawnCubes)
                SpawnObjects();
        };
    }

    private void SpawnObjects()
    {
        for (int i = 1; i <= maxSpawnedObjects; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-7f, 7f), 3, Random.Range(-7f, 5f));
            GameObject spawnedObject = Instantiate(cubePrefab, pos, Quaternion.identity);
            if (i <= 3)
            {
                spawnedObject.GetComponent<Renderer>().material.color = Color.red;
                spawnedObject.GetComponent<Collectable>().id = 0;
            }

            else if (i > 3 && i <= 6)
            {
                spawnedObject.GetComponent<Renderer>().material.color = Color.green;
                spawnedObject.GetComponent<Collectable>().id = 1;
            }

            else if (i > 6 && i <= 9)
            {
                spawnedObject.GetComponent<Renderer>().material.color = Color.blue;
                spawnedObject.GetComponent<Collectable>().id = 2;
            }
        }
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