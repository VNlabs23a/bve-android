using System;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Mathematical utilities ported from OpenBVE/gt-openbve.
    /// Uses double precision for track calculations (critical for long routes).
    /// </summary>
    public static class Calc
    {
        public const double PI = Math.PI;
        public const double PI2 = Math.PI * 2.0;
        public const double DegToRad = Math.PI / 180.0;
        public const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Cross product for 3D vectors (double precision).
        /// </summary>
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(
                (float)(a.Y * b.Z - a.Z * b.Y),
                (float)(a.Z * b.X - a.X * b.Z),
                (float)(a.X * b.Y - a.Y * b.X)
            );
        }

        /// <summary>
        /// Cross product for 2D vectors (returns scalar).
        /// </summary>
        public static double Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        /// <summary>
        /// Cross product: Vector2 × Vector3 (treats Vector2 as XY, Z=0).
        /// </summary>
        public static Vector3 Cross(Vector2 a, Vector3 b)
        {
            return new Vector3(
                (float)(-a.Y * b.Z),
                (float)(a.X * b.Z),
                (float)(a.X * b.Y - a.Y * b.X)
            );
        }

        /// <summary>
        /// Cross product: Vector3 × Vector2 (treats Vector2 as XY, Z=0).
        /// </summary>
        public static Vector3 Cross(Vector3 a, Vector2 b)
        {
            return new Vector3(
                (float)(a.Y * 0 - a.Z * b.Y),
                (float)(a.Z * b.X - a.X * 0),
                (float)(a.X * b.Y - a.Y * b.X)
            );
        }

        /// <summary>
        /// Rotate vector around axis using Rodrigues' rotation formula (double precision).
        /// </summary>
        public static Vector3 Rotate(Vector3 v, Vector3 axis, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double oneMinusCos = 1.0 - cos;

            double x = v.X;
            double y = v.Y;
            double z = v.Z;
            double ax = axis.X;
            double ay = axis.Y;
            double az = axis.Z;

            double rx = x * (cos + ax * ax * oneMinusCos) +
                        y * (ax * ay * oneMinusCos - az * sin) +
                        z * (ax * az * oneMinusCos + ay * sin);

            double ry = x * (ay * ax * oneMinusCos + az * sin) +
                        y * (cos + ay * ay * oneMinusCos) +
                        z * (ay * az * oneMinusCos - ax * sin);

            double rz = x * (az * ax * oneMinusCos - ay * sin) +
                        y * (az * ay * oneMinusCos + ax * sin) +
                        z * (cos + az * az * oneMinusCos);

            return new Vector3((float)rx, (float)ry, (float)rz);
        }

        /// <summary>
        /// Normalize 3D vector (double precision internal).
        /// </summary>
        public static Vector3 Normalize(Vector3 v)
        {
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            if (len < 1e-10)
                return Vector3.Zero;
            return new Vector3((float)(v.X / len), (float)(v.Y / len), (float)(v.Z / len));
        }

        /// <summary>
        /// Normalize 2D vector (double precision internal).
        /// </summary>
        public static Vector2 Normalize(Vector2 v)
        {
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (len < 1e-10)
                return Vector2.Zero;
            return new Vector2((float)(v.X / len), (float)(v.Y / len));
        }

        /// <summary>
        /// Squared length (avoids sqrt for comparisons).
        /// </summary>
        public static double NormSquared(Vector3 v)
        {
            return v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        }

        /// <summary>
        /// Squared length for 2D.
        /// </summary>
        public static double NormSquared(Vector2 v)
        {
            return v.X * v.X + v.Y * v.Y;
        }

        /// <summary>
        /// Distance between two 3D points.
        /// </summary>
        public static double Distance(Vector3 a, Vector3 b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Linear interpolation.
        /// </summary>
        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Clamp value to range.
        /// </summary>
        public static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        /// <summary>
        /// Smoothstep interpolation.
        /// </summary>
        public static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>
        /// Cubic Hermite spline interpolation.
        /// </summary>
        public static double CubicHermite(double p0, double p1, double m0, double m1, double t)
        {
            double t2 = t * t;
            double t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * p0 +
                   (t3 - 2 * t2 + t) * m0 +
                   (-2 * t3 + 3 * t2) * p1 +
                   (t3 - t2) * m1;
        }

        /// <summary>
        /// Convert degrees to radians.
        /// </summary>
        public static double ToRadians(double degrees)
        {
            return degrees * DegToRad;
        }

        /// <summary>
        /// Convert radians to degrees.
        /// </summary>
        public static double ToDegrees(double radians)
        {
            return radians * RadToDeg;
        }

        /// <summary>
        /// Wrap angle to [-PI, PI].
        /// </summary>
        public static double WrapAngle(double angle)
        {
            angle = (angle + PI) % PI2;
            if (angle < 0) angle += PI2;
            return angle - PI;
        }

        /// <summary>
        /// Sign function.
        /// </summary>
        public static int Sign(double value)
        {
            return value > 0 ? 1 : (value < 0 ? -1 : 0);
        }
    }
}