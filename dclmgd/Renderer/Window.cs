using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Numerics;
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
                Profile = ContextProfile.Core,
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

        Camera camera;

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

            // load the shader ubo
            matricesUbo = new();
            camera = new(new(4, 8, 4), new(0, 4, 0), mat => { matricesUbo.Data.view = mat; matricesUbo.Update(); });

            // set the object shader light properties
            var objectShader = ShaderProgramCache.Get("object");
            objectShader.Set("light.ambient", new Vector3(.2f, .2f, .2f));
            objectShader.Set("light.diffuse", new Vector3(.5f, .5f, .5f));
            objectShader.Set("light.specular", new Vector3(1f, 1f, 1f));
            objectShader.Set("light.constant", 1.0f);
            objectShader.Set("light.linear", 0.045f);
            objectShader.Set("light.quadratic", 0.0075f);

            GL.Enable(EnableCap.DepthTest);
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

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            const float delta = 0.1f;
            if (up) camera.Position.Y += delta;
            if (down) camera.Position.Y -= delta;
            if (up || down) camera.Update();
        }

        double time;
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            time += args.Time;
            float lightSpeedMultiplier = 1f;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            matricesUbo.Bind(0);

            var objectShader = ShaderProgramCache.Get("object");
            objectShader.Set("view_position", ref camera.Position);
            objectShader.Set("light.position",
                new Vector3(MathF.Sin((float)(time * lightSpeedMultiplier)) * 4f, 8f, MathF.Cos((float)(time * lightSpeedMultiplier)) * 4f));

            heroMeshModel.Draw();
            wallsMeshModel.Draw();

            SwapBuffers();
        }
    }
}
