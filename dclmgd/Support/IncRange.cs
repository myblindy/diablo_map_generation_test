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
    [DebuggerDisplay("{Start}-{End}")]
    public record IncRange(int Start, int End)
    {
        public IncRange(int val) : this(val, val) { }
    }

    public class IncRangeJsonConverter : JsonConverter<IncRange>
    {
        public override IncRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            var m = Regex.Match(s, @"^(\d+)(?:-(\d+))$");
            return m.Success ? m.Groups[2].Success ? new(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : new(int.Parse(m.Groups[1].Value))
                : throw new InvalidOperationException();
        }

        public override void Write(Utf8JsonWriter writer, IncRange value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
