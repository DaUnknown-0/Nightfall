// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * GeometryModel - the world the first-person view is raycast against.
 *
 * WHY A SEGMENT MODEL AND NOT Physics2D
 * -------------------------------------
 * The obvious way to raycast a wall in Unity is Physics2D.Raycast. It is also the way that would
 * sink this feature: at 320 columns per frame it means 320 managed-to-native transitions every
 * single frame, on an Il2Cpp interop layer where that cost is entirely unmeasured, in a mod family
 * whose notes already record one case of a small Il2Cpp method taking the whole process down when
 * it was detoured. So the geometry is lifted out of Unity exactly ONCE per round, converted into
 * plain line segments, and every ray after that is cast in pure C# against those segments. Nothing
 * crosses the interop boundary during rendering at all.
 *
 * The second reason is verification. A model made of plain numbers can be loaded by the offline
 * render tool, which has no Unity, and that is what makes it possible to look at the finished view
 * as a PNG before the game is ever started.
 *
 * THE UNIFORM GRID
 * ----------------
 * Polus produces on the order of a thousand segments. Testing every one of them against every one
 * of 320 rays would be 320.000 intersection tests per frame, which is wasteful rather than fatal,
 * but it scales badly with taller maps like Airship. So segments are bucketed into a uniform grid
 * and each ray walks the grid cell by cell (a DDA, the same traversal the classic raycasters use)
 * testing only the handful of segments in the cells it actually crosses. A visit stamp keeps a
 * segment that spans several cells from being tested twice by the same ray.
 *
 * A uniform grid rather than a BSP or a quadtree because the input is friendly: Among Us maps are
 * flat, bounded, and their walls are spread fairly evenly. The grid costs one array of ints, is
 * rebuilt in microseconds, and has no worst case that a map author could accidentally trigger.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

/// How tall a piece of geometry stands in the first-person view. The value is derived from the
/// physics layer it came from, which is why the view is instantly readable: Among Us already sorts
/// its colliders into "wall", "tall thing" and "thing you can see over", it just never had a
/// perspective in which that distinction was visible.
public enum WallHeight : byte
{
    /// Full wall, floor to ceiling. Blocks sight completely.
    Full = 0,
    /// Waist-high: railings, desks, consoles, crates. You see over it, but not through it.
    Low = 1,
    /// Tall obstacle that is not architecture: machines, tanks, pillars.
    Tall = 2,
}

public struct Segment
{
    public NfVec2 A, B;
    /// Index into MaterialTable.Materials.
    public byte Material;
    public WallHeight Height;
    /// Index of the source collider. Doors keep this so they can be toggled at runtime without
    /// rebuilding the model.
    public int SourceId;
    /// Precomputed B - A, because the intersection test needs it for every single ray.
    public NfVec2 Dir;
    public float Length;

    /// The colour of the room this wall belongs to, read out of the map's own artwork once when
    /// the model is built (see MapAtlas). Zero alpha-equivalent means "no photograph available",
    /// in which case the renderer falls back to the procedural surface colour.
    public byte TintR, TintG, TintB;
    public bool HasTint;

    public void Finish()
    {
        Dir = B - A;
        Length = Dir.Length;
    }
}

/// What a ray found.
public struct RayHit
{
    /// Room colour of the hit segment, straight from the map artwork. Copied out of the segment so
    /// the renderer never has to touch the segment array again.
    public byte TintR, TintG, TintB;
    public bool HasTint;
    public bool Hit;
    /// Distance along the ray, in world units.
    public float Distance;
    public NfVec2 Point;
    /// Texture coordinate along the wall, in world units from the segment's A end. Using world
    /// units rather than a normalised 0..1 keeps the texture the same physical size on a two-metre
    /// wall and a twenty-metre one, which is what stops long corridors from looking smeared.
    public float U;
    public byte Material;
    public WallHeight Height;
    public int SegmentIndex;
    /// True when the ray hit the segment's back face. Used to shade the two sides of a wall
    /// slightly differently, which is what gives corners their edge without any lighting model.
    public bool Backface;
    /// Unit normal of the hit segment, always pointing back towards the ray's origin. The renderer
    /// walks along it to read the wall's drawn face out of the map artwork.
    public NfVec2 Normal;
}

public sealed class GeometryModel
{
    public Segment[] Segments = Array.Empty<Segment>();

    // ---- bounds ----
    public NfVec2 Min, Max;

    // ---- uniform grid, CSR layout ----
    private float cellSize = 2f;
    private int gridW, gridH;
    private int[] cellStart = Array.Empty<int>();   // length gridW*gridH + 1
    private int[] cellItems = Array.Empty<int>();   // segment indices, grouped by cell

    /// Per-segment stamp of the last ray that tested it. An int compare replaces the hash set a
    /// naive implementation would reach for, and costs nothing per ray.
    private int[] visitStamp = Array.Empty<int>();
    private int rayCounter;

    /// Segments belonging to a collider that is currently switched off (an open door). Checked per
    /// hit rather than rebuilt into the grid, because doors open and close constantly and a rebuild
    /// mid-round would stutter.
    private readonly HashSet<int> disabledSources = new();

    public int SegmentCount => Segments.Length;
    public float CellSize => cellSize;
    public int GridWidth => gridW;
    public int GridHeight => gridH;

    // ================================================================================
    // Build
    // ================================================================================
    public void SetSegments(List<Segment> segments, float targetCellSize = 2f)
    {
        Segments = segments.ToArray();
        for (int i = 0; i < Segments.Length; i++) Segments[i].Finish();
        cellSize = MathF.Max(0.25f, targetCellSize);
        visitStamp = new int[Segments.Length];
        rayCounter = 0;
        BuildGrid();
    }

    public void SetSourceEnabled(int sourceId, bool enabled)
    {
        if (enabled) disabledSources.Remove(sourceId);
        else disabledSources.Add(sourceId);
    }

    public void ClearDisabledSources() => disabledSources.Clear();

    private void BuildGrid()
    {
        if (Segments.Length == 0)
        {
            Min = new NfVec2(0f, 0f);
            Max = new NfVec2(1f, 1f);
            gridW = gridH = 1;
            cellStart = new int[2];
            cellItems = Array.Empty<int>();
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var s in Segments)
        {
            minX = MathF.Min(minX, MathF.Min(s.A.X, s.B.X));
            minY = MathF.Min(minY, MathF.Min(s.A.Y, s.B.Y));
            maxX = MathF.Max(maxX, MathF.Max(s.A.X, s.B.X));
            maxY = MathF.Max(maxY, MathF.Max(s.A.Y, s.B.Y));
        }

        // One cell of padding on every side so a ray that starts exactly on the boundary still has
        // a cell to begin in.
        Min = new NfVec2(minX - cellSize, minY - cellSize);
        Max = new NfVec2(maxX + cellSize, maxY + cellSize);

        gridW = Math.Max(1, (int)MathF.Ceiling((Max.X - Min.X) / cellSize));
        gridH = Math.Max(1, (int)MathF.Ceiling((Max.Y - Min.Y) / cellSize));

        int cells = gridW * gridH;
        var counts = new int[cells + 1];

        // Two passes over the segments: count first, then fill. That produces the compact CSR
        // layout (one int array for the data, one for the offsets) instead of a list per cell,
        // which matters because this is the array the ray walk touches most.
        ForEachSegmentCell((seg, cell) => counts[cell + 1]++);

        cellStart = new int[cells + 1];
        for (int i = 0; i < cells; i++) cellStart[i + 1] = cellStart[i] + counts[i + 1];

        cellItems = new int[cellStart[cells]];
        var cursor = new int[cells];
        ForEachSegmentCell((seg, cell) =>
        {
            cellItems[cellStart[cell] + cursor[cell]] = seg;
            cursor[cell]++;
        });
    }

    /// Visits every (segment, cell) pair. A segment is registered in every cell its line passes
    /// through, walked with the same DDA the ray uses, so a long wall really is findable from every
    /// cell it crosses instead of only from the two cells holding its endpoints.
    private void ForEachSegmentCell(Action<int, int> visit)
    {
        for (int i = 0; i < Segments.Length; i++)
        {
            var s = Segments[i];
            int x0 = CellX(s.A.X), y0 = CellY(s.A.Y);
            int x1 = CellX(s.B.X), y1 = CellY(s.B.Y);

            // Degenerate or single-cell segment.
            if (x0 == x1 && y0 == y1)
            {
                visit(i, Index(x0, y0));
                continue;
            }

            // Walk the supporting line cell by cell.
            float dx = s.B.X - s.A.X, dy = s.B.Y - s.A.Y;
            int stepX = dx > 0f ? 1 : -1, stepY = dy > 0f ? 1 : -1;
            float tDeltaX = MathF.Abs(dx) < 1e-9f ? float.MaxValue : MathF.Abs(cellSize / dx);
            float tDeltaY = MathF.Abs(dy) < 1e-9f ? float.MaxValue : MathF.Abs(cellSize / dy);

            float nextBoundaryX = Min.X + (x0 + (stepX > 0 ? 1 : 0)) * cellSize;
            float nextBoundaryY = Min.Y + (y0 + (stepY > 0 ? 1 : 0)) * cellSize;
            float tMaxX = MathF.Abs(dx) < 1e-9f ? float.MaxValue : (nextBoundaryX - s.A.X) / dx;
            float tMaxY = MathF.Abs(dy) < 1e-9f ? float.MaxValue : (nextBoundaryY - s.A.Y) / dy;

            int cx = x0, cy = y0;
            visit(i, Index(cx, cy));

            int guard = 0;
            int maxSteps = gridW + gridH + 4;
            while (guard++ < maxSteps)
            {
                if (tMaxX < tMaxY)
                {
                    if (tMaxX > 1f) break;
                    cx += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    if (tMaxY > 1f) break;
                    cy += stepY;
                    tMaxY += tDeltaY;
                }
                if (cx < 0 || cy < 0 || cx >= gridW || cy >= gridH) break;
                visit(i, Index(cx, cy));
                if (cx == x1 && cy == y1) break;
            }
        }
    }

    private int CellX(float worldX) => NfMath.ClampInt((int)((worldX - Min.X) / cellSize), 0, gridW - 1);
    private int CellY(float worldY) => NfMath.ClampInt((int)((worldY - Min.Y) / cellSize), 0, gridH - 1);
    private int Index(int cx, int cy) => cy * gridW + cx;

    // ================================================================================
    // The ray cast
    // ================================================================================
    /// Casts a ray and returns the nearest segment it meets.
    ///
    /// `ignoreLow` lets the caller ask for the nearest FULL-height wall while ignoring waist-high
    /// furniture, which is exactly what the renderer needs: the low pieces are drawn as their own
    /// short columns in front of whatever wall stands behind them, so the wall behind has to be
    /// found as well, not occluded away.
    public bool Raycast(NfVec2 origin, NfVec2 dir, float maxDistance, out RayHit hit,
                        bool ignoreLow = false)
    {
        hit = default;
        if (Segments.Length == 0) return false;

        int stamp = ++rayCounter;
        float best = maxDistance;
        int bestIdx = -1;
        float bestU = 0f;
        bool bestBack = false;

        // ---- DDA setup ----
        float px = origin.X, py = origin.Y;
        int cx = CellX(px), cy = CellY(py);

        int stepX = dir.X > 0f ? 1 : -1, stepY = dir.Y > 0f ? 1 : -1;
        float tDeltaX = MathF.Abs(dir.X) < 1e-9f ? float.MaxValue : MathF.Abs(cellSize / dir.X);
        float tDeltaY = MathF.Abs(dir.Y) < 1e-9f ? float.MaxValue : MathF.Abs(cellSize / dir.Y);

        float boundX = Min.X + (cx + (stepX > 0 ? 1 : 0)) * cellSize;
        float boundY = Min.Y + (cy + (stepY > 0 ? 1 : 0)) * cellSize;
        float tMaxX = MathF.Abs(dir.X) < 1e-9f ? float.MaxValue : (boundX - px) / dir.X;
        float tMaxY = MathF.Abs(dir.Y) < 1e-9f ? float.MaxValue : (boundY - py) / dir.Y;

        int guard = 0;
        int maxSteps = gridW + gridH + 4;

        while (guard++ < maxSteps)
        {
            // ---- test this cell ----
            int cell = Index(cx, cy);
            int from = cellStart[cell], to = cellStart[cell + 1];
            for (int k = from; k < to; k++)
            {
                int si = cellItems[k];
                if (visitStamp[si] == stamp) continue;
                visitStamp[si] = stamp;

                ref var seg = ref Segments[si];
                if (ignoreLow && seg.Height == WallHeight.Low) continue;
                if (disabledSources.Count > 0 && disabledSources.Contains(seg.SourceId)) continue;

                if (!IntersectRaySegment(origin, dir, seg, out float t, out float u, out bool back))
                    continue;
                if (t >= best || t < 0f) continue;

                best = t;
                bestIdx = si;
                bestU = u;
                bestBack = back;
            }

            // A hit inside this cell is only final once the ray has left the cell: a segment
            // registered in a LATER cell can still start inside this one and be nearer. The exit
            // distance is the correct cut-off, and it is why the loop tests it after the cell.
            float cellExit = MathF.Min(tMaxX, tMaxY);
            if (bestIdx >= 0 && best <= cellExit) break;
            if (cellExit > maxDistance) break;

            if (tMaxX < tMaxY) { cx += stepX; tMaxX += tDeltaX; }
            else { cy += stepY; tMaxY += tDeltaY; }

            if (cx < 0 || cy < 0 || cx >= gridW || cy >= gridH) break;
        }

        if (bestIdx < 0) return false;

        ref var s2 = ref Segments[bestIdx];
        hit.Hit = true;
        hit.Distance = best;
        hit.Point = origin + dir * best;
        hit.U = bestU;
        hit.Material = s2.Material;
        hit.Height = s2.Height;
        hit.SegmentIndex = bestIdx;
        hit.Backface = bestBack;
        // Segment normal, flipped to face the viewer.
        float nx = -s2.Dir.Y / MathF.Max(1e-6f, s2.Length);
        float ny = s2.Dir.X / MathF.Max(1e-6f, s2.Length);
        if (nx * dir.X + ny * dir.Y > 0f) { nx = -nx; ny = -ny; }
        hit.Normal = new NfVec2(nx, ny);
        hit.TintR = s2.TintR;
        hit.TintG = s2.TintG;
        hit.TintB = s2.TintB;
        hit.HasTint = s2.HasTint;
        return true;
    }

    /// Ray/segment intersection by the cross-product form. `t` comes back in ray-length units and
    /// `u` in world units along the segment, ready to be used as a texture coordinate.
    private static bool IntersectRaySegment(NfVec2 ro, NfVec2 rd, in Segment seg,
                                            out float t, out float u, out bool backface)
    {
        t = 0f; u = 0f; backface = false;

        NfVec2 sd = seg.Dir;
        float denom = NfVec2.Cross(rd, sd);
        if (MathF.Abs(denom) < 1e-9f) return false;   // parallel

        NfVec2 diff = seg.A - ro;
        float tt = NfVec2.Cross(diff, sd) / denom;
        if (tt < 0f) return false;

        float ss = NfVec2.Cross(diff, rd) / denom;
        if (ss < 0f || ss > 1f) return false;

        t = tt;
        u = ss * seg.Length;
        // The segment's normal is (-dy, dx). If the ray runs WITH the normal it arrives from
        // behind, which the renderer uses to shade the two faces of a wall differently.
        backface = (rd.X * -sd.Y + rd.Y * sd.X) > 0f;
        return true;
    }

    /// Straight point-in-world query used by the spawn/sanity checks and by the offline tool when
    /// it places a virtual camera: returns true when the point sits inside no wall.
    public bool IsClearOfWalls(NfVec2 p, float radius)
    {
        float r2 = radius * radius;
        int cx = CellX(p.X), cy = CellY(p.Y);
        int span = Math.Max(1, (int)MathF.Ceiling(radius / cellSize));
        for (int y = cy - span; y <= cy + span; y++)
        {
            if (y < 0 || y >= gridH) continue;
            for (int x = cx - span; x <= cx + span; x++)
            {
                if (x < 0 || x >= gridW) continue;
                int cell = Index(x, y);
                for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                {
                    ref var s = ref Segments[cellItems[k]];
                    if (s.Height == WallHeight.Low) continue;
                    if (SqrDistancePointSegment(p, s) < r2) return false;
                }
            }
        }
        return true;
    }

    private static float SqrDistancePointSegment(NfVec2 p, in Segment s)
    {
        float len2 = s.Dir.SqrLength;
        if (len2 < 1e-9f) return (p - s.A).SqrLength;
        float t = NfMath.Clamp01(NfVec2.Dot(p - s.A, s.Dir) / len2);
        return (p - (s.A + s.Dir * t)).SqrLength;
    }
}
