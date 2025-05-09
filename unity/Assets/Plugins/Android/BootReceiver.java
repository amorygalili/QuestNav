package com.unity3d.player;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

/**
 * Broadcast receiver that starts the QuestNav application when the device boots up
 * or when the app needs to be restarted after a crash.
 */
public class BootReceiver extends BroadcastReceiver {
    private static final String TAG = "QuestNav_BootReceiver";
    public static final String ACTION_RESTART_APP = "com.unity3d.player.ACTION_RESTART_APP";

    @Override
    public void onReceive(Context context, Intent intent) {
        String action = intent.getAction();

        if (Intent.ACTION_BOOT_COMPLETED.equals(action)) {
            Log.d(TAG, "Boot completed, starting QuestNav application");
            launchApp(context);
        }
        else if (ACTION_RESTART_APP.equals(action)) {
            Log.d(TAG, "Received restart request, restarting QuestNav application");
            launchApp(context);
        }
    }

    /**
     * Helper method to launch the main Unity activity
     */
    private void launchApp(Context context) {
        try {
            // Create an intent to launch the main Unity activity
            Intent launchIntent = new Intent(context, UnityPlayerGameActivity.class);
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            context.startActivity(launchIntent);
            Log.d(TAG, "QuestNav application started successfully");
        } catch (Exception e) {
            Log.e(TAG, "Error starting QuestNav application", e);
        }
    }
}
