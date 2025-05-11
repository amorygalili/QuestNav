# QuestNav WebSocket Diagnostic Tool
# This script runs a comprehensive diagnostic on the WebSocket connection

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

# Set up port forwarding
Write-Header "PORT FORWARDING SETUP"
$forwardings = adb forward --list
$ntForwarding = $forwardings | Select-String "tcp:$NT_PORT"

if (-not $ntForwarding) {
    Write-Info "Setting up port forwarding for NetworkTables port $NT_PORT..."
    $result = adb forward tcp:$NT_PORT tcp:$NT_PORT
    if ($?) {
        Write-Success "Port forwarding set up successfully!"
    } else {
        Write-Error "Failed to set up port forwarding."
        exit 1
    }
} else {
    Write-Success "Port forwarding is already set up: $ntForwarding"
}

# Create a Python WebSocket diagnostic script
Write-Header "CREATING DIAGNOSTIC SCRIPT"
Write-Info "Creating a Python WebSocket diagnostic script..."

$pythonScript = @"
import socket
import sys
import time
import json
import base64
import hashlib
import struct
import random
import threading

def log(message):
    print(f"[DIAGNOSTIC] {message}")
    sys.stdout.flush()

def test_tcp_connection(host, port):
    log(f"Testing TCP connection to {host}:{port}...")
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(5)
        result = sock.connect_ex((host, port))
        if result == 0:
            log(f"TCP connection successful to {host}:{port}")
            sock.close()
            return True
        else:
            log(f"TCP connection failed to {host}:{port} with error code {result}")
            return False
    except Exception as e:
        log(f"TCP connection error: {str(e)}")
        return False

def create_websocket_handshake(host, port, path):
    key = base64.b64encode(bytes([random.randint(0, 255) for _ in range(16)])).decode('utf-8')
    
    handshake = [
        f"GET {path} HTTP/1.1",
        f"Host: {host}:{port}",
        "Upgrade: websocket",
        "Connection: Upgrade",
        f"Sec-WebSocket-Key: {key}",
        "Sec-WebSocket-Version: 13",
        "",
        ""
    ]
    
    return "\r\n".join(handshake), key

def test_websocket_handshake(host, port, path="/nt/QuestNav"):
    log(f"Testing WebSocket handshake to ws://{host}:{port}{path}...")
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(5)
        sock.connect((host, port))
        
        handshake, key = create_websocket_handshake(host, port, path)
        sock.send(handshake.encode())
        
        response = sock.recv(4096).decode('utf-8')
        log(f"Received response: {response}")
        
        if "101 Switching Protocols" in response and "Upgrade: websocket" in response:
            log("WebSocket handshake successful!")
            
            # Verify the Sec-WebSocket-Accept header
            accept_key = None
            for line in response.split("\r\n"):
                if line.startswith("Sec-WebSocket-Accept:"):
                    accept_key = line.split(": ")[1].strip()
                    break
            
            if accept_key:
                expected_key = base64.b64encode(
                    hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()
                ).decode('utf-8')
                
                if accept_key == expected_key:
                    log("Sec-WebSocket-Accept key is valid")
                else:
                    log(f"Sec-WebSocket-Accept key is invalid. Expected: {expected_key}, Got: {accept_key}")
            
            return True
        else:
            log("WebSocket handshake failed")
            return False
    except Exception as e:
        log(f"WebSocket handshake error: {str(e)}")
        return False
    finally:
        try:
            sock.close()
        except:
            pass

def send_websocket_frame(sock, data):
    # Create a binary WebSocket frame
    header = bytearray()
    header.append(0x82)  # Binary frame (0x82)
    
    if len(data) <= 125:
        header.append(len(data))
    elif len(data) <= 65535:
        header.append(126)
        header.extend(struct.pack(">H", len(data)))
    else:
        header.append(127)
        header.extend(struct.pack(">Q", len(data)))
    
    sock.send(header + data)

def test_networktables_connection(host, port, path="/nt/QuestNav"):
    log(f"Testing NetworkTables connection to ws://{host}:{port}{path}...")
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(5)
        sock.connect((host, port))
        
        handshake, _ = create_websocket_handshake(host, port, path)
        sock.send(handshake.encode())
        
        response = sock.recv(4096).decode('utf-8')
        if "101 Switching Protocols" not in response:
            log("WebSocket handshake failed")
            return False
        
        log("WebSocket connection established, sending NT4 timestamp message...")
        
        # Send a timestamp message (similar to what NT4 client does)
        # Format: [-1, 0, 2, timestamp]
        timestamp = int(time.time() * 1000000)
        data = struct.pack(">iiiq", -1, 0, 2, timestamp)
        send_websocket_frame(sock, data)
        
        # Try to receive a response
        log("Waiting for server response...")
        sock.settimeout(3)
        try:
            response = sock.recv(4096)
            log(f"Received {len(response)} bytes from server")
            return True
        except socket.timeout:
            log("No response received from server (timeout)")
            return False
    except Exception as e:
        log(f"NetworkTables connection error: {str(e)}")
        return False
    finally:
        try:
            sock.close()
        except:
            pass

def run_diagnostics():
    host = "127.0.0.1"
    port = $NT_PORT
    
    log("Starting WebSocket diagnostics")
    log(f"Target: ws://{host}:{port}/nt/QuestNav")
    
    # Test TCP connection
    if not test_tcp_connection(host, port):
        log("TCP connection failed, cannot proceed with WebSocket tests")
        return
    
    # Test WebSocket handshake
    if not test_websocket_handshake(host, port):
        log("WebSocket handshake failed, cannot proceed with NetworkTables tests")
        return
    
    # Test NetworkTables connection
    test_networktables_connection(host, port)
    
    log("Diagnostics complete")

if __name__ == "__main__":
    run_diagnostics()
"@

# Save the Python script
$pythonScript | Out-File -FilePath "websocket_diagnostic.py" -Encoding ASCII

# Push the script to the Quest
Write-Info "Pushing diagnostic script to the Quest..."
adb push websocket_diagnostic.py /data/local/tmp/

# Run the diagnostic script
Write-Header "RUNNING WEBSOCKET DIAGNOSTICS"
Write-Info "Running WebSocket diagnostic script on the Quest..."
adb shell "cd /data/local/tmp && python3 websocket_diagnostic.py"

# Clean up
Remove-Item -Path "websocket_diagnostic.py" -Force -ErrorAction SilentlyContinue

Write-Header "NEXT STEPS"
Write-Info "Based on the diagnostic results, you can:"
Write-Info "1. Check if the WebSocket handshake is successful"
Write-Info "2. Verify if the NetworkTables server is responding to the timestamp message"
Write-Info "3. Run the monitor_networktables_logs.ps1 script to see detailed logs from the Quest app"
Write-Info "4. Make sure your NetworkTables server supports the NT4 protocol and WebSocket connections"
