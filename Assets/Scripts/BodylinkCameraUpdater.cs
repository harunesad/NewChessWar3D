using UnityEngine;
using UnityEngine.SceneManagement;
using BodylinkSDK;

/// <summary>
/// Sahne değişiminde Bodylink SDK'sındaki Camera referansını otomatik günceller.
/// Bodylink DontDestroyOnLoad olduğundan sahnedeki kamera yok olduğunda
/// referans null kalır. Bu script yeni sahnenin Main Camera'sını atar.
/// </summary>
[RequireComponent(typeof(Bodylink))]
public class BodylinkCameraUpdater : MonoBehaviour
{
    private Bodylink bodylink;

    private void Awake()
    {
        bodylink = GetComponent<Bodylink>();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // Yeni sahnenin Main Camera'sını bul ve Bodylink'e ata
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            bodylink.cam = mainCam;
            Debug.Log($"[BodylinkCameraUpdater] Kamera güncellendi: {mainCam.name} ({newScene.name})");
        }
        else
        {
            Debug.LogWarning($"[BodylinkCameraUpdater] '{newScene.name}' sahnesinde Main Camera bulunamadı!");
        }
    }
}
