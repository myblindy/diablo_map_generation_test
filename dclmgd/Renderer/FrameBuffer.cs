using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Renderer
{
    class FrameBuffer
    {
        readonly uint name;

        public FrameBuffer(Texture color, Texture depth)
        {
            GL.CreateFramebuffers(1, out name);

            if (color is not null)
                throw new NotImplementedException();

            if (depth is not null)
                GL.NamedFramebufferTexture(name, FramebufferAttachment.DepthAttachment, depth.Name, 0);

            var status = GL.CheckNamedFramebufferStatus(name, FramebufferTarget.Framebuffer);
            if (status != FramebufferStatus.FramebufferComplete)
                throw new InvalidOperationException($"Framebuffer incomplete: {status}");
        }

        public void Bind() => GL.BindFramebuffer(FramebufferTarget.Framebuffer, name);

        public void Unbind() => GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
}
