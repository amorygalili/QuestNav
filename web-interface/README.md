# QuestNav Web Interface

This is a web-based interface for the QuestNav application. It allows you to control and monitor the QuestNav app from a web browser when connected to the same network as the Quest headset.

## Features

- Set team number for robot connection
- Connect to simulation
- View connection status
- View device information and debug data
- Stream Quest 3 passthrough cameras via MJPEG
- Start/stop camera streaming controls

## Development

This project is built with:

- React
- TypeScript
- Material-UI
- Vite

### Prerequisites

- Node.js 16+
- npm or yarn

### Setup

1. Install dependencies:

```bash
npm install
```

2. Start the development server:

```bash
npm run dev
```

3. Build for production:

```bash
npm run build
```

## Integration with QuestNav

The built web interface is served by a simple HTTP server running on the Quest headset. The server is implemented in Java and integrated with the Unity application.

### Building and Deploying

To build the web interface and copy it to the Unity project, run the `build-web-interface.ps1` script from the root of the project:

```bash
./build-web-interface.ps1
```

This will:
1. Install dependencies
2. Build the web interface
3. Copy the built files to the Unity project's StreamingAssets folder

## Accessing the Web Interface

When the QuestNav app is running on the Quest headset and connected to a robot network, you can access the web interface by navigating to the Quest's IP address on port 8080 (e.g., `http://192.168.1.100:8080`) in a web browser on any device connected to the same network.

## API Endpoints

The web interface communicates with the QuestNav app through the following API endpoints:

- `GET /api/status` - Get the current status of the QuestNav app
- `POST /api/team` - Update the team number
- `POST /api/sim` - Connect to simulation
- `GET /api/camera/stream` - MJPEG stream of Quest 3 passthrough cameras
- `GET /api/camera/frame` - Single JPEG frame from Quest 3 cameras
- `POST /api/camera/start` - Start camera streaming
- `POST /api/camera/stop` - Stop camera streaming
