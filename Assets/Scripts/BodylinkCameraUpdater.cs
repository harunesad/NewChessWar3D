using UnityEngine;
using UnityEngine.SceneManagement;
using BodylinkSDK;

/// <summary>
/// Sahne değişiminde Bodylink SDK'sındaki Camera referansını otomatik günceller.
/// Android'de ön kamera seçimi artık Bodylink.cs içinde pipeline kurulmadan önce yapılıyor.
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
