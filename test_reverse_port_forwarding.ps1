# QuestNav Reverse Port Forwarding Test Script
# This script tests if the reverse port forwarding is working correctly from the Quest side

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

# Check if NT4 server is running on the computer
Write-Header "NT4 SERVER CHECK"
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $result = $tcpClient.BeginConnect("127.0.0.1", $NT_PORT, $null, $null)
    $success = $result.AsyncWaitHandle.WaitOne(1000, $true)
    
    if ($success) {
        $tcpClient.EndConnect($result)
        Write-Success "NT4 server is running on your computer at port $NT_PORT."
        $tcpClient.Close()
    } else {
        Write-Warning "No NT4 server detected on your computer at port $NT_PORT."
        Write-Info "Please start your NT4 server before testing the reverse port forwarding."
        exit 1
    }
} catch {
    Write-Error "Error checking NT4 server: $_"
    exit 1
}

# Set up reverse port forwarding
Write-Header "REVERSE PORT FORWARDING SETUP"
Write-Info "Setting up reverse port forwarding for NetworkTables port $NT_PORT..."
$result = adb reverse tcp:$NT_PORT tcp:$NT_PORT
if ($?) {
    Write-Success "Reverse port forwarding set up successfully!"
} else {
    Write-Error "Failed to set up reverse port forwarding."
    exit 1
}

# Create a test script to run on the Quest
Write-Header "CREATING TEST SCRIPT"
Write-Info "Creating a test script to run on the Quest..."

$testScript = @"
#!/system/bin/sh
# Test script for reverse port forwarding

echo "Testing connection to 127.0.0.1:$NT_PORT..."

# Test TCP connection
echo "Testing TCP connection..."
nc -z -w 5 127.0.0.1 $NT_PORT
if [ \$? -ne 0 ]; then
  echo "TCP connection failed"
  exit 1
else
  echo "TCP connection successful"
fi

# Test WebSocket handshake
echo "Testing WebSocket handshake..."
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
$testScript | Out-File -FilePath "reverse_port_test.sh" -Encoding ASCII
adb push reverse_port_test.sh /data/local/tmp/
adb shell "chmod 755 /data/local/tmp/reverse_port_test.sh"

# Run the test script
Write-Header "RUNNING TEST ON QUEST"
Write-Info "Running test script on the Quest..."
$testResult = adb shell "/data/local/tmp/reverse_port_test.sh"
Write-Info "Test result:"
Write-Info $testResult

# Check for success indicators in the test result
if ($testResult -match "TCP connection successful") {
    Write-Success "TCP connection from Quest to your computer via reverse port forwarding is working!"
} else {
    Write-Error "TCP connection from Quest to your computer via reverse port forwarding failed."
    Write-Info "This indicates that the reverse port forwarding is not working correctly."
}

if ($testResult -match "101 Switching Protocols" -or $testResult -match "Upgrade: websocket") {
    Write-Success "WebSocket handshake from Quest to your computer via reverse port forwarding is working!"
} else {
    Write-Warning "WebSocket handshake from Quest to your computer via reverse port forwarding may have failed."
    Write-Info "This could indicate that your NT4 server is not properly configured for WebSocket connections."
}

# Clean up
Remove-Item -Path "reverse_port_test.sh" -Force -ErrorAction SilentlyContinue

# Provide recommendations
Write-Header "RECOMMENDATIONS"

if ($testResult -match "TCP connection successful" -and ($testResult -match "101 Switching Protocols" -or $testResult -match "Upgrade: websocket")) {
    Write-Success "Reverse port forwarding is working correctly!"
    Write-Info "Your Quest app should be able to connect to your NT4 server through reverse port forwarding."
    Write-Info "If you're still having issues, check the app logs for more details."
} else {
    Write-Warning "There appears to be an issue with the reverse port forwarding."
    Write-Info "Try these steps:"
    Write-Info "1. Make sure your NT4 server is running and configured correctly"
    Write-Info "2. Check if any firewall is blocking the connection"
    Write-Info "3. Try restarting both the server and the Quest app"
    Write-Info "4. Make sure the Quest app is configured to connect to 127.0.0.1"
}

Write-Info "`nTo monitor logs in real-time, run:"
Write-Info ".\monitor_logs_simple.ps1"
