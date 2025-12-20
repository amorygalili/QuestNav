using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Rendering;

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

        [Header("Performance Optimization")]
        [SerializeField] private bool enableOptimizations = false; // Disabled by default for safety
        [SerializeField] private bool enableAdaptiveQuality = true;
        [SerializeField] private int minJpegQuality = 30;
        [SerializeField] private int maxJpegQuality = 90;
        [SerializeField] private bool enableFrameSkipping = true;
        [SerializeField] private float maxProcessingTimeMs = 33.0f; // ~30fps budget (more realistic)
        [SerializeField] private bool enableAsyncEncoding = false; // Disabled by default for safety
        [SerializeField] private bool enableTextureReuse = false; // Disabled by default for safety

        [Header("Bandwidth Optimization")]
        [SerializeField] private bool enableROICropping = false;
        [SerializeField] private Rect roiRect = new Rect(0.25f, 0.25f, 0.5f, 0.5f); // Center 50%
        [SerializeField] private bool enableResolutionScaling = false; // Disabled by default for safety
        [SerializeField] private float lowBandwidthScale = 0.5f;
        #endregion

        #region Private Fields
        private WebCamTexture webCamTexture;
        private Texture2D captureTexture;
        private RenderTexture tempRenderTexture;
        private Queue<byte[]> frameBuffer;
        private bool isStreaming = false;
        private CancellationTokenSource cancellationTokenSource;
        private readonly object frameLock = new object();
        private byte[] latestFrame;
        private float lastCaptureTime;
        private float captureInterval;

        // Performance tracking
        private float averageProcessingTime = 0f;
        private int frameCount = 0;
        private int skippedFrames = 0;
        private int currentQuality;
        private float currentScale = 1.0f;

        // Async processing
        private bool isProcessingFrame = false;

        // Texture reuse for performance
        private Texture2D[] texturePool;
        private int texturePoolIndex = 0;
        private const int TEXTURE_POOL_SIZE = 3;
        #endregion

        #region Unity Lifecycle
        void Start()
        {
            InitializeCamera();
            frameBuffer = new Queue<byte[]>();
            captureInterval = 1.0f / targetFrameRate;
            currentQuality = jpegQuality;

            // Initialize texture pool for reuse
            if (enableTextureReuse)
            {
                InitializeTexturePool();
            }

            Debug.Log($"[QuestNav] PassthroughCameraStreamer initialized with {captureWidth}x{captureHeight} at {targetFrameRate}fps");
        }

        void Update()
        {
            if (isStreaming && Time.time - lastCaptureTime >= captureInterval)
            {
                // Skip frame if still processing previous frame and frame skipping is enabled
                if (enableFrameSkipping && isProcessingFrame)
                {
                    skippedFrames++;
                    Debug.Log($"[QuestNav] Skipped frame (processing overload). Total skipped: {skippedFrames}");
                    return;
                }

                _ = CaptureFrameAsync();
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
        /// Capture a frame from Quest 3 passthrough camera (optimized async version)
        /// </summary>
        private async Task CaptureFrameAsync()
        {
            if (!isStreaming || webCamTexture == null || isProcessingFrame)
                return;

            isProcessingFrame = true;
            var startTime = Time.realtimeSinceStartup;

            try
            {
                if (webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
                {
                    if (enableOptimizations)
                    {
                        await ProcessFrameOptimized();
                    }
                    else
                    {
                        ProcessFrameSimple();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error in async frame capture: {ex.Message}");
            }
            finally
            {
                var processingTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                UpdatePerformanceMetrics(processingTime);
                isProcessingFrame = false;
            }
        }

        /// <summary>
        /// Optimized frame processing with safer approach
        /// </summary>
        private async Task ProcessFrameOptimized()
        {
            // Get pixels from WebCamTexture (original working method)
            Color32[] pixels = webCamTexture.GetPixels32();

            // Calculate target dimensions with scaling
            int targetWidth = Mathf.RoundToInt(webCamTexture.width * currentScale);
            int targetHeight = Mathf.RoundToInt(webCamTexture.height * currentScale);

            // Apply ROI cropping if enabled
            Color32[] processedPixels = pixels;
            int sourceWidth = webCamTexture.width;
            int sourceHeight = webCamTexture.height;

            if (enableROICropping)
            {
                // Extract ROI pixels
                int roiX = Mathf.RoundToInt(roiRect.x * sourceWidth);
                int roiY = Mathf.RoundToInt(roiRect.y * sourceHeight);
                int roiWidth = Mathf.RoundToInt(roiRect.width * sourceWidth);
                int roiHeight = Mathf.RoundToInt(roiRect.height * sourceHeight);

                processedPixels = ExtractROI(pixels, sourceWidth, sourceHeight, roiX, roiY, roiWidth, roiHeight);
                sourceWidth = roiWidth;
                sourceHeight = roiHeight;
            }

            // Scale if needed
            if (currentScale != 1.0f)
            {
                processedPixels = await Task.Run(() => ScalePixels(processedPixels, sourceWidth, sourceHeight, targetWidth, targetHeight));
                sourceWidth = targetWidth;
                sourceHeight = targetHeight;
            }

            // Create or resize capture texture if needed
            if (captureTexture == null ||
                captureTexture.width != sourceWidth ||
                captureTexture.height != sourceHeight)
            {
                if (captureTexture != null)
                    DestroyImmediate(captureTexture);

                captureTexture = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGB24, false);
            }

            // Set pixels and encode
            captureTexture.SetPixels32(processedPixels);
            captureTexture.Apply();

            // Encode to JPEG with current quality
            byte[] jpegData = captureTexture.EncodeToJPG(currentQuality);

            if (jpegData != null && jpegData.Length > 0)
            {
                // Process frame data on background thread
                await Task.Run(() =>
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
                });

                // Send frame to Java web server (back on main thread)
                SendFrameToJavaOptimized(jpegData);
            }
        }

        /// <summary>
        /// Extract Region of Interest from pixel array
        /// </summary>
        private Color32[] ExtractROI(Color32[] sourcePixels, int sourceWidth, int sourceHeight, int roiX, int roiY, int roiWidth, int roiHeight)
        {
            Color32[] roiPixels = new Color32[roiWidth * roiHeight];

            for (int y = 0; y < roiHeight; y++)
            {
                for (int x = 0; x < roiWidth; x++)
                {
                    int sourceIndex = (roiY + y) * sourceWidth + (roiX + x);
                    int roiIndex = y * roiWidth + x;

                    if (sourceIndex >= 0 && sourceIndex < sourcePixels.Length)
                    {
                        roiPixels[roiIndex] = sourcePixels[sourceIndex];
                    }
                }
            }

            return roiPixels;
        }

        /// <summary>
        /// Simple bilinear scaling of pixel array
        /// </summary>
        private Color32[] ScalePixels(Color32[] sourcePixels, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            Color32[] scaledPixels = new Color32[targetWidth * targetHeight];

            float xRatio = (float)sourceWidth / targetWidth;
            float yRatio = (float)sourceHeight / targetHeight;

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Mathf.FloorToInt(x * xRatio);
                    int sourceY = Mathf.FloorToInt(y * yRatio);

                    sourceX = Mathf.Clamp(sourceX, 0, sourceWidth - 1);
                    sourceY = Mathf.Clamp(sourceY, 0, sourceHeight - 1);

                    int sourceIndex = sourceY * sourceWidth + sourceX;
                    int targetIndex = y * targetWidth + x;

                    scaledPixels[targetIndex] = sourcePixels[sourceIndex];
                }
            }

            return scaledPixels;
        }

        /// <summary>
        /// Simple frame processing (optimized for speed)
        /// </summary>
        private void ProcessFrameSimple()
        {
            try
            {
                // Get reusable texture
                Texture2D workingTexture = GetWorkingTexture();

                if (workingTexture == null)
                {
                    Debug.LogWarning("[QuestNav] No working texture available");
                    return;
                }

                // Get pixels from WebCamTexture
                Color32[] pixels = webCamTexture.GetPixels32();

                // Set pixels and apply (this is the main bottleneck)
                workingTexture.SetPixels32(pixels);
                workingTexture.Apply(false); // false = don't generate mipmaps for speed

                if (enableAsyncEncoding)
                {
                    // Encode asynchronously to avoid blocking main thread
                    _ = EncodeAndSendAsync(workingTexture, pixels);
                }
                else
                {
                    // Synchronous encoding (original method)
                    byte[] jpegData = workingTexture.EncodeToJPG(currentQuality);
                    ProcessEncodedFrame(jpegData);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error in simple frame processing: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronous encoding to avoid blocking main thread
        /// </summary>
        private async Task EncodeAndSendAsync(Texture2D texture, Color32[] pixels)
        {
            try
            {
                // Create a copy of the texture data for background processing
                byte[] jpegData = await Task.Run(() =>
                {
                    // Create temporary texture for encoding on background thread
                    // Note: This approach may not work on all platforms due to Unity threading restrictions
                    // Fallback to main thread encoding if needed
                    return texture.EncodeToJPG(currentQuality);
                });

                // Process the encoded frame back on main thread
                ProcessEncodedFrame(jpegData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error in async encoding: {ex.Message}");
                // Fallback to synchronous encoding
                byte[] jpegData = texture.EncodeToJPG(currentQuality);
                ProcessEncodedFrame(jpegData);
            }
        }

        /// <summary>
        /// Process encoded frame data
        /// </summary>
        private void ProcessEncodedFrame(byte[] jpegData)
        {
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

                // Send frame to Java web server (async to avoid blocking)
                _ = Task.Run(() => SendFrameToJavaOptimized(jpegData));
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
        /// Send camera frame to Java web server (optimized version)
        /// </summary>
        private void SendFrameToJavaOptimized(byte[] jpegData)
        {
            try
            {
                // Try direct byte array transfer first (if supported)
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass webServerClass = new AndroidJavaClass("com.questnav.webserver.QuestNavWebServer"))
                {
                    // Check if direct byte array method exists
                    try
                    {
                        webServerClass.CallStatic("updateCameraFrameDirect", jpegData);
                    }
                    catch
                    {
                        // Fallback to base64 if direct method not available
                        string base64Data = System.Convert.ToBase64String(jpegData);
                        webServerClass.CallStatic("updateCameraFrameBase64", base64Data);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNav] Error sending frame to Java: {ex.Message}");
            }
        }

        /// <summary>
        /// Update performance metrics and adjust quality/resolution dynamically
        /// </summary>
        private void UpdatePerformanceMetrics(float processingTimeMs)
        {
            frameCount++;
            averageProcessingTime = (averageProcessingTime * (frameCount - 1) + processingTimeMs) / frameCount;

            // Adaptive quality adjustment
            if (enableAdaptiveQuality && frameCount % 30 == 0) // Adjust every 30 frames
            {
                if (averageProcessingTime > maxProcessingTimeMs)
                {
                    // Performance is poor, reduce quality/resolution
                    if (currentQuality > minJpegQuality)
                    {
                        currentQuality = Mathf.Max(minJpegQuality, currentQuality - 5);
                        Debug.Log($"[QuestNav] Reduced JPEG quality to {currentQuality} (avg processing: {averageProcessingTime:F1}ms)");
                    }
                    else if (enableResolutionScaling && currentScale > lowBandwidthScale)
                    {
                        currentScale = Mathf.Max(lowBandwidthScale, currentScale - 0.1f);
                        Debug.Log($"[QuestNav] Reduced resolution scale to {currentScale:F1} (avg processing: {averageProcessingTime:F1}ms)");
                    }
                }
                else if (averageProcessingTime < maxProcessingTimeMs * 0.7f)
                {
                    // Performance is good, can increase quality/resolution
                    if (currentScale < 1.0f)
                    {
                        currentScale = Mathf.Min(1.0f, currentScale + 0.1f);
                        Debug.Log($"[QuestNav] Increased resolution scale to {currentScale:F1} (avg processing: {averageProcessingTime:F1}ms)");
                    }
                    else if (currentQuality < maxJpegQuality)
                    {
                        currentQuality = Mathf.Min(maxJpegQuality, currentQuality + 5);
                        Debug.Log($"[QuestNav] Increased JPEG quality to {currentQuality} (avg processing: {averageProcessingTime:F1}ms)");
                    }
                }
            }

            // Log performance stats periodically
            if (frameCount % 100 == 0)
            {
                Debug.Log($"[QuestNav] Performance: {averageProcessingTime:F1}ms avg, {skippedFrames} skipped frames, Quality: {currentQuality}, Scale: {currentScale:F1}");
            }
        }

        /// <summary>
        /// Initialize texture pool for reuse
        /// </summary>
        private void InitializeTexturePool()
        {
            texturePool = new Texture2D[TEXTURE_POOL_SIZE];
            // Textures will be created on-demand with correct dimensions
        }

        /// <summary>
        /// Get a working texture from the pool
        /// </summary>
        private Texture2D GetWorkingTexture()
        {
            if (!enableTextureReuse)
            {
                // Create new texture each time (original behavior)
                if (captureTexture == null ||
                    captureTexture.width != webCamTexture.width ||
                    captureTexture.height != webCamTexture.height)
                {
                    if (captureTexture != null)
                        DestroyImmediate(captureTexture);

                    captureTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
                }
                return captureTexture;
            }

            // Use texture pool
            int currentIndex = texturePoolIndex;
            texturePoolIndex = (texturePoolIndex + 1) % TEXTURE_POOL_SIZE;

            if (texturePool[currentIndex] == null ||
                texturePool[currentIndex].width != webCamTexture.width ||
                texturePool[currentIndex].height != webCamTexture.height)
            {
                if (texturePool[currentIndex] != null)
                    DestroyImmediate(texturePool[currentIndex]);

                texturePool[currentIndex] = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            }

            return texturePool[currentIndex];
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

            if (tempRenderTexture != null)
            {
                tempRenderTexture.Release();
                tempRenderTexture = null;
            }

            // Clean up texture pool
            if (texturePool != null)
            {
                for (int i = 0; i < texturePool.Length; i++)
                {
                    if (texturePool[i] != null)
                    {
                        DestroyImmediate(texturePool[i]);
                        texturePool[i] = null;
                    }
                }
                texturePool = null;
            }

            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                    webCamTexture.Stop();
                DestroyImmediate(webCamTexture);
                webCamTexture = null;
            }
        }

        #region Public API Extensions
        /// <summary>
        /// Set adaptive quality parameters at runtime
        /// </summary>
        public void SetAdaptiveQuality(bool enabled, int minQuality = 30, int maxQuality = 90)
        {
            enableAdaptiveQuality = enabled;
            minJpegQuality = minQuality;
            maxJpegQuality = maxQuality;
            currentQuality = Mathf.Clamp(currentQuality, minQuality, maxQuality);
        }

        /// <summary>
        /// Set resolution scaling parameters at runtime
        /// </summary>
        public void SetResolutionScaling(bool enabled, float lowBandwidthScale = 0.5f)
        {
            enableResolutionScaling = enabled;
            this.lowBandwidthScale = lowBandwidthScale;
        }

        /// <summary>
        /// Set ROI cropping parameters at runtime
        /// </summary>
        public void SetROICropping(bool enabled, Rect roi = default)
        {
            enableROICropping = enabled;
            if (roi != default)
                roiRect = roi;
        }

        /// <summary>
        /// Get current performance statistics
        /// </summary>
        public (float avgProcessingTime, int frameCount, int skippedFrames, int currentQuality, float currentScale) GetPerformanceStats()
        {
            return (averageProcessingTime, frameCount, skippedFrames, currentQuality, currentScale);
        }

        /// <summary>
        /// Reset performance statistics
        /// </summary>
        public void ResetPerformanceStats()
        {
            averageProcessingTime = 0f;
            frameCount = 0;
            skippedFrames = 0;
        }
        #endregion
        #endregion
    }
}
