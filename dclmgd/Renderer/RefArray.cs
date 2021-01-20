using System;
using System.Runtime.CompilerServices;

namespace dclmgd.Renderer
{
    unsafe struct RefArray<T> where T : unmanaged
    {
        readonly T* memory;
        readonly int elementCapacity;

        public int Length { get; set; }

        public RefArray(T* memory, int elementCapacity)
        {
            this.memory = memory;
            this.elementCapacity = elementCapacity;
            Length = 0;
        }

        public ref T this[int idx] => ref memory[idx];

        public void Clear() => Length = 0;

        public void Add(T value) => memory[Length++] = value;

        public void Add(ref T value) => memory[Length++] = value;

        public void AddRange(T[] source)
        {
            Unsafe.Copy(memory + Length, ref source[0]);
            Length += source.Length;
        }
    }
}
