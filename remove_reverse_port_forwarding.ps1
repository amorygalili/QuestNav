# QuestNav Remove Reverse Port Forwarding Script
# This script removes ADB reverse port forwarding for NetworkTables

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

# Check for existing reverse port forwarding
Write-Header "CHECKING REVERSE PORT FORWARDING"
Write-Info "Checking for existing reverse port forwarding..."
$reverseForwardings = adb reverse --list
$ntReverseForwarding = $reverseForwardings | Select-String "tcp:$NT_PORT"

if ($ntReverseForwarding) {
    Write-Success "Found reverse port forwarding: $ntReverseForwarding"
    Write-Info "Removing reverse port forwarding..."
    adb reverse --remove tcp:$NT_PORT
    if ($?) {
        Write-Success "Reverse port forwarding removed successfully."
    } else {
        Write-Error "Failed to remove reverse port forwarding."
    }
} else {
    Write-Warning "No reverse port forwarding found for port $NT_PORT."
}

# Check for existing forward port forwarding
Write-Header "CHECKING FORWARD PORT FORWARDING"
Write-Info "Checking for existing forward port forwarding..."
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if ($ntForwarding) {
    Write-Warning "Found forward port forwarding: $ntForwarding"
    Write-Info "Removing forward port forwarding..."
    adb forward --remove tcp:$NT_PORT
    if ($?) {
        Write-Success "Forward port forwarding removed successfully."
    } else {
        Write-Error "Failed to remove forward port forwarding."
    }
} else {
    Write-Info "No forward port forwarding found for port $NT_PORT."
}

Write-Header "SUMMARY"
Write-Info "All port forwarding for port $NT_PORT has been removed."
Write-Info "Your local NT4 server should now be accessible to other applications on your computer."
Write-Info "The Quest app will no longer be able to connect to your NT4 server until port forwarding is set up again."
