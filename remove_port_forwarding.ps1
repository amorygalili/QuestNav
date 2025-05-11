# QuestNav NetworkTables Port Forwarding Removal Script
# This script removes the ADB port forwarding for NetworkTables

# NetworkTables port used by QuestNav
$NT_PORT = 5810

# Check if ADB is available
try {
    $adbVersion = adb version
    Write-Host "ADB found: $adbVersion"
} catch {
    Write-Host "Error: ADB not found in PATH. Please install Android SDK Platform Tools and add them to your PATH."
    exit 1
}

# Check if port forwarding exists
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if ($ntForwarding) {
    Write-Host "Found NetworkTables port forwarding: $ntForwarding"
    Write-Host "Removing port forwarding..."
    
    $result = adb forward --remove tcp:$NT_PORT
    
    if ($?) {
        Write-Host "Port forwarding removed successfully!"
    } else {
        Write-Host "Error removing port forwarding."
    }
} else {
    Write-Host "No port forwarding found for NetworkTables port $NT_PORT."
}
