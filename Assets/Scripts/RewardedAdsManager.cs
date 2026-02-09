using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;

public class RewardedAdsManager : MonoBehaviour
{
    [Header("AdMob Configuration")]
    [SerializeField] private string rewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    
    private RewardedAd rewardedAd;
    private bool isAdLoaded = false;
    private bool isLoading = false;
    private int retryCount = 0;
    private const int MAX_RETRIES = 5;
    
    // Singleton instance
    public static RewardedAdsManager Instance { get; private set; }
    
    // Events
    public System.Action OnRewardEarned;
    public System.Action OnAdFailedToLoad;
    public System.Action OnAdFailedToShow;
    public System.Action OnAdOpened;
    public System.Action OnAdClosed;
    
    void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // If another instance already exists, destroy this duplicate
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // Initialize Mobile Ads SDK
        MobileAds.Initialize(initStatus => {
            Debug.Log("AdMob initialized successfully");
            LoadRewardedAd();
        });
    }
    
    /*void Update()
    {
        // Optional: Check if ad is ready periodically
        if (!isAdLoaded && rewardedAd == null)
        {
            LoadRewardedAd();
        }
    }*/
    
    /// <summary>
    /// Loads a rewarded ad
    /// </summary>
    public void LoadRewardedAd()
    {
        // Prevent multiple simultaneous load requests
        if (isLoading)
        {
            Debug.Log("Ad is already loading, skipping duplicate request");
            return;
        }
        
        // Clean up the old ad before creating a new one
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }
        
        isLoading = true;
        isAdLoaded = false;
        Debug.Log("Loading rewarded ad...");
        
        // Create our request used to load the ad
        AdRequest adRequest = new AdRequest();
        
        // Send the request to load the ad
        RewardedAd.Load(rewardedAdUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            isLoading = false;
            
            // If error is not null, the load request failed
            if (error != null || ad == null)
            {
                string errorDetails = error != null 
                    ? $"Message: {error.GetMessage()}, Code: {error.GetCode()}, Domain: {error.GetDomain()}, Cause: {error.GetCause()}" 
                    : "Ad is null";
                Debug.LogError($"Rewarded ad failed to load: {errorDetails}");
                isAdLoaded = false;
                OnAdFailedToLoad?.Invoke();
                // Retry loading after a delay
                StartCoroutine(ReloadAdAfterDelay(3f));
                return;
            }
            
            Debug.Log("Rewarded ad loaded successfully");
            rewardedAd = ad;
            isAdLoaded = true;
            retryCount = 0; // Reset retry counter on successful load
            
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
            try
            {
                rewardedAd.Show((Reward reward) =>
                {
                    Debug.Log($"✓✓✓ REWARD EARNED - Rewarded ad completed! Reward: {reward.Amount} {reward.Type}");
                    Debug.Log("✓✓✓ Invoking OnRewardEarned callback...");
                    OnRewardEarned?.Invoke();
                    Debug.Log("✓✓✓ OnRewardEarned callback completed");
                    
                    // Don't reload here - OnAdFullScreenContentClosed will handle it
                });
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Exception while showing rewarded ad: {e.Message}");
                OnAdFailedToShow?.Invoke();
                // Clean up and reload
                if (rewardedAd != null)
                {
                    rewardedAd.Destroy();
                    rewardedAd = null;
                }
                isAdLoaded = false;
                StartCoroutine(ReloadAdAfterDelay(1f));
            }
        }
        else
        {
            Debug.LogWarning($"Rewarded ad is not ready yet. rewardedAd: {rewardedAd != null}, CanShowAd: {rewardedAd?.CanShowAd() ?? false}, isAdLoaded: {isAdLoaded}");
            OnAdFailedToShow?.Invoke();
            
            // Try to load a new ad if not already loading
            if (!isLoading && !isAdLoaded)
            {
                LoadRewardedAd();
            }
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
            Debug.Log("✓✓✓ IMPRESSION RECORDED - Rewarded ad recorded an impression (This is what AdMob counts!)");
        };
        
        // Raised when a click is recorded for an ad
        ad.OnAdClicked += () =>
        {
            Debug.Log("Rewarded ad was clicked");
        };
        
        // Raised when an ad opened full screen content
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("✓✓✓ AD OPENED - Rewarded ad full screen content opened (User can now see the ad)");
            OnAdOpened?.Invoke();
        };
        
        // Raised when the ad closed full screen content
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("✓✓✓ AD CLOSED - Rewarded ad full screen content closed");
            Debug.Log("NOTE: If impression was not recorded, user may have closed ad before completion");
            OnAdClosed?.Invoke();
            // Clean up the old ad
            rewardedAd = null;
            isAdLoaded = false;
            // Reload ad for next time after a short delay
            StartCoroutine(ReloadAdAfterDelay(0.5f));
        };
        
        // Raised when the ad failed to open full screen content
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"Rewarded ad failed to open full screen content: {error.GetMessage()} (Code: {error.GetCode()}, Domain: {error.GetDomain()}, Cause: {error.GetCause()})");
            OnAdFailedToShow?.Invoke();
            // Clean up the failed ad
            rewardedAd = null;
            isAdLoaded = false;
            // Try to load a new ad after a short delay
            StartCoroutine(ReloadAdAfterDelay(1f));
        };
    }
    
    /// <summary>
    /// Reloads ad after a delay to prevent too frequent requests
    /// </summary>
    private IEnumerator ReloadAdAfterDelay(float delay)
    {
        if (retryCount >= MAX_RETRIES)
        {
            Debug.LogError($"Max retry limit ({MAX_RETRIES}) reached for ad loading. Stopping retry attempts.");
            yield break;
        }
        
        retryCount++;
        Debug.Log($"Retrying ad load... Attempt {retryCount}/{MAX_RETRIES}");
        yield return new WaitForSeconds(delay);
        LoadRewardedAd();
    }
    
    void OnDestroy()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
        }
    }
}
