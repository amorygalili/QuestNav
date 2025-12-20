---
sidebar_position: 8
---

# Web Interface

QuestNav includes a web-based interface that allows you to control and monitor the application from any web browser on the same network as the Quest headset.

## Accessing the Web Interface

When the QuestNav app is running on the Quest headset and connected to a robot network, you can access the web interface by navigating to the Quest's IP address on port 8080 (e.g., `http://192.168.1.100:8080`) in a web browser on any device connected to the same network.

## Features

The web interface provides the following features:

### Connection Settings

- **Team Number**: Enter your FRC team number and click "Update" to connect to your robot
- **Connect to Sim**: Click this button to connect to a simulation running on your local computer

### Connection Status

The interface displays real-time information about the connection status:

- Whether the app is connected to NetworkTables
- The current connection state
- The IP address of the connected robot
- The current team number

### Device Information

The interface also provides detailed information about the Quest headset:

- Battery level and charging status
- Tracking status (whether the headset is currently tracking)
- Device model and name
- Operating system information
- Hardware specifications (CPU, GPU, memory)

## Troubleshooting

If you cannot access the web interface:

1. Make sure your computer is connected to the same network as the Quest headset
2. Verify that the QuestNav app is running on the Quest
3. Make sure you're using the correct IP address of the Quest headset
4. Check if there are any firewall settings blocking access to port 8080

## Technical Details

The web interface is built with React and Material-UI, and is served by a simple HTTP server running on the Quest headset. The server is implemented in Java and integrated with the Unity application.

The interface communicates with the QuestNav app through REST API endpoints:

- `GET /api/status` - Get the current status of the QuestNav app
- `POST /api/team` - Update the team number
- `POST /api/sim` - Connect to simulation
