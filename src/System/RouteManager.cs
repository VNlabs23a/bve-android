using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Route loading and management.
    /// Supports OpenBVE CSV/RW format, BVE5 TXT, and Mechanik DAT formats.
    /// </summary>
    public class RouteManager
    {
        public Route CurrentRoute { get; private set; }
        public string CurrentRoutePath { get; private set; }

        // Format detectors
        private static readonly string[] CsvExtensions = { ".csv", ".rw" };
        private static readonly string[] Bve5Extensions = { ".txt" };
        private static readonly string[] DatExtensions = { ".dat" };

        public RouteManager()
        {
        }

        /// <summary>
        /// Load a route from file, auto-detecting format.
        /// </summary>
        public Route LoadRoute(string path)
        {
            if (!File.Exists(path))
            {
                GD.PrintErr($"Route file not found: {path}");
                return null;
            }

            CurrentRoutePath = path;
            string ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                Route route = null;

                if (Array.Exists(CsvExtensions, e => e == ext))
                {
                    route = LoadCsvRwRoute(path);
                }
                else if (Array.Exists(Bve5Extensions, e => e == ext))
                {
                    route = LoadBve5Route(path);
                }
                else if (Array.Exists(DatExtensions, e => e == ext))
                {
                    route = LoadDatRoute(path);
                }
                else
                {
                    // Try CSV parser as fallback
                    route = LoadCsvRwRoute(path);
                }

                if (route != null)
                {
                    CurrentRoute = route;
                    GD.Print($"Loaded route: {route.Name} ({route.Tracks.Count} tracks, {route.TotalLength:F0}m)");
                }

                return route;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to load route {path}: {ex.Message}");
                GD.PrintErr(ex.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// Load OpenBVE CSV/RW format route.
        /// </summary>
        private Route LoadCsvRwRoute(string path)
        {
            var route = new Route
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Format = RouteFormat.CsvRw,
                SourcePath = path
            };

            var lines = File.ReadAllLines(path, Encoding.GetEncoding("shift_jis"));
            int lineNumber = 0;

            // Parse sections
            string currentSection = "";
            List<string> trackLines = new();
            Dictionary<string, List<string>> sections = new();

            foreach (var rawLine in lines)
            {
                lineNumber++;
                string line = rawLine.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                // Section header: [SectionName]
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line[1..^1].Trim();
                    if (!sections.ContainsKey(currentSection))
                        sections[currentSection] = new List<string>();
                    continue;
                }

                if (!string.IsNullOrEmpty(currentSection))
                {
                    sections[currentSection].Add(line);
                }
            }

            // Parse Route section (metadata)
            if (sections.TryGetValue("Route", out var routeSection))
            {
                foreach (var line in routeSection)
                {
                    ParseKeyValue(line, (key, value) =>
                    {
                        switch (key.ToLowerInvariant())
                        {
                            case "name": route.Name = value; break;
                            case "comment": route.Comment = value; break;
                            case "image": route.LoadingImage = value; break;
                            case "trainfolder": route.TrainFolder = value; break;
                            case "objectfolder": route.ObjectFolder = value; break;
                            case "soundfolder": route.SoundFolder = value; break;
                            case "structurefolder": route.StructureFolder = value; break;
                        }
                    });
                }
            }

            // Parse Track section
            if (sections.TryGetValue("Track", out var trackSection))
            {
                route.Tracks = ParseTrackData(trackSection, route);
            }

            // Parse Train section (consist)
            if (sections.TryGetValue("Train", out var trainSection))
            {
                route.TrainConsist = ParseTrainConsist(trainSection);
            }

            // Parse Signals
            if (sections.TryGetValue("Signal", out var signalSection))
            {
                route.Signals = ParseSignals(signalSection);
            }

            // Parse Structures/Objects
            if (sections.TryGetValue("Structure", out var structSection))
            {
                route.Structures = ParseStructures(structSection, route);
            }

            route.CalculateDerivedData();
            return route;
        }

        private List<Track> ParseTrackData(List<string> lines, Route route)
        {
            var tracks = new List<Track>();
            var currentTrackElements = new List<TrackElement>();
            int trackIndex = 0;

            // Default track 0
            currentTrackElements.Add(TrackElement.CreateStraight(Vector3.Zero, -Vector3.Forward, Vector3.Up));

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 4) continue;

                // Track commands: TrackIndex, RailType, CurveRadius, Cant
                // Simplified parser - real implementation would be much more complex
                if (int.TryParse(parts[0].Trim(), out int idx))
                {
                    if (idx != trackIndex)
                    {
                        // Finalize previous track
                        if (currentTrackElements.Count > 0)
                        {
                            tracks.Add(new Track(currentTrackElements.ToArray(), 1.435, 4, trackIndex, $"Track {trackIndex}"));
                        }
                        trackIndex = idx;
                        currentTrackElements.Clear();
                    }
                }

                // Parse track element data (simplified)
                // Real parser handles: Curve, Cant, Pitch, Adhesion, RailStart/End, etc.
                double curveRadius = 0;
                double cant = 0;
                double pitch = 0;

                if (parts.Length > 2 && double.TryParse(parts[2].Trim(), out double cr))
                    curveRadius = cr;
                if (parts.Length > 3 && double.TryParse(parts[3].Trim(), out double c))
                    cant = c * Calc.DegToRad;

                var lastPos = currentTrackElements.Count > 0
                    ? currentTrackElements[^1].Position
                    : Vector3.Zero;
                var lastDir = currentTrackElements.Count > 0
                    ? currentTrackElements[^1].Direction
                    : -Vector3.Forward;
                var lastUp = currentTrackElements.Count > 0
                    ? currentTrackElements[^1].Up
                    : Vector3.Up;

                // Generate next element position (simplified 25m steps)
                Vector3 nextPos = lastPos + lastDir * 25.0f;
                Vector3 nextDir = lastDir;
                Vector3 nextUp = lastUp;

                if (Math.Abs(curveRadius) > 1e-6)
                {
                    // Curved track - rotate direction
                    double angle = 25.0 / Math.Abs(curveRadius);
                    Vector3 axis = curveRadius > 0 ? lastUp : -lastUp;
                    nextDir = Calc.Rotate(lastDir, axis, angle);
                    nextUp = Calc.Rotate(lastUp, axis, angle);
                }

                var elem = TrackElement.CreateCurved(nextPos, nextDir, nextUp, curveRadius, cant);
                currentTrackElements.Add(elem);
            }

            // Add final track
            if (currentTrackElements.Count > 0)
            {
                tracks.Add(new Track(currentTrackElements.ToArray(), 1.435, 4, trackIndex, $"Track {trackIndex}"));
            }

            return tracks;
        }

        private List<TrainCar> ParseTrainConsist(List<string> lines)
        {
            var consist = new List<TrainCar>();
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    consist.Add(new TrainCar
                    {
                        TrainPath = parts[0].Trim(),
                        CarCount = int.TryParse(parts[1].Trim(), out int c) ? c : 1
                    });
                }
            }
            return consist;
        }

        private List<Signal> ParseSignals(List<string> lines)
        {
            var signals = new List<Signal>();
            // Simplified - real parser handles .Signal, .Section, .Transponder
            return signals;
        }

        private List<Structure> ParseStructures(List<string> lines, Route route)
        {
            var structures = new List<Structure>();
            // Simplified - real parser handles .FreeObj, .Rail, .Wall, .Dike, .Form, etc.
            return structures;
        }

        private Route LoadBve5Route(string path)
        {
            // BVE5 format parser (simplified)
            var route = new Route
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Format = RouteFormat.Bve5,
                SourcePath = path
            };
            // TODO: Implement BVE5 parsing
            return route;
        }

        private Route LoadDatRoute(string path)
        {
            // Mechanik DAT format parser
            var route = new Route
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Format = RouteFormat.MechanikDat,
                SourcePath = path
            };
            // TODO: Implement DAT parsing
            return route;
        }

        private void ParseKeyValue(string line, Action<string, string> callback)
        {
            int eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                string key = line[..eqIndex].Trim();
                string value = line[(eqIndex + 1)..].Trim().Trim('"');
                callback(key, value);
            }
        }

        /// <summary>
        /// Get available routes from mods directory.
        /// </summary>
        public List<RouteInfo> GetAvailableRoutes()
        {
            var routes = new List<RouteInfo>();
            // TODO: Scan VirtualFS for route files
            return routes;
        }
    }

    /// <summary>
    /// Route data container.
    /// </summary>
    public class Route
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public string LoadingImage { get; set; }
        public string TrainFolder { get; set; }
        public string ObjectFolder { get; set; }
        public string SoundFolder { get; set; }
        public string StructureFolder { get; set; }
        public RouteFormat Format { get; set; }
        public string SourcePath { get; set; }

        public List<Track> Tracks { get; set; } = new();
        public List<TrainCar> TrainConsist { get; set; } = new();
        public List<Signal> Signals { get; set; } = new();
        public List<Structure> Structures { get; set; } = new();
        public List<Station> Stations { get; set; } = new();
        public List<PointOfInterest> POIs { get; set; } = new();

        public double TotalLength { get; private set; }
        public int TotalTrackCount => Tracks.Count;

        public void CalculateDerivedData()
        {
            TotalLength = 0;
            foreach (var track in Tracks)
            {
                TotalLength += track.Length;
            }
        }
    }

    public enum RouteFormat
    {
        CsvRw,
        Bve5,
        MechanikDat,
        Unknown
    }

    public class TrainCar
    {
        public string TrainPath { get; set; }
        public int CarCount { get; set; } = 1;
    }

    public class Signal
    {
        public int Section { get; set; }
        public int Aspect { get; set; }
        public double Position { get; set; }
        public int TrackIndex { get; set; }
    }

    public class Structure
    {
        public string ObjectPath { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public int TrackIndex { get; set; }
        public double TrackPosition { get; set; }
        public StructureType Type { get; set; }
    }

    public enum StructureType
    {
        FreeObject,
        Rail,
        Wall,
        Dike,
        Form,
        Pole,
        Crack,
        Sound
    }

    public class Station
    {
        public string Name { get; set; }
        public double TrackPosition { get; set; }
        public int TrackIndex { get; set; }
        public double PlatformLength { get; set; }
        public bool IsTerminal { get; set; }
        public int PlatformSide { get; set; } // -1 left, 0 both, 1 right
    }

    public class PointOfInterest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Vector3 Position { get; set; }
        public int TrackIndex { get; set; }
        public double TrackPosition { get; set; }
    }

    public class RouteInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Format { get; set; }
        public double Length { get; set; }
        public string PreviewImage { get; set; }
    }
}