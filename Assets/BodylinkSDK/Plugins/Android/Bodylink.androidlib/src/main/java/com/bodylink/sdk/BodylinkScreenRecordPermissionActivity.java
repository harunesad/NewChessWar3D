package com.bodylink.sdk;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;

import java.lang.ref.WeakReference;

public class BodylinkScreenRecordPermissionActivity extends Activity {
    private static final int SCREEN_RECORD_REQUEST_CODE = 777;
    private static final String EXTRA_CAPTURE_INTENT = "com.bodylink.sdk.EXTRA_CAPTURE_INTENT";
    private static WeakReference<BodylinkScreenRecorder> pendingRecorder;

    public static void start(Activity hostActivity, Intent captureIntent, BodylinkScreenRecorder recorder) {
        pendingRecorder = new WeakReference<>(recorder);

        Intent intent = new Intent(hostActivity, BodylinkScreenRecordPermissionActivity.class);
        intent.putExtra(EXTRA_CAPTURE_INTENT, captureIntent);
        hostActivity.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Intent captureIntent = getIntent().getParcelableExtra(EXTRA_CAPTURE_INTENT);
        if (captureIntent == null) {
            finish();
            return;
        }

        startActivityForResult(captureIntent, SCREEN_RECORD_REQUEST_CODE);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode == SCREEN_RECORD_REQUEST_CODE && pendingRecorder != null) {
            BodylinkScreenRecorder recorder = pendingRecorder.get();
            if (recorder != null) {
                recorder.onScreenCapturePermissionResult(this, resultCode, data);
            }
        }

        pendingRecorder = null;
        finish();
    }
}
