import { useState, useEffect } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import Container from '@mui/material/Container';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Grid from '@mui/material/Grid';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import axios from 'axios';
import './App.css';

// Define the theme
const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#38BDF8',
    },
    secondary: {
      main: '#FB923C',
    },
    background: {
      default: '#0F172A',
      paper: '#1E293B',
    },
  },
  typography: {
    fontFamily: 'Roboto, Arial, sans-serif',
    h1: {
      fontSize: '2.5rem',
      fontWeight: 600,
    },
    h2: {
      fontSize: '2rem',
      fontWeight: 500,
    },
    h3: {
      fontSize: '1.5rem',
      fontWeight: 500,
    },
  },
});

// Define the status interface
interface Status {
  isConnected: boolean;
  connectionState: string;
  ipAddress: string;
  teamNumber: string;
  batteryPercent: number;
  isCharging: boolean;
  deviceModel: string;
  deviceName: string;
  operatingSystem: string;
  systemMemorySize: number;
  processorCount: number;
  processorFrequency: number;
  processorType: string;
  graphicsDeviceName: string;
  graphicsMemorySize: number;
  graphicsDeviceVersion: string;
  currentlyTracking: boolean;
}

function App() {
  const [status, setStatus] = useState<Status | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [teamNumber, setTeamNumber] = useState<string>('');
  const [updateSuccess, setUpdateSuccess] = useState<boolean>(false);

  // Fetch status on component mount and every 2 seconds
  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const response = await axios.get('/api/status');
        setStatus(response.data);
        setTeamNumber(response.data.teamNumber);
        setLoading(false);
        setError(null);
      } catch (err) {
        setError('Failed to fetch status. The server might be unavailable.');
        setLoading(false);
      }
    };

    // Initial fetch
    fetchStatus();

    // Set up interval for periodic updates
    const intervalId = setInterval(fetchStatus, 2000);

    // Clean up interval on component unmount
    return () => clearInterval(intervalId);
  }, []);

  // Handle team number update
  const handleTeamNumberUpdate = async () => {
    try {
      await axios.post('/api/team', { teamNumber });
      setUpdateSuccess(true);
      setTimeout(() => setUpdateSuccess(false), 3000);
    } catch (err) {
      setError('Failed to update team number');
      setTimeout(() => setError(null), 3000);
    }
  };

  // Handle connect to simulation
  const handleConnectToSim = async () => {
    try {
      await axios.post('/api/sim');
      setUpdateSuccess(true);
      setTimeout(() => setUpdateSuccess(false), 3000);
    } catch (err) {
      setError('Failed to connect to simulation');
      setTimeout(() => setError(null), 3000);
    }
  };

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Container maxWidth="md">
        <Box sx={{ my: 4 }}>
          <Typography variant="h1" component="h1" gutterBottom align="center">
            QuestNav
          </Typography>

          {loading ? (
            <Box sx={{ display: 'flex', justifyContent: 'center', my: 4 }}>
              <CircularProgress />
            </Box>
          ) : error ? (
            <Alert severity="error" sx={{ my: 2 }}>{error}</Alert>
          ) : (
            <>
              {updateSuccess && (
                <Alert severity="success" sx={{ my: 2 }}>Update successful!</Alert>
              )}

              <Paper sx={{ p: 3, mb: 3 }}>
                <Typography variant="h2" component="h2" gutterBottom>
                  Connection Settings
                </Typography>
                <Grid container spacing={2} alignItems="center">
                  <Grid item xs={12} md={6}>
                    <TextField
                      label="Team Number"
                      variant="outlined"
                      fullWidth
                      value={teamNumber}
                      onChange={(e) => setTeamNumber(e.target.value)}
                    />
                  </Grid>
                  <Grid item xs={6} md={3}>
                    <Button
                      variant="contained"
                      color="primary"
                      fullWidth
                      onClick={handleTeamNumberUpdate}
                    >
                      Update
                    </Button>
                  </Grid>
                  <Grid item xs={6} md={3}>
                    <Button
                      variant="contained"
                      color="secondary"
                      fullWidth
                      onClick={handleConnectToSim}
                    >
                      Connect to Sim
                    </Button>
                  </Grid>
                </Grid>
              </Paper>

              <Paper sx={{ p: 3, mb: 3 }}>
                <Typography variant="h2" component="h2" gutterBottom>
                  Connection Status
                </Typography>
                <Grid container spacing={2}>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>Status:</strong> {status?.isConnected ? 'Connected' : 'Disconnected'}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>State:</strong> {status?.connectionState}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>IP Address:</strong> {status?.ipAddress || 'N/A'}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>Team Number:</strong> {status?.teamNumber}
                    </Typography>
                  </Grid>
                </Grid>
              </Paper>

              <Paper sx={{ p: 3 }}>
                <Typography variant="h2" component="h2" gutterBottom>
                  Device Information
                </Typography>
                <Grid container spacing={2}>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>Battery:</strong> {status?.batteryPercent.toFixed(1)}%
                      {status?.isCharging ? ' (Charging)' : ''}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>Tracking:</strong> {status?.currentlyTracking ? 'Active' : 'Lost'}
                    </Typography>
                  </Grid>
                  <Grid item xs={12}>
                    <Divider sx={{ my: 2 }} />
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>Device:</strong> {status?.deviceModel}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>Name:</strong> {status?.deviceName}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>OS:</strong> {status?.operatingSystem}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>Memory:</strong> {status?.systemMemorySize} MB
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>CPU:</strong> {status?.processorType}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>CPU Cores:</strong> {status?.processorCount}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>GPU:</strong> {status?.graphicsDeviceName}
                    </Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="body1">
                      <strong>GPU Memory:</strong> {status?.graphicsMemorySize} MB
                    </Typography>
                  </Grid>
                </Grid>
              </Paper>
            </>
          )}
        </Box>
      </Container>
    </ThemeProvider>
  );
}

export default App;
