using System;
using Godot;

namespace OpenBVEAndroid.UI
{
    /// <summary>
    /// Heads-Up Display showing speed, notch, signals, pressures, timetable.
    /// </summary>
    public partial class HUD : Control
    {
        // UI Elements
        private Label _routeName;
        private Label _nextStation;
        private Label _speedValue;
        private Label _signalSpeedLimit;
        private TextureRect _signalAspect;
        private Label _notchValue;
        private Label _brakeValue;
        private Label _reverserValue;
        private Label _scheduleTime;
        private Label _delayDisplay;
        private Label _brakePipeValue;
        private Label _mainResValue;
        private Label _brakeCylValue;
        private Label _debugDisplay;

        // References
        private Simulation _simulation;
        private TrainPhysics _trainPhysics;
        private TrackFollower _follower;

        public override void _Ready()
        {
            _simulation = Simulation.Instance;
            _trainPhysics = _simulation?.TrainPhysics;
            _follower = _simulation?.PlayerFollower;

            // Get nodes
            _routeName = GetNode<Label>("TopBar/RouteInfo/RouteName");
            _nextStation = GetNode<Label>("TopBar/RouteInfo/NextStation");
            _speedValue = GetNode<Label>("TopBar/SpeedDisplay/SpeedValue");
            _signalSpeedLimit = GetNode<Label>("TopBar/SignalDisplay/SpeedLimit");
            _signalAspect = GetNode<TextureRect>("TopBar/SignalDisplay/SignalAspect");
            _notchValue = GetNode<Label>("BottomBar/LeftPanel/NotchDisplay/NotchValue");
            _brakeValue = GetNode<Label>("BottomBar/LeftPanel/BrakeDisplay/BrakeValue");
            _reverserValue = GetNode<Label>("BottomBar/LeftPanel/ReverserDisplay/ReverserValue");
            _scheduleTime = GetNode<Label>("BottomBar/CenterPanel/TimetableDisplay/ScheduleTime");
            _delayDisplay = GetNode<Label>("BottomBar/CenterPanel/TimetableDisplay/DelayDisplay");
            _brakePipeValue = GetNode<Label>("BottomBar/RightPanel/PressureDisplay/BrakePipeValue");
            _mainResValue = GetNode<Label>("BottomBar/RightPanel/PressureDisplay/MainResValue");
            _brakeCylValue = GetNode<Label>("BottomBar/RightPanel/PressureDisplay/BrakeCylValue");
            _debugDisplay = GetNode<Label>("BottomBar/RightPanel/DebugDisplay");
        }

        public override void _Process(double delta)
        {
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            if (_simulation == null || _trainPhysics == null || _follower == null)
                return;

            // Speed
            float speedKmh = _simulation.GetSpeedKmh();
            _speedValue.Text = $"{speedKmh:F0}";

            // Color code speed based on limit
            double limit = _follower.CurrentSpeedLimit;
            if (limit > 0)
            {
                float limitKmh = (float)(limit * 3.6);
                _signalSpeedLimit.Text = $"{limitKmh:F0}";

                if (speedKmh > limitKmh * 1.05f)
                    _speedValue.Modulate = Colors.Red;
                else if (speedKmh > limitKmh * 0.95f)
                    _speedValue.Modulate = Colors.Yellow;
                else
                    _speedValue.Modulate = Colors.White;
            }
            else
            {
                _signalSpeedLimit.Text = "---";
                _speedValue.Modulate = Colors.White;
            }

            // Notch
            _notchValue.Text = $"{_simulation.GetNotch()}";
            _notchValue.Modulate = _simulation.GetNotch() > 0 ? Colors.LightGreen :
                                   _simulation.GetNotch() < 0 ? Colors.LightBlue : Colors.White;

            // Brake notch
            _brakeValue.Text = $"{_trainPhysics.GetBrakeNotch()}";

            // Reverser
            _reverserValue.Text = InputManager.Instance.GetReverserText();

            // Pressures
            _brakePipeValue.Text = $"{_trainPhysics.GetBrakePipePressureKpa() / 100.0:F1}";
            _mainResValue.Text = $"{_trainPhysics.GetMainReservoirPressureKpa() / 100.0:F1}";
            _brakeCylValue.Text = $"{_trainPhysics.GetBrakeCylinderPressureKpa() / 100.0:F1}";

            // Next station
            string nextStation = _simulation.GetNextStation();
            double distToStation = _simulation.GetDistanceToStation();
            if (distToStation > 0)
            {
                _nextStation.Text = $"Next: {nextStation} ({distToStation:F0}m)";
            }
            else
            {
                _nextStation.Text = $"At: {nextStation}";
            }

            // Timetable (placeholder)
            TimeSpan simTime = TimeSpan.FromSeconds(_simulation.SimulationTime);
            _scheduleTime.Text = simTime.ToString(@"hh\:mm\:ss");
            _delayDisplay.Text = "+0:00"; // Would calculate from schedule

            // Debug
            if (OS.IsDebugBuild())
            {
                _debugDisplay.Text = _simulation.GetDebugInfo();
            }
            else
            {
                _debugDisplay.Text = "";
            }
        }

        public void SetRouteName(string name)
        {
            if (_routeName != null)
                _routeName.Text = name;
        }

        public void SetSignalAspect(int aspect)
        {
            // Update signal aspect texture
            // 0=Red, 1=Yellow, 2=Green, 3=Double Yellow, etc.
        }
    }
}