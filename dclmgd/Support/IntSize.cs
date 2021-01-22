using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dclmgd.Support
{
    [DebuggerDisplay("{Width}-{Height}")]
    public struct IntSize : IEquatable<IntSize>
    {
        public int Width;
        public int Height;

        public IntSize(int width, int height) => (Width, Height) = (width, height);

        public void SwapInPlace() => (Width, Height) = (Height, Width);

        public void SortInPlace() { if (Width > Height) SwapInPlace(); }
        public void SortDescInPlace() { if (Width < Height) SwapInPlace(); }

        public override bool Equals(object obj) => obj is IntSize size && Equals(size);

        public bool Equals(IntSize other) => Width == other.Width && Height == other.Height;

        public override int GetHashCode() => HashCode.Combine(Width, Height);

        public static bool operator ==(IntSize left, IntSize right) => left.Equals(right);

        public static bool operator !=(IntSize left, IntSize right) => !(left == right);
    }

    public class IntSizeJsonConverter : JsonConverter<IntSize>
    {
        public override IntSize Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            var m = Regex.Match(s, @"^(\d+)x(\d+)$");
            return m.Success ? new(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))
                : throw new InvalidOperationException();
        }

        public override void Write(Utf8JsonWriter writer, IntSize value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
