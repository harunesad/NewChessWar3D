using System;
using UnityEngine;
using GoogleMobileAds.Api;
using System.Collections;

public class RewardedInterstitialManager : MonoBehaviour
{
    [Header("AdMob Configuration")]
    // Test ID for Rewarded Interstitial (Android)
    [SerializeField] private string adUnitId = "ca-app-pub-3940256099942544/5354046379";

    private RewardedInterstitialAd rewardedInterstitialAd;
    private bool isLoading = false;
    private int retryCount = 0;
    private const int MAX_RETRIES = 5;

    public static RewardedInterstitialManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RewardedInterstitialManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("RewardedInterstitialManager");
                    _instance = go.AddComponent<RewardedInterstitialManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    private static RewardedInterstitialManager _instance;

    public Action OnRewardEarned;
    public Action OnAdFailedToShow;
    public Action OnAdClosed;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // MobileAds.Initialize is usually called in RewardedAdsManager, 
        // but we'll ensure it's loaded here as well.
        MobileAds.Initialize(initStatus => {
            Debug.Log("AdMob initialized (Rewarded Interstitial)");
            LoadAd();
        });
    }

    public void LoadAd()
    {
        if (isLoading) return;

        if (rewardedInterstitialAd != null)
        {
            rewardedInterstitialAd.Destroy();
            rewardedInterstitialAd = null;
        }

        isLoading = true;
        Debug.Log("Loading Rewarded Interstitial Ad...");

        var adRequest = new AdRequest();
        RewardedInterstitialAd.Load(adUnitId, adRequest, (RewardedInterstitialAd ad, LoadAdError error) =>
        {
            isLoading = false;
            if (error != null || ad == null)
            {
                Debug.LogError($"Rewarded Interstitial failed to load: {error?.GetMessage()}");
                StartCoroutine(ReloadAfterDelay(5f));
                return;
            }

            Debug.Log("Rewarded Interstitial ad loaded successfully.");
            rewardedInterstitialAd = ad;
            retryCount = 0;
            RegisterEventHandlers(rewardedInterstitialAd);
        });
    }

    public bool IsAdReady()
    {
        return rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd();
    }

    public void ShowAd()
    {
        if (rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd())
        {
            Debug.Log("Showing Rewarded Interstitial ad...");
            rewardedInterstitialAd.Show((Reward reward) =>
            {
                Debug.Log($"Rewarded Interstitial reward earned: {reward.Amount} {reward.Type}");
                OnRewardEarned?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("Rewarded Interstitial ad not ready.");
            OnAdFailedToShow?.Invoke();
            LoadAd();
        }
    }

    private void RegisterEventHandlers(RewardedInterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded Interstitial closed.");
            OnAdClosed?.Invoke();
            LoadAd();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"Rewarded Interstitial failed to show: {error.GetMessage()}");
            OnAdFailedToShow?.Invoke();
            LoadAd();
        };
    }

    private IEnumerator ReloadAfterDelay(float delay)
    {
        if (retryCount < MAX_RETRIES)
        {
            retryCount++;
            yield return new WaitForSeconds(delay);
            LoadAd();
        }
    }

    private void OnDestroy()
    {
        if (rewardedInterstitialAd != null)
        {
            rewardedInterstitialAd.Destroy();
        }
    }
}
