using System;
using System.IO;
using Godot;
using OpenBVEAndroid.System;

namespace OpenBVEAndroid.Android
{
    /// <summary>
    /// Bridge to Android Java/Kotlin APIs via JNI.
    /// Handles platform-specific functionality not available in Godot core.
    /// </summary>
    [GlobalClass]
    public partial class AndroidBridge : Node
    {
        public static AndroidBridge Instance { get; private set; }

        // Android context and activity (stubbed for compilation)
        private object _activity;
        private object _context;
        private object _powerManager;
        private object _wakeLock;

        // Storage
        private string _externalFilesDir = "";
        private string _externalCacheDir = "";

        // Audio focus
        private object _audioManager;
        private int _audioFocusRequest = 0;

        // Permissions
        public bool HasStoragePermission { get; private set; } = false;
        public bool HasAudioPermission { get; private set; } = false;

        public override void _EnterTree()
        {
            Instance = this;

            if (OS.GetName() == "Android")
            {
                InitializeAndroid();
            }
        }

        public override void _ExitTree()
        {
            ReleaseWakeLock();
            AbandonAudioFocus();
            Instance = null;
        }

        private void InitializeAndroid()
        {
            try
            {
                // Get UnityPlayerActivity equivalent (Godot's MainActivity)
                // Note: Actual JNI implementation would go here
                // For now, we stub the paths
                _externalFilesDir = OS.GetUserDataDir();
                _externalCacheDir = OS.GetUserDataDir() + "/cache";

                GD.Print($"AndroidBridge: Initialized (stub). External files: {_externalFilesDir}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"AndroidBridge initialization failed: {ex.Message}");
            }
        }

        private void CheckPermissions()
        {
            // Stub - actual implementation would check Android permissions
            HasStoragePermission = true;
            HasAudioPermission = true;
        }

        /// <summary>
        /// Request storage permission from user.
        /// </summary>
        public void RequestStoragePermission()
        {
            // Stub - actual implementation would call Android API
            GD.Print("AndroidBridge: RequestStoragePermission called (stub)");
        }

        /// <summary>
        /// Acquire wake lock to prevent screen sleep during gameplay.
        /// </summary>
        public void AcquireWakeLock()
        {
            if (_wakeLock != null) return;

            try
            {
                // In Godot 4.x, use OS.SetKeepScreenOn (or DisplayServer on some versions)
                // For Android, we stub this since actual implementation needs JNI
                _wakeLock = new object(); // Dummy object
                GD.Print("AndroidBridge: Wake lock acquired (stub)");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Wake lock failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Release wake lock.
        /// </summary>
        public void ReleaseWakeLock()
        {
            if (_wakeLock != null)
            {
                try
                {
                    _wakeLock = null;
                    GD.Print("AndroidBridge: Wake lock released (stub)");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Wake lock release failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Request audio focus for game audio.
        /// </summary>
        public bool RequestAudioFocus()
        {
            // Stub - Godot handles audio focus automatically
            _audioFocusRequest = 1;
            return true;
        }

        /// <summary>
        /// Abandon audio focus.
        /// </summary>
        public void AbandonAudioFocus()
        {
            _audioFocusRequest = 0;
        }

        /// <summary>
        /// Show system toast message.
        /// </summary>
        public void ShowToast(string message, bool longDuration = false)
        {
            // Use Godot's built-in notification instead
            GD.Print($"Toast: {message}");
        }

        /// <summary>
        /// Open file picker for mod installation (Storage Access Framework).
        /// </summary>
        public void OpenFilePicker(string mimeType = "*/*")
        {
            // Stub - actual implementation would use FileDialog or Android SAF
            GD.Print("AndroidBridge: OpenFilePicker called (stub)");
        }

        /// <summary>
        /// Get external storage path for mods.
        /// </summary>
        public string GetExternalModsPath()
        {
            if (!string.IsNullOrEmpty(_externalFilesDir))
            {
                return global::System.IO.Path.Combine(_externalFilesDir, "mods");
            }
            return "";
        }

        /// <summary>
        /// Vibrate device (haptic feedback).
        /// </summary>
        public void Vibrate(long milliseconds = 50)
        {
            // Use Godot's built-in vibration (expects milliseconds as int)
            Input.VibrateHandheld((int)milliseconds);
        }

        /// <summary>
        /// Get device info for quality preset selection.
        /// </summary>
        public DeviceInfo GetDeviceInfo()
        {
            var info = new DeviceInfo();

            try
            {
                // Screen density
                info.ScreenDensity = (int)DisplayServer.ScreenGetDpi();
                info.ScreenWidth = DisplayServer.WindowGetSize().X;
                info.ScreenHeight = DisplayServer.WindowGetSize().Y;

                // Memory - approximate
                info.TotalMemoryMB = 4096; // Default assumption
                info.AvailableMemoryMB = 2048;

                // CPU cores
                info.CpuCores = OS.GetProcessorCount();

                // GPU info
                info.GpuRenderer = RenderingServer.GetRenderingDevice().GetDeviceName();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Get device info failed: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Suggest quality preset based on device capabilities.
        /// </summary>
        public QualityManager.QualityPreset SuggestQualityPreset()
        {
            var info = GetDeviceInfo();

            // Score-based selection
            int score = 0;

            // Memory
            if (info.TotalMemoryMB >= 8000) score += 3;
            else if (info.TotalMemoryMB >= 4000) score += 2;
            else if (info.TotalMemoryMB >= 2000) score += 1;

            // CPU cores
            if (info.CpuCores >= 8) score += 2;
            else if (info.CpuCores >= 4) score += 1;

            // Screen resolution (lower = easier to render)
            int pixels = info.ScreenWidth * info.ScreenHeight;
            if (pixels <= 1280 * 720) score += 2;
            else if (pixels <= 1920 * 1080) score += 1;

            // Map score to preset
            return score switch
            {
                >= 7 => QualityManager.QualityPreset.Ultra,
                >= 5 => QualityManager.QualityPreset.High,
                >= 3 => QualityManager.QualityPreset.Medium,
                _ => QualityManager.QualityPreset.Low
            };
        }

        public class DeviceInfo
        {
            public int ScreenWidth { get; set; }
            public int ScreenHeight { get; set; }
            public int ScreenDensity { get; set; }
            public long TotalMemoryMB { get; set; }
            public long AvailableMemoryMB { get; set; }
            public int CpuCores { get; set; }
            public string GpuRenderer { get; set; } = "";
        }
    }
}