using Assimp;
using dclmgd.Renderer;
using dclmgd.Support;
using MoreLinq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

using Matrix4x4 = System.Numerics.Matrix4x4;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;

namespace dclmgd.Renderer
{
    class Window : GameWindow
    {
        public Window() : base(
            new()
            {
                RenderFrequency = 60,
                UpdateFrequency = 60,
                IsMultiThreaded = false,
            }, new()
            {
                Profile = ContextProfile.Any,
                API = ContextAPI.OpenGL,
                APIVersion = new Version(4, 6),
                StartFocused = true,
                StartVisible = true,
                Size = new OpenTK.Mathematics.Vector2i(800, 600),
                Title = "dclmgd",
                Flags = ContextFlags.ForwardCompatible,
            })
        {
        }

        protected override void OnResize(ResizeEventArgs e) =>
            GL.Viewport(0, 0, e.Width, e.Height);

        static readonly AssimpContext assimpContext = new();

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 UV;

            public Vertex(Vector3 position, Vector3 normal, Vector2 uv) =>
                (Position, Normal, UV) = (position, normal, uv);
        }
        VertexArrayObject<Vertex, uint> vao;

        [StructLayout(LayoutKind.Sequential)]
        struct MatricesUbo
        {
            public Matrix4x4 projection, view;
        }
        UniformBufferObject<MatricesUbo> matricesUbo;
        ShaderProgram program;

        protected override void OnLoad()
        {
            VSync = VSyncMode.Off;

            // enable debug messages
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);

            GL.DebugMessageCallback((src, type, id, severity, len, msg, usr) =>
            {
                if (severity > DebugSeverity.DebugSeverityNotification)
                    unsafe
                    {
                        Console.WriteLine($"GL ERROR {Encoding.ASCII.GetString((byte*)msg, len)}, type: {type}, severity: {severity}, source: {src}");
                    }
            }, IntPtr.Zero);

            // load the model
            var model = assimpContext.ImportFile("Data/MapObjects/column.dae");

            var mesh = model.Meshes[0];
            var indices = mesh.GetUnsignedIndices();

            vao = new(true, mesh.VertexCount, indices.Length);
            mesh.Vertices.Select(w => new Vector3(w.X, w.Y, w.Z))
                .Zip(mesh.Normals.Select(w => new Vector3(w.X, w.Y, w.Z)))
                .Zip(mesh.TextureCoordinateChannels[0].Select(w => new Vector2(w.X, w.Y)), (a, uv) => (pos: a.First, norm: a.Second, uv))
                .ForEach(v => vao.Vertices.Add(new(v.pos, v.norm, v.uv)));
            vao.Indices.AddRange(indices);

            // load the shader
            matricesUbo = new();
            matricesUbo.Data.projection = Matrix4x4.CreatePerspectiveFieldOfView(103f / 180f * MathF.PI, Size.X / Size.Y, .1f, 800f);
            matricesUbo.Data.view = Matrix4x4.CreateLookAt(new(20, 20, 20), new(0, 0, 0), new(0, 0, 1));
            matricesUbo.Update();

            program = new("object");
            program.UniformBlockBind("matrices", 0);
            program.Set("world", model.RootNode.Transform.ToNumerics() * model.RootNode.Children[0].Transform.ToNumerics());
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            program.Use();
            matricesUbo.Bind(0);
            vao.Draw(PrimitiveType.Triangles);

            SwapBuffers();
        }
    }
}
