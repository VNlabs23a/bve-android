using System;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Unified input abstraction for touch, gamepad, keyboard, and mouse.
    /// Provides normalized input values regardless of source.
    /// </summary>
    [GlobalClass]
    public partial class InputManager : Node
    {
        public static InputManager Instance { get; private set; }

        // Input state
        public Vector2 TouchMovement { get; private set; }
        public Vector2 TouchLook { get; private set; }
        public float ThrottleAxis { get; private set; }
        public bool EmergencyBrake { get; private set; }
        public bool HornPressed { get; private set; }
        public int ReverserPosition { get; private set; }
        public int LightState { get; private set; }
        public bool PausePressed { get; private set; }
        public bool CameraCyclePressed { get; private set; }

        // Touch tracking
        private int _movementTouchId = -1;
        private int _lookTouchId = -1;
        private Vector2 _movementStartPos;
        private Vector2 _lookStartPos;

        // Input zones (normalized screen coordinates 0-1)
        private Rect2 _throttleZone = new Rect2(0.05f, 0.6f, 0.2f, 0.35f);      // Left side vertical
        private Rect2 _lookZone = new Rect2(0.3f, 0.2f, 0.7f, 0.7f);            // Center area
        private Rect2 _eBrakeZone = new Rect2(0.8f, 0.75f, 0.15f, 0.15f);       // Bottom right
        private Rect2 _hornZone = new Rect2(0.6f, 0.75f, 0.15f, 0.15f);         // Bottom right-ish
        private Rect2 _reverserZone = new Rect2(0.05f, 0.1f, 0.15f, 0.1f);      // Top left
        private Rect2 _lightsZone = new Rect2(0.25f, 0.1f, 0.15f, 0.1f);        // Top left-ish
        private Rect2 _cameraZone = new Rect2(0.45f, 0.1f, 0.15f, 0.1f);        // Top center
        private Rect2 _pauseZone = new Rect2(0.85f, 0.05f, 0.1f, 0.1f);         // Top right

        // Input smoothing
        private float _throttleSmoothing = 0.15f;
        private float _targetThrottle = 0f;

        // Gamepad state
        private int _gamepadId = -1;
        private float _gamepadDeadzone = 0.2f;

        public override void _EnterTree()
        {
            Instance = this;
            Input.TouchScreenEnabled = true;
            Input.MouseMode = Input.MouseModeEnum.Hidden;
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        public override void _Process(double delta)
        {
            ProcessGamepad();
            ProcessKeyboard();
            SmoothThrottle((float)delta);
        }

        public override void _Input(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventScreenTouch touch:
                    ProcessTouch(touch);
                    break;
                case InputEventScreenDrag drag:
                    ProcessDrag(drag);
                    break;
                case InputEventJoypadButton joyBtn:
                    ProcessGamepadButton(joyBtn);
                    break;
                case InputEventJoypadMotion joyMotion:
                    ProcessGamepadMotion(joyMotion);
                    break;
                case InputEventKey key:
                    ProcessKey(key);
                    break;
            }
        }

        private void ProcessTouch(InputEventScreenTouch touch)
        {
            Vector2 normPos = new Vector2(
                touch.Position.X / GetViewportRect().Size.X,
                touch.Position.Y / GetViewportRect().Size.Y
            );

            if (touch.Pressed)
            {
                // Check which zone was touched
                if (_throttleZone.HasPoint(normPos))
                {
                    _movementTouchId = touch.Index;
                    _movementStartPos = normPos;
                }
                else if (_lookZone.HasPoint(normPos) && _movementTouchId != touch.Index)
                {
                    _lookTouchId = touch.Index;
                    _lookStartPos = normPos;
                }
                else if (_eBrakeZone.HasPoint(normPos))
                {
                    EmergencyBrake = true;
                }
                else if (_hornZone.HasPoint(normPos))
                {
                    HornPressed = true;
                }
                else if (_reverserZone.HasPoint(normPos))
                {
                    CycleReverser();
                }
                else if (_lightsZone.HasPoint(normPos))
                {
                    CycleLights();
                }
                else if (_cameraZone.HasPoint(normPos))
                {
                    CameraCyclePressed = true;
                }
                else if (_pauseZone.HasPoint(normPos))
                {
                    PausePressed = true;
                }
            }
            else
            {
                // Touch released
                if (touch.Index == _movementTouchId)
                {
                    _movementTouchId = -1;
                    _targetThrottle = 0f;
                }
                if (touch.Index == _lookTouchId)
                {
                    _lookTouchId = -1;
                    TouchLook = Vector2.Zero;
                }

                EmergencyBrake = false;
                HornPressed = false;
                CameraCyclePressed = false;
                PausePressed = false;
            }
        }

        private void ProcessDrag(InputEventScreenDrag drag)
        {
            Vector2 normPos = new Vector2(
                drag.Position.X / GetViewportRect().Size.X,
                drag.Position.Y / GetViewportRect().Size.Y
            );

            if (drag.Index == _movementTouchId)
            {
                // Vertical drag for throttle
                float deltaY = _movementStartPos.Y - normPos.Y;
                _targetThrottle = Math.Clamp(deltaY * 3.0f, -1.0f, 1.0f);
            }
            else if (drag.Index == _lookTouchId)
            {
                // Free look drag
                TouchLook = (normPos - _lookStartPos) * 2.0f;
                TouchLook = TouchLook.LimitLength(1.0f);
            }
        }

        private void ProcessGamepad()
        {
            // Auto-detect gamepad
            if (_gamepadId == -1)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Input.IsJoyKnown(i))
                    {
                        _gamepadId = i;
                        break;
                    }
                }
            }

            if (_gamepadId >= 0)
            {
                // Left stick Y for throttle (inverted)
                float ly = Input.GetJoyAxis(_gamepadId, JoyAxis.LeftY);
                if (Math.Abs(ly) > _gamepadDeadzone)
                {
                    _targetThrottle = -ly;
                }

                // Right stick for camera look
                float rx = Input.GetJoyAxis(_gamepadId, JoyAxis.RightX);
                float ry = Input.GetJoyAxis(_gamepadId, JoyAxis.RightY);
                if (Math.Abs(rx) > _gamepadDeadzone || Math.Abs(ry) > _gamepadDeadzone)
                {
                    TouchLook = new Vector2(rx, ry).LimitLength(1.0f);
                }

                // Triggers for fine throttle control
                float lt = Input.GetJoyAxis(_gamepadId, JoyAxis.LeftTrigger);
                float rt = Input.GetJoyAxis(_gamepadId, JoyAxis.RightTrigger);
                if (lt > _gamepadDeadzone || rt > _gamepadDeadzone)
                {
                    _targetThrottle = rt - lt;
                }
            }
        }

        private void ProcessGamepadButton(InputEventJoypadButton btn)
        {
            if (btn.Device != _gamepadId) return;

            if (btn.Pressed)
            {
                switch (btn.ButtonIndex)
                {
                    case JoyButton.A: // Cross / A
                        HornPressed = true;
                        break;
                    case JoyButton.B: // Circle / B
                        EmergencyBrake = true;
                        break;
                    case JoyButton.X: // Square / X
                        CycleLights();
                        break;
                    case JoyButton.Y: // Triangle / Y
                        CycleReverser();
                        break;
                    case JoyButton.L1: // LB
                        CameraCyclePressed = true;
                        break;
                    case JoyButton.R1: // RB
                        // Could be sand/brake
                        break;
                    case JoyButton.Start:
                    case JoyButton.Select:
                    case JoyButton.Mode:
                        PausePressed = true;
                        break;
                    case JoyButton.DpadUp:
                        AdjustThrottle(0.25f);
                        break;
                    case JoyButton.DpadDown:
                        AdjustThrottle(-0.25f);
                        break;
                }
            }
            else
            {
                switch (btn.ButtonIndex)
                {
                    case JoyButton.A:
                        HornPressed = false;
                        break;
                    case JoyButton.B:
                        EmergencyBrake = false;
                        break;
                    case JoyButton.L1:
                        CameraCyclePressed = false;
                        break;
                }
            }
        }

        private void ProcessGamepadMotion(InputEventJoypadMotion motion)
        {
            // Handled in ProcessGamepad
        }

        private void ProcessKeyboard()
        {
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
                AdjustThrottle(0.02f);
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
                AdjustThrottle(-0.02f);
            if (Input.IsKeyPressed(Key.Space))
                EmergencyBrake = true;
            else
                EmergencyBrake = false;
            if (Input.IsKeyPressed(Key.H))
                HornPressed = true;
            else
                HornPressed = false;
            if (Input.IsKeyPressed(Key.R))
                CycleReverser();
            if (Input.IsKeyPressed(Key.L))
                CycleLights();
            if (Input.IsKeyPressed(Key.C))
                CameraCyclePressed = true;
            else
                CameraCyclePressed = false;
            if (Input.IsKeyPressed(Key.Escape))
                PausePressed = true;
            else
                PausePressed = false;
        }

        private void ProcessKey(InputEventKey key)
        {
            if (key.Pressed && !key.Echo)
            {
                switch (key.Keycode)
                {
                    case Key.R:
                        CycleReverser();
                        break;
                    case Key.L:
                        CycleLights();
                        break;
                    case Key.C:
                        CameraCyclePressed = true;
                        break;
                    case Key.Escape:
                        PausePressed = true;
                        break;
                }
            }
        }

        private void SmoothThrottle(float delta)
        {
            ThrottleAxis = Mathf.Lerp(ThrottleAxis, _targetThrottle, _throttleSmoothing * 60 * delta);
        }

        private void AdjustThrottle(float delta)
        {
            _targetThrottle = Math.Clamp(_targetThrottle + delta, -1.0f, 1.0f);
        }

        private void CycleReverser()
        {
            ReverserPosition = (ReverserPosition + 1) % 3 - 1; // -1, 0, 1
        }

        private void CycleLights()
        {
            LightState = (LightState + 1) % 3; // 0, 1, 2
        }

        // Getters for UI
        public string GetReverserText() => ReverserPosition switch
        {
            -1 => "REV",
            0 => "N",
            1 => "FWD",
            _ => "N"
        };

        public string GetLightText() => LightState switch
        {
            0 => "OFF",
            1 => "LOW",
            2 => "HIGH",
            _ => "OFF"
        };

        public bool IsTouchDevice() => OS.HasFeature("touchscreen");

        // Zone accessors for UI rendering
        public Rect2 GetThrottleZone() => _throttleZone;
        public Rect2 GetLookZone() => _lookZone;
        public Rect2 GetEBrakeZone() => _eBrakeZone;
        public Rect2 GetHornZone() => _hornZone;
        public Rect2 GetReverserZone() => _reverserZone;
        public Rect2 GetLightsZone() => _lightsZone;
        public Rect2 GetCameraZone() => _cameraZone;
        public Rect2 GetPauseZone() => _pauseZone;
    }
}