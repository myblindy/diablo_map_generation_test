using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Runtime.InteropServices;
using System.Text;

using Matrix4x4 = System.Numerics.Matrix4x4;

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
                APIVersion = new(4, 6),
                StartFocused = true,
                StartVisible = true,
                Size = new(800, 600),
                Title = "dclmgd",
                Flags = ContextFlags.ForwardCompatible,
            })
        {
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);
            matricesUbo.Data.projection = Matrix4x4.CreatePerspectiveFieldOfView(103f / 180f * MathF.PI, (float)e.Width / e.Height, .1f, 800f);
            matricesUbo.Update();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MatricesUbo
        {
            public Matrix4x4 projection, view;
        }
        UniformBufferObject<MatricesUbo> matricesUbo;

        MeshModel heroMeshModel, wallsMeshModel;

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
            heroMeshModel = new("Data/Models/Actors/Hero");
            wallsMeshModel = new("Data/Models/MapObjects/DemoWall");

            // load the shader
            matricesUbo = new();
            LoadCameraViewMatrix();

            GL.Enable(EnableCap.DepthTest);
        }

        private void LoadCameraViewMatrix()
        {
            matricesUbo.Data.view = Matrix4x4.CreateLookAt(new(4, height, 4), new(0, 4, 0), new(0, 1, 0));
            matricesUbo.Update();
        }

        bool up, down;
        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (e.Key == Keys.Up) up = true;
            if (e.Key == Keys.Down) down = true;
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            if (e.Key == Keys.Up) up = false;
            if (e.Key == Keys.Down) down = false;
        }

        float height = 8;
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            const float delta = 0.1f;
            if (up) height += delta;
            if (down) height -= delta;
            if (up || down) LoadCameraViewMatrix();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            matricesUbo.Bind(0);
            heroMeshModel.Draw();
            wallsMeshModel.Draw();

            SwapBuffers();
        }
    }
}
