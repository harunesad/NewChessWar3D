using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Media;
#endif
namespace BodylinkSDK
{
    public enum AndroidPermission
    {
        ACCESS_COARSE_LOCATION,
        ACCESS_FINE_LOCATION,
        ADD_VOICEMAIL,
        BODY_SENSORS,
        CALL_PHONE,
        CAMERA,
        GET_ACCOUNTS,
        PROCESS_OUTGOING_CALLS,
        READ_CALENDAR,
        READ_CALL_LOG,
        READ_CONTACTS,
        READ_EXTERNAL_STORAGE,
        READ_PHONE_STATE,
        READ_SMS,
        RECEIVE_MMS,
        RECEIVE_SMS,
        RECEIVE_WAP_PUSH,
        RECORD_AUDIO,
        SEND_SMS,
        USE_SIP,
        WRITE_CALENDAR,
        WRITE_CALL_LOG,
        WRITE_CONTACTS,
        WRITE_EXTERNAL_STORAGE
    }

    public enum VideoEncoder
    {
        DEFAULT,
        H263,
        H264,
        HEVC,
        MPEG_4_SP,
        VP8
    }

    public class BodylinkScreenRecording : MonoBehaviour
    {
        private const float SCREEN_WIDTH = 1920;
        private const string ANDROID_SAVE_FOLDER_NAME = "Bodylink";
        private const string ANDROID_CALLBACK_GAME_OBJECT = "AndroidUtils";
        private const string VIDEO_NAME = "Record";

        public UnityAction onStartRecord;
        public UnityAction<string> onStopRecording;
        public UnityAction<Texture2D, string> onShareRecording;

        public static UnityAction onAllowCallback;
        public static UnityAction onDenyCallback;
        public static UnityAction onDenyAndNeverAskAgainCallback;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject androidRecorder;
    private bool isAudioEnabledForRecording = true;
#endif

        private string lastSavedVideoPath;
        private DateTime recordingStartUtc;
        private Coroutine resolveSavedVideoCoroutine;
        private Coroutine autoStopCoroutine;
        private UnityAction<string> pendingStopCallbacks;
        private bool isStopRequested;
        private bool isRecordingSessionActive;
        private VideoShareManager videoShareManager;

        public string LastSavedVideoPath => lastSavedVideoPath;

#if UNITY_EDITOR
        private int editorFrameRate = 30;
        private MediaEncoder editorMediaEncoder;
        private Texture2D editorFrameTexture;
        private Coroutine editorRecordingCoroutine;
        private string editorOutputPath;
        private long editorFrameIndex;
        private double editorNextCaptureTime;
        private double editorFrameInterval;
#endif

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (gameObject.name != ANDROID_CALLBACK_GAME_OBJECT)
                gameObject.name = ANDROID_CALLBACK_GAME_OBJECT;
#endif
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            androidRecorder = new AndroidJavaObject(
                "com.bodylink.sdk.BodylinkScreenRecorder",
                currentActivity,
                ANDROID_CALLBACK_GAME_OBJECT);
            androidRecorder.Call("setUpSaveFolder", ANDROID_SAVE_FOLDER_NAME);

            int width = (int)(Screen.width > SCREEN_WIDTH ? SCREEN_WIDTH : Screen.width);
            int height = Screen.width > SCREEN_WIDTH ? (int)(Screen.height * SCREEN_WIDTH / Screen.width) : Screen.height;
            int bitrate = (int)(1f * width * height / 100 * 240 * 7);
            int fps = 30;

            isAudioEnabledForRecording = !IsAndroidTv();
            androidRecorder.Call("setupVideo", width, height, bitrate, fps, isAudioEnabledForRecording, VideoEncoder.H264.ToString());
        }
#endif
        }

        public void StartRecording(int time = 0)
        {
            StopAutoStopTimer();
            isStopRequested = false;

            if (time > 0)
                autoStopCoroutine = StartCoroutine(AutoStopAfterSeconds(time));

#if UNITY_EDITOR
            StartEditorRecording();
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (isAudioEnabledForRecording && !BodylinkScreenRecording.IsPermitted(AndroidPermission.RECORD_AUDIO))
        {
            BodylinkScreenRecording.RequestPermission(AndroidPermission.RECORD_AUDIO);
            onAllowCallback = () =>
            {
                recordingStartUtc = DateTime.UtcNow;
                isRecordingSessionActive = true;
                androidRecorder.Call("startRecording");
            };
            onDenyCallback = () => { ShowToast("Need RECORD_AUDIO permission to record voice"); };
            onDenyAndNeverAskAgainCallback = () => { ShowToast("Need RECORD_AUDIO permission to record voice"); };
        }
        else
        {
            recordingStartUtc = DateTime.UtcNow;
            isRecordingSessionActive = true;
            androidRecorder.Call("startRecording");
        }
#endif
        }

        public void StopRecording(UnityAction<string> savedVideoPath = null)
        {
            if (savedVideoPath != null)
                pendingStopCallbacks += savedVideoPath;

            if (!isRecordingSessionActive && !isStopRequested)
            {
                if (savedVideoPath != null)
                    savedVideoPath.Invoke(lastSavedVideoPath);
                return;
            }

            if (isStopRequested)
                return;

            isStopRequested = true;
            StopAutoStopTimer();

#if UNITY_EDITOR
            StopEditorRecording();
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (androidRecorder != null)
            androidRecorder.Call("stopRecording");
        else
            InvokeRecordingStopped(null);
#endif
        }

        public void ShareRecording(string videoPath, UnityAction<Texture2D, string> onShareReady)
        {
            if (string.IsNullOrEmpty(videoPath))
                videoPath = lastSavedVideoPath;

            if (string.IsNullOrEmpty(videoPath))
            {
                Debug.LogWarning("ShareRecording failed. Video path is empty.");
                onShareReady?.Invoke(null, null);
                onShareRecording?.Invoke(null, null);
                return;
            }

            VideoShareManager shareManager = GetOrCreateVideoShareManager();
            if (shareManager == null)
            {
                Debug.LogWarning("ShareRecording failed. Could not get VideoShareManager.");
                onShareReady?.Invoke(null, null);
                onShareRecording?.Invoke(null, null);
                return;
            }

            shareManager.ShareVideo(videoPath, (qrTexture, shareUrl) =>
            {
                onShareReady?.Invoke(qrTexture, shareUrl);
                onShareRecording?.Invoke(qrTexture, shareUrl);
            });
        }

        public void StopSharing()
        {
            if (videoShareManager != null)
                videoShareManager.StopSharing();
        }

        private IEnumerator AutoStopAfterSeconds(int seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            autoStopCoroutine = null;
            StopRecording();
        }

        private void StopAutoStopTimer()
        {
            if (autoStopCoroutine != null)
            {
                StopCoroutine(autoStopCoroutine);
                autoStopCoroutine = null;
            }
        }

        private void InvokeRecordingStopped(string savedPath)
        {
            isRecordingSessionActive = false;
            isStopRequested = false;
            StopAutoStopTimer();

            if (!string.IsNullOrEmpty(savedPath))
                lastSavedVideoPath = savedPath;

            onStopRecording?.Invoke(savedPath);

            if (pendingStopCallbacks != null)
            {
                pendingStopCallbacks.Invoke(savedPath);
                pendingStopCallbacks = null;
            }
        }

        private VideoShareManager GetOrCreateVideoShareManager()
        {
            if (videoShareManager == null)
            {
                videoShareManager = FindFirstObjectByType<VideoShareManager>();
                if (videoShareManager == null)
                    videoShareManager = gameObject.AddComponent<VideoShareManager>();
            }

            return videoShareManager;
        }

#if UNITY_EDITOR
        private void StartEditorRecording()
        {
            if (!Application.isPlaying || editorMediaEncoder != null)
                return;

            string videosFolder = Path.Combine(Application.dataPath, "Videos");
            Directory.CreateDirectory(videosFolder);

            int captureWidth = Mathf.Max(2, Screen.width - (Screen.width % 2));
            int captureHeight = Mathf.Max(2, Screen.height - (Screen.height % 2));
            editorOutputPath = Path.Combine(videosFolder, $"{VIDEO_NAME}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            recordingStartUtc = DateTime.UtcNow;

            var videoAttributes = new VideoTrackAttributes
            {
                width = (uint)captureWidth,
                height = (uint)captureHeight,
                frameRate = new MediaRational(editorFrameRate),
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High
            };

            editorMediaEncoder = new MediaEncoder(editorOutputPath, videoAttributes);
            editorFrameTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
            editorFrameIndex = 0;
            editorFrameInterval = 1.0d / Mathf.Max(1, editorFrameRate);
            editorNextCaptureTime = Time.realtimeSinceStartupAsDouble;
            editorRecordingCoroutine = StartCoroutine(CaptureEditorFrames());

            isRecordingSessionActive = true;
            onStartRecord?.Invoke();
            Debug.Log($"Started editor recording: {editorOutputPath}");
        }

        private IEnumerator CaptureEditorFrames()
        {
            WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
            while (editorMediaEncoder != null)
            {
                yield return waitForEndOfFrame;
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < editorNextCaptureTime)
                    continue;

                editorFrameTexture.ReadPixels(new Rect(0, 0, editorFrameTexture.width, editorFrameTexture.height), 0, 0, false);
                editorFrameTexture.Apply(false, false);

                do
                {
                    editorMediaEncoder.AddFrame(
                        editorFrameTexture,
                        new MediaTime(editorFrameIndex, (uint)editorFrameRate, 1u));
                    editorFrameIndex++;
                    editorNextCaptureTime += editorFrameInterval;
                } while (now >= editorNextCaptureTime);
            }
        }

        private void StopEditorRecording()
        {
            if (editorMediaEncoder == null)
            {
                InvokeRecordingStopped(lastSavedVideoPath);
                return;
            }

            if (editorRecordingCoroutine != null)
            {
                StopCoroutine(editorRecordingCoroutine);
                editorRecordingCoroutine = null;
            }

            editorMediaEncoder.Dispose();
            editorMediaEncoder = null;

            if (editorFrameTexture != null)
            {
                Destroy(editorFrameTexture);
                editorFrameTexture = null;
            }

            AssetDatabase.Refresh();
            Debug.Log($"Saved editor recording: {editorOutputPath}");
            InvokeRecordingStopped(editorOutputPath);
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnNativeRecordingStopped()
    {
        if (resolveSavedVideoCoroutine != null)
            StopCoroutine(resolveSavedVideoCoroutine);
        resolveSavedVideoCoroutine = StartCoroutine(ResolveAndNotifyAndroidSavedVideo());
    }

    private IEnumerator ResolveAndNotifyAndroidSavedVideo()
    {
        const float timeoutSeconds = 8f;
        const float pollIntervalSeconds = 0.25f;
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        string resolvedPath = null;

        while (Time.realtimeSinceStartup < deadline)
        {
            resolvedPath = FindLatestAndroidVideoPath();
            if (!string.IsNullOrEmpty(resolvedPath))
                break;
            yield return new WaitForSecondsRealtime(pollIntervalSeconds);
        }

        resolveSavedVideoCoroutine = null;
        if (string.IsNullOrEmpty(resolvedPath))
            Debug.LogWarning("Recording finished but video path could not be resolved.");

        InvokeRecordingStopped(resolvedPath);
    }

    private string FindLatestAndroidVideoPath()
    {
        List<string> searchFolders = new List<string>
        {
            Path.Combine("/storage/emulated/0/Movies", ANDROID_SAVE_FOLDER_NAME),
            Path.Combine("/storage/emulated/0", ANDROID_SAVE_FOLDER_NAME),
            Path.Combine("/sdcard/Movies", ANDROID_SAVE_FOLDER_NAME),
            Path.Combine("/sdcard", ANDROID_SAVE_FOLDER_NAME)
        };

        DateTime minExpectedWriteTime = recordingStartUtc == default
            ? DateTime.MinValue
            : recordingStartUtc.AddSeconds(-2);

        string newestRecent = null;
        DateTime newestRecentTime = DateTime.MinValue;
        string newestAny = null;
        DateTime newestAnyTime = DateTime.MinValue;

        for (int i = 0; i < searchFolders.Count; i++)
        {
            string folder = searchFolders[i];
            if (!Directory.Exists(folder))
                continue;

            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.mp4");
            }
            catch
            {
                continue;
            }

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string filePath = files[fileIndex];
                DateTime writeTime;
                try
                {
                    writeTime = File.GetLastWriteTimeUtc(filePath);
                }
                catch
                {
                    continue;
                }

                if (writeTime > newestAnyTime)
                {
                    newestAnyTime = writeTime;
                    newestAny = filePath;
                }

                if (writeTime >= minExpectedWriteTime && writeTime > newestRecentTime)
                {
                    newestRecentTime = writeTime;
                    newestRecent = filePath;
                }
            }
        }

        return !string.IsNullOrEmpty(newestRecent) ? newestRecent : newestAny;
    }
#endif

        [Preserve]
        public void VideoRecorderCallback(string message)
        {
            switch (message)
            {
                case "init_record_error":
                    break;
                case "start_record":
                    isRecordingSessionActive = true;
                    onStartRecord?.Invoke();
                    break;
                case "stop_record":
#if UNITY_ANDROID && !UNITY_EDITOR
                OnNativeRecordingStopped();
#else
                    InvokeRecordingStopped(lastSavedVideoPath);
#endif
                    break;
            }
        }

        private bool IsAndroidTv()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass packageManagerClass = new AndroidJavaClass("android.content.pm.PackageManager"))
        using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            string featureLeanback = packageManagerClass.GetStatic<string>("FEATURE_LEANBACK");
            return currentActivity != null &&
                   currentActivity.Call<AndroidJavaObject>("getPackageManager").Call<bool>("hasSystemFeature", featureLeanback);
        }
#else
            return false;
#endif
        }

        [Preserve]
        public void OnAllow(string _)
        {
            onAllowCallback?.Invoke();
            ResetAllCallBacks();
        }

        [Preserve]
        public void OnDeny(string _)
        {
            onDenyCallback?.Invoke();
            ResetAllCallBacks();
        }

        [Preserve]
        public void OnDenyAndNeverAskAgain(string _)
        {
            onDenyAndNeverAskAgainCallback?.Invoke();
            ResetAllCallBacks();
        }

        private void ResetAllCallBacks()
        {
            onAllowCallback = null;
            onDenyCallback = null;
            onDenyAndNeverAskAgainCallback = null;
        }

        public static bool IsPermitted(AndroidPermission permission)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaClass recorderClass = new AndroidJavaClass("com.bodylink.sdk.BodylinkScreenRecorder"))
        {
            return recorderClass.CallStatic<bool>("hasPermission", currentActivity, GetPermissionStrr(permission));
        }
#endif
            return true;
        }

        public static void RequestPermission(AndroidPermission permission, UnityAction onAllow = null, UnityAction onDeny = null, UnityAction onDenyAndNeverAskAgain = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        onAllowCallback = onAllow;
        onDenyCallback = onDeny;
        onDenyAndNeverAskAgainCallback = onDenyAndNeverAskAgain;
        using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaClass recorderClass = new AndroidJavaClass("com.bodylink.sdk.BodylinkScreenRecorder"))
        {
            recorderClass.CallStatic("requestPermission", currentActivity, GetPermissionStrr(permission), ANDROID_CALLBACK_GAME_OBJECT);
        }
#endif
        }

        private static string GetPermissionStrr(AndroidPermission permission)
        {
            return "android.permission." + permission;
        }

        public static void ShowToast(string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject currentActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
        currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            new AndroidJavaClass("android.widget.Toast")
                .CallStatic<AndroidJavaObject>("makeText", currentActivity.Call<AndroidJavaObject>("getApplicationContext"), new AndroidJavaObject("java.lang.String", message), 0)
                .Call("show");
        }));
#endif
        }

        public static void ShareAndroid(string body, string subject, string url, string filePath, string mimeType, bool chooser, string chooserText)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
        using (AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent"))
        {
            using (intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"))) { }
            using (intentObject.Call<AndroidJavaObject>("setType", mimeType)) { }
            using (intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject)) { }
            using (intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), body)) { }

            if (!string.IsNullOrEmpty(url))
            {
                using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
                using (AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("parse", url))
                using (intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uriObject)) { }
            }
            else if (filePath != null)
            {
                using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
                using (AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + filePath))
                using (intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uriObject)) { }
            }

            using (AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (chooser)
                {
                    AndroidJavaObject jChooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, chooserText);
                    currentActivity.Call("startActivity", jChooser);
                }
                else
                {
                    currentActivity.Call("startActivity", intentObject);
                }
            }
        }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (editorMediaEncoder != null)
                StopEditorRecording();
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (resolveSavedVideoCoroutine != null)
        {
            StopCoroutine(resolveSavedVideoCoroutine);
            resolveSavedVideoCoroutine = null;
        }
#endif

            StopAutoStopTimer();
            StopSharing();
        }
    }
}
