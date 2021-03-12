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
        public static unsafe VertexArrayObject<TVertex, TIndex> CreateDynamic(bool hasIndexBuffer, int vertexCapacity, int indexCapacity)
        {
            GL.CreateBuffers(1, out uint vertexBufferName);
            GL.NamedBufferStorage(vertexBufferName, Unsafe.SizeOf<TVertex>() * vertexCapacity, IntPtr.Zero,
                BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapReadBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.DynamicStorageBit);

            GL.CreateVertexArrays(1, out uint vertexArrayName);
            GL.VertexArrayVertexBuffer(vertexArrayName, 0, vertexBufferName, IntPtr.Zero, Unsafe.SizeOf<TVertex>());
            SetupFields(vertexArrayName);

            uint indexBufferName = default;
            if (hasIndexBuffer)
            {
                GL.CreateBuffers(1, out indexBufferName);
                GL.NamedBufferStorage(indexBufferName, Unsafe.SizeOf<TIndex>() * indexCapacity, IntPtr.Zero,
                    BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapReadBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.DynamicStorageBit);
            }

            return new()
            {
                HasIndexBuffer = hasIndexBuffer,
                VertexCapacity = vertexCapacity,
                IndexCapacity = indexCapacity,
                VertexBufferName = vertexBufferName,
                IndexBufferName = indexBufferName,
                Vertices = new((TVertex*)GL.MapNamedBufferRange(vertexBufferName, IntPtr.Zero, Unsafe.SizeOf<TVertex>() * vertexCapacity,
                    BufferAccessMask.MapWriteBit | BufferAccessMask.MapReadBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit).ToPointer(), vertexCapacity),
                Indices = !hasIndexBuffer ? default : new((TIndex*)GL.MapNamedBufferRange(indexBufferName, IntPtr.Zero, Unsafe.SizeOf<TIndex>() * indexCapacity,
                    BufferAccessMask.MapWriteBit | BufferAccessMask.MapReadBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit).ToPointer(), indexCapacity),
                DrawElementsType = !hasIndexBuffer ? default : drawElementsType[typeof(TIndex)],
                VertexArrayName = vertexArrayName,
            };
        }

        public static unsafe VertexArrayObject<TVertex, TIndex> CreateStatic(TVertex[] vertices, TIndex[] indices = null)
        {
            var (vertexCount, indexCount) = (vertices.Length, indices?.Length ?? 0);

            GL.CreateBuffers(1, out uint vertexBufferName);
            GL.NamedBufferStorage(vertexBufferName, Unsafe.SizeOf<TVertex>() * vertexCount, ref vertices[0], BufferStorageFlags.None);

            GL.CreateVertexArrays(1, out uint vertexArrayName);
            GL.VertexArrayVertexBuffer(vertexArrayName, 0, vertexBufferName, IntPtr.Zero, Unsafe.SizeOf<TVertex>());
            SetupFields(vertexArrayName);

            uint indexBufferName = default;
            if (indices is not null)
            {
                GL.CreateBuffers(1, out indexBufferName);
                GL.NamedBufferStorage(indexBufferName, Unsafe.SizeOf<TIndex>() * indexCount, ref indices[0], BufferStorageFlags.None);
            }

            return new()
            {
                HasIndexBuffer = indices is not null,
                VertexBufferName = vertexBufferName,
                IndexBufferName = indexBufferName,
                Vertices = new(vertexCount),
                Indices = new(indexCount),
                DrawElementsType = indices is null ? default : drawElementsType[typeof(TIndex)],
                VertexArrayName = vertexArrayName,
            };
        }

        private static unsafe void SetupFields(uint vertexArrayName)
        {
            uint idx = 0, offset = 0;
            foreach (var fi in typeof(TVertex).GetFields())
            {
                GL.EnableVertexArrayAttrib(vertexArrayName, idx);

                if (fi.GetCustomAttributes(typeof(FixedBufferAttribute), true).FirstOrDefault() is FixedBufferAttribute fba)
                    if (fba.ElementType == typeof(int))
                    {
                        GL.VertexArrayAttribIFormat(vertexArrayName, idx, fba.Length, fieldTypes[fba.ElementType], offset);
                        offset += (uint)(sizeof(int) * fba.Length);
                    }
                    else if (fba.ElementType == typeof(float))
                    {
                        GL.VertexArrayAttribFormat(vertexArrayName, idx, fba.Length, fieldTypes[fba.ElementType], false, offset);
                        offset += (uint)(sizeof(float) * fba.Length);
                    }
                    else
                        throw new NotImplementedException();
                else if (fi.FieldType != typeof(int))
                {
                    GL.VertexArrayAttribFormat(vertexArrayName, idx, fieldCounts[fi.FieldType], fieldTypes[fi.FieldType], false, offset);
                    offset += fieldSizes[fi.FieldType];
                }
                else
                    throw new NotImplementedException();

                GL.VertexArrayAttribBinding(vertexArrayName, idx, 0);

                ++idx;
            }
        }

        static readonly Dictionary<Type, DrawElementsType> drawElementsType = new()
        {
            [typeof(byte)] = DrawElementsType.UnsignedByte,
            [typeof(uint)] = DrawElementsType.UnsignedInt,
            [typeof(ushort)] = DrawElementsType.UnsignedShort,
            [typeof(sbyte)] = DrawElementsType.UnsignedByte,
            [typeof(int)] = DrawElementsType.UnsignedInt,
            [typeof(short)] = DrawElementsType.UnsignedShort,
        };

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
            [typeof(int)] = VertexAttribType.Int,
            [typeof(Vector2)] = VertexAttribType.Float,
            [typeof(Vector3)] = VertexAttribType.Float,
            [typeof(Vector4)] = VertexAttribType.Float,
        };

        static readonly Dictionary<Type, uint> fieldSizes = new()
        {
            [typeof(float)] = (uint)Unsafe.SizeOf<float>(),
            [typeof(Vector2)] = (uint)Unsafe.SizeOf<Vector2>(),
            [typeof(Vector3)] = (uint)Unsafe.SizeOf<Vector3>(),
            [typeof(Vector4)] = (uint)Unsafe.SizeOf<Vector4>(),
        };

        internal void Draw(PrimitiveType primitiveType)
        {
            GL.BindVertexArray(VertexArrayName);
            if (HasIndexBuffer)
            {
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferName);
                GL.DrawElements(primitiveType, Indices.Length, DrawElementsType, 0);
            }
            else
                GL.DrawArrays(primitiveType, 0, Vertices.Length);
        }

        uint VertexBufferName { get; init; }
        uint IndexBufferName { get; init; }
        uint VertexArrayName { get; init; }
        DrawElementsType DrawElementsType { get; init; }

        RefArray<TVertex> vertices;
        public ref RefArray<TVertex> Vertices => ref vertices;

        RefArray<TIndex> indices;
        public ref RefArray<TIndex> Indices => ref indices;

        public bool HasIndexBuffer { get; init; }
        public int VertexCapacity { get; private set; }
        public int IndexCapacity { get; private set; }
    }
}
