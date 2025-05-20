# Build and copy the web interface to the Unity project

# Get the current directory (project root)
$projectRoot = Get-Location
Write-Host "Project root: $projectRoot"

# Set the paths with absolute paths
$webInterfacePath = Join-Path -Path $projectRoot -ChildPath "web-interface"
$unityPath = Join-Path -Path $projectRoot -ChildPath "unity"
$unityStreamingAssetsPath = Join-Path -Path $unityPath -ChildPath "Assets\StreamingAssets\webinterface"

Write-Host "Web interface path: $webInterfacePath"
Write-Host "Unity path: $unityPath"
Write-Host "Unity StreamingAssets path: $unityStreamingAssetsPath"

# Create the StreamingAssets directory if it doesn't exist
$streamingAssetsDir = Join-Path -Path $unityPath -ChildPath "Assets\StreamingAssets"
if (-not (Test-Path $streamingAssetsDir)) {
    New-Item -ItemType Directory -Path $streamingAssetsDir | Out-Null
    Write-Host "Created StreamingAssets directory at $streamingAssetsDir"
}

# Create the webinterface directory if it doesn't exist
if (-not (Test-Path $unityStreamingAssetsPath)) {
    New-Item -ItemType Directory -Path $unityStreamingAssetsPath | Out-Null
    Write-Host "Created webinterface directory at $unityStreamingAssetsPath"
}

# Navigate to the web interface directory
Push-Location $webInterfacePath

try {
    # Install dependencies
    Write-Host "Installing dependencies..."
    npm install

    # Build the web interface
    Write-Host "Building web interface..."
    npm run build

    # Check if the build was successful
    $distPath = Join-Path -Path $webInterfacePath -ChildPath "dist"
    if (-not (Test-Path $distPath)) {
        Write-Host "Build failed. No dist directory found at $distPath" -ForegroundColor Red
        exit 1
    }

    # Clean the destination directory
    Write-Host "Cleaning destination directory..."
    Remove-Item -Path "$unityStreamingAssetsPath\*" -Recurse -Force -ErrorAction SilentlyContinue

    # Copy the built files to the Unity project
    Write-Host "Copying files to Unity project..."
    $distPath = Join-Path -Path $webInterfacePath -ChildPath "dist"
    Copy-Item -Path "$distPath\*" -Destination $unityStreamingAssetsPath -Recurse

    Write-Host "Web interface built and copied successfully!" -ForegroundColor Green
}
finally {
    # Return to the original directory
    Pop-Location
}
