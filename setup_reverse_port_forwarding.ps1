# QuestNav Reverse Port Forwarding Script
# This script sets up ADB reverse port forwarding for NetworkTables communication
# from the Quest to your local computer's NT4 server

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

# Remove any existing port forwarding
Write-Header "REMOVING EXISTING PORT FORWARDING"
Write-Info "Checking for existing port forwarding..."
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if ($ntForwarding) {
    Write-Warning "Found existing port forwarding: $ntForwarding"
    Write-Info "Removing existing port forwarding..."
    adb forward --remove tcp:$NT_PORT
    if ($?) {
        Write-Success "Existing port forwarding removed successfully."
    } else {
        Write-Error "Failed to remove existing port forwarding."
    }
}

# Get local IP address
Write-Header "LOCAL IP ADDRESS"
$localIp = (Get-NetIPAddress | Where-Object {$_.AddressFamily -eq "IPv4" -and $_.PrefixOrigin -ne "WellKnown"} | Select-Object -First 1).IPAddress
Write-Info "Your computer's IP address: $localIp"

# Set up reverse port forwarding
Write-Header "SETTING UP REVERSE PORT FORWARDING"
Write-Info "Setting up reverse port forwarding for NetworkTables port $NT_PORT..."
$result = adb reverse tcp:$NT_PORT tcp:$NT_PORT
if ($?) {
    Write-Success "Reverse port forwarding set up successfully!"
    Write-Info "Port $NT_PORT on the Quest will now be forwarded to port $NT_PORT on your computer."
    
    # Verify the reverse port forwarding
    $reverseForwardings = adb reverse --list
    $ntReverseForwarding = $reverseForwardings | Select-String "tcp:$NT_PORT"
    if ($ntReverseForwarding) {
        Write-Success "Verified reverse port forwarding: $ntReverseForwarding"
    } else {
        Write-Warning "Could not verify reverse port forwarding. It may not have been set up correctly."
    }
} else {
    Write-Error "Failed to set up reverse port forwarding."
    exit 1
}

# Check if the NT4 server is running
Write-Header "NT4 SERVER CHECK"
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $result = $tcpClient.BeginConnect("127.0.0.1", $NT_PORT, $null, $null)
    $success = $result.AsyncWaitHandle.WaitOne(1000, $true)
    
    if ($success) {
        $tcpClient.EndConnect($result)
        Write-Success "NT4 server is running and listening on port $NT_PORT."
        $tcpClient.Close()
    } else {
        Write-Warning "No NT4 server detected on port $NT_PORT."
        Write-Info "Please start your NT4 server before attempting to connect from the Quest."
    }
} catch {
    Write-Error "Error checking NT4 server: $_"
}

# Provide instructions
Write-Header "NEXT STEPS"
Write-Info "With reverse port forwarding set up:"
Write-Info "1. When your Quest app connects to 127.0.0.1:$NT_PORT, it will be forwarded to your computer's port $NT_PORT"
Write-Info "2. Make sure your NT4 server is running on your computer and listening on port $NT_PORT"
Write-Info "3. Launch the QuestNav app on your Quest"
Write-Info "4. The app should now be able to connect to your NT4 server"
Write-Info ""
Write-Info "To remove the reverse port forwarding when done:"
Write-Info "adb reverse --remove tcp:$NT_PORT"
Write-Info ""
Write-Info "To monitor logs, run:"
Write-Info ".\monitor_logs_simple.ps1"
