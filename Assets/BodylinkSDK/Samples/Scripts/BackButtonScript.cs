using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;
using BodylinkSDK;
using Mediapipe.Unity.Sample;

public class BackButtonScript : MonoBehaviour
{
    private Button button;

    void OnEnable()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnBackButtonPressed);
    }

    void OnDisable()
    {
        button.onClick.RemoveListener(OnBackButtonPressed);
    }
    // Hook this to your back button onClick
    public void OnBackButtonPressed()
    {
        DestroyAllDontDestroyOnLoadObjects();

        //StartCoroutine(BackAndClean());
    }

    private void DestroyAllDontDestroyOnLoadObjects()
    {
        var bodylinkPrefab = FindAnyObjectByType<Bodylink>();
        if (bodylinkPrefab != null)
        {
            Destroy(bodylinkPrefab);
            Destroy(bodylinkPrefab.gameObject);
        }
        // var bootPrefab = FindAnyObjectByType<Bootstrap>();
        // if (bootPrefab != null)
        // {
        //     Destroy(bootPrefab.gameObject);
        // }
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        Resources.UnloadUnusedAssets();
        SceneManager.LoadScene(0, LoadSceneMode.Single);


    }
}
