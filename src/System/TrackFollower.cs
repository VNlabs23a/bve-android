using System;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Core track following logic for trains and cameras.
    /// Handles position, direction, cant, curve transitions, and event triggering.
    /// Ported from gt-openbve TrackFollower.
    /// </summary>
    public class TrackFollower
    {
        /// <summary>
        /// Current track position in meters from route start.
        /// </summary>
        public double TrackPosition { get; private set; }

        /// <summary>
        /// Current speed in m/s.
        /// </summary>
        public double Speed { get; private set; }

        /// <summary>
        /// Current acceleration in m/s².
        /// </summary>
        public double Acceleration { get; private set; }

        /// <summary>
        /// Current track being followed.
        /// </summary>
        public Track CurrentTrack { get; private set; }

        /// <summary>
        /// Index of current track in route.
        /// </summary>
        public int CurrentTrackIndex { get; private set; }

        /// <summary>
        /// World position (interpolated).
        /// </summary>
        public Vector3 WorldPosition { get; private set; }

        /// <summary>
        /// World forward direction (interpolated).
        /// </summary>
        public Vector3 WorldDirection { get; private set; }

        /// <summary>
        /// World up vector (interpolated).
        /// </summary>
        public Vector3 WorldUp { get; private set; }

        /// <summary>
        /// World side vector (interpolated).
        /// </summary>
        public Vector3 WorldSide { get; private set; }

        /// <summary>
        /// Current cant (superelevation) in radians.
        /// </summary>
        public double CurrentCant { get; private set; }

        /// <summary>
        /// Current curve radius in meters.
        /// </summary>
        public double CurrentCurveRadius { get; private set; }

        /// <summary>
        /// Current pitch (gradient) in radians.
        /// </summary>
        public double CurrentPitch { get; private set; }

        /// <summary>
        /// Current adhesion factor.
        /// </summary>
        public double CurrentAdhesion { get; private set; }

        /// <summary>
        /// Current speed limit in m/s (-1 = none).
        /// </summary>
        public double CurrentSpeedLimit { get; private set; }

        /// <summary>
        /// Current signal section index.
        /// </summary>
        public int CurrentSignalSection { get; private set; }

        /// <summary>
        /// Odometer - total distance travelled.
        /// </summary>
        public double Odometer { get; private set; }

        /// <summary>
        /// Previous track position (for delta calculation).
        /// </summary>
        private double _previousTrackPosition;

        /// <summary>
        /// Track inaccuracy offset.
        /// </summary>
        private Vector3 _inaccuracyOffset;

        /// <summary>
        /// Cant change rate (for smooth transitions).
        /// </summary>
        private double _cantChangeRate;

        /// <summary>
        /// Whether follower is initialized.
        /// </summary>
        private bool _initialized;

        /// <summary>
        /// Delegate for track position events.
        /// </summary>
        public delegate void TrackEventHandler(double position, TrackEventType type, object data);

        /// <summary>
        /// Event fired when passing track markers.
        /// </summary>
        public event TrackEventHandler OnTrackEvent;

        /// <summary>
        /// Event fired when entering/leaving station.
        /// </summary>
        public event TrackEventHandler OnStationEvent;

        /// <summary>
        /// Initialize follower at a specific track and position.
        /// </summary>
        public void Initialize(Track track, int trackIndex, double position = 0.0)
        {
            CurrentTrack = track;
            CurrentTrackIndex = trackIndex;
            TrackPosition = Math.Max(0.0, Math.Min(track.Length, position));
            _previousTrackPosition = TrackPosition;
            Speed = 0.0;
            Acceleration = 0.0;
            Odometer = 0.0;
            _initialized = true;

            UpdateState();
        }

        /// <summary>
        /// Advance follower by time delta (fixed timestep).
        /// </summary>
        public void Advance(double deltaTime, double acceleration)
        {
            if (!_initialized || CurrentTrack == null)
                return;

            _previousTrackPosition = TrackPosition;
            Acceleration = acceleration;

            // Update speed with acceleration
            Speed += acceleration * deltaTime;
            Speed = Math.Max(0.0, Speed); // No reverse in basic simulation

            // Calculate distance travelled
            double distance = Speed * deltaTime;
            TrackPosition += distance;
            Odometer += distance;

            // Handle track transitions (would switch tracks in full implementation)
            if (TrackPosition > CurrentTrack.Length)
            {
                TrackPosition = CurrentTrack.Length;
                Speed = 0.0;
            }

            UpdateState();

            // Check for events
            CheckEvents();
        }

        /// <summary>
        /// Set position directly (for spawning, rewinding).
        /// </summary>
        public void SetPosition(double position)
        {
            if (CurrentTrack == null)
                return;

            TrackPosition = Math.Max(0.0, Math.Min(CurrentTrack.Length, position));
            _previousTrackPosition = TrackPosition;
            UpdateState();
        }

        /// <summary>
        /// Set speed directly.
        /// </summary>
        public void SetSpeed(double speed)
        {
            Speed = Math.Max(0.0, speed);
        }

        /// <summary>
        /// Emergency stop.
        /// </summary>
        public void EmergencyStop()
        {
            Speed = 0.0;
            Acceleration = -10.0; // High deceleration
        }

        /// <summary>
        /// Update all interpolated state from current track position.
        /// </summary>
        private void UpdateState()
        {
            if (CurrentTrack == null)
                return;

            var elem = CurrentTrack.GetElementAt(TrackPosition);

            WorldPosition = elem.Position + elem.InaccuracyOffset;
            WorldDirection = elem.Direction;
            WorldUp = elem.Up;
            WorldSide = elem.Side;
            CurrentCant = elem.Cant;
            CurrentCurveRadius = elem.CurveRadius;
            CurrentPitch = elem.Pitch;
            CurrentAdhesion = elem.Adhesion;
            CurrentSpeedLimit = elem.SpeedLimit;
            CurrentSignalSection = elem.SignalSectionIndex;
            _inaccuracyOffset = elem.InaccuracyOffset;

            // Calculate cant change rate for physics
            if (Math.Abs(elem.CurveRadius) > 1e-6)
            {
                double cantGradient = elem.Cant / (Math.Abs(elem.CurveRadius) * Math.PI);
                _cantChangeRate = cantGradient * Speed;
            }
            else
            {
                _cantChangeRate = 0.0;
            }
        }

        /// <summary>
        /// Check for track events at current position.
        /// </summary>
        private void CheckEvents()
        {
            if (CurrentTrack == null || CurrentTrack.Elements.Length == 0)
                return;

            int prevIndex = CurrentTrack.GetElementIndexAt(_previousTrackPosition);
            int currIndex = CurrentTrack.GetElementIndexAt(TrackPosition);

            // Check all elements between previous and current position
            int start = Math.Min(prevIndex, currIndex);
            int end = Math.Max(prevIndex, currIndex);

            for (int i = start; i <= end && i < CurrentTrack.Elements.Length; i++)
            {
                var elem = CurrentTrack.Elements[i];

                // Station events
                if (!string.IsNullOrEmpty(elem.StationStop.Name))
                {
                    double stationPos = elem.Distance;
                    if ((_previousTrackPosition <= stationPos && TrackPosition >= stationPos) ||
                        (_previousTrackPosition >= stationPos && TrackPosition <= stationPos))
                    {
                        OnStationEvent?.Invoke(stationPos, TrackEventType.StationEnter, elem.StationStop);
                    }
                }

                // Signal section changes
                if (elem.SignalSectionIndex >= 0 && elem.SignalSectionIndex != CurrentSignalSection)
                {
                    OnTrackEvent?.Invoke(elem.Distance, TrackEventType.SignalSectionChange, elem.SignalSectionIndex);
                }

                // Speed limit changes
                if (elem.SpeedLimit >= 0 && elem.SpeedLimit != CurrentSpeedLimit)
                {
                    OnTrackEvent?.Invoke(elem.Distance, TrackEventType.SpeedLimitChange, elem.SpeedLimit);
                }
            }
        }

        /// <summary>
        /// Get world transform for rendering.
        /// </summary>
        public Transform3D GetWorldTransform()
        {
            var basis = new Basis(
                WorldSide.X, WorldUp.X, -WorldDirection.X,
                WorldSide.Y, WorldUp.Y, -WorldDirection.Y,
                WorldSide.Z, WorldUp.Z, -WorldDirection.Z
            );

            return new Transform3D(basis, WorldPosition);
        }

        /// <summary>
        /// Get transform with cant applied (for cab view).
        /// </summary>
        public Transform3D GetCabTransform()
        {
            var transform = GetWorldTransform();

            // Apply cant rotation around forward axis
            if (Math.Abs(CurrentCant) > 1e-6)
            {
                var cantRotation = Basis.Identity.Rotated(WorldDirection, (float)CurrentCant);
                transform.Basis = cantRotation * transform.Basis;
            }

            return transform;
        }

        /// <summary>
        /// Get lateral acceleration (cant deficiency/excess).
        /// </summary>
        public double GetLateralAcceleration()
        {
            if (Math.Abs(CurrentCurveRadius) < 1e-6)
                return 0.0;

            double v2 = Speed * Speed;
            double r = Math.Abs(CurrentCurveRadius);
            double g = 9.80665;

            // Lateral acceleration = v²/r - g*tan(cant)
            double lateral = v2 / r - g * Math.Tan(CurrentCant);
            return lateral;
        }

        /// <summary>
        /// Get current track element index.
        /// </summary>
        public int GetCurrentElementIndex()
        {
            return CurrentTrack?.GetElementIndexAt(TrackPosition) ?? -1;
        }

        /// <summary>
        /// Distance to next element of specified type.
        /// </summary>
        public double DistanceToNext(TrackElementType type)
        {
            if (CurrentTrack == null)
                return double.MaxValue;

            int startIndex = GetCurrentElementIndex() + 1;
            for (int i = startIndex; i < CurrentTrack.Elements.Length; i++)
            {
                if (CurrentTrack.Elements[i].Type == type)
                {
                    return CurrentTrack.Elements[i].Distance - TrackPosition;
                }
            }
            return double.MaxValue;
        }

        /// <summary>
        /// Check if currently in a station stop zone.
        /// </summary>
        public bool IsInStationZone(out StationStopInfo stationStop)
        {
            stationStop = default;
            if (CurrentTrack == null)
                return false;

            var elem = CurrentTrack.GetElementAt(TrackPosition);
            if (!string.IsNullOrEmpty(elem.StationStop.Name))
            {
                stationStop = elem.StationStop;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Switch to a different track (for junctions, depots).
        /// </summary>
        public bool SwitchTrack(Track newTrack, int newTrackIndex, double position = 0.0)
        {
            if (newTrack == null)
                return false;

            CurrentTrack = newTrack;
            CurrentTrackIndex = newTrackIndex;
            TrackPosition = Math.Max(0.0, Math.Min(newTrack.Length, position));
            _previousTrackPosition = TrackPosition;
            UpdateState();
            return true;
        }

        /// <summary>
        /// Get debug information string.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Pos: {TrackPosition:F1}m | Spd: {Speed * 3.6:F1} km/h | " +
                   $"Acc: {Acceleration:F2} m/s² | Cant: {Calc.ToDegrees(CurrentCant):F1}° | " +
                   $"Curve: {CurrentCurveRadius:F0}m | Pitch: {Calc.ToDegrees(CurrentPitch):F2}° | " +
                   $"Track: {CurrentTrackIndex} | Odo: {Odometer:F0}m";
        }
    }

    /// <summary>
    /// Track event types for event system.
    /// </summary>
    public enum TrackEventType
    {
        SignalSectionChange,
        SpeedLimitChange,
        StationEnter,
        StationLeave,
        Marker,
        GradientChange,
        CurveStart,
        CurveEnd
    }
}