using System;
using Godot;

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

        // Android context and activity
        private Godot.Object _activity;
        private Godot.Object _context;
        private Godot.Object _powerManager;
        private Godot.Object _wakeLock;

        // Storage
        private string _externalFilesDir;
        private string _externalCacheDir;

        // Audio focus
        private Godot.Object _audioManager;
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
                _activity = GetAndroidActivity();
                _context = _activity.Call<Godot.Object>("getApplicationContext");

                // Power manager for wake lock
                _powerManager = _context.Call<Godot.Object>("getSystemService", "power");

                // Audio manager
                _audioManager = _context.Call<Godot.Object>("getSystemService", "audio");

                // Storage paths
                _externalFilesDir = _context.Call<string>("getExternalFilesDir", (string)null);
                _externalCacheDir = _context.Call<string>("getExternalCacheDir");

                // Check permissions
                CheckPermissions();

                GD.Print($"AndroidBridge: Initialized. External files: {_externalFilesDir}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"AndroidBridge initialization failed: {ex.Message}");
            }
        }

        private Godot.Object GetAndroidActivity()
        {
            // Godot 4.x provides JavaClass wrapper
            var unityPlayer = new JavaClass("com.godot.game.GodotActivity");
            return unityPlayer.GetStatic<Godot.Object>("getActivity");
        }

        private void CheckPermissions()
        {
            if (_context == null) return;

            try
            {
                // Check storage permission (Android 10+)
                int storagePerm = _context.Call<int>("checkSelfPermission",
                    "android.permission.READ_EXTERNAL_STORAGE");
                HasStoragePermission = storagePerm == 0; // PERMISSION_GRANTED

                int audioPerm = _context.Call<int>("checkSelfPermission",
                    "android.permission.RECORD_AUDIO");
                HasAudioPermission = audioPerm == 0;
            }
            catch
            {
                HasStoragePermission = false;
                HasAudioPermission = false;
            }
        }

        /// <summary>
        /// Request storage permission from user.
        /// </summary>
        public void RequestStoragePermission()
        {
            if (_activity == null) return;

            try
            {
                var permissions = new string[] {
                    "android.permission.READ_EXTERNAL_STORAGE",
                    "android.permission.WRITE_EXTERNAL_STORAGE"
                };

                _activity.Call("requestPermissions", permissions, 1001);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Request permission failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Acquire wake lock to prevent screen sleep during gameplay.
        /// </summary>
        public void AcquireWakeLock()
        {
            if (_powerManager == null || _wakeLock != null) return;

            try
            {
                // PARTIAL_WAKE_LOCK = 1
                _wakeLock = _powerManager.Call<Godot.Object>("newWakeLock", 1, "OpenBVE::GameLoop");
                _wakeLock.Call("acquire");
                GD.Print("AndroidBridge: Wake lock acquired");
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
                    _wakeLock.Call("release");
                    _wakeLock = null;
                    GD.Print("AndroidBridge: Wake lock released");
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
            if (_audioManager == null) return false;

            try
            {
                // AUDIOFOCUS_GAIN = 1
                _audioFocusRequest = _audioManager.Call<int>("requestAudioFocus",
                    null, // OnAudioFocusChangeListener (optional)
                    3,    // STREAM_MUSIC
                    1     // AUDIOFOCUS_GAIN
                );

                return _audioFocusRequest == 1; // AUDIOFOCUS_REQUEST_GRANTED
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Audio focus request failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Abandon audio focus.
        /// </summary>
        public void AbandonAudioFocus()
        {
            if (_audioManager == null || _audioFocusRequest == 0) return;

            try
            {
                _audioManager.Call("abandonAudioFocus", null);
                _audioFocusRequest = 0;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Abandon audio focus failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Show system toast message.
        /// </summary>
        public void ShowToast(string message, bool longDuration = false)
        {
            if (_activity == null) return;

            try
            {
                var toastClass = new JavaClass("android.widget.Toast");
                var toast = toastClass.CallStatic<Godot.Object>("makeText",
                    _context, message, longDuration ? 1 : 0); // LENGTH_LONG : LENGTH_SHORT
                toast.Call("show");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Toast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Open file picker for mod installation (Storage Access Framework).
        /// </summary>
        public void OpenFilePicker(string mimeType = "*/*")
        {
            if (_activity == null) return;

            try
            {
                var intentClass = new JavaClass("android.content.Intent");
                var intent = intentClass.New("android.intent.action.OPEN_DOCUMENT_TREE");
                intent.Call<Godot.Object>("addFlags", 0x00000001); // FLAG_GRANT_READ_URI_PERMISSION
                intent.Call<GodotObject>("addFlags", 0x00000002); // FLAG_GRANT_WRITE_URI_PERMISSION
                intent.Call<GodotObject>("addFlags", 0x00000004); // FLAG_GRANT_PERSISTABLE_URI_PERMISSION

                _activity.Call("startActivityForResult", intent, 2001);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"File picker failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get external storage path for mods.
        /// </summary>
        public string GetExternalModsPath()
        {
            if (!string.IsNullOrEmpty(_externalFilesDir))
            {
                return System.IO.Path.Combine(_externalFilesDir, "mods");
            }
            return null;
        }

        /// <summary>
        /// Vibrate device (haptic feedback).
        /// </summary>
        public void Vibrate(long milliseconds = 50)
        {
            if (_context == null) return;

            try
            {
                var vibrator = _context.Call<Godot.Object>("getSystemService", "vibrator");
                if (vibrator != null)
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Vibrate failed: {ex.Message}");
            }
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
                var metrics = _context.Call<Godot.Object>("getResources").Call<Godot.Object>("getDisplayMetrics");
                info.ScreenDensity = metrics.Get("densityDpi").AsInt32();
                info.ScreenWidth = metrics.Get("widthPixels").AsInt32();
                info.ScreenHeight = metrics.Get("heightPixels").AsInt32();

                // Memory
                var activityManager = _context.Call<Godot.Object>("getSystemService", "activity");
                var memInfo = new JavaClass("android.app.ActivityManager$MemoryInfo").New();
                activityManager.Call("getMemoryInfo", memInfo);
                info.TotalMemoryMB = memInfo.Get("totalMem").AsInt64() / 1024 / 1024;
                info.AvailableMemoryMB = memInfo.Get("availMem").AsInt64() / 1024 / 1024;

                // CPU cores
                info.CpuCores = OS.GetProcessorCount();

                // GPU info (approximate)
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
            public string GpuRenderer { get; set; }
        }
    }
}