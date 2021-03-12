using MoreLinq;
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
            matricesUbo.Data.projection = Matrix4x4.CreatePerspectiveFieldOfView(70f / 180f * MathF.PI, (float)e.Width / e.Height, .1f, 800f);
            matricesUbo.Update();
            base.OnResize(e);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MatricesUbo
        {
            public Matrix4x4 projection, view;
        }
        UniformBufferObject<MatricesUbo> matricesUbo;

        Camera camera;
        MeshModel heroMeshModel, wallsMeshModel;

        const int shadowMapResolution = 1024;
        TextureCubeMap shadowMap;
        FrameBuffer shadowFrameBuffer;
        const float shadowFarPlane = 25f;
        readonly static Matrix4x4 shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2f, 1f, 1f, shadowFarPlane);

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
            heroMeshModel.CurrentAnimationName = "";
            heroMeshModel.Update(0);
            wallsMeshModel = new("Data/Models/MapObjects/DemoWall");

            // load the camera ubo
            matricesUbo = new();
            camera = new(new(6, 10, 6), new(0, 4, 0), mat => { matricesUbo.Data.view = mat; matricesUbo.Update(); });

            // set the object shader light properties
            setShaderLight(ShaderProgramCache.Get("object-bones"));
            setShaderLight(ShaderProgramCache.Get("object-normal"));

            static void setShaderLight(ShaderProgram shader)
            {
                shader.Set("light.farPlane", shadowFarPlane);
                shader.Set("light.depthMap", 0);
                shader.Set("light.ambient", new Vector3(.2f, .2f, .2f));
                shader.Set("light.diffuse", new Vector3(.5f, .5f, .5f));
                shader.Set("light.specular", new Vector3(1f, 1f, 1f));
                shader.Set("light.constant", 1.0f);
                shader.Set("light.linear", 0.01f);
                shader.Set("light.quadratic", 0.001f);
            }

            // set the object shader shadow properties
            var objectShadowShader = ShaderProgramCache.Get("object-shadow");
            objectShadowShader.Set("farPlane", shadowFarPlane);

            // shadow map
            shadowMap = new(shadowMapResolution, shadowMapResolution, TextureStorageType.DepthOnly, TextureFilteringType.NearestMinNearestMag, TextureClampingType.ClampToEdge);
            shadowFrameBuffer = new(null, shadowMap);

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

            heroMeshModel.Update(args.Time);
        }

        double time;
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            time += args.Time;

            const float lightSpeedMultiplier = 1f;
            var lightPosition = new Vector3(MathF.Sin((float)(time * lightSpeedMultiplier)) * 5f, 4f, MathF.Cos((float)(time * lightSpeedMultiplier)) * 5f);

            void RenderScene(bool shadowPass)
            {
                heroMeshModel.Draw(shadowPass);
                wallsMeshModel.Draw(shadowPass);
            }

            // first pass: shadows
            GL.Viewport(0, 0, shadowMapResolution, shadowMapResolution);
            shadowFrameBuffer.Bind();
            GL.ReadBuffer(ReadBufferMode.None);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            var shadowTransforms = new[]
            {
                Matrix4x4.CreateLookAt(lightPosition, lightPosition + new Vector3(1, 0, 0), new(0, -1, 0)) * shadowProjection,
                Matrix4x4.CreateLookAt(lightPosition, lightPosition + new Vector3(-1, 0, 0), new(0, -1, 0)) * shadowProjection,
                Matrix4x4.CreateLookAt(lightPosition, lightPosition + new Vector3(0, 1, 0), new(0, 0, 1)) * shadowProjection,
                Matrix4x4.CreateLookAt(lightPosition, lightPosition + new Vector3(0, -1, 0), new(0, 0, -1)) * shadowProjection,
                Matrix4x4.CreateLookAt(lightPosition, lightPosition + new Vector3(0, 0, 1), new(0, -1, 0)) * shadowProjection,
                Matrix4x4.CreateLookAt(lightPosition, lightPosition + new Vector3(0, 0, -1), new(0, -1, 0)) * shadowProjection,
            };
            var objectShadowShader = ShaderProgramCache.Get("object-shadow");
            shadowTransforms.ForEach((t, idx) => objectShadowShader.Set($"shadowMatrices[{idx}]", ref t, false));
            objectShadowShader.Set("lightPos", ref lightPosition);

            RenderScene(true);
            FrameBuffer.Unbind();

            // second pass: the actual scene
            GL.Viewport(0, 0, Size.X, Size.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            matricesUbo.Bind(0);
            setShaderPositions(ShaderProgramCache.Get("object-bones"));
            setShaderPositions(ShaderProgramCache.Get("object-normal"));

            void setShaderPositions(ShaderProgram shader)
            {
                shader.Set("view_position", ref camera.Position);
                shader.Set("light.position", ref lightPosition);
            }

            shadowMap.Bind(0);
            RenderScene(false);

            SwapBuffers();
        }
    }
}
