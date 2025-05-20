package com.questnav.webserver;

import android.content.Context;
import android.net.nsd.NsdManager;
import android.net.nsd.NsdServiceInfo;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;

import fi.iki.elonen.NanoHTTPD;

/**
 * QuestNavWebServer - A simple web server for QuestNav that serves a web interface
 * and provides API endpoints for controlling the QuestNav app.
 */
public class QuestNavWebServer extends NanoHTTPD {
    private static final String TAG = "QuestNavWebServer";
    private static final String SERVICE_NAME = "QuestNav";
    private static final String SERVICE_TYPE = "_http._tcp.";
    private static final int DEFAULT_PORT = 8080;

    private final Context context;
    private NsdManager nsdManager;
    private NsdManager.RegistrationListener registrationListener;
    private String webInterfacePath;
    private boolean isRunning = false;

    /**
     * Create a new QuestNavWebServer
     * @param context Android context
     * @param webInterfacePath Path to the web interface files
     */
    public QuestNavWebServer(Context context, String webInterfacePath) {
        super(DEFAULT_PORT);
        this.context = context;
        this.webInterfacePath = webInterfacePath;
    }

    /**
     * Start the web server and register the mDNS service
     */
    public void start() {
        if (isRunning) {
            return;
        }

        try {
            super.start(NanoHTTPD.SOCKET_READ_TIMEOUT, false);
            registerService();
            isRunning = true;
            Log.d(TAG, "Web server started on port " + DEFAULT_PORT);
        } catch (IOException e) {
            Log.e(TAG, "Failed to start web server", e);
        }
    }

    /**
     * Stop the web server and unregister the mDNS service
     */
    @Override
    public void stop() {
        if (!isRunning) {
            return;
        }

        unregisterService();
        super.stop();
        isRunning = false;
        Log.d(TAG, "Web server stopped");
    }

    /**
     * Register the mDNS service for discovery as questnav.local
     */
    private void registerService() {
        nsdManager = (NsdManager) context.getSystemService(Context.NSD_SERVICE);
        
        NsdServiceInfo serviceInfo = new NsdServiceInfo();
        serviceInfo.setServiceName(SERVICE_NAME);
        serviceInfo.setServiceType(SERVICE_TYPE);
        serviceInfo.setPort(DEFAULT_PORT);

        registrationListener = new NsdManager.RegistrationListener() {
            @Override
            public void onServiceRegistered(NsdServiceInfo serviceInfo) {
                Log.d(TAG, "Service registered: " + serviceInfo.getServiceName());
            }

            @Override
            public void onRegistrationFailed(NsdServiceInfo serviceInfo, int errorCode) {
                Log.e(TAG, "Service registration failed: " + errorCode);
            }

            @Override
            public void onServiceUnregistered(NsdServiceInfo serviceInfo) {
                Log.d(TAG, "Service unregistered: " + serviceInfo.getServiceName());
            }

            @Override
            public void onUnregistrationFailed(NsdServiceInfo serviceInfo, int errorCode) {
                Log.e(TAG, "Service unregistration failed: " + errorCode);
            }
        };

        try {
            nsdManager.registerService(serviceInfo, NsdManager.PROTOCOL_DNS_SD, registrationListener);
        } catch (Exception e) {
            Log.e(TAG, "Failed to register NSD service", e);
        }
    }

    /**
     * Unregister the mDNS service
     */
    private void unregisterService() {
        if (nsdManager != null && registrationListener != null) {
            try {
                nsdManager.unregisterService(registrationListener);
            } catch (Exception e) {
                Log.e(TAG, "Failed to unregister NSD service", e);
            }
            registrationListener = null;
        }
    }

    /**
     * Handle HTTP requests
     */
    @Override
    public Response serve(IHTTPSession session) {
        String uri = session.getUri();
        Log.d(TAG, "Request: " + session.getMethod() + " " + uri);

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
        Method method = session.getMethod();

        // Handle GET requests
        if (method == Method.GET) {
            if (uri.equals("/api/status")) {
                return handleStatusRequest();
            }
        }

        // Handle POST requests
        if (method == Method.POST) {
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
                Log.e(TAG, "Error parsing POST data", e);
                return newFixedLengthResponse(Response.Status.INTERNAL_ERROR, "application/json", 
                    "{\"error\": \"Failed to parse request\"}");
            }
        }

        return newFixedLengthResponse(Response.Status.NOT_FOUND, "application/json", 
            "{\"error\": \"API endpoint not found\"}");
    }

    /**
     * Handle status request
     */
    private Response handleStatusRequest() {
        // Call Unity to get the current status
        String status = UnityPlayer.UnitySendMessage("QuestNavWebInterface", "GetStatus", "");
        return newFixedLengthResponse(Response.Status.OK, "application/json", status);
    }

    /**
     * Handle team number update
     */
    private Response handleTeamNumberUpdate(String postData) {
        try {
            JSONObject json = new JSONObject(postData);
            String teamNumber = json.getString("teamNumber");
            
            // Call Unity to update the team number
            UnityPlayer.UnitySendMessage("QuestNavWebInterface", "UpdateTeamNumber", teamNumber);
            
            return newFixedLengthResponse(Response.Status.OK, "application/json", 
                "{\"success\": true}");
        } catch (JSONException e) {
            Log.e(TAG, "Error parsing team number JSON", e);
            return newFixedLengthResponse(Response.Status.BAD_REQUEST, "application/json", 
                "{\"error\": \"Invalid JSON\"}");
        }
    }

    /**
     * Handle connect to simulation request
     */
    private Response handleConnectToSim() {
        // Call Unity to connect to simulation
        UnityPlayer.UnitySendMessage("QuestNavWebInterface", "ConnectToSim", "");
        
        return newFixedLengthResponse(Response.Status.OK, "application/json", 
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
                
                // Read file
                FileInputStream fis = new FileInputStream(file);
                BufferedInputStream bis = new BufferedInputStream(fis);
                
                return newChunkedResponse(Response.Status.OK, mimeType, bis);
            }
            
            // If file not found, serve index.html for SPA routing
            if (!uri.equals("index.html")) {
                File indexFile = new File(webInterfacePath, "index.html");
                if (indexFile.exists()) {
                    FileInputStream fis = new FileInputStream(indexFile);
                    BufferedInputStream bis = new BufferedInputStream(fis);
                    
                    return newChunkedResponse(Response.Status.OK, "text/html", bis);
                }
            }
        } catch (IOException e) {
            Log.e(TAG, "Error serving file: " + uri, e);
        }

        // Return 404 if file not found
        return newFixedLengthResponse(Response.Status.NOT_FOUND, "text/plain", "File not found");
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
