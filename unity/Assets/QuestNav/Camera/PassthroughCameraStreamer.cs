using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Threading;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace QuestNav.Camera
{
    /// <summary>
    /// Captures and streams Quest 3 passthrough camera frames for web interface display.
    /// Uses Meta's Passthrough Camera API (Horizon OS v74+) to access real camera data and converts to JPEG for MJPEG streaming.
    /// Requires horizonos.permission.HEADSET_CAMERA permission.
    /// </summary>
    public class PassthroughCameraStreamer : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Camera Settings")]
        [SerializeField] private int targetFrameRate = 15;
        [SerializeField] private int jpegQuality = 75;
        [SerializeField] private int maxFrameBufferSize = 5;

        [Header("Camera Resolution")]
        [SerializeField] private int captureWidth = 640;
        [SerializeField] private int captureHeight = 480;
        #endregion

        #region Private Fields
        private WebCamTexture webCamTexture;
        private Texture2D captureTexture;
        private Queue<byte[]> frameBuffer;
        private bool isStreaming = false;
        private CancellationTokenSource cancellationTokenSource;
        private readonly object frameLock = new object();
        private byte[] latestFrame;
        private float lastCaptureTime;
        private float captureInterval;
        #endregion

        #region Unity Lifecycle
        void Start()
        {
            InitializeCamera();
            frameBuffer = new Queue<byte[]>();
            captureInterval = 1.0f / targetFrameRate;

            Debug.Log($"[QuestNav] PassthroughCameraStreamer initialized with {captureWidth}x{captureHeight} at {targetFrameRate}fps");
        }

        void Update()
        {
            if (isStreaming && Time.time - lastCaptureTime >= captureInterval)
            {
                StartCoroutine(CaptureFrameCoroutine());
                lastCaptureTime = Time.time;
            }
        }

        void OnDestroy()
        {
            StopStreaming();
            CleanupResources();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Start streaming passthrough camera frames
        /// </summary>
        public void StartStreaming()
        {
            if (isStreaming)
                return;

            Debug.Log("[QuestNav] Starting camera streaming");

            // Check Quest 3 passthrough camera requirements
            CheckPassthroughCameraRequirements();

            // Request camera permissions if needed
            StartCoroutine(RequestCameraPermissionsAndStart());
        }

        /// <summary>
        /// Request camera permissions and start streaming
        /// </summary>
        private IEnumerator RequestCameraPermissionsAndStart()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Check if we need to request HEADSET_CAMERA permission
            if (!HasHeadsetCameraPermission())
            {
                Debug.Log("[QuestNav] Requesting HEADSET_CAMERA permission");

                // Request the permission
                Permission.RequestUserPermission("horizonos.permission.HEADSET_CAMERA");

                // Wait for user response (timeout after 10 seconds)
                float timeout = 10f;
                float elapsed = 0f;

                while (!HasHeadsetCameraPermission() && elapsed < timeout)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }

                if (HasHeadsetCameraPermission())
                {
                    Debug.Log("[QuestNav] HEADSET_CAMERA permission granted!");

                    // Re-initialize camera now that we have permission
                    InitializeCamera();
                }
                else
                {
                    Debug.LogWarning("[QuestNav] HEADSET_CAMERA permission denied or timed out");
                }
            }
#else
            // On non-Android platforms, just wait one frame
            yield return null;
#endif

            // Start Quest 3 passthrough camera
            if (webCamTexture != null)
            {
                Debug.Log("[QuestNav] Starting Quest 3 passthrough camera");
                webCamTexture.Play();

                // Wait a frame for initialization as recommended by Meta
                StartCoroutine(WaitForCameraInitialization());
            }
            else
            {
                Debug.LogWarning("[QuestNav] No passthrough camera available - ensure permissions are granted");
            }

            isStreaming = true;
            cancellationTokenSource = new CancellationTokenSource();

            // Clear any existing frames
            lock (frameLock)
            {
                frameBuffer.Clear();
                latestFrame = null;
            }
        }

        /// <summary>
        /// Stop streaming passthrough camera frames
        /// </summary>
        public void StopStreaming()
        {
            if (!isStreaming)
                return;

            Debug.Log("[QuestNav] Stopping passthrough camera streaming");

            if (webCamTexture != null)
            {
                Debug.Log("[QuestNav] Stopping Quest 3 passthrough camera");
                webCamTexture.Stop();
            }

            isStreaming = false;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            lock (frameLock)
            {
                frameBuffer.Clear();
                latestFrame = null;
            }
        }

        /// <summary>
        /// Get the latest captured frame as JPEG bytes
        /// </summary>
        /// <returns>JPEG frame data or null if no frame available</returns>
        public byte[] GetLatestFrame()
        {
            lock (frameLock)
            {
                return latestFrame;
            }
        }

        /// <summary>
        /// Get the next frame from the buffer for MJPEG streaming
        /// </summary>
        /// <returns>JPEG frame data or null if buffer is empty</returns>
        public byte[] GetNextFrame()
        {
            lock (frameLock)
            {
                if (frameBuffer.Count > 0)
                {
                    return frameBuffer.Dequeue();
                }
                return null;
            }
        }

        /// <summary>
        /// Check if streaming is currently active
        /// </summary>
        public bool IsStreaming => isStreaming;

        /// <summary>
        /// Get the number of frames currently in the buffer
        /// </summary>
        public int BufferCount
        {
            get
            {
                lock (frameLock)
                {
                    return frameBuffer.Count;
                }
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Initialize Quest 3 passthrough camera
        /// </summary>
        private void InitializeCamera()
        {
            try
            {
                TryInitializePassthroughCamera();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Failed to initialize passthrough camera: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize Quest 3 passthrough camera using WebCamTexture API
        /// </summary>
        private void TryInitializePassthroughCamera()
        {
            try
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                Debug.Log($"[QuestNav] Found {devices.Length} camera devices");

                for (int i = 0; i < devices.Length; i++)
                {
                    Debug.Log($"[QuestNav] Camera {i}: {devices[i].name} (Front: {devices[i].isFrontFacing})");
                }

                if (devices.Length > 0)
                {
                    // Look for Quest-specific camera devices first
                    string selectedDevice = null;

                    // Try to find Quest passthrough cameras (they might have specific names)
                    foreach (var device in devices)
                    {
                        string deviceName = device.name.ToLower();
                        if (deviceName.Contains("quest") || deviceName.Contains("passthrough") ||
                            deviceName.Contains("meta") || deviceName.Contains("oculus"))
                        {
                            selectedDevice = device.name;
                            Debug.Log($"[QuestNav] Found Quest-specific camera: {selectedDevice}");
                            break;
                        }
                    }

                    // If no Quest-specific camera found, use the first available
                    if (selectedDevice == null)
                    {
                        selectedDevice = devices[0].name;
                        Debug.Log($"[QuestNav] Using first available camera: {selectedDevice}");
                    }

                    // Create WebCamTexture with Quest 3 optimal settings
                    // According to the API docs: max ~1280x960 at ~30 FPS
                    int optimalWidth = Mathf.Min(captureWidth, 1280);
                    int optimalHeight = Mathf.Min(captureHeight, 960);
                    int optimalFPS = Mathf.Min(targetFrameRate, 30);

                    webCamTexture = new WebCamTexture(selectedDevice, optimalWidth, optimalHeight, optimalFPS);

                    // Create texture for reading pixels
                    captureTexture = new Texture2D(optimalWidth, optimalHeight, TextureFormat.RGB24, false);

                    Debug.Log($"[QuestNav] Quest 3 passthrough camera initialized: {selectedDevice} ({optimalWidth}x{optimalHeight} @ {optimalFPS}fps)");
                }
                else
                {
                    Debug.LogWarning("[QuestNav] No camera devices found - ensure Horizon OS v74+ and HEADSET_CAMERA permission granted");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error initializing Quest 3 passthrough camera: {ex.Message}");
            }
        }

        /// <summary>
        /// Capture a frame from Quest 3 passthrough camera
        /// </summary>
        private IEnumerator CaptureFrameCoroutine()
        {
            if (!isStreaming || webCamTexture == null)
                yield break;

            bool success = false;
            byte[] jpegData = null;

            try
            {
                if (webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
                {
                    // Get pixels from WebCamTexture
                    Color32[] pixels = webCamTexture.GetPixels32();

                    // Create or resize capture texture if needed
                    if (captureTexture.width != webCamTexture.width || captureTexture.height != webCamTexture.height)
                    {
                        DestroyImmediate(captureTexture);
                        captureTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
                    }

                    captureTexture.SetPixels32(pixels);
                    captureTexture.Apply();

                    success = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error capturing passthrough camera frame: {ex.Message}");
            }

            if (success)
            {
                try
                {
                    // Encode to JPEG
                    jpegData = captureTexture.EncodeToJPG(jpegQuality);

                    if (jpegData != null && jpegData.Length > 0)
                    {
                        lock (frameLock)
                        {
                            // Update latest frame
                            latestFrame = jpegData;

                            // Add to buffer for streaming
                            frameBuffer.Enqueue(jpegData);

                            // Limit buffer size
                            while (frameBuffer.Count > maxFrameBufferSize)
                            {
                                frameBuffer.Dequeue();
                            }
                        }

                        // Send frame to Java web server
                        SendFrameToJava(jpegData);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[QuestNav] Error encoding frame to JPEG: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if HEADSET_CAMERA permission is granted
        /// </summary>
        private bool HasHeadsetCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Check using Unity's Permission API
                if (Permission.HasUserAuthorizedPermission("horizonos.permission.HEADSET_CAMERA"))
                {
                    return true;
                }

                // Fallback: check using Android API directly
                using (var unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
                {
                    var permissionResult = context.Call<int>("checkSelfPermission", "horizonos.permission.HEADSET_CAMERA");
                    return permissionResult == 0; // PackageManager.PERMISSION_GRANTED = 0
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QuestNav] Error checking HEADSET_CAMERA permission: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Check Quest 3 passthrough camera requirements
        /// </summary>
        private void CheckPassthroughCameraRequirements()
        {
            try
            {
                // Check headset type
                var headsetType = OVRPlugin.GetSystemHeadsetType();
                Debug.Log($"[QuestNav] Headset type: {headsetType}");

                if (headsetType != OVRPlugin.SystemHeadset.Meta_Quest_3)
                {
                    Debug.LogWarning("[QuestNav] Passthrough Camera API requires Quest 3 or Quest 3S");
                }

                // Check if we're on Android (Quest runs Android)
                if (Application.platform == RuntimePlatform.Android)
                {
                    Debug.Log("[QuestNav] Running on Android platform (Quest)");

                    // Try to check permissions via Android
                    using (var unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
                    {
                        try
                        {
                            // Check for HEADSET_CAMERA permission
                            var permissionResult = context.Call<int>("checkSelfPermission", "horizonos.permission.HEADSET_CAMERA");
                            bool hasPermission = permissionResult == 0; // PackageManager.PERMISSION_GRANTED = 0

                            Debug.Log($"[QuestNav] HEADSET_CAMERA permission granted: {hasPermission}");

                            if (!hasPermission)
                            {
                                Debug.LogWarning("[QuestNav] HEADSET_CAMERA permission not granted. User will be prompted at runtime.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[QuestNav] Could not check HEADSET_CAMERA permission: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[QuestNav] Not running on Android - passthrough camera only available on Quest");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error checking passthrough camera requirements: {ex.Message}");
            }
        }

        /// <summary>
        /// Wait for camera initialization as recommended by Meta
        /// </summary>
        private IEnumerator WaitForCameraInitialization()
        {
            yield return null; // Wait one frame

            if (webCamTexture != null)
            {
                Debug.Log($"[QuestNav] Camera initialization complete. IsPlaying: {webCamTexture.isPlaying}, Width: {webCamTexture.width}, Height: {webCamTexture.height}");

                if (!webCamTexture.isPlaying)
                {
                    Debug.LogWarning("[QuestNav] WebCamTexture failed to start. Check permissions and device compatibility.");
                }
            }
        }

        /// <summary>
        /// Send camera frame to Java web server
        /// </summary>
        private void SendFrameToJava(byte[] jpegData)
        {
            try
            {
                // Convert byte array to base64 string for transmission to Java
                string base64Data = System.Convert.ToBase64String(jpegData);

                // Send to Java via AndroidJavaClass
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass webServerClass = new AndroidJavaClass("com.questnav.webserver.QuestNavWebServer"))
                {
                    webServerClass.CallStatic("updateCameraFrameBase64", base64Data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error sending frame to Java: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        private void CleanupResources()
        {
            if (captureTexture != null)
            {
                DestroyImmediate(captureTexture);
                captureTexture = null;
            }

            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                    webCamTexture.Stop();
                DestroyImmediate(webCamTexture);
                webCamTexture = null;
            }
        }
        #endregion
    }
}
