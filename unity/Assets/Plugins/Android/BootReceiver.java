package com.unity3d.player;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

/**
 * Broadcast receiver that starts the QuestNav application when the device boots up.
 */
public class BootReceiver extends BroadcastReceiver {
    private static final String TAG = "QuestNav_BootReceiver";

    @Override
    public void onReceive(Context context, Intent intent) {
        if (Intent.ACTION_BOOT_COMPLETED.equals(intent.getAction())) {
            Log.d(TAG, "Boot completed, starting QuestNav application");
            
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
}
