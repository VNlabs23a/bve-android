using System;
using System.Collections.Generic;
using Godot;
using OpenBVEAndroid.System;

namespace OpenBVEAndroid.Rendering
{
    /// <summary>
    /// Train rendering with LOD support and dynamic bogie animation.
    /// </summary>
    public partial class TrainRenderer : Node3D
    {
        [Export] public string TrainPath { get; set; } = "";
        [Export] public int QualityLevel { get; set; } = 2;

        private TrainSpec _trainSpec;
        public TrainSpec TrainSpec => _trainSpec;

        // Train composition
        private List<CarRenderer> _cars = new();
        private Node3D _trainRoot;

        // LOD meshes per car type
        private Dictionary<string, CarLODSet> _lodCache = new();

        // Animation state
        private double _animationTime;
        private bool _doorsOpen;

        public override void _Ready()
        {
            _trainRoot = new Node3D { Name = "TrainRoot" };
            AddChild(_trainRoot);

            if (!string.IsNullOrEmpty(TrainPath))
            {
                LoadTrainFromPath(TrainPath);
            }
        }

        public void LoadTrainFromPath(string path)
        {
            var vfs = VirtualFS.Instance;
            if (vfs == null) return;

            string realPath = vfs.Resolve(path);
            if (realPath == null) return;

            // In a real implementation, this would parse the train file
            // For now, use default spec
            _trainSpec = TrainSpec.Default;
        }

        public void SetTrainSpec(TrainSpec spec)
        {
            _trainSpec = spec;
        }

        /// <summary>
        /// Initialize train with consist specification.
        /// </summary>
        public void InitializeTrain(List<TrainCarSpec> consist)
        {
            ClearTrain();

            foreach (var carSpec in consist)
            {
                var car = new CarRenderer
                {
                    Spec = carSpec,
                    QualityLevel = QualityLevel
                };
                car.Initialize();
                _trainRoot.AddChild(car);
                _cars.Add(car);
            }

            PositionCars();
        }

        private void ClearTrain()
        {
            foreach (var car in _cars)
                car.QueueFree();
            _cars.Clear();
        }

        private void PositionCars()
        {
            float zOffset = 0;
            foreach (var car in _cars)
            {
                car.Position = new Vector3(0, 0, zOffset);
                zOffset -= car.Spec.Length;
            }
        }

        /// <summary>
        /// Update train position and animation from follower.
        /// </summary>
        public void UpdateTrain(TrackFollower follower, double deltaTime)
        {
            if (_cars.Count == 0 || follower.CurrentTrack == null)
                return;

            _animationTime += deltaTime;

            // Lead car follows track directly
            var leadCar = _cars[0];
            leadCar.GlobalTransform = follower.GetWorldTransform();

            // Subsequent cars follow with coupling constraints
            for (int i = 1; i < _cars.Count; i++)
            {
                UpdateCarCoupling(_cars[i - 1], _cars[i], follower);
            }

            // Animate bogies
            foreach (var car in _cars)
            {
                car.AnimateBogies(_animationTime, follower.Speed);
            }

            // Animate wheels
            foreach (var car in _cars)
            {
                car.AnimateWheels(_animationTime, follower.Speed);
            }

            // Pantograph animation (if electric)
            if (TrainSpec.Voltage > 0)
            {
                AnimatePantographs(follower.Speed);
            }
        }

        private void UpdateCarCoupling(CarRenderer front, CarRenderer rear, TrackFollower follower)
        {
            // Simple coupling: rear car follows front car with fixed distance
            Vector3 frontPos = front.GlobalPosition;
            Vector3 frontDir = -front.GlobalTransform.Basis.Z;
            float couplingDist = front.Spec.Length * 0.5f + rear.Spec.Length * 0.5f + 0.5f; // Coupling gap

            Vector3 targetPos = frontPos + frontDir * couplingDist;

            // Smooth follow
            rear.GlobalPosition = rear.GlobalPosition.Lerp(targetPos, 0.1f);
            rear.GlobalRotation = front.GlobalRotation; // Simplified - real would use track follower per car
        }

        private void AnimatePantographs(double speed)
        {
            // Pantographs raise/lower based on speed and overhead wire
            foreach (var car in _cars)
            {
                if (car.HasPantograph)
                {
                    float targetHeight = speed > 1.0 ? 1.0f : 0.0f; // Raised above ~3.6 km/h
                    car.SetPantographHeight(targetHeight);
                }
            }
        }

        public void SetQualityLevel(int level)
        {
            QualityLevel = Math.Clamp(level, 0, 3);
            foreach (var car in _cars)
                car.SetQualityLevel(QualityLevel);
        }

        public void SetDoorsOpen(bool open)
        {
            _doorsOpen = open;
            foreach (var car in _cars)
                car.SetDoorsOpen(open);
        }

        public void SetLights(int state) // 0=off, 1=low, 2=high
        {
            foreach (var car in _cars)
                car.SetLights(state);
        }
    }

    /// <summary>
    /// Individual car renderer with LOD.
    /// </summary>
    public partial class CarRenderer : Node3D
    {
        public TrainCarSpec Spec { get; set; }
        public int QualityLevel { get; set; } = 2;
        public bool HasPantograph { get; private set; }

        private MeshInstance3D _bodyMesh;
        private MeshInstance3D _bogieFrontMesh;
        private MeshInstance3D _bogieRearMesh;
        private List<MeshInstance3D> _wheelMeshes = new();
        private MeshInstance3D _pantographMesh;
        private AnimationPlayer _animPlayer;

        public void Initialize()
        {
            Name = $"Car_{Spec.Type}";

            // Load or generate LOD meshes
            LoadLODMeshes();

            // Create body
            _bodyMesh = new MeshInstance3D { Mesh = GetLODMesh("body") };
            AddChild(_bodyMesh);

            // Create bogies
            _bogieFrontMesh = new MeshInstance3D { Mesh = GetLODMesh("bogie") };
            _bogieFrontMesh.Position = new Vector3(0, -1.2f, -Spec.Length * 0.4f);
            AddChild(_bogieFrontMesh);

            _bogieRearMesh = new MeshInstance3D { Mesh = GetLODMesh("bogie") };
            _bogieRearMesh.Position = new Vector3(0, -1.2f, Spec.Length * 0.4f);
            AddChild(_bogieRearMesh);

            // Create wheels (8 per bogie typically)
            CreateWheels();

            // Pantograph for electric cars
            if (Spec.Type == CarType.Electric || Spec.Type == CarType.Locomotive)
            {
                HasPantograph = true;
                _pantographMesh = new MeshInstance3D { Mesh = GetLODMesh("pantograph") };
                _pantographMesh.Position = new Vector3(0, 2.0f, 0);
                AddChild(_pantographMesh);
            }

            // Animation player for doors
            _animPlayer = new AnimationPlayer();
            AddChild(_animPlayer);
            CreateDoorAnimations();
        }

        private void LoadLODMeshes()
        {
            // In real implementation, load from .obj/.gltf files
            // For now, generate procedural meshes
        }

        private ArrayMesh GetLODMesh(string part)
        {
            // Return appropriate LOD mesh based on QualityLevel
            return GenerateProceduralMesh(part);
        }

        private ArrayMesh GenerateProceduralMesh(string part)
        {
            return part switch
            {
                "body" => GenerateCarBody(),
                "bogie" => GenerateBogie(),
                "pantograph" => GeneratePantograph(),
                _ => new ArrayMesh()
            };
        }

        private ArrayMesh GenerateCarBody()
        {
            float halfL = Spec.Length * 0.5f;
            float halfW = Spec.Width * 0.5f;
            float halfH = Spec.Height * 0.5f;
            float floorH = 1.2f;

            // Simple box car body
            Vector3[] corners = new Vector3[8]
            {
                new(-halfW, floorH, -halfL),
                new(halfW, floorH, -halfL),
                new(halfW, floorH + Spec.Height, -halfL),
                new(-halfW, floorH + Spec.Height, -halfL),
                new(-halfW, floorH, halfL),
                new(halfW, floorH, halfL),
                new(halfW, floorH + Spec.Height, halfL),
                new(-halfW, floorH + Spec.Height, halfL)
            };

            int[][] faces = new int[][]
            {
                new[]{0,1,2,3}, new[]{4,7,6,5}, new[]{0,4,5,1},
                new[]{2,6,7,3}, new[]{1,5,6,2}, new[]{4,0,3,7}
            };

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();

            foreach (var face in faces)
            {
                // Calculate face normal
                Vector3 v0 = corners[face[0]];
                Vector3 v1 = corners[face[1]];
                Vector3 v2 = corners[face[2]];
                Vector3 normal = (v1 - v0).Cross(v2 - v0).Normalized();

                // Triangulate quad (0,1,2,3) -> (0,1,2) and (0,2,3)
                int[] triIndices = { 0, 1, 2, 0, 2, 3 };
                Vector2[] faceUvs = { new(0,0), new(1,0), new(1,1), new(0,1) };

                foreach (int idx in triIndices)
                {
                    vertices.Add(corners[face[idx]]);
                    uvs.Add(faceUvs[idx]);
                    normals.Add(normal);
                }
            }

            var mesh = new ArrayMesh();
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)RenderingServer.ArrayType.Max);
            arrays[(int)RenderingServer.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)RenderingServer.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)RenderingServer.ArrayType.Normal] = normals.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private ArrayMesh GenerateBogie()
        {
            // Simplified bogie frame
            float w = 1.8f, h = 0.4f, l = 2.5f;
            Vector3[] corners = new Vector3[8]
            {
                new(-w*0.5f, -h*0.5f, -l*0.5f),
                new(w*0.5f, -h*0.5f, -l*0.5f),
                new(w*0.5f, h*0.5f, -l*0.5f),
                new(-w*0.5f, h*0.5f, -l*0.5f),
                new(-w*0.5f, -h*0.5f, l*0.5f),
                new(w*0.5f, -h*0.5f, l*0.5f),
                new(w*0.5f, h*0.5f, l*0.5f),
                new(-w*0.5f, h*0.5f, l*0.5f)
            };

            int[][] faces = new int[][]
            {
                new[]{0,1,2,3}, new[]{4,7,6,5}, new[]{0,4,5,1},
                new[]{2,6,7,3}, new[]{1,5,6,2}, new[]{4,0,3,7}
            };

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();

            foreach (var face in faces)
            {
                Vector3 v0 = corners[face[0]];
                Vector3 v1 = corners[face[1]];
                Vector3 v2 = corners[face[2]];
                Vector3 normal = (v1 - v0).Cross(v2 - v0).Normalized();

                int[] triIndices = { 0, 1, 2, 0, 2, 3 };
                Vector2[] faceUvs = { new(0,0), new(1,0), new(1,1), new(0,1) };

                foreach (int idx in triIndices)
                {
                    vertices.Add(corners[face[idx]]);
                    uvs.Add(faceUvs[idx]);
                    normals.Add(normal);
                }
            }

            var mesh = new ArrayMesh();
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)RenderingServer.ArrayType.Max);
            arrays[(int)RenderingServer.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)RenderingServer.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)RenderingServer.ArrayType.Normal] = normals.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private ArrayMesh GeneratePantograph()
        {
            // Simple pantograph shape (diamond) - 4 vertices, 2 triangles
            var vertices = new Vector3[]
            {
                new(0, 0, 0),
                new(0.5f, 1.0f, 0),
                new(0, 2.0f, 0),
                new(-0.5f, 1.0f, 0)
            };

            var uvs = new Vector2[]
            {
                new(0.5f, 0.5f),
                new(1, 0.5f),
                new(0.5f, 1),
                new(0, 0.5f)
            };

            var normals = new Vector3[]
            {
                new(0, 0, 1), new(0, 0, 1), new(0, 0, 1), new(0, 0, 1)
            };

            // Two triangles: 0,1,2 and 0,2,3
            var indices = new int[] { 0, 1, 2, 0, 2, 3 };

            var mesh = new ArrayMesh();
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)RenderingServer.ArrayType.Max);
            arrays[(int)RenderingServer.ArrayType.Vertex] = vertices;
            arrays[(int)RenderingServer.ArrayType.TexUV] = uvs;
            arrays[(int)RenderingServer.ArrayType.Normal] = normals;
            arrays[(int)RenderingServer.ArrayType.Index] = indices;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private void CreateWheels()
        {
            float wheelRadius = 0.43f; // Standard 860mm diameter
            float wheelWidth = 0.13f;
            float axleWidth = 1.6f;
            float bogieZ = Spec.Length * 0.4f;

            // 4 wheels per bogie (2 axles)
            Vector3[] positions = new Vector3[8]
            {
                new(-axleWidth*0.5f, -1.2f + wheelRadius, -bogieZ),
                new(axleWidth*0.5f, -1.2f + wheelRadius, -bogieZ),
                new(-axleWidth*0.5f, -1.2f + wheelRadius, -bogieZ + 1.5f),
                new(axleWidth*0.5f, -1.2f + wheelRadius, -bogieZ + 1.5f),
                new(-axleWidth*0.5f, -1.2f + wheelRadius, bogieZ),
                new(axleWidth*0.5f, -1.2f + wheelRadius, bogieZ),
                new(-axleWidth*0.5f, -1.2f + wheelRadius, bogieZ - 1.5f),
                new(axleWidth*0.5f, -1.2f + wheelRadius, bogieZ - 1.5f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var wheel = new MeshInstance3D { Mesh = GenerateWheel(wheelRadius, wheelWidth) };
                wheel.Position = positions[i];
                wheel.RotationDegrees = new Vector3(90, 0, 0); // Rotate to stand upright
                AddChild(wheel);
                _wheelMeshes.Add(wheel);
            }
        }

        private ArrayMesh GenerateWheel(float radius, float width)
        {
            int segments = QualityLevel >= 2 ? 16 : 8;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();

            // Generate cylinder side faces
            for (int i = 0; i < segments; i++)
            {
                float a1 = (float)i / segments * 2 * MathF.PI;
                float a2 = (float)(i + 1) / segments * 2 * MathF.PI;

                Vector3 p1 = new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0) * radius;
                Vector3 p2 = new Vector3(MathF.Cos(a2), MathF.Sin(a2), 0) * radius;

                // Side face - two triangles
                Vector3 v1 = new Vector3(p1.X, p1.Y, -width * 0.5f);
                Vector3 v2 = new Vector3(p2.X, p2.Y, -width * 0.5f);
                Vector3 v3 = new Vector3(p2.X, p2.Y, width * 0.5f);
                Vector3 v4 = new Vector3(p1.X, p1.Y, width * 0.5f);

                Vector3 normal = (v2 - v1).Cross(v4 - v1).Normalized();

                // Triangle 1: v1, v2, v3
                vertices.Add(v1); uvs.Add(new Vector2((float)i / segments, 0)); normals.Add(normal);
                vertices.Add(v2); uvs.Add(new Vector2((float)(i + 1) / segments, 0)); normals.Add(normal);
                vertices.Add(v3); uvs.Add(new Vector2((float)(i + 1) / segments, 1)); normals.Add(normal);

                // Triangle 2: v1, v3, v4
                vertices.Add(v1); uvs.Add(new Vector2((float)i / segments, 0)); normals.Add(normal);
                vertices.Add(v3); uvs.Add(new Vector2((float)(i + 1) / segments, 1)); normals.Add(normal);
                vertices.Add(v4); uvs.Add(new Vector2((float)i / segments, 1)); normals.Add(normal);
            }

            // Generate end caps (circles)
            // Front cap (z = width * 0.5f)
            Vector3 centerFront = new Vector3(0, 0, width * 0.5f);
            Vector3 normalFront = new Vector3(0, 0, 1);
            for (int i = 0; i < segments; i++)
            {
                float a1 = (float)i / segments * 2 * MathF.PI;
                float a2 = (float)(i + 1) / segments * 2 * MathF.PI;
                Vector3 p1 = new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0) * radius;
                Vector3 p2 = new Vector3(MathF.Cos(a2), MathF.Sin(a2), 0) * radius;

                vertices.Add(centerFront); uvs.Add(new Vector2(0.5f, 0.5f)); normals.Add(normalFront);
                vertices.Add(new Vector3(p1.X, p1.Y, width * 0.5f)); uvs.Add(new Vector2((float)i / segments, 0.5f)); normals.Add(normalFront);
                vertices.Add(new Vector3(p2.X, p2.Y, width * 0.5f)); uvs.Add(new Vector2((float)(i + 1) / segments, 0.5f)); normals.Add(normalFront);
            }

            // Back cap (z = -width * 0.5f)
            Vector3 centerBack = new Vector3(0, 0, -width * 0.5f);
            Vector3 normalBack = new Vector3(0, 0, -1);
            for (int i = 0; i < segments; i++)
            {
                float a1 = (float)i / segments * 2 * MathF.PI;
                float a2 = (float)(i + 1) / segments * 2 * MathF.PI;
                Vector3 p1 = new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0) * radius;
                Vector3 p2 = new Vector3(MathF.Cos(a2), MathF.Sin(a2), 0) * radius;

                vertices.Add(centerBack); uvs.Add(new Vector2(0.5f, 0.5f)); normals.Add(normalBack);
                vertices.Add(new Vector3(p2.X, p2.Y, -width * 0.5f)); uvs.Add(new Vector2((float)(i + 1) / segments, 0.5f)); normals.Add(normalBack);
                vertices.Add(new Vector3(p1.X, p1.Y, -width * 0.5f)); uvs.Add(new Vector2((float)i / segments, 0.5f)); normals.Add(normalBack);
            }

            var mesh = new ArrayMesh();
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)RenderingServer.ArrayType.Max);
            arrays[(int)RenderingServer.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)RenderingServer.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)RenderingServer.ArrayType.Normal] = normals.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private void CreateDoorAnimations()
        {
            // Door open/close animations would be created here
        }

        public void AnimateBogies(double time, double speed)
        {
            // Bogie rotation based on curve (simplified)
            float sway = (float)(Math.Sin(time * 2.0) * 0.5 * speed / 30.0);
            _bogieFrontMesh.RotationDegrees = new Vector3(0, sway, 0);
            _bogieRearMesh.RotationDegrees = new Vector3(0, -sway, 0);
        }

        public void AnimateWheels(double time, double speed)
        {
            float rotationSpeed = (float)(speed / 0.86 * 2 * Math.PI); // radians per second
            float angle = (float)(time * rotationSpeed);

            foreach (var wheel in _wheelMeshes)
            {
                wheel.RotationDegrees = new Vector3(90, 0, angle * 180 / MathF.PI);
            }
        }

        public void SetPantographHeight(float height)
        {
            if (_pantographMesh != null)
            {
                _pantographMesh.Scale = new Vector3(1, height, 1);
            }
        }

        public void SetDoorsOpen(bool open)
        {
            // Play door animation
        }

        public void SetLights(int state)
        {
            // Update emission shader parameters
            var mat = _bodyMesh.GetSurfaceOverrideMaterial(0) as ShaderMaterial;
            if (mat != null)
            {
                mat.SetShaderParameter("use_emission", state > 0);
                mat.SetShaderParameter("emission_strength", state == 2 ? 2.0f : 1.0f);
            }
        }

        public void SetQualityLevel(int level)
        {
            QualityLevel = Math.Clamp(level, 0, 3);
            // Regenerate meshes with new LOD
        }
    }

    /// <summary>
    /// Train car specification.
    /// </summary>
    public class TrainCarSpec
    {
        public CarType Type { get; set; }
        public float Length { get; set; } = 20.0f;
        public float Width { get; set; } = 2.9f;
        public float Height { get; set; } = 4.1f;
        public float Mass { get; set; } = 35000.0f;
        public string ModelPath { get; set; }
        public string TexturePath { get; set; }
    }

    public enum CarType
    {
        Trailer,
        Motor,
        Electric,
        Locomotive,
        CabCar
    }

    /// <summary>
    /// LOD mesh set for a car type.
    /// </summary>
    public class CarLODSet
    {
        public ArrayMesh BodyLOD0 { get; set; }
        public ArrayMesh BodyLOD1 { get; set; }
        public ArrayMesh BodyLOD2 { get; set; }
        public ArrayMesh BodyLOD3 { get; set; }
        public ArrayMesh BogieLOD0 { get; set; }
        public ArrayMesh BogieLOD1 { get; set; }
        public ArrayMesh WheelLOD0 { get; set; }
        public ArrayMesh WheelLOD1 { get; set; }
    }
}