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
                    "assets/index-BMTqiGKw.js",
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
}
