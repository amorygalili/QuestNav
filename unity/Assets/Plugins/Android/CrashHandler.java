package com.unity3d.player;

import android.app.AlarmManager;
import android.app.Application;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.Process;
import android.util.Log;

/**
 * Global crash handler for the QuestNav application.
 * This class catches uncaught exceptions and restarts the app after a crash.
 * Includes crash loop protection to prevent continuous crash-restart cycles.
 */
public class CrashHandler implements Thread.UncaughtExceptionHandler {
    private static final String TAG = "QuestNav_CrashHandler";
    private static final int INITIAL_RESTART_DELAY_MS = 3000; // 3 second initial delay
    private static final int MAX_RESTART_DELAY_MS = 60000; // Maximum 1 minute delay

    // Crash loop detection constants
    private static final String PREFS_NAME = "QuestNavCrashPrefs";
    private static final String KEY_CRASH_COUNT = "crash_count";
    private static final String KEY_LAST_CRASH_TIME = "last_crash_time";
    private static final int CRASH_COUNT_RESET_TIME_MS = 60000; // Reset crash count after 1 minute of stability
    private static final int MAX_CRASHES_BEFORE_BACKOFF = 3; // Start increasing delay after this many crashes

    private final Application application;
    private final Thread.UncaughtExceptionHandler defaultExceptionHandler;

    /**
     * Initialize the crash handler for the application
     * @param application The application context
     */
    public static void init(Application application) {
        Thread.setDefaultUncaughtExceptionHandler(new CrashHandler(application));
        Log.d(TAG, "CrashHandler initialized");
    }

    private CrashHandler(Application application) {
        this.application = application;
        this.defaultExceptionHandler = Thread.getDefaultUncaughtExceptionHandler();
    }

    @Override
    public void uncaughtException(Thread thread, Throwable throwable) {
        Log.e(TAG, "Uncaught exception detected, preparing for app restart", throwable);

        try {
            // Schedule app restart with crash loop protection
            scheduleRestartWithProtection();

            // Wait to ensure the restart intent is scheduled
            Thread.sleep(200);
        } catch (Exception e) {
            Log.e(TAG, "Error while handling crash", e);
        }

        // Pass the exception to the default handler
        if (defaultExceptionHandler != null) {
            defaultExceptionHandler.uncaughtException(thread, throwable);
        } else {
            // If no default handler exists, terminate the process
            Process.killProcess(Process.myPid());
            System.exit(10);
        }
    }

    /**
     * Schedule the app to restart with crash loop protection
     */
    private void scheduleRestartWithProtection() {
        try {
            SharedPreferences prefs = application.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            long currentTime = System.currentTimeMillis();
            long lastCrashTime = prefs.getLong(KEY_LAST_CRASH_TIME, 0);
            int crashCount = prefs.getInt(KEY_CRASH_COUNT, 0);

            // If it's been a while since the last crash, reset the counter
            if (currentTime - lastCrashTime > CRASH_COUNT_RESET_TIME_MS) {
                crashCount = 0;
            }

            // Increment crash counter
            crashCount++;

            // Save updated crash information
            SharedPreferences.Editor editor = prefs.edit();
            editor.putInt(KEY_CRASH_COUNT, crashCount);
            editor.putLong(KEY_LAST_CRASH_TIME, currentTime);
            editor.apply();

            // Calculate delay with exponential backoff
            int delayMs = INITIAL_RESTART_DELAY_MS;
            if (crashCount > MAX_CRASHES_BEFORE_BACKOFF) {
                // Exponential backoff: double the delay for each crash beyond the threshold
                int backoffFactor = crashCount - MAX_CRASHES_BEFORE_BACKOFF;
                delayMs = INITIAL_RESTART_DELAY_MS * (1 << Math.min(backoffFactor, 4)); // Cap at 2^4 multiplier

                // Ensure we don't exceed maximum delay
                delayMs = Math.min(delayMs, MAX_RESTART_DELAY_MS);
            }

            Log.d(TAG, "Crash count: " + crashCount + ", scheduling restart with " + delayMs + "ms delay");

            // Create an intent to broadcast to our BootReceiver
            Intent intent = new Intent(application, BootReceiver.class);
            intent.setAction(BootReceiver.ACTION_RESTART_APP);

            // Create a pending intent for the alarm manager
            PendingIntent pendingIntent;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                pendingIntent = PendingIntent.getBroadcast(
                    application,
                    0,
                    intent,
                    PendingIntent.FLAG_ONE_SHOT | PendingIntent.FLAG_IMMUTABLE
                );
            } else {
                pendingIntent = PendingIntent.getBroadcast(
                    application,
                    0,
                    intent,
                    PendingIntent.FLAG_ONE_SHOT
                );
            }

            // Schedule the restart
            AlarmManager alarmManager = (AlarmManager) application.getSystemService(Context.ALARM_SERVICE);
            if (alarmManager != null) {
                alarmManager.set(AlarmManager.RTC, System.currentTimeMillis() + delayMs, pendingIntent);
                Log.d(TAG, "App restart scheduled in " + delayMs + "ms");
            } else {
                Log.e(TAG, "AlarmManager service not available");
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to schedule app restart", e);
        }
    }
}
