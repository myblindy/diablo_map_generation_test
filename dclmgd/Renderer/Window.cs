using Assimp;
using dclmgd.MapGenerators;
using dclmgd.Renderer;
using dclmgd.Support;
using MoreLinq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 UV;
            public Vector4 Color;

            public Vertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 color) =>
                (Position, Normal, UV, Color) = (position, normal, uv, color);
        }
        VertexArrayObject<Vertex, Nothing> vao;

        [StructLayout(LayoutKind.Sequential)]
        struct MatricesUbo
        {
            public Matrix4x4 projection, view;
        }
        UniformBufferObject<MatricesUbo> matricesUbo;
        ShaderProgram program;

        MapGenerator mapGenerator;

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

            // generate the map
            mapGenerator = MapGenerator.Generate("Cells");

            var vertices = new List<Vertex>();
            void quad(Vector3 v0, Vector3 v1, Vector3 normal, Vector4 color)
            {
                if (v0.Z == v1.Z)
                {
                    vertices.Add(new(new(v0.X, v0.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v1.X, v0.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v1.Y, v0.Z), normal, new(), color));

                    vertices.Add(new(new(v1.X, v0.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v1.X, v1.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v1.Y, v0.Z), normal, new(), color));
                }
                else if (v0.Y == v1.Y)
                {
                    vertices.Add(new(new(v0.X, v0.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v1.X, v0.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v0.Y, v1.Z), normal, new(), color));

                    vertices.Add(new(new(v1.X, v0.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v1.X, v0.Y, v1.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v0.Y, v1.Z), normal, new(), color));
                }
                else if (v0.X == v1.X)
                {
                    vertices.Add(new(new(v0.X, v0.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v1.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v0.Y, v1.Z), normal, new(), color));

                    vertices.Add(new(new(v0.X, v1.Y, v0.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v1.Y, v1.Z), normal, new(), color));
                    vertices.Add(new(new(v0.X, v0.Y, v1.Z), normal, new(), color));
                }
            }

            const float cellMargin = 0.1f, cellHeight = 2f;
            Vector4 floorColor = new(.5f, 1, .5f, 1), wallColor = new(1, 0, 1, 1);
            mapGenerator.MapCells.ForEach(c =>
            {
                quad(new(c.X + cellMargin, c.Y + cellMargin, 0), new(c.X + c.Width - cellMargin, c.Y + c.Height - cellMargin, 0), new(0, 0, 1), floorColor);

                quad(new(c.X + cellMargin, c.Y + cellMargin, 0), new(c.X + c.Width - cellMargin, c.Y + cellMargin, cellHeight), new(), wallColor);
                quad(new(c.X + cellMargin, c.Y + c.Height - cellMargin, 0), new(c.X + c.Width - cellMargin, c.Y + c.Height - cellMargin, cellHeight), new(), wallColor);
                quad(new(c.X + cellMargin, c.Y + cellMargin, 0), new(c.X + cellMargin, c.Y + c.Height - cellMargin, cellHeight), new(), wallColor);
                quad(new(c.X + c.Width - cellMargin, c.Y + cellMargin, 0), new(c.X + c.Width - cellMargin, c.Y + c.Height - cellMargin, cellHeight), new(), wallColor);
            });

            vao = new(false, vertices.Count, 0);
            vao.Vertices.AddRange(vertices);

            // load the shader
            matricesUbo = new();
            matricesUbo.Data.projection = Matrix4x4.CreatePerspectiveFieldOfView(103f / 180f * MathF.PI, Size.X / Size.Y, .1f, 800f);
            matricesUbo.Data.view = Matrix4x4.CreateLookAt(
                new(mapGenerator.Size.Width / 2f + 20, mapGenerator.Size.Height / 2f + 20, 20),
                new(mapGenerator.Size.Width / 2f, mapGenerator.Size.Height / 2f, 0),
                new(0, 0, 1));
            matricesUbo.Update();

            program = new("walls");
            program.UniformBlockBind("matrices", 0);
            program.Set("world", Matrix4x4.Identity);
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
