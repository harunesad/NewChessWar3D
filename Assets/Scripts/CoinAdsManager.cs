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
    JsonSave jsonSave;
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
            coinAdsManagerUI[i].coinAdsBtn.transform.GetChild(1).gameObject.SetActive(true);
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
            // Clear any existing listeners first
            rewardedAdsManager.OnRewardEarned = null;
            rewardedAdsManager.OnAdFailedToShow = null;
            
            // Subscribe to reward earned event
            rewardedAdsManager.OnRewardEarned += () => OnRewardEarned(index);
            rewardedAdsManager.OnAdFailedToShow += () => OnAdFailed();
            
            // Show the rewarded ad
            rewardedAdsManager.ShowRewardedAd();
        }
        else
        {
            menuUIManager.SendMessage("Rewarded ad is not ready yet");
            Debug.LogWarning("Rewarded ad is not ready yet");
        }
    }
    
    private void OnRewardEarned(int index)
    {
        // Get coin amount from TextMeshPro text
        int coinAmount = GetCoinAmountFromText(index);
        
        // Increment coins based on TextMeshPro text value
        jsonSave.sv.adsCoin++;
        jsonSave.sv.coin += coinAmount;
        SaveManager.Save(jsonSave.sv);
        UpdateUI();
        jsonSave.CoinUpdate();
    }
    
    private void OnAdFailed()
    {
        menuUIManager.SendMessage("Rewarded ad failed to show");
        Debug.LogWarning("Rewarded ad failed to show");
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
