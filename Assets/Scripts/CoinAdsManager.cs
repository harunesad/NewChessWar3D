using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class CoinAdsManager : MonoBehaviour
{
    [SerializeField] List<CoinAdsManagerUI> coinAdsManagerUI;
    [SerializeField] RewardedAdsManager rewardedAdsManager;
    [SerializeField] MenuUIManager menuUIManager;
    [SerializeField] List<int> coinAmounts;
    JsonSave jsonSave;
    
    // Store current event handlers to properly unsubscribe
    private System.Action currentRewardHandler;
    private System.Action currentFailureHandler;
    
    private void CleanupEventHandlers()
    {
        if (rewardedAdsManager != null)
        {
            if (currentRewardHandler != null)
            {
                rewardedAdsManager.OnRewardEarned -= currentRewardHandler;
                currentRewardHandler = null;
            }
            if (currentFailureHandler != null)
            {
                rewardedAdsManager.OnAdFailedToShow -= currentFailureHandler;
                currentFailureHandler = null;
            }
            rewardedAdsManager.OnAdClosed -= CleanupEventHandlers;
        }
    }
    private int currentAdIndex = -1;
    private void Awake()
    {
        jsonSave = FindAnyObjectByType<JsonSave>();
        for (int i = 0; i < coinAdsManagerUI.Count; i++)
        {
            int j = i;
            coinAdsManagerUI[i].coinAdsBtn.onClick.AddListener(delegate { CoinAds(j); });
        }
    }
    void Start()
    {
        CheckDailyReset();
        UpdateUI();
    }
    
    void CheckDailyReset()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string lastResetDate = jsonSave.sv.lastAdsResetDate;
        
        // Eğer bugün farklı bir günse veya ilk kez çalışıyorsa sıfırla
        if (lastResetDate != today)
        {
            jsonSave.sv.adsCoin = 0;
            jsonSave.sv.lastAdsResetDate = today;
            SaveManager.Save(jsonSave.sv);
        }
    }
    
    void UpdateUI()
    {
        for (int i = 0; i < jsonSave.sv.adsCoin; i++)
        {
            coinAdsManagerUI[i].coinAdsBtn.transform.GetChild(0).gameObject.SetActive(true);
            coinAdsManagerUI[i].coinAdsBtn.interactable = false;
        }
        if (jsonSave.sv.adsCoin < 6)
        {
            for (int i = jsonSave.sv.adsCoin; i < jsonSave.sv.adsCoin + 1; i++)
            {
                coinAdsManagerUI[i].coinAdsBtn.interactable = true;
            }
        }
        if (jsonSave.sv.adsCoin < 5)
        {
            for (int i = jsonSave.sv.adsCoin + 1; i < coinAdsManagerUI.Count; i++)
            {
                coinAdsManagerUI[i].coinAdsBtn.interactable = false;
            }
        }
    }
    public void CoinAds(int index)
    {
        // Show rewarded ad first
        if (rewardedAdsManager != null && rewardedAdsManager.IsRewardedAdReady())
        {
            // Unsubscribe from previous events to prevent duplicate calls
            CleanupEventHandlers();
            
            // Store the current index
            currentAdIndex = index;
            
            // Create new handlers
            currentRewardHandler = () => OnRewardEarned(currentAdIndex);
            currentFailureHandler = OnAdFailed;
            
            // Subscribe to events
            rewardedAdsManager.OnRewardEarned += currentRewardHandler;
            rewardedAdsManager.OnAdFailedToShow += currentFailureHandler;
            rewardedAdsManager.OnAdClosed += CleanupEventHandlers;
            
            // Show the rewarded ad
            rewardedAdsManager.ShowRewardedAd();
        }
        else
        {
            if (menuUIManager != null)
            {
                menuUIManager.MessageShow("Ad Not Ready");
            }
            Debug.LogWarning("Rewarded ad is not ready yet");
            
            // Try to load the ad if it's not ready
            if (rewardedAdsManager != null)
            {
                rewardedAdsManager.LoadRewardedAd();
            }
        }
    }
    
    private void OnRewardEarned(int index)
    {
        // Unsubscribe to prevent multiple calls
        CleanupEventHandlers();

        // Get coin amount from TextMeshPro text
        //int coinAmount = GetCoinAmountFromText(index);
        int coinAmount = coinAmounts[index];
        // Increment coins based on TextMeshPro text value
        jsonSave.sv.adsCoin++;
        jsonSave.sv.coin += coinAmount;
        SaveManager.Save(jsonSave.sv);
        UpdateUI();
        jsonSave.CoinUpdate();
        
        Debug.Log($"✓✓✓ COINS ADDED - Reward earned: {coinAmount} coins for ad index {index}");
        Debug.Log($"✓✓✓ Total coins after reward: {jsonSave.sv.coin}, Ads watched today: {jsonSave.sv.adsCoin}");
        currentAdIndex = -1;
    }
    
    private void OnAdFailed()
    {
        // Unsubscribe to prevent multiple calls
        CleanupEventHandlers();
        
        if (menuUIManager != null)
        {
            menuUIManager.MessageShow("Ad Failed");
        }
        Debug.LogWarning("Rewarded ad failed to show");
        currentAdIndex = -1;
    }
    
    private int GetCoinAmountFromText(int index)
    {
        if (index < coinAdsManagerUI.Count && coinAdsManagerUI[index].coinAdsBtn != null)
        {
            // Look for TextMeshPro component in child objects
            Text textComponent = coinAdsManagerUI[index].coinAdsBtn.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                string text = textComponent.text;
                if (int.TryParse(text, out int coinAmount))
                {
                    return coinAmount;
                }
            }
        }
        
        // Default to 1 if text parsing fails
        return 1;
    }
}
[System.Serializable]
public class CoinAdsManagerUI
{
    public Button coinAdsBtn;
}
