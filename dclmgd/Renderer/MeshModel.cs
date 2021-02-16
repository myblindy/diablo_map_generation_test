using Assimp;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
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

        readonly VertexArrayObject<Vertex, Nothing> vao;
        readonly Texture texture;
        readonly ShaderProgram program;

        public MeshModel(string path)
        {
            var scene = new AssimpContext().ImportFile(path, PostProcessSteps.FlipUVs);

            var mesh = scene.Meshes[0];
            vao = new(false, mesh.Vertices.Count, 0);
            vao.Vertices.AddRange(
                mesh.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z))
                    .Zip(mesh.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)), (v, n) => (v, n))
                    .Zip(mesh.TextureCoordinateChannels[0].Select(v => new Vector2(v.X, v.Y)), (w, uv) => (w.v, w.n, uv))
                    .Select(w => new Vertex { Position = w.v, Normal = w.n, UV = w.uv }));

            var sceneTexture = scene.Materials[mesh.MaterialIndex].TextureDiffuse;
            texture = new(Path.Combine(Path.GetDirectoryName(path), sceneTexture.FilePath));

            program = new("object");
            program.UniformBlockBind("matrices", 0);
            program.Set("world", Matrix4x4.Identity);
            program.Set("diffuseTexture", 0);
        }

        public void Draw()
        {
            texture.Bind();
            program.Use();
            vao.Draw(PrimitiveType.Triangles);
        }
    }
}
