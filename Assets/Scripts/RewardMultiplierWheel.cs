using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class RewardMultiplierWheel : MonoBehaviour
{
    [Header("UI Objects to Hide")]
    [SerializeField] private GameObject wheelObj;
    [SerializeField] private GameObject arrowObj;
    [SerializeField] private GameObject buttonObj;

    [Header("UI References")]
    [SerializeField] private RectTransform needle;
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI multiplierText;
    [SerializeField] private TextMeshProUGUI earnedCoinText;
    
    [Header("Settings")]
    [SerializeField] private float rotationSpeed = 4.5f;
    [SerializeField] private float maxAngle = 90f; 
    
    private bool isMoving = true;
    private float timer = 0f;

    private void OnEnable()
    {
        isMoving = true;
        timer = 0f; // Reset timer every time the panel is enabled
        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimButtonClicked);
        }
    }

    void Update()
    {
        if (isMoving && needle != null)
        {
            // Use unscaledTime so it works during Pause/GameOver
            float angle = Mathf.Sin(Time.unscaledTime * rotationSpeed) * maxAngle;
            needle.localRotation = Quaternion.Euler(0, 0, angle);
            
            int mult = CalculateMultiplier(angle);
            if (multiplierText != null)
            {
                multiplierText.text = "CLAIM x" + mult;
            }
        }
    }

    public void OnClaimButtonClicked()
    {
        if (!isMoving) return;
        
        isMoving = false;
        
        // Get the current Z rotation
        float finalAngle = needle.localRotation.eulerAngles.z;
        // Normalize to -180 to 180 range
        if (finalAngle > 180) finalAngle -= 360;
        
        int multiplier = CalculateMultiplier(finalAngle);
        Debug.Log($"Wheel stopped at {finalAngle} degrees. Multiplier: x{multiplier}");

        // Show Ad
        if (RewardedInterstitialManager.Instance != null && RewardedInterstitialManager.Instance.IsAdReady())
        {
            Action rewardHandler = null;
            Action cleanupHandler = null;

            rewardHandler = () => {
                ApplyReward(multiplier);
            };

            cleanupHandler = () => {
                RewardedInterstitialManager.Instance.OnRewardEarned -= rewardHandler;
                RewardedInterstitialManager.Instance.OnAdClosed -= cleanupHandler;
                RewardedInterstitialManager.Instance.OnAdFailedToShow -= cleanupHandler;
                
                // Hide all components
                if (wheelObj != null) wheelObj.SetActive(false);
                if (arrowObj != null) arrowObj.SetActive(false);
                if (buttonObj != null) buttonObj.SetActive(false);
                
                gameObject.SetActive(false);
            };
            
            RewardedInterstitialManager.Instance.OnRewardEarned += rewardHandler;
            RewardedInterstitialManager.Instance.OnAdClosed += cleanupHandler;
            RewardedInterstitialManager.Instance.OnAdFailedToShow += cleanupHandler;
            
            RewardedInterstitialManager.Instance.ShowAd();
        }
        else
        {
            // If ad not ready, just close (or handle as fallback)
            Debug.LogWarning("Ad not ready for multiplier. Closing wheel.");
            gameObject.SetActive(false);
        }
    }

    private int CalculateMultiplier(float angle)
    {
        float absAngle = Mathf.Abs(angle);
        
        // Based on a 180-degree arc (-90 to 90):
        // 0 - 20 degrees: x1 (Center)
        // 20 - 65 degrees: x2 (Middle)
        // 65 - 90 degrees: x3 (Edges)
        
        if (absAngle <= 20f) return 1;
        if (absAngle <= 65f) return 2;
        return 3;
    }

    private void ApplyReward(int multiplier)
    {
        if (GameSave.gameSave == null) return;

        int baseReward = GameSave.gameSave.lastEarnedReward;
        
        // If x1, no extra reward is added (base was already added in ChessSave)
        if (multiplier > 1)
        {
            int totalReward = baseReward * multiplier;
            int extraToAdd = totalReward - baseReward;

            if (extraToAdd > 0)
            {
                GameSave.gameSave.sv.coin += extraToAdd;
                SaveManager.Save(GameSave.gameSave.sv);
                GameSave.gameSave.CoinUpdate();

                // Update the earned coin text on UI directly via reference
                if (earnedCoinText != null)
                {
                    earnedCoinText.text = "+" + totalReward.ToString("N0", new System.Globalization.CultureInfo("tr-TR"));
                }

                Debug.Log($"✓✓✓ MULTIPLIER APPLIED - x{multiplier} gave extra {extraToAdd} coins!");
            }
        }
        else
        {
            Debug.Log("Multiplier was x1. Base reward remains unchanged.");
        }
    }
}
