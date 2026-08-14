// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * PropFinder - finds the furniture by looking at the map, because the game does not know it is
 * there.
 *
 * WHY THIS HAD TO BE BUILT
 * ------------------------
 * Props were being built from collision data, and a review of every room on Polus showed the
 * consequence: Security, Comms and Office came out completely empty. The reason is simple once
 * seen. Most Among Us furniture has NO collider at all - you walk straight through the desk in
 * Security, the console bank in Comms, the conference table in Office. Colliders exist for walls
 * and for the few objects that block you, and for nothing else.
 *
 * But every one of those objects is drawn on the map, and the mod already photographs the map. So
 * the furniture is found the way a person finds it: by looking for the things that are not floor.
 *
 * HOW
 * ---
 *   1. For each room, the floor colour is the MEDIAN over the room. On a hand-drawn map that is
 *      reliably the floor itself, because the floor is what covers most of the area.
 *   2. The room is sampled on a fine grid, and every cell whose colour differs clearly from that
 *      floor colour is marked as "not floor".
 *   3. A blob is only allowed to START from a cell whose whole 4-neighbourhood is ALSO marked (an
 *      eroded "seed"), but from there it is grown through every touching marked cell, seed or not.
 *      This is what keeps a patterned floor - the tile grid in Admin, the carpet weave in Office,
 *      the seam lines in Comms - from bridging every object in the room into one wall-to-wall
 *      blob: a painted line is one cell wide almost everywhere and can never seed, while a real
 *      object's solid body is several cells wide and always can. Without this step Admin, Office
 *      and Dropship each produced a single room-sized blob that was furniture and floor pattern
 *      welded together, which is worthless as a box and gets thrown out by the size filter below,
 *      taking every real object in the room down with it.
 *   4. A blob whose footprint is clearly wider than one object AND is solidly filled (not a thin
 *      diagonal pipe) is a row of objects standing flush against each other - ten crates in
 *      Storage read as one blob because nothing in their colour separates them. It is cut on a
 *      grid sized to a real prop's footprint, so ten crates come out as ten boxes and not one.
 *   5. Each surviving blob becomes a box: its footprint is the blob's extent, its colour the
 *      blob's average, and its height a guess from its size, because a top-down image cannot show
 *      height.
 *
 * WHAT IT CANNOT DO
 * -----------------
 * It has no idea WHAT it found. A blob is a coloured box of roughly the right size in exactly the
 * right place, which is what makes a room recognisable from across it; it is not a modelled desk.
 * Height in particular is invented, since no top-down picture contains it. And it can only see
 * what is inside a room's own trigger volume: furniture drawn in a corridor or spilling past a
 * room's wall into open ground (Storage has crates stacked right outside its own doorway) is
 * outside every room this function is ever asked to look at, because Find() iterates map.Rooms.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public static class PropFinder
{
    /// Sampling grid. Fine enough that a single seed cell (see Threshold/erosion below) still
    /// fits inside the smallest real props (a fire extinguisher, a wall panel), coarse enough
    /// that a whole map is well under a hundred thousand cells.
    private const float Cell = 0.10f;

    /// How different from the floor a cell must be to count as an object. Compared in a rough
    /// perceptual sense: brightness difference plus colour difference.
    ///
    /// This no longer has to single-handedly reject floor texture - erosion (step 3 in the
    /// header) does that job now - so it stays low enough to catch furniture that is close in
    /// tone to the floor it stands on (a green crate on a green floor in Storage).
    private const float Threshold = 0.11f;

    /// Erosion radius for IsSeed, in cells: a candidate seed must have every cell within this
    /// square neighbourhood also marked. One cell (a plain N/E/S/W check) turned out to be too
    /// forgiving - the lab's floor is not grout lines but a scatter of solid ~0.4 unit polka-dot
    /// tiles, four cells wide at this grid, and those survive a one-cell erosion with their centre
    /// intact and seed as if they were furniture. Two cells needs a clear ~0.5 unit radius of
    /// matching colour in every direction, which no single floor tile has but any real prop
    /// (crates, consoles, tables) comfortably does.
    private const int SeedRadius = 2;

    /// A blob may only START from a cell whose whole neighbourhood (SeedRadius above) is ALSO
    /// marked. Floor texture - grout, carpet seams, a scattered tile, the ragged rim of a stain -
    /// does not have a solid patch that size; a real object's body always does. The blob then
    /// grows through every touching marked cell (seed or not, see FindInRoom), so a thin cable or
    /// rim on a real object is still included once something solid has opened the door.
    private static bool IsSeed(bool[] mark, int nx, int ny, int gx, int gy)
    {
        if (gx < SeedRadius || gy < SeedRadius || gx >= nx - SeedRadius || gy >= ny - SeedRadius) return false;
        for (int dy = -SeedRadius; dy <= SeedRadius; dy++)
        {
            int row = (gy + dy) * nx + gx;
            for (int dx = -SeedRadius; dx <= SeedRadius; dx++)
                if (!mark[row + dx]) return false;
        }
        return true;
    }

    /// Blobs below this many cells are noise that still slipped through erosion: a single
    /// isolated seed neighbourhood is five cells, so this is deliberately just under that rather
    /// than a size judgement in its own right.
    private const int MinCells = 5;

    /// Blobs above this fraction of the room are not furniture, they are the floor pattern itself
    /// or a mis-detected room. Kept as a last safety net; erosion is what actually keeps a
    /// patterned floor from reaching this large in the first place.
    private const float MaxRoomFraction = 0.55f;

    /// Roughly the footprint of a single Among Us prop, taken from the survey's own real
    /// colliders: Storage/storagebox is 1.20x0.86, Comms/commstable 0.96x0.51, an Electrical
    /// transformer 1.04x0.84. A blob noticeably wider than this in a solid, filled shape is not
    /// one object, it is several standing flush against each other (ten crates in Storage), and
    /// gets cut on a grid of this size rather than left as one oversized box.
    private const float SplitUnit = 1.0f;

    /// A blob is only cut if it is this solidly filled. A thin diagonal pipe or an L-shaped room
    /// corner can have a bounding box much bigger than one prop without being several props, and
    /// cutting it on a grid would slice a single object into fragments instead of separating a
    /// cluster of them.
    private const float SplitFillRatio = 0.5f;

    /// A cap on how many pieces one blob may be cut into. A real cluster of furniture - even every
    /// crate in Storage - stays well under this. What does not is a heavily textured floor (the
    /// lab's tile pattern, a room's own ridged metal panelling) that colour alone cannot always
    /// tell apart from furniture and that erosion did not fully break up either: without a cap,
    /// treating that as "one big blob of many objects" and slicing it on the SplitUnit grid
    /// produces dozens of boxes across the whole room. Both dimensions are scaled down together so
    /// the cut pieces stay roughly square rather than degenerating into slivers.
    private const int MaxSplitPieces = 20;

    public sealed class FoundProp
    {
        public NfVec2 Min, Max;
        public NfColor Color;
        public float Height;
        /// True when the blob is small and bright: consoles and screens, which get a lit front.
        public bool Lit;
    }

    // TEMP diagnostic for tuning: prints a per-room breakdown when set, never touched by the game.
    private static readonly bool DebugDump = Environment.GetEnvironmentVariable("NF_PROPFINDER_DEBUG") == "1";

    public static List<FoundProp> Find(MapModel map)
    {
        var result = new List<FoundProp>();
        var atlas = map.Atlas;
        if (atlas == null || !atlas.IsValid) return result;

        foreach (var room in map.Rooms)
        {
            float w = room.Max.X - room.Min.X, h = room.Max.Y - room.Min.Y;
            if (w < 0.8f || h < 0.8f) continue;
            if (w > 40f || h > 40f) continue;                 // not a room, a region

            int before = result.Count;
            FindInRoom(map, room, result);
            if (DebugDump)
            {
                Scene3D.NightfallLog($"[PropFinder] {room.Name,-16} {w,5:0.0}x{h,-5:0.0} -> {result.Count - before,3} props");
                for (int i = before; i < result.Count; i++)
                {
                    var p = result[i];
                    Scene3D.NightfallLog($"    #{i - before,-2} min=({p.Min.X:0.00},{p.Min.Y:0.00}) "
                                      + $"max=({p.Max.X:0.00},{p.Max.Y:0.00}) size=({p.Max.X - p.Min.X:0.00}x{p.Max.Y - p.Min.Y:0.00})");
                }
            }
        }
        return result;
    }

    private static void FindInRoom(MapModel map, RoomInfo room, List<FoundProp> outp)
    {
        var atlas = map.Atlas;

        // The room shrunk slightly, so the wall line itself is never mistaken for furniture.
        const float inset = 0.28f;
        float x0 = room.Min.X + inset, x1 = room.Max.X - inset;
        float y0 = room.Min.Y + inset, y1 = room.Max.Y - inset;
        if (x1 - x0 < 0.5f || y1 - y0 < 0.5f) return;

        int nx = (int)((x1 - x0) / Cell);
        int ny = (int)((y1 - y0) / Cell);
        if (nx < 3 || ny < 3) return;

        // 1. sample the whole grid once, up front. room.Min/Max is only the room's bounding
        // RECTANGLE, and Polus rooms are routinely not rectangular - Science's box also covers a
        // slice of the outdoor rocket pad outside its own wall, Electrical's covers a corner of
        // Security. That outdoor ground is legitimately "not floor" by colour, but it is not
        // furniture either, and counting it in only taught every filter below the wrong picture of
        // the room. map.IsInside is the same room-trigger rasterisation the floor grid already
        // trusts for "does this cell have a ceiling", so it is used here to skip anything outside
        // the room's actual footprint before it ever reaches the colour comparison.
        //
        // That rasterisation is not perfect, though: it only recognises a trigger sitting exactly
        // one level under the ship root as "the room", and at least one room on Polus (Specimens,
        // internally "RightPod") has its room trigger one level deeper than that, so IsInside is
        // false almost everywhere inside it. Rather than lose the room entirely, the grid is
        // sampled twice - once filtered by IsInside, and if that leaves out most of the room's own
        // rectangle, once more without the filter, trusting the rectangle instead. A room where
        // IsInside genuinely works only pays for the first pass.
        var mark = new bool[nx * ny];
        var cr = new float[nx * ny];
        var cg = new float[nx * ny];
        var cb = new float[nx * ny];
        var valid = new bool[nx * ny];
        var lum = new float[nx * ny];
        int validCount = 0;

        void SampleGrid(bool requireInside)
        {
            validCount = 0;
            for (int gy = 0; gy < ny; gy++)
            {
                float wy = y0 + (gy + 0.5f) * Cell;
                for (int gx = 0; gx < nx; gx++)
                {
                    float wx = x0 + (gx + 0.5f) * Cell;
                    int i = gy * nx + gx;
                    if (requireInside && !map.IsInside(new NfVec2(wx, wy))) { valid[i] = false; continue; }
                    if (!atlas.Sample(wx, wy, out float r, out float g, out float b)) { valid[i] = false; continue; }

                    cr[i] = r; cg[i] = g; cb[i] = b;
                    lum[i] = r * 0.3f + g * 0.6f + b * 0.1f;
                    valid[i] = true;
                    validCount++;
                }
            }
        }

        SampleGrid(requireInside: true);
        if (validCount < nx * ny / 2) SampleGrid(requireInside: false);
        if (validCount == 0) return;

        // A single room-wide median floor colour assumes the room IS one flat colour, which broke
        // down badly in practice: room.Min/Max is only a bounding RECTANGLE, and several Polus
        // rooms are not rectangular or not one material - Electrical's bounding box also covers
        // its own purple transformer alcove and a slice of neighbouring Security, the lab spans a
        // blue-tiled and a plain-floored half, and the map's own baked lighting puts a visible
        // brightness gradient across any big room. One median there does not describe "the floor",
        // it describes an average of several different floors, and everything on the wrong side of
        // it - which was often most of the room - got marked as an object and then discarded whole
        // by the size filters below. So the floor reference is local: the room is cut into blocks
        // a few times bigger than a single prop, each gets its OWN median from the samples that
        // land in it, and a cell is judged against the floor of ITS block. An object big enough to
        // fill a whole block on its own is rare enough on Polus to accept as a blind spot.
        const float BlockSize = 2.0f;
        int bnx = Math.Max(1, (int)MathF.Ceiling(nx * Cell / BlockSize));
        int bny = Math.Max(1, (int)MathF.Ceiling(ny * Cell / BlockSize));

        // The room-wide median still exists, purely as the fallback for a block that ends up with
        // no floor sample of its own (a block sitting entirely under one large object).
        var order = new int[validCount];
        for (int i = 0, k = 0; i < mark.Length; i++) if (valid[i]) order[k++] = i;
        Array.Sort(order, (a, b) => lum[a].CompareTo(lum[b]));
        int medianIdx = order[order.Length / 2];
        float fr = cr[medianIdx], fg = cg[medianIdx], fb = cb[medianIdx], fLum = lum[medianIdx];

        int BlockOf(int gx, int gy) =>
            Math.Min(bny - 1, (int)(gy * Cell / BlockSize)) * bnx + Math.Min(bnx - 1, (int)(gx * Cell / BlockSize));

        // Builds one median colour per block from whichever cells are currently believed to be
        // floor. excludeAsObject is null on the first pass, so every cell counts; a dense cluster
        // of furniture (ten crates cover a good half of Storage) can then dominate a block's own
        // median and make the block "believe" the crates ARE its floor, at which point the real
        // floor around them starts getting marked as the object instead of the crates. Running
        // this twice - the second time with whatever the first round already flagged as furniture
        // excluded - lets a block's estimate settle on the actual floor even under heavy coverage.
        void ComputeBlockFloor(bool[] excludeAsObject, float[] outFr, float[] outFg, float[] outFb, float[] outLum)
        {
            var blockCells = new List<int>[bnx * bny];
            for (int i = 0; i < mark.Length; i++)
            {
                if (!valid[i] || (excludeAsObject != null && excludeAsObject[i])) continue;
                (blockCells[BlockOf(i % nx, i / nx)] ??= new List<int>()).Add(i);
            }
            for (int b = 0; b < blockCells.Length; b++)
            {
                var cells = blockCells[b];
                outFr[b] = fr; outFg[b] = fg; outFb[b] = fb; outLum[b] = fLum;
                if (cells == null || cells.Count == 0) continue;

                cells.Sort((a, c) => lum[a].CompareTo(lum[c]));
                int m = cells[cells.Count / 2];
                outFr[b] = cr[m]; outFg[b] = cg[m]; outFb[b] = cb[m]; outLum[b] = lum[m];
            }
        }

        void MarkAll(float[] bFr, float[] bFg, float[] bFb, float[] bLum)
        {
            for (int gy = 0; gy < ny; gy++)
                for (int gx = 0; gx < nx; gx++)
                {
                    int i = gy * nx + gx;
                    if (!valid[i]) continue;
                    int b = BlockOf(gx, gy);
                    float dl = MathF.Abs(lum[i] - bLum[b]);
                    float dc = MathF.Abs(cr[i] - bFr[b]) + MathF.Abs(cg[i] - bFg[b]) + MathF.Abs(cb[i] - bFb[b]);
                    mark[i] = dl + dc * 0.5f > Threshold;
                }
        }

        // 2. mark every cell that is clearly not the floor of its OWN block, refined once
        var blockFr = new float[bnx * bny];
        var blockFg = new float[bnx * bny];
        var blockFb = new float[bnx * bny];
        var blockLum = new float[bnx * bny];

        ComputeBlockFloor(null, blockFr, blockFg, blockFb, blockLum);
        MarkAll(blockFr, blockFg, blockFb, blockLum);
        ComputeBlockFloor(mark, blockFr, blockFg, blockFb, blockLum);
        MarkAll(blockFr, blockFg, blockFb, blockLum);

        // 3. precompute which marked cells are allowed to START a blob (see IsSeed above). Done
        // once up front so the flood fill below is a plain lookup rather than a re-derivation.
        var seed = new bool[nx * ny];
        for (int gy = 0; gy < ny; gy++)
            for (int gx = 0; gx < nx; gx++)
                if (mark[gy * nx + gx]) seed[gy * nx + gx] = IsSeed(mark, nx, ny, gx, gy);

        if (DebugDump)
        {
            int mc = 0, sc = 0;
            foreach (var m in mark) if (m) mc++;
            foreach (var s in seed) if (s) sc++;
            Scene3D.NightfallLog($"[PropFinder]   {room.Name,-16} grid={nx}x{ny} blocks={bnx}x{bny} floor=({fr:0.00},{fg:0.00},{fb:0.00}) "
                              + $"marked={mc}/{nx * ny} seeds={sc}");
        }

        // 3. group into blobs, iteratively so a large object cannot blow the stack. A blob may
        // only be started from a seed cell, but expands through every touching marked cell same
        // as before - this is the only change from a plain flood fill, and it is what stops a
        // patterned floor from welding every object in the room together (see header).
        var visited = new bool[nx * ny];
        var stack = new Stack<int>();
        var members = new List<int>(64);
        int roomCells = nx * ny;

        for (int start = 0; start < mark.Length; start++)
        {
            if (!seed[start] || visited[start]) continue;

            stack.Clear();
            members.Clear();
            stack.Push(start);
            visited[start] = true;

            int count = 0;
            int minGx = nx, maxGx = 0, minGy = ny, maxGy = 0;
            float sumR = 0f, sumG = 0f, sumB = 0f;

            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int gx = i % nx, gy = i / nx;
                count++;
                members.Add(i);
                if (gx < minGx) minGx = gx;
                if (gx > maxGx) maxGx = gx;
                if (gy < minGy) minGy = gy;
                if (gy > maxGy) maxGy = gy;
                sumR += cr[i]; sumG += cg[i]; sumB += cb[i];

                void Push(int x, int y)
                {
                    if (x < 0 || y < 0 || x >= nx || y >= ny) return;
                    int j = y * nx + x;
                    if (!mark[j] || visited[j]) return;
                    visited[j] = true;
                    stack.Push(j);
                }
                Push(gx - 1, gy); Push(gx + 1, gy); Push(gx, gy - 1); Push(gx, gy + 1);
            }

            if (DebugDump)
                Scene3D.NightfallLog($"        blob count={count} bbox={maxGx - minGx + 1}x{maxGy - minGy + 1} "
                                  + $"roomFrac={count / (float)roomCells:0.00}");

            if (count < MinCells) continue;

            int bboxW = maxGx - minGx + 1, bboxH = maxGy - minGy + 1;
            float bw = bboxW * Cell, bh = bboxH * Cell;

            // Very long thin blobs are wall trim, carpet edges or pipes painted on the floor, not
            // objects standing in the room - reject these outright, splitting or not.
            float ratio = MathF.Max(bw, bh) / MathF.Max(0.01f, MathF.Min(bw, bh));
            if (ratio > 9f) continue;

            int splitX = Math.Max(1, (int)MathF.Round(bw / SplitUnit));
            int splitY = Math.Max(1, (int)MathF.Round(bh / SplitUnit));
            if (splitX * splitY > MaxSplitPieces)
            {
                float down = MathF.Sqrt(MaxSplitPieces / (float)(splitX * splitY));
                splitX = Math.Max(1, (int)MathF.Round(splitX * down));
                splitY = Math.Max(1, (int)MathF.Round(splitY * down));
            }
            float fillRatio = count / (float)(bboxW * bboxH);
            bool willSplit = (splitX > 1 || splitY > 1) && fillRatio > SplitFillRatio;

            // The room-fraction veto only makes sense against a blob that is about to become ONE
            // box: a cluttered room can easily be more than half covered by real furniture (Storage
            // reads as roughly two thirds crate), and that coverage is exactly what the split above
            // is for. Rejecting it here before splitting had a chance to run threw every crate in
            // the room out along with the one bad box it was actually meant to catch.
            if (!willSplit && count > roomCells * MaxRoomFraction) continue;

            if (willSplit)
            {
                SplitBlob(members, cr, cg, cb, nx, x0, y0, minGx, minGy, bboxW, bboxH,
                          splitX, splitY, outp);
            }
            else
            {
                float bx0 = x0 + minGx * Cell, bx1 = x0 + (maxGx + 1) * Cell;
                float by0 = y0 + minGy * Cell, by1 = y0 + (maxGy + 1) * Cell;
                EmitProp(bx0, by0, bx1, by1, sumR, sumG, sumB, count, outp);
            }
        }
    }

    /// Cuts one oversized, solidly-filled blob into a splitX by splitY grid of pieces sized to a
    /// real prop's footprint (SplitUnit), so a row of crates standing flush against each other
    /// comes out as one box per crate instead of one box for the whole row. Each piece keeps only
    /// its OWN member cells' extent and colour rather than the grid cell's nominal rectangle, so a
    /// ragged cluster still yields honestly-sized boxes instead of a neat but wrong checkerboard.
    private static void SplitBlob(List<int> members, float[] cr, float[] cg, float[] cb, int nx,
                                  float x0, float y0, int minGx, int minGy, int bboxW, int bboxH,
                                  int splitX, int splitY, List<FoundProp> outp)
    {
        int buckets = splitX * splitY;
        var count = new int[buckets];
        var bMinGx = new int[buckets]; var bMaxGx = new int[buckets];
        var bMinGy = new int[buckets]; var bMaxGy = new int[buckets];
        var bSumR = new float[buckets]; var bSumG = new float[buckets]; var bSumB = new float[buckets];
        for (int b = 0; b < buckets; b++) { bMinGx[b] = int.MaxValue; bMinGy[b] = int.MaxValue; }

        foreach (int i in members)
        {
            int gx = i % nx, gy = i / nx;
            int sx = NfMath.ClampInt((int)((gx - minGx) / (float)bboxW * splitX), 0, splitX - 1);
            int sy = NfMath.ClampInt((int)((gy - minGy) / (float)bboxH * splitY), 0, splitY - 1);
            int b = sy * splitX + sx;

            count[b]++;
            if (gx < bMinGx[b]) bMinGx[b] = gx;
            if (gx > bMaxGx[b]) bMaxGx[b] = gx;
            if (gy < bMinGy[b]) bMinGy[b] = gy;
            if (gy > bMaxGy[b]) bMaxGy[b] = gy;
            bSumR[b] += cr[i]; bSumG[b] += cg[i]; bSumB[b] += cb[i];
        }

        for (int b = 0; b < buckets; b++)
        {
            // A ragged cluster does not fill every grid cell evenly (an L-shaped group of three
            // crates leaves one corner bucket empty) - skip whatever genuinely has nothing in it,
            // same MinCells bar as an ordinary blob so a sliver at a bucket's edge is not kept.
            if (count[b] < MinCells) continue;

            float bx0 = x0 + bMinGx[b] * Cell, bx1 = x0 + (bMaxGx[b] + 1) * Cell;
            float by0 = y0 + bMinGy[b] * Cell, by1 = y0 + (bMaxGy[b] + 1) * Cell;
            EmitProp(bx0, by0, bx1, by1, bSumR[b], bSumG[b], bSumB[b], count[b], outp);
        }
    }

    private static void EmitProp(float bx0, float by0, float bx1, float by1,
                                 float sumR, float sumG, float sumB, int count,
                                 List<FoundProp> outp)
    {
        var col = new NfColor(sumR / count, sumG / count, sumB / count);
        float area = (bx1 - bx0) * (by1 - by0);

        outp.Add(new FoundProp
        {
            Min = new NfVec2(bx0, by0),
            Max = new NfVec2(bx1, by1),
            Color = col,
            // Height is invented, because a top-down photograph cannot contain it. Small
            // footprints are consoles and crates, large ones are tables and tanks, and nothing
            // is allowed to grow tall enough to block the room.
            Height = NfMath.Clamp(0.34f + area * 0.16f, 0.34f, 0.95f),
            Lit = area < 1.1f && (col.R + col.G + col.B) / 3f > 0.28f,
        });
    }
}
