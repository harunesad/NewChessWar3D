using GoogleMobileAds.Api;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AdsManager : MonoBehaviour
{
    InterstitialAd interstitialAd;
    void Start()
    {
        MobileAds.Initialize(initStatus => { LoadInterstitialAd(); });
    }
    #region IntersitialAd
#if UNITY_ANDROID
    private string adUnitId = "ca-app-pub-4860105960035905/8858418012";
#endif
    public void LoadInterstitialAd()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }
        Debug.Log("Load");
        var adRequest = new AdRequest();

        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError adError) =>
        {
            if (adError != null || ad == null)
            {
                Debug.LogError("Fail" + "error: " + adError);
                return;
            }
            Debug.Log("Loaded with response: " + ad.GetResponseInfo());
            interstitialAd = ad;

            RegisterReloadHandler(interstitialAd);
        });
    }
    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("Show");
            interstitialAd.Show();
            //RegisterReloadHandler(interstitialAd);
        }
        else
        {
            Debug.LogError("Not ready yet");
            if (PlayerPrefs.GetString("Type") == "White")
            {
                SceneManager.LoadScene(2);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
            LoadInterstitialAd();
        }
    }
    private void RegisterReloadHandler(InterstitialAd interstitialAd)
    {
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Full screen content closed.");
            if (PlayerPrefs.GetString("Type") == "White")
            {
                SceneManager.LoadScene(2);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
            LoadInterstitialAd();
        };
        interstitialAd.OnAdFullScreenContentFailed += (AdError adError) =>
        {
            Debug.LogError("Failed" + "with error: " + adError);
            if (PlayerPrefs.GetString("Type") == "White")
            {
                SceneManager.LoadScene(2);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
            LoadInterstitialAd();
        };
    }
    #endregion
}
