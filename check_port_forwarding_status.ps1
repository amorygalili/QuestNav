# QuestNav Port Forwarding Status Check Script
# This script checks the status of both forward and reverse port forwarding

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

# Check for existing forward port forwarding
Write-Header "FORWARD PORT FORWARDING STATUS"
Write-Info "Checking for existing forward port forwarding (computer -> Quest)..."
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if ($ntForwarding) {
    Write-Success "Forward port forwarding is active: $ntForwarding"
    Write-Info "This means: Connections to port $NT_PORT on your computer are forwarded to port $NT_PORT on the Quest."
    Write-Warning "This type of forwarding can interfere with local NT4 clients connecting to your NT4 server."
} else {
    Write-Info "No forward port forwarding found for port $NT_PORT."
}

# Check for existing reverse port forwarding
Write-Header "REVERSE PORT FORWARDING STATUS"
Write-Info "Checking for existing reverse port forwarding (Quest -> computer)..."
$reverseForwardings = adb reverse --list
$ntReverseForwarding = $reverseForwardings | Select-String "tcp:$NT_PORT"

if ($ntReverseForwarding) {
    Write-Success "Reverse port forwarding is active: $ntReverseForwarding"
    Write-Info "This means: Connections to port $NT_PORT on the Quest are forwarded to port $NT_PORT on your computer."
    Write-Info "This is the recommended setup for connecting the Quest app to your local NT4 server."
} else {
    Write-Info "No reverse port forwarding found for port $NT_PORT."
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

# Provide recommendations
Write-Header "RECOMMENDATIONS"

if ($ntForwarding -and -not $ntReverseForwarding) {
    Write-Warning "You are currently using forward port forwarding, which can cause issues."
    Write-Info "Recommended action: Remove forward port forwarding and set up reverse port forwarding instead."
    Write-Info "1. Run: .\remove_reverse_port_forwarding.ps1 (this will remove both types of forwarding)"
    Write-Info "2. Run: .\setup_reverse_port_forwarding.ps1"
} elseif ($ntReverseForwarding -and -not $ntForwarding) {
    Write-Success "You are using reverse port forwarding, which is the recommended setup."
    Write-Info "No changes needed. Your Quest app should be able to connect to your NT4 server."
} elseif ($ntForwarding -and $ntReverseForwarding) {
    Write-Warning "You have both forward and reverse port forwarding active, which may cause conflicts."
    Write-Info "Recommended action: Remove both and set up only reverse port forwarding."
    Write-Info "1. Run: .\remove_reverse_port_forwarding.ps1"
    Write-Info "2. Run: .\setup_reverse_port_forwarding.ps1"
} else {
    Write-Info "No port forwarding is currently set up."
    Write-Info "To connect your Quest app to your NT4 server, run: .\setup_reverse_port_forwarding.ps1"
}

if (-not $success) {
    Write-Warning "No NT4 server was detected. Make sure your NT4 server is running before connecting from the Quest."
}
