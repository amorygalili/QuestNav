package com.questnav.webserver;

import java.io.BufferedInputStream;
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;
import java.util.Base64;

import com.questnav.unity.UnityBridge;

import fi.iki.elonen.NanoHTTPD;

/**
 * QuestNavWebServer - A simple web server for QuestNav that serves a web interface
 * and provides API endpoints for controlling the QuestNav app.
 */
public class QuestNavWebServer extends NanoHTTPD {
    private static final String TAG = "QuestNavWebServer";
    private static final int DEFAULT_PORT = 8080;

    private String webInterfacePath;
    private boolean isRunning = false;
    private String cachedStatus = null;

    /**
     * Create a new QuestNavWebServer
     * @param webInterfacePath Path to the web interface files
     */
    public QuestNavWebServer(String webInterfacePath) {
        super(DEFAULT_PORT);
        this.webInterfacePath = webInterfacePath;
        System.out.println(TAG + ": Created web server with interface path: " + webInterfacePath);
    }

    /**
     * Start the web server
     */
    public void start() {
        if (isRunning) {
            return;
        }

        try {
            super.start(NanoHTTPD.SOCKET_READ_TIMEOUT, false);
            isRunning = true;
            System.out.println(TAG + ": Web server started on port " + DEFAULT_PORT);
        } catch (IOException e) {
            System.err.println(TAG + ": Failed to start web server: " + e.getMessage());
            e.printStackTrace();
        }
    }

    /**
     * Stop the web server
     */
    @Override
    public void stop() {
        if (!isRunning) {
            return;
        }

        super.stop();
        isRunning = false;
        System.out.println(TAG + ": Web server stopped");
    }

    /**
     * Update the status information from Unity
     * This is called from Unity to update the cached status
     * @param status JSON string with status information
     */
    public void updateStatus(String status) {
        this.cachedStatus = status;
        System.out.println(TAG + ": Status updated from Unity");
    }

    /**
     * Handle HTTP requests
     */
    @Override
    public Response serve(IHTTPSession session) {
        String uri = session.getUri();
        System.out.println(TAG + ": Request: " + session.getMethod() + " " + uri);

        // Handle API requests
        if (uri.startsWith("/api/")) {
            return handleApiRequest(session);
        }

        // Serve web interface files
        return serveWebInterface(session);
    }



    /**
     * Handle API requests
     */
    private Response handleApiRequest(IHTTPSession session) {
        String uri = session.getUri();
        String method = session.getMethod();

        // Handle GET requests
        if (method.equalsIgnoreCase("GET")) {
            if (uri.equals("/api/status")) {
                return handleStatusRequest();
            } else if (uri.equals("/api/camera/stream")) {
                return handleCameraStream();
            } else if (uri.equals("/api/camera/frame")) {
                return handleCameraFrame();
            } else if (uri.equals("/api/camera/settings")) {
                return handleGetCameraSettings();
            } else if (uri.equals("/api/camera/debug")) {
                return handleCameraDebug();
            }
        }

        // Handle POST requests
        if (method.equalsIgnoreCase("POST")) {
            try {
                String postData = null;

                // Check Content-Type to determine how to parse the body
                Map<String, String> headers = session.getHeaders();
                String contentType = headers.get("content-type");

                if (contentType != null && contentType.toLowerCase().contains("application/json")) {
                    // For JSON content, get the data from parameters (set by NanoHTTPD)
                    postData = session.getParameters().get("postData");
                    System.out.println("[QuestNav] Received JSON POST data: " + postData);
                } else {
                    // For form data, use the existing parseBody method
                    Map<String, String> files = new HashMap<>();
                    session.parseBody(files);
                    postData = files.get("postData");
                    System.out.println("[QuestNav] Received form POST data: " + postData);
                }

                if (uri.equals("/api/team")) {
                    return handleTeamNumberUpdate(postData);
                } else if (uri.equals("/api/sim")) {
                    return handleConnectToSim();
                } else if (uri.equals("/api/camera/start")) {
                    return handleStartCameraStreaming();
                } else if (uri.equals("/api/camera/stop")) {
                    return handleStopCameraStreaming();
                } else if (uri.equals("/api/camera/settings")) {
                    return handleUpdateCameraSettings(postData);
                } else if (uri.equals("/api/camera/reset")) {
                    return handleResetCameraStats();
                }
            } catch (IOException | ResponseException e) {
                System.err.println("[QuestNav] Error parsing POST data: " + e.getMessage());
                return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                    "{\"error\": \"Failed to parse request\"}");
            }
        }

        return new Response(Response.Status.NOT_FOUND, "application/json",
            "{\"error\": \"API endpoint not found\"}");
    }

    /**
     * Handle status request
     */
    private Response handleStatusRequest() {
        // Use the cached status if available
        if (cachedStatus != null && !cachedStatus.isEmpty()) {
            return new Response(Response.Status.OK, "application/json", cachedStatus);
        }

        // Fallback to a placeholder response if no cached status is available
        String statusJson = "{" +
            "\"isConnected\": false," +
            "\"connectionState\": \"Disconnected\"," +
            "\"ipAddress\": \"\"," +
            "\"teamNumber\": \"9999\"," +
            "\"batteryPercent\": 100," +
            "\"isCharging\": false," +
            "\"deviceModel\": \"Quest 3\"," +
            "\"deviceName\": \"Quest\"," +
            "\"operatingSystem\": \"Android\"," +
            "\"systemMemorySize\": 8192," +
            "\"processorCount\": 8," +
            "\"processorFrequency\": 2800," +
            "\"processorType\": \"Snapdragon XR2 Gen 2\"," +
            "\"graphicsDeviceName\": \"Adreno 740\"," +
            "\"graphicsMemorySize\": 4096," +
            "\"graphicsDeviceVersion\": \"OpenGL ES 3.2\"," +
            "\"currentlyTracking\": true" +
            "}";

        return new Response(Response.Status.OK, "application/json", statusJson);
    }

    /**
     * Handle team number update
     */
    private Response handleTeamNumberUpdate(String postData) {
        try {
            // Simple parsing to extract team number from JSON
            // Format expected: {"teamNumber":"1234"}
            String teamNumber = "9999"; // Default

            if (postData != null && postData.contains("teamNumber")) {
                int startIndex = postData.indexOf("teamNumber") + 13; // "teamNumber":"
                int endIndex = postData.indexOf("\"", startIndex);
                if (startIndex > 0 && endIndex > startIndex) {
                    teamNumber = postData.substring(startIndex, endIndex);
                }
            }

            System.out.println(TAG + ": Team number updated to " + teamNumber);

            // Call Unity to update the team number
            try {
                UnityBridge.sendMessage("QuestNavWebInterface", "UpdateTeamNumber", teamNumber);
                System.out.println(TAG + ": Sent team number update to Unity: " + teamNumber);
            } catch (Exception e) {
                System.err.println(TAG + ": Error sending team number to Unity: " + e.getMessage());
            }

            return new Response(Response.Status.OK, "application/json",
                "{\"success\": true}");
        } catch (Exception e) {
            System.err.println(TAG + ": Error parsing team number: " + e.getMessage());
            return new Response(Response.Status.BAD_REQUEST, "application/json",
                "{\"error\": \"Invalid request\"}");
        }
    }

    /**
     * Handle connect to simulation request
     */
    private Response handleConnectToSim() {
        System.out.println(TAG + ": Connect to simulation requested");

        // Call Unity to connect to simulation
        try {
            UnityBridge.sendMessage("QuestNavWebInterface", "ConnectToSim", "");
            System.out.println(TAG + ": Sent connect to sim request to Unity");
        } catch (Exception e) {
            System.err.println(TAG + ": Error sending connect to sim request to Unity: " + e.getMessage());
        }

        return new Response(Response.Status.OK, "application/json",
            "{\"success\": true}");
    }

    /**
     * Serve web interface files
     */
    private Response serveWebInterface(IHTTPSession session) {
        String uri = session.getUri();
        System.out.println("[QuestNav] serveWebInterface: Original URI: " + uri);
        System.out.println("[QuestNav] serveWebInterface: Web interface path: " + webInterfacePath);

        // Default to index.html for root or directory paths
        if (uri.equals("/") || uri.endsWith("/")) {
            uri += "index.html";
            System.out.println("[QuestNav] serveWebInterface: Modified URI to: " + uri);
        }

        // Remove leading slash
        if (uri.startsWith("/")) {
            uri = uri.substring(1);
            System.out.println("[QuestNav] serveWebInterface: Removed leading slash, URI now: " + uri);
        }

        try {
            // Try to find the file in the web interface directory
            File file = new File(webInterfacePath, uri);
            System.out.println("[QuestNav] serveWebInterface: Looking for file: " + file.getAbsolutePath());
            System.out.println("[QuestNav] serveWebInterface: File exists: " + file.exists());
            System.out.println("[QuestNav] serveWebInterface: Is file: " + file.isFile());

            // List directory contents for debugging
            File webDir = new File(webInterfacePath);
            if (webDir.exists() && webDir.isDirectory()) {
                File[] files = webDir.listFiles();
                System.out.println("[QuestNav] serveWebInterface: Web directory contains " + (files != null ? files.length : 0) + " items:");
                if (files != null) {
                    for (File f : files) {
                        System.out.println("[QuestNav] serveWebInterface:   - " + f.getName() + " (isFile: " + f.isFile() + ", isDir: " + f.isDirectory() + ")");
                    }
                }
            } else {
                System.err.println("[QuestNav] serveWebInterface: Web interface directory does not exist or is not a directory: " + webInterfacePath);
            }

            if (file.exists() && file.isFile()) {
                // Determine MIME type
                String mimeType = getMimeTypeForFile(uri);

                System.out.println("[QuestNav] serveWebInterface: Serving file: " + file.getAbsolutePath() + " (" + mimeType + ")");

                // Read file
                FileInputStream fis = new FileInputStream(file);
                BufferedInputStream bis = new BufferedInputStream(fis);

                return new Response(Response.Status.OK, mimeType, bis);
            }

            // If file not found, serve index.html for SPA routing
            if (!uri.equals("index.html")) {
                File indexFile = new File(webInterfacePath, "index.html");
                System.out.println("[QuestNav] serveWebInterface: Trying fallback to index.html: " + indexFile.getAbsolutePath());
                System.out.println("[QuestNav] serveWebInterface: Index file exists: " + indexFile.exists());

                if (indexFile.exists()) {
                    System.out.println("[QuestNav] serveWebInterface: File not found, serving index.html instead: " + uri);

                    FileInputStream fis = new FileInputStream(indexFile);
                    BufferedInputStream bis = new BufferedInputStream(fis);

                    return new Response(Response.Status.OK, "text/html", bis);
                }
            }

            System.err.println("[QuestNav] serveWebInterface: File not found: " + file.getAbsolutePath());
        } catch (IOException e) {
            System.err.println("[QuestNav] serveWebInterface: Error serving file: " + uri + " - " + e.getMessage());
            e.printStackTrace();
        }

        // Return 404 if file not found
        return new Response(Response.Status.NOT_FOUND, "text/plain", "File not found");
    }

    /**
     * Get MIME type for file
     */
    private String getMimeTypeForFile(String uri) {
        if (uri.endsWith(".html")) return "text/html";
        if (uri.endsWith(".css")) return "text/css";
        if (uri.endsWith(".js")) return "application/javascript";
        if (uri.endsWith(".json")) return "application/json";
        if (uri.endsWith(".png")) return "image/png";
        if (uri.endsWith(".jpg") || uri.endsWith(".jpeg")) return "image/jpeg";
        if (uri.endsWith(".svg")) return "image/svg+xml";
        if (uri.endsWith(".ico")) return "image/x-icon";
        return "application/octet-stream";
    }

    /**
     * Handle camera stream request (MJPEG streaming)
     */
    private Response handleCameraStream() {
        System.out.println("[QuestNav] Camera stream requested");

        try {
            // Start camera streaming in Unity
            UnityBridge.sendMessage("QuestNavWebInterface", "StartCameraStreaming", "");

            // Create MJPEG stream response
            return new Response(Response.Status.OK, "multipart/x-mixed-replace; boundary=frame",
                new CameraStreamInputStream());
        } catch (Exception e) {
            System.err.println("[QuestNav] Error starting camera stream: " + e.getMessage());
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to start camera stream\"}");
        }
    }

    /**
     * Handle single camera frame request
     */
    private Response handleCameraFrame() {
        System.out.println("[QuestNav] Single camera frame requested");

        try {
            // Get latest frame from Unity
            byte[] frameData = getLatestCameraFrame();

            System.out.println("[QuestNav] Frame data: " +
                (frameData != null ? frameData.length + " bytes" : "null"));

            if (frameData != null && frameData.length > 0) {
                System.out.println("[QuestNav] Returning frame data as JPEG");
                return new Response(Response.Status.OK, "image/jpeg",
                    new ByteArrayInputStream(frameData));
            } else {
                System.out.println("[QuestNav] No frame data available - returning 204");
                return new Response(Response.Status.NO_CONTENT, "application/json",
                    "{\"error\": \"No frame available\"}");
            }
        } catch (Exception e) {
            System.err.println("[QuestNav] Error getting camera frame: " + e.getMessage());
            e.printStackTrace();
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to get camera frame\"}");
        }
    }

    /**
     * Handle start camera streaming request
     */
    private Response handleStartCameraStreaming() {
        System.out.println("[QuestNav] Start camera streaming requested");

        try {
            UnityBridge.sendMessage("QuestNavWebInterface", "StartCameraStreaming", "");
            return new Response(Response.Status.OK, "application/json",
                "{\"success\": true}");
        } catch (Exception e) {
            System.err.println("[QuestNav] Error starting camera streaming: " + e.getMessage());
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to start camera streaming\"}");
        }
    }

    /**
     * Handle stop camera streaming request
     */
    private Response handleStopCameraStreaming() {
        System.out.println("[QuestNav] Stop camera streaming requested");

        try {
            UnityBridge.sendMessage("QuestNavWebInterface", "StopCameraStreaming", "");
            return new Response(Response.Status.OK, "application/json",
                "{\"success\": true}");
        } catch (Exception e) {
            System.err.println("[QuestNav] Error stopping camera streaming: " + e.getMessage());
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to stop camera streaming\"}");
        }
    }

    /**
     * Handle camera debug request
     */
    private Response handleCameraDebug() {
        System.out.println("[QuestNav] Camera debug info requested");

        try {
            // Get latest frame from Unity
            byte[] frameData = getLatestCameraFrame();

            // Try to ping Unity for streaming status (fire-and-forget)
            try {
                UnityBridge.sendMessage("QuestNavWebInterface", "IsCameraStreaming", "");
            } catch (Exception e) {
                System.err.println("[QuestNav] Error checking streaming status: " + e.getMessage());
            }

            String debugInfo = String.format(
                "{" +
                "\"frameDataAvailable\": %s," +
                "\"frameDataSize\": %d," +
                "\"timestamp\": %d" +
                "}",
                frameData != null,
                frameData != null ? frameData.length : 0,
                System.currentTimeMillis()
            );

            System.out.println("[QuestNav] Debug info: " + debugInfo);
            return new Response(Response.Status.OK, "application/json", debugInfo);
        } catch (Exception e) {
            System.err.println("[QuestNav] Error getting camera debug info: " + e.getMessage());
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to get camera debug info\"}");
        }
    }

    /**
     * Handle get camera settings request
     */
    private Response handleGetCameraSettings() {
        System.out.println("[QuestNav] Get camera settings requested");

        try {
            // Request camera settings from Unity
            System.out.println("[QuestNav] Sending GetCameraSettings message to Unity");
            UnityBridge.sendMessage("QuestNavWebInterface", "GetCameraSettings", "");

            // Wait a moment for Unity to respond
            try {
                Thread.sleep(100); // Give Unity time to respond
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }

            // Return cached settings
            synchronized (cameraSettingsLock) {
                System.out.println("[QuestNav] Returning camera settings: " + latestCameraSettings);
                return new Response(Response.Status.OK, "application/json", latestCameraSettings);
            }
        } catch (Exception e) {
            System.err.println("[QuestNav] Error getting camera settings: " + e.getMessage());
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to get camera settings\"}");
        }
    }

    /**
     * Handle update camera settings request
     */
    private Response handleUpdateCameraSettings(String postData) {
        System.out.println("[QuestNav] Update camera settings requested");

        try {
            if (postData != null && !postData.isEmpty()) {
                // Send settings to Unity
                UnityBridge.sendMessage("QuestNavWebInterface", "UpdateCameraSettings", postData);

                return new Response(Response.Status.OK, "application/json",
                    "{\"success\": true, \"message\": \"Camera settings updated\"}");
            } else {
                return new Response(Response.Status.BAD_REQUEST, "application/json",
                    "{\"error\": \"No settings data provided\"}");
            }
        } catch (Exception e) {
            System.err.println("[QuestNav] Error updating camera settings: " + e.getMessage());
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to update camera settings\"}");
        }
    }

    /**
     * Handle reset camera stats request
     */
    private Response handleResetCameraStats() {
        System.out.println("[QuestNav] Reset camera stats requested");

        try {
            // Reset camera stats in Unity
            UnityBridge.sendMessage("QuestNavWebInterface", "ResetCameraStats", "");

            return new Response(Response.Status.OK, "application/json",
                "{\"success\": true, \"message\": \"Camera stats reset\"}");
        } catch (Exception e) {
            System.err.println("[QuestNav] Error resetting camera stats: " + e.getMessage());
            return new Response(Response.Status.INTERNAL_ERROR, "application/json",
                "{\"error\": \"Failed to reset camera stats\"}");
        }
    }

    // Static field to store the latest camera frame from Unity
    private static byte[] latestCameraFrame = null;
    private static final Object cameraFrameLock = new Object();

    // Static field to store camera settings from Unity
    private static String latestCameraSettings = "{}";
    private static final Object cameraSettingsLock = new Object();

    /**
     * Get latest camera frame from Unity
     */
    private byte[] getLatestCameraFrame() {
        synchronized (cameraFrameLock) {
            return latestCameraFrame;
        }
    }

    /**
     * Called from Unity to update the latest camera frame
     * This method is called via UnitySendMessage from the Unity side
     */
    public static void updateCameraFrame(byte[] frameData) {
        System.out.println("[QuestNav] Received camera frame from Unity: " +
                          (frameData != null ? frameData.length + " bytes" : "null"));

        synchronized (cameraFrameLock) {
            latestCameraFrame = frameData;
            if (frameData != null && frameData.length > 0) {
                System.out.println("[QuestNav] Successfully stored frame data");
            } else {
                System.out.println("[QuestNav] Warning: Received null or empty frame data");
            }
        }
    }

    /**
     * Called from Unity to update the latest camera frame (string version for UnitySendMessage)
     * Unity will call this method with base64 encoded frame data
     */
    public static void updateCameraFrameBase64(String base64Data) {
        try {
            if (base64Data != null && !base64Data.isEmpty()) {
                byte[] frameData = Base64.getDecoder().decode(base64Data);
                updateCameraFrame(frameData);
            } else {
                System.out.println("[QuestNav] Received empty camera frame data from Unity");
                updateCameraFrame(null);
            }
        } catch (Exception e) {
            System.err.println("[QuestNav] Error decoding base64 camera frame: " + e.getMessage());
            updateCameraFrame(null);
        }
    }

    /**
     * Called from Unity to update the latest camera frame (direct byte array version - optimized)
     * This method avoids base64 encoding overhead for better performance
     */
    public static void updateCameraFrameDirect(byte[] frameData) {
        System.out.println("[QuestNav] Received camera frame from Unity (direct): " +
                          (frameData != null ? frameData.length + " bytes" : "null"));
        updateCameraFrame(frameData);
    }

    /**
     * Called from Unity to update camera settings
     * This method is called via UnitySendMessage from the Unity side
     */
    public static void updateCameraSettings(String settingsJson) {
        System.out.println("[QuestNav] Received camera settings from Unity: " +
                          (settingsJson != null ? settingsJson.length() + " chars" : "null"));

        synchronized (cameraSettingsLock) {
            latestCameraSettings = settingsJson != null ? settingsJson : "{}";
        }
    }

    /**
     * Inner class for MJPEG streaming
     */
    private class CameraStreamInputStream extends InputStream {
        private boolean isStreaming = true;
        private byte[] currentFrame = null;
        private int framePosition = 0;
        private boolean headerSent = false;

        @Override
        public int read() throws IOException {
            if (!isStreaming) {
                return -1; // End of stream
            }

            // Send MJPEG frame header if needed
            if (!headerSent || (currentFrame != null && framePosition >= currentFrame.length)) {
                getNextFrame();
            }

            if (currentFrame != null && framePosition < currentFrame.length) {
                return currentFrame[framePosition++] & 0xFF;
            }

            return -1;
        }

        private void getNextFrame() {
            try {
                // Get next frame from Unity
                byte[] frameData = getLatestCameraFrame();

                if (frameData != null && frameData.length > 0) {
                    // Create MJPEG frame with boundary
                    String header = "\r\n--frame\r\nContent-Type: image/jpeg\r\nContent-Length: " +
                                  frameData.length + "\r\n\r\n";
                    byte[] headerBytes = header.getBytes();

                    currentFrame = new byte[headerBytes.length + frameData.length];
                    System.arraycopy(headerBytes, 0, currentFrame, 0, headerBytes.length);
                    System.arraycopy(frameData, 0, currentFrame, headerBytes.length, frameData.length);

                    framePosition = 0;
                    headerSent = true;
                } else {
                    // No frame available, wait adaptively (shorter wait for better responsiveness)
                    try {
                        Thread.sleep(16); // ~60 FPS polling for better responsiveness
                    } catch (InterruptedException e) {
                        Thread.currentThread().interrupt();
                        isStreaming = false;
                    }
                }
            } catch (Exception e) {
                System.err.println("[QuestNav] Error in camera stream: " + e.getMessage());
                isStreaming = false;
            }
        }

        @Override
        public void close() throws IOException {
            isStreaming = false;
            super.close();
        }
    }
}
