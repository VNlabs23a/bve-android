using System;
using System.Collections.Generic;
using Godot;
using OpenBVEAndroid.System;

namespace OpenBVEAndroid.Rendering
{
    /// <summary>
    /// Track mesh generation and rendering.
    /// Generates procedural track geometry from TrackElement data.
    /// </summary>
    public partial class TrackRenderer : Node3D
    {
        [Export] public string TrackPath { get; set; } = "";
        [Export] public int QualityLevel { get; set; } = 2; // 0=Low, 1=Medium, 2=High, 3=Ultra

        private Track _track;
        public Track Track => _track;

        // Shader variants
        private Shader _basicShader;
        private Shader _transparentShader;
        private Shader _doubleSidedShader;
        private Shader _doubleTransShader;

        // Mesh data
        private ArrayMesh _railMesh;
        private ArrayMesh _sleeperMesh;
        private ArrayMesh _ballastMesh;

        // Materials per quality
        private Material _railMaterial;
        private Material _sleeperMaterial;
        private Material _ballastMaterial;

        // LOD settings
        private readonly float[] _lodDistances = { 50f, 150f, 400f, 1000f };
        private readonly int[] _lodSegments = { 1, 2, 4, 8 };

        // Mesh building data
        private List<Vector3> _railVertices = new();
        private List<Vector2> _railUvs = new();
        private List<Vector3> _railNormals = new();
        private List<Vector3> _sleeperVertices = new();
        private List<Vector2> _sleeperUvs = new();
        private List<Vector3> _sleeperNormals = new();
        private List<Vector3> _ballastVertices = new();
        private List<Vector2> _ballastUvs = new();
        private List<Vector3> _ballastNormals = new();

        public override void _Ready()
        {
            LoadShaders();
            CreateMaterials();

            // Try to load track from path if specified
            if (!string.IsNullOrEmpty(TrackPath))
            {
                LoadTrackFromPath(TrackPath);
            }
        }

        public void LoadTrackFromPath(string path)
        {
            var vfs = VirtualFS.Instance;
            if (vfs == null) return;

            string realPath = vfs.Resolve(path);
            if (realPath == null) return;

            // In a real implementation, this would parse the route file
            // For now, we'll rely on the simulation to set the track
        }

        public void SetTrack(Track track)
        {
            _track = track;
            if (_track != null)
            {
                GenerateTrackMesh();
            }
        }

        private void LoadShaders()
        {
            _basicShader = GD.Load<Shader>("res://shaders/track_basic.gdshader");
            _transparentShader = GD.Load<Shader>("res://shaders/track_transparent.gdshader");
            _doubleSidedShader = GD.Load<Shader>("res://shaders/track_double_sided.gdshader");
            _doubleTransShader = GD.Load<Shader>("res://shaders/track_double_trans.gdshader");
        }

        private void CreateMaterials()
        {
            _railMaterial = new ShaderMaterial { Shader = _basicShader };
            _sleeperMaterial = new ShaderMaterial { Shader = _basicShader };
            _ballastMaterial = new ShaderMaterial { Shader = _basicShader };

            // Set default textures (will be replaced by actual textures)
            var defaultImage = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            var defaultTex = ImageTexture.CreateFromImage(defaultImage);
            ((ShaderMaterial)_railMaterial).SetShaderParameter("day_texture", defaultTex);
            ((ShaderMaterial)_sleeperMaterial).SetShaderParameter("day_texture", defaultTex);
            ((ShaderMaterial)_ballastMaterial).SetShaderParameter("day_texture", defaultTex);
        }

        public void GenerateTrackMesh()
        {
            if (Track == null || Track.Elements.Length == 0)
                return;

            // Clear mesh building data
            _railVertices.Clear(); _railUvs.Clear(); _railNormals.Clear();
            _sleeperVertices.Clear(); _sleeperUvs.Clear(); _sleeperNormals.Clear();
            _ballastVertices.Clear(); _ballastUvs.Clear(); _ballastNormals.Clear();

            GenerateRails();
            GenerateSleepers();
            GenerateBallast();

            // Create meshes from accumulated data
            _railMesh = CreateMeshFromArrays(_railVertices, _railUvs, _railNormals);
            _sleeperMesh = CreateMeshFromArrays(_sleeperVertices, _sleeperUvs, _sleeperNormals);
            _ballastMesh = CreateMeshFromArrays(_ballastVertices, _ballastUvs, _ballastNormals);

            // Create MeshInstance3D children for each mesh type
            CreateMeshInstances();
        }

        private ArrayMesh CreateMeshFromArrays(List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals)
        {
            var mesh = new ArrayMesh();
            if (vertices.Count == 0) return mesh;

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)RenderingServer.ArrayType.Max);
            arrays[(int)RenderingServer.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)RenderingServer.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)RenderingServer.ArrayType.Normal] = normals.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private void GenerateRails()
        {
            var elements = Track.Elements;
            int segments = _lodSegments[Math.Min(QualityLevel, 3)];

            for (int i = 0; i < elements.Length - 1; i++)
            {
                var a = elements[i];
                var b = elements[i + 1];

                // Skip if too far (LOD)
                float distToCamera = 0f; // Would check against camera position
                if (distToCamera > _lodDistances[QualityLevel])
                    continue;

                float halfGauge = (float)(a.Gauge * 0.5);
                float railHeight = 0.18f; // Rail height above sleepers

                // Left rail
                AddRailSegment(
                    a.Position + a.Side * -halfGauge + a.Up * railHeight + a.InaccuracyOffset,
                    b.Position + b.Side * -halfGauge + b.Up * railHeight + b.InaccuracyOffset,
                    a.Side, a.Up, a.Direction
                );

                // Right rail
                AddRailSegment(
                    a.Position + a.Side * halfGauge + a.Up * railHeight + a.InaccuracyOffset,
                    b.Position + b.Side * halfGauge + b.Up * railHeight + b.InaccuracyOffset,
                    a.Side, a.Up, a.Direction
                );
            }
        }

        private void AddRailSegment(Vector3 a, Vector3 b, Vector3 side, Vector3 up, Vector3 dir)
        {
            // Rail profile cross-section (simplified U-shape)
            var profile = new Vector2[]
            {
                new(-0.035f, 0),      // Bottom left
                new(-0.035f, 0.05f),  // Web bottom
                new(-0.015f, 0.05f),  // Web top inner
                new(-0.015f, 0.15f),  // Head inner bottom
                new(-0.05f, 0.15f),   // Head inner top
                new(0.05f, 0.15f),    // Head outer top
                new(0.015f, 0.15f),   // Head outer bottom
                new(0.015f, 0.05f),   // Web top outer
                new(0.035f, 0.05f),   // Web bottom
                new(0.035f, 0)        // Bottom right
            };

            ExtrudeProfile(profile, a, b, side, up, dir);
        }

        private void GenerateSleepers()
        {
            var elements = Track.Elements;
            float sleeperSpacing = 0.6f; // 60cm spacing
            float sleeperLength = 2.6f;
            float sleeperWidth = 0.25f;
            float sleeperHeight = 0.2f;

            for (int i = 0; i < elements.Length; i++)
            {
                var elem = elements[i];
                double trackPos = elem.Distance;

                // Generate sleepers at intervals
                int sleeperCount = (int)(25.0 / sleeperSpacing);
                for (int s = 0; s < sleeperCount; s++)
                {
                    double t = (double)s / sleeperCount;
                    var interp = TrackElement.Interpolate(ref elem, ref elements[Math.Min(i + 1, elements.Length - 1)], t);

                    Vector3 pos = interp.Position + interp.InaccuracyOffset;
                    Vector3 side = interp.Side;
                    Vector3 up = interp.Up;

                    // Sleeper box
                    AddBox(pos, side * (sleeperLength * 0.5f), up * sleeperHeight, side * sleeperWidth);
                }
            }
        }

        private void GenerateBallast()
        {
            var elements = Track.Elements;
            float ballastWidth = 4.0f;
            float ballastDepth = 0.5f;
            float shoulderWidth = 1.5f;

            for (int i = 0; i < elements.Length - 1; i++)
            {
                var a = elements[i];
                var b = elements[i + 1];

                Vector3 centerA = a.Position + a.InaccuracyOffset;
                Vector3 centerB = b.Position + b.InaccuracyOffset;
                Vector3 sideA = a.Side;
                Vector3 sideB = b.Side;
                Vector3 upA = a.Up;
                Vector3 upB = b.Up;

                // Ballast cross-section: trapezoid
                var profile = new (Vector3 offset, float u)[]
                {
                    (sideA * -ballastWidth, 0),                    // Left shoulder outer
                    (sideA * -(ballastWidth - shoulderWidth), 0.25f), // Left shoulder inner
                    (sideA * -1.5f, 0.5f),                          // Ballast edge left
                    (Vector3.Zero, 0.5f),                           // Center
                    (sideA * 1.5f, 0.5f),                           // Ballast edge right
                    (sideA * (ballastWidth - shoulderWidth), 0.75f), // Right shoulder inner
                    (sideA * ballastWidth, 1.0f)                    // Right shoulder outer
                };

                ExtrudeProfile3D(profile, centerA, centerB, upA, upB, sideA, sideB, -ballastDepth);
            }
        }

        private void ExtrudeProfile(Vector2[] profile, Vector3 a, Vector3 b, Vector3 side, Vector3 up, Vector3 dir)
        {
            int count = profile.Length;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uvs = new Vector2[count * 2];

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = side * profile[i].X + up * profile[i].Y;
                vertices[i * 2] = a + offset;
                vertices[i * 2 + 1] = b + offset;

                uvs[i * 2] = new Vector2(profile[i].X + 0.5f, 0);
                uvs[i * 2 + 1] = new Vector2(profile[i].X + 0.5f, 1);
            }

            // Calculate normal for the rail (pointing up)
            Vector3 normal = up;

            // Create triangles
            for (int i = 0; i < count - 1; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = (i + 1) * 2;
                int i3 = (i + 1) * 2 + 1;

                // Two triangles per quad: (i0, i2, i1) and (i1, i2, i3)
                _railVertices.Add(vertices[i0]); _railUvs.Add(uvs[i0]); _railNormals.Add(normal);
                _railVertices.Add(vertices[i2]); _railUvs.Add(uvs[i2]); _railNormals.Add(normal);
                _railVertices.Add(vertices[i1]); _railUvs.Add(uvs[i1]); _railNormals.Add(normal);

                _railVertices.Add(vertices[i1]); _railUvs.Add(uvs[i1]); _railNormals.Add(normal);
                _railVertices.Add(vertices[i2]); _railUvs.Add(uvs[i2]); _railNormals.Add(normal);
                _railVertices.Add(vertices[i3]); _railUvs.Add(uvs[i3]); _railNormals.Add(normal);
            }
        }

        private void ExtrudeProfile3D((Vector3 offset, float u)[] profile, Vector3 a, Vector3 b, Vector3 upA, Vector3 upB, Vector3 sideA, Vector3 sideB, float depth)
        {
            int count = profile.Length;
            var verticesA = new Vector3[count];
            var verticesB = new Vector3[count];
            var uvs = new Vector2[count * 2];

            for (int i = 0; i < count; i++)
            {
                verticesA[i] = a + profile[i].offset + upA * depth;
                verticesB[i] = b + sideA.Lerp(sideB, 0.5f) * profile[i].offset.X + upB * depth;
                uvs[i * 2] = new Vector2(profile[i].u, 0);
                uvs[i * 2 + 1] = new Vector2(profile[i].u, 1);
            }

            // Calculate normal (pointing down for ballast bottom)
            Vector3 normal = -upA; // Ballast bottom faces down

            for (int i = 0; i < count - 1; i++)
            {
                // Bottom face - two triangles
                _ballastVertices.Add(verticesA[i]); _ballastUvs.Add(uvs[i * 2]); _ballastNormals.Add(normal);
                _ballastVertices.Add(verticesA[i + 1]); _ballastUvs.Add(uvs[(i + 1) * 2]); _ballastNormals.Add(normal);
                _ballastVertices.Add(verticesB[i]); _ballastUvs.Add(uvs[i * 2 + 1]); _ballastNormals.Add(normal);

                _ballastVertices.Add(verticesB[i]); _ballastUvs.Add(uvs[i * 2 + 1]); _ballastNormals.Add(normal);
                _ballastVertices.Add(verticesA[i + 1]); _ballastUvs.Add(uvs[(i + 1) * 2]); _ballastNormals.Add(normal);
                _ballastVertices.Add(verticesB[i + 1]); _ballastUvs.Add(uvs[(i + 1) * 2 + 1]); _ballastNormals.Add(normal);
            }
        }

        private void AddBox(Vector3 center, Vector3 halfExtentX, Vector3 halfExtentY, Vector3 halfExtentZ)
        {
            // Add 6 faces of a box
            var corners = new Vector3[8]
            {
                center - halfExtentX - halfExtentY - halfExtentZ,
                center + halfExtentX - halfExtentY - halfExtentZ,
                center + halfExtentX + halfExtentY - halfExtentZ,
                center - halfExtentX + halfExtentY - halfExtentZ,
                center - halfExtentX - halfExtentY + halfExtentZ,
                center + halfExtentX - halfExtentY + halfExtentZ,
                center + halfExtentX + halfExtentY + halfExtentZ,
                center - halfExtentX + halfExtentY + halfExtentZ
            };

            int[][] faces = new int[][]
            {
                new[] {0, 1, 2, 3}, // Bottom
                new[] {4, 7, 6, 5}, // Top
                new[] {0, 4, 5, 1}, // Front
                new[] {2, 6, 7, 3}, // Back
                new[] {1, 5, 6, 2}, // Right
                new[] {4, 0, 3, 7}  // Left
            };

            Vector2[] faceUvs = new Vector2[4] { new(0,0), new(1,0), new(1,1), new(0,1) };
            // Face normals: bottom, top, front, back, right, left
            Vector3[] faceNormals = new Vector3[6]
            {
                -halfExtentY.Normalized(),  // Bottom
                halfExtentY.Normalized(),   // Top
                -halfExtentZ.Normalized(),  // Front
                halfExtentZ.Normalized(),   // Back
                halfExtentX.Normalized(),   // Right
                -halfExtentX.Normalized()   // Left
            };

            for (int faceIdx = 0; faceIdx < faces.Length; faceIdx++)
            {
                var face = faces[faceIdx];
                Vector3 normal = faceNormals[faceIdx];

                // Triangulate quad: (0,1,2) and (0,2,3)
                int[] triIndices = { 0, 1, 2, 0, 2, 3 };

                foreach (int idx in triIndices)
                {
                    _sleeperVertices.Add(corners[face[idx]]);
                    _sleeperUvs.Add(faceUvs[idx]);
                    _sleeperNormals.Add(normal);
                }
            }
        }

        private void CreateMeshInstances()
        {
            // Rails
            var railInstance = new MeshInstance3D { Mesh = _railMesh };
            railInstance.SetSurfaceOverrideMaterial(0, _railMaterial);
            AddChild(railInstance);

            // Sleepers
            if (_sleeperMesh != null && _sleeperMesh.GetSurfaceCount() > 0)
            {
                var sleeperInstance = new MeshInstance3D { Mesh = _sleeperMesh };
                sleeperInstance.SetSurfaceOverrideMaterial(0, _sleeperMaterial);
                AddChild(sleeperInstance);
            }

            // Ballast
            if (_ballastMesh != null && _ballastMesh.GetSurfaceCount() > 0)
            {
                var ballastInstance = new MeshInstance3D { Mesh = _ballastMesh };
                ballastInstance.SetSurfaceOverrideMaterial(0, _ballastMaterial);
                AddChild(ballastInstance);
            }
        }

        public void SetQualityLevel(int level)
        {
            QualityLevel = Math.Clamp(level, 0, 3);
            RegenerateMesh();
        }

        private void RegenerateMesh()
        {
            // Clear children
            foreach (var child in GetChildren())
                child.QueueFree();

            GenerateTrackMesh();
        }

        public void UpdateTextures(Texture2D railTex, Texture2D sleeperTex, Texture2D ballastTex)
        {
            if (railTex != null)
                ((ShaderMaterial)_railMaterial).SetShaderParameter("day_texture", railTex);
            if (sleeperTex != null)
                ((ShaderMaterial)_sleeperMaterial).SetShaderParameter("day_texture", sleeperTex);
            if (ballastTex != null)
                ((ShaderMaterial)_ballastMaterial).SetShaderParameter("day_texture", ballastTex);
        }
    }
}