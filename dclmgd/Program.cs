﻿using JM.LinqFaster;
using SixLabors.Fonts;
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

    record Cell(int X, int Y, int Width, int Height, int Index, double Value)
    {
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

        public static List<string> Strings { get; } = new();

        public StopwatchPrint(string text) => this.text = text;

        public void Dispose() => Strings.Add($"{text} took {stopwatch.ElapsedMilliseconds}ms");
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

            int startPointX = rng.Next(width / 6), startPointY = rng.Next(height / 6);
            int endPointX = rng.Next(width * 5 / 6, width), endPointY = rng.Next(height * 5 / 6, height);
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

                        var cell = new Cell(x, y, cellWidth, cellHeight, cells.Count, cellValue);
                        cells.Add(cell);

                        if (startPointX.Between(cell.X, cell.X + cell.Width - 1) && startPointY.Between(cell.Y, cell.Y + cell.Height - 1))
                            (startCell, startCellIndex) = (cell, cells.Count - 1);
                        if (endPointX.Between(cell.X, cell.X + cell.Width - 1) && endPointY.Between(cell.Y, cell.Y + cell.Height - 1))
                            (endCell, endCellIndex) = (cell, cells.Count - 1);

                        y += cellHeight - 1;
                    }

            // build the cell's neighbour graph data
            using (new StopwatchPrint("Building the cells' neighbour graph data"))
                foreach (var cell in cells)
                    foreach (var otherCell in cells)
                        if (cell != otherCell)
                            if (((otherCell.X + otherCell.Width == cell.X || cell.X + cell.Width == otherCell.X)
                                    && (otherCell.Y.Between(cell.Y, cell.Y + cell.Height - 1) || cell.Y.Between(otherCell.Y, otherCell.Y + otherCell.Height - 1)))
                                || ((otherCell.Y + otherCell.Height == cell.Y || cell.Y + cell.Height == otherCell.Y)
                                    && (otherCell.X.Between(cell.X, cell.X + cell.Width - 1) || cell.X.Between(otherCell.X, otherCell.X + otherCell.Width - 1))))
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

            // select the cells along the path, and some other random cells linked to some of them, possibly recursive
            var selectedCells = new List<Cell>();
            var extraLinks = new List<(Cell c1, Cell c2)>();
            using (new StopwatchPrint("Selecting cells"))
            {
                for (var cell = endCell; cell is not null; cell = prevCell[cell])
                    selectedCells.Add(cell);

                for (var extraRoomsCount = rng.Next(width * height / 15, width * height / 5); extraRoomsCount >= 0; --extraRoomsCount)
                {
                    Cell cellToAdd = default;

                    // select a random cell to pull a neighbor
                    do
                    {
                        var selectedCell = selectedCells[rng.Next(selectedCells.Count)];
                        var availableNeighbours = selectedCell.Neighbours.Except(selectedCells).ToList();
                        if (availableNeighbours.Any())
                        {
                            cellToAdd = availableNeighbours[rng.Next(availableNeighbours.Count)];
                            extraLinks.Add((selectedCell, cellToAdd));
                        }
                    } while (cellToAdd is null);

                    selectedCells.Add(cellToAdd);
                }
            }

            // link a few more neighbours 
            using (new StopwatchPrint("Linking extra neighbours"))
                for (var extraLinksCount = rng.Next(width * height / 40, width * height / 20); extraLinksCount >= 0; --extraLinksCount)
                    while (true)
                    {
                        // select a random pair of selected cells and make sure they're not already linked
                        var c1 = selectedCells[rng.Next(selectedCells.Count)];
                        var c2 = c1.Neighbours[rng.Next(c1.Neighbours.Count)];
                        if (c1 != c2 && selectedCells.Contains(c2) && prevCell[c1] != c2 && prevCell[c2] != c1 && !extraLinks.Any(w => w == (c1, c2) && w == (c2, c1)))
                        {
                            extraLinks.Add((c1, c2));
                            break;
                        }
                    }

            // draw the output
            const int drawScale = 15;
            const float graphLineThickness = 4, borderThickness = 2;
            var img = new Image<Rgba32>(width * drawScale, height * drawScale);
            using (new StopwatchPrint("Rendering the output"))
            {
                img.Mutate(ctx =>
                {
                    ctx.Clear(Color.White);

                    selectedCells.ForEach(cell =>
                    {
                        var rect = new RectangleF(cell.X * drawScale, cell.Y * drawScale, cell.Width * drawScale, cell.Height * drawScale);
                        ctx.Fill(new Color(new Rgba32((float)cell.Value, (float)cell.Value, (float)cell.Value)), rect)
                            .Draw(Color.Pink, borderThickness, rect);
                    });

                    static PointF getCenter(Cell cell) => new((cell.X + cell.Width / 2.0f) * drawScale, (cell.Y + cell.Height / 2.0f) * drawScale);

                    // room graph
                    var lastPoint = getCenter(endCell);
                    for (var cell = prevCell[endCell]; cell is not null; cell = prevCell[cell])
                    {
                        var currentPoint = getCenter(cell);
                        ctx.DrawLines(Color.Red, graphLineThickness, lastPoint, currentPoint);
                        lastPoint = currentPoint;
                    }

                    // extra links
                    extraLinks.ForEach(w => ctx.DrawLines(Color.Blue, graphLineThickness, getCenter(w.c1), getCenter(w.c2)));

                    // status text
                    ctx.DrawText(new TextGraphicsOptions() { TextOptions = { HorizontalAlignment = HorizontalAlignment.Right } },
                        string.Join("\n", StopwatchPrint.Strings), SystemFonts.CreateFont("Segoe UI", 24, FontStyle.Bold), Color.Black, new(width * drawScale, 0));
                });
            }

            img.SaveAsPng("output.png");
            Process.Start(new ProcessStartInfo("output.png") { UseShellExecute = true });
        }
    }
}
