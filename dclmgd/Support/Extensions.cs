using System;
using System.Collections.Generic;
using System.Linq;
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

        public static T Next<T>(this Random rng, IList<T> list) =>
            list[rng.Next(list.Count)];

        public static T Next<T>(this Random rng, ISet<T> set) =>
            set.ElementAt(rng.Next(set.Count));

        static (T foundVal, int wantedIdx) NextRandom<T>(Random rng, Func<(T, bool)> next, int idx)
        {
            var (val, exists) = next();

            if (!exists)
                return (default, rng.Next(idx));

            var (foundVal, wantedIdx) = NextRandom(rng, next, idx + 1);
            return wantedIdx == idx ? (val, wantedIdx) : (default, wantedIdx);
        }

        public static T Next<T>(this Random rng, IEnumerable<T> list)
        {
            using var enumerator = list.GetEnumerator();
            return NextRandom(rng, () => { var hasNext = enumerator.MoveNext(); return hasNext ? (enumerator.Current, true) : (default, false); }, 0).foundVal;
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
                list.Add(item);
        }

        public static void AddRange<T>(this ISet<T> set, IEnumerable<T> items)
        {
            foreach (var item in items)
                set.Add(item);
        }

        public static void RemoveRange<T>(this ISet<T> set, IEnumerable<T> items)
        {
            foreach (var item in items)
                set.Remove(item);
        }

        public static T[] ToArray<T>(this IEnumerable<T> source, int length)
        {
            var arr = new T[length];

            int idx = 0;
            foreach (var item in source)
                arr[idx++] = item;

            return arr;
        }

        public static T[] ToArray<T>(this IEnumerable<T> source, int length, T fill)
        {
            var arr = new T[length];

            int idx = 0;
            foreach (var item in source)
                arr[idx++] = item;

            for (; idx < length; ++idx)
                arr[idx] = fill;

            return arr;
        }

        public static TValue[] ToArraySequentialBy<TSource, TValue>(this IEnumerable<TSource> source, int length, Func<TSource, int> seqSelector, Func<TSource, TValue> valueSelector)
        {
            var arr = new TValue[length];

            foreach (var item in source)
                arr[seqSelector(item)] = valueSelector(item);

            return arr;
        }

        public static Matrix4x4 ToNumerics(this Assimp.Matrix4x4 assimpMat4x4) =>
            new(assimpMat4x4.A1, assimpMat4x4.A2, assimpMat4x4.A3, assimpMat4x4.A4,
                assimpMat4x4.B1, assimpMat4x4.B2, assimpMat4x4.B3, assimpMat4x4.B4,
                assimpMat4x4.C1, assimpMat4x4.C2, assimpMat4x4.C3, assimpMat4x4.C4,
                assimpMat4x4.D1, assimpMat4x4.D2, assimpMat4x4.D3, assimpMat4x4.D4);
    }
}
