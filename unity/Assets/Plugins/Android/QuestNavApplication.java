package com.unity3d.player;

import android.app.Application;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/**
 * Custom Application class for QuestNav that initializes the crash handler.
 */
public class QuestNavApplication extends Application {
    private static final String TAG = "QuestNav_Application";
    private static final int VR_INIT_DELAY_MS = 3000; // 3 second delay to allow VR system to initialize
    private static final String PREFS_NAME = "QuestNavPrefs";
    private static final String KEY_FIRST_BOOT = "first_boot_completed";

    // VR Focus Manager instance
    private VRFocusManager vrFocusManager;

    @Override
    public void onCreate() {
        super.onCreate();
        Log.d(TAG, "[QuestNav] QuestNavApplication onCreate");

        // Initialize the crash handler
        CrashHandler.init(this);

        // Initialize VR Focus Manager
        vrFocusManager = VRFocusManager.getInstance(this);

        // Check if this is the first time the app is running after installation
        checkFirstBoot();

        // Delay VR initialization to ensure proper VR focus acquisition
        delayedVRInitialization();
    }

    @Override
    public void onTerminate() {
        // Stop VR focus management when the application terminates
        if (vrFocusManager != null) {
            vrFocusManager.stop();
        }
        super.onTerminate();
    }

    /**
     * Checks if this is the first time the app is running after installation.
     * If it is, we'll mark it as no longer the first boot.
     */
    private void checkFirstBoot() {
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        boolean isFirstBoot = !prefs.getBoolean(KEY_FIRST_BOOT, false);

        if (isFirstBoot) {
            Log.d(TAG, "[QuestNav] First boot after installation detected");

            // Mark that first boot is completed
            SharedPreferences.Editor editor = prefs.edit();
            editor.putBoolean(KEY_FIRST_BOOT, true);
            editor.apply();

            // Launch the main activity to ensure proper initialization
            try {
                Intent launchIntent = new Intent(this, UnityPlayerGameActivity.class);
                launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                startActivity(launchIntent);
                Log.d(TAG, "[QuestNav] Launched main activity on first boot");
            } catch (Exception e) {
                Log.e(TAG, "[QuestNav] Error launching main activity on first boot", e);
            }
        }
    }

    /**
     * Delays VR initialization to ensure the system has time to properly
     * initialize and grant VR focus before accessing head tracking.
     */
    private void delayedVRInitialization() {
        new Handler(Looper.getMainLooper()).postDelayed(new Runnable() {
            @Override
            public void run() {
                Log.d(TAG, "[QuestNav] Delayed VR initialization complete");

                // Start VR focus management
                if (vrFocusManager != null) {
                    vrFocusManager.start();
                    Log.d(TAG, "[QuestNav] VR focus management started");
                }
            }
        }, VR_INIT_DELAY_MS);
    }
}
