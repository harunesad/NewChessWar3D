using System;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    private const string ChannelId = "chess_war_reminders";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeNotifications();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeNotifications()
    {
#if UNITY_ANDROID
        // 1. Bildirim kanalını oluştur
        var channel = new AndroidNotificationChannel()
        {
            Id = ChannelId,
            Name = "Chess War Reminders",
            Importance = Importance.High,
            Description = "Daily rewards and battle reminders",
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);

        // 2. Uygulama açıldığında bekleyen tüm bildirimleri temizle
        AndroidNotificationCenter.CancelAllNotifications();
#endif
        
        // 3. Standart geri çağırma bildirimlerini planla
        ScheduleRetentionNotifications();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Uygulama arka plana geçtiğinde bildirimleri tekrar planla
            ScheduleRetentionNotifications();
        }
        else
        {
            // Uygulama geri geldiğinde bildirimleri temizle
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
#endif
        }
    }

    public void ScheduleRetentionNotifications()
    {
#if UNITY_ANDROID
        // 24 Saat Sonra (Geri Çağırma)
        SendNotification("Your army awaits! ⚔️", "Protect your King and claim your victory today!", 24);

        // 72 Saat Sonra (Geri Çağırma)
        SendNotification("Long time no see, Commander! ♟️", "Your strategy skills might get rusty. Let's play a match!", 72);
#endif
    }

    public void ScheduleDailyRewardNotification(TimeSpan delay)
    {
#if UNITY_ANDROID
        // Mevcut günlük ödül bildirimini temizle (varsa)
        // Not: Basitlik adına CancelAll kullanıyoruz ama ID bazlı da yapılabilir.

        SendNotification("Golden Reward Ready! 💰", "Your daily gift is waiting for you. Come and get it!", (float)delay.TotalHours);
#endif
        Debug.Log("Daily reward notification scheduled in " + delay.TotalHours + " hours.");
    }

    private void SendNotification(string title, string text, float fireTimeInHours)
    {
#if UNITY_ANDROID
        var notification = new AndroidNotification
        {
            Title = title,
            Text = text,
            FireTime = DateTime.Now.AddHours(fireTimeInHours),
            SmallIcon = "small_icon_0", // Unity'de proje ayarlarında tanımlanmalı
            LargeIcon = "large_icon_0"
        };

        AndroidNotificationCenter.SendNotification(notification, ChannelId);
#endif
    }
}
