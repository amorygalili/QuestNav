using System;
using System.IO;
using System.Threading.Tasks;
using QuestNav.Core;
using QuestNav.Network;
using UnityEngine;
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
        #endregion

        #region Private Fields
        private AndroidJavaObject webServer;
        private bool isWebServerRunning = false;
        private string webInterfacePath;
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
        #endregion

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
                    Debug.Log("[QuestNavWebInterface] Extracting web interface files");

                    // On Android, we need to use WWW to access StreamingAssets
                    if (Application.platform == RuntimePlatform.Android)
                    {
                        ExtractFilesOnAndroid();
                    }
                    else
                    {
                        // Extract files from StreamingAssets
                        string sourcePath = Path.Combine(Application.streamingAssetsPath, "webinterface");

                        // Copy all files from StreamingAssets to persistent data path
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
                // Create a minimal web interface if the files don't exist
                string indexHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>QuestNav Web Interface</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #0F172A; color: white; }
        h1 { color: #38BDF8; }
        .card { background-color: #1E293B; padding: 20px; margin-bottom: 20px; border-radius: 8px; }
        button { background-color: #38BDF8; color: white; border: none; padding: 10px 15px; border-radius: 4px; cursor: pointer; }
        button:hover { background-color: #0EA5E9; }
        input { padding: 8px; margin-right: 10px; border-radius: 4px; border: 1px solid #ccc; }
        .status { margin-top: 10px; }
        .connected { color: #10B981; }
        .disconnected { color: #EF4444; }
    </style>
</head>
<body>
    <h1>QuestNav Web Interface</h1>
    <div class='card'>
        <h2>Connection Settings</h2>
        <div>
            <input type='text' id='teamNumber' placeholder='Team Number'>
            <button onclick='updateTeamNumber()'>Update</button>
            <button onclick='connectToSim()' style='background-color: #FB923C;'>Connect to Sim</button>
        </div>
    </div>
    <div class='card'>
        <h2>Status</h2>
        <div id='status'>Loading...</div>
    </div>

    <script>
        // Fetch status every 2 seconds
        setInterval(fetchStatus, 2000);
        fetchStatus();

        function fetchStatus() {
            fetch('/api/status')
                .then(response => response.json())
                .then(data => {
                    const statusDiv = document.getElementById('status');
                    statusDiv.innerHTML = `
                        <div class='status'>
                            <p><strong>Connection:</strong> <span class='${data.isConnected ? 'connected' : 'disconnected'}'>${data.isConnected ? 'Connected' : 'Disconnected'}</span></p>
                            <p><strong>State:</strong> ${data.connectionState}</p>
                            <p><strong>IP Address:</strong> ${data.ipAddress || 'N/A'}</p>
                            <p><strong>Team Number:</strong> ${data.teamNumber}</p>
                            <p><strong>Battery:</strong> ${data.batteryPercent.toFixed(1)}%${data.isCharging ? ' (Charging)' : ''}</p>
                            <p><strong>Tracking:</strong> ${data.currentlyTracking ? 'Active' : 'Lost'}</p>
                        </div>
                    `;
                    document.getElementById('teamNumber').value = data.teamNumber;
                })
                .catch(error => {
                    document.getElementById('status').innerHTML = '<p>Error connecting to server</p>';
                });
        }

        function updateTeamNumber() {
            const teamNumber = document.getElementById('teamNumber').value;
            fetch('/api/team', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ teamNumber }),
            })
            .then(response => {
                if (response.ok) {
                    fetchStatus();
                }
            });
        }

        function connectToSim() {
            fetch('/api/sim', {
                method: 'POST',
            })
            .then(response => {
                if (response.ok) {
                    fetchStatus();
                }
            });
        }
    </script>
</body>
</html>";

                // Write the index.html file
                string indexPath = Path.Combine(webInterfacePath, "index.html");
                File.WriteAllText(indexPath, indexHtml);

                Debug.Log("[QuestNavWebInterface] Created minimal web interface");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestNavWebInterface] Error extracting files on Android: {ex.Message}");
            }
        }

        /// <summary>
        /// Copy files recursively from source to destination
        /// </summary>
        private void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            // Create the target directory if it doesn't exist
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            // Copy all files
            foreach (string filePath in Directory.GetFiles(sourcePath))
            {
                string fileName = Path.GetFileName(filePath);
                string destFile = Path.Combine(targetPath, fileName);
                File.Copy(filePath, destFile, true);
            }

            // Copy all subdirectories
            foreach (string dirPath in Directory.GetDirectories(sourcePath))
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

                    // Get the Android context
                    AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");

                    // Create the web server
                    AndroidJavaClass webServerClass = new AndroidJavaClass("com.questnav.webserver.QuestNavWebServer");
                    webServer = new AndroidJavaObject("com.questnav.webserver.QuestNavWebServer", context, webInterfacePath);

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
