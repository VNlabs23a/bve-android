using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Virtual File System for mod support.
    /// Provides case-insensitive path resolution across mods, base game, and external storage.
    /// </summary>
    [GlobalClass]
    public partial class VirtualFS : Node
    {
        public static VirtualFS Instance { get; private set; }

        // Mount points in priority order (first = highest priority)
        private List<MountPoint> _mountPoints = new();

        // Cache for resolved paths
        private Dictionary<string, string> _pathCache = new();

        public override void _EnterTree()
        {
            Instance = this;
            InitializeDefaultMounts();
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        private void InitializeDefaultMounts()
        {
            // 1. User mods (highest priority)
            string userMods = OS.GetUserDataDir() + "/mods";
            if (DirAccess.DirExistsAbsolute(userMods))
            {
                Mount(new DirectoryMount(userMods, "/mods", true));
            }

            // 2. External storage mods (Android scoped storage)
            if (OS.GetName() == "Android")
            {
                string extMods = "/storage/emulated/0/OpenBVE/mods";
                if (DirAccess.DirExistsAbsolute(extMods))
                {
                    Mount(new DirectoryMount(extMods, "/mods/external", false));
                }
            }

            // 3. Base game assets (read-only)
            Mount(new DirectoryMount("res://assets/default_mods", "/base", false));

            // 4. Built-in resources
            Mount(new ResourceMount("res://", "/internal", false));
        }

        /// <summary>
        /// Add a mount point.
        /// </summary>
        public void Mount(MountPoint mount)
        {
            _mountPoints.Insert(0, mount); // Insert at front for priority
            _pathCache.Clear();
            GD.Print($"VFS: Mounted {mount.SourcePath} at {mount.VirtualPath} (priority: {mount.Priority})");
        }

        /// <summary>
        /// Remove a mount point.
        /// </summary>
        public void Unmount(string virtualPath)
        {
            _mountPoints.RemoveAll(m => m.VirtualPath == virtualPath);
            _pathCache.Clear();
        }

        /// <summary>
        /// Resolve a virtual path to a real filesystem path.
        /// </summary>
        public string Resolve(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
                return null;

            // Normalize path
            virtualPath = NormalizePath(virtualPath);

            // Check cache
            if (_pathCache.TryGetValue(virtualPath, out string cached))
                return cached;

            // Search mount points in priority order
            foreach (var mount in _mountPoints)
            {
                if (mount.TryResolve(virtualPath, out string realPath))
                {
                    _pathCache[virtualPath] = realPath;
                    return realPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if a virtual path exists.
        /// </summary>
        public bool Exists(string virtualPath)
        {
            return Resolve(virtualPath) != null;
        }

        /// <summary>
        /// Open a file for reading.
        /// </summary>
        public Godot.FileAccess OpenRead(string virtualPath)
        {
            string realPath = Resolve(virtualPath);
            if (realPath == null)
                return null;

            if (realPath.StartsWith("res://"))
            {
                return Godot.FileAccess.Open(realPath, Godot.FileAccess.ModeFlags.Read);
            }
            else
            {
                return Godot.FileAccess.Open(realPath, Godot.FileAccess.ModeFlags.Read);
            }
        }

        /// <summary>
        /// List files in a virtual directory.
        /// </summary>
        public string[] ListFiles(string virtualPath, string pattern = "*")
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var mount in _mountPoints)
            {
                if (mount is DirectoryMount dirMount)
                {
                    var mountFiles = dirMount.ListFiles(virtualPath, pattern);
                    foreach (var f in mountFiles)
                        files.Add(f);
                }
            }

            return files.ToArray();
        }

        /// <summary>
        /// List directories in a virtual directory.
        /// </summary>
        public string[] ListDirectories(string virtualPath)
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var mount in _mountPoints)
            {
                if (mount is DirectoryMount dirMount)
                {
                    var mountDirs = dirMount.ListDirectories(virtualPath);
                    foreach (var d in mountDirs)
                        dirs.Add(d);
                }
            }

            return dirs.ToArray();
        }

        /// <summary>
        /// Get all routes available in the VFS.
        /// </summary>
        public RouteInfo[] GetAvailableRoutes()
        {
            var routes = new List<RouteInfo>();

            // Scan common route locations
            string[] searchPaths = { "/routes", "/mods", "/base/routes" };

            foreach (var searchPath in searchPaths)
            {
                var files = ListFiles(searchPath, "*.csv");
                foreach (var file in files)
                {
                    routes.Add(new RouteInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Path = searchPath + "/" + file,
                        Format = "CSV/RW"
                    });
                }

                files = ListFiles(searchPath, "*.rw");
                foreach (var file in files)
                {
                    routes.Add(new RouteInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Path = searchPath + "/" + file,
                        Format = "CSV/RW"
                    });
                }
            }

            return routes.ToArray();
        }

        /// <summary>
        /// Get all trains available in the VFS.
        /// </summary>
        public TrainInfo[] GetAvailableTrains()
        {
            var trains = new List<TrainInfo>();

            string[] searchPaths = { "/trains", "/mods", "/base/trains" };

            foreach (var searchPath in searchPaths)
            {
                var dirs = ListDirectories(searchPath);
                foreach (var dir in dirs)
                {
                    // Check for train.dat or train folder
                    string datPath = searchPath + "/" + dir + "/train.dat";
                    if (Exists(datPath))
                    {
                        trains.Add(new TrainInfo
                        {
                            Name = dir,
                            Path = searchPath + "/" + dir,
                            Format = "OpenBVE"
                        });
                    }
                }
            }

            return trains.ToArray();
        }

        private string NormalizePath(string path)
        {
            // Convert to forward slashes, remove duplicate slashes
            path = path.Replace('\\', '/');
            while (path.Contains("//"))
                path = path.Replace("//", "/");

            // Remove leading/trailing slashes
            path = path.Trim('/');

            // Ensure starts with /
            if (!path.StartsWith("/"))
                path = "/" + path;

            return path;
        }

        public void ClearCache()
        {
            _pathCache.Clear();
        }
    }

    /// <summary>
    /// Abstract mount point.
    /// </summary>
    public abstract class MountPoint
    {
        public string SourcePath { get; protected set; }
        public string VirtualPath { get; protected set; }
        public bool ReadOnly { get; protected set; }
        public int Priority { get; protected set; }

        public abstract bool TryResolve(string virtualPath, out string realPath);
        public abstract string[] ListFiles(string virtualPath, string pattern);
        public abstract string[] ListDirectories(string virtualPath);
    }

    /// <summary>
    /// Directory-based mount point.
    /// </summary>
    public class DirectoryMount : MountPoint
    {
        private string _rootPath;

        public DirectoryMount(string rootPath, string virtualPath, bool readOnly, int priority = 0)
        {
            _rootPath = rootPath;
            SourcePath = rootPath;
            VirtualPath = virtualPath;
            ReadOnly = readOnly;
            Priority = priority;
        }

        public override bool TryResolve(string virtualPath, out string realPath)
        {
            // Check if path is under this mount's virtual path
            if (!virtualPath.StartsWith(VirtualPath.Trim('/') + "/") && virtualPath != VirtualPath.Trim('/'))
            {
                realPath = null;
                return false;
            }

            // Get relative path
            string relPath = virtualPath;
            if (virtualPath.StartsWith(VirtualPath.Trim('/') + "/"))
            {
                relPath = virtualPath.Substring(VirtualPath.Trim('/').Length + 1);
            }
            else if (virtualPath == VirtualPath.Trim('/'))
            {
                relPath = "";
            }

            // Build real path
            realPath = Path.Combine(_rootPath, relPath);

            // Case-insensitive search on case-sensitive filesystems
            if (!File.Exists(realPath) && !Directory.Exists(realPath))
            {
                realPath = FindCaseInsensitive(_rootPath, relPath);
            }

            return File.Exists(realPath) || Directory.Exists(realPath);
        }

        private string FindCaseInsensitive(string basePath, string relPath)
        {
            string[] parts = relPath.Split('/');
            string currentPath = basePath;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                try
                {
                    var dirs = Directory.GetDirectories(currentPath);
                    string match = dirs.FirstOrDefault(d =>
                        Path.GetFileName(d).Equals(part, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        currentPath = match;
                        continue;
                    }

                    var files = Directory.GetFiles(currentPath);
                    match = files.FirstOrDefault(f =>
                        Path.GetFileName(f).Equals(part, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        return match;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return currentPath;
        }

        public override string[] ListFiles(string virtualPath, string pattern)
        {
            if (!TryResolve(virtualPath, out string realPath) || !Directory.Exists(realPath))
                return Array.Empty<string>();

            try
            {
                return Directory.GetFiles(realPath, pattern)
                    .Select(f => VirtualPath.Trim('/') + "/" + Path.GetRelativePath(realPath, f).Replace('\\', '/'))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public override string[] ListDirectories(string virtualPath)
        {
            if (!TryResolve(virtualPath, out string realPath) || !Directory.Exists(realPath))
                return Array.Empty<string>();

            try
            {
                return Directory.GetDirectories(realPath)
                    .Select(d => VirtualPath.Trim('/') + "/" + Path.GetRelativePath(realPath, d).Replace('\\', '/'))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    /// <summary>
    /// Godot resource mount point (res://).
    /// </summary>
    public class ResourceMount : MountPoint
    {
        public ResourceMount(string sourcePath, string virtualPath, bool readOnly, int priority = 0)
        {
            SourcePath = sourcePath;
            VirtualPath = virtualPath;
            ReadOnly = readOnly;
            Priority = priority;
        }

        public override bool TryResolve(string virtualPath, out string realPath)
        {
            if (!virtualPath.StartsWith(VirtualPath.Trim('/') + "/") && virtualPath != VirtualPath.Trim('/'))
            {
                realPath = null;
                return false;
            }

            string relPath = virtualPath;
            if (virtualPath.StartsWith(VirtualPath.Trim('/') + "/"))
            {
                relPath = virtualPath.Substring(VirtualPath.Trim('/').Length + 1);
            }

            realPath = SourcePath + relPath;
            return ResourceLoader.Exists(realPath);
        }

        public override string[] ListFiles(string virtualPath, string pattern)
        {
            // Godot doesn't support directory listing in exported projects easily
            return Array.Empty<string>();
        }

        public override string[] ListDirectories(string virtualPath)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Archive mount point (.zip, .pak).
    /// </summary>
    public class ArchiveMount : MountPoint
    {
        private IArchive _archive;

        public ArchiveMount(string archivePath, string virtualPath, bool readOnly, int priority = 0)
        {
            SourcePath = archivePath;
            VirtualPath = virtualPath;
            ReadOnly = true; // Archives are always read-only
            Priority = priority;

            using var stream = File.OpenRead(archivePath);
            _archive = ArchiveFactory.Open(stream);
        }

        public override bool TryResolve(string virtualPath, out string realPath)
        {
            if (!virtualPath.StartsWith(VirtualPath.Trim('/') + "/"))
            {
                realPath = null;
                return false;
            }

            string relPath = virtualPath.Substring(VirtualPath.Trim('/').Length + 1);
            var entry = _archive.Entries.FirstOrDefault(e =>
                e.Key.Equals(relPath, StringComparison.OrdinalIgnoreCase) && !e.IsDirectory);

            if (entry != null)
            {
                // Extract to temp and return temp path
                string tempPath = Path.Combine(OS.GetUserDataDir(), "tmp", relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));

                // Use the newer SharpCompress API
                entry.WriteToFile(tempPath, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
                realPath = tempPath;
                return true;
            }

            realPath = null;
            return false;
        }

        public override string[] ListFiles(string virtualPath, string pattern)
        {
            string prefix = virtualPath.Trim('/') + "/";
            return _archive.Entries
                .Where(e => e.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !e.IsDirectory)
                .Select(e => VirtualPath + "/" + e.Key)
                .ToArray();
        }

        public override string[] ListDirectories(string virtualPath)
        {
            string prefix = virtualPath.Trim('/') + "/";
            return _archive.Entries
                .Where(e => e.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && e.IsDirectory)
                .Select(e => VirtualPath + "/" + e.Key)
                .Distinct()
                .ToArray();
        }
    }

    public class TrainInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Format { get; set; }
        public string PreviewImage { get; set; }
    }
}