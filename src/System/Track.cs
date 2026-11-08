using System;
using System.Collections.Generic;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Represents a complete track (running line) with multiple elements.
    /// Handles smoothing, inaccuracy generation, and track following queries.
    /// </summary>
    public class Track
    {
        /// <summary>
        /// All track elements in order.
        /// </summary>
        public TrackElement[] Elements { get; private set; }

        /// <summary>
        /// Rail gauge in meters (standard 1.435m).
        /// </summary>
        public double Gauge { get; private set; }

        /// <summary>
        /// Number of subdivisions per element for smoothing (1 = no smoothing).
        /// </summary>
        public int Smoothing { get; private set; }

        /// <summary>
        /// Total track length in meters.
        /// </summary>
        public double Length { get; private set; }

        /// <summary>
        /// Whether this track has inaccuracy simulation enabled.
        /// </summary>
        public bool HasInaccuracy { get; private set; }

        /// <summary>
        /// Track identifier.
        /// </summary>
        public int TrackIndex { get; private set; }

        /// <summary>
        /// Track name/description.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Create a new track from elements.
        /// </summary>
        public Track(TrackElement[] elements, double gauge = 1.435, int smoothing = 4, int trackIndex = 0, string name = "")
        {
            TrackIndex = trackIndex;
            Name = name;
            Gauge = gauge;
            Smoothing = Math.Max(1, smoothing);
            HasInaccuracy = true;

            // Apply smoothing if needed
            if (Smoothing > 1)
            {
                Elements = ApplySmoothing(elements);
            }
            else
            {
                Elements = elements;
            }

            // Update indices and distances
            UpdateIndicesAndDistances();

            // Generate inaccuracy
            if (HasInaccuracy)
            {
                GenerateInaccuracy();
            }

            Length = Elements.Length > 0 ? Elements[^1].Distance : 0.0;
        }

        /// <summary>
        /// Apply Catmull-Rom spline smoothing to track elements.
        /// </summary>
        private TrackElement[] ApplySmoothing(TrackElement[] input)
        {
            if (input.Length < 4)
                return input;

            var output = new List<TrackElement>();
            int segmentsPerElement = Smoothing;

            for (int i = 0; i < input.Length - 1; i++)
            {
                TrackElement p0 = i > 0 ? input[i - 1] : input[i];
                TrackElement p1 = input[i];
                TrackElement p2 = input[i + 1];
                TrackElement p3 = i + 2 < input.Length ? input[i + 2] : input[i + 1];

                for (int s = 0; s < segmentsPerElement; s++)
                {
                    double t = (double)s / segmentsPerElement;
                    var interpolated = CatmullRom(ref p0, ref p1, ref p2, ref p3, t);
                    output.Add(interpolated);
                }
            }

            // Add final element
            output.Add(input[^1]);

            return output.ToArray();
        }

        /// <summary>
        /// Catmull-Rom spline interpolation for smooth curves.
        /// </summary>
        private static TrackElement CatmullRom(ref TrackElement p0, ref TrackElement p1, ref TrackElement p2, ref TrackElement p3, double t)
        {
            double t2 = t * t;
            double t3 = t2 * t;

            // Centripetal Catmull-Rom for better parameterization
            double alpha = 0.5; // 0.5 = centripetal, 0 = uniform, 1 = chordal

            // For simplicity, using standard Catmull-Rom
            float ft = (float)t;
            float ft2 = (float)t2;
            float ft3 = (float)t3;

            Vector3 pos = 0.5f * ((2 * p1.Position) +
                (-p0.Position + p2.Position) * ft +
                (2 * p0.Position - 5 * p1.Position + 4 * p2.Position - p3.Position) * ft2 +
                (-p0.Position + 3 * p1.Position - 3 * p2.Position + p3.Position) * ft3);

            Vector3 dir = Calc.Normalize(0.5f * ((-p0.Position + p2.Position) +
                (2 * p0.Position - 5 * p1.Position + 4 * p2.Position - p3.Position) * ft * 2 +
                (-p0.Position + 3 * p1.Position - 3 * p2.Position + p3.Position) * ft2 * 3));

            // Interpolate other properties - use Vector3.Lerp instance method (Godot 4.x)
            Vector3 up = Calc.Normalize(p1.Up.Lerp(p2.Up, ft));
            Vector3 side = Calc.Normalize(p1.Side.Lerp(p2.Side, ft));

            double curveRadius = Calc.Lerp(p1.CurveRadius, p2.CurveRadius, t);
            double cant = Calc.Lerp(p1.Cant, p2.Cant, t);
            double pitch = Calc.Lerp(p1.Pitch, p2.Pitch, t);

            return new TrackElement
            {
                Position = pos,
                Direction = dir,
                Up = up,
                Side = side,
                CurveRadius = curveRadius,
                Cant = cant,
                Pitch = pitch,
                Adhesion = Calc.Lerp(p1.Adhesion, p2.Adhesion, t),
                Gauge = Calc.Lerp(p1.Gauge, p2.Gauge, t),
                Index = -1,
                Distance = 0.0, // Will be updated later
                InaccuracyOffset = Vector3.Zero, // Will be generated later
                Type = t < 0.5 ? p1.Type : p2.Type,
                SignalSectionIndex = t < 0.5 ? p1.SignalSectionIndex : p2.SignalSectionIndex,
                SpeedLimit = Calc.Lerp(p1.SpeedLimit, p2.SpeedLimit, t),
                StationStop = t < 0.5 ? p1.StationStop : p2.StationStop
            };
        }

        /// <summary>
        /// Update element indices and cumulative distances.
        /// </summary>
        private void UpdateIndicesAndDistances()
        {
            double cumulativeDistance = 0.0;
            double elementLength = Length > 0 ? Length / Elements.Length : 25.0; // Default 25m per element

            for (int i = 0; i < Elements.Length; i++)
            {
                var elem = Elements[i];
                elem.Index = i;
                elem.Distance = cumulativeDistance;
                Elements[i] = elem;
                cumulativeDistance += elementLength;
            }

            Length = cumulativeDistance;
        }

        /// <summary>
        /// Generate procedural track inaccuracy (gauge spread, bounce).
        /// Based on OpenBVE's inaccuracy simulation.
        /// </summary>
        private void GenerateInaccuracy()
        {
            var random = new Random(TrackIndex * 12345 + 67890);

            double gaugeSpread = 0.0;
            double gaugeSpreadTarget = 0.0;
            double verticalBounce = 0.0;
            double verticalBounceTarget = 0.0;
            double lateralBounce = 0.0;
            double lateralBounceTarget = 0.0;

            for (int i = 0; i < Elements.Length; i++)
            {
                // Update targets periodically
                if (i % 100 == 0)
                {
                    gaugeSpreadTarget = (random.NextDouble() - 0.5) * 0.02; // ±10mm
                    verticalBounceTarget = (random.NextDouble() - 0.5) * 0.01; // ±5mm
                    lateralBounceTarget = (random.NextDouble() - 0.5) * 0.015; // ±7.5mm
                }

                // Smooth towards targets
                gaugeSpread = Calc.Lerp(gaugeSpread, gaugeSpreadTarget, 0.01);
                verticalBounce = Calc.Lerp(verticalBounce, verticalBounceTarget, 0.02);
                lateralBounce = Calc.Lerp(lateralBounce, lateralBounceTarget, 0.02);

                var elem = Elements[i];
                // Cast double to float for Vector3 construction
                elem.InaccuracyOffset = new Vector3(
                    (float)(elem.Side.X * lateralBounce + elem.Side.X * gaugeSpread * 0.5),
                    (float)verticalBounce,
                    (float)(elem.Side.Z * lateralBounce + elem.Side.Z * gaugeSpread * 0.5)
                );
                Elements[i] = elem;
            }
        }

        /// <summary>
        /// Get track element at a specific distance (with interpolation).
        /// </summary>
        public TrackElement GetElementAt(double distance)
        {
            if (Elements.Length == 0)
                return default;

            // Clamp distance
            distance = Math.Max(0.0, Math.Min(Length, distance));

            if (Elements.Length == 1)
                return Elements[0];

            // Binary search for the element
            int low = 0;
            int high = Elements.Length - 1;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (Elements[mid].Distance <= distance)
                    low = mid;
                else
                    high = mid - 1;
            }

            if (low == Elements.Length - 1)
                return Elements[low];

            // Interpolate between low and low+1
            var a = Elements[low];
            var b = Elements[low + 1];

            double segmentLength = b.Distance - a.Distance;
            if (segmentLength <= 0)
                return a;

            double t = (distance - a.Distance) / segmentLength;
            return TrackElement.Interpolate(ref a, ref b, t);
        }

        /// <summary>
        /// Get element index at distance (no interpolation).
        /// </summary>
        public int GetElementIndexAt(double distance)
        {
            if (Elements.Length == 0)
                return -1;

            distance = Math.Max(0.0, Math.Min(Length, distance));

            int low = 0;
            int high = Elements.Length - 1;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (Elements[mid].Distance <= distance)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low;
        }

        /// <summary>
        /// Get world transform at distance (position, rotation).
        /// </summary>
        public Transform3D GetTransformAt(double distance)
        {
            var elem = GetElementAt(distance);

            // Build basis from direction, up, side
            var basis = new Basis(
                elem.Side.X, elem.Up.X, -elem.Direction.X,
                elem.Side.Y, elem.Up.Y, -elem.Direction.Y,
                elem.Side.Z, elem.Up.Z, -elem.Direction.Z
            );

            return new Transform3D(basis, elem.Position + elem.InaccuracyOffset);
        }

        /// <summary>
        /// Get rail positions (left and right rail) at distance.
        /// </summary>
        public (Vector3 LeftRail, Vector3 RightRail) GetRailPositions(double distance)
        {
            var elem = GetElementAt(distance);
            double halfGauge = elem.Gauge * 0.5;

            // Cast double to float for Vector3 operations
            Vector3 leftRail = elem.Position + elem.Side * (float)(-halfGauge) + elem.InaccuracyOffset;
            Vector3 rightRail = elem.Position + elem.Side * (float)halfGauge + elem.InaccuracyOffset;

            return (leftRail, rightRail);
        }
    }
}