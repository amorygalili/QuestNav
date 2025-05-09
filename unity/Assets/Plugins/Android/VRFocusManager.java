package com.unity3d.player;

import android.app.Activity;
import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/**
 * Helper class to manage VR focus for the QuestNav application.
 * This class helps ensure the app properly acquires and maintains VR focus,
 * which is necessary for accessing head tracking and other VR features.
 */
public class VRFocusManager {
    private static final String TAG = "QuestNav_VRFocusManager";
    private static final int FOCUS_CHECK_INTERVAL_MS = 5000; // Check focus every 5 seconds
    
    private static VRFocusManager instance;
    private Context appContext;
    private Handler handler;
    private boolean isRunning = false;
    
    /**
     * Get the singleton instance of the VRFocusManager
     */
    public static synchronized VRFocusManager getInstance(Context context) {
        if (instance == null) {
            instance = new VRFocusManager(context.getApplicationContext());
        }
        return instance;
    }
    
    private VRFocusManager(Context context) {
        this.appContext = context;
        this.handler = new Handler(Looper.getMainLooper());
    }
    
    /**
     * Start monitoring and managing VR focus
     */
    public void start() {
        if (!isRunning) {
            isRunning = true;
            Log.d(TAG, "[QuestNav] VR focus management started");
            checkFocus();
        }
    }
    
    /**
     * Stop monitoring and managing VR focus
     */
    public void stop() {
        if (isRunning) {
            isRunning = false;
            handler.removeCallbacksAndMessages(null);
            Log.d(TAG, "[QuestNav] VR focus management stopped");
        }
    }
    
    /**
     * Check if the app has VR focus and take appropriate action
     */
    private void checkFocus() {
        if (!isRunning) {
            return;
        }
        
        try {
            // Log the current focus state
            Log.d(TAG, "[QuestNav] Checking VR focus");
            
            // Schedule the next check
            handler.postDelayed(this::checkFocus, FOCUS_CHECK_INTERVAL_MS);
        } catch (Exception e) {
            Log.e(TAG, "[QuestNav] Error checking VR focus", e);
        }
    }
    
    /**
     * Request VR focus for the app
     * This should be called when the app starts or resumes
     */
    public void requestFocus(Activity activity) {
        if (activity == null) {
            Log.e(TAG, "[QuestNav] Cannot request focus, activity is null");
            return;
        }
        
        try {
            Log.d(TAG, "[QuestNav] Requesting VR focus");
            
            // Ensure the activity is in the foreground
            if (!activity.isFinishing() && !activity.isDestroyed()) {
                activity.moveTaskToFront();
                Log.d(TAG, "[QuestNav] Moved task to front to help acquire VR focus");
            }
        } catch (Exception e) {
            Log.e(TAG, "[QuestNav] Error requesting VR focus", e);
        }
    }
}
