# QuestNav NetworkTables Log Monitor
# This script continuously monitors logs related to NetworkTables connections on the Quest

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
try {
    $adbVersion = adb version
    Write-Success "ADB found: $adbVersion"
} catch {
    Write-Error "Error: ADB not found in PATH. Please install Android SDK Platform Tools and add them to your PATH."
    exit 1
}

# Check if Quest is connected
$devices = adb devices
if (-not ($devices -match "device$")) {
    Write-Error "No Quest device found or device is not properly connected."
    Write-Info "Make sure your Quest is connected via USB and USB debugging is enabled."
    exit 1
}

Write-Header "NETWORKTABLES LOG MONITOR"
Write-Info "Starting continuous log monitoring for NetworkTables connections..."
Write-Info "Press Ctrl+C to stop monitoring."
Write-Info ""

# Clear existing logs to start fresh
adb logcat -c

# Define the log patterns to look for
$patterns = @(
    "NetworkTable",
    "QuestNav",
    "NT4",
    "WebSocket",
    "ws://",
    "127.0.0.1"
)

# Join patterns with OR operator for grep
$grepPattern = $patterns -join "|"

try {
    # Start the logcat process and filter for relevant logs
    $process = Start-Process -FilePath "adb" -ArgumentList "logcat" -NoNewWindow -PassThru
    
    # Use a separate process to filter the logs
    $filterProcess = Start-Process -FilePath "adb" -ArgumentList "logcat", "-v", "time" -NoNewWindow -RedirectStandardOutput "temp_logs.txt" -PassThru
    
    # Monitor the log file and display filtered logs
    while ($true) {
        Start-Sleep -Milliseconds 500
        
        if (Test-Path "temp_logs.txt") {
            $newLogs = Get-Content "temp_logs.txt" | Select-String -Pattern $grepPattern
            
            foreach ($log in $newLogs) {
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
            
            # Clear the log file for the next iteration
            Clear-Content "temp_logs.txt"
        }
    }
} finally {
    # Clean up processes and temporary file when script is terminated
    if ($process -ne $null) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    if ($filterProcess -ne $null) {
        Stop-Process -Id $filterProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path "temp_logs.txt") {
        Remove-Item "temp_logs.txt" -Force -ErrorAction SilentlyContinue
    }
}
