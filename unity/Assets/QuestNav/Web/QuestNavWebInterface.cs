using System;
using System.IO;
using System.Threading.Tasks;
using QuestNav.Core;
using QuestNav.Network;
using QuestNav.Camera;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace QuestNav.Web
{
    /// <summary>
    /// Manages the web interface for QuestNav, providing a bridge between the Unity app and the web server.
    /// </summary>
    public class QuestNavWebInterface : MonoBehaviour
    {
        #region Serialized Fields
        [SerializeField] private NetworkTableConnection networkConnection;
        [SerializeField] private PassthroughCameraStreamer cameraStreamer;
        #endregion

        #region Private Fields
        private AndroidJavaObject webServer;
        private bool isWebServerRunning = false;
        private string webInterfacePath;
        private string cachedStatusJson;
        private float lastStatusUpdateTime;
        private const float STATUS_UPDATE_INTERVAL = 1.0f; // Update status every second
        #endregion

        #region Unity Lifecycle Methods
        /// <summary>
        /// Initialize the web interface
        /// </summary>
        private void Awake()
        {
            // Make sure this object persists across scenes
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Start the web server when the app starts
        /// </summary>
        private void Start()
        {
            // Extract web interface files if needed
            ExtractWebInterfaceFiles();

            // Start the web server
            StartWebServer();
        }

        /// <summary>
        /// Clean up when the app is closed
        /// </summary>
        private void OnDestroy()
        {
            StopWebServer();
        }

        /// <summary>
        /// Update is called once per frame
        /// </summary>
        private void Update()
        {
            // Update the status cache periodically
            if (Time.time - lastStatusUpdateTime > STATUS_UPDATE_INTERVAL)
            {
                UpdateStatusCache();
                lastStatusUpdateTime = Time.time;
            }
        }
        #endregion

        /// <summary>
        /// Update the cached status JSON
        /// </summary>
        private void UpdateStatusCache()
        {
            if (isWebServerRunning)
            {
                cachedStatusJson = GetStatus();

                // If the web server is running, update the status in the Java code
                if (webServer != null)
                {
                    try
                    {
                        webServer.Call("updateStatus", cachedStatusJson);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[QuestNavWebInterface] Error updating status in web server: {ex.Message}");
                    }
                }
            }
        }

        #region Public Methods
        /// <summary>
        /// Get the current status of the QuestNav app
        /// </summary>
        /// <returns>JSON string with status information</returns>
        public string GetStatus()
        {
            var status = new
            {
                isConnected = networkConnection.IsConnected,
                connectionState = networkConnection.ConnectionStateMessage,
                ipAddress = networkConnection.IPAddress,
                teamNumber = PlayerPrefs.GetString("TeamNumber", QuestNavConstants.Network.DEFAULT_TEAM_NUMBER),
                batteryPercent = SystemInfo.batteryLevel * 100f,
                isCharging = SystemInfo.batteryStatus == BatteryStatus.Charging,
                deviceModel = SystemInfo.deviceModel,
                deviceName = SystemInfo.deviceName,
                operatingSystem = SystemInfo.operatingSystem,
                systemMemorySize = SystemInfo.systemMemorySize,
                processorCount = SystemInfo.processorCount,
                processorFrequency = SystemInfo.processorFrequency,
                processorType = SystemInfo.processorType,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsMemorySize = SystemInfo.graphicsMemorySize,
                graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                currentlyTracking = OVRManager.isHmdPresent && OVRManager.hasInputFocus
            };

            return JsonConvert.SerializeObject(status);
        }

        /// <summary>
        /// Get the current status for the web server
        /// This is called from the Java code via UnitySendMessage
        /// </summary>
        public void GetStatusForWebServer(string message)
        {
            // Make sure the status is up to date
            if (string.IsNullOrEmpty(cachedStatusJson))
            {
                UpdateStatusCache();
            }

            // Log the status for debugging
            Debug.Log($"[QuestNavWebInterface] GetStatusForWebServer: {cachedStatusJson}");

            // The status is now sent to the Java code via the updateStatus method
            // which is called periodically in the Update method
        }

        /// <summary>
        /// Update the team number
        /// </summary>
        /// <param name="teamNumber">The new team number</param>
        public void UpdateTeamNumber(string teamNumber)
        {
            Debug.Log($"[QuestNavWebInterface] Updating team number to {teamNumber}");

            // Save the team number
            PlayerPrefs.SetString("TeamNumber", teamNumber);
            PlayerPrefs.Save();

            // Update the network connection
            networkConnection.UpdateTeamNumber(teamNumber);
        }

        /// <summary>
        /// Connect to simulation
        /// </summary>
        public void ConnectToSim()
        {
            Debug.Log("[QuestNavWebInterface] Connecting to simulation");

            // Update the team number to "localhost"
            UpdateTeamNumber("localhost");
        }

        /// <summary>
        /// Start camera streaming
        /// This is called from the Java code via UnitySendMessage
        /// </summary>
        public void StartCameraStreaming(string message)
        {
            Debug.Log("[QuestNavWebInterface] Starting camera streaming");

            if (cameraStreamer != null)
            {
                cameraStreamer.StartStreaming();
            }
            else
            {
                Debug.LogError("[QuestNavWebInterface] Camera streamer not found");
            }
        }

        /// <summary>
        /// Stop camera streaming
        /// This is called from the Java code via UnitySendMessage
        /// </summary>
        public void StopCameraStreaming(string message)
        {
            Debug.Log("[QuestNavWebInterface] Stopping camera streaming");

            if (cameraStreamer != null)
            {
                cameraStreamer.StopStreaming();
            }
        }

        /// <summary>
        /// Get the latest camera frame
        /// This is called from the Java code via UnitySendMessage
        /// </summary>
        public byte[] GetLatestCameraFrame()
        {
            if (cameraStreamer != null)
            {
                return cameraStreamer.GetLatestFrame();
            }
            return null;
        }

        /// <summary>
        /// Get the next camera frame from buffer
        /// This is called from the Java code via UnitySendMessage
        /// </summary>
        public byte[] GetNextCameraFrame()
        {
            if (cameraStreamer != null)
            {
                return cameraStreamer.GetNextFrame();
            }
            return null;
        }

        /// <summary>
        /// Check if camera streaming is active
        /// </summary>
        public bool IsCameraStreaming()
        {
            if (cameraStreamer != null)
            {
                return cameraStreamer.IsStreaming;
            }
            return false;
        }

        /// <summary>
        /// Get camera optimization settings (called from Java)
        /// This is called from the Java code via UnitySendMessage
        /// </summary>
        public void GetCameraSettings(string message)
        {
            Debug.Log("[QuestNavWebInterface] GetCameraSettings called");

            // Try to find camera streamer if not assigned
            if (cameraStreamer == null)
            {
                Debug.Log("[QuestNavWebInterface] Camera streamer is null, trying to find it");
                cameraStreamer = FindObjectOfType<PassthroughCameraStreamer>();

                if (cameraStreamer == null)
                {
                    Debug.LogError("[QuestNavWebInterface] Could not find PassthroughCameraStreamer in scene");
                }
                else
                {
                    Debug.Log("[QuestNavWebInterface] Found PassthroughCameraStreamer");
                }
            }

            string settingsJson = GetCameraSettingsJson();
            Debug.Log($"[QuestNavWebInterface] Camera settings JSON: {settingsJson}");

            // Send settings to Java web server
            try
            {
                using (AndroidJavaClass webServerClass = new AndroidJavaClass("com.questnav.webserver.QuestNavWebServer"))
                {
                    webServerClass.CallStatic("updateCameraSettings", settingsJson);
                }
                Debug.Log("[QuestNavWebInterface] Successfully sent camera settings to Java");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[QuestNavWebInterface] Error sending camera settings to Java: {ex.Message}");
            }
        }

        /// <summary>
        /// Get camera optimization settings as JSON string
        /// </summary>
        public string GetCameraSettingsJson()
        {
            Debug.Log("[QuestNavWebInterface] GetCameraSettingsJson called");

            if (cameraStreamer != null)
            {
                Debug.Log("[QuestNavWebInterface] Camera streamer found, getting settings");
                try
                {
                    var stats = cameraStreamer.GetPerformanceStats();
                    var optimizationSettings = GetCameraOptimizationSettings();

                    var cameraResponse = new CameraResponse
                    {
                        isStreaming = cameraStreamer.IsStreaming,
                        performance = new PerformanceData
                        {
                            avgProcessingTime = stats.avgProcessingTime,
                            frameCount = stats.frameCount,
                            skippedFrames = stats.skippedFrames,
                            currentQuality = stats.currentQuality,
                            currentScale = stats.currentScale
                        },
                        settings = optimizationSettings
                    };

                    string json = JsonUtility.ToJson(cameraResponse);
                    Debug.Log($"[QuestNavWebInterface] Generated camera settings JSON: {json}");
                    return json;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[QuestNavWebInterface] Error getting camera settings: {ex.Message}");
                    return GetDefaultCameraSettings();
                }
            }
            else
            {
                Debug.LogWarning("[QuestNavWebInterface] Camera streamer is null, returning default settings");
                return GetDefaultCameraSettings();
            }
        }

        /// <summary>
        /// Get default camera settings when camera streamer is not available
        /// </summary>
        private string GetDefaultCameraSettings()
        {
            var defaultResponse = new CameraResponse
            {
                isStreaming = false,
                performance = new PerformanceData
                {
                    avgProcessingTime = 0.0f,
                    frameCount = 0,
                    skippedFrames = 0,
                    currentQuality = 75,
                    currentScale = 1.0f
                },
                settings = new CameraSettingsData()
                {
                    enableOptimizations = true,
                    enableAdaptiveQuality = true,
                    minJpegQuality = 30,
                    maxJpegQuality = 90,
                    enableFrameSkipping = true,
                    maxProcessingTimeMs = 33.0f,
                    enableAsyncEncoding = true,
                    enableTextureReuse = true,
                    enableROICropping = false,
                    enableResolutionScaling = false,
                    lowBandwidthScale = 0.5f,
                    roiX = 0.25f,
                    roiY = 0.25f,
                    roiWidth = 0.5f,
                    roiHeight = 0.5f
                }
            };

            string json = JsonUtility.ToJson(defaultResponse);
            Debug.Log($"[QuestNavWebInterface] Using default camera settings: {json}");
            return json;
        }

        /// <summary>
        /// Update camera optimization settings
        /// </summary>
        public void UpdateCameraSettings(string settingsJson)
        {
            if (cameraStreamer == null) return;

            try
            {
                var settings = JsonUtility.FromJson<CameraSettingsData>(settingsJson);

                // Apply settings
                cameraStreamer.SetAdaptiveQuality(settings.enableAdaptiveQuality, settings.minJpegQuality, settings.maxJpegQuality);
                cameraStreamer.SetResolutionScaling(settings.enableResolutionScaling, settings.lowBandwidthScale);
                cameraStreamer.SetROICropping(settings.enableROICropping, new Rect(settings.roiX, settings.roiY, settings.roiWidth, settings.roiHeight));

                // Use reflection to set private fields
                var streamerType = cameraStreamer.GetType();
                SetPrivateField(streamerType, "enableOptimizations", settings.enableOptimizations);
                SetPrivateField(streamerType, "enableFrameSkipping", settings.enableFrameSkipping);
                SetPrivateField(streamerType, "enableAsyncEncoding", settings.enableAsyncEncoding);
                SetPrivateField(streamerType, "enableTextureReuse", settings.enableTextureReuse);
                SetPrivateField(streamerType, "maxProcessingTimeMs", settings.maxProcessingTimeMs);

                Debug.Log($"[QuestNav] Camera settings updated: Quality={settings.enableAdaptiveQuality}, Async={settings.enableAsyncEncoding}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[QuestNav] Error updating camera settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset camera performance statistics
        /// This is called from the Java code via UnitySendMessage
        /// </summary>
        public void ResetCameraStats(string message)
        {
            cameraStreamer?.ResetPerformanceStats();
            Debug.Log("[QuestNav] Camera performance stats reset");
        }

        private CameraSettingsData GetCameraOptimizationSettings()
        {
            if (cameraStreamer == null) return new CameraSettingsData();

            var streamerType = cameraStreamer.GetType();
            return new CameraSettingsData
            {
                enableOptimizations = GetPrivateField<bool>(streamerType, "enableOptimizations"),
                enableAdaptiveQuality = GetPrivateField<bool>(streamerType, "enableAdaptiveQuality"),
                minJpegQuality = GetPrivateField<int>(streamerType, "minJpegQuality"),
                maxJpegQuality = GetPrivateField<int>(streamerType, "maxJpegQuality"),
                enableFrameSkipping = GetPrivateField<bool>(streamerType, "enableFrameSkipping"),
                maxProcessingTimeMs = GetPrivateField<float>(streamerType, "maxProcessingTimeMs"),
                enableAsyncEncoding = GetPrivateField<bool>(streamerType, "enableAsyncEncoding"),
                enableTextureReuse = GetPrivateField<bool>(streamerType, "enableTextureReuse"),
                enableROICropping = GetPrivateField<bool>(streamerType, "enableROICropping"),
                enableResolutionScaling = GetPrivateField<bool>(streamerType, "enableResolutionScaling"),
                lowBandwidthScale = GetPrivateField<float>(streamerType, "lowBandwidthScale"),
                roiX = GetPrivateField<Rect>(streamerType, "roiRect").x,
                roiY = GetPrivateField<Rect>(streamerType, "roiRect").y,
                roiWidth = GetPrivateField<Rect>(streamerType, "roiRect").width,
                roiHeight = GetPrivateField<Rect>(streamerType, "roiRect").height
            };
        }

        private T GetPrivateField<T>(System.Type type, string fieldName)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(cameraStreamer);
            }
            return default(T);
        }

        private void SetPrivateField(System.Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(cameraStreamer, value);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Extract web interface files from the app bundle to a location accessible by the web server
        /// </summary>
        private void ExtractWebInterfaceFiles()
        {
            try
            {
                // Define the path where web interface files will be stored
                webInterfacePath = Path.Combine(Application.persistentDataPath, "webinterface");

                // Create the directory if it doesn't exist
                if (!Directory.Exists(webInterfacePath))
                {
                    Directory.CreateDirectory(webInterfacePath);
                }

                // Check if we need to extract files (check for index.html as indicator)
                string indexPath = Path.Combine(webInterfacePath, "index.html");
                if (!File.Exists(indexPath))
                {
                    Debug.Log("[QuestNavWebInterface] Extracting web interface files from StreamingAssets");

                    // On Android, we need to use UnityWebRequest to access StreamingAssets
                    if (Application.platform == RuntimePlatform.Android)
                    {
                        ExtractFilesOnAndroid();
                    }
                    else
                    {
                        // Extract files from StreamingAssets on other platforms
                        string sourcePath = Path.Combine(Application.streamingAssetsPath, "webinterface");
                        CopyFilesRecursively(sourcePath, webInterfacePath);
                    }
                }

                Debug.Log($"[QuestNavWebInterface] Web interface files ready at {webInterfacePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNavWebInterface] Error extracting web interface files: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract files on Android using UnityWebRequest
        /// </summary>
        private async void ExtractFilesOnAndroid()
        {
            try
            {
                Debug.Log("[QuestNavWebInterface] Starting Android file extraction");

                // List of files to extract (we need to know the file names since we can't list directory contents in APK)
                string[] filesToExtract = {
                    "index.html",
                    "favicon.svg",
                    "vite.svg",
                    "assets/index-DXs7gu7z.js",
                    "assets/index-CPuA3Y3i.css"
                };

                foreach (string fileName in filesToExtract)
                {
                    await ExtractFileFromStreamingAssets(fileName);
                }

                Debug.Log("[QuestNavWebInterface] Android file extraction completed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNavWebInterface] Error extracting files on Android: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract a single file from StreamingAssets on Android
        /// </summary>
        private async Task ExtractFileFromStreamingAssets(string fileName)
        {
            try
            {
                string sourceUrl = Path.Combine(Application.streamingAssetsPath, "webinterface", fileName).Replace('\\', '/');
                string destPath = Path.Combine(webInterfacePath, fileName);

                Debug.Log($"[QuestNavWebInterface] Extracting: {sourceUrl} -> {destPath}");

                // Create directory if needed
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    Debug.Log($"[QuestNavWebInterface] Created directory: {destDir}");
                }

                // Use UnityWebRequest to read the file from StreamingAssets
                using (var request = UnityWebRequest.Get(sourceUrl))
                {
                    var operation = request.SendWebRequest();

                    // Wait for the request to complete
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // Write the file to the destination
                        File.WriteAllBytes(destPath, request.downloadHandler.data);
                        Debug.Log($"[QuestNavWebInterface] Successfully extracted: {fileName}");
                    }
                    else
                    {
                        Debug.LogError($"[QuestNavWebInterface] Failed to extract {fileName}: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNavWebInterface] Error extracting {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Copy files recursively from source to destination
        /// </summary>
        private void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            Debug.Log($"[QuestNavWebInterface] CopyFilesRecursively: {sourcePath} -> {targetPath}");

            // Check if source directory exists
            if (!Directory.Exists(sourcePath))
            {
                Debug.LogError($"[QuestNavWebInterface] Source directory does not exist: {sourcePath}");
                return;
            }

            // Create the target directory if it doesn't exist
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
                Debug.Log($"[QuestNavWebInterface] Created target directory: {targetPath}");
            }

            // Copy all files (excluding .meta files)
            string[] files = Directory.GetFiles(sourcePath);
            Debug.Log($"[QuestNavWebInterface] Found {files.Length} files in {sourcePath}");

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);

                // Skip Unity .meta files
                if (fileName.EndsWith(".meta"))
                {
                    Debug.Log($"[QuestNavWebInterface] Skipping .meta file: {fileName}");
                    continue;
                }

                string destFile = Path.Combine(targetPath, fileName);
                File.Copy(filePath, destFile, true);
                Debug.Log($"[QuestNavWebInterface] Copied file: {fileName}");
            }

            // Copy all subdirectories
            string[] directories = Directory.GetDirectories(sourcePath);
            Debug.Log($"[QuestNavWebInterface] Found {directories.Length} subdirectories in {sourcePath}");

            foreach (string dirPath in directories)
            {
                string dirName = Path.GetFileName(dirPath);
                string destDir = Path.Combine(targetPath, dirName);
                CopyFilesRecursively(dirPath, destDir);
            }
        }

        /// <summary>
        /// Start the web server
        /// </summary>
        private void StartWebServer()
        {
            if (isWebServerRunning)
            {
                return;
            }

            try
            {
                // Only run on Android
                if (Application.platform == RuntimePlatform.Android)
                {
                    Debug.Log("[QuestNavWebInterface] Starting web server");

                    // Create the web server
                    AndroidJavaClass webServerClass = new AndroidJavaClass("com.questnav.webserver.QuestNavWebServer");
                    webServer = new AndroidJavaObject("com.questnav.webserver.QuestNavWebServer", webInterfacePath);

                    // Start the web server
                    webServer.Call("start");
                    isWebServerRunning = true;

                    Debug.Log("[QuestNavWebInterface] Web server started");
                }
                else
                {
                    Debug.Log("[QuestNavWebInterface] Web server only runs on Android");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNavWebInterface] Error starting web server: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the web server
        /// </summary>
        private void StopWebServer()
        {
            if (!isWebServerRunning || webServer == null)
            {
                return;
            }

            try
            {
                Debug.Log("[QuestNavWebInterface] Stopping web server");
                webServer.Call("stop");
                isWebServerRunning = false;
                Debug.Log("[QuestNavWebInterface] Web server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNavWebInterface] Error stopping web server: {ex.Message}");
            }
        }
        #endregion
    }

    /// <summary>
    /// Data structure for camera optimization settings
    /// </summary>
    [System.Serializable]
    public class CameraSettingsData
    {
        public bool enableOptimizations = false; // Disabled by default for safety
        public bool enableAdaptiveQuality = true;
        public int minJpegQuality = 30;
        public int maxJpegQuality = 90;
        public bool enableFrameSkipping = true;
        public float maxProcessingTimeMs = 33.0f;
        public bool enableAsyncEncoding = false; // Disabled by default for safety
        public bool enableTextureReuse = false; // Disabled by default for safety
        public bool enableROICropping = false;
        public bool enableResolutionScaling = false;
        public float lowBandwidthScale = 0.5f;
        public float roiX = 0.25f;
        public float roiY = 0.25f;
        public float roiWidth = 0.5f;
        public float roiHeight = 0.5f;
    }

    /// <summary>
    /// Data structure for camera performance statistics
    /// </summary>
    [System.Serializable]
    public class PerformanceData
    {
        public float avgProcessingTime;
        public int frameCount;
        public int skippedFrames;
        public int currentQuality;
        public float currentScale;
    }

    /// <summary>
    /// Data structure for complete camera response
    /// </summary>
    [System.Serializable]
    public class CameraResponse
    {
        public bool isStreaming;
        public PerformanceData performance;
        public CameraSettingsData settings;
    }
}
