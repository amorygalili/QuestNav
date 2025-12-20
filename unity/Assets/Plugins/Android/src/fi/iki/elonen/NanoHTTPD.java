package fi.iki.elonen;

import java.io.BufferedInputStream;
import java.io.BufferedReader;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.Closeable;
import java.io.DataOutput;
import java.io.DataOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.PrintWriter;
import java.io.RandomAccessFile;
import java.io.UnsupportedEncodingException;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.SocketException;
import java.net.SocketTimeoutException;
import java.net.URLDecoder;
import java.nio.ByteBuffer;
import java.nio.channels.FileChannel;
import java.nio.charset.Charset;
import java.nio.charset.CharsetEncoder;
import java.security.KeyStore;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Collections;
import java.util.Date;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.StringTokenizer;
import java.util.TimeZone;

/**
 * A simple, tiny, nicely embeddable HTTP server in Java
 * <p/>
 * <p/>
 * NanoHTTPD
 * <p>
 * Copyright (c) 2012-2013 by Paul S. Hawke, 2001,2005-2013 by Jarno Elonen,
 * 2010 by Konstantinos Togias
 * </p>
 * <p/>
 * <p/>
 * <b>Features + limitations: </b>
 * <ul>
 * <li>Only one Java file</li>
 * <li>Java 5 compatible</li>
 * <li>Released as open source, Modified BSD licence</li>
 * <li>No fixed config files, logging, authorization etc. (Implement yourself if
 * you need them.)</li>
 * <li>Supports parameter parsing of GET and POST methods (+ rudimentary PUT
 * support in 1.25)</li>
 * <li>Supports both dynamic content and file serving</li>
 * <li>Supports file upload (since version 1.2, 2010)</li>
 * <li>Supports partial content (streaming)</li>
 * <li>Supports ETags</li>
 * <li>Never caches anything</li>
 * <li>Doesn't limit bandwidth, request time or simultaneous connections</li>
 * <li>Default code serves files and shows all HTTP parameters and headers</li>
 * <li>File server supports directory listing, index.html and index.htm</li>
 * <li>File server supports partial content (streaming)</li>
 * <li>File server supports ETags</li>
 * <li>File server does the 301 redirection trick for directories without '/'</li>
 * <li>File server supports simple skipping for files (continue download)</li>
 * <li>File server serves also very long files without memory overhead</li>
 * <li>Contains a built-in list of most common MIME types</li>
 * <li>All header names are converted to lower case so they don't vary between
 * browsers/clients</li>
 * </ul>
 * <p/>
 * <p/>
 * <b>How to use: </b>
 * <ul>
 * <li>Subclass and implement serve() and embed to your own program</li>
 * </ul>
 * <p/>
 * See the separate "LICENSE.md" file for the distribution license (Modified BSD
 * licence)
 */
public abstract class NanoHTTPD {
    /**
     * Maximum time to wait on Socket.getInputStream().read() (in milliseconds)
     * This is required as the Keep-Alive HTTP connections would otherwise block
     * the socket reading thread forever (or as long the browser is open).
     */
    public static final int SOCKET_READ_TIMEOUT = 5000;
    /**
     * Common MIME type for dynamic content: plain text
     */
    public static final String MIME_PLAINTEXT = "text/plain";
    /**
     * Common MIME type for dynamic content: html
     */
    public static final String MIME_HTML = "text/html";
    /**
     * Pseudo-Parameter to use to store the actual query string in the
     * parameters map for later re-processing.
     */
    private static final String QUERY_STRING_PARAMETER = "NanoHttpd.QUERY_STRING";
    private static final String CONTENT_LENGTH = "content-length";
    private static final String CONTENT_TYPE = "content-type";
    private final String hostname;
    private final int myPort;
    private ServerSocket myServerSocket;
    private Thread myThread;
    private volatile boolean running = false;
    private static final String TAG = "NanoHTTPD";

    /**
     * Constructs an HTTP server on given port.
     */
    public NanoHTTPD(int port) {
        this(null, port);
    }

    /**
     * Constructs an HTTP server on given hostname and port.
     */
    public NanoHTTPD(String hostname, int port) {
        this.hostname = hostname;
        this.myPort = port;
    }

    /**
     * Start the server.
     *
     * @throws IOException if the socket is in use.
     */
    public void start() throws IOException {
        start(SOCKET_READ_TIMEOUT);
    }

    /**
     * Start the server.
     *
     * @param timeout The socket timeout to use.
     * @throws IOException if the socket is in use.
     */
    public void start(final int timeout) throws IOException {
        start(timeout, false);
    }

    /**
     * Start the server.
     *
     * @param timeout The socket timeout to use.
     * @param daemon  Indicates whether or not the thread is a daemon thread
     * @throws IOException if the socket is in use.
     */
    public synchronized void start(final int timeout, boolean daemon) throws IOException {
        if (running) {
            return;
        }
        running = true;
        myServerSocket = new ServerSocket();
        myServerSocket.setReuseAddress(true);
        myServerSocket.bind(hostname != null ? new InetSocketAddress(hostname, myPort) : new InetSocketAddress(myPort));
        myThread = new Thread(new Runnable() {
            @Override
            public void run() {
                do {
                    try {
                        final Socket finalAccept = myServerSocket.accept();
                        finalAccept.setSoTimeout(timeout);
                        final InputStream inputStream = finalAccept.getInputStream();
                        new Thread(new Runnable() {
                            @Override
                            public void run() {
                                try {
                                    processClientRequest(inputStream, finalAccept);
                                } catch (Exception e) {
                                    System.err.println(TAG + ": Error processing client request: " + e.getMessage());
                                    e.printStackTrace();
                                } finally {
                                    safeClose(finalAccept);
                                }
                            }
                        }).start();
                    } catch (IOException e) {
                        if (running) {
                            System.err.println(TAG + ": Communication error: " + e.getMessage());
                            e.printStackTrace();
                        }
                    }
                } while (running);
            }
        });
        myThread.setDaemon(daemon);
        myThread.start();
    }

    /**
     * Stop the server.
     */
    public synchronized void stop() {
        running = false;
        if (myServerSocket != null) {
            try {
                myServerSocket.close();
                myServerSocket = null;
            } catch (IOException e) {
                System.err.println(TAG + ": Error closing server socket: " + e.getMessage());
            }
        }
        if (myThread != null) {
            myThread.interrupt();
            myThread = null;
        }
    }

    /**
     * Process client request.
     *
     * @param inputStream The input stream from the client.
     * @param socket      The client socket.
     */
    private void processClientRequest(InputStream inputStream, Socket socket) {
        try {
            BufferedReader reader = new BufferedReader(new InputStreamReader(inputStream));
            String line = reader.readLine();
            if (line == null) {
                return;
            }

            StringTokenizer st = new StringTokenizer(line);
            if (!st.hasMoreTokens()) {
                return;
            }

            String method = st.nextToken();
            if (!st.hasMoreTokens()) {
                return;
            }

            String uri = st.nextToken();

            Map<String, String> headers = new HashMap<>();
            while ((line = reader.readLine()) != null && !line.trim().isEmpty()) {
                int p = line.indexOf(':');
                if (p >= 0) {
                    headers.put(line.substring(0, p).trim().toLowerCase(Locale.US), line.substring(p + 1).trim());
                }
            }

            Map<String, String> params = new HashMap<>();
            if (method.equalsIgnoreCase("POST")) {
                String contentType = headers.get("content-type");
                int contentLength = Integer.parseInt(headers.getOrDefault("content-length", "0"));

                if (contentLength > 0) {
                    char[] postData = new char[contentLength];
                    int readLength = reader.read(postData);
                    if (readLength > 0) {
                        String postDataString = new String(postData, 0, readLength);

                        if (contentType != null && contentType.toLowerCase().contains("application/x-www-form-urlencoded")) {
                            parseQueryString(postDataString, params);
                        } else if (contentType != null && contentType.toLowerCase().contains("application/json")) {
                            // For JSON, store the raw data in a special parameter
                            params.put("postData", postDataString);
                        }
                    }
                }
            }

            // Parse query parameters from URI
            int queryIndex = uri.indexOf('?');
            if (queryIndex >= 0) {
                parseQueryString(uri.substring(queryIndex + 1), params);
                uri = uri.substring(0, queryIndex);
            }

            // Create session
            IHTTPSession session = new HTTPSession(method, uri, headers, params);

            // Handle the request
            Response response = serve(session);
            if (response == null) {
                response = new Response(Response.Status.NOT_FOUND, MIME_PLAINTEXT, "Not Found");
            }

            // Send the response
            OutputStream outputStream = socket.getOutputStream();
            response.send(outputStream);
        } catch (IOException e) {
            System.err.println(TAG + ": Error processing client request: " + e.getMessage());
        }
    }

    /**
     * Parse query string into parameters map.
     *
     * @param queryString The query string to parse.
     * @param params      The parameters map to populate.
     */
    private void parseQueryString(String queryString, Map<String, String> params) {
        if (queryString != null) {
            StringTokenizer st = new StringTokenizer(queryString, "&");
            while (st.hasMoreTokens()) {
                String token = st.nextToken();
                int eq = token.indexOf('=');
                if (eq >= 0) {
                    try {
                        String key = URLDecoder.decode(token.substring(0, eq), "UTF-8");
                        String value = URLDecoder.decode(token.substring(eq + 1), "UTF-8");
                        params.put(key, value);
                    } catch (UnsupportedEncodingException e) {
                        System.err.println(TAG + ": Error decoding query parameter: " + e.getMessage());
                    }
                }
            }
        }
    }

    /**
     * Safely close a closeable object.
     *
     * @param closeable The object to close.
     */
    private void safeClose(Closeable closeable) {
        if (closeable != null) {
            try {
                closeable.close();
            } catch (IOException e) {
                System.err.println(TAG + ": Error closing resource: " + e.getMessage());
            }
        }
    }

    /**
     * Override this to customize the server.
     *
     * @param session The HTTP session
     * @return The response to send to the client
     */
    public abstract Response serve(IHTTPSession session);

    /**
     * HTTP response.
     * Return one of these from serve().
     */
    public static class Response {
        /**
         * HTTP status code after processing, e.g. "200 OK", Status.OK
         */
        private Status status;
        /**
         * MIME type of content, e.g. "text/html"
         */
        private String mimeType;
        /**
         * Data of the response, may be null.
         */
        private InputStream data;
        /**
         * Headers for the HTTP response. Use addHeader() to add lines.
         */
        private Map<String, String> headers = new HashMap<>();
        /**
         * The request method that spawned this response.
         */
        private Method requestMethod;
        /**
         * Use chunkedTransfer
         */
        private boolean chunkedTransfer;

        /**
         * Default constructor: response = Status.OK, mime = "text/html",
         * data = null
         */
        public Response() {
            this(Status.OK, MIME_HTML, (InputStream)null);
        }

        /**
         * Basic constructor.
         */
        public Response(Status status, String mimeType, InputStream data) {
            this.status = status;
            this.mimeType = mimeType;
            this.data = data;
        }

        /**
         * Convenience method that makes an InputStream out of given text.
         */
        public Response(Status status, String mimeType, String txt) {
            this.status = status;
            this.mimeType = mimeType;
            try {
                this.data = txt != null ? new ByteArrayInputStream(txt.getBytes("UTF-8")) : null;
            } catch (UnsupportedEncodingException e) {
                System.err.println(TAG + ": Error creating response: " + e.getMessage());
            }
        }

        /**
         * Adds given line to the header.
         */
        public void addHeader(String name, String value) {
            headers.put(name, value);
        }

        /**
         * Sends the response to the socket.
         */
        protected void send(OutputStream outputStream) {
            try {
                String mime = mimeType;
                SimpleDateFormat gmtFrmt = new SimpleDateFormat("E, d MMM yyyy HH:mm:ss 'GMT'", Locale.US);
                gmtFrmt.setTimeZone(TimeZone.getTimeZone("GMT"));

                // Write the response headers
                PrintWriter pw = new PrintWriter(outputStream);
                pw.print("HTTP/1.1 " + status.getDescription() + " \r\n");
                pw.print("Content-Type: " + mime + "\r\n");
                if (headers == null || headers.get("Date") == null) {
                    pw.print("Date: " + gmtFrmt.format(new Date()) + "\r\n");
                }
                for (Map.Entry<String, String> entry : headers.entrySet()) {
                    pw.print(entry.getKey() + ": " + entry.getValue() + "\r\n");
                }
                pw.print("\r\n");
                pw.flush();

                // Write the response data
                if (data != null) {
                    byte[] buff = new byte[4096];
                    int read;
                    while ((read = data.read(buff)) > 0) {
                        outputStream.write(buff, 0, read);
                    }
                }
                outputStream.flush();
                if (data != null) {
                    try {
                        data.close();
                    } catch (IOException e) {
                        System.err.println(TAG + ": Error closing data stream: " + e.getMessage());
                    }
                }
            } catch (IOException e) {
                System.err.println(TAG + ": Error sending response: " + e.getMessage());
            }
        }

        /**
         * Some HTTP response status codes
         */
        public enum Status {
            OK(200, "OK"),
            CREATED(201, "Created"),
            ACCEPTED(202, "Accepted"),
            NO_CONTENT(204, "No Content"),
            PARTIAL_CONTENT(206, "Partial Content"),
            REDIRECT(301, "Moved Permanently"),
            NOT_MODIFIED(304, "Not Modified"),
            BAD_REQUEST(400, "Bad Request"),
            UNAUTHORIZED(401, "Unauthorized"),
            FORBIDDEN(403, "Forbidden"),
            NOT_FOUND(404, "Not Found"),
            METHOD_NOT_ALLOWED(405, "Method Not Allowed"),
            RANGE_NOT_SATISFIABLE(416, "Requested Range Not Satisfiable"),
            INTERNAL_ERROR(500, "Internal Server Error");

            private final int requestStatus;
            private final String description;

            Status(int requestStatus, String description) {
                this.requestStatus = requestStatus;
                this.description = description;
            }

            public int getRequestStatus() {
                return requestStatus;
            }

            public String getDescription() {
                return "" + requestStatus + " " + description;
            }
        }

        /**
         * HTTP request methods.
         */
        public enum Method {
            GET, PUT, POST, DELETE, HEAD, OPTIONS;

            public static Method lookup(String method) {
                for (Method m : Method.values()) {
                    if (m.toString().equalsIgnoreCase(method)) {
                        return m;
                    }
                }
                return null;
            }
        }
    }

    /**
     * HTTP request session.
     */
    public interface IHTTPSession {
        /**
         * Get the request method.
         *
         * @return The request method.
         */
        String getMethod();

        /**
         * Get the URI.
         *
         * @return The URI.
         */
        String getUri();

        /**
         * Get the headers.
         *
         * @return The headers.
         */
        Map<String, String> getHeaders();

        /**
         * Get the parameters.
         *
         * @return The parameters.
         */
        Map<String, String> getParameters();

        /**
         * Parse the body as form data.
         *
         * @param files Map to store uploaded files.
         * @throws IOException            If an error occurs.
         * @throws ResponseException      If an error occurs.
         */
        void parseBody(Map<String, String> files) throws IOException, ResponseException;
    }

    /**
     * HTTP session implementation.
     */
    private class HTTPSession implements IHTTPSession {
        private final String method;
        private final String uri;
        private final Map<String, String> headers;
        private final Map<String, String> params;

        public HTTPSession(String method, String uri, Map<String, String> headers, Map<String, String> params) {
            this.method = method;
            this.uri = uri;
            this.headers = headers;
            this.params = params;
        }

        @Override
        public String getMethod() {
            return method;
        }

        @Override
        public String getUri() {
            return uri;
        }

        @Override
        public Map<String, String> getHeaders() {
            return headers;
        }

        @Override
        public Map<String, String> getParameters() {
            return params;
        }

        @Override
        public void parseBody(Map<String, String> files) throws IOException, ResponseException {
            // This is a simplified implementation
            // In a real implementation, this would parse multipart form data
        }
    }

    /**
     * Exception that can be thrown by the server in case of an error.
     */
    public static class ResponseException extends Exception {
        private final Response.Status status;

        public ResponseException(Response.Status status, String message) {
            super(message);
            this.status = status;
        }

        public Response.Status getStatus() {
            return status;
        }
    }
}
