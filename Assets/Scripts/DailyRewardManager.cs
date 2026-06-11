using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class DailyRewardManager : MonoBehaviour
{
    [Header("UI Panelleri")]
    [SerializeField] CanvasGroup rewardMenu;
    [SerializeField] List<Button> dayButtons; // 7 adet buton (Sırasıyla 1-7. günler)
    
    [Header("Görsel Ayarlar")]
    [SerializeField] Color availableColor = Color.white;
    [SerializeField] Color claimedColor = Color.gray;
    [SerializeField] Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Ödül Değerleri (7 Gün)")]
    [SerializeField] int[] coinRewards = { 10, 0, 25, 0, 50, 0, 150 };
    [SerializeField] int[] healthRewards = { 0, 1, 0, 1, 0, 2, 0 };

    private const string LAST_CLAIM_DATE = "LastClaimDate";
    private const string REWARD_STREAK = "RewardStreak"; // 0-6 arası index

    void Start()
    {
        // Butonlara tıklama olaylarını bağla
        for (int i = 0; i < dayButtons.Count; i++)
        {
            int index = i;
            dayButtons[i].onClick.AddListener(() => OnDayClick(index));
        }

        // Başlangıçta sadece UI durumlarını (renkler, streak kontrolü) güncelle
        Invoke("UpdateUI", 0.5f);
    }

    public void UpdateUI()
    {
        if (JsonSave.jsonSave == null || JsonSave.jsonSave.sv == null) return;

        int currentStreak = PlayerPrefs.GetInt(REWARD_STREAK, 0);
        string lastClaim = PlayerPrefs.GetString(LAST_CLAIM_DATE, "");
        bool alreadyClaimedToday = IsAlreadyClaimedToday(lastClaim);

        // Eğer gün atlandıysa (Streak bozulduysa)
        if (!alreadyClaimedToday && !IsStreakBroken(lastClaim))
        {
            // Devam ediyor
        }
        else if (!alreadyClaimedToday && IsStreakBroken(lastClaim))
        {
            // Streak bozuldu, başa dön
            currentStreak = 0;
            PlayerPrefs.SetInt(REWARD_STREAK, 0);
        }

        for (int i = 0; i < dayButtons.Count; i++)
        {
            Image btnImg = dayButtons[i].GetComponent<Image>();
            
            if (i < currentStreak)
            {
                // ÖNCEKİ GÜNLER (Zaten alındı)
                btnImg.color = claimedColor;
                dayButtons[i].interactable = true;
            }
            else if (i == currentStreak)
            {
                if (alreadyClaimedToday)
                {
                    // BUGÜNÜN ÖDÜLÜ ZATEN ALINDI, BU BİR SONRAKİ GÜN (Kilitli görünmeli)
                    btnImg.color = lockedColor;
                    dayButtons[i].interactable = true;
                }
                else
                {
                    // ŞİMDİ ALINABİLİR
                    btnImg.color = availableColor;
                    dayButtons[i].interactable = true;
                }
            }
            else
            {
                // GELECEK GÜNLER (Kilitli)
                btnImg.color = lockedColor;
                dayButtons[i].interactable = true;
            }
        }
    }

    void OnDayClick(int dayIndex)
    {
        int currentStreak = PlayerPrefs.GetInt(REWARD_STREAK, 0);
        string lastClaim = PlayerPrefs.GetString(LAST_CLAIM_DATE, "");
        bool alreadyClaimedToday = IsAlreadyClaimedToday(lastClaim);

        if (dayIndex < currentStreak)
        {
            ShowMessage("Bu ödülü zaten aldın!");
        }
        else if (dayIndex == currentStreak)
        {
            if (alreadyClaimedToday)
            {
                ShowMessage("Bugünkü ödülünü aldın! Yarın tekrar gel.");
            }
            else
            {
                ClaimReward(dayIndex);
            }
        }
        else
        {
            ShowMessage("Bu ödül henüz kilitli. Yarın tekrar gel!");
        }
    }

    void ClaimReward(int index)
    {
        if (JsonSave.jsonSave != null && JsonSave.jsonSave.sv != null)
        {
            // Ödülleri ver
            JsonSave.jsonSave.sv.coin += coinRewards[index];
            JsonSave.jsonSave.sv.health += healthRewards[index];
            
            // Kaydet
            SaveManager.Save(JsonSave.jsonSave.sv);
            JsonSave.jsonSave.CoinUpdate();
            JsonSave.jsonSave.HealthUpdate();
            
            // PlayerPrefs güncelle
            PlayerPrefs.SetString(LAST_CLAIM_DATE, DateTime.Today.ToString());
            
            // Eğer 7. gün ise başa dön, değilse artır
            int nextStreak = (index + 1) % 7;
            PlayerPrefs.SetInt(REWARD_STREAK, nextStreak);
            PlayerPrefs.Save();


            // Bildirim Planla: Yarın sabah 09:00 için (veya 24 saat sonra)
            if (NotificationManager.Instance != null)
            {
                // Basitlik için 24 saat sonrasına kuruyoruz
                NotificationManager.Instance.ScheduleDailyRewardNotification(TimeSpan.FromHours(24));
            }

            ShowMessage("Tebrikler! Ödülünü aldın.");
            UpdateUI();
        }
    }

    bool IsAlreadyClaimedToday(string lastClaim)
    {
        if (string.IsNullOrEmpty(lastClaim)) return false;
        DateTime lastDate = DateTime.Parse(lastClaim);
        return lastDate.Date == DateTime.Today;
    }

    bool IsStreakBroken(string lastClaim)
    {
        if (string.IsNullOrEmpty(lastClaim)) return false;
        DateTime lastDate = DateTime.Parse(lastClaim);
        return (DateTime.Today - lastDate.Date).Days > 1;
    }

    void ShowMessage(string msg)
    {
        MenuUIManager menuUI = FindObjectOfType<MenuUIManager>();
        if (menuUI != null)
        {
            menuUI.MessageShow(msg);
        }
        else
        {
            Debug.Log(msg);
        }
    }

    public void ClosePanel()
    {
        if (rewardMenu != null)
        {
            rewardMenu.alpha = 0;
            rewardMenu.interactable = false;
            rewardMenu.blocksRaycasts = false;
        }
    }

    // --- TEST / DEBUG FONKSIYONLARI ---
    // Bu metodları Inspector'daki componente sağ tıklayarak (ContextMenu) çalıştırabilirsin.

    [ContextMenu("DEBUG: Verileri Sıfırla")]
    public void DebugResetData()
    {
        PlayerPrefs.DeleteKey(LAST_CLAIM_DATE);
        PlayerPrefs.SetInt(REWARD_STREAK, 0);
        PlayerPrefs.Save();
        UpdateUI();
        Debug.Log("Daily Reward verileri sıfırlandı.");
    }

    [ContextMenu("DEBUG: Bir Gün Atla (Simüle Et)")]
    public void DebugSkipToNextDay()
    {
        // Son alınan tarihi dün yaparsak, kod bugün girilmiş gibi davranır
        DateTime yesterday = DateTime.Today.AddDays(-1);
        PlayerPrefs.SetString(LAST_CLAIM_DATE, yesterday.ToString());
        PlayerPrefs.Save();
        UpdateUI();
        Debug.Log("Bir gün atlandı simüle edildi. Şimdi ödül alabilirsin.");
    }
}
