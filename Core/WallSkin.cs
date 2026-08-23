// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * WallSkin - a wall's texture, read off the wall itself.
 *
 * THE PROBLEM WITH ONE COLOUR PER WALL
 * ------------------------------------
 * Until now a wall got a neutral drawn texture plus a single tint colour, sampled somewhere near
 * its middle. That is one number for a surface that in the artwork is never one number: a wall runs
 * past a door frame, through a window band, into a corner where the room changes colour, along a
 * strip of hazard yellow, behind a pipe. Averaging all of that produced a flat panel in a colour
 * that appears nowhere on the real wall, and rooms that were supposed to look different came out
 * looking the same.
 *
 * WHAT THE MAP ACTUALLY CONTAINS
 * ------------------------------
 * Among Us draws its walls in three-quarter view: seen from above, a wall is a band roughly forty
 * centimetres wide, and that band IS the face of the wall, in the exact colours the artists chose,
 * with the door frames and window panes and colour changes already in the right places along it.
 * The photograph of the map contains that band, at fifty-two pixels per unit.
 *
 * So the band is read column by column along the wall, and each column becomes one column of the
 * wall's texture. Everything horizontal in the picture - where the wall changes colour, where the
 * window is - comes straight from the game. Only the VERTICAL arrangement is invented, because a
 * drawing seen from above genuinely does not say what is at knee height:
 *
 *      top edge      ink outline, the way the game outlines everything
 *      upper face    the lightest tone found in the band: the top of the wall, catching the sky
 *      panel         the band's own main tone, the largest part of the surface
 *      skirting      its darkest tone, along the floor, where the game paints its contact shadow
 *
 * Those four tones are not invented: they are the lightest, most common and darkest colours found
 * in the band at that exact point along the wall. A wall built this way is in the colours of the
 * room it belongs to, at the positions the artwork puts them, and reads as Polus rather than as a
 * corridor in some other game.
 */

using System;

namespace Nightfall.Core;

public static class WallSkin
{
    /// How wide a strip of the photograph, centred on the collider line, counts as "the wall".
    /// Polus' wall strokes are about this wide; a wider band starts eating the floor on one side
    /// and the ground outside on the other.
    private const float BandWidth = 0.44f;

    /// Taps across the band per column. Nine is enough to find the outline, the face and the top
    /// without the sort below mattering to the frame budget.
    private const int Taps = 9;

    /// Texture resolution. Horizontally the wall is sampled about sixteen times per world unit,
    /// which is a third of the photograph's own detail and still finer than the wall is ever drawn
    /// on screen; vertically it carries the drawn panel structure and wants a little more room.
    private const int PixelsPerUnitU = 16;
    private const int MinU = 4, MaxU = 96;
    private const int TexV = 48;

    /// How far the tones are averaged ALONG the wall, in columns either side.
    ///
    /// The drawn band is only about twenty pixels wide, so a single column of taps sometimes lands
    /// on the wall face and sometimes half on the floor beside it, and the colour jittered from
    /// column to column. Rendered, that jitter became vertical streaks running the full height of
    /// the wall - the walls looked like melted glass. Averaging over a few columns removes the
    /// jitter and keeps the real changes, which are all much wider than this.
    private const int SmoothRadius = 3;

    /// How much world the drawn panel structure spans before it repeats.
    private const float StructureSpan = 2.0f;

    // ================================================================================
    /// Builds the texture for one wall segment. Returns null when there is no photograph to read,
    /// in which case the caller keeps the procedural surface.
    public static Surface3D Build(MapAtlas atlas, in Segment seg, float wallTop)
    {
        if (atlas == null || !atlas.IsValid || seg.Length < 0.01f) return null;

        int w = Math.Clamp((int)MathF.Round(seg.Length * PixelsPerUnitU), MinU, MaxU);
        int h = TexV;

        var dir = new NfVec2(seg.Dir.X / seg.Length, seg.Dir.Y / seg.Length);
        var nrm = new NfVec2(-dir.Y, dir.X);

        // ---- pass one: three tones per column, read off the photograph ----
        var darkC = new NfColor[w];
        var mainC = new NfColor[w];
        var lightC = new NfColor[w];

        Span<float> lum = stackalloc float[Taps];
        Span<int> order = stackalloc int[Taps];
        Span<float> tr = stackalloc float[Taps];
        Span<float> tg = stackalloc float[Taps];
        Span<float> tb = stackalloc float[Taps];

        for (int x = 0; x < w; x++)
        {
            float t = (x + 0.5f) / w;
            float wx = seg.A.X + dir.X * seg.Length * t;
            float wy = seg.A.Y + dir.Y * seg.Length * t;

            int n = 0;
            for (int j = 0; j < Taps; j++)
            {
                float off = (-0.5f + (j + 0.5f) / Taps) * BandWidth;
                if (!atlas.SampleBilinear(wx + nrm.X * off, wy + nrm.Y * off,
                                          out float r, out float g, out float b))
                    continue;
                tr[n] = r; tg[n] = g; tb[n] = b;
                lum[n] = r * 0.3f + g * 0.6f + b * 0.1f;
                order[n] = n;
                n++;
            }
            if (n == 0)
            {
                darkC[x] = Shade(StationWall, 0.55f);
                mainC[x] = StationWall;
                lightC[x] = Shade(StationWall, 1.22f);
                continue;
            }

            // Insertion sort by luminance: nine entries, and it keeps this allocation free.
            for (int i = 1; i < n; i++)
            {
                int k = order[i];
                int j2 = i - 1;
                while (j2 >= 0 && lum[order[j2]] > lum[k]) { order[j2 + 1] = order[j2]; j2--; }
                order[j2 + 1] = k;
            }

            var dark = At(tr, tg, tb, order[Math.Min(n - 1, n / 6)]);
            var main = At(tr, tg, tb, order[n / 2]);
            var light = At(tr, tg, tb, order[Math.Min(n - 1, (int)(n * 0.82f))]);

            // A band that is mostly the violet of the planet surface is not a wall face, it is the
            // ground the wall stands on: the collider ran along the edge of the artwork rather than
            // through it. The station's own wall colour is always closer than that.
            if (IsGround(main)) { main = StationWall; light = Shade(main, 1.22f); dark = Shade(main, 0.55f); }

            darkC[x] = dark; mainC[x] = main; lightC[x] = light;
        }

        Smooth(darkC); Smooth(mainC); Smooth(lightC);

        // ---- pass two: the drawn wall, coloured by those tones ----
        //
        // The STRUCTURE - panel, rail, skirting, seams, ink outlines - comes from the drawn wall
        // surface, in neutral grey. The COLOUR comes from the photograph, per column. Neither alone
        // was enough: structure without the map's colours is a corridor in some other game, and
        // colours without structure is a flat sheet that reads as fog.
        var structure = AuSurfaces.Get(SurfaceRole.Wall);
        var px = new byte[w * h * 4];

        for (int x = 0; x < w; x++)
        {
            float alongUnits = (x + 0.5f) / w * seg.Length;
            float u = alongUnits / StructureSpan;

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                structure.Sample(u, v, out float sr, out float sg, out float sb, out _);

                // Which of the three tones this height wears. The top of the wall catches the sky,
                // the floor line sits in the game's own contact shadow.
                var tone = v < 0.16f ? lightC[x]
                         : v < 0.24f ? NfColor.Lerp(lightC[x], mainC[x], (v - 0.16f) / 0.08f)
                         : v < 0.82f ? mainC[x]
                         : NfColor.Lerp(mainC[x], darkC[x], NfMath.Clamp01((v - 0.82f) / 0.18f));

                // ToByteRAW, not ToByte (AUDIT-2026-08-23, L-26): this is TEXTURE data, and
                // ToByte carries the torch's highlight-compression curve, which belongs at
                // the very end of the pipeline where a LIT value becomes a pixel. Applied
                // here it darkened every source value above 0.75 - up to 12.5% at full
                // white - and then the renderer applied the same curve again to the lit
                // result. Bright wall and prop surfaces came out muddy for no reason, and
                // ToByteRaw's own doc comment already said which one belongs here.
                int o = (y * w + x) * 4;
                px[o] = NfMath.ToByteRaw(sr * tone.R);
                px[o + 1] = NfMath.ToByteRaw(sg * tone.G);
                px[o + 2] = NfMath.ToByteRaw(sb * tone.B);
                // Alpha is the TINT MASK, not opacity. These colours are already the wall's own, so
                // it is zero everywhere: nothing here may be recoloured a second time.
                px[o + 3] = 0;
            }
        }

        return new Surface3D(px, w, h);
    }

    /// Box blur along the wall, clamped at the ends.
    private static void Smooth(NfColor[] c)
    {
        if (c.Length <= 2) return;
        var src = (NfColor[])c.Clone();
        for (int i = 0; i < c.Length; i++)
        {
            float r = 0f, g = 0f, b = 0f;
            int n = 0;
            for (int d = -SmoothRadius; d <= SmoothRadius; d++)
            {
                int j = Math.Clamp(i + d, 0, c.Length - 1);
                r += src[j].R; g += src[j].G; b += src[j].B; n++;
            }
            c[i] = new NfColor(r / n, g / n, b / n);
        }
    }

    private static NfColor At(Span<float> r, Span<float> g, Span<float> b, int i) =>
        new(r[i], g[i], b[i]);

    private static NfColor Shade(NfColor c, float f) => new(c.R * f, c.G * f, c.B * f);

    /// Polus' ground violet: red and blue well above green.
    private static bool IsGround(NfColor c) => (c.R + c.B) * 0.5f - c.G > 0.06f;

    private static readonly NfColor StationWall = new(0.255f, 0.325f, 0.290f);
}
