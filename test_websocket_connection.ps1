# QuestNav WebSocket Connection Test Script
# This script tests the WebSocket connection between the Quest and your computer

# NetworkTables port used by QuestNav
$NT_PORT = 5810

# Function to display section headers
function Write-Header {
    param([string]$text)
    Write-Host "`n===== $text =====" -ForegroundColor Cyan
}

# Function to display success messages
function Write-Success {
    param([string]$text)
    Write-Host $text -ForegroundColor Green
}

# Function to display warning messages
function Write-Warning {
    param([string]$text)
    Write-Host $text -ForegroundColor Yellow
}

# Function to display error messages
function Write-Error {
    param([string]$text)
    Write-Host $text -ForegroundColor Red
}

# Function to display info messages
function Write-Info {
    param([string]$text)
    Write-Host $text -ForegroundColor White
}

# Check if ADB is available
Write-Header "ADB AVAILABILITY CHECK"
try {
    $adbVersion = adb version
    Write-Success "ADB found: $adbVersion"
} catch {
    Write-Error "Error: ADB not found in PATH. Please install Android SDK Platform Tools and add them to your PATH."
    exit 1
}

# Check if Quest is connected
Write-Header "QUEST DEVICE CHECK"
$devices = adb devices
if ($devices -match "device$") {
    Write-Success "Quest device found and connected."
} else {
    Write-Error "No Quest device found or device is not properly connected."
    exit 1
}

# Check port forwarding
Write-Header "PORT FORWARDING CHECK"
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if (-not $ntForwarding) {
    Write-Warning "Setting up port forwarding for NetworkTables port $NT_PORT..."
    $result = adb forward tcp:$NT_PORT tcp:$NT_PORT
    if ($?) {
        Write-Success "Port forwarding set up successfully!"
    } else {
        Write-Error "Failed to set up port forwarding."
        exit 1
    }
} else {
    Write-Success "Port forwarding is already set up: $ntForwarding"
}

# Test TCP connection to the NetworkTables port
Write-Header "TCP CONNECTION TEST"
try {
    Write-Info "Testing TCP connection to localhost:$NT_PORT..."
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $result = $tcpClient.BeginConnect("127.0.0.1", $NT_PORT, $null, $null)
    $success = $result.AsyncWaitHandle.WaitOne(1000, $true)
    
    if ($success) {
        $tcpClient.EndConnect($result)
        Write-Success "TCP connection to localhost:$NT_PORT successful!"
        $tcpClient.Close()
    } else {
        Write-Error "TCP connection to localhost:$NT_PORT failed."
        Write-Info "Make sure your NetworkTables server is running and listening on port $NT_PORT."
        exit 1
    }
} catch {
    Write-Error "Error testing TCP connection: $_"
    exit 1
}

# Create a simple WebSocket test on the Quest
Write-Header "WEBSOCKET TEST FROM QUEST"
Write-Info "Creating a simple WebSocket test script on the Quest..."

$webSocketTestScript = @"
#!/system/bin/sh
# Simple WebSocket test script

# Test if we can reach the host
ping -c 1 127.0.0.1
if [ \$? -ne 0 ]; then
  echo "Cannot ping localhost (127.0.0.1)"
  exit 1
fi

# Try to connect to the WebSocket port
nc -z -w 5 127.0.0.1 $NT_PORT
if [ \$? -ne 0 ]; then
  echo "Cannot connect to port $NT_PORT on localhost"
  exit 1
else
  echo "Successfully connected to port $NT_PORT on localhost"
fi

# Try to make a WebSocket handshake
(
echo -e "GET /nt/QuestNav HTTP/1.1\r"
echo -e "Host: 127.0.0.1:$NT_PORT\r"
echo -e "Upgrade: websocket\r"
echo -e "Connection: Upgrade\r"
echo -e "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r"
echo -e "Sec-WebSocket-Version: 13\r"
echo -e "\r"
) | nc 127.0.0.1 $NT_PORT
"@

# Push the test script to the Quest
$webSocketTestScript | Out-File -FilePath "websocket_test.sh" -Encoding ASCII
adb push websocket_test.sh /data/local/tmp/
adb shell "chmod 755 /data/local/tmp/websocket_test.sh"

# Run the test script
Write-Info "Running WebSocket test on the Quest..."
$testResult = adb shell "/data/local/tmp/websocket_test.sh"
Write-Info "Test result:"
Write-Info $testResult

# Check for success indicators in the test result
if ($testResult -match "Successfully connected") {
    Write-Success "TCP connection test from Quest to localhost:$NT_PORT was successful."
} else {
    Write-Warning "TCP connection test from Quest to localhost:$NT_PORT failed."
}

if ($testResult -match "101 Switching Protocols" -or $testResult -match "Upgrade: websocket") {
    Write-Success "WebSocket handshake appears to be successful!"
} else {
    Write-Warning "WebSocket handshake may have failed."
    Write-Info "This could indicate that the server is not properly configured for WebSocket connections."
}

# Clean up
Remove-Item -Path "websocket_test.sh" -Force -ErrorAction SilentlyContinue

# Provide recommendations
Write-Header "RECOMMENDATIONS"
Write-Info "Based on the test results:"

if ($testResult -match "Successfully connected" -and ($testResult -match "101 Switching Protocols" -or $testResult -match "Upgrade: websocket")) {
    Write-Success "The basic WebSocket connection appears to be working correctly."
    Write-Info "If your QuestNav app is still having trouble connecting, the issue might be with:"
    Write-Info "1. The NetworkTables protocol implementation"
    Write-Info "2. Authentication or handshake details specific to NetworkTables"
    Write-Info "3. The WebSocket library used in the app"
    
    Write-Info "`nTry these steps:"
    Write-Info "1. Check the QuestNav logs for specific error messages"
    Write-Info "2. Verify that your NetworkTables server supports the NT4 protocol"
    Write-Info "3. Ensure the server is configured to accept WebSocket connections"
} else {
    Write-Warning "There appears to be an issue with the WebSocket connection."
    Write-Info "Try these steps:"
    Write-Info "1. Make sure your NetworkTables server is running and configured correctly"
    Write-Info "2. Check if any firewall is blocking the connection"
    Write-Info "3. Verify that the server supports WebSocket connections"
    Write-Info "4. Try restarting both the server and the Quest app"
}

Write-Info "`nTo monitor logs in real-time, run the monitor_networktables_logs.ps1 script."
