using System;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Represents a single track element (cell) - typically 25m in OpenBVE.
    /// Contains all geometric and physical properties for simulation and rendering.
    /// </summary>
    public struct TrackElement
    {
        /// <summary>
        /// World position of the track element center.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// Forward direction (tangent) - normalized.
        /// Godot uses -Z as forward, OpenBVE uses +Z.
        /// </summary>
        public Vector3 Direction;

        /// <summary>
        /// Up vector (normal) - normalized.
        /// </summary>
        public Vector3 Up;

        /// <summary>
        /// Side vector (binormal) - normalized, perpendicular to Direction and Up.
        /// </summary>
        public Vector3 Side;

        /// <summary>
        /// Curve radius in meters. Positive = right curve, Negative = left curve, 0 = straight.
        /// </summary>
        public double CurveRadius;

        /// <summary>
        /// Cant (superelevation) in radians.
        /// </summary>
        public double Cant;

        /// <summary>
        /// Pitch (gradient) in radians.
        /// </summary>
        public double Pitch;

        /// <summary>
        /// Adhesion factor (0.0 - 1.0), affects braking/acceleration.
        /// </summary>
        public double Adhesion;

        /// <summary>
        /// Rail gauge in meters (standard 1.435m).
        /// </summary>
        public double Gauge;

        /// <summary>
        /// Index of this element in the track array.
        /// </summary>
        public int Index;

        /// <summary>
        /// Distance from route start to this element center (meters).
        /// </summary>
        public double Distance;

        /// <summary>
        /// Track inaccuracy offset (procedural gauge spread/bounce).
        /// </summary>
        public Vector3 InaccuracyOffset;

        /// <summary>
        /// Track element type for rendering.
        /// </summary>
        public TrackElementType Type;

        /// <summary>
        /// Associated signal section index (-1 if none).
        /// </summary>
        public int SignalSectionIndex;

        /// <summary>
        /// Speed limit at this position (m/s, -1 = no limit).
        /// </summary>
        public double SpeedLimit;

        /// <summary>
        /// Station stop information.
        /// </summary>
        public StationStopInfo StationStop;

        /// <summary>
        /// Creates a default straight track element.
        /// </summary>
        public static TrackElement CreateStraight(Vector3 position, Vector3 direction, Vector3 up, double gauge = 1.435)
        {
            var side = Calc.Normalize(Calc.Cross(direction, up));
            var correctedUp = Calc.Normalize(Calc.Cross(side, direction));

            return new TrackElement
            {
                Position = position,
                Direction = direction,
                Up = correctedUp,
                Side = side,
                CurveRadius = 0.0,
                Cant = 0.0,
                Pitch = 0.0,
                Adhesion = 1.0,
                Gauge = gauge,
                Index = -1,
                Distance = 0.0,
                InaccuracyOffset = Vector3.Zero,
                Type = TrackElementType.Straight,
                SignalSectionIndex = -1,
                SpeedLimit = -1.0,
                StationStop = default
            };
        }

        /// <summary>
        /// Creates a curved track element.
        /// </summary>
        public static TrackElement CreateCurved(Vector3 position, Vector3 direction, Vector3 up, double radius, double cant, double gauge = 1.435)
        {
            var side = Calc.Normalize(Calc.Cross(direction, up));
            var correctedUp = Calc.Normalize(Calc.Cross(side, direction));

            return new TrackElement
            {
                Position = position,
                Direction = direction,
                Up = correctedUp,
                Side = side,
                CurveRadius = radius,
                Cant = cant,
                Pitch = 0.0,
                Adhesion = 1.0,
                Gauge = gauge,
                Index = -1,
                Distance = 0.0,
                InaccuracyOffset = Vector3.Zero,
                Type = radius > 0 ? TrackElementType.CurveRight : TrackElementType.CurveLeft,
                SignalSectionIndex = -1,
                SpeedLimit = -1.0,
                StationStop = default
            };
        }

        /// <summary>
        /// Interpolate between two track elements (for smooth transitions).
        /// </summary>
        public static TrackElement Interpolate(ref TrackElement a, ref TrackElement b, double t)
        {
            double invT = 1.0 - t;
            var result = new TrackElement
            {
                Position = a.Position * (float)invT + b.Position * (float)t,
                Direction = Calc.Normalize(a.Direction * (float)invT + b.Direction * (float)t),
                Up = Calc.Normalize(a.Up * (float)invT + b.Up * (float)t),
                Side = Calc.Normalize(a.Side * (float)invT + b.Side * (float)t),
                CurveRadius = Calc.Lerp(a.CurveRadius, b.CurveRadius, t),
                Cant = Calc.Lerp(a.Cant, b.Cant, t),
                Pitch = Calc.Lerp(a.Pitch, b.Pitch, t),
                Adhesion = Calc.Lerp(a.Adhesion, b.Adhesion, t),
                Gauge = Calc.Lerp(a.Gauge, b.Gauge, t),
                Index = -1,
                Distance = Calc.Lerp(a.Distance, b.Distance, t),
                InaccuracyOffset = a.InaccuracyOffset * (float)invT + b.InaccuracyOffset * (float)t,
                Type = t < 0.5 ? a.Type : b.Type,
                SignalSectionIndex = t < 0.5 ? a.SignalSectionIndex : b.SignalSectionIndex,
                SpeedLimit = Calc.Lerp(a.SpeedLimit, b.SpeedLimit, t),
                StationStop = t < 0.5 ? a.StationStop : b.StationStop
            };
            return result;
        }
    }

    /// <summary>
    /// Track element classification for rendering and physics.
    /// </summary>
    public enum TrackElementType
    {
        Straight,
        CurveLeft,
        CurveRight,
        Transition,    // Clothoid/easement curve
        Switch,        // Turnout/points
        Crossing,      // Diamond crossing
        BufferStop,    // End of line
        Gradient       // Significant grade change
    }

    /// <summary>
    /// Station stop information.
    /// </summary>
    public struct StationStopInfo
    {
        public string Name;
        public double StopPosition;      // Track position where train should stop
        public double PlatformLength;    // Platform length in meters
        public bool IsTerminal;          // Terminal station
        public double DwellTime;         // Scheduled dwell time (seconds)
        public int PlatformSide;         // -1 = left, 0 = both, 1 = right
    }
}