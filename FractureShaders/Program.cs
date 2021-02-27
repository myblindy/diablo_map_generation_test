using Kaos.Combinatorics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using static MoreLinq.Extensions.ForEachExtension;

namespace FractureShaders
{
    class Program
    {
        class Define
        {
            public string Name { get; set; }
            public string FileNamePart { get; set; }
        }

        class Configuration
        {
            public List<Define> Defines { get; set; }
        }

        static void Main(string[] args)
        {
            var reIfDef = new Regex(@"^\s*#ifdef\s+([\w-]+)\s*$");
            var reEndIf = new Regex(@"^\s*#endif\s*$");

            Console.WriteLine($"Fracturing shaders starting in {args[0]}.");

            var startPath = Path.Combine(args[0], @"Data/ShaderSources");
            var extensions = new[] { ".frag", ".vert", ".geom" };
            var destinationPath = Path.Combine(args[0], @"Data/Shaders");

            // read configuration
            var cfg = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(Path.Combine(startPath, "fracture.json")), new() { PropertyNameCaseInsensitive = true });
            var defineFileNames = cfg.Defines.ToDictionary(w => w.Name, w => w.FileNamePart);

            // find all files
            var allFiles = Directory.EnumerateFiles(startPath, "*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f)))
                .Select(file => (file, File.GetLastWriteTime(file)))
                .ToArray();

            // first pass, for each file track every referenced define
            var perFileDefines = allFiles
                .Select(f => (path: f.file, lines: File.ReadAllLines(f.file)))
                .Select(w => (w.path, w.lines, defs: w.lines.Select(l => reIfDef.Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct().Where(d => cfg.Defines.Any(dd => dd.Name == d)).ToHashSet()))
                .ToDictionary(w => w.path, w => w.defs);

            // group files by file name and share the superset of defines between them
            perFileDefines.GroupBy(w => (dir: Path.GetDirectoryName(w.Key), fn: Path.GetFileNameWithoutExtension(w.Key)))
                .ForEach(g =>
                {
                    var superSet = new HashSet<string>();
                    g.SelectMany(w => w.Value).ForEach(w => superSet.Add(w));
                    superSet.ForEach(def => g.ForEach(w => w.Value.Add(def)));
                });

            // parse every shader file
            foreach (var (shaderFile, date) in allFiles)
            {
                // copy the original file
                string dstPath = Path.Combine(destinationPath, Path.GetRelativePath(startPath, shaderFile));
                Directory.CreateDirectory(Path.GetDirectoryName(dstPath));

                var allDefinesUsed = perFileDefines[shaderFile];

                // for all files that use defines, fill in all combinations
                var srcLines = File.ReadAllLines(shaderFile);

                foreach (var combination in new Combination(allDefinesUsed.Count).GetRowsForAllPicks().Append(new()))
                {
                    var definedUsed = allDefinesUsed.Where((v, idx) => combination.Contains(idx)).ToHashSet();

                    string realDstPath = Path.Combine(
                        Path.GetDirectoryName(dstPath),
                        Path.GetFileNameWithoutExtension(dstPath) + string.Concat(combination.Select(cidx => $"-{defineFileNames[allDefinesUsed.ElementAt(cidx)]}")) + Path.GetExtension(dstPath));
                    if (File.Exists(realDstPath) && File.GetLastWriteTime(realDstPath) >= date) continue;

                    Console.WriteLine($"{Path.GetRelativePath(args[0], shaderFile)} => {Path.GetRelativePath(args[0], realDstPath)}");
                    using var dstStream = File.CreateText(realDstPath);

                    int nesting = 0, ignoreUntilNesting = -1;
                    foreach (var srcLine in srcLines)
                    {
                        var m = reIfDef.Match(srcLine);
                        if (m.Success)
                        {
                            ++nesting;
                            if (!definedUsed.Contains(m.Groups[1].Value))
                            {
                                // ignore all nested ifdefs
                                ignoreUntilNesting = nesting - 1;
                            }
                        }
                        else if (reEndIf.IsMatch(srcLine))
                        {
                            --nesting;

                            if (ignoreUntilNesting >= 0 && ignoreUntilNesting == nesting)
                                ignoreUntilNesting = -1;
                        }
                        else if (ignoreUntilNesting == -1)
                            dstStream.WriteLine(srcLine);
                    }
                }
            }
        }
    }
}
