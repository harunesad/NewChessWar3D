package com.bodylink.sdk;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;

import com.unity3d.player.UnityPlayer;

public class BodylinkPermissionActivity extends Activity {
    private static final int PERMISSION_REQUEST_CODE = 701;
    private static final String EXTRA_PERMISSION = "com.bodylink.sdk.EXTRA_PERMISSION";
    private static final String EXTRA_GAME_OBJECT = "com.bodylink.sdk.EXTRA_GAME_OBJECT";

    public static void request(Activity hostActivity, String permission, String gameObject) {
        Intent intent = new Intent(hostActivity, BodylinkPermissionActivity.class);
        intent.putExtra(EXTRA_PERMISSION, permission);
        intent.putExtra(EXTRA_GAME_OBJECT, gameObject);
        hostActivity.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        String permission = getIntent().getStringExtra(EXTRA_PERMISSION);
        if (permission == null || permission.length() == 0) {
            finish();
            return;
        }

        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M ||
                checkCallingOrSelfPermission(permission) == PackageManager.PERMISSION_GRANTED) {
            sendResult("OnAllow");
            finish();
            return;
        }

        requestPermissions(new String[]{permission}, PERMISSION_REQUEST_CODE);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode != PERMISSION_REQUEST_CODE || grantResults.length == 0) {
            sendResult("OnDeny");
            finish();
            return;
        }

        if (grantResults[0] == PackageManager.PERMISSION_GRANTED) {
            sendResult("OnAllow");
        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M &&
                permissions.length > 0 &&
                shouldShowRequestPermissionRationale(permissions[0])) {
            sendResult("OnDeny");
        } else {
            sendResult("OnDenyAndNeverAskAgain");
        }

        finish();
    }

    private void sendResult(String method) {
        String gameObject = getIntent().getStringExtra(EXTRA_GAME_OBJECT);
        if (gameObject == null || gameObject.length() == 0) {
            gameObject = "AndroidUtils";
        }

        UnityPlayer.UnitySendMessage(gameObject, method, "");
    }
}
