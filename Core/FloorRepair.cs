// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * FloorRepair - takes the furniture back out of the floor.
 *
 * The floor is the photograph of the map, sampled per pixel. The photograph was taken from
 * straight above, so it contains the tables, the rocks and the snowmen as well as the ground they
 * stand on. Once those objects are also standing in the scene as objects, every one of them is in
 * the picture twice: once upright where it belongs, and once smeared flat on the floor underneath
 * it, in perfect silhouette, like a decal of itself.
 *
 * So the pixels an object covers are painted out of the floor before the floor is used, and filled
 * with the ground around that object. The fill is a ring median rather than a blur: Among Us'
 * ground is large areas of flat colour with hard edges, and a blur across the edge between violet
 * dust and white snow produces a grey halo that is more visible than the decal was.
 *
 * This only ever touches a COPY of the photograph, kept for the floor. The original stays intact
 * because the walls read their colours out of it and a wall whose band had been painted out would
 * come back grey.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public static class FloorRepair
{
    /// How far outside an object's rectangle the replacement colour is looked for, in world units.
    private const float RingUnits = 0.55f;

    /// Alpha above which an object's pixel counts as covering the ground.
    private const byte Solid = 40;

    /// Returns a copy of the atlas with every standing object erased from it.
    public static MapAtlas Without(MapAtlas atlas, IReadOnlyList<PropPiece> props)
    {
        if (atlas == null || !atlas.IsValid || props == null || props.Count == 0) return atlas;

        var copy = new MapAtlas();
        var px = (byte[])atlas.Pixels.Clone();
        copy.Set(px, atlas.Width, atlas.Height, atlas.Min, atlas.Max);

        float ppu = atlas.PixelsPerUnit;
        int ring = Math.Max(2, (int)(RingUnits * ppu));

        foreach (var p in props)
        {
            // Only what STANDS leaves a false shadow. A ramp or a contact shadow is painted on the
            // ground and belongs in the floor exactly where it is.
            if (p.Stance != PropStance.Upright) continue;

            // The atlas is stored bottom row first, so world y maps straight through.
            int x0 = PxX(atlas, p.Min.X), x1 = PxX(atlas, p.Max.X);
            int y0 = PxY(atlas, p.Min.Y), y1 = PxY(atlas, p.Max.Y);
            if (x1 <= x0 || y1 <= y0) continue;

            if (!RingColour(atlas, x0, y0, x1, y1, ring, out byte fr, out byte fg, out byte fb))
                continue;

            for (int y = y0; y < y1; y++)
            {
                if (y < 0 || y >= atlas.Height) continue;
                // Piece rows run top-down; atlas rows run bottom-up.
                int sy = Math.Clamp((y1 - 1 - y) * p.H / Math.Max(1, y1 - y0), 0, p.H - 1);
                for (int x = x0; x < x1; x++)
                {
                    if (x < 0 || x >= atlas.Width) continue;
                    int sx = Math.Clamp((x - x0) * p.W / Math.Max(1, x1 - x0), 0, p.W - 1);
                    if (p.Rgba[(sy * p.W + sx) * 4 + 3] < Solid) continue;

                    int o = (y * atlas.Width + x) * 3;
                    px[o] = fr; px[o + 1] = fg; px[o + 2] = fb;
                }
            }
        }
        return copy;
    }

    /// The most common colour in a ring around the rectangle: the ground the object stands on.
    private static bool RingColour(MapAtlas atlas, int x0, int y0, int x1, int y1, int ring,
                                   out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var lum = new List<float>(256);
        var off = new List<int>(256);

        void Row(int y)
        {
            if (y < 0 || y >= atlas.Height) return;
            for (int x = x0 - ring; x < x1 + ring; x += 2)
            {
                if (x < 0 || x >= atlas.Width) continue;
                int o = (y * atlas.Width + x) * 3;
                lum.Add(atlas.Pixels[o] * 0.3f + atlas.Pixels[o + 1] * 0.6f + atlas.Pixels[o + 2] * 0.1f);
                off.Add(o);
            }
        }
        void Col(int x)
        {
            if (x < 0 || x >= atlas.Width) return;
            for (int y = y0; y < y1; y += 2)
            {
                if (y < 0 || y >= atlas.Height) continue;
                int o = (y * atlas.Width + x) * 3;
                lum.Add(atlas.Pixels[o] * 0.3f + atlas.Pixels[o + 1] * 0.6f + atlas.Pixels[o + 2] * 0.1f);
                off.Add(o);
            }
        }

        for (int d = 1; d <= ring; d += 2) { Row(y0 - d); Row(y1 + d); Col(x0 - d); Col(x1 + d); }
        if (off.Count == 0) return false;

        // Median by luminance. An average across the boundary between snow and violet dust is a
        // colour that appears nowhere on Polus; the median is one of the two, which is right.
        var idx = new int[off.Count];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        Array.Sort(idx, (a, c) => lum[a].CompareTo(lum[c]));
        int m = off[idx[idx.Length / 2]];
        r = atlas.Pixels[m]; g = atlas.Pixels[m + 1]; b = atlas.Pixels[m + 2];
        return true;
    }

    private static int PxX(MapAtlas a, float wx) =>
        (int)MathF.Round((wx - a.Min.X) / (a.Max.X - a.Min.X) * a.Width);

    private static int PxY(MapAtlas a, float wy) =>
        (int)MathF.Round((wy - a.Min.Y) / (a.Max.Y - a.Min.Y) * a.Height);
}
