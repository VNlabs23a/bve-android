using System;
using System.Collections.Generic;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Train physics simulation - handles acceleration, braking, resistance forces.
    /// Based on OpenBVE physics model.
    /// </summary>
    public class TrainPhysics
    {
        // Train specifications
        public TrainSpec Spec { get; private set; }

        // Current state
        public double CurrentSpeed { get; private set; } // m/s
        public double CurrentAcceleration { get; private set; } // m/s²
        public int CurrentNotch { get; private set; }
        public int CurrentBrakeNotch { get; private set; }
        public double BrakePipePressure { get; private set; } // kPa
        public double MainReservoirPressure { get; private set; } // kPa
        public double BrakeCylinderPressure { get; private set; } // kPa

        // Internal state
        private double _motorTorque;
        private double _brakeForce;
        private double _resistanceForce;
        private bool _emergencyBrakeActive;

        public TrainPhysics()
        {
            Spec = TrainSpec.Default;
            Reset();
        }

        public void Reset()
        {
            CurrentSpeed = 0.0;
            CurrentAcceleration = 0.0;
            CurrentNotch = 0;
            CurrentBrakeNotch = 0;
            BrakePipePressure = 500.0; // 500 kPa = 5 bar
            MainReservoirPressure = 900.0; // 9 bar
            BrakeCylinderPressure = 0.0;
            _motorTorque = 0.0;
            _brakeForce = 0.0;
            _resistanceForce = 0.0;
            _emergencyBrakeActive = false;
        }

        /// <summary>
        /// Load train specification from file.
        /// </summary>
        public bool LoadTrain(string path)
        {
            try
            {
                // TODO: Parse .dat/.csv train files
                // For now, use default spec
                Spec = TrainSpec.Default;
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to load train: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculate net acceleration based on inputs and conditions.
        /// </summary>
        public double CalculateAcceleration(
            double throttleInput,      // -1.0 to 1.0
            bool emergencyBrake,
            int reverserPosition,      // -1, 0, 1
            double adhesion,           // 0.0 to 1.0
            double lateralAcceleration, // m/s²
            double speedLimit)         // m/s, -1 = no limit
        {
            _emergencyBrakeActive = emergencyBrake;

            // Update notch based on throttle input (8 power notches + 8 brake notches)
            int targetNotch = (int)Math.Round(throttleInput * 8.0);
            targetNotch = Math.Clamp(targetNotch, -8, 8);

            if (targetNotch > 0)
            {
                CurrentNotch = targetNotch;
                CurrentBrakeNotch = 0;
            }
            else if (targetNotch < 0)
            {
                CurrentNotch = 0;
                CurrentBrakeNotch = -targetNotch;
            }
            else
            {
                CurrentNotch = 0;
                CurrentBrakeNotch = 0;
            }

            // Apply reverser
            if (reverserPosition == 0)
            {
                CurrentNotch = 0;
                CurrentBrakeNotch = Math.Max(CurrentBrakeNotch, 1); // Hold brake in neutral
            }
            else if (reverserPosition < 0 && CurrentSpeed > 0.1)
            {
                // Don't allow reverse while moving forward at speed
                CurrentNotch = 0;
            }

            // Speed limiter
            if (speedLimit > 0 && CurrentSpeed > speedLimit)
            {
                // Apply service brake to maintain speed limit
                CurrentBrakeNotch = Math.Max(CurrentBrakeNotch, 2);
            }

            // Calculate forces
            CalculateMotorForce(adhesion);
            CalculateBrakeForce(adhesion);
            CalculateResistance(lateralAcceleration);

            // Net force = motor - brake - resistance
            double netForce = _motorTorque - _brakeForce - _resistanceForce;

            // Acceleration = F / m
            double acceleration = netForce / Spec.Mass;

            // Apply adhesion limit
            double maxAdhesionAccel = adhesion * 9.80665 * Spec.AdhesionFactor;
            acceleration = Math.Clamp(acceleration, -maxAdhesionAccel, maxAdhesionAccel);

            CurrentAcceleration = acceleration;
            return acceleration;
        }

        private void CalculateMotorForce(double adhesion)
        {
            if (CurrentNotch <= 0)
            {
                _motorTorque = 0.0;
                return;
            }

            // Power curve: torque decreases with speed
            double notchFraction = CurrentNotch / 8.0;
            double maxTorque = Spec.MaxTorque * notchFraction;

            // Torque curve: constant power region, then constant torque
            double baseSpeed = Spec.MaxTorqueSpeed; // Speed at max torque (m/s)
            double maxPower = Spec.MaxPower; // Watts

            if (CurrentSpeed < baseSpeed)
            {
                // Constant torque region
                _motorTorque = maxTorque;
            }
            else
            {
                // Constant power region: torque = power / speed
                _motorTorque = maxPower / Math.Max(CurrentSpeed, 0.1);
            }

            // Apply adhesion limit
            double maxAdhesionForce = Spec.Mass * adhesion * 9.80665 * Spec.AdhesionFactor;
            _motorTorque = Math.Min(_motorTorque, maxAdhesionForce);
        }

        private void CalculateBrakeForce(double adhesion)
        {
            if (CurrentBrakeNotch <= 0 && !_emergencyBrakeActive)
            {
                _brakeForce = 0.0;
                BrakeCylinderPressure = 0.0;
                return;
            }

            double brakeNotch = _emergencyBrakeActive ? 8.0 : CurrentBrakeNotch;
            double notchFraction = brakeNotch / 8.0;

            // Brake pipe pressure simulation (simplified)
            double targetBrakePipePressure = 500.0 * (1.0 - notchFraction * 0.8); // 500 -> 100 kPa
            BrakePipePressure = Math.Max(BrakePipePressure - 50.0 * (1.0 / 60.0), targetBrakePipePressure);

            // Brake cylinder pressure builds up
            double targetCylinderPressure = (500.0 - BrakePipePressure) * Spec.BrakeCylinderRatio;
            BrakeCylinderPressure = Math.Min(BrakeCylinderPressure + 200.0 * (1.0 / 60.0), targetCylinderPressure);

            // Brake force from cylinder pressure
            _brakeForce = BrakeCylinderPressure * Spec.BrakeForcePerPressure * Spec.BrakeCylinderCount;

            // Emergency brake multiplier
            if (_emergencyBrakeActive)
            {
                _brakeForce *= 1.5;
            }

            // Apply adhesion limit for braking too
            double maxAdhesionForce = Spec.Mass * adhesion * 9.80665 * Spec.AdhesionFactor;
            _brakeForce = Math.Min(_brakeForce, maxAdhesionForce);
        }

        private void CalculateResistance(double lateralAcceleration)
        {
            // Davis formula: R = A + B*v + C*v²
            // A = rolling resistance, B = viscous resistance, C = aerodynamic drag
            double v = CurrentSpeed; // m/s
            double vKmh = v * 3.6;

            double A = Spec.Mass * 9.80665 * Spec.RollingResistanceCoeff; // N
            double B = Spec.Mass * 9.80665 * Spec.ViscousResistanceCoeff; // N/(m/s)
            double C = 0.5 * Spec.FrontalArea * Spec.DragCoefficient * 1.225; // N/(m/s)²

            _resistanceForce = A + B * v + C * v * v;

            // Curve resistance (additional)
            if (Math.Abs(lateralAcceleration) > 0.1)
            {
                double curveResistance = Spec.Mass * 9.80665 * 0.001 * Math.Abs(lateralAcceleration) / 9.80665;
                _resistanceForce += curveResistance;
            }

            // Gradient resistance (handled via pitch in follower)
        }

        public void Update(double deltaTime, double speed, double acceleration)
        {
            CurrentSpeed = speed;
            CurrentAcceleration = acceleration;

            // Update pressures (simplified)
            if (CurrentBrakeNotch == 0 && !_emergencyBrakeActive)
            {
                // Recharge brake pipe
                BrakePipePressure = Math.Min(BrakePipePressure + 100.0 * deltaTime, 500.0);
                BrakeCylinderPressure = Math.Max(BrakeCylinderPressure - 300.0 * deltaTime, 0.0);
            }

            // Compressor
            if (MainReservoirPressure < 900.0)
            {
                MainReservoirPressure = Math.Min(MainReservoirPressure + 50.0 * deltaTime, 900.0);
            }
        }

        // Getters for HUD
        public int GetPowerNotch() => CurrentNotch;
        public int GetBrakeNotch() => CurrentBrakeNotch;
        public double GetBrakePipePressureKpa() => BrakePipePressure;
        public double GetMainReservoirPressureKpa() => MainReservoirPressure;
        public double GetBrakeCylinderPressureKpa() => BrakeCylinderPressure;
        public double GetMotorForce() => _motorTorque;
        public double GetBrakeForce() => _brakeForce;
        public double GetResistanceForce() => _resistanceForce;
    }

    /// <summary>
    /// Train specification parameters.
    /// </summary>
    public class TrainSpec
    {
        public string Name { get; set; } = "Default Train";
        public double Mass { get; set; } = 200000.0; // kg (200t)
        public int CarCount { get; set; } = 4;
        public double Length { get; set; } = 80.0; // meters
        public double Width { get; set; } = 2.9; // meters
        public double Height { get; set; } = 4.1; // meters

        // Performance
        public double MaxPower { get; set; } = 2000000.0; // Watts (2MW)
        public double MaxTorque { get; set; } = 150000.0; // Newtons
        public double MaxTorqueSpeed { get; set; } = 13.9; // m/s (50 km/h)
        public double MaxSpeed { get; set; } = 44.4; // m/s (160 km/h)

        // Adhesion
        public double AdhesionFactor { get; set; } = 0.35; // Typical for steel on steel

        // Resistance (Davis formula coefficients)
        public double RollingResistanceCoeff { get; set; } = 0.0015;
        public double ViscousResistanceCoeff { get; set; } = 0.00005;
        public double DragCoefficient { get; set; } = 0.3;
        public double FrontalArea { get; set; } = 12.0; // m²

        // Braking
        public double BrakeCylinderRatio { get; set; } = 2.5; // Cylinder pressure / (500 - pipe pressure)
        public double BrakeForcePerPressure { get; set; } = 500.0; // N/kPa per cylinder
        public int BrakeCylinderCount { get; set; } = 8; // 2 per car

        // Electrical
        public double Voltage { get; set; } = 1500.0; // V DC
        public double MaxCurrent { get; set; } = 2000.0; // A

        public static TrainSpec Default => new TrainSpec();

        public static TrainSpec CreateEMU(int cars = 4, double maxSpeedKmh = 160)
        {
            return new TrainSpec
            {
                Name = $"{cars}-car EMU",
                CarCount = cars,
                Mass = cars * 35000.0,
                Length = cars * 20.0,
                MaxPower = cars * 500000.0,
                MaxTorque = cars * 35000.0,
                MaxSpeed = maxSpeedKmh / 3.6,
            };
        }

        public static TrainSpec CreateLocomotive(double powerMW = 4.0, double massT = 80.0)
        {
            return new TrainSpec
            {
                Name = "Electric Locomotive",
                CarCount = 1,
                Mass = massT * 1000.0,
                Length = 18.0,
                MaxPower = powerMW * 1000000.0,
                MaxTorque = 300000.0,
                MaxSpeed = 160.0 / 3.6,
                BrakeCylinderCount = 2,
            };
        }
    }
}