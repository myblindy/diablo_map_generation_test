using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Renderer
{
    enum TextureStorageType { Rgbx, DepthOnly }
    enum TextureFilteringType { NearestMinNearestMag, LinearMinLinearMag }
    enum TextureClampingType { ClampToEdge, Repeat }

    abstract class Texture
    {
        public uint Name { get; protected set; }

        protected void SetupFiltering(TextureFilteringType filtering)
        {
            var minFilter = filtering switch
            {
                TextureFilteringType.NearestMinNearestMag => (int)TextureMinFilter.Nearest,
                TextureFilteringType.LinearMinLinearMag => (int)TextureMinFilter.Linear,
                _ => throw new NotImplementedException(),
            };
            var magFilter = filtering switch
            {
                TextureFilteringType.NearestMinNearestMag => (int)TextureMinFilter.Nearest,
                TextureFilteringType.LinearMinLinearMag => (int)TextureMinFilter.Linear,
                _ => throw new NotImplementedException(),
            };
            GL.TextureParameterI(Name, TextureParameterName.TextureMinFilter, ref minFilter);
            GL.TextureParameterI(Name, TextureParameterName.TextureMagFilter, ref magFilter);
        }

        protected void SetupClamping(TextureClampingType clamping)
        {
            var wrap = clamping switch
            {
                TextureClampingType.ClampToEdge => (int)TextureWrapMode.ClampToEdge,
                TextureClampingType.Repeat => (int)TextureWrapMode.Repeat,
                _ => throw new NotImplementedException(),
            };
            GL.TextureParameterI(Name, TextureParameterName.TextureWrapS, ref wrap);
            GL.TextureParameterI(Name, TextureParameterName.TextureWrapT, ref wrap);
            GL.TextureParameterI(Name, TextureParameterName.TextureWrapR, ref wrap);
        }

        public abstract void Bind(int unit = 0);
    }

    class Texture2D : Texture
    {
        public unsafe Texture2D(string path, TextureStorageType type, TextureFilteringType filtering, TextureClampingType clamping)
        {
            GL.CreateTextures(TextureTarget.Texture2D, 1, out uint name);
            Name = name;

            switch (type)
            {
                case TextureStorageType.Rgbx:
                    {
                        using var imgData = Image.Load<Bgra32>(path);
                        GL.TextureStorage2D(Name, 1, SizedInternalFormat.Rgba8, imgData.Width, imgData.Height);
                        fixed (Bgra32* p = imgData.GetPixelRowSpan(0))
                            GL.TextureSubImage2D(Name, 0, 0, 0, imgData.Width, imgData.Height, PixelFormat.Bgra, PixelType.UnsignedByte, new IntPtr(p));
                        break;
                    }

                default:
                    throw new NotImplementedException();
            }

            SetupFiltering(filtering);
            SetupClamping(clamping);
        }

        public override void Bind(int unit = 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2D, Name);
        }
    }

    class TextureCubeMap : Texture
    {
        public TextureCubeMap(int width, int height, TextureStorageType type, TextureFilteringType filtering, TextureClampingType clamping)
        {
            GL.CreateTextures(TextureTarget.TextureCubeMap, 1, out uint name);
            Name = name;

            switch (type)
            {
                case TextureStorageType.DepthOnly:
                    {
                        GL.TextureStorage2D(Name, 1, (SizedInternalFormat)OpenTK.Graphics.ES30.SizedDepthStencilFormat.DepthComponent32f, width, height);
                        break;
                    }

                default:
                    throw new NotImplementedException();
            }

            SetupFiltering(filtering);
            SetupClamping(clamping);
        }

        public override void Bind(int unit = 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.TextureCubeMap, Name);
        }
    }
}
