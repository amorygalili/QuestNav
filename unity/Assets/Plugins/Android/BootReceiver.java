package com.unity3d.player;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.app.AlarmManager;
import android.app.PendingIntent;
import android.os.SystemClock;

/**
 * Broadcast receiver that starts the QuestNav application when the device boots up
 * or when the app needs to be restarted after a crash.
 */
public class BootReceiver extends BroadcastReceiver {
    private static final String TAG = "QuestNav_BootReceiver";
    public static final String ACTION_RESTART_APP = "com.unity3d.player.ACTION_RESTART_APP";
    private static final int BOOT_DELAY_MS = 15000; // 15 second delay after boot
    private static final int RESTART_DELAY_MS = 2000; // 2 second delay for app restarts

    private static final String PREFS_NAME = "QuestNavBootPrefs";
    private static final String KEY_BOOT_ATTEMPTS = "boot_attempts";
    private static final String KEY_LAST_BOOT_TIME = "last_boot_time";
    private static final int MAX_BOOT_ATTEMPTS = 3;

    @Override
    public void onReceive(Context context, Intent intent) {
        String action = intent.getAction();

        if (Intent.ACTION_BOOT_COMPLETED.equals(action)) {
            Log.d(TAG, "[QuestNav] Boot completed, scheduling QuestNav application start with delay");

            // Track boot attempts to prevent boot loops
            if (shouldAttemptBoot(context)) {
                // Use AlarmManager to delay app start after boot
                scheduleAppStart(context, BOOT_DELAY_MS);
            } else {
                Log.w(TAG, "[QuestNav] Too many boot attempts, skipping auto-start");
            }
        }
        else if (ACTION_RESTART_APP.equals(action)) {
            Log.d(TAG, "[QuestNav] Received restart request, restarting QuestNav application");
            // For app restarts, use a shorter delay
            scheduleAppStart(context, RESTART_DELAY_MS);
        }
    }

    /**
     * Determines if we should attempt to boot the app based on recent boot history
     */
    private boolean shouldAttemptBoot(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        long currentTime = System.currentTimeMillis();
        long lastBootTime = prefs.getLong(KEY_LAST_BOOT_TIME, 0);
        int bootAttempts = prefs.getInt(KEY_BOOT_ATTEMPTS, 0);

        // If it's been more than 5 minutes since the last boot attempt, reset the counter
        if (currentTime - lastBootTime > 5 * 60 * 1000) {
            bootAttempts = 0;
        }

        // Increment and save boot attempts
        bootAttempts++;
        SharedPreferences.Editor editor = prefs.edit();
        editor.putInt(KEY_BOOT_ATTEMPTS, bootAttempts);
        editor.putLong(KEY_LAST_BOOT_TIME, currentTime);
        editor.apply();

        Log.d(TAG, "[QuestNav] Boot attempt " + bootAttempts + " of " + MAX_BOOT_ATTEMPTS);

        // Allow boot if we haven't exceeded the maximum attempts
        return bootAttempts <= MAX_BOOT_ATTEMPTS;
    }

    /**
     * Schedules the app to start after a delay using AlarmManager
     */
    private void scheduleAppStart(Context context, int delayMs) {
        try {
            // Create an explicit intent for the main activity
            Intent launchIntent = new Intent(context, UnityPlayerGameActivity.class);
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);

            // Create a pending intent for the AlarmManager
            PendingIntent pendingIntent = PendingIntent.getActivity(
                context,
                0,
                launchIntent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
            );

            // Get the AlarmManager service
            AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
            if (alarmManager != null) {
                // Schedule the app to start after the delay
                // Use ELAPSED_REALTIME_WAKEUP which is more reliable for this purpose
                alarmManager.set(
                    AlarmManager.ELAPSED_REALTIME_WAKEUP,
                    SystemClock.elapsedRealtime() + delayMs,
                    pendingIntent
                );
                Log.d(TAG, "[QuestNav] Application start scheduled in " + delayMs + "ms");
            } else {
                Log.e(TAG, "[QuestNav] AlarmManager service not available, launching immediately");
                launchApp(context);
            }
        } catch (Exception e) {
            Log.e(TAG, "[QuestNav] Error scheduling application start", e);
            // Fall back to immediate launch
            launchApp(context);
        }
    }

    /**
     * Helper method to launch the main Unity activity immediately
     */
    private void launchApp(Context context) {
        try {
            // Create an intent to launch the main Unity activity
            Intent launchIntent = new Intent(context, UnityPlayerGameActivity.class);
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
            context.startActivity(launchIntent);
            Log.d(TAG, "[QuestNav] Application started successfully");
        } catch (Exception e) {
            Log.e(TAG, "[QuestNav] Error starting application", e);
        }
    }
}
