package com.unity3d.player;

import android.annotation.TargetApi;
import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.os.Build;
import android.os.Bundle;
import android.view.SurfaceView;
import android.view.ViewTreeObserver;
import android.view.inputmethod.InputMethodManager;
import android.widget.FrameLayout;
import android.view.Window;
import android.util.Log;

import androidx.core.view.ViewCompat;

import com.google.androidgamesdk.GameActivity;


public class UnityPlayerGameActivity extends GameActivity implements IUnityPlayerLifecycleEvents, IUnityPermissionRequestSupport, IUnityPlayerSupport
{
    private static final String TAG = "QuestNav_GameActivity";
    protected UnityPlayerForGameActivity mUnityPlayer;
    private VRFocusManager vrFocusManager;
    
    protected String updateUnityCommandLineArguments(String cmdLine)
    {
        return cmdLine;
    }

    static
    {
        System.loadLibrary("game");
    }

    @Override
    protected void onCreate(Bundle savedInstanceState){
        super.onCreate(savedInstanceState);
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onCreate");
        
        // On devices with API Level >= 30 system bars are no longer accounted for and because of that window/views don't resize
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            getWindow().setDecorFitsSystemWindows(true);
        }
        
        // Get the VR focus manager instance
        vrFocusManager = VRFocusManager.getInstance(this);
    }

    @Override
    public UnityPlayerForGameActivity getUnityPlayerConnection() {
        return mUnityPlayer;
    }

    // Soft keyboard relies on inset listener for listening to various events - keyboard opened/closed/text entered.
    private void applyInsetListener(SurfaceView surfaceView)
    {
        surfaceView.getViewTreeObserver().addOnGlobalLayoutListener(
                () -> onApplyWindowInsets(surfaceView, ViewCompat.getRootWindowInsets(getWindow().getDecorView())));
    }

    @Override protected void onCreateSurfaceView() {
        super.onCreateSurfaceView();
        FrameLayout frameLayout = findViewById(contentViewId);

        applyInsetListener(mSurfaceView);

        mSurfaceView.setId(UnityPlayerForGameActivity.getUnityViewIdentifier(this));

        String cmdLine = updateUnityCommandLineArguments(getIntent().getStringExtra("unity"));
        getIntent().putExtra("unity", cmdLine);
        
        // Unity requires access to frame layout for setting the static splash screen.
        mUnityPlayer = new UnityPlayerForGameActivity(this, frameLayout, mSurfaceView, this);
        
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity surface view created");
    }

    @Override
    public void onUnityPlayerUnloaded() {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onUnityPlayerUnloaded");
    }

    @Override
    public void onUnityPlayerQuitted() {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onUnityPlayerQuitted");
    }

    // Quit Unity
    @Override protected void onDestroy ()
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onDestroy");
        mUnityPlayer.destroy();
        super.onDestroy();
    }

    @Override protected void onStop()
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onStop");
        // Note: we want Java onStop callbacks to be processed before the native part processes the onStop callback
        mUnityPlayer.onStop();
        super.onStop();
    }

    @Override protected void onStart()
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onStart");
        // Note: we want Java onStart callbacks to be processed before the native part processes the onStart callback
        mUnityPlayer.onStart();
        super.onStart();
        
        // Request VR focus when the activity starts
        if (vrFocusManager != null) {
            vrFocusManager.requestFocus(this);
        }
    }

    // Pause Unity
    @Override protected void onPause()
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onPause");
        // Note: we want Java onPause callbacks to be processed before the native part processes the onPause callback
        mUnityPlayer.onPause();
        super.onPause();
    }

    // Resume Unity
    @Override protected void onResume()
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onResume");
        // Note: we want Java onResume callbacks to be processed before the native part processes the onResume callback
        mUnityPlayer.onResume();
        super.onResume();
        
        // Request VR focus when the activity resumes
        if (vrFocusManager != null) {
            vrFocusManager.requestFocus(this);
        }
    }

    // Configuration changes are used by Video playback logic in Unity
    @Override public void onConfigurationChanged(Configuration newConfig)
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onConfigurationChanged");
        mUnityPlayer.configurationChanged(newConfig);
        super.onConfigurationChanged(newConfig);
    }

    // Notify Unity of the focus change.
    @Override public void onWindowFocusChanged(boolean hasFocus)
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onWindowFocusChanged: " + hasFocus);
        mUnityPlayer.windowFocusChanged(hasFocus);
        super.onWindowFocusChanged(hasFocus);
        
        // Request VR focus when the window gains focus
        if (hasFocus && vrFocusManager != null) {
            vrFocusManager.requestFocus(this);
        }
    }

    @Override protected void onNewIntent(Intent intent)
    {
        Log.d(TAG, "[QuestNav] UnityPlayerGameActivity onNewIntent");
        super.onNewIntent(intent);
        // To support deep linking, we need to make sure that the client can get access to
        // the last sent intent. The clients access this through a JNI api that allows them
        // to get the intent set on launch. To update that after launch we have to manually
        // replace the intent with the one caught here.
        setIntent(intent);
        mUnityPlayer.newIntent(intent);
    }

    @Override
    @TargetApi(Build.VERSION_CODES.M)
    public void requestPermissions(PermissionRequest request)
    {
        mUnityPlayer.addPermissionRequest(request);
    }
}
