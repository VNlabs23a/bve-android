using System;
using Godot;
using OpenBVEAndroid.System;
using OpenBVEAndroid.Rendering;

namespace OpenBVEAndroid.Android
{
    /// <summary>
    /// Android lifecycle management - handles pause/resume, low memory, configuration changes.
    /// </summary>
    [GlobalClass]
    public partial class LifecycleManager : Node
    {
        public static LifecycleManager Instance { get; private set; }

        public bool IsPaused { get; private set; } = false;
        public bool IsBackgrounded { get; private set; } = false;

        // State saving
        private bool _needsStateSave = false;

        // Events
        public event Action OnPause;
        public event Action OnResume;
        public event Action OnLowMemory;
        public event Action ConfigurationChanged; // Renamed to avoid conflict with method
        public event Action OnSaveState;
        public event Action OnRestoreState;

        // Godot 4.x notification codes (from MainLoop)
        private const long NotificationApplicationFocusIn = 1002;
        private const long NotificationApplicationFocusOut = 1003;
        private const long NotificationWMCloseRequest = 1004;
        private const long NotificationWMGoBackRequest = 1005;
        private const long NotificationMemoryWarning = 1006;
        private const long NotificationTranslationChanged = 1007;

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        public override void _Notification(int what)
        {
            switch (what)
            {
                case (int)NotificationApplicationFocusIn:
                    OnFocusGained();
                    break;
                case (int)NotificationApplicationFocusOut:
                    OnFocusLost();
                    break;
                case (int)NotificationWMCloseRequest:
                    OnQuitRequest();
                    break;
                case (int)NotificationWMGoBackRequest:
                    OnBackPressed();
                    break;
                case (int)NotificationMemoryWarning:
                    OnMemoryWarning();
                    break;
                case (int)NotificationTranslationChanged:
                    OnTranslationChanged();
                    break;
            }
        }

        private void OnFocusGained()
        {
            if (IsPaused || IsBackgrounded)
            {
                IsPaused = false;
                IsBackgrounded = false;
                GD.Print("LifecycleManager: App resumed");

                // Re-acquire resources
                AndroidBridge.Instance?.AcquireWakeLock();
                AndroidBridge.Instance?.RequestAudioFocus();

                // Restore simulation
                var sim = Simulation.Instance;
                if (sim != null && sim.IsRunning)
                {
                    sim.SetPaused(false);
                }

                OnResume?.Invoke();
            }
        }

        private void OnFocusLost()
        {
            if (!IsPaused && !IsBackgrounded)
            {
                IsPaused = true;
                IsBackgrounded = true;
                GD.Print("LifecycleManager: App paused/backgrounded");

                // Save state
                _needsStateSave = true;
                OnSaveState?.Invoke();

                // Pause simulation
                var sim = Simulation.Instance;
                if (sim != null && sim.IsRunning)
                {
                    sim.SetPaused(true);
                }

                // Release resources
                AndroidBridge.Instance?.ReleaseWakeLock();
                AndroidBridge.Instance?.AbandonAudioFocus();

                OnPause?.Invoke();
            }
        }

        private void OnMemoryWarning()
        {
            GD.Print("LifecycleManager: Low memory warning");

            // Flush caches
            var vfs = VirtualFS.Instance;
            vfs?.ClearCache();

            // Reduce quality if needed
            var quality = QualityManager.Instance;
            if (quality != null && quality.CurrentPreset > QualityManager.QualityPreset.Low)
            {
                quality.ApplyQualityPreset(quality.CurrentPreset - 1);
            }

            // Clear texture cache (Godot 4.x handles this automatically)
            // RenderingServer.FreeUnusedResources();

            OnLowMemory?.Invoke();
        }

        private void OnQuitRequest()
        {
            GD.Print("LifecycleManager: Quit requested");

            // Save state before quit
            OnSaveState?.Invoke();

            // Stop simulation
            var sim = Simulation.Instance;
            if (sim != null)
            {
                sim.StopSimulation();
            }

            // Allow quit
            GetTree().Quit();
        }

        private void OnBackPressed()
        {
            GD.Print("LifecycleManager: Back pressed");

            var sim = Simulation.Instance;
            if (sim != null && sim.IsRunning)
            {
                // Pause simulation instead of quitting
                sim.SetPaused(!sim.IsPaused);
            }
            else
            {
                // Quit if not in simulation
                GetTree().Quit();
            }
        }

        private void OnTranslationChanged()
        {
            // Handle language change
            GD.Print("LifecycleManager: Language changed");
        }

        /// <summary>
        /// Called from Android activity onConfigurationChanged.
        /// </summary>
        public void OnConfigurationChanged()
        {
            GD.Print("LifecycleManager: Configuration changed (rotation, keyboard, etc.)");

            // Recreate UI if needed
            ConfigurationChanged?.Invoke();
        }

        /// <summary>
        /// Called from Android activity onSaveInstanceState.
        /// </summary>
        public void OnSaveInstanceState(Godot.Collections.Dictionary bundle)
        {
            GD.Print("LifecycleManager: Saving instance state");

            var sim = Simulation.Instance;
            if (sim != null)
            {
                bundle["simulation_running"] = sim.IsRunning;
                bundle["simulation_time"] = sim.SimulationTime;
                bundle["route_path"] = sim.RouteManager?.CurrentRoutePath ?? "";
                bundle["camera_mode"] = (int)sim.CameraViewMode;
            }

            var input = InputManager.Instance;
            if (input != null)
            {
                bundle["throttle"] = input.ThrottleAxis;
                bundle["reverser"] = input.ReverserPosition;
                bundle["lights"] = input.LightState;
            }

            OnSaveState?.Invoke();
        }

        /// <summary>
        /// Called from Android activity onRestoreInstanceState.
        /// </summary>
        public void OnRestoreInstanceState(Godot.Collections.Dictionary bundle)
        {
            GD.Print("LifecycleManager: Restoring instance state");

            if (bundle.ContainsKey("simulation_running") && (bool)bundle["simulation_running"])
            {
                string routePath = bundle.ContainsKey("route_path") ? (string)bundle["route_path"] : "";
                if (!string.IsNullOrEmpty(routePath))
                {
                    // Restore will happen after scene is ready
                    _needsStateSave = false;
                    CallDeferred(nameof(RestoreSimulation), bundle);
                }
            }

            OnRestoreState?.Invoke();
        }

        private void RestoreSimulation(Godot.Collections.Dictionary bundle)
        {
            var sim = Simulation.Instance;
            if (sim == null) return;

            string routePath = (string)bundle["route_path"];
            double simTime = bundle.ContainsKey("simulation_time") ? (double)bundle["simulation_time"] : 0;
            int cameraMode = bundle.ContainsKey("camera_mode") ? (int)bundle["camera_mode"] : 0;

            sim.StartSimulation(routePath);
            sim.SimulationTime = simTime;
            sim.CameraViewMode = (CameraViewMode)cameraMode;

            // Restore input
            var input = InputManager.Instance;
            if (input != null)
            {
                input.ThrottleAxis = bundle.ContainsKey("throttle") ? (float)bundle["throttle"] : 0;
                input.ReverserPosition = bundle.ContainsKey("reverser") ? (int)bundle["reverser"] : 1;
                input.LightState = bundle.ContainsKey("lights") ? (int)bundle["lights"] : 0;
            }
        }

        public bool NeedsStateSave()
        {
            return _needsStateSave;
        }

        public void MarkStateSaved()
        {
            _needsStateSave = false;
        }
    }
}