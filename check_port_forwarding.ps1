# QuestNav NetworkTables Port Forwarding Check Script
# This script checks if the port forwarding for NetworkTables is set up correctly
# and provides detailed debugging information

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
    Write-Info "You can download them from: https://developer.android.com/tools/releases/platform-tools"
    exit 1
}

# Check if Quest is connected
Write-Header "QUEST DEVICE CHECK"
$devices = adb devices
if ($devices -match "device$") {
    Write-Success "Quest device found and connected."
    Write-Info "Connected devices:"
    Write-Info $devices
} else {
    Write-Error "No Quest device found or device is not properly connected."
    Write-Info "Make sure your Quest is connected via USB and USB debugging is enabled."
    Write-Info "To enable USB debugging on Quest:"
    Write-Info "1. Put on your Quest headset"
    Write-Info "2. Go to Settings > System > Developer"
    Write-Info "3. Enable Developer Mode and USB Connection Dialog"
    Write-Info "4. Connect your Quest to your computer via USB"
    Write-Info "5. In the headset, allow USB debugging when prompted"
    exit 1
}

# Check active port forwardings
Write-Header "PORT FORWARDING CHECK"
Write-Info "Checking active port forwardings..."
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if ($ntForwarding) {
    Write-Success "NetworkTables port forwarding is active: $ntForwarding"
    Write-Info "Port forwarding is set up correctly!"
} else {
    Write-Warning "No port forwarding found for NetworkTables port $NT_PORT."
    Write-Info "Setting up port forwarding now..."
    $result = adb forward tcp:$NT_PORT tcp:$NT_PORT
    if ($?) {
        Write-Success "Port forwarding set up successfully!"
    } else {
        Write-Error "Failed to set up port forwarding. Please check if ADB is working correctly."
        exit 1
    }
}

# Check if the port is in use on the local machine
Write-Header "NETWORKTABLES SERVER CHECK"
try {
    Write-Info "Checking if NetworkTables server is running on port $NT_PORT..."
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $result = $tcpClient.BeginConnect("127.0.0.1", $NT_PORT, $null, $null)
    $success = $result.AsyncWaitHandle.WaitOne(1000, $true)

    if ($success) {
        $tcpClient.EndConnect($result)
        Write-Success "A service is listening on port $NT_PORT on your local machine."
        Write-Info "This is good! It means your NetworkTables server is running."

        # Get more details about the connection
        $localEndPoint = $tcpClient.Client.LocalEndPoint
        $remoteEndPoint = $tcpClient.Client.RemoteEndPoint
        Write-Info "Local endpoint: $localEndPoint"
        Write-Info "Remote endpoint: $remoteEndPoint"
        Write-Info "Connection is established and ready for data transfer."
    } else {
        Write-Warning "No service is listening on port $NT_PORT on your local machine."
        Write-Info "You need to start a NetworkTables server on your computer for the connection to work."
        Write-Info "Make sure your robot code or NetworkTables server is running and listening on port $NT_PORT."
    }

    $tcpClient.Close()
} catch {
    Write-Error "Error checking local port: $_"
}

# Check network connectivity on the Quest
Write-Header "QUEST NETWORK CONNECTIVITY CHECK"
Write-Info "Checking network connectivity on the Quest device..."
try {
    # Check if the Quest can reach the internet
    $pingResult = adb shell ping -c 1 8.8.8.8
    if ($pingResult -match "1 received") {
        Write-Success "Quest has internet connectivity."
    } else {
        Write-Warning "Quest may not have internet connectivity."
        Write-Info "This might not be a problem for local connections, but worth noting."
    }

    # Check if the Quest can reach the local computer
    $localIp = (Get-NetIPAddress | Where-Object {$_.AddressFamily -eq "IPv4" -and $_.PrefixOrigin -ne "WellKnown"} | Select-Object -First 1).IPAddress
    Write-Info "Your computer's IP address: $localIp"
    $pingLocalResult = adb shell ping -c 1 $localIp
    if ($pingLocalResult -match "1 received") {
        Write-Success "Quest can reach your computer at $localIp."
    } else {
        Write-Warning "Quest cannot reach your computer at $localIp."
        Write-Info "This might indicate a network isolation issue or firewall problem."
    }
} catch {
    Write-Error "Error checking Quest network connectivity: $_"
}

# Check if QuestNav app is running
Write-Header "QUESTNAV APP CHECK"
try {
    $runningApps = adb shell "ps -A | grep -i questnav"
    if ($runningApps) {
        Write-Success "QuestNav app is running on the Quest."
        Write-Info "Process information: $runningApps"
    } else {
        Write-Warning "QuestNav app does not appear to be running on the Quest."
        Write-Info "Please launch the QuestNav app on your Quest headset."
    }
} catch {
    Write-Error "Error checking if QuestNav app is running: $_"
}

# Capture and display relevant logs
Write-Header "QUESTNAV LOGS"
Write-Info "Capturing recent logs from the Quest device..."
try {
    Write-Info "Clearing log buffer to get fresh logs..."
    adb logcat -c

    Write-Info "Waiting for 5 seconds to collect new logs..."
    Start-Sleep -Seconds 5

    Write-Info "Retrieving NetworkTables and QuestNav related logs..."
    $logs = adb logcat -d | Select-String -Pattern "NetworkTable|QuestNav|NT4|WebSocket"

    if ($logs) {
        Write-Info "Found relevant logs:"
        foreach ($log in $logs) {
            if ($log -match "Error|Exception|fail|Fail") {
                Write-Error $log
            } elseif ($log -match "Warning|warn|Warn") {
                Write-Warning $log
            } elseif ($log -match "Connected|Success|success") {
                Write-Success $log
            } else {
                Write-Info $log
            }
        }
    } else {
        Write-Warning "No relevant logs found. This might indicate the app is not attempting to connect."
        Write-Info "Try restarting the QuestNav app and check logs again."
    }
} catch {
    Write-Error "Error capturing logs: $_"
}

# Provide a summary and recommendations
Write-Header "SUMMARY AND RECOMMENDATIONS"
Write-Info "Based on the checks performed:"

if ($ntForwarding -and $success -and $runningApps) {
    Write-Success "All basic requirements for NetworkTables connection are met:"
    Write-Info "✓ ADB is working"
    Write-Info "✓ Quest device is connected"
    Write-Info "✓ Port forwarding is set up"
    Write-Info "✓ NetworkTables server is running"
    Write-Info "✓ QuestNav app is running"

    Write-Info "`nIf you're still experiencing connection issues, consider the following:"
    Write-Info "1. Check if your NetworkTables server is configured to accept WebSocket connections"
    Write-Info "2. Verify that your NetworkTables server is using port $NT_PORT"
    Write-Info "3. Check for any firewall rules that might be blocking the connection"
    Write-Info "4. Restart both the NetworkTables server and the QuestNav app"
    Write-Info "5. Try using a different USB port or cable"
} else {
    Write-Warning "Some requirements for NetworkTables connection are not met:"

    if (-not $ntForwarding) {
        Write-Info "✗ Port forwarding is not set up correctly"
    } else {
        Write-Info "✓ Port forwarding is set up"
    }

    if (-not $success) {
        Write-Info "✗ NetworkTables server is not running or not accessible"
    } else {
        Write-Info "✓ NetworkTables server is running"
    }

    if (-not $runningApps) {
        Write-Info "✗ QuestNav app is not running on the Quest"
    } else {
        Write-Info "✓ QuestNav app is running"
    }

    Write-Info "`nPlease address the issues marked with ✗ before attempting to connect."
}

Write-Info "`nTo get continuous logs from the Quest, run this command in a separate terminal:"
Write-Info "adb logcat | Select-String -Pattern 'NetworkTable|QuestNav|NT4|WebSocket'"
