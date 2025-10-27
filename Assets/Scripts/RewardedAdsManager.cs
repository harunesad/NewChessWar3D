using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;

public class RewardedAdsManager : MonoBehaviour
{
    [Header("AdMob Configuration")]
    [SerializeField] private string rewardedAdUnitId = "ca-app-pub-4860105960035905/8122020873"; // Test ID
    
    private RewardedAd rewardedAd;
    private bool isAdLoaded = false;
    
    // Events
    public System.Action OnRewardEarned;
    public System.Action OnAdFailedToLoad;
    public System.Action OnAdFailedToShow;
    
    void Start()
    {
        // Initialize Mobile Ads SDK
        MobileAds.Initialize(initStatus => {
            Debug.Log("AdMob initialized successfully");
            LoadRewardedAd();
        });
    }
    
    void Update()
    {
        // Optional: Check if ad is ready periodically
        if (!isAdLoaded && rewardedAd == null)
        {
            LoadRewardedAd();
        }
    }
    
    /// <summary>
    /// Loads a rewarded ad
    /// </summary>
    public void LoadRewardedAd()
    {
        // Clean up the old ad before creating a new one
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }
        
        Debug.Log("Loading rewarded ad...");
        
        // Create our request used to load the ad
        AdRequest adRequest = new AdRequest();
        
        // Send the request to load the ad
        RewardedAd.Load(rewardedAdUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            // If error is not null, the load request failed
            if (error != null || ad == null)
            {
                Debug.LogError("Rewarded ad failed to load: " + error);
                isAdLoaded = false;
                OnAdFailedToLoad?.Invoke();
                return;
            }
            
            Debug.Log("Rewarded ad loaded successfully");
            rewardedAd = ad;
            isAdLoaded = true;
            
            // Register to ad events to extend functionality
            RegisterEventHandlers(rewardedAd);
        });
    }
    
    /// <summary>
    /// Shows the rewarded ad
    /// </summary>
    public void ShowRewardedAd()
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            Debug.Log("Showing rewarded ad");
            rewardedAd.Show((Reward reward) =>
            {
                Debug.Log($"Rewarded ad completed! Reward: {reward.Amount} {reward.Type}");
                OnRewardEarned?.Invoke();
                
                // Reload ad for next time
                LoadRewardedAd();
            });
        }
        else
        {
            Debug.LogWarning("Rewarded ad is not ready yet");
            OnAdFailedToShow?.Invoke();
            
            // Try to load a new ad
            LoadRewardedAd();
        }
    }
    
    /// <summary>
    /// Checks if rewarded ad is ready to show
    /// </summary>
    public bool IsRewardedAdReady()
    {
        return rewardedAd != null && rewardedAd.CanShowAd();
    }
    
    /// <summary>
    /// Registers event handlers for the rewarded ad
    /// </summary>
    private void RegisterEventHandlers(RewardedAd ad)
    {
        // Raised when the ad is estimated to have earned money
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"Rewarded ad paid {adValue.Value} {adValue.CurrencyCode}");
        };
        
        // Raised when an impression is recorded for an ad
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Rewarded ad recorded an impression");
        };
        
        // Raised when a click is recorded for an ad
        ad.OnAdClicked += () =>
        {
            Debug.Log("Rewarded ad was clicked");
        };
        
        // Raised when an ad opened full screen content
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Rewarded ad full screen content opened");
        };
        
        // Raised when the ad closed full screen content
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded ad full screen content closed");
            LoadRewardedAd(); // Reload ad for next time
        };
        
        // Raised when the ad failed to open full screen content
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Rewarded ad failed to open full screen content: " + error);
            OnAdFailedToShow?.Invoke();
            LoadRewardedAd(); // Try to load a new ad
        };
    }
    
    void OnDestroy()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
        }
    }
}
