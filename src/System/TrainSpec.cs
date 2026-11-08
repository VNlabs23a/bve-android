using System;

namespace OpenBVEAndroid.System
{
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