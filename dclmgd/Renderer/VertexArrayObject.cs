using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Renderer
{
    struct Nothing { }

    class VertexArrayObject<TVertex, TIndex> where TVertex : unmanaged where TIndex : unmanaged
    {
        public unsafe VertexArrayObject(bool hasIndexBuffer, int vertexCapacity, int indexCapacity)
        {
            HasIndexBuffer = hasIndexBuffer;
            VertexCapacity = vertexCapacity;
            IndexCapacity = indexCapacity;

            GL.CreateBuffers(1, out vertexBufferName);
            GL.NamedBufferStorage(vertexBufferName, Unsafe.SizeOf<TVertex>() * vertexCapacity, IntPtr.Zero,
                BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapReadBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.DynamicStorageBit);
            Vertices = new((TVertex*)GL.MapNamedBufferRange(vertexBufferName, IntPtr.Zero, Unsafe.SizeOf<TVertex>() * vertexCapacity,
                BufferAccessMask.MapWriteBit | BufferAccessMask.MapReadBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit).ToPointer(), vertexCapacity);

            if (hasIndexBuffer)
            {
                GL.CreateBuffers(1, out indexBufferName);
                GL.NamedBufferStorage(indexBufferName, Unsafe.SizeOf<TIndex>() * indexCapacity, IntPtr.Zero,
                    BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapReadBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.DynamicStorageBit);
                Indices = new((TIndex*)GL.MapNamedBufferRange(indexBufferName, IntPtr.Zero, Unsafe.SizeOf<TIndex>() * indexCapacity,
                    BufferAccessMask.MapWriteBit | BufferAccessMask.MapReadBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit).ToPointer(), indexCapacity);

                drawElementsType =
                    typeof(TIndex) == typeof(byte) ? DrawElementsType.UnsignedByte :
                    typeof(TIndex) == typeof(uint) ? DrawElementsType.UnsignedInt :
                    typeof(TIndex) == typeof(ushort) ? DrawElementsType.UnsignedShort :
                    typeof(TIndex) == typeof(sbyte) ? DrawElementsType.UnsignedByte :
                    typeof(TIndex) == typeof(int) ? DrawElementsType.UnsignedInt :
                    typeof(TIndex) == typeof(short) ? DrawElementsType.UnsignedShort :
                    throw new InvalidOperationException();
            }

            GL.CreateVertexArrays(1, out vertexArrayName);
            GL.VertexArrayVertexBuffer(vertexArrayName, 0, vertexBufferName, IntPtr.Zero, Unsafe.SizeOf<TVertex>());

            int idx = 0, offset = 0;
            foreach (var fi in typeof(TVertex).GetFields())
            {
                GL.EnableVertexArrayAttrib(vertexArrayName, idx);
                GL.VertexArrayAttribFormat(vertexArrayName, idx, fieldCounts[fi.FieldType], fieldTypes[fi.FieldType], false, offset);
                offset += fieldSizes[fi.FieldType];
                GL.VertexArrayAttribBinding(vertexArrayName, idx, 0);

                ++idx;
            }
        }

        static readonly Dictionary<Type, int> fieldCounts = new()
        {
            [typeof(float)] = 1,
            [typeof(Vector2)] = 2,
            [typeof(Vector3)] = 3,
            [typeof(Vector4)] = 4,
        };

        static readonly Dictionary<Type, VertexAttribType> fieldTypes = new()
        {
            [typeof(float)] = VertexAttribType.Float,
            [typeof(Vector2)] = VertexAttribType.Float,
            [typeof(Vector3)] = VertexAttribType.Float,
            [typeof(Vector4)] = VertexAttribType.Float,
        };

        static readonly Dictionary<Type, int> fieldSizes = new()
        {
            [typeof(float)] = Unsafe.SizeOf<float>(),
            [typeof(Vector2)] = Unsafe.SizeOf<Vector2>(),
            [typeof(Vector3)] = Unsafe.SizeOf<Vector3>(),
            [typeof(Vector4)] = Unsafe.SizeOf<Vector4>(),
        };

        internal void Draw(PrimitiveType primitiveType)
        {
            GL.BindVertexArray(vertexArrayName);
            if (HasIndexBuffer)
            {
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferName);
                GL.DrawElements(primitiveType, Indices.Length, drawElementsType, 0);
            }
            else
                GL.DrawArrays(primitiveType, 0, Vertices.Length);
        }

        readonly int vertexBufferName, indexBufferName, vertexArrayName;
        readonly DrawElementsType drawElementsType;

        RefArray<TVertex> vertices;
        public ref RefArray<TVertex> Vertices => ref vertices;

        RefArray<TIndex> indices;
        public ref RefArray<TIndex> Indices => ref indices;

        public bool HasIndexBuffer { get; }
        public int VertexCapacity { get; private set; }
        public int IndexCapacity { get; private set; }
    }
}
