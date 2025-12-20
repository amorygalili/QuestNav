import { useState, useEffect } from 'react';
import {
  Paper,
  Typography,
  Grid,
  Switch,
  Slider,
  FormControlLabel,
  Button,
  Alert,
  Box,
  Divider,
  Chip,
  Card,
  CardContent,
} from '@mui/material';
import axios from 'axios';

interface CameraPerformance {
  avgProcessingTime: number;
  frameCount: number;
  skippedFrames: number;
  currentQuality: number;
  currentScale: number;
}

interface CameraOptimizationSettings {
  enableOptimizations: boolean;
  enableAdaptiveQuality: boolean;
  minJpegQuality: number;
  maxJpegQuality: number;
  enableFrameSkipping: boolean;
  maxProcessingTimeMs: number;
  enableAsyncEncoding: boolean;
  enableTextureReuse: boolean;
  enableROICropping: boolean;
  enableResolutionScaling: boolean;
  lowBandwidthScale: number;
  roiX: number;
  roiY: number;
  roiWidth: number;
  roiHeight: number;
}

interface CameraSettingsData {
  isStreaming: boolean;
  performance: CameraPerformance;
  settings: CameraOptimizationSettings;
}

export default function CameraSettings() {
  const [cameraData, setCameraData] = useState<CameraSettingsData | null>(null);
  const [settings, setSettings] = useState<CameraOptimizationSettings | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [updateSuccess, setUpdateSuccess] = useState<boolean>(false);

  // Fetch camera settings
  const fetchCameraSettings = async () => {
    try {
      const response = await axios.get('/api/camera/settings');
      setCameraData(response.data);
      setSettings(response.data.settings);
      setLoading(false);
      setError(null);
    } catch (err) {
      setError('Failed to fetch camera settings');
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCameraSettings();
    // Refresh every 5 seconds
    const interval = setInterval(fetchCameraSettings, 5000);
    return () => clearInterval(interval);
  }, []);

  // Update camera settings
  const updateCameraSettings = async () => {
    if (!settings) return;

    try {
      await axios.post('/api/camera/settings', settings);
      setUpdateSuccess(true);
      setTimeout(() => setUpdateSuccess(false), 3000);
      // Refresh settings after update
      setTimeout(fetchCameraSettings, 500);
    } catch (err) {
      setError('Failed to update camera settings');
      setTimeout(() => setError(null), 3000);
    }
  };

  // Reset camera stats
  const resetCameraStats = async () => {
    try {
      await axios.post('/api/camera/reset');
      setUpdateSuccess(true);
      setTimeout(() => setUpdateSuccess(false), 3000);
      // Refresh settings after reset
      setTimeout(fetchCameraSettings, 500);
    } catch (err) {
      setError('Failed to reset camera stats');
      setTimeout(() => setError(null), 3000);
    }
  };

  // Preset configurations
  const applyPreset = (preset: 'fast' | 'balanced' | 'quality' | 'lowBandwidth') => {
    if (!settings) return;

    const newSettings = { ...settings };

    switch (preset) {
      case 'fast':
        newSettings.enableOptimizations = true;
        newSettings.enableAsyncEncoding = true;
        newSettings.enableTextureReuse = true;
        newSettings.enableAdaptiveQuality = true;
        newSettings.minJpegQuality = 40;
        newSettings.maxJpegQuality = 70;
        newSettings.enableFrameSkipping = true;
        newSettings.enableResolutionScaling = true;
        newSettings.lowBandwidthScale = 0.75;
        break;
      case 'balanced':
        newSettings.enableOptimizations = true;
        newSettings.enableAsyncEncoding = true;
        newSettings.enableTextureReuse = true;
        newSettings.enableAdaptiveQuality = true;
        newSettings.minJpegQuality = 50;
        newSettings.maxJpegQuality = 85;
        newSettings.enableFrameSkipping = true;
        newSettings.enableResolutionScaling = false;
        break;
      case 'quality':
        newSettings.enableOptimizations = true;
        newSettings.enableAsyncEncoding = true;
        newSettings.enableTextureReuse = true;
        newSettings.enableAdaptiveQuality = true;
        newSettings.minJpegQuality = 70;
        newSettings.maxJpegQuality = 95;
        newSettings.enableFrameSkipping = false;
        newSettings.enableResolutionScaling = false;
        newSettings.enableROICropping = false;
        break;
      case 'lowBandwidth':
        newSettings.enableOptimizations = true;
        newSettings.enableAsyncEncoding = true;
        newSettings.enableTextureReuse = true;
        newSettings.enableAdaptiveQuality = true;
        newSettings.minJpegQuality = 30;
        newSettings.maxJpegQuality = 60;
        newSettings.enableFrameSkipping = true;
        newSettings.enableResolutionScaling = true;
        newSettings.lowBandwidthScale = 0.5;
        newSettings.enableROICropping = true;
        break;
    }

    setSettings(newSettings);
  };

  if (loading) return <Typography>Loading camera settings...</Typography>;
  if (!cameraData || !settings) return <Typography>No camera data available</Typography>;

  return (
    <Paper sx={{ p: 3, mb: 3 }}>
      <Typography variant="h2" component="h2" gutterBottom>
        Camera Optimization Settings
      </Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {updateSuccess && <Alert severity="success" sx={{ mb: 2 }}>Settings updated successfully!</Alert>}

      {/* Performance Stats */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>Performance Statistics</Typography>
          <Grid container spacing={2}>
            <Grid item xs={6} md={3}>
              <Typography variant="body2" color="textSecondary">Processing Time</Typography>
              <Typography variant="h6">{cameraData.performance.avgProcessingTime.toFixed(1)}ms</Typography>
            </Grid>
            <Grid item xs={6} md={3}>
              <Typography variant="body2" color="textSecondary">Frame Count</Typography>
              <Typography variant="h6">{cameraData.performance.frameCount}</Typography>
            </Grid>
            <Grid item xs={6} md={3}>
              <Typography variant="body2" color="textSecondary">Skipped Frames</Typography>
              <Typography variant="h6">{cameraData.performance.skippedFrames}</Typography>
            </Grid>
            <Grid item xs={6} md={3}>
              <Typography variant="body2" color="textSecondary">Current Quality</Typography>
              <Typography variant="h6">{cameraData.performance.currentQuality}%</Typography>
            </Grid>
          </Grid>
          <Box sx={{ mt: 2 }}>
            <Chip 
              label={cameraData.isStreaming ? "Streaming Active" : "Streaming Stopped"} 
              color={cameraData.isStreaming ? "success" : "default"}
              sx={{ mr: 1 }}
            />
            <Chip 
              label={`Scale: ${(cameraData.performance.currentScale * 100).toFixed(0)}%`} 
              variant="outlined"
            />
          </Box>
        </CardContent>
      </Card>

      {/* Preset Buttons */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="h6" gutterBottom>Quick Presets</Typography>
        <Grid container spacing={1}>
          <Grid item>
            <Button variant="outlined" size="small" onClick={() => applyPreset('fast')}>
              Fast Mode
            </Button>
          </Grid>
          <Grid item>
            <Button variant="outlined" size="small" onClick={() => applyPreset('balanced')}>
              Balanced
            </Button>
          </Grid>
          <Grid item>
            <Button variant="outlined" size="small" onClick={() => applyPreset('quality')}>
              High Quality
            </Button>
          </Grid>
          <Grid item>
            <Button variant="outlined" size="small" onClick={() => applyPreset('lowBandwidth')}>
              Low Bandwidth
            </Button>
          </Grid>
        </Grid>
      </Box>

      <Divider sx={{ my: 2 }} />

      {/* Detailed Settings */}
      <Grid container spacing={3}>
        {/* Performance Settings */}
        <Grid item xs={12} md={6}>
          <Typography variant="h6" gutterBottom>Performance</Typography>
          
          <FormControlLabel
            control={
              <Switch
                checked={settings.enableOptimizations}
                onChange={(e) => setSettings({...settings, enableOptimizations: e.target.checked})}
              />
            }
            label="Enable Optimizations"
          />
          
          <FormControlLabel
            control={
              <Switch
                checked={settings.enableAsyncEncoding}
                onChange={(e) => setSettings({...settings, enableAsyncEncoding: e.target.checked})}
              />
            }
            label="Async Encoding"
          />
          
          <FormControlLabel
            control={
              <Switch
                checked={settings.enableTextureReuse}
                onChange={(e) => setSettings({...settings, enableTextureReuse: e.target.checked})}
              />
            }
            label="Texture Reuse"
          />
          
          <FormControlLabel
            control={
              <Switch
                checked={settings.enableFrameSkipping}
                onChange={(e) => setSettings({...settings, enableFrameSkipping: e.target.checked})}
              />
            }
            label="Frame Skipping"
          />

          <Box sx={{ mt: 2 }}>
            <Typography gutterBottom>Max Processing Time (ms)</Typography>
            <Slider
              value={settings.maxProcessingTimeMs}
              onChange={(_, value) => setSettings({...settings, maxProcessingTimeMs: value as number})}
              min={16}
              max={100}
              step={1}
              valueLabelDisplay="auto"
            />
          </Box>
        </Grid>

        {/* Quality Settings */}
        <Grid item xs={12} md={6}>
          <Typography variant="h6" gutterBottom>Quality & Bandwidth</Typography>
          
          <FormControlLabel
            control={
              <Switch
                checked={settings.enableAdaptiveQuality}
                onChange={(e) => setSettings({...settings, enableAdaptiveQuality: e.target.checked})}
              />
            }
            label="Adaptive Quality"
          />

          <Box sx={{ mt: 2 }}>
            <Typography gutterBottom>JPEG Quality Range</Typography>
            <Slider
              value={[settings.minJpegQuality, settings.maxJpegQuality]}
              onChange={(_, value) => {
                const [min, max] = value as number[];
                setSettings({...settings, minJpegQuality: min, maxJpegQuality: max});
              }}
              min={10}
              max={100}
              step={5}
              valueLabelDisplay="auto"
              marks={[
                { value: 30, label: '30%' },
                { value: 70, label: '70%' },
              ]}
            />
          </Box>

          <FormControlLabel
            control={
              <Switch
                checked={settings.enableResolutionScaling}
                onChange={(e) => setSettings({...settings, enableResolutionScaling: e.target.checked})}
              />
            }
            label="Resolution Scaling"
          />

          {settings.enableResolutionScaling && (
            <Box sx={{ mt: 1 }}>
              <Typography gutterBottom>Low Bandwidth Scale</Typography>
              <Slider
                value={settings.lowBandwidthScale}
                onChange={(_, value) => setSettings({...settings, lowBandwidthScale: value as number})}
                min={0.25}
                max={1.0}
                step={0.05}
                valueLabelDisplay="auto"
                valueLabelFormat={(value) => `${(value * 100).toFixed(0)}%`}
              />
            </Box>
          )}

          <FormControlLabel
            control={
              <Switch
                checked={settings.enableROICropping}
                onChange={(e) => setSettings({...settings, enableROICropping: e.target.checked})}
              />
            }
            label="ROI Cropping (Center 50%)"
          />
        </Grid>
      </Grid>

      {/* Action Buttons */}
      <Box sx={{ mt: 3, display: 'flex', gap: 2 }}>
        <Button
          variant="contained"
          color="primary"
          onClick={updateCameraSettings}
        >
          Apply Settings
        </Button>
        <Button
          variant="outlined"
          onClick={resetCameraStats}
        >
          Reset Stats
        </Button>
        <Button
          variant="outlined"
          onClick={fetchCameraSettings}
        >
          Refresh
        </Button>
      </Box>
    </Paper>
  );
}
