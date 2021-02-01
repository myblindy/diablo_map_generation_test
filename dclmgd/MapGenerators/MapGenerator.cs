using dclmgd.Support;
using JM.LinqFaster;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace dclmgd.MapGenerators
{
    public record MapCell(int X, int Y, int Width, int Height, BitArray64 DoorsNorth, BitArray64 DoorsSouth, BitArray64 DoorsEast, BitArray64 DoorsWest)
    {
    }

    abstract class MapGenerator
    {
        public MapCell[] MapCells { get; protected set; }
        public IntSize Size { get; protected set; }

        protected static readonly Dictionary<string, Func<MapTemplateData, MapGenerator>> generators =
            Assembly.GetEntryAssembly().GetTypes()
                .Where(t => t.IsAssignableTo(typeof(MapGenerator)))
                .ToDictionary(t => t.Name[..^"Generator".Length], t =>
                {
                    var constructor = t.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();
                    return (Func<MapTemplateData, MapGenerator>)(data => (MapGenerator)constructor.Invoke(new object[] { data }));
                }, StringComparer.InvariantCultureIgnoreCase);

        protected readonly List<string> log = new();

        protected class MapTemplateData
        {
            public string Name { get; set; }
            public string Generator { get; set; }
            public IncRange Width { get; set; }
            public IncRange Height { get; set; }
            public MapCellTemplateData[] CellTemplates { get; set; }
        }

        protected class MapCellTemplateData
        {
            public string Name { get; set; }
            public IntSize Size { get; set; }
            public double Weight { get; set; } = 1;
            public int MaximumCount { get; set; } = int.MaxValue;
            public bool[] Doors { get; set; }

            [JsonIgnore]
            public ReadOnlySpan<bool> DoorsNorth => Doors.Slice(0, Size.Width);
            [JsonIgnore]
            public ReadOnlySpan<bool> DoorsEast => Doors.Slice(Size.Width, Size.Height);
            [JsonIgnore]
            public ReadOnlySpan<bool> DoorsSouth => Doors.Slice(Size.Width + Size.Height, Size.Width);
            [JsonIgnore]
            public ReadOnlySpan<bool> DoorsWest => Doors.Slice(2 * Size.Width + Size.Height, Size.Height);
        }

        static JsonSerializerOptions BuildJsonSerializerOptions() => new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(), new IncRangeJsonConverter(), new IntSizeJsonConverter() },
        };

        public static MapGenerator Generate(string mapName)
        {
            JsonSerializerOptions options = BuildJsonSerializerOptions();
            var data = JsonSerializer.Deserialize<MapTemplateData>(File.ReadAllText($"Data/Maps/{mapName}/def.json"), options);
            data.CellTemplates = Directory.EnumerateFiles($"Data/Maps/{mapName}", "cell*.json", SearchOption.TopDirectoryOnly)
                .Select(path =>
                {
                    var cellData = JsonSerializer.Deserialize<MapCellTemplateData>(File.ReadAllText(path), options);
                    cellData.Name = Path.GetFileNameWithoutExtension(path);
                    return cellData;
                })
                .ToArray();

            return generators[data.Generator](data);
        }

        protected class StopwatchMessageList : IDisposable
        {
            readonly Stopwatch stopwatch = Stopwatch.StartNew();
            readonly string text;
            readonly List<string> log;

            public StopwatchMessageList(string text, MapGenerator gen) => (this.text, log) = (text, gen.log);

            public void Dispose() => log.Add($"{text} took {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
