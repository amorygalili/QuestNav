# QuestNav WebSocket Connection Test Script (Simple Version)
# This script tests the WebSocket connection between the Quest and your computer

# NetworkTables port used by QuestNav
$NT_PORT = 5810

# Check if ADB is available
Write-Host "`n===== ADB AVAILABILITY CHECK =====" -ForegroundColor Cyan
try {
    $adbVersion = adb version
    Write-Host "ADB found: $adbVersion" -ForegroundColor Green
} catch {
    Write-Host "Error: ADB not found in PATH. Please install Android SDK Platform Tools and add them to your PATH." -ForegroundColor Red
    exit 1
}

# Check if Quest is connected
Write-Host "`n===== QUEST DEVICE CHECK =====" -ForegroundColor Cyan
$devices = adb devices
if ($devices -match "device$") {
    Write-Host "Quest device found and connected." -ForegroundColor Green
} else {
    Write-Host "No Quest device found or device is not properly connected." -ForegroundColor Red
    exit 1
}

# Check port forwarding
Write-Host "`n===== PORT FORWARDING CHECK =====" -ForegroundColor Cyan
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if (-not $ntForwarding) {
    Write-Host "Setting up port forwarding for NetworkTables port $NT_PORT..." -ForegroundColor Yellow
    $result = adb forward tcp:$NT_PORT tcp:$NT_PORT
    if ($?) {
        Write-Host "Port forwarding set up successfully!" -ForegroundColor Green
    } else {
        Write-Host "Failed to set up port forwarding." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Port forwarding is already set up: $ntForwarding" -ForegroundColor Green
}

# Test TCP connection to the NetworkTables port
Write-Host "`n===== TCP CONNECTION TEST =====" -ForegroundColor Cyan
try {
    Write-Host "Testing TCP connection to localhost:$NT_PORT..." -ForegroundColor White
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $result = $tcpClient.BeginConnect("127.0.0.1", $NT_PORT, $null, $null)
    $success = $result.AsyncWaitHandle.WaitOne(1000, $true)
    
    if ($success) {
        $tcpClient.EndConnect($result)
        Write-Host "TCP connection to localhost:$NT_PORT successful!" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "TCP connection to localhost:$NT_PORT failed." -ForegroundColor Red
        Write-Host "Make sure your NetworkTables server is running and listening on port $NT_PORT." -ForegroundColor White
        exit 1
    }
} catch {
    Write-Host "Error testing TCP connection: $_" -ForegroundColor Red
    exit 1
}

# Create a simple WebSocket test on the Quest
Write-Host "`n===== WEBSOCKET TEST FROM QUEST =====" -ForegroundColor Cyan
Write-Host "Creating a simple WebSocket test script on the Quest..." -ForegroundColor White

$webSocketTestScript = @"
#!/system/bin/sh
# Simple WebSocket test script

echo "Testing connection to localhost:$NT_PORT..."

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
Write-Host "Running WebSocket test on the Quest..." -ForegroundColor White
$testResult = adb shell "/data/local/tmp/websocket_test.sh"
Write-Host "Test result:" -ForegroundColor White
Write-Host $testResult -ForegroundColor White

# Check for success indicators in the test result
if ($testResult -match "Successfully connected") {
    Write-Host "TCP connection test from Quest to localhost:$NT_PORT was successful." -ForegroundColor Green
} else {
    Write-Host "TCP connection test from Quest to localhost:$NT_PORT failed." -ForegroundColor Yellow
}

if ($testResult -match "101 Switching Protocols" -or $testResult -match "Upgrade: websocket") {
    Write-Host "WebSocket handshake appears to be successful!" -ForegroundColor Green
} else {
    Write-Host "WebSocket handshake may have failed." -ForegroundColor Yellow
    Write-Host "This could indicate that the server is not properly configured for WebSocket connections." -ForegroundColor White
}

# Clean up
Remove-Item -Path "websocket_test.sh" -Force -ErrorAction SilentlyContinue

# Provide recommendations
Write-Host "`n===== RECOMMENDATIONS =====" -ForegroundColor Cyan
Write-Host "Based on the test results:" -ForegroundColor White

if ($testResult -match "Successfully connected" -and ($testResult -match "101 Switching Protocols" -or $testResult -match "Upgrade: websocket")) {
    Write-Host "The basic WebSocket connection appears to be working correctly." -ForegroundColor Green
    Write-Host "If your QuestNav app is still having trouble connecting, the issue might be with:" -ForegroundColor White
    Write-Host "1. The NetworkTables protocol implementation" -ForegroundColor White
    Write-Host "2. Authentication or handshake details specific to NetworkTables" -ForegroundColor White
    Write-Host "3. The WebSocket library used in the app" -ForegroundColor White
    
    Write-Host "`nTry these steps:" -ForegroundColor White
    Write-Host "1. Check the QuestNav logs for specific error messages" -ForegroundColor White
    Write-Host "2. Verify that your NetworkTables server supports the NT4 protocol" -ForegroundColor White
    Write-Host "3. Ensure the server is configured to accept WebSocket connections" -ForegroundColor White
} else {
    Write-Host "There appears to be an issue with the WebSocket connection." -ForegroundColor Yellow
    Write-Host "Try these steps:" -ForegroundColor White
    Write-Host "1. Make sure your NetworkTables server is running and configured correctly" -ForegroundColor White
    Write-Host "2. Check if any firewall is blocking the connection" -ForegroundColor White
    Write-Host "3. Verify that the server supports WebSocket connections" -ForegroundColor White
    Write-Host "4. Try restarting both the server and the Quest app" -ForegroundColor White
}

Write-Host "`nTo monitor logs in real-time, run the monitor_logs_simple.ps1 script." -ForegroundColor White
