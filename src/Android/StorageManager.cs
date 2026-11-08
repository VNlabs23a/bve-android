using System;
using System.IO;
using System.Collections.Generic;
using Godot;

namespace OpenBVEAndroid.Android
{
    /// <summary>
    /// Storage management for Android - handles scoped storage, SAF, and file access.
    /// </summary>
    [GlobalClass]
    public partial class StorageManager : Node
    {
        public static StorageManager Instance { get; private set; }

        // Paths
        public string InternalModsPath { get; private set; }
        public string ExternalModsPath { get; private set; }
        public string SavesPath { get; private set; }
        public string CachePath { get; private set; }

        // SAF URIs for external directories
        private Dictionary<string, string> _safUris = new();

        public override void _EnterTree()
        {
            Instance = this;
            InitializePaths();
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        private void InitializePaths()
        {
            string userData = OS.GetUserDataDir();

            // Internal storage (always accessible)
            InternalModsPath = Path.Combine(userData, "mods");
            SavesPath = Path.Combine(userData, "saves");
            CachePath = Path.Combine(userData, "cache");

            // Create directories
            Directory.CreateDirectory(InternalModsPath);
            Directory.CreateDirectory(SavesPath);
            Directory.CreateDirectory(CachePath);

            // External storage (Android 10+ scoped storage)
            if (OS.GetName() == "Android")
            {
                var bridge = AndroidBridge.Instance;
                if (bridge != null)
                {
                    ExternalModsPath = bridge.GetExternalModsPath();
                    if (!string.IsNullOrEmpty(ExternalModsPath))
                    {
                        Directory.CreateDirectory(ExternalModsPath);
                    }
                }
            }

            GD.Print($"StorageManager: Internal mods: {InternalModsPath}");
            GD.Print($"StorageManager: External mods: {ExternalModsPath}");
            GD.Print($"StorageManager: Saves: {SavesPath}");
        }

        /// <summary>
        /// Get all available mods directories.
        /// </summary>
        public string[] GetModsDirectories()
        {
            var dirs = new List<string> { InternalModsPath };

            if (!string.IsNullOrEmpty(ExternalModsPath) && Directory.Exists(ExternalModsPath))
            {
                dirs.Add(ExternalModsPath);
            }

            return dirs.ToArray();
        }

        /// <summary>
        /// Copy mod from external to internal storage.
        /// </summary>
        public bool ImportMod(string sourcePath, string modName)
        {
            try
            {
                string destPath = Path.Combine(InternalModsPath, modName);

                if (Directory.Exists(destPath))
                {
                    // Backup existing
                    string backupPath = destPath + "_backup_" + DateTime.Now.Ticks;
                    Directory.Move(destPath, backupPath);
                }

                CopyDirectory(sourcePath, destPath);
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Import mod failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export mod to external storage (for sharing).
        /// </summary>
        public bool ExportMod(string modName, string destPath)
        {
            try
            {
                string sourcePath = Path.Combine(InternalModsPath, modName);
                if (!Directory.Exists(sourcePath))
                    return false;

                CopyDirectory(sourcePath, destPath);
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Export mod failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete a mod.
        /// </summary>
        public bool DeleteMod(string modName)
        {
            try
            {
                string path = Path.Combine(InternalModsPath, modName);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Delete mod failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save game state.
        /// </summary>
        public bool SaveGame(string saveName, byte[] data)
        {
            try
            {
                string path = Path.Combine(SavesPath, saveName + ".sav");
                File.WriteAllBytes(path, data);
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Save game failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load game state.
        /// </summary>
        public byte[] LoadGame(string saveName)
        {
            try
            {
                string path = Path.Combine(SavesPath, saveName + ".sav");
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
                return null;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Load game failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// List save files.
        /// </summary>
        public string[] ListSaves()
        {
            try
            {
                return Directory.GetFiles(SavesPath, "*.sav")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Delete save file.
        /// </summary>
        public bool DeleteSave(string saveName)
        {
            try
            {
                string path = Path.Combine(SavesPath, saveName + ".sav");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Delete save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear cache directory.
        /// </summary>
        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(CachePath))
                {
                    Directory.Delete(CachePath, true);
                    Directory.CreateDirectory(CachePath);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Clear cache failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get available storage space in bytes.
        /// </summary>
        public long GetAvailableSpace(string path)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path));
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get total storage space in bytes.
        /// </summary>
        public long GetTotalSpace(string path)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path));
                return drive.TotalSize;
            }
            catch
            {
                return 0;
            }
        }

        private void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(dest, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(dest, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        /// <summary>
        /// Handle SAF URI permission grant (called from Android activity result).
        /// </summary>
        public void OnSafUriGranted(string uri, string displayName)
        {
            _safUris[displayName] = uri;
            GD.Print($"StorageManager: SAF URI granted for {displayName}: {uri}");
        }

        /// <summary>
        /// Read file via SAF (for external mods).
        /// </summary>
        public byte[] ReadSafFile(string displayName)
        {
            if (!_safUris.TryGetValue(displayName, out string uri))
                return null;

            try
            {
                // Use ContentResolver to open input stream
                var bridge = AndroidBridge.Instance;
                if (bridge == null) return null;

                var contentResolver = bridge._context.Call<Godot.Object>("getContentResolver");
                var uriObj = new JavaClass("android.net.Uri").CallStatic<Godot.Object>("parse", uri);
                var inputStream = contentResolver.Call<Godot.Object>("openInputStream", uriObj);

                // Read all bytes
                var buffer = new System.Collections.Generic.List<byte>();
                int b;
                while ((b = inputStream.Call<int>("read")) != -1)
                {
                    buffer.Add((byte)b);
                }
                inputStream.Call("close");

                return buffer.ToArray();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"SAF read failed: {ex.Message}");
                return null;
            }
        }
    }
}