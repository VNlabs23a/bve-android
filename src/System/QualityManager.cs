using System;
using System.Text.Json;
using Godot;
using OpenBVEAndroid.Rendering;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Graphics quality management with presets for different device tiers.
    /// Handles applying quality settings at runtime.
    /// </summary>
    [GlobalClass]
    public partial class QualityManager : Node
    {
        public static QualityManager Instance { get; private set; }

        public QualityPreset CurrentPreset { get; private set; } = QualityPreset.Medium;
        public bool AutoQuality { get; set; } = true;

        // Quality settings
        public QualitySettings CurrentSettings { get; private set; }

        // References
        private LODManager _lodManager;
        private PerformanceMonitor _perfMonitor;

        public override void _EnterTree()
        {
            Instance = this;
            _lodManager = LODManager.Instance;
            _perfMonitor = PerformanceMonitor.Instance;

            LoadQualitySettings();
            ApplyQualityPreset(CurrentPreset);
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        public override void _Process(double delta)
        {
            if (AutoQuality && _perfMonitor != null)
            {
                CheckAutoQuality();
            }
        }

        /// <summary>
        /// Apply a quality preset.
        /// </summary>
        public void ApplyQualityPreset(QualityPreset preset)
        {
            CurrentPreset = preset;
            CurrentSettings = GetPresetSettings(preset);

            // Apply to LOD manager
            _lodManager?.SetQualityLevel((int)preset);

            // Apply rendering settings
            ApplyRenderingSettings();

            // Apply shadow settings
            ApplyShadowSettings();

            // Apply post-processing
            ApplyPostProcessingSettings();

            // Save
            SaveQualitySettings();

            GD.Print($"QualityManager: Applied {preset} preset");
        }

        /// <summary>
        /// Apply custom quality settings.
        /// </summary>
        public void ApplyCustomSettings(QualitySettings settings)
        {
            CurrentSettings = settings;
            CurrentPreset = QualityPreset.Custom;

            ApplyRenderingSettings();
            ApplyShadowSettings();
            ApplyPostProcessingSettings();

            SaveQualitySettings();
        }

        private void ApplyRenderingSettings()
        {
            // VSync
            DisplayServer.WindowSetVsyncMode(
                CurrentSettings.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled
            );

            // Texture filter and anisotropic filtering are set per-material in Godot 4.x
            // Engine.MaxFps for frame rate limiting
            Engine.MaxFps = CurrentSettings.TargetFPS;
        }

        private void ApplyShadowSettings()
        {
            // Shadow settings are configured per-light and per-environment in Godot 4.x
            // This is a simplified stub - real implementation would configure directional light shadows
            GD.Print($"QualityManager: Shadow quality set to {CurrentSettings.ShadowQuality}");
        }

        private void ApplyPostProcessingSettings()
        {
            // Post-processing settings are configured via WorldEnvironment node in Godot 4.x
            // This is a simplified stub
            GD.Print($"QualityManager: Post-processing - Bloom: {CurrentSettings.BloomEnabled}, ToneMapping: {CurrentSettings.ToneMappingEnabled}");
        }

        private void CheckAutoQuality()
        {
            // Auto-adjust based on performance
            float avgFrameTime = _perfMonitor.AverageFrameTime;
            float targetFrameTime = 1000f / CurrentSettings.TargetFPS;

            if (avgFrameTime > targetFrameTime * 1.2f && CurrentPreset > QualityPreset.Low)
            {
                // Performance too low, reduce quality
                ApplyQualityPreset(CurrentPreset - 1);
                GD.Print($"QualityManager: Auto-reduced to {CurrentPreset} (frame time: {avgFrameTime:F1}ms)");
            }
            else if (avgFrameTime < targetFrameTime * 0.7f && CurrentPreset < QualityPreset.Ultra)
            {
                // Headroom available, increase quality
                ApplyQualityPreset(CurrentPreset + 1);
                GD.Print($"QualityManager: Auto-increased to {CurrentPreset} (frame time: {avgFrameTime:F1}ms)");
            }
        }

        private QualitySettings GetPresetSettings(QualityPreset preset)
        {
            return preset switch
            {
                QualityPreset.Low => new QualitySettings
                {
                    RenderScale = 0.5f,
                    TextureFilter = RenderingServer.CanvasItemTextureFilter.Nearest,
                    AnisotropicLevel = 0,
                    ShadowsEnabled = false,
                    ShadowQuality = ShadowQuality.Low,
                    ShadowCascades = 1,
                    ShadowFilter = RenderingServer.CanvasItemTextureFilter.Nearest,
                    BloomEnabled = false,
                    BloomIntensity = 0f,
                    BloomThreshold = 1f,
                    ToneMappingEnabled = false,
                    Exposure = 1f,
                    Contrast = 1f,
                    Saturation = 1f,
                    VSync = true,
                    TargetFPS = 30,
                    LODBias = 2.0f,
                    ParticleQuality = ParticleQuality.Low,
                    MaxLights = 4
                },
                QualityPreset.Medium => new QualitySettings
                {
                    RenderScale = 0.75f,
                    TextureFilter = RenderingServer.CanvasItemTextureFilter.Linear,
                    AnisotropicLevel = 2,
                    ShadowsEnabled = true,
                    ShadowQuality = ShadowQuality.Medium,
                    ShadowCascades = 2,
                    ShadowFilter = RenderingServer.CanvasItemTextureFilter.Linear,
                    BloomEnabled = false,
                    BloomIntensity = 0.3f,
                    BloomThreshold = 1f,
                    ToneMappingEnabled = true,
                    Exposure = 1f,
                    Contrast = 1f,
                    Saturation = 1f,
                    VSync = true,
                    TargetFPS = 30,
                    LODBias = 1.0f,
                    ParticleQuality = ParticleQuality.Medium,
                    MaxLights = 8
                },
                QualityPreset.High => new QualitySettings
                {
                    RenderScale = 1.0f,
                    TextureFilter = RenderingServer.CanvasItemTextureFilter.Linear,
                    AnisotropicLevel = 4,
                    ShadowsEnabled = true,
                    ShadowQuality = ShadowQuality.High,
                    ShadowCascades = 3,
                    ShadowFilter = RenderingServer.CanvasItemTextureFilter.Linear,
                    BloomEnabled = true,
                    BloomIntensity = 0.5f,
                    BloomThreshold = 1f,
                    ToneMappingEnabled = true,
                    Exposure = 1f,
                    Contrast = 1.1f,
                    Saturation = 1.1f,
                    VSync = true,
                    TargetFPS = 60,
                    LODBias = 0.5f,
                    ParticleQuality = ParticleQuality.High,
                    MaxLights = 16
                },
                QualityPreset.Ultra => new QualitySettings
                {
                    RenderScale = 1.0f,
                    TextureFilter = RenderingServer.CanvasItemTextureFilter.Linear,
                    AnisotropicLevel = 8,
                    ShadowsEnabled = true,
                    ShadowQuality = ShadowQuality.Ultra,
                    ShadowCascades = 4,
                    ShadowFilter = RenderingServer.CanvasItemTextureFilter.Linear,
                    BloomEnabled = true,
                    BloomIntensity = 0.7f,
                    BloomThreshold = 0.8f,
                    ToneMappingEnabled = true,
                    Exposure = 1f,
                    Contrast = 1.15f,
                    Saturation = 1.2f,
                    VSync = false, // Allow higher FPS
                    TargetFPS = 120,
                    LODBias = 0.25f,
                    ParticleQuality = ParticleQuality.Ultra,
                    MaxLights = 32
                },
                _ => GetPresetSettings(QualityPreset.Medium)
            };
        }

        private void SaveQualitySettings()
        {
            string configPath = OS.GetUserDataDir() + "/quality_config.json";
            var config = new QualityConfig
            {
                Preset = CurrentPreset.ToString(),
                AutoQuality = AutoQuality,
                CustomSettings = CurrentSettings
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private void LoadQualitySettings()
        {
            string configPath = OS.GetUserDataDir() + "/quality_config.json";
            if (!File.Exists(configPath))
                return;

            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<QualityConfig>(json);

                if (Enum.TryParse(config.Preset, out QualityPreset preset))
                {
                    CurrentPreset = preset;
                }
                AutoQuality = config.AutoQuality;
                if (config.CustomSettings != null)
                {
                    CurrentSettings = config.CustomSettings;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to load quality config: {ex.Message}");
            }
        }

        public string GetPresetName(QualityPreset preset)
        {
            return preset switch
            {
                QualityPreset.Low => "Low (30 FPS target)",
                QualityPreset.Medium => "Medium (30 FPS target)",
                QualityPreset.High => "High (60 FPS target)",
                QualityPreset.Ultra => "Ultra (60-120 FPS target)",
                QualityPreset.Custom => "Custom",
                _ => "Unknown"
            };
        }

        public enum QualityPreset
        {
            Low = 0,
            Medium = 1,
            High = 2,
            Ultra = 3,
            Custom = 4
        }

        public enum ShadowQuality
        {
            Low,
            Medium,
            High,
            Ultra
        }

        public enum ParticleQuality
        {
            Low,
            Medium,
            High,
            Ultra
        }
    }

    [Serializable]
    public class QualitySettings
    {
        public float RenderScale { get; set; } = 1.0f;
        public RenderingServer.CanvasItemTextureFilter TextureFilter { get; set; }
        public int AnisotropicLevel { get; set; } = 4;
        public bool ShadowsEnabled { get; set; } = true;
        public QualityManager.ShadowQuality ShadowQuality { get; set; }
        public int ShadowCascades { get; set; } = 3;
        public RenderingServer.CanvasItemTextureFilter ShadowFilter { get; set; }
        public bool BloomEnabled { get; set; } = true;
        public float BloomIntensity { get; set; } = 0.5f;
        public float BloomThreshold { get; set; } = 1.0f;
        public bool ToneMappingEnabled { get; set; } = true;
        public float Exposure { get; set; } = 1.0f;
        public float Contrast { get; set; } = 1.0f;
        public float Saturation { get; set; } = 1.0f;
        public bool VSync { get; set; } = true;
        public int TargetFPS { get; set; } = 60;
        public float LODBias { get; set; } = 1.0f;
        public QualityManager.ParticleQuality ParticleQuality { get; set; }
        public int MaxLights { get; set; } = 16;
    }

    [Serializable]
    public class QualityConfig
    {
        public string Preset { get; set; } = "Medium";
        public bool AutoQuality { get; set; } = true;
        public QualitySettings CustomSettings { get; set; }
    }
}