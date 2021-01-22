using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Renderer
{
    class UniformBufferObject<T> where T : unmanaged
    {
        public int Name { get; }

        T data;
        public ref T Data => ref data;

        public UniformBufferObject()
        {
            GL.CreateBuffers(1, out int name);
            Name = name;
            GL.NamedBufferData(Name, Unsafe.SizeOf<T>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }

        public void Update() =>
            GL.NamedBufferSubData(Name, IntPtr.Zero, Unsafe.SizeOf<T>(), ref data);

        public void Bind(int bindingPoint) =>
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, bindingPoint, Name);
    }
}
