# QuestNav NetworkTables Port Forwarding Script
# This script sets up ADB port forwarding for NetworkTables communication
# between the Quest and a local NetworkTables server

# NetworkTables port used by QuestNav
$NT_PORT = 5810

# Check if ADB is available
try {
    $adbVersion = adb version
    Write-Host "ADB found: $adbVersion"
} catch {
    Write-Host "Error: ADB not found in PATH. Please install Android SDK Platform Tools and add them to your PATH."
    Write-Host "You can download them from: https://developer.android.com/tools/releases/platform-tools"
    exit 1
}

# Check if Quest is connected
$devices = adb devices
if ($devices -match "device$") {
    Write-Host "Quest device found."
} else {
    Write-Host "Error: No Quest device found. Please make sure your Quest is connected via USB and USB debugging is enabled."
    Write-Host "To enable USB debugging on Quest:"
    Write-Host "1. Put on your Quest headset"
    Write-Host "2. Go to Settings > System > Developer"
    Write-Host "3. Enable Developer Mode and USB Connection Dialog"
    Write-Host "4. Connect your Quest to your computer via USB"
    Write-Host "5. In the headset, allow USB debugging when prompted"
    exit 1
}

# Set up port forwarding
Write-Host "Setting up port forwarding for NetworkTables port $NT_PORT..."
$result = adb forward tcp:$NT_PORT tcp:$NT_PORT
if ($?) {
    Write-Host "Port forwarding set up successfully!"
    Write-Host "Port $NT_PORT on Quest is now forwarded to port $NT_PORT on your computer."
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "1. Start a NetworkTables server on your computer (port $NT_PORT)"
    Write-Host "2. Launch the QuestNav app on your Quest"
    Write-Host "3. The app should connect to the NetworkTables server through the forwarded port"
    Write-Host ""
    Write-Host "To check active port forwardings:"
    Write-Host "adb forward --list"
    Write-Host ""
    Write-Host "To remove port forwarding when done:"
    Write-Host "adb forward --remove tcp:$NT_PORT"
} else {
    Write-Host "Error setting up port forwarding. Please check if the port is already in use."
    Write-Host "To check active port forwardings:"
    Write-Host "adb forward --list"
    exit 1
}
