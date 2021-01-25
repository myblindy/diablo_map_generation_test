using dclmgd.Support;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static MoreLinq.Extensions.ForEachExtension;
using static MoreLinq.Extensions.LagExtension;

namespace dclmgd.MapGenerators
{
    class RoomGenerator : MapGenerator
    {
        record Cell(int X, int Y, int Width, int Height, int Index, double Value)
        {
            BitArray64 doorsNorth = new(Width);
            BitArray64 doorsSouth = new(Width);
            BitArray64 doorsEast = new(Height);
            BitArray64 doorsWest = new(Height);

            public List<Cell> Neighbours { get; } = new();

            public ref BitArray64 DoorsNorth => ref doorsNorth;
            public ref BitArray64 DoorsSouth => ref doorsSouth;
            public ref BitArray64 DoorsEast => ref doorsEast;
            public ref BitArray64 DoorsWest => ref doorsWest;
        }

        protected RoomGenerator(MapTemplateData data)
        {
            var rng = new Random();

            int width = rng.Next(data.Width);
            int height = rng.Next(data.Height);

            int maxCellSize = data.CellTemplates.Max(w => Math.Max(w.Size.Width, w.Size.Height));
            var cellSizesSet = data.CellTemplates.Select(t => { var s = t.Size; s.SortInPlace(); return s; })
                .Concat(data.CellTemplates.Select(t => { var s = t.Size; s.SortDescInPlace(); return s; }))
                .ToHashSet();

            var cellData = new double[width, height];
            var cells = new List<Cell>();

            int startPointX = rng.Next(width / 6), startPointY = rng.Next(height / 6);
            int endPointX = rng.Next(width * 5 / 6, width), endPointY = rng.Next(height * 5 / 6, height);
            Cell startCell = default, endCell = default;
            int startCellIndex = -1, endCellIndex = -1;

            // build the cells
            using (new StopwatchMessageList("Building the cells", this))
                for (int x = 0; x < width; ++x)
                    for (int y = 0; y < height; ++y)
                    {
                        int maxX, maxY;
                        for (maxX = maxCellSize; maxX >= 0; --maxX)
                            if (x + maxX - 1 < width)
                            {
                                var ok = true;
                                for (int i = 0; i < maxX; ++i)
                                    if (cellData[x + i, y] != 0) { ok = false; break; }
                                if (ok) break;
                            }
                        if (maxX == 0) continue;

                        for (maxY = maxCellSize; maxY >= 0; --maxY)
                            if (y + maxY - 1 < height)
                            {
                                var ok = true;
                                for (int i = 0; i < maxY; ++i)
                                    if (cellData[x, y + i] != 0) { ok = false; break; }
                                if (ok) break;
                            }
                        if (maxY == 0) continue;

                        IntSize cellSize;
                        do
                        {
                            cellSize = new(rng.Next(1, maxX + 1), rng.Next(1, maxY + 1));
                        } while (!cellSizesSet.Contains(cellSize));

                        var cellValue = rng.NextDouble();
                        for (int xp = x; xp < x + cellSize.Width; ++xp)
                            for (int yp = y; yp < y + cellSize.Height; ++yp)
                                cellData[xp, yp] = cellValue;

                        var cell = new Cell(x, y, cellSize.Width, cellSize.Height, cells.Count, cellValue);
                        cells.Add(cell);

                        if (startPointX.Between(cell.X, cell.X + cell.Width - 1) && startPointY.Between(cell.Y, cell.Y + cell.Height - 1))
                            (startCell, startCellIndex) = (cell, cells.Count - 1);
                        if (endPointX.Between(cell.X, cell.X + cell.Width - 1) && endPointY.Between(cell.Y, cell.Y + cell.Height - 1))
                            (endCell, endCellIndex) = (cell, cells.Count - 1);

                        y += cellSize.Height - 1;
                    }

            // build the cell's neighbour graph data
            using (new StopwatchMessageList("Building the cells' neighbour graph data", this))
                foreach (var cell in cells)
                    foreach (var otherCell in cells)
                        // if (cell != otherCell)
                        if ((otherCell.X + otherCell.Width == cell.X || cell.X + cell.Width == otherCell.X)
                                && (otherCell.Y.Between(cell.Y, cell.Y + cell.Height - 1) || cell.Y.Between(otherCell.Y, otherCell.Y + otherCell.Height - 1))
                            || (otherCell.Y + otherCell.Height == cell.Y || cell.Y + cell.Height == otherCell.Y)
                                && (otherCell.X.Between(cell.X, cell.X + cell.Width - 1) || cell.X.Between(otherCell.X, otherCell.X + otherCell.Width - 1)))
                        {
                            cell.Neighbours.Add(otherCell);
                        }

            // Dijkstra's algorithm to find the shortest path in the cell graph
            var prevCell = new Cell[cells.Count];
            using (new StopwatchMessageList("Running the shortest path algorithm", this))
            {
                var cellDistance = new double[cells.Count];
                cellDistance.SetAll(idx => idx == startCellIndex ? 0 : double.PositiveInfinity);

                var visitedCells = new BitList64(cells.Count);

                do
                {
                    // current cell is the smallest unvisited tentative distance cell 
                    Cell currentCell = null;
                    double currentCellCost = double.PositiveInfinity;
                    for (int cellDistanceIdx = 0; cellDistanceIdx < cellDistance.Length; ++cellDistanceIdx)
                        if (cellDistance[cellDistanceIdx] < currentCellCost && !visitedCells[cellDistanceIdx])
                        {
                            currentCell = cells[cellDistanceIdx];
                            currentCellCost = cellDistance[cellDistanceIdx];
                        }

                    foreach (var neighbourCell in currentCell.Neighbours)
                        if (!visitedCells[neighbourCell.Index])
                        {
                            var distanceThroughCurrentCell = cellDistance[currentCell.Index] + neighbourCell.Value;
                            if (cellDistance[neighbourCell.Index] > distanceThroughCurrentCell)
                            {
                                cellDistance[neighbourCell.Index] = distanceThroughCurrentCell;
                                prevCell[neighbourCell.Index] = currentCell;
                            }
                        }

                    visitedCells[currentCell.Index] = true;
                } while (!visitedCells.AllTrue() && !visitedCells[endCell.Index]);
            }

            // select the cells along the path, and some other random cells linked to some of them, possibly recursive
            Cell[] selectedCellsDirectPath;
            var selectedCells = new List<Cell>();
            var extraLinks = new List<(Cell c1, Cell c2)>();
            using (new StopwatchMessageList("Selecting cells", this))
            {
                for (var cell = endCell; cell is not null; cell = prevCell[cell.Index])
                    selectedCells.Add(cell);
                selectedCellsDirectPath = selectedCells.ToArray();

                var neighboursSet = new HashSet<int>();

                for (var extraRoomsCount = rng.Next(width * height / 15, width * height / 5); extraRoomsCount >= 0; --extraRoomsCount)
                {
                    Cell cellToAdd = default;

                    // select a random cell to pull a neighbor
                    do
                    {
                        var selectedCell = selectedCells[rng.Next(selectedCells.Count)];

                        neighboursSet.Clear();
                        neighboursSet.AddRange(selectedCell.Neighbours.Select(c => c.Index));
                        neighboursSet.RemoveRange(selectedCells.Select(c => c.Index));

                        if (neighboursSet.Any())
                        {
                            cellToAdd = cells[rng.Next(neighboursSet)];
                            extraLinks.Add((selectedCell, cellToAdd));
                        }
                    } while (cellToAdd is null);

                    selectedCells.Add(cellToAdd);
                }
            }

            // link a few more neighbours 
            using (new StopwatchMessageList("Linking extra neighbours", this))
                for (var extraLinksCount = rng.Next(width * height / 60, width * height / 40); extraLinksCount >= 0; --extraLinksCount)
                    while (true)
                    {
                        // select a random pair of selected cells and make sure they're not already linked
                        var c1 = selectedCells[rng.Next(selectedCells.Count)];
                        var c2 = c1.Neighbours[rng.Next(c1.Neighbours.Count)];
                        if (c1 != c2 && selectedCells.Contains(c2) && prevCell[c1.Index] != c2 && prevCell[c2.Index] != c1 && !extraLinks.Any(w => w == (c1, c2) && w == (c2, c1)))
                        {
                            extraLinks.Add((c1, c2));
                            break;
                        }
                    }

            // for each linked room, select a door
            using (new StopwatchMessageList("Adding doors", this))
                selectedCellsDirectPath.Lag(1, (c1, c2) => (c1, c2)).Skip(1).Concat(extraLinks).ForEach(w =>
                {
                    var c1 = w.c1;
                    var c2 = w.c2;

                    if (c1.X > c2.X) (c1, c2) = (c2, c1);
                    if (c1.X + c1.Width == c2.X)
                    {
                        // east of c1, west of c2
                        int offset1, offset2, length;
                        if (c1.Y <= c2.Y && c1.Y + c1.Height >= c2.Y + c2.Height)                      // [c1 [c2 c2] c1]
                            (offset1, offset2, length) = (c2.Y - c1.Y, 0, c2.Height);
                        else if (c2.Y <= c1.Y && c2.Y + c2.Height >= c1.Y + c1.Height)                 // [c2 [c1 c1] c2]
                            (offset1, offset2, length) = (0, c1.Y - c2.Y, c1.Height);
                        else if (c2.Y <= c1.Y && c1.Y + c1.Height >= c2.Y + c2.Height)                 // [c2 [c1 c2] c1]
                            (offset1, offset2, length) = (0, c1.Y - c2.Y, c2.Y + c2.Height - c1.Y);
                        else if (c1.Y <= c2.Y && c2.Y + c2.Height >= c1.Y + c1.Height)                 // [c1 [c2 c1] c2]
                            (offset1, offset2, length) = (c2.Y - c1.Y, 0, c1.Y + c1.Height - c2.Y);
                        else
                            throw new InvalidOperationException();

                        var doorIdx = rng.Next(length);
                        c1.DoorsEast[doorIdx + offset1] = true;
                        c2.DoorsWest[doorIdx + offset2] = true;
                    }
                    else
                    {
                        if (c1.Y > c2.Y) (c1, c2) = (c2, c1);
                        if (c1.Y + c1.Height == c2.Y)
                        {
                            // south of c1, north of c2
                            int offset1, offset2, length;
                            if (c1.X <= c2.X && c1.X + c1.Width >= c2.X + c2.Width)                      // [c1 [c2 c2] c1]
                                (offset1, offset2, length) = (c2.X - c1.X, 0, c2.Width);
                            else if (c2.X <= c1.X && c2.X + c2.Width >= c1.X + c1.Width)                 // [c2 [c1 c1] c2]
                                (offset1, offset2, length) = (0, c1.X - c2.X, c1.Width);
                            else if (c2.X <= c1.X && c1.X + c1.Width >= c2.X + c2.Width)                  // [c2 [c1 c2] c1]
                                (offset1, offset2, length) = (0, c1.X - c2.X, c2.X + c2.Width - c1.X);
                            else if (c1.X <= c2.X && c2.X + c2.Width >= c1.X + c1.Width)                  // [c1 [c2 c1] c2]
                                (offset1, offset2, length) = (c2.X - c1.X, 0, c1.X + c1.Width - c2.X);
                            else
                                throw new InvalidOperationException();

                            var doorIdx = rng.Next(length);
                            c1.DoorsSouth[doorIdx + offset1] = true;
                            c2.DoorsNorth[doorIdx + offset2] = true;
                        }
                        else
                            throw new InvalidOperationException();
                    }
                });

            // draw the output
            const int drawScale = 20;
            const float graphLineThickness = 4, borderThickness = 2, doorWidth = 8;
            var img = new Image<Rgba32>(width * drawScale, height * drawScale);
            using (new StopwatchMessageList("Rendering the output", this))
                img.Mutate(ctx =>
                {
                    ctx.Clear(Color.White);

                    // rooms
                    selectedCells.ForEach(cell =>
                    {
                        var rect = new RectangleF(cell.X * drawScale, cell.Y * drawScale, cell.Width * drawScale, cell.Height * drawScale);
                        ctx.Fill(new Color(new Rgba32((float)cell.Value, (float)cell.Value, (float)cell.Value)), rect)
                            .Draw(Color.Pink, borderThickness, rect);
                    });

                    static PointF getCenter(Cell cell) => new((cell.X + cell.Width / 2.0f) * drawScale, (cell.Y + cell.Height / 2.0f) * drawScale);

                    // room graph
                    var lastPoint = getCenter(endCell);
                    for (var cell = prevCell[endCell.Index]; cell is not null; cell = prevCell[cell.Index])
                    {
                        var currentPoint = getCenter(cell);
                        ctx.DrawLines(Color.Red, graphLineThickness, lastPoint, currentPoint);
                        lastPoint = currentPoint;
                    }

                    // extra links
                    extraLinks.ForEach(w => ctx.DrawLines(Color.Blue, graphLineThickness, getCenter(w.c1), getCenter(w.c2)));

                    // doors
                    selectedCells.ForEach(cell =>
                        cell.DoorsNorth.Select((v, idx) => (v, rc: new RectangleF((cell.X + idx + .5f) * drawScale - doorWidth / 2, cell.Y * drawScale, doorWidth, doorWidth / 2)))
                            .Concat(cell.DoorsSouth.Select((v, idx) => (v, rc: new RectangleF((cell.X + idx + .5f) * drawScale - doorWidth / 2, (cell.Y + cell.Height) * drawScale - doorWidth / 2, doorWidth, doorWidth / 2))))
                            .Concat(cell.DoorsWest.Select((v, idx) => (v, rc: new RectangleF(cell.X * drawScale, (cell.Y + idx + .5f) * drawScale - doorWidth / 2, doorWidth / 2, doorWidth))))
                            .Concat(cell.DoorsEast.Select((v, idx) => (v, rc: new RectangleF((cell.X + cell.Width) * drawScale - doorWidth / 2, (cell.Y + idx + .5f) * drawScale - doorWidth / 2, doorWidth / 2, doorWidth))))
                            .Where(w => w.v)
                            .ForEach(w => ctx.Fill(Color.LimeGreen, w.rc)));

                    // status text
                    ctx.DrawText(new TextGraphicsOptions() { TextOptions = { HorizontalAlignment = HorizontalAlignment.Right } },
                         string.Join("\n", log), SystemFonts.CreateFont("Segoe UI", 24, FontStyle.Bold), Color.Black, new(width * drawScale, 0));
                });

            img.SaveAsPng("output.png");
            Process.Start(new ProcessStartInfo("output.png") { UseShellExecute = true });
        }
    }
}
