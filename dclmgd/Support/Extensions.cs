using System;
using System.Collections.Generic;

using Matrix4x4 = System.Numerics.Matrix4x4;

namespace dclmgd.Support
{
    static class Extensions
    {
        public static bool Between(this int val, int min, int max) => min <= val && val <= max;

        public static void SetAll<T>(this IList<T> arr, Func<int, T> generator)
        {
            for (int i = 0; i < arr.Count; ++i)
                arr[i] = generator(i);
        }

        public static int Next(this Random rng, IncRange range) =>
            rng.Next(range.Start, range.End + 1);

        public static Matrix4x4 ToNumerics(this Assimp.Matrix4x4 assimpMat4x4) =>
            new(assimpMat4x4.A1, assimpMat4x4.A2, assimpMat4x4.A3, assimpMat4x4.A4,
                assimpMat4x4.B1, assimpMat4x4.B2, assimpMat4x4.B3, assimpMat4x4.B4,
                assimpMat4x4.C1, assimpMat4x4.C2, assimpMat4x4.C3, assimpMat4x4.C4,
                assimpMat4x4.D1, assimpMat4x4.D2, assimpMat4x4.D3, assimpMat4x4.D4);
    }
}
