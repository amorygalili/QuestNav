using UnityEngine;
using System;

namespace QuestNav.Core
{
    /// <summary>
    /// Manages application lifecycle to ensure the app continues to function
    /// even when not in focus or when the device is idle.
    /// </summary>
    public class AppLifecycleManager : MonoBehaviour
    {
        [SerializeField] private bool keepRunningInBackground = true;
        [SerializeField] private bool preventSleep = true;
        
        private static AppLifecycleManager _instance;
        public static AppLifecycleManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Apply settings
            ApplySettings();
            
            // Log initialization
            Debug.Log("[QuestNav] AppLifecycleManager initialized");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log($"[QuestNav] Application focus changed: {hasFocus}");
            
            // Reapply settings when focus changes to ensure they're maintained
            ApplySettings();
        }

        private void OnApplicationPause(bool isPaused)
        {
            Debug.Log($"[QuestNav] Application pause state changed: {isPaused}");
            
            // Reapply settings when pause state changes
            ApplySettings();
        }

        /// <summary>
        /// Apply all application lifecycle settings
        /// </summary>
        private void ApplySettings()
        {
            // Set the application to run in background
            Application.runInBackground = keepRunningInBackground;
            
            // Prevent the device from sleeping
            if (preventSleep)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
            }
            
            Debug.Log($"[QuestNav] Applied settings - RunInBackground: {Application.runInBackground}, SleepTimeout: {Screen.sleepTimeout}");
        }
    }
}
