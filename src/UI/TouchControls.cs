using System;
using Godot;
using OpenBVEAndroid.System;

namespace OpenBVEAndroid.UI
{
    /// <summary>
    /// Touch control UI - handles visual feedback for touch inputs.
    /// </summary>
    public partial class TouchControls : Control
    {
        // UI Elements
        private TextureRect _throttleHandle;
        private TextureRect[] _notchIndicators = new TextureRect[17]; // P8-P1, N, B1-B8
        private TextureButton _eBrakeBtn;
        private TextureButton _hornBtn;
        private TextureButton _reverserBtn;
        private Label _reverserLabel;
        private TextureButton _lightsBtn;
        private Label _lightsLabel;
        private TextureButton _cameraBtn;
        private Label _cameraLabel;
        private TextureButton _pauseBtn;

        // References
        private InputManager _inputManager;
        private Simulation _simulation;

        public override void _Ready()
        {
            _inputManager = InputManager.Instance;
            _simulation = Simulation.Instance;

            // Get nodes
            _throttleHandle = GetNode<TextureRect>("ThrottleZone/ThrottleHandle");
            _notchIndicators[0] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch8");
            _notchIndicators[1] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch7");
            _notchIndicators[2] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch6");
            _notchIndicators[3] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch5");
            _notchIndicators[4] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch4");
            _notchIndicators[5] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch4");
            _notchIndicators[6] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch3");
            _notchIndicators[7] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch2");
            _notchIndicators[8] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch1");
            _notchIndicators[9] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/Notch0");
            _notchIndicators[10] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/NotchB1");
            _notchIndicators[11] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/NotchB2");
            _notchIndicators[12] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/NotchB3");
            _notchIndicators[13] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/NotchB4");
            _notchIndicators[14] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/NotchB5");
            _notchIndicators[15] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/NotchB6");
            _notchIndicators[16] = GetNode<TextureRect>("ThrottleZone/NotchIndicators/NotchB7");
            // Note: Only 17 indicators but we have 8 power + 1 neutral + 8 brake = 17

            _eBrakeBtn = GetNode<TextureButton>("EBrakeButton");
            _hornBtn = GetNode<TextureButton>("HornButton");
            _reverserBtn = GetNode<TextureButton>("ReverserButton");
            _reverserLabel = GetNode<Label>("ReverserButton/ReverserLabel");
            _lightsBtn = GetNode<TextureButton>("LightsButton");
            _lightsLabel = GetNode<Label>("LightsButton/LightsLabel");
            _cameraBtn = GetNode<TextureButton>("CameraButton");
            _cameraLabel = GetNode<Label>("CameraButton/CameraLabel");
            _pauseBtn = GetNode<TextureButton>("PauseButton");

            // Connect signals
            _eBrakeBtn.Pressed += OnEBrakePressed;
            _eBrakeBtn.ButtonUp += OnEBrakeReleased;
            _hornBtn.Pressed += OnHornPressed;
            _hornBtn.ButtonUp += OnHornReleased;
            _reverserBtn.Pressed += OnReverserPressed;
            _lightsBtn.Pressed += OnLightsPressed;
            _cameraBtn.Pressed += OnCameraPressed;
            _pauseBtn.Pressed += OnPausePressed;

            // Hide on non-touch devices initially
            if (!_inputManager.IsTouchDevice())
            {
                Visible = false;
            }
        }

        public override void _Process(double delta)
        {
            UpdateThrottleVisual();
            UpdateButtonStates();
        }

        private void UpdateThrottleVisual()
        {
            if (_throttleHandle == null) return;

            float throttle = _inputManager.ThrottleAxis;

            // Move handle vertically (0.5 = center/neutral)
            float handlePos = 0.5f - throttle * 0.45f; // 0.05 to 0.95
            handlePos = Math.Clamp(handlePos, 0.05f, 0.95f);

            _throttleHandle.AnchorTop = handlePos;
            _throttleHandle.AnchorBottom = handlePos;
            _throttleHandle.OffsetTop = -40;
            _throttleHandle.OffsetBottom = 40;

            // Update notch indicators
            int notch = (int)Math.Round(throttle * 8.0f);
            notch = Math.Clamp(notch, -8, 8);

            for (int i = 0; i < _notchIndicators.Length; i++)
            {
                if (_notchIndicators[i] != null)
                {
                    // Index mapping: 0=P8, 7=P1, 8=N, 9=B1, 16=B8
                    int indicatorNotch = 8 - i;
                    if (i > 8) indicatorNotch = i - 8; // B1=1, B8=8

                    bool active = (notch >= 0 && indicatorNotch <= notch && indicatorNotch > 0) ||
                                  (notch < 0 && indicatorNotch <= -notch && indicatorNotch > 0) ||
                                  (notch == 0 && indicatorNotch == 0);

                    _notchIndicators[i].Modulate = active ? Colors.Yellow : Colors.White;
                }
            }
        }

        private void UpdateButtonStates()
        {
            // Emergency brake visual
            if (_eBrakeBtn != null)
                _eBrakeBtn.Modulate = _inputManager.EmergencyBrake ? Colors.Red : Colors.White;

            // Horn visual
            if (_hornBtn != null)
                _hornBtn.Modulate = _inputManager.HornPressed ? Colors.Orange : Colors.White;

            // Reverser label
            if (_reverserLabel != null)
                _reverserLabel.Text = _inputManager.GetReverserText();

            // Lights label
            if (_lightsLabel != null)
                _lightsLabel.Text = _inputManager.GetLightText();

            // Camera label
            if (_cameraLabel != null && _simulation != null)
                _cameraLabel.Text = _simulation.CameraViewMode.ToString().ToUpper();

            // Pause visual
            if (_pauseBtn != null)
                _pauseBtn.Modulate = _inputManager.PausePressed ? Colors.Red : Colors.White;
        }

        // Signal handlers for direct button interaction
        private void OnEBrakePressed() => _inputManager.SetEmergencyBrake(true);
        private void OnEBrakeReleased() => _inputManager.SetEmergencyBrake(false);

        private void OnHornPressed() => _inputManager.SetHorn(true);
        private void OnHornReleased() => _inputManager.SetHorn(false);

        private void OnReverserPressed()
        {
            _inputManager.SetReverser((_inputManager.ReverserPosition + 1) % 3 - 1);
        }

        private void OnLightsPressed()
        {
            _inputManager.CycleLightsPublic();
        }

        private void OnCameraPressed()
        {
            _simulation?.CycleCamera();
        }

        private void OnPausePressed()
        {
            _simulation?.SetPaused(!_simulation.IsPaused);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
        }
    }
}