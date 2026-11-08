using System;
using System.Collections.Generic;
using Godot;

namespace OpenBVEAndroid.System
{
    /// <summary>
    /// Performance monitoring for dynamic quality scaling.
    /// Tracks frame times, GPU/CPU stats, and memory usage.
    /// </summary>
    [GlobalClass]
    public partial class PerformanceMonitor : Node
    {
        public static PerformanceMonitor Instance { get; private set; }

        // Frame timing
        public float AverageFrameTime { get; private set; }
        public float CurrentFrameTime { get; private set; }
        public float MinFrameTime { get; private set; } = float.MaxValue;
        public float MaxFrameTime { get; private set; }
        public float CurrentFPS { get; private set; }
        public float AverageFPS { get; private set; }

        // Frame history for averaging
        private Queue<float> _frameTimes = new Queue<float>();
        private const int HistorySize = 120; // 2 seconds at 60 FPS

        // GPU/CPU stats (approximate)
        public long StaticMemoryUsage { get; private set; }
        public long DynamicMemoryUsage { get; private set; }
        public int DrawCalls { get; private set; }
        public int Vertices { get; private set; }
        public int Primitives { get; private set; }

        // Thermal state (Android)
        public ThermalStateEnum ThermalState { get; private set; } = ThermalStateEnum.Normal;

        // Events
        public event Action<float> OnFrameTimeUpdate;
        public event Action<ThermalStateEnum> OnThermalStateChanged;

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        public override void _Process(double delta)
        {
            CurrentFrameTime = (float)(delta * 1000.0); // ms
            CurrentFPS = 1000.0f / Math.Max(CurrentFrameTime, 0.001f);

            _frameTimes.Enqueue(CurrentFrameTime);
            if (_frameTimes.Count > HistorySize)
                _frameTimes.Dequeue();

            // Calculate average
            float sum = 0;
            MinFrameTime = float.MaxValue;
            MaxFrameTime = 0;

            foreach (var ft in _frameTimes)
            {
                sum += ft;
                MinFrameTime = Math.Min(MinFrameTime, ft);
                MaxFrameTime = Math.Max(MaxFrameTime, ft);
            }

            AverageFrameTime = sum / _frameTimes.Count;
            AverageFPS = 1000.0f / Math.Max(AverageFrameTime, 0.001f);

            OnFrameTimeUpdate?.Invoke(AverageFrameTime);

            // Update memory stats
            UpdateMemoryStats();

            // Check thermal state (Android)
            CheckThermalState();
        }

        private void UpdateMemoryStats()
        {
            // Godot 4.x only exposes GetStaticMemoryUsage (returns ulong)
            // Dynamic memory usage is not directly available, approximate from static
            StaticMemoryUsage = (long)OS.GetStaticMemoryUsage();
            DynamicMemoryUsage = StaticMemoryUsage; // Approximation
        }

        private void CheckThermalState()
        {
            // On Android, we could use PowerManager or ThermalManager via JNI
            // For now, estimate from frame time variance
            float variance = MaxFrameTime - MinFrameTime;

            ThermalStateEnum newState;
            if (AverageFrameTime > 33.0f) // > 30 FPS target
                newState = ThermalStateEnum.Throttling;
            else if (variance > 20.0f)
                newState = ThermalStateEnum.Warm;
            else
                newState = ThermalStateEnum.Normal;

            if (newState != ThermalState)
            {
                ThermalState = newState;
                OnThermalStateChanged?.Invoke(ThermalState);
            }
        }

        public void RecordDrawCall()
        {
            DrawCalls++;
        }

        public void RecordVertices(int count)
        {
            Vertices += count;
        }

        public void RecordPrimitives(int count)
        {
            Primitives += count;
        }

        public void ResetFrame()
        {
            DrawCalls = 0;
            Vertices = 0;
            Primitives = 0;
        }

        public string GetStatsString()
        {
            return $"FPS: {CurrentFPS:F1} (avg: {AverageFPS:F1}) | " +
                   $"Frame: {CurrentFrameTime:F2}ms (avg: {AverageFrameTime:F2}ms) | " +
                   $"Min/Max: {MinFrameTime:F1}/{MaxFrameTime:F1}ms | " +
                   $"Draw calls: {DrawCalls} | " +
                   $"Verts: {Vertices:N0} | " +
                   $"Mem: {DynamicMemoryUsage / 1024 / 1024}MB | " +
                   $"Thermal: {ThermalState}";
        }

        public enum ThermalStateEnum
        {
            Normal,
            Warm,
            Throttling,
            Critical
        }
    }
}