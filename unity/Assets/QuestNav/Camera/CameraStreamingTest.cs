using UnityEngine;
using UnityEngine.UI;

namespace QuestNav.Camera
{
    /// <summary>
    /// Test script to demonstrate and configure the optimized PassthroughCameraStreamer
    /// </summary>
    public class CameraStreamingTest : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text performanceText;
        [SerializeField] private Slider qualitySlider;
        [SerializeField] private Slider scaleSlider;
        [SerializeField] private Toggle adaptiveQualityToggle;
        [SerializeField] private Toggle roiToggle;

        [Header("Camera Streamer")]
        [SerializeField] private PassthroughCameraStreamer cameraStreamer;

        private void Start()
        {
            if (cameraStreamer == null)
                cameraStreamer = FindObjectOfType<PassthroughCameraStreamer>();

            SetupUI();
        }

        private void Update()
        {
            UpdatePerformanceDisplay();
        }

        private void SetupUI()
        {
            if (startButton != null)
                startButton.onClick.AddListener(() => cameraStreamer?.StartStreaming());

            if (stopButton != null)
                stopButton.onClick.AddListener(() => cameraStreamer?.StopStreaming());

            if (qualitySlider != null)
            {
                qualitySlider.minValue = 10;
                qualitySlider.maxValue = 100;
                qualitySlider.value = 75;
                qualitySlider.onValueChanged.AddListener(OnQualityChanged);
            }

            if (scaleSlider != null)
            {
                scaleSlider.minValue = 0.25f;
                scaleSlider.maxValue = 1.0f;
                scaleSlider.value = 1.0f;
                scaleSlider.onValueChanged.AddListener(OnScaleChanged);
            }

            if (adaptiveQualityToggle != null)
                adaptiveQualityToggle.onValueChanged.AddListener(OnAdaptiveQualityToggled);

            if (roiToggle != null)
                roiToggle.onValueChanged.AddListener(OnROIToggled);
        }

        private void OnQualityChanged(float value)
        {
            int quality = Mathf.RoundToInt(value);
            cameraStreamer?.SetAdaptiveQuality(false, quality, quality);
            Debug.Log($"[QuestNav] Manual quality set to {quality}");
        }

        private void OnScaleChanged(float value)
        {
            cameraStreamer?.SetResolutionScaling(true, value);
            Debug.Log($"[QuestNav] Resolution scale set to {value:F2}");
        }

        private void OnAdaptiveQualityToggled(bool enabled)
        {
            cameraStreamer?.SetAdaptiveQuality(enabled);
            Debug.Log($"[QuestNav] Adaptive quality {(enabled ? "enabled" : "disabled")}");
        }

        private void OnROIToggled(bool enabled)
        {
            // Use center 50% as ROI
            Rect roi = new Rect(0.25f, 0.25f, 0.5f, 0.5f);
            cameraStreamer?.SetROICropping(enabled, roi);
            Debug.Log($"[QuestNav] ROI cropping {(enabled ? "enabled" : "disabled")}");
        }

        private void UpdatePerformanceDisplay()
        {
            if (cameraStreamer == null) return;

            if (statusText != null)
            {
                statusText.text = cameraStreamer.IsStreaming ? "Streaming" : "Stopped";
            }

            if (performanceText != null)
            {
                var stats = cameraStreamer.GetPerformanceStats();
                performanceText.text = $"Avg: {stats.avgProcessingTime:F1}ms\n" +
                                     $"Frames: {stats.frameCount}\n" +
                                     $"Skipped: {stats.skippedFrames}\n" +
                                     $"Quality: {stats.currentQuality}\n" +
                                     $"Scale: {stats.currentScale:F2}";
            }
        }

        [ContextMenu("Test Simple Mode (Safe)")]
        public void TestSimpleMode()
        {
            if (cameraStreamer != null)
            {
                // Disable all optimizations for safe operation
                var streamerType = cameraStreamer.GetType();
                var enableOptField = streamerType.GetField("enableOptimizations",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                enableOptField?.SetValue(cameraStreamer, false);

                Debug.Log("[QuestNav] Simple mode activated (no optimizations)");
            }
        }

        [ContextMenu("Test Fast Mode")]
        public void TestFastMode()
        {
            if (cameraStreamer != null)
            {
                var streamerType = cameraStreamer.GetType();

                // Enable performance optimizations
                var enableOptField = streamerType.GetField("enableOptimizations",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                enableOptField?.SetValue(cameraStreamer, true);

                var enableAsyncField = streamerType.GetField("enableAsyncEncoding",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                enableAsyncField?.SetValue(cameraStreamer, true);

                var enableTextureReuseField = streamerType.GetField("enableTextureReuse",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                enableTextureReuseField?.SetValue(cameraStreamer, true);

                // Set aggressive quality settings for speed
                cameraStreamer.SetAdaptiveQuality(true, 40, 70);

                Debug.Log("[QuestNav] Fast mode activated (all optimizations enabled)");
            }
        }

        [ContextMenu("Test Low Bandwidth Mode")]
        public void TestLowBandwidthMode()
        {
            if (cameraStreamer != null)
            {
                cameraStreamer.SetAdaptiveQuality(true, 30, 60);
                cameraStreamer.SetResolutionScaling(false); // Disabled for safety
                cameraStreamer.SetROICropping(true, new Rect(0.25f, 0.25f, 0.5f, 0.5f));
                Debug.Log("[QuestNav] Low bandwidth mode activated");
            }
        }

        [ContextMenu("Test High Quality Mode")]
        public void TestHighQualityMode()
        {
            if (cameraStreamer != null)
            {
                cameraStreamer.SetAdaptiveQuality(true, 70, 95);
                cameraStreamer.SetResolutionScaling(false);
                cameraStreamer.SetROICropping(false);
                Debug.Log("[QuestNav] High quality mode activated");
            }
        }

        [ContextMenu("Reset Performance Stats")]
        public void ResetPerformanceStats()
        {
            cameraStreamer?.ResetPerformanceStats();
            Debug.Log("[QuestNav] Performance stats reset");
        }
    }
}
