using Assimp;
using dclmgd.Support;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Matrix4x4 = System.Numerics.Matrix4x4;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;

namespace dclmgd.Renderer
{
    class MeshModel
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 UV;

            public Vertex(Vector3 position, Vector3 normal, Vector2 uv) =>
                (Position, Normal, UV) = (position, normal, uv);
        }

        enum TextureType { Diffuse, Normal }

        struct PerMeshData
        {
            public readonly VertexArrayObject<Vertex, ushort> vao;
            public readonly Texture diffuseTexture, normalTexture;
            public readonly ShaderProgram program;
            public readonly Matrix4x4 model;

            public PerMeshData(VertexArrayObject<Vertex, ushort> vao, Texture diffuseTexture, Texture normalTexture, ShaderProgram program, Matrix4x4 model) =>
                (this.vao, this.diffuseTexture, this.normalTexture, this.program, this.model) = (vao, diffuseTexture, normalTexture, program, model);
        }
        readonly PerMeshData[] perMeshData;

        static readonly AssimpContext assimpContext = new();

        static Texture TryLoadTexture(string meshPath, Mesh mesh, TextureType textureType)
        {
            var texturesPath = Path.Combine(meshPath, mesh.Name);
            return tryLoad("png") ?? tryLoad("jpg");

            Texture tryLoad(string ext)
            {
                var texturePath = Path.Combine(texturesPath, $"{textureType}.{ext}");
                return File.Exists(texturePath) ? new(texturePath) : null;
            }
        }

        class MaterialType
        {
            public float Shininess { get; set; } = 1;
        }
        readonly MaterialType material;

        public MeshModel(string path)
        {
            var scene = assimpContext.ImportFile(Path.Combine(path, "model.dae"), PostProcessSteps.FlipUVs | PostProcessSteps.OptimizeGraph | PostProcessSteps.OptimizeMeshes
                /*| PostProcessSteps.RemoveRedundantMaterials*/ | PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.LimitBoneWeights
                /*| PostProcessSteps.MakeLeftHanded*/ | PostProcessSteps.GenerateBoundingBoxes);

            // calculate the per-mesh transforms
            var transformsDictionary = new Dictionary<Mesh, Matrix4x4>();

            void visitTransforms(Node node, Matrix4x4 mat)
            {
                mat = node.Transform.ToNumerics() * mat;
                foreach (var meshIndex in node.MeshIndices)
                    transformsDictionary[scene.Meshes[meshIndex]] = mat;
                foreach (var child in node.Children)
                    visitTransforms(child, mat);
            }
            visitTransforms(scene.RootNode, Matrix4x4.Identity);

            perMeshData = new PerMeshData[scene.Meshes.Count];

            for (int meshIdx = 0; meshIdx < scene.Meshes.Count; ++meshIdx)
            {
                var mesh = scene.Meshes[meshIdx];
                ref PerMeshData perMeshDataSlot = ref perMeshData[meshIdx];

                // load the material json file, if any
                var materialPath = Path.Combine(path, mesh.Name, "material.json");
                if (File.Exists(materialPath))
                    material = JsonSerializer.Deserialize<MaterialType>(File.ReadAllText(materialPath));

                var indices = mesh.GetShortIndices();
                perMeshDataSlot = new(
                    vao: VertexArrayObject<Vertex, ushort>.CreateStatic(
                        mesh.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z))
                            .Zip(mesh.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)), (v, n) => (v, n))
                            .Zip(mesh.TextureCoordinateChannels[0].Select(v => new Vector2(v.X, v.Y)), (w, uv) => (w.v, w.n, uv))
                            .Select(w => new Vertex { Position = w.v, Normal = w.n, UV = w.uv })
                            .ToArray(mesh.Vertices.Count),
                        indices.Cast<ushort>().ToArray(indices.Length)),
                    TryLoadTexture(path, mesh, TextureType.Diffuse),
                    TryLoadTexture(path, mesh, TextureType.Normal),
                    program: ShaderProgramCache.Get("object", shader =>
                    {
                        shader.UniformBlockBind("matrices", 0);
                        shader.Set("material.diffuse", 0);
                    }),
                    model: transformsDictionary[mesh]);
            }
        }

        public void Draw()
        {
            foreach (var perMeshDataItem in perMeshData)
            {
                perMeshDataItem.diffuseTexture?.Bind();
                perMeshDataItem.program.Use();
                perMeshDataItem.program.Set("model", perMeshDataItem.model);
                perMeshDataItem.program.Set("material.shininess", material.Shininess);

                perMeshDataItem.vao.Draw(PrimitiveType.Triangles);
            }
        }
    }
}
