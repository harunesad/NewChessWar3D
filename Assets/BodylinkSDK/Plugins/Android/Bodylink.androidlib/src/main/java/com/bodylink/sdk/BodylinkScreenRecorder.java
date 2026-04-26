package com.bodylink.sdk;

import android.Manifest;
import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.media.MediaScannerConnection;
import android.media.projection.MediaProjectionManager;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.util.Log;
import android.widget.Toast;

import com.hbisoft.hbrecorder.HBRecorder;
import com.hbisoft.hbrecorder.HBRecorderListener;
import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

public class BodylinkScreenRecorder implements HBRecorderListener {
    private static final String TAG = "BodylinkScreenRecorder";
    private static final String DEFAULT_GAME_OBJECT = "AndroidUtils";

    private final Activity activity;
    private final HBRecorder hbRecorder;
    private String gameObject;
    private String saveFolder = "Bodylink";
    private ContentValues contentValues;
    private Uri outputUri;
    private boolean customSetting;

    public BodylinkScreenRecorder(Activity activity, String gameObject) {
        this.activity = activity;
        this.gameObject = gameObject == null || gameObject.length() == 0 ? DEFAULT_GAME_OBJECT : gameObject;
        this.hbRecorder = new HBRecorder(activity, this);
    }

    public void setUpSaveFolder(String folderName) {
        if (folderName != null && folderName.length() > 0) {
            saveFolder = folderName;
        }
    }

    public void setupVideo(int width, int height, int bitrate, int fps, boolean audioEnabled) {
        setupVideo(width, height, bitrate, fps, audioEnabled, null);
    }

    public void setupVideo(int width, int height, int bitrate, int fps, boolean audioEnabled, String encoder) {
        hbRecorder.enableCustomSettings();
        hbRecorder.setScreenDimensions(height, width);
        hbRecorder.setVideoFrameRate(fps);
        hbRecorder.setVideoBitrate(bitrate);

        if (encoder != null && encoder.length() > 0) {
            hbRecorder.setVideoEncoder(encoder);
        }

        hbRecorder.setAudioBitrate(128000);
        hbRecorder.setAudioSamplingRate(44100);
        hbRecorder.isAudioEnabled(audioEnabled);
        customSetting = true;
    }

    public void startRecording() {
        if (!customSetting) {
            applyQuickSettings();
        }

        MediaProjectionManager projectionManager =
                (MediaProjectionManager) activity.getSystemService(Context.MEDIA_PROJECTION_SERVICE);
        if (projectionManager == null) {
            HBRecorderOnError(-1, "MediaProjectionManager is unavailable.");
            return;
        }

        BodylinkScreenRecordPermissionActivity.start(activity, projectionManager.createScreenCaptureIntent(), this);
    }

    public void onScreenCapturePermissionResult(Activity resultActivity, int resultCode, Intent data) {
        if (resultCode != Activity.RESULT_OK || data == null) {
            sendUnityMessage("init_record_error");
            return;
        }

        setOutputPath();
        sendUnityMessage("start_record");
        hbRecorder.startScreenRecording(data, resultCode, resultActivity);
    }

    public void stopRecording() {
        hbRecorder.stopScreenRecording();
        sendUnityMessage("stop_record");
    }

    public PackageManager getPackageManager() {
        return activity.getPackageManager();
    }

    public boolean hasPermission(String permission) {
        return hasPermission(activity, permission);
    }

    public void requestPermission(String permission) {
        requestPermission(activity, permission, gameObject);
    }

    public static boolean hasPermission(Activity activity, String permission) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) {
            return true;
        }

        return activity != null &&
                activity.getApplicationContext().checkCallingOrSelfPermission(permission) == PackageManager.PERMISSION_GRANTED;
    }

    public static void requestPermission(Activity activity, String permission, String gameObject) {
        if (activity == null || hasPermission(activity, permission)) {
            UnityPlayer.UnitySendMessage(resolveGameObject(gameObject), "OnAllow", "");
            return;
        }

        BodylinkPermissionActivity.request(activity, permission, resolveGameObject(gameObject));
    }

    @Override
    public void HBRecorderOnStart() {
        Log.i(TAG, "HBRecorderOnStart");
        sendUnityMessage("start_record");
    }

    @Override
    public void HBRecorderOnComplete() {
        showLongToast("Saved Successfully");

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP && hbRecorder.wasUriSet()) {
            updateGalleryUri();
        } else {
            refreshGalleryFile();
        }

        sendUnityMessage("stop_record");
    }

    @Override
    public void HBRecorderOnError(int errorCode, String reason) {
        if (errorCode == 38) {
            showLongToast("Some settings are not supported by your device");
        } else {
            showLongToast("HBRecorderOnError - See Log");
            Log.e(TAG, reason == null ? "Unknown recorder error" : reason);
        }

        sendUnityMessage("init_record_error");
    }

    private void setOutputPath() {
        String fileName = generateFileName();

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            contentValues = new ContentValues();
            contentValues.put(MediaStore.Video.Media.RELATIVE_PATH, Environment.DIRECTORY_MOVIES + "/" + saveFolder);
            contentValues.put(MediaStore.Video.Media.TITLE, fileName);
            contentValues.put(MediaStore.Video.Media.DISPLAY_NAME, fileName);
            contentValues.put(MediaStore.Video.Media.MIME_TYPE, "video/mp4");
            contentValues.put(MediaStore.Video.Media.IS_PENDING, 1);

            ContentResolver resolver = activity.getContentResolver();
            outputUri = resolver.insert(MediaStore.Video.Media.EXTERNAL_CONTENT_URI, contentValues);
            hbRecorder.setFileName(fileName);
            hbRecorder.setOutputUri(outputUri);
        } else {
            createFolder();
            hbRecorder.setOutputPath(
                    Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MOVIES) + "/" + saveFolder);
        }
    }

    private void applyQuickSettings() {
        hbRecorder.setAudioBitrate(128000);
        hbRecorder.setAudioSamplingRate(44100);
        hbRecorder.recordHDVideo(false);
        hbRecorder.isAudioEnabled(true);
        hbRecorder.setNotificationTitle("Recording your screen");
        hbRecorder.setNotificationDescription("Drag down to stop the recording");
    }

    private void createFolder() {
        File folder = new File(
                Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MOVIES),
                saveFolder);

        if (!folder.exists() && folder.mkdirs()) {
            Log.i(TAG, "Recording folder created.");
        }
    }

    private void refreshGalleryFile() {
        String filePath = hbRecorder.getFilePath();
        if (filePath == null || filePath.length() == 0) {
            return;
        }

        MediaScannerConnection.scanFile(activity, new String[]{filePath}, null, null);
    }

    private void updateGalleryUri() {
        if (contentValues == null || outputUri == null) {
            return;
        }

        contentValues.clear();
        contentValues.put(MediaStore.Video.Media.IS_PENDING, 0);
        activity.getContentResolver().update(outputUri, contentValues, null, null);
    }

    private String generateFileName() {
        return new SimpleDateFormat("yyyy-MM-dd-HH-mm-ss", Locale.getDefault()).format(new Date());
    }

    private void showLongToast(final String message) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                Toast.makeText(activity.getApplicationContext(), message, Toast.LENGTH_LONG).show();
            }
        });
    }

    private void sendUnityMessage(String message) {
        UnityPlayer.UnitySendMessage(gameObject, "VideoRecorderCallback", message);
    }

    private static String resolveGameObject(String gameObject) {
        return gameObject == null || gameObject.length() == 0 ? DEFAULT_GAME_OBJECT : gameObject;
    }
}
