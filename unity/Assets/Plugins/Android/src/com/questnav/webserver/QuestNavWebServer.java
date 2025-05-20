package com.questnav.webserver;

import java.io.BufferedInputStream;
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;

import com.questnav.unity.UnityBridge;

import fi.iki.elonen.NanoHTTPD;
import fi.iki.elonen.NanoHTTPD.Response;
import fi.iki.elonen.NanoHTTPD.IHTTPSession;
import fi.iki.elonen.NanoHTTPD.ResponseException;

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
            }
        }

        // Handle POST requests
        if (method.equalsIgnoreCase("POST")) {
            try {
                Map<String, String> files = new HashMap<>();
                session.parseBody(files);
                String postData = files.get("postData");

                if (uri.equals("/api/team")) {
                    return handleTeamNumberUpdate(postData);
                } else if (uri.equals("/api/sim")) {
                    return handleConnectToSim();
                }
            } catch (IOException | ResponseException e) {
                System.err.println(TAG + ": Error parsing POST data: " + e.getMessage());
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

        // Default to index.html for root or directory paths
        if (uri.equals("/") || uri.endsWith("/")) {
            uri += "index.html";
        }

        // Remove leading slash
        if (uri.startsWith("/")) {
            uri = uri.substring(1);
        }

        try {
            // Try to find the file in the web interface directory
            File file = new File(webInterfacePath, uri);
            if (file.exists() && file.isFile()) {
                // Determine MIME type
                String mimeType = getMimeTypeForFile(uri);

                System.out.println(TAG + ": Serving file: " + file.getAbsolutePath() + " (" + mimeType + ")");

                // Read file
                FileInputStream fis = new FileInputStream(file);
                BufferedInputStream bis = new BufferedInputStream(fis);

                return new Response(Response.Status.OK, mimeType, bis);
            }

            // If file not found, serve index.html for SPA routing
            if (!uri.equals("index.html")) {
                File indexFile = new File(webInterfacePath, "index.html");
                if (indexFile.exists()) {
                    System.out.println(TAG + ": File not found, serving index.html instead: " + uri);

                    FileInputStream fis = new FileInputStream(indexFile);
                    BufferedInputStream bis = new BufferedInputStream(fis);

                    return new Response(Response.Status.OK, "text/html", bis);
                }
            }

            System.err.println(TAG + ": File not found: " + file.getAbsolutePath());
        } catch (IOException e) {
            System.err.println(TAG + ": Error serving file: " + uri + " - " + e.getMessage());
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
}
