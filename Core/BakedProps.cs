// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * BakedProps - the furniture that has no object of its own.
 *
 * THE GAP THIS CLOSES
 * -------------------
 * SpriteHarvest gets every object that is a SpriteRenderer, which on Polus is a hundred and fifty
 * of them. But a lot of the station's furniture is not an object at all: the row of green lockers
 * in Science, the sinks and the tub in the bathroom, the counters, the medbay bed are all PAINTED
 * INTO the room's single `<Room>/Walls` drawing. Without them the rooms come out as empty shells
 * with a beautifully accurate floor.
 *
 * WHAT WAS TRIED FIRST, AND WHY IT FAILED
 * ---------------------------------------
 * The obvious hook is the collider: everything solid has one, the collider says where the object
 * is, the photograph says what it looks like. Measured, that turned out to be worth nothing here.
 * Polus has fifty non-trigger colliders on the object layers; thirty-three already belong to a
 * harvested sprite and the remaining seventeen are, every single one of them, the shadow collider
 * of a door. THE PAINTED-IN FURNITURE HAS NO COLLIDER AT ALL - you walk straight through the
 * lockers in the game. So the picture is the only witness.
 *
 * HOW THE PICTURE IS READ
 * -----------------------
 * The old PropFinder asked the same question of the same pixels and got a box the size of the
 * laboratory. The difference here is that the question is asked the other way round, and from a
 * position of knowledge:
 *
 *   1. The map itself says where a player can WALK: inside a room, clear of every wall collider.
 *      Sampled on a grid, that is a few hundred points per room which are floor BY DEFINITION.
 *   2. The colours at those points are the floor's own palette - four or five flat fills, because
 *      that is how Among Us paints, including the accent tiles and the grout.
 *   3. A flood fill runs from those points across every pixel matching the palette. It reaches
 *      the whole floor of the room and stops at everything that is not floor.
 *   4. The wall bands are erased first, so the ring of wall around the room cannot connect all the
 *      furniture into one blob - which is exactly the failure that produced the room-sized box.
 *
 * What the flood never reached, in one connected piece, is one object. PropFinder guessed at what
 * an object might look like; this states what the floor IS and takes the complement.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public static class BakedProps
{
    /// Colour distance, per channel, within which a pixel counts as one of the floor's own colours.
    /// Among Us fills are flat, so a real match is nearly exact; loose thresholds start eating the
    /// furniture.
    private const float FloorTolerance = 0.06f;

    /// Below this share of the sampled floor, a colour is a stain rather than a surface.
    private const float PaletteShare = 0.015f;
    private const int MaxPaletteEntries = 12;

    /// Half-width of the drawn wall band that is erased before anything is measured.
    private const float WallHalfWidth = 0.26f;

    /// Spacing of the walkable probes, in world units.
    private const float ProbeStep = 0.3f;

    // ---- what counts as an object ----
    private const float MinSide = 0.18f;
    private const float MaxSide = 4.0f;
    private const float MinAreaUnits = 0.035f;
    private const float MinFill = 0.14f;

    /// THE TEST THAT SEPARATES AN OBJECT FROM A PATCH OF GROUND.
    ///
    /// The flood fill answers "this is not floor", and a great deal is not floor: a drift of snow,
    /// a shadow across the tiles, a strip of wall the mask missed, the edge of a differently
    /// coloured surface. Stood up, every one of those is a slab of ground hanging in the air, and
    /// the first sheet of results was about half of them.
    ///
    /// Among Us' own drawing rules settle it. A THING is outlined - a thick, near-black stroke all
    /// the way round, on every console, crate, rock and snowman in the game. A SURFACE is not: the
    /// snow simply meets the dust, and the tiles simply meet the carpet. So the outer ring of each
    /// candidate is measured, and a candidate that is not outlined is not an object.
    private const float InkLuminance = 0.30f;
    private const float MinOutlined = 0.45f;

    // ================================================================================
    public static List<PropPiece> Extract(MapModel map, IReadOnlyList<PropPiece> harvested)
    {
        var result = new List<PropPiece>();
        var atlas = map.Atlas;
        if (atlas == null || !atlas.IsValid || map.Rooms.Count == 0) return result;

        foreach (var room in map.Rooms)
        {
            if (room.Max.X - room.Min.X < 0.5f || room.Max.Y - room.Min.Y < 0.5f) continue;
            ExtractRoom(map, atlas, room, harvested, result);
        }

        Scene3D.NightfallLog($"[BakedProps] {result.Count} objects read out of the room artwork");
        return result;
    }

    // ================================================================================
    private static void ExtractRoom(MapModel map, MapAtlas atlas, RoomInfo room,
                                    IReadOnlyList<PropPiece> harvested, List<PropPiece> into)
    {
        int px0 = Math.Max(0, PxX(atlas, room.Min.X - 0.3f));
        int py0 = Math.Max(0, PxY(atlas, room.Min.Y - 0.3f));
        int px1 = Math.Min(atlas.Width, PxX(atlas, room.Max.X + 0.3f));
        int py1 = Math.Min(atlas.Height, PxY(atlas, room.Max.Y + 0.3f));
        int w = px1 - px0, h = py1 - py0;
        if (w < 8 || h < 8 || (long)w * h > 4_000_000) return;

        // ---- 1. where can a player stand ----
        var seeds = new List<int>();
        for (float wy = room.Min.Y; wy <= room.Max.Y; wy += ProbeStep)
        {
            for (float wx = room.Min.X; wx <= room.Max.X; wx += ProbeStep)
            {
                var p = new NfVec2(wx, wy);
                if (!map.IsInside(p)) continue;
                if (!map.Geometry.IsClearOfWalls(p, 0.28f)) continue;
                int x = PxX(atlas, wx) - px0, y = PxY(atlas, wy) - py0;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                seeds.Add(y * w + x);
            }
        }
        if (seeds.Count < 12) return;

        // ---- 2. the floor's own palette, measured at those points ----
        var palette = Palette(atlas, px0, py0, w, h, seeds);
        if (palette.Count == 0) return;

        // ---- 3. erase the wall bands ----
        var blocked = new bool[w * h];
        StrikeWalls(map, atlas, px0, py0, w, h, blocked);

        // ---- 4. flood the floor ----
        var floorLike = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            int row = (py0 + y) * atlas.Width;
            for (int x = 0; x < w; x++)
            {
                int o = (row + px0 + x) * 3;
                floorLike[y * w + x] = Matches(palette, atlas.Pixels[o] / 255f,
                    atlas.Pixels[o + 1] / 255f, atlas.Pixels[o + 2] / 255f);
            }
        }

        var reached = new bool[w * h];
        var stack = new Stack<int>();
        foreach (int s in seeds)
        {
            if (reached[s] || !floorLike[s]) continue;
            reached[s] = true;
            stack.Push(s);
        }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % w, y = i / w;
            void Step(int nx, int ny)
            {
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                int j = ny * w + nx;
                if (reached[j] || !floorLike[j] || blocked[j]) return;
                reached[j] = true;
                stack.Push(j);
            }
            Step(x - 1, y); Step(x + 1, y); Step(x, y - 1); Step(x, y + 1);
        }

        // ---- 5. what is left, in connected pieces ----
        float ppu = atlas.PixelsPerUnit;
        var labelled = new bool[w * h];
        var stamp = new int[w * h];
        int gen = 0;
        var members = new List<int>();
        var queue = new Queue<int>();

        for (int start = 0; start < w * h; start++)
        {
            if (labelled[start] || reached[start] || blocked[start]) continue;

            members.Clear();
            queue.Clear();
            labelled[start] = true;
            queue.Enqueue(start);
            int minX = w, minY = h, maxX = -1, maxY = -1;

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                members.Add(i);
                int x = i % w, y = i / w;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                // Eight-connected: Among Us' outlines are diagonal as often as not, and four-way
                // labelling split every rounded object into a handful of slivers.
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (labelled[j] || reached[j] || blocked[j]) continue;
                        labelled[j] = true;
                        queue.Enqueue(j);
                    }
                }
                if (members.Count > 400_000) break;
            }

            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            float uw = cw / ppu, uh = ch / ppu;
            if (uw < MinSide || uh < MinSide) continue;
            if (uw > MaxSide || uh > MaxSide) continue;
            if (members.Count / (ppu * ppu) < MinAreaUnits) continue;
            if (members.Count < cw * ch * MinFill) continue;

            // Running out of the region is a sign the blob is part of something larger - the room's
            // wall, the ground outside - that happens to be cut off here.
            if (minX == 0 || minY == 0 || maxX == w - 1 || maxY == h - 1) continue;

            if (!IsOutlined(atlas, px0, py0, w, h, members, stamp, ++gen)) continue;

            var piece = BuildPiece(atlas, px0, py0, w, minX, minY, cw, ch, members, ppu);
            if (piece == null) continue;
            if (Overlaps(piece, harvested)) continue;
            if (Overlaps(piece, into)) continue;
            into.Add(piece);
        }
    }

    // ================================================================================
    /// How much of the candidate's outer ring is drawn in Among Us' outline ink.
    private static bool IsOutlined(MapAtlas atlas, int px0, int py0, int w, int h,
                                   List<int> members, int[] stamp, int gen)
    {
        foreach (int i in members) stamp[i] = gen;

        int edge = 0, dark = 0;
        foreach (int i in members)
        {
            int x = i % w, y = i / w;
            bool onEdge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                       || stamp[i - 1] != gen || stamp[i + 1] != gen
                       || stamp[i - w] != gen || stamp[i + w] != gen;
            if (!onEdge) continue;

            edge++;
            int o = ((py0 + y) * atlas.Width + px0 + x) * 3;
            float lum = (atlas.Pixels[o] * 0.3f + atlas.Pixels[o + 1] * 0.6f
                         + atlas.Pixels[o + 2] * 0.1f) / 255f;
            if (lum < InkLuminance) dark++;
        }
        return edge > 0 && dark / (float)edge >= MinOutlined;
    }

    private static PropPiece BuildPiece(MapAtlas atlas, int px0, int py0, int regionW,
                                        int minX, int minY, int cw, int ch,
                                        List<int> members, float ppu)
    {
        var rgba = new byte[cw * ch * 4];

        foreach (int i in members)
        {
            int x = i % regionW - minX, y = i / regionW - minY;
            if (x < 0 || y < 0 || x >= cw || y >= ch) continue;
            int so = ((py0 + minY + y) * atlas.Width + px0 + minX + x) * 3;
            int to = (y * cw + x) * 4;
            rgba[to] = atlas.Pixels[so];
            rgba[to + 1] = atlas.Pixels[so + 1];
            rgba[to + 2] = atlas.Pixels[so + 2];
            rgba[to + 3] = 255;
        }

        float worldX0 = atlas.Min.X + (px0 + minX) / ppu;
        float worldY0 = atlas.Min.Y + (py0 + minY) / ppu;

        return new PropPiece
        {
            Path = "baked",
            Name = "baked",
            Min = new NfVec2(worldX0, worldY0),
            Max = new NfVec2(worldX0 + cw / ppu, worldY0 + ch / ppu),
            // The atlas is stored bottom row first and a piece wants its rows top-down.
            Rgba = Flip(rgba, cw, ch),
            W = cw,
            H = ch,
            // Always upright. Anything that belongs on the ground is ALREADY on the ground: it is
            // painted into the photograph the floor is made of, exactly where it is. Laying a copy
            // of it back down on top of itself would buy nothing but triangles.
            Stance = PropStance.Upright,
        };
    }

    private static bool Overlaps(PropPiece p, IReadOnlyList<PropPiece> others)
    {
        float area = MathF.Max(1e-4f, p.WorldWidth * p.WorldHeight);
        foreach (var o in others)
        {
            float ox = MathF.Min(p.Max.X, o.Max.X) - MathF.Max(p.Min.X, o.Min.X);
            float oy = MathF.Min(p.Max.Y, o.Max.Y) - MathF.Max(p.Min.Y, o.Min.Y);
            if (ox <= 0f || oy <= 0f) continue;
            if (ox * oy / area > 0.3f) return true;
        }
        return false;
    }

    private static byte[] Flip(byte[] rgba, int w, int h)
    {
        var o = new byte[rgba.Length];
        for (int y = 0; y < h; y++)
            Array.Copy(rgba, (h - 1 - y) * w * 4, o, y * w * 4, w * 4);
        return o;
    }

    // ================================================================================
    /// Paints the drawn wall bands into the blocked mask, so the ring of wall around a room cannot
    /// join every object in it into one blob.
    private static void StrikeWalls(MapModel map, MapAtlas atlas, int px0, int py0, int w, int h,
                                    bool[] blocked)
    {
        float ppu = atlas.PixelsPerUnit;
        int rad = Math.Max(1, (int)(WallHalfWidth * ppu));

        foreach (var s in map.Geometry.Segments)
        {
            if (s.Height == WallHeight.Low) continue;
            int steps = Math.Max(2, (int)(s.Length * ppu));
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                float wx = s.A.X + (s.B.X - s.A.X) * t;
                float wy = s.A.Y + (s.B.Y - s.A.Y) * t;
                int cx = PxX(atlas, wx) - px0, cy = PxY(atlas, wy) - py0;
                if (cx < -rad || cy < -rad || cx >= w + rad || cy >= h + rad) continue;
                for (int y = cy - rad; y <= cy + rad; y++)
                {
                    if (y < 0 || y >= h) continue;
                    for (int x = cx - rad; x <= cx + rad; x++)
                    {
                        if (x < 0 || x >= w) continue;
                        blocked[y * w + x] = true;
                    }
                }
            }
        }
    }

    private static List<(float r, float g, float b)> Palette(MapAtlas atlas, int px0, int py0,
                                                             int w, int h, List<int> seeds)
    {
        var counts = new Dictionary<int, int>();
        int total = 0;

        foreach (int s in seeds)
        {
            int sx = s % w, sy = s / w;
            for (int dy = -2; dy <= 2; dy++)
            {
                int y = sy + dy;
                if (y < 0 || y >= h) continue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = sx + dx;
                    if (x < 0 || x >= w) continue;
                    int o = ((py0 + y) * atlas.Width + px0 + x) * 3;
                    int key = (atlas.Pixels[o] >> 4) << 8
                            | (atlas.Pixels[o + 1] >> 4) << 4
                            | (atlas.Pixels[o + 2] >> 4);
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                    total++;
                }
            }
        }
        if (total == 0) return new List<(float, float, float)>();

        var ordered = new List<KeyValuePair<int, int>>(counts);
        ordered.Sort((a, b) => b.Value.CompareTo(a.Value));

        var palette = new List<(float r, float g, float b)>();
        foreach (var kv in ordered)
        {
            if (palette.Count >= MaxPaletteEntries) break;
            if (kv.Value < total * PaletteShare) break;
            palette.Add(((((kv.Key >> 8) & 15) * 16 + 8) / 255f,
                         (((kv.Key >> 4) & 15) * 16 + 8) / 255f,
                         ((kv.Key & 15) * 16 + 8) / 255f));
        }
        return palette;
    }

    private static bool Matches(List<(float r, float g, float b)> palette, float r, float g, float b)
        => Near(palette, (r, g, b), FloorTolerance);

    private static bool Near(List<(float r, float g, float b)> palette,
                             (float r, float g, float b) c, float tol)
    {
        foreach (var p in palette)
            if (MathF.Abs(p.r - c.r) <= tol && MathF.Abs(p.g - c.g) <= tol
                && MathF.Abs(p.b - c.b) <= tol) return true;
        return false;
    }

    private static int PxX(MapAtlas a, float wx) =>
        (int)MathF.Round((wx - a.Min.X) / (a.Max.X - a.Min.X) * a.Width);

    private static int PxY(MapAtlas a, float wy) =>
        (int)MathF.Round((wy - a.Min.Y) / (a.Max.Y - a.Min.Y) * a.Height);
}
