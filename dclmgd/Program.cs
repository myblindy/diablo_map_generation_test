using JM.LinqFaster;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
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

    class Cell
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int Index { get; init; }
        public double Value { get; init; }
        public List<Cell> Neighbours { get; } = new();
    }

    static class Extensions
    {
        public static bool Between(this int val, int min, int max) => min <= val && val <= max;
        public static void SetAll<T>(this IList<T> arr, Func<int, T> generator)
        {
            for (int i = 0; i < arr.Count; ++i)
                arr[i] = generator(i);
        }
    }

    class StopwatchPrint : IDisposable
    {
        readonly Stopwatch stopwatch = Stopwatch.StartNew();
        readonly string text;

        public StopwatchPrint(string text) => this.text = text;

        public void Dispose() => Console.WriteLine($"{text} took {stopwatch.ElapsedMilliseconds}ms");
    }

    class Program
    {
        static void Main(string[] args)
        {
            const int width = 80, height = 80;
            const int maxCellWidth = 3, maxCellHeight = 3;

            var rng = new Random();
            var cellData = new double[width, height];
            var cells = new List<Cell>();

            int startPointX = rng.Next(width / 10), startPointY = rng.Next(height / 10);
            int endPointX = rng.Next(width * 9 / 10, width), endPointY = rng.Next(height * 9 / 10, height);
            Cell startCell = default, endCell = default;
            int startCellIndex = -1, endCellIndex = -1;

            // build the cells
            using (new StopwatchPrint("Building the cells"))
                for (int x = 0; x < width; ++x)
                    for (int y = 0; y < height; ++y)
                    {
                        int maxX, maxY;
                        for (maxX = maxCellWidth; maxX >= 0; --maxX)
                            if (x + maxX - 1 < width)
                            {
                                var ok = true;
                                for (int i = 0; i < maxX; ++i)
                                    if (cellData[x + i, y] != 0) { ok = false; break; }
                                if (ok) break;
                            }
                        if (maxX == 0) continue;

                        for (maxY = maxCellHeight; maxY >= 0; --maxY)
                            if (y + maxY - 1 < height)
                            {
                                var ok = true;
                                for (int i = 0; i < maxY; ++i)
                                    if (cellData[x, y + i] != 0) { ok = false; break; }
                                if (ok) break;
                            }
                        if (maxY == 0) continue;

                        var cellWidth = rng.Next(1, maxX + 1);
                        var cellHeight = rng.Next(1, maxY + 1);
                        var cellValue = rng.NextDouble();
                        for (int xp = x; xp < x + cellWidth; ++xp)
                            for (int yp = y; yp < y + cellHeight; ++yp)
                                cellData[xp, yp] = cellValue;

                        var cell = new Cell { X = x, Y = y, Width = cellWidth, Height = cellHeight, Value = cellValue, Index = cells.Count };
                        cells.Add(cell);

                        if (startPointX.Between(cell.X, cell.X + cell.Width) && startPointY.Between(cell.Y, cell.Y + cell.Height))
                            (startCell, startCellIndex) = (cell, cells.Count - 1);
                        if (endPointX.Between(cell.X, cell.X + cell.Width) && endPointY.Between(cell.Y, cell.Y + cell.Height))
                            (endCell, endCellIndex) = (cell, cells.Count - 1);

                        y += cellHeight - 1;
                    }

            // build the cell's neighbour graph data
            using (new StopwatchPrint("Building the cells' neighbour graph data"))
                foreach (var cell in cells)
                    foreach (var otherCell in cells)
                        if (cell != otherCell)
                            if (((otherCell.X + otherCell.Width == cell.X || cell.X + cell.Width == otherCell.X)
                                    && (otherCell.Y.Between(cell.Y, cell.Y + cell.Height) || cell.Y.Between(otherCell.Y, otherCell.Y + otherCell.Height)))
                                || ((otherCell.Y + otherCell.Height == cell.Y || cell.Y + cell.Height == otherCell.Y)
                                    && (otherCell.X.Between(cell.X, cell.X + cell.Width) || cell.X.Between(otherCell.X, otherCell.X + otherCell.Width))))
                            {
                                cell.Neighbours.Add(otherCell);
                            }

            // Dijkstra's algorithm to find the shortest path in the cell graph
            var prevCell = cells.ToDictionary(cell => cell, _ => (Cell)null);
            using (new StopwatchPrint("Running the shortest path algorithm"))
            {
                var cellDistance = new double[cells.Count];
                cellDistance.SetAll(idx => idx == startCellIndex ? 0 : double.PositiveInfinity);

                var visitedCells = new HashSet<int>();

                do
                {
                    // current cell is the smallest unvisited tentative distance cell 
                    var currentCell = cells[cellDistance.Select((d, idx) => (d, idx)).OrderBy(x => x.d).First(x => !visitedCells.Contains(cells[x.idx].Index)).idx];

                    foreach (var neighbourCell in currentCell.Neighbours)
                        if (!visitedCells.Contains(neighbourCell.Index))
                        {
                            var distanceThroughCurrentCell = cellDistance[currentCell.Index] + neighbourCell.Value;
                            if (cellDistance[neighbourCell.Index] > distanceThroughCurrentCell)
                            {
                                cellDistance[neighbourCell.Index] = distanceThroughCurrentCell;
                                prevCell[neighbourCell] = currentCell;
                            }
                        }

                    visitedCells.Add(currentCell.Index);
                } while (visitedCells.Count != cells.Count && !visitedCells.Contains(endCell.Index));
            }

            // draw the output
            const int drawScale = 15;
            var img = new Image<Rgba32>((width + 1) * drawScale, (height + 1) * drawScale);
            using (new StopwatchPrint("Rendering the output"))
            {
                img.Mutate(ctx =>
                {
                    cells.ForEach(cell => ctx.Fill(new Color(new Rgba32((float)cell.Value, (float)cell.Value, (float)cell.Value)),
                        new RectangleF(cell.X * drawScale, cell.Y * drawScale, cell.Width * drawScale, cell.Height * drawScale)));

                    static (int x, int y) getCenter(Cell cell) => ((cell.X + cell.Width / 2) * drawScale, (cell.Y + cell.Height / 2) * drawScale);

                    var (lastX, lastY) = getCenter(endCell);
                    for (var cell = prevCell[endCell]; cell is not null; cell = prevCell[cell])
                    {
                        var (x, y) = getCenter(cell);
                        ctx.DrawLines(Color.Red, 2, new(lastX, lastY), new(x, y));
                        (lastX, lastY) = (x, y);
                    }
                });
            }

            img.SaveAsPng("output.png");
            Process.Start(new ProcessStartInfo("output.png") { UseShellExecute = true });

            //var parts = Directory.EnumerateFiles("Maps/WideOpen").SelectMany(p =>
            //{
            //    var image = Image.Load<Rgba32>(p);

            //    return new[]
            //    {
            //        new MapPart(image),
            //        new MapPart(image.Clone(c => c.Rotate(RotateMode.Rotate90))),
            //        new MapPart(image.Clone(c => c.Rotate(RotateMode.Rotate180))),
            //        new MapPart(image.Clone(c => c.Rotate(RotateMode.Rotate270))),

            //        new MapPart(image.Clone(c => c.Flip(FlipMode.Horizontal))),
            //        new MapPart(image.Clone(c => c.Flip(FlipMode.Vertical))),
            //    };
            //}).ToList();
        }
    }
}
