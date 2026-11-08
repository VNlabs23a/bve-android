using System;
using System.Collections.Generic;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Main simulation engine - runs at fixed 60Hz timestep.
    /// Manages train physics, route loading, and coordinates all subsystems.
    /// </summary>
    [GlobalClass]
    public partial class Simulation : Node
    {
        public const double FixedTimestep = 1.0 / 60.0; // 60 Hz
        public const int MaxSubSteps = 5;

        // Singleton instance
        public static Simulation Instance { get; private set; }

        // State
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public double SimulationTime { get; private set; }
        public double FixedTimeAccumulator { get; private set; }

        // Current route and train
        public RouteManager RouteManager { get; private set; }
        public TrainPhysics TrainPhysics { get; private set; }
        public TrackFollower PlayerFollower { get; private set; }

        // Camera
        public Camera3D MainCamera { get; private set; }
        public CameraMode CameraMode { get; set; } = CameraMode.Cab;

        // Events
        public event Action<double> OnSimulationStep;
        public event Action OnRouteLoaded;
        public event Action OnTrainLoaded;
        public event Action<string> OnError;

        // Input state
        private double _throttleInput = 0.0;      // -1.0 to 1.0
        private bool _emergencyBrake = false;
        private bool _hornActive = false;
        private int _reverserPosition = 1;        // -1, 0, 1
        private int _lightState = 0;              // 0=off, 1=low, 2=high

        public override void _EnterTree()
        {
            Instance = this;
            RouteManager = new RouteManager();
            TrainPhysics = new TrainPhysics();
            PlayerFollower = new TrackFollower();

            // Subscribe to follower events
            PlayerFollower.OnTrackEvent += HandleTrackEvent;
            PlayerFollower.OnStationEvent += HandleStationEvent;
        }

        public override void _ExitTree()
        {
            Instance = null;
            PlayerFollower.OnTrackEvent -= HandleTrackEvent;
            PlayerFollower.OnStationEvent -= HandleStationEvent;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsRunning || IsPaused)
                return;

            SimulationTime += delta;
            FixedTimeAccumulator += delta;

            // Fixed timestep loop
            int steps = 0;
            while (FixedTimeAccumulator >= FixedTimestep && steps < MaxSubSteps)
            {
                StepSimulation(FixedTimestep);
                FixedTimeAccumulator -= FixedTimestep;
                steps++;
            }

            // Interpolation factor for rendering
            float interpolationAlpha = (float)(FixedTimeAccumulator / FixedTimestep);
            InterpolateRenderState(interpolationAlpha);
        }

        private void StepSimulation(double deltaTime)
        {
            // Get acceleration from train physics
            double acceleration = TrainPhysics.CalculateAcceleration(
                _throttleInput,
                _emergencyBrake,
                _reverserPosition,
                PlayerFollower.CurrentAdhesion,
                PlayerFollower.GetLateralAcceleration(),
                PlayerFollower.CurrentSpeedLimit
            );

            // Advance follower
            PlayerFollower.Advance(deltaTime, acceleration);

            // Update train physics internal state
            TrainPhysics.Update(deltaTime, PlayerFollower.Speed, acceleration);

            // Update camera
            UpdateCamera(deltaTime);

            // Fire step event
            OnSimulationStep?.Invoke(deltaTime);
        }

        private void InterpolateRenderState(float alpha)
        {
            // Interpolate camera position for smooth rendering
            if (MainCamera != null && PlayerFollower != null)
            {
                // Camera interpolation handled in UpdateCamera
            }
        }

        private void UpdateCamera(double deltaTime)
        {
            if (MainCamera == null || PlayerFollower == null)
                return;

            Transform3D targetTransform = CameraMode switch
            {
                CameraMode.Cab => PlayerFollower.GetCabTransform(),
                CameraMode.Chase => GetChaseCameraTransform(),
                CameraMode.Free => MainCamera.GlobalTransform, // Controlled by input
                _ => PlayerFollower.GetWorldTransform()
            };

            // Smooth camera transition
            MainCamera.GlobalTransform = MainCamera.GlobalTransform.InterpolateWith(targetTransform, (float)(deltaTime * 10.0));
        }

        private Transform3D GetChaseCameraTransform()
        {
            var followerTransform = PlayerFollower.GetWorldTransform();
            Vector3 back = -followerTransform.Basis.Z;
            Vector3 up = followerTransform.Basis.Y;

            Vector3 cameraPos = followerTransform.Origin + back * 15.0f + up * 5.0f;
            Vector3 targetPos = followerTransform.Origin + up * 2.0f;

            var basis = Basis.LookingAt((targetPos - cameraPos).Normalized(), up);
            return new Transform3D(basis, cameraPos);
        }

        /// <summary>
        /// Start simulation with a route and train.
        /// </summary>
        public void StartSimulation(string routePath, string trainPath = "")
        {
            try
            {
                // Load route
                var route = RouteManager.LoadRoute(routePath);
                if (route == null)
                {
                    OnError?.Invoke($"Failed to load route: {routePath}");
                    return;
                }

                // Initialize follower on first track
                if (route.Tracks.Count > 0)
                {
                    PlayerFollower.Initialize(route.Tracks[0], 0, 0.0);
                }

                // Load train (placeholder)
                if (!string.IsNullOrEmpty(trainPath))
                {
                    TrainPhysics.LoadTrain(trainPath);
                }

                IsRunning = true;
                IsPaused = false;
                SimulationTime = 0.0;
                FixedTimeAccumulator = 0.0;

                OnRouteLoaded?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Simulation start error: {ex.Message}");
                GD.PrintErr(ex);
            }
        }

        /// <summary>
        /// Pause/resume simulation.
        /// </summary>
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        /// <summary>
        /// Stop simulation completely.
        /// </summary>
        public void StopSimulation()
        {
            IsRunning = false;
            IsPaused = false;
        }

        /// <summary>
        /// Set main camera reference.
        /// </summary>
        public void SetCamera(Camera3D camera)
        {
            MainCamera = camera;
        }

        // Input handlers
        public void SetThrottle(double value)
        {
            _throttleInput = Math.Clamp(value, -1.0, 1.0);
        }

        public void SetEmergencyBrake(bool active)
        {
            _emergencyBrake = active;
            if (active)
            {
                PlayerFollower.EmergencyStop();
            }
        }

        public void SetHorn(bool active)
        {
            _hornActive = active;
        }

        public void SetReverser(int position)
        {
            _reverserPosition = Math.Clamp(position, -1, 1);
        }

        public void CycleLights()
        {
            _lightState = (_lightState + 1) % 3;
        }

        public void CycleCamera()
        {
            CameraMode = (CameraMode)(((int)CameraMode + 1) % 3);
        }

        // Getters for UI
        public double GetSpeedKmh() => PlayerFollower?.Speed * 3.6 ?? 0.0;
        public int GetNotch() => (int)Math.Round(_throttleInput * 8.0); // Assuming 8 notches
        public string GetNextStation() => PlayerFollower.IsInStationZone(out var stop) ? stop.Name : "---";
        public double GetDistanceToStation() => PlayerFollower.IsInStationZone(out var stop) ? stop.StopPosition - PlayerFollower.TrackPosition : -1;
        public string GetDebugInfo() => PlayerFollower?.GetDebugInfo() ?? "Not initialized";

        private void HandleTrackEvent(double position, TrackEventType type, object data)
        {
            GD.Print($"Track event at {position:F1}m: {type} = {data}");
        }

        private void HandleStationEvent(double position, TrackEventType type, object data)
        {
            if (data is StationStopInfo stop)
            {
                GD.Print($"Station event: {stop.Name} at {position:F1}m");
            }
        }
    }

    /// <summary>
    /// Camera view modes.
    /// </summary>
    public enum CameraMode
    {
        Cab,      // First person from driver's seat
        Chase,    // Third person behind train
        Free      // Free flight camera
    }
}