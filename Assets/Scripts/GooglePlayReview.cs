using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Google.Play.Review;
using TMPro;

public class GooglePlayReview : MonoBehaviour
{
    [SerializeField] private Button reviewButton;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool fallbackToPlayStore = true; // Dialog gösterilmezse Play Store'a yönlendir
    [SerializeField] private bool showPlayStoreAfterSuccess = false; // Başarılı durumda da Play Store'a yönlendir (dialog görünmüyorsa)
    [SerializeField] private float playStoreDelayAfterSuccess = 2f; // Başarılı durumda kaç saniye sonra Play Store'a yönlendir
    [SerializeField] private string packageName = "com.HEANDAMS.ChessWar3D"; // Package name
    
    private ReviewManager _reviewManager;
    private PlayReviewInfo _playReviewInfo;
    private bool _isRequestingReview = false;
    private bool _dialogShown = false;

    void Start()
    {
        _reviewManager = new ReviewManager();
        
        // Buton varsa onClick event'ine metodu bağla
        if (reviewButton != null)
        {
            reviewButton.onClick.AddListener(RequestReview);
            LogDebug("Review butonu bağlandı");
        }
        else
        {
            LogDebug("UYARI: Review butonu atanmamış!");
        }
    }

    /// <summary>
    /// Google Play In-App Review penceresini açar.
    /// UI butonundan çağrılabilir.
    /// </summary>
    public void RequestReview()
    {
        LogDebug("RequestReview() çağrıldı");
        
        if (_isRequestingReview)
        {
            LogDebug("Review zaten isteniyor, lütfen bekleyin...");
            return;
        }

        if (_reviewManager == null)
        {
            LogDebug("HATA: ReviewManager null!");
            _reviewManager = new ReviewManager();
        }

        StartCoroutine(RequestAndLaunchReviewCoroutine());
    }

    /// <summary>
    /// Review flow'u başlatır ve gösterir.
    /// Kullanıcılar oyundan çıkmadan yorum yapabilir.
    /// </summary>
    private IEnumerator RequestAndLaunchReviewCoroutine()
    {
        _isRequestingReview = true;
        
        LogDebug("=== Google Play In-App Review başlatılıyor ===");

        // UNITY EDITOR KONTROLÜ
        if (Application.isEditor)
        {
            LogDebug("UYARI: Google Play In-App Review sadece gerçek Android cihazlarda çalışır!");
            ShowMessageToUser("Review system works on Android devices only (Editor fallback)");
            if (fallbackToPlayStore)
            {
                OpenPlayStorePage();
            }
            _isRequestingReview = false;
            yield break;
        }

        // Review bilgisini al
        LogDebug("1. Adım: RequestReviewFlow() çağrılıyor...");
        var requestFlowOperation = _reviewManager.RequestReviewFlow();
        yield return requestFlowOperation;

        LogDebug($"RequestReviewFlow tamamlandı. Error: {requestFlowOperation.Error}");

        // Hata kontrolü
        if (requestFlowOperation.Error != ReviewErrorCode.NoError)
        {
            string errorMessage = GetErrorMessage(requestFlowOperation.Error);
            LogDebug($"HATA - Review isteği başarısız: {errorMessage} (Error Code: {requestFlowOperation.Error})");

            // Eğer Play Store hatasıysa veya uygulama yüklü değilse fallback yap
            if (fallbackToPlayStore)
            {
                OpenPlayStorePage();
            }
            else
            {
                ShowMessageToUser(errorMessage);
            }
            
            _isRequestingReview = false;
            yield break;
        }

        _playReviewInfo = requestFlowOperation.GetResult();
        LogDebug("2. Adım: Review bilgisi başarıyla alındı!");
        if (_playReviewInfo == null)
        {
            LogDebug("HATA: PlayReviewInfo null!");
            if (fallbackToPlayStore) OpenPlayStorePage();
            _isRequestingReview = false;
            yield break;
        }

        // UI'ın güncellenmesi için kısa bir bekleme
        yield return new WaitForSecondsRealtime(0.1f);

        // Review dialog'unu göster
        LogDebug("3. Adım: LaunchReviewFlow() çağrılıyor...");
        _dialogShown = false;
        
        var launchFlowOperation = _reviewManager.LaunchReviewFlow(_playReviewInfo);
        yield return launchFlowOperation;

        LogDebug($"LaunchReviewFlow tamamlandı. Error: {launchFlowOperation.Error}");

        // Hata kontrolü
        if (launchFlowOperation.Error != ReviewErrorCode.NoError)
        {
            string errorMessage = GetErrorMessage(launchFlowOperation.Error);
            LogDebug($"HATA - Review dialog açılamadı: {errorMessage} (Error Code: {launchFlowOperation.Error})");
            
            if (fallbackToPlayStore)
            {
                OpenPlayStorePage();
            }
            else
            {
                ShowMessageToUser(errorMessage);
            }
        }
        else
        {
            LogDebug("✓ BAŞARILI: Google Play In-App Review dialog'u gösterildi!");
            _dialogShown = true;
            
            // ÖNEMLİ NOT: Google bazen API başarılı olsa bile kota dolduğu için pencereyi göstermez.
            // Bu durumda kullanıcıya "Teşekkürler" mesajı veya Play Store yönlendirmesi yapılabilir.

            if (showPlayStoreAfterSuccess && fallbackToPlayStore)
            {
                StartCoroutine(OpenPlayStoreAfterDelay());
            }
        }

        _playReviewInfo = null;
        _isRequestingReview = false;
        LogDebug("=== Review işlemi tamamlandı ===");
    }

    private string GetErrorMessage(ReviewErrorCode errorCode)
    {
        switch (errorCode)
        {
            case ReviewErrorCode.NoError:
                return "Hata yok";
            case ReviewErrorCode.ErrorRequestingFlow:
                return "Review isteği başarısız. Uygulama Google Play Store'dan indirilmiş olmalı.";
            case ReviewErrorCode.ErrorLaunchingFlow:
                return "Review dialog açılamadı. Google Play Services güncel olmalı.";
            case ReviewErrorCode.PlayStoreNotFound:
                return "Google Play Store bulunamadı. Uygulama Google Play Store'dan indirilmiş olmalı.";
            default:
                return $"Bilinmeyen hata: {errorCode}";
        }
    }

    private void ShowMessageToUser(string message)
    {
        // 1. Önce Menu UI Manager'ı dene
        MenuUIManager menuUI = FindObjectOfType<MenuUIManager>();
        if (menuUI != null)
        {
            menuUI.MessageShow(message);
            return;
        }

        // 2. Olmazsa Game UI Manager'ı dene
        GameUIManager gameUI = FindObjectOfType<GameUIManager>();
        if (gameUI != null)
        {
            gameUI.MessageShow(message);
        }
    }

    /// <summary>
    /// Başarılı durumda belirli bir süre sonra Play Store'a yönlendir
    /// </summary>
    private IEnumerator OpenPlayStoreAfterDelay()
    {
        yield return new WaitForSecondsRealtime(playStoreDelayAfterSuccess);
        LogDebug("Başarılı durumda Play Store'a yönlendiriliyor (dialog görünmüyorsa)...");
        OpenPlayStorePage();
    }

    /// <summary>
    /// Play Store sayfasını açar (fallback mekanizması)
    /// UI butonundan da çağrılabilir
    /// </summary>
    public void OpenPlayStorePage()
    {
        string playStoreUrl = $"market://details?id={packageName}";
        string webUrl = $"https://play.google.com/store/apps/details?id={packageName}";
        
        LogDebug($"Play Store URL açılıyor: {playStoreUrl}");
        
        // Önce market:// protokolünü dene (Play Store uygulaması)
        try
        {
            Application.OpenURL(playStoreUrl);
            LogDebug("Play Store uygulaması açıldı");
        }
        catch
        {
            // Play Store uygulaması yoksa web tarayıcısında aç
            LogDebug("Play Store uygulaması bulunamadı, web tarayıcısında açılıyor...");
            Application.OpenURL(webUrl);
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GooglePlayReview] {message}");
        }
    }
}
