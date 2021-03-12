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
                        var imageInfo = Image.Identify(path);
                        if (imageInfo.PixelType.BitsPerPixel == 24 && imageInfo.PixelType.AlphaRepresentation != PixelAlphaRepresentation.Unassociated)
                        {
                            using var img = Image.Load<Bgr24>(path);
                            GL.TextureStorage2D(Name, 1, (SizedInternalFormat)All.Rgb8, img.Width, img.Height);
                            fixed (Bgr24* p = img.GetPixelRowSpan(0))
                                GL.TextureSubImage2D(Name, 0, 0, 0, img.Width, img.Height, PixelFormat.Bgr, PixelType.UnsignedByte, new IntPtr(p));
                        }
                        else if (imageInfo.PixelType.BitsPerPixel == 24 || imageInfo.PixelType.BitsPerPixel == 32)
                        {
                            using var img = Image.Load<Bgra32>(path);
                            GL.TextureStorage2D(Name, 1, (SizedInternalFormat)All.Rgba8, img.Width, img.Height);
                            fixed (Bgra32* p = img.GetPixelRowSpan(0))
                                GL.TextureSubImage2D(Name, 0, 0, 0, img.Width, img.Height, PixelFormat.Bgra, PixelType.UnsignedByte, new IntPtr(p));
                        }
                        else
                            throw new NotImplementedException();
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
                        GL.TextureStorage2D(Name, 1, (SizedInternalFormat)All.DepthComponent32f, width, height);
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
