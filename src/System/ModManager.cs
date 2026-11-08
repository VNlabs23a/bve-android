using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Mod management system for loading and managing OpenBVE mods.
    /// Supports routes, trains, objects, and dependencies.
    /// </summary>
    [GlobalClass]
    public partial class ModManager : Node
    {
        public static ModManager Instance { get; private set; }

        public List<Mod> LoadedMods { get; private set; } = new();
        public List<Mod> EnabledMods => LoadedMods.Where(m => m.Enabled).ToList();

        // Mod database
        private Dictionary<string, Mod> _modDatabase = new();

        // Events
        public event Action<Mod> OnModLoaded;
        public event Action<Mod> OnModEnabled;
        public event Action<Mod> OnModDisabled;
        public event Action<string> OnError;

        public override void _EnterTree()
        {
            Instance = this;
            ScanMods();
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        /// <summary>
        /// Scan for mods in all mounted directories.
        /// </summary>
        public void ScanMods()
        {
            _modDatabase.Clear();
            LoadedMods.Clear();

            var vfs = VirtualFS.Instance;
            if (vfs == null) return;

            // Scan mods directory
            string[] modDirs = vfs.ListDirectories("/mods");
            foreach (var dir in modDirs)
            {
                try
                {
                    var mod = LoadModFromDirectory(dir);
                    if (mod != null)
                    {
                        _modDatabase[mod.Id] = mod;
                        LoadedMods.Add(mod);
                        OnModLoaded?.Invoke(mod);
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Failed to load mod {dir}: {ex.Message}");
                }
            }

            // Scan external mods
            string[] extDirs = vfs.ListDirectories("/mods/external");
            foreach (var dir in extDirs)
            {
                try
                {
                    var mod = LoadModFromDirectory(dir);
                    if (mod != null)
                    {
                        _modDatabase[mod.Id] = mod;
                        LoadedMods.Add(mod);
                        OnModLoaded?.Invoke(mod);
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Failed to load external mod {dir}: {ex.Message}");
                }
            }

            // Load enabled state from config
            LoadModConfig();

            GD.Print($"ModManager: Found {LoadedMods.Count} mods ({EnabledMods.Count} enabled)");
        }

        private Mod LoadModFromDirectory(string virtualPath)
        {
            var vfs = VirtualFS.Instance;
            string manifestPath = virtualPath + "/mod.json";

            Mod mod;
            if (vfs.Exists(manifestPath))
            {
                // Load from manifest
                using var file = vfs.OpenRead(manifestPath);
                if (file == null) return null;

                string json = file.GetAsText();
                file.Close();

                mod = JsonSerializer.Deserialize<Mod>(json);
                if (mod == null) return null;

                mod.VirtualPath = virtualPath;
                mod.Source = ModSource.Directory;
            }
            else
            {
                // Auto-detect mod type
                mod = DetectModType(virtualPath);
                if (mod == null) return null;
            }

            mod.VirtualPath = virtualPath;
            mod.Id = GenerateModId(virtualPath);
            mod.LastScanned = DateTime.Now;

            return mod;
        }

        private Mod DetectModType(string virtualPath)
        {
            var vfs = VirtualFS.Instance;

            // Check for route
            string[] routeFiles = vfs.ListFiles(virtualPath, "*.csv");
            if (routeFiles.Length > 0)
            {
                return new Mod
                {
                    Name = Path.GetFileName(virtualPath),
                    Type = ModType.Route,
                    Description = "Auto-detected route",
                    VirtualPath = virtualPath
                };
            }

            routeFiles = vfs.ListFiles(virtualPath, "*.rw");
            if (routeFiles.Length > 0)
            {
                return new Mod
                {
                    Name = Path.GetFileName(virtualPath),
                    Type = ModType.Route,
                    Description = "Auto-detected route (RW)",
                    VirtualPath = virtualPath
                };
            }

            // Check for train
            if (vfs.Exists(virtualPath + "/train.dat"))
            {
                return new Mod
                {
                    Name = Path.GetFileName(virtualPath),
                    Type = ModType.Train,
                    Description = "Auto-detected train",
                    VirtualPath = virtualPath
                };
            }

            // Check for objects
            string[] objFiles = vfs.ListFiles(virtualPath, "*.csv");
            if (objFiles.Length > 0)
            {
                // Check if it's objects (has texture files but no route structure)
                string[] texFiles = vfs.ListFiles(virtualPath, "*.png");
                texFiles = texFiles.Concat(vfs.ListFiles(virtualPath, "*.jpg")).ToArray();
                texFiles = texFiles.Concat(vfs.ListFiles(virtualPath, "*.bmp")).ToArray();

                if (texFiles.Length > 0)
                {
                    return new Mod
                    {
                        Name = Path.GetFileName(virtualPath),
                        Type = ModType.Object,
                        Description = "Auto-detected object pack",
                        VirtualPath = virtualPath
                    };
                }
            }

            return null;
        }

        private string GenerateModId(string virtualPath)
        {
            // Generate stable ID from path
            return virtualPath.Replace("/", "_").Trim('_').ToLowerInvariant();
        }

        /// <summary>
        /// Enable a mod.
        /// </summary>
        public bool EnableMod(string modId)
        {
            if (!_modDatabase.TryGetValue(modId, out Mod mod))
                return false;

            if (mod.Enabled)
                return true;

            // Check dependencies
            if (!CheckDependencies(mod))
            {
                OnError?.Invoke($"Mod {mod.Name} has unmet dependencies");
                return false;
            }

            // Enable dependencies first
            foreach (var depId in mod.Dependencies)
            {
                if (_modDatabase.TryGetValue(depId, out Mod dep) && !dep.Enabled)
                {
                    EnableMod(depId);
                }
            }

            mod.Enabled = true;
            OnModEnabled?.Invoke(mod);
            SaveModConfig();

            GD.Print($"ModManager: Enabled {mod.Name}");
            return true;
        }

        /// <summary>
        /// Disable a mod.
        /// </summary>
        public void DisableMod(string modId)
        {
            if (!_modDatabase.TryGetValue(modId, out Mod mod))
                return;

            if (!mod.Enabled)
                return;

            // Check if other enabled mods depend on this
            foreach (var other in EnabledMods)
            {
                if (other.Dependencies.Contains(modId))
                {
                    OnError?.Invoke($"Cannot disable {mod.Name}: required by {other.Name}");
                    return;
                }
            }

            mod.Enabled = false;
            OnModDisabled?.Invoke(mod);
            SaveModConfig();

            GD.Print($"ModManager: Disabled {mod.Name}");
        }

        private bool CheckDependencies(Mod mod)
        {
            foreach (var depId in mod.Dependencies)
            {
                if (!_modDatabase.ContainsKey(depId))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Get mod by ID.
        /// </summary>
        public Mod GetMod(string modId)
        {
            _modDatabase.TryGetValue(modId, out Mod mod);
            return mod;
        }

        /// <summary>
        /// Get all routes from enabled mods.
        /// </summary>
        public Mod[] GetEnabledRoutes()
        {
            return EnabledMods.Where(m => m.Type == ModType.Route).ToArray();
        }

        /// <summary>
        /// Get all trains from enabled mods.
        /// </summary>
        public Mod[] GetEnabledTrains()
        {
            return EnabledMods.Where(m => m.Type == ModType.Train).ToArray();
        }

        /// <summary>
        /// Get all object packs from enabled mods.
        /// </summary>
        public Mod[] GetEnabledObjects()
        {
            return EnabledMods.Where(m => m.Type == ModType.Object).ToArray();
        }

        /// <summary>
        /// Install mod from ZIP file.
        /// </summary>
        public async void InstallModFromZip(string zipPath)
        {
            try
            {
                string extractPath = Path.Combine(OS.GetUserDataDir(), "mods", Path.GetFileNameWithoutExtension(zipPath));

                // Extract using SharpCompress
                using var stream = File.OpenRead(zipPath);
                var archive = SharpCompress.Archives.ArchiveFactory.Open(stream);
                archive.WriteToDirectory(extractPath, new SharpCompress.Common.ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });

                // Scan the new mod
                var mod = LoadModFromDirectory("/mods/" + Path.GetFileNameWithoutExtension(zipPath));
                if (mod != null)
                {
                    _modDatabase[mod.Id] = mod;
                    LoadedMods.Add(mod);
                    OnModLoaded?.Invoke(mod);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to install mod: {ex.Message}");
            }
        }

        private void SaveModConfig()
        {
            string configPath = OS.GetUserDataDir() + "/mods_config.json";
            var config = new ModConfig
            {
                EnabledMods = EnabledMods.Select(m => m.Id).ToArray()
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private void LoadModConfig()
        {
            string configPath = OS.GetUserDataDir() + "/mods_config.json";
            if (!File.Exists(configPath))
                return;

            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ModConfig>(json);

                if (config?.EnabledMods != null)
                {
                    foreach (var modId in config.EnabledMods)
                    {
                        if (_modDatabase.TryGetValue(modId, out Mod mod))
                        {
                            mod.Enabled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to load mod config: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Mod metadata.
    /// </summary>
    public class Mod
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; }
        public ModType Type { get; set; }
        public string VirtualPath { get; set; }
        public ModSource Source { get; set; }
        public bool Enabled { get; set; } = false;
        public DateTime LastScanned { get; set; }
        public string[] Dependencies { get; set; } = Array.Empty<string>();
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string PreviewImage { get; set; }
        public long SizeBytes { get; set; }
    }

    public enum ModType
    {
        Route,
        Train,
        Object,
        Sound,
        Texture,
        Script,
        Pack // Collection of multiple types
    }

    public enum ModSource
    {
        Directory,
        Zip,
        Workshop,
        BuiltIn
    }

    public class ModConfig
    {
        public string[] EnabledMods { get; set; } = Array.Empty<string>();
    }
}