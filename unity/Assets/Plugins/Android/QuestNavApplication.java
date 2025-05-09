package com.unity3d.player;

import android.app.Application;
import android.util.Log;

/**
 * Custom Application class for QuestNav that initializes the crash handler.
 */
public class QuestNavApplication extends Application {
    private static final String TAG = "QuestNav_Application";
    
    @Override
    public void onCreate() {
        super.onCreate();
        Log.d(TAG, "QuestNavApplication onCreate");
        
        // Initialize the crash handler
        CrashHandler.init(this);
    }
}
