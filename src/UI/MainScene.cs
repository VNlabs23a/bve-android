using System;
using System.IO;
using Godot;
using OpenBVEAndroid.System;
using OpenBVEAndroid.Rendering;
using OpenBVEAndroid.Android;

namespace OpenBVEAndroid.UI
{
    /// <summary>
    /// Main scene controller - coordinates all systems.
    /// </summary>
    public partial class MainScene : Node3D
    {
        // Child nodes
        private Camera3D _camera;
        private TrackRenderer _trackRenderer;
        private TrainRenderer _trainRenderer;
        private TouchControls _touchControls;
        private HUD _hud;

        // Systems
        private Simulation _simulation;
        private InputManager _inputManager;
        private QualityManager _qualityManager;
        private PerformanceMonitor _perfMonitor;
        private ModManager _modManager;
        private VirtualFS _vfs;
        private AndroidBridge _androidBridge;
        private StorageManager _storageManager;
        private LifecycleManager _lifecycleManager;

        // State
        private bool _systemsInitialized = false;

        public override void _Ready()
        {
            // Get child nodes
            _camera = GetNode<Camera3D>("Camera3D");
            _trackRenderer = GetNode<Node3D>("TrackRenderer") as TrackRenderer;
            _trainRenderer = GetNode<Node3D>("TrainRenderer") as TrainRenderer;
            _touchControls = GetNode<TouchControls>("TouchControls");
            _hud = GetNode<HUD>("HUD");

            // Get autoload systems
            _simulation = Simulation.Instance;
            _inputManager = InputManager.Instance;
            _qualityManager = QualityManager.Instance;
            _perfMonitor = PerformanceMonitor.Instance;
            _modManager = ModManager.Instance;
            _vfs = VirtualFS.Instance;
            _androidBridge = AndroidBridge.Instance;
            _storageManager = StorageManager.Instance;
            _lifecycleManager = LifecycleManager.Instance;

            InitializeSystems();
        }

        private void InitializeSystems()
        {
            if (_systemsInitialized) return;

            // Set camera for simulation
            _simulation?.SetCamera(_camera);

            // Set camera for LOD manager
            LODManager.Instance?.SetCamera(_camera);

            // Set quality preset from device detection
            if (_androidBridge != null)
            {
                var suggested = _androidBridge.SuggestQualityPreset();
                _qualityManager.ApplyQualityPreset(suggested);
            }

            // Auto-suggest quality on mobile
            if (OS.GetName() == "Android")
            {
                _qualityManager.AutoQuality = true;
            }

            // Connect lifecycle events
            _lifecycleManager.OnPause += OnAppPause;
            _lifecycleManager.OnResume += OnAppResume;
            _lifecycleManager.OnLowMemory += OnLowMemory;

            // Connect mod manager events
            _modManager.OnError += OnModError;

            // Connect performance monitor
            _perfMonitor.OnFrameTimeUpdate += OnFrameTimeUpdate;

            _systemsInitialized = true;

            GD.Print("MainScene: Systems initialized");

            // Auto-start demo if route available
            // StartDemoRoute();
        }

        public override void _Process(double delta)
        {
            // Update train renderer from simulation
            if (_simulation != null && _simulation.IsRunning && !_simulation.IsPaused)
            {
                _trainRenderer?.UpdateTrain(_simulation.PlayerFollower, delta);
            }

            // Handle pause input
            if (_inputManager.PausePressed)
            {
                _simulation?.SetPaused(!_simulation.IsPaused);
            }

            // Handle camera cycle
            if (_inputManager.CameraCyclePressed)
            {
                _simulation?.CycleCamera();
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            // Pass input to simulation
            if (_simulation != null && _simulation.IsRunning && !_simulation.IsPaused)
            {
                _simulation.SetThrottle(_inputManager.ThrottleAxis);
                _simulation.SetEmergencyBrake(_inputManager.EmergencyBrake);
                _simulation.SetHorn(_inputManager.HornPressed);
                _simulation.SetReverser(_inputManager.ReverserPosition);

                // Lights cycle handled by input manager
            }
        }

        /// <summary>
        /// Start a demo route for testing.
        /// </summary>
        public void StartDemoRoute()
        {
            // Look for a route in mods
            var routes = _modManager.GetEnabledRoutes();
            if (routes.Length > 0)
            {
                _simulation.StartSimulation(routes[0].VirtualPath);
            }
            else
            {
                // Create a simple test route
                CreateTestRoute();
            }
        }

        private void CreateTestRoute()
        {
            // Create a simple oval track for testing
            var elements = new TrackElement[]
            {
                TrackElement.CreateStraight(Vector3.Zero, -Vector3.Forward, Vector3.Up),
                TrackElement.CreateStraight(new Vector3(0, 0, -100), -Vector3.Forward, Vector3.Up)
            };
            var track = new Track(elements);

            // This would be populated by RouteManager in real usage
            // For now, just start simulation with empty route
            _simulation.StartSimulation("");
        }

        /// <summary>
        /// Load a specific route.
        /// </summary>
        public void LoadRoute(string virtualPath)
        {
            _simulation?.StartSimulation(virtualPath);

            // Update HUD route name
            var routeName = Path.GetFileNameWithoutExtension(virtualPath);
            _hud?.SetRouteName(routeName);
        }

        /// <summary>
        /// Load a train.
        /// </summary>
        public void LoadTrain(string virtualPath)
        {
            // Train loading handled by simulation
        }

        // Event handlers
        private void OnAppPause()
        {
            GD.Print("MainScene: App paused");
            _touchControls?.SetVisible(false);
        }

        private void OnAppResume()
        {
            GD.Print("MainScene: App resumed");
            _touchControls?.SetVisible(true);
        }

        private void OnLowMemory()
        {
            GD.Print("MainScene: Low memory - reducing quality");
            _qualityManager.ApplyQualityPreset(QualityManager.QualityPreset.Low);
        }

        private void OnModError(string error)
        {
            GD.PrintErr($"Mod Error: {error}");
            _androidBridge?.ShowToast($"Mod Error: {error}");
        }

        private void OnFrameTimeUpdate(float avgFrameTime)
        {
            // Could update performance HUD here
        }

        /// <summary>
        /// Show settings menu.
        /// </summary>
        public void ShowSettings()
        {
            // TODO: Instantiate settings menu scene
        }

        /// <summary>
        /// Show mod browser.
        /// </summary>
        public void ShowModBrowser()
        {
            // TODO: Instantiate mod browser scene
        }

        /// <summary>
        /// Quit application.
        /// </summary>
        public void Quit()
        {
            _simulation?.StopSimulation();
            GetTree().Quit();
        }
    }
}