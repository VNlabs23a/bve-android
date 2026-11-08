using System;
using System.Collections.Generic;
using Godot;

namespace OpenBVEAndroid.Rendering
{
    /// <summary>
    /// Level of Detail management for performance scaling.
    /// Handles distance-based mesh swapping and quality tiers.
    /// </summary>
    [GlobalClass]
    public partial class LODManager : Node
    {
        public static LODManager Instance { get; private set; }

        [Export] public int CurrentQualityLevel { get; private set; } = 2;
        [Export] public bool AutoLOD { get; set; } = true;

        // LOD distances per quality level
        private readonly float[][] _lodDistances = new float[][]
        {
            // Low
            new float[] { 30f, 80f, 200f, 500f },
            // Medium
            new float[] { 50f, 150f, 400f, 1000f },
            // High
            new float[] { 80f, 250f, 600f, 1500f },
            // Ultra
            new float[] { 120f, 400f, 1000f, 2500f }
        };

        // Registered LOD objects
        private List<LODObject> _lodObjects = new();

        // Camera reference
        private Camera3D _camera;

        // Frustum planes for culling (calculated from camera)
        private Plane[] _frustumPlanes = new Plane[6];

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _ExitTree()
        {
            Instance = null;
        }

        public void SetCamera(Camera3D camera)
        {
            _camera = camera;
        }

        public void SetQualityLevel(int level)
        {
            CurrentQualityLevel = Math.Clamp(level, 0, 3);
            GD.Print($"LOD Quality Level set to: {GetQualityName(CurrentQualityLevel)}");
        }

        public void RegisterLODObject(LODObject obj)
        {
            if (!_lodObjects.Contains(obj))
                _lodObjects.Add(obj);
        }

        public void UnregisterLODObject(LODObject obj)
        {
            _lodObjects.Remove(obj);
        }

        public override void _Process(double delta)
        {
            if (!AutoLOD || _camera == null)
                return;

            UpdateLODs();
        }

        private void UpdateLODs()
        {
            Vector3 camPos = _camera.GlobalPosition;

            // Update frustum planes for culling
            UpdateFrustumPlanes();

            foreach (var obj in _lodObjects)
            {
                if (obj == null || !IsInstanceValid(obj))
                    continue;

                float dist = camPos.DistanceTo(obj.GlobalPosition);
                int lodLevel = CalculateLODLevel(dist);

                if (lodLevel != obj.CurrentLOD)
                {
                    obj.SetLOD(lodLevel);
                }

                // Frustum culling
                bool visible = IsInFrustum(obj);
                obj.Visible = visible;
            }
        }

        private void UpdateFrustumPlanes()
        {
            if (_camera == null) return;

            // Get frustum planes directly from Camera3D (Godot 4.x API)
            var frustum = _camera.GetFrustum();
            if (frustum.Count >= 6)
            {
                for (int i = 0; i < 6; i++)
                {
                    _frustumPlanes[i] = frustum[i];
                }
            }
        }

        private int CalculateLODLevel(float distance)
        {
            var distances = _lodDistances[CurrentQualityLevel];

            if (distance < distances[0]) return 0; // Highest detail
            if (distance < distances[1]) return 1;
            if (distance < distances[2]) return 2;
            if (distance < distances[3]) return 3;
            return 4; // Culled/hidden
        }

        private bool IsInFrustum(LODObject obj)
        {
            if (_camera == null) return true;

            // Sphere vs frustum test
            Vector3 center = obj.GlobalPosition;
            float radius = obj.BoundingRadius;

            for (int i = 0; i < 6; i++)
            {
                float distance = _frustumPlanes[i].DistanceTo(center);
                if (distance < -radius)
                    return false; // Outside this plane
            }

            return true;
        }

        public float GetLODDistance(int level, int lodIndex)
        {
            if (level >= 0 && level < _lodDistances.Length && lodIndex < _lodDistances[level].Length)
                return _lodDistances[level][lodIndex];
            return 1000f;
        }

        public string GetQualityName(int level)
        {
            return level switch
            {
                0 => "Low",
                1 => "Medium",
                2 => "High",
                3 => "Ultra",
                _ => "Unknown"
            };
        }

        // Quality preset application
        public void ApplyQualityPreset(QualityPreset preset)
        {
            SetQualityLevel((int)preset);
            GD.Print($"Applied quality preset: {preset}");
        }

        public enum QualityPreset
        {
            Low = 0,
            Medium = 1,
            High = 2,
            Ultra = 3
        }
    }

    /// <summary>
    /// Base class for objects that support LOD.
    /// </summary>
    public abstract partial class LODObject : Node3D
    {
        public int CurrentLOD { get; protected set; } = 0;
        public float BoundingRadius { get; protected set; } = 10f;

        public abstract void SetLOD(int level);

        protected float GetLODDistance(int lodIndex)
        {
            return LODManager.Instance?.GetLODDistance(LODManager.Instance.CurrentQualityLevel, lodIndex) ?? 1000f;
        }
    }

    /// <summary>
    /// LOD-enabled mesh instance with multiple mesh variants.
    /// </summary>
    public partial class LODMeshInstance : LODObject
    {
        [Export] public ArrayMesh MeshLOD0 { get; set; }
        [Export] public ArrayMesh MeshLOD1 { get; set; }
        [Export] public ArrayMesh MeshLOD2 { get; set; }
        [Export] public ArrayMesh MeshLOD3 { get; set; }

        private MeshInstance3D _meshInstance;

        public override void _Ready()
        {
            _meshInstance = new MeshInstance3D();
            AddChild(_meshInstance);

            // Calculate bounding radius from highest LOD mesh
            if (MeshLOD0 != null)
            {
                var aabb = MeshLOD0.GetAabb();
                BoundingRadius = aabb.Size.Length() * 0.5f;
            }
            else
            {
                BoundingRadius = 10f;
            }

            SetLOD(0);
        }

        public override void SetLOD(int level)
        {
            if (level == CurrentLOD) return;

            CurrentLOD = level;

            ArrayMesh mesh = level switch
            {
                0 => MeshLOD0,
                1 => MeshLOD1,
                2 => MeshLOD2,
                3 => MeshLOD3,
                _ => null
            };

            if (mesh != null)
            {
                _meshInstance.Mesh = mesh;
                _meshInstance.Visible = true;
            }
            else
            {
                _meshInstance.Visible = false;
            }
        }
    }
}