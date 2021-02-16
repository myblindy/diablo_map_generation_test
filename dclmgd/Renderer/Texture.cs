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
    class Texture
    {
        readonly uint name;

        public unsafe Texture(string path)
        {
            using var imgData = Image.Load<Bgra32>(path);

            GL.CreateTextures(TextureTarget.Texture2D, 1, out name);
            GL.TextureStorage2D(name, 1, SizedInternalFormat.Rgba8, imgData.Width, imgData.Height);
            fixed (Bgra32* p = imgData.GetPixelRowSpan(0))
                GL.TextureSubImage2D(name, 0, 0, 0, imgData.Width, imgData.Height, PixelFormat.Bgra, PixelType.UnsignedByte, new IntPtr(p));
        }

        public void Bind() => GL.BindTexture(TextureTarget.Texture2D, name);
    }
}
