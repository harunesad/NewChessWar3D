using GoogleMobileAds.Api;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameAnalyticsSDK;

public class AdsManager : MonoBehaviour
{
    InterstitialAd interstitialAd;
    void Start()
    {
        MobileAds.Initialize(initStatus => { LoadInterstitialAd(); });
    }
    #region IntersitialAd
#if UNITY_ANDROID
    private string adUnitId = "ca-app-pub-3940256099942544/1033173712";
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
            GameAnalytics.NewDesignEvent("Ads:Interstitial:Show");
            interstitialAd.Show();
            //RegisterReloadHandler(interstitialAd);
        }
        else
        {
            Debug.LogError("Not ready yet");
            if (PlayerPrefs.GetInt("Scene") == 0)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            LoadInterstitialAd();
        }
    }
    private void RegisterReloadHandler(InterstitialAd interstitialAd)
    {
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Full screen content closed.");
            if (PlayerPrefs.GetInt("Scene") == 0)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            LoadInterstitialAd();
        };
        interstitialAd.OnAdFullScreenContentFailed += (AdError adError) =>
        {
            Debug.LogError("Failed" + "with error: " + adError);
            if (PlayerPrefs.GetInt("Scene") == 0)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            LoadInterstitialAd();
        };
    }
    #endregion
}
