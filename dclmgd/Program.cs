using Assimp;
using dclmgd.MapGenerators;
using dclmgd.Renderer;
using dclmgd.Support;
using JM.LinqFaster;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace dclmgd
{
    [Flags]
    enum WallSides { None = 0, N = 1 << 0, W = 1 << 1, S = 1 << 2, E = 1 << 3 }

    class MapPart
    {
        public string Name { get; }
        public Image<Rgba32> Image { get; }
        public WallSides WallSides { get; }

        public MapPart(Image<Rgba32> image)
        {
            Image = image;

            var black = new Rgba32(0, 0, 0, 255);
            if (Image.GetPixelRowSpan(0).AllF(c => c == black)) WallSides |= WallSides.N;
            if (Image.GetPixelRowSpan(Image.Height - 1).AllF(c => c == black)) WallSides |= WallSides.S;

            bool allW = true, allE = true;
            for (int rowIdx = 0; rowIdx < Image.Height; ++rowIdx)
            {
                var row = Image.GetPixelRowSpan(rowIdx);
                if (row[0] != black) allW = false;
                if (row.LastF() != black) allE = false;

                if (!allW && !allE) break;
            }

            if (allW) WallSides |= WallSides.W;
            if (allE) WallSides |= WallSides.E;
        }
    }

    class Program
    {
        static void Main()
        {
            new Window().Run();
        }
    }
}
