# QuestNav NetworkTables Log Monitor (Simple Version)
# This script continuously monitors logs related to NetworkTables connections on the Quest

# Check if ADB is available
try {
    $adbVersion = adb version
    Write-Host "ADB found: $adbVersion" -ForegroundColor Green
} catch {
    Write-Host "Error: ADB not found in PATH. Please install Android SDK Platform Tools and add them to your PATH." -ForegroundColor Red
    exit 1
}

# Check if Quest is connected
$devices = adb devices
if (-not ($devices -match "device$")) {
    Write-Host "No Quest device found or device is not properly connected." -ForegroundColor Red
    Write-Host "Make sure your Quest is connected via USB and USB debugging is enabled." -ForegroundColor White
    exit 1
}

Write-Host "`n===== NETWORKTABLES LOG MONITOR =====" -ForegroundColor Cyan
Write-Host "Starting continuous log monitoring for NetworkTables connections..." -ForegroundColor White
Write-Host "Press Ctrl+C to stop monitoring." -ForegroundColor White
Write-Host ""

# Clear existing logs to start fresh
adb logcat -c

# Start monitoring logs
Write-Host "Monitoring logs... (filtered for NetworkTables, QuestNav, NT4, WebSocket)" -ForegroundColor Cyan

# Run the logcat command and filter the output
try {
    adb logcat | ForEach-Object {
        $line = $_
        if ($line -match "NetworkTable|QuestNav|NT4|WebSocket|ws://|127.0.0.1") {
            if ($line -match "Error|Exception|fail|Fail") {
                Write-Host $line -ForegroundColor Red
            } elseif ($line -match "Warning|warn|Warn") {
                Write-Host $line -ForegroundColor Yellow
            } elseif ($line -match "Connected|Success|success") {
                Write-Host $line -ForegroundColor Green
            } else {
                Write-Host $line -ForegroundColor White
            }
        }
    }
} catch {
    Write-Host "Error monitoring logs: $_" -ForegroundColor Red
} finally {
    Write-Host "Log monitoring stopped." -ForegroundColor Cyan
}
