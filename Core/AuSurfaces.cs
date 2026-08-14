// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * AuSurfaces - the surfaces of the world, drawn in Among Us' visual language.
 *
 * THE RULES OF THAT LANGUAGE, WRITTEN DOWN
 * ----------------------------------------
 * These were arrived at by getting them wrong first. Everything that looked like a different game
 * broke one of them:
 *   1. FLAT fills. Not gradients, and above all not noise. A surface is one colour, plus at most
 *      two flat bands of light and shade.
 *   2. THICK DARK OUTLINES, in a very dark desaturated blue rather than black.
 *   3. FEW, LARGE shapes. A wall is a panel, a rail, a skirting board and some seams. That is all.
 *   4. ROUNDED corners on anything that is an object rather than architecture.
 *   5. CLEAN edges. Antialiased, never speckled, never weathered, never dirty.
 *
 * The colours come from the map photograph, so a room's surfaces are that room's own colours; the
 * shapes come from here, because the game has no artwork for a vertical surface.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

/// A drawn texture, sampled bilinearly, with a mip pyramid for anything seen small or at an angle.
public sealed class Surface3D
{
    public readonly byte[] Px;
    public readonly int W, H;
    /// True when the texture has transparent areas that must not be drawn at all.
    public readonly bool HasCutout;

    /// NOTE ON THE ALPHA CHANNEL: it is not opacity here, it is the TINT MASK. 1 means "this texel
    /// takes the surface's room colour", 0 means "this texel keeps the colour it was drawn in".
    /// That is what lets one neutral texture serve every room while a window stays night-blue and
    /// a hazard stripe stays yellow.

    /*
     * THE MIP PYRAMID, AND WHY IT IS NOT OPTIONAL
     * -------------------------------------------
     * Bilinear filtering answers "which texel is under this pixel" smoothly. It says nothing about
     * "how MANY texels are under this pixel", and that is the whole problem: a corrugated wall seen
     * along its length, a plated floor stretching to the horizon, or the layered bedrock of the
     * gorge put twenty texels under one pixel, and picking two of the twenty is a lottery that comes
     * out differently for the neighbouring pixel. The result is the striping and the crawling moire
     * that the first playtest reported from four different rooms - never in the middle of a wall you
     * stand in front of, always where a surface runs away from the eye.
     *
     * A pyramid is the standard answer and the only cheap one: each level is the level above
     * averaged two by two, so a level exists whose texels are about the size of a pixel, and the
     * rasteriser picks it from the screen-space derivative of the UVs. The average IS the twenty
     * texels, computed once at build time instead of guessed per frame.
     *
     * The pyramid costs a third of the texture's memory. On this map that is a few hundred kilobytes
     * for every surface in the world.
     *
     * TWO DIFFERENT DOWNSAMPLES, because alpha means two different things here. On a cut-out (a
     * harvested prop) alpha is opacity, so the average has to be PREMULTIPLIED - otherwise the
     * transparent black outside a table's silhouette is averaged into its edge and every object
     * grows a dark halo. On everything else alpha is the tint mask and a plain average is right.
     */
    private readonly byte[][] mips;   // mips[0] is Px
    private readonly int[] mipW, mipH;
    public int MipCount => mips.Length;

    public Surface3D(byte[] rgba, int w, int h, bool cutout = false)
    {
        Px = rgba; W = w; H = h; HasCutout = cutout;

        int levels = 1;
        for (int lw = w, lh = h; lw > 1 || lh > 1; lw = Math.Max(1, lw >> 1), lh = Math.Max(1, lh >> 1))
            levels++;

        mips = new byte[levels][];
        mipW = new int[levels];
        mipH = new int[levels];
        mips[0] = rgba; mipW[0] = w; mipH[0] = h;

        for (int l = 1; l < levels; l++)
        {
            int pw = mipW[l - 1], ph = mipH[l - 1];
            int nw = Math.Max(1, pw >> 1), nh = Math.Max(1, ph >> 1);
            var src = mips[l - 1];
            var dst = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++)
            {
                int y0 = Math.Min(ph - 1, y * 2), y1 = Math.Min(ph - 1, y * 2 + 1);
                for (int x = 0; x < nw; x++)
                {
                    int x0 = Math.Min(pw - 1, x * 2), x1 = Math.Min(pw - 1, x * 2 + 1);
                    int oA = (y0 * pw + x0) * 4, oB = (y0 * pw + x1) * 4;
                    int oC = (y1 * pw + x0) * 4, oD = (y1 * pw + x1) * 4;
                    int o = (y * nw + x) * 4;

                    int a = src[oA + 3] + src[oB + 3] + src[oC + 3] + src[oD + 3];
                    if (cutout)
                    {
                        if (a == 0)
                        {
                            dst[o] = dst[o + 1] = dst[o + 2] = dst[o + 3] = 0;
                            continue;
                        }
                        for (int k = 0; k < 3; k++)
                            dst[o + k] = (byte)((src[oA + k] * src[oA + 3] + src[oB + k] * src[oB + 3]
                                               + src[oC + k] * src[oC + 3] + src[oD + k] * src[oD + 3]) / a);
                    }
                    else
                    {
                        for (int k = 0; k < 3; k++)
                            dst[o + k] = (byte)((src[oA + k] + src[oB + k] + src[oC + k] + src[oD + k]) >> 2);
                    }
                    dst[o + 3] = (byte)(a >> 2);
                }
            }
            mips[l] = dst; mipW[l] = nw; mipH[l] = nh;
        }
    }

    /// Bilinear on one level of the pyramid, linear BETWEEN two levels. The blend between levels is
    /// what keeps a floor from showing the seam where one mip hands over to the next: a hard switch
    /// draws a visible ring on the ground around the player that slides as they walk.
    ///
    /// `lod` is in levels, 0 meaning "one texel per pixel or bigger". Below zero the texture is
    /// magnified and level 0 is the answer for one bilinear fetch, which is the common case for the
    /// wall you are standing at and therefore the case that must stay cheap.
    public bool SampleLod(float u, float v, float lod, out float r, out float g, out float b, out float a)
    {
        if (lod <= 0f) return Sample(u, v, out r, out g, out b, out a);

        int last = mips.Length - 1;
        int l0 = (int)lod;
        if (l0 >= last) return SampleLevel(last, u, v, out r, out g, out b, out a);

        float f = lod - l0;
        SampleLevel(l0, u, v, out r, out g, out b, out a);
        if (f > 0.02f)
        {
            SampleLevel(l0 + 1, u, v, out float r1, out float g1, out float b1, out float a1);
            r += (r1 - r) * f; g += (g1 - g) * f; b += (b1 - b) * f; a += (a1 - a) * f;
        }
        return true;
    }

    /// Bilinear. Among Us' art is all smooth curves and crisp straight lines, and point sampling
    /// ruins both as soon as a wall is closer than a couple of metres.
    ///
    /// Repeating textures WRAP, cut-out ones CLAMP. A cut-out is one object, trimmed to its own
    /// silhouette, so wrapping blends the left edge of a table into its right edge and hangs a
    /// column of the wrong pixels down one side of it.
    public bool Sample(float u, float v, out float r, out float g, out float b, out float a) =>
        SampleLevel(0, u, v, out r, out g, out b, out a);

    private bool SampleLevel(int level, float u, float v,
                             out float r, out float g, out float b, out float a)
    {
        var px = mips[level];
        int W = mipW[level], H = mipH[level];

        if (HasCutout)
        {
            u = NfMath.Clamp01(u);
            v = NfMath.Clamp01(v);
        }
        else
        {
            u -= MathF.Floor(u);
            v -= MathF.Floor(v);
        }

        float fx = u * W - 0.5f, fy = v * H - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float tx = fx - x0, ty = fy - y0;
        int x1, y1;
        if (HasCutout)
        {
            x0 = Math.Clamp(x0, 0, W - 1); y0 = Math.Clamp(y0, 0, H - 1);
            x1 = Math.Min(x0 + 1, W - 1); y1 = Math.Min(y0 + 1, H - 1);
        }
        else
        {
            x1 = ((x0 + 1) % W + W) % W; y1 = ((y0 + 1) % H + H) % H;
            x0 = (x0 % W + W) % W; y0 = (y0 % H + H) % H;
        }

        var Px = px;
        int o00 = (y0 * W + x0) * 4, o10 = (y0 * W + x1) * 4;
        int o01 = (y1 * W + x0) * 4, o11 = (y1 * W + x1) * 4;

        // Written out rather than looped through a local function. That function captured six
        // locals, so every texel of every pixel of every frame allocated and invoked a closure:
        // it was the single most expensive thing in the renderer.
        const float inv = 1f / 255f;
        float t0 = Px[o00] + (Px[o10] - Px[o00]) * tx;
        float b0 = Px[o01] + (Px[o11] - Px[o01]) * tx;
        r = (t0 + (b0 - t0) * ty) * inv;

        t0 = Px[o00 + 1] + (Px[o10 + 1] - Px[o00 + 1]) * tx;
        b0 = Px[o01 + 1] + (Px[o11 + 1] - Px[o01 + 1]) * tx;
        g = (t0 + (b0 - t0) * ty) * inv;

        t0 = Px[o00 + 2] + (Px[o10 + 2] - Px[o00 + 2]) * tx;
        b0 = Px[o01 + 2] + (Px[o11 + 2] - Px[o01 + 2]) * tx;
        b = (t0 + (b0 - t0) * ty) * inv;

        t0 = Px[o00 + 3] + (Px[o10 + 3] - Px[o00 + 3]) * tx;
        b0 = Px[o01 + 3] + (Px[o11 + 3] - Px[o01 + 3]) * tx;
        a = (t0 + (b0 - t0) * ty) * inv;
        return true;
    }
}

public static class AuSurfaces
{
    /// Among Us' outline colour. Never pure black: black reads as a hole, this reads as ink.
    public static readonly NfColor Ink = new(0.110f, 0.125f, 0.170f);

    public const int TexSize = 256;

    private static NfColor Shade(NfColor c, float f) => new(c.R * f, c.G * f, c.B * f);

    /// One texture per ROLE, never per colour.
    ///
    /// The first version keyed the cache on the room colour too, which sounded harmless and was
    /// fatal: every floor patch on Polus has its own sampled colour, so the map generated over a
    /// thousand 256x256 textures and building the model took THIRTEEN SECONDS inside the game. The
    /// texture now carries only STRUCTURE, drawn in neutral grey, and the colour arrives at render
    /// time as a per-triangle tint. Eight textures for the whole map.
    private static readonly Dictionary<int, Surface3D> cache = new();

    public static void ClearCache() => cache.Clear();

    public static Surface3D Get(SurfaceRole role)
    {
        if (cache.TryGetValue((int)role, out var s)) return s;
        s = Build(role, Neutral);
        cache[(int)role] = s;
        return s;
    }

    /// The base grey every texture is drawn in. Multiplying it by a room colour reproduces that
    /// colour exactly, so a tinted surface is indistinguishable from one drawn in that colour.
    private static readonly NfColor Neutral = new(1f, 1f, 1f);

    private static Surface3D Build(SurfaceRole role, NfColor c) => role switch
    {
        SurfaceRole.Wall => Wall(c),
        SurfaceRole.WallWindow => WallWithWindow(c),
        SurfaceRole.Floor => Floor(c),
        SurfaceRole.Ceiling => Ceiling(c),
        SurfaceRole.Door => Door(c),
        SurfaceRole.ConsoleFront => ConsoleFront(c),
        SurfaceRole.PropSide => Plain(c),
        SurfaceRole.PropTop => PropTop(c),
        SurfaceRole.Rock => Rock(c),
        _ => Plain(c),
    };

    // ================================================================================
    // Architecture
    // ================================================================================
    /// The standard interior wall: flat panel, a lighter rail near the top, a darker skirting along
    /// the floor, evenly spaced seams, ink lines where planes meet.
    public static Surface3D Wall(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(c);

        cv.VerticalBand(0, n * 0.30f, NfColor.White, 0.16f, 0f);
        cv.VerticalBand(n * 0.70f, n * 0.30f, NfColor.Black, 0f, 0.14f);

        float seamW = MathF.Max(1.5f, n / 190f);
        for (int i = 1; i < 4; i++)
        {
            float x = n * i / 4f;
            cv.Line(x, n * 0.10f, x, n * 0.86f, seamW, Shade(c, 0.74f));
        }

        float railY = n * 0.155f, railH = n * 0.055f;
        cv.FillRect(0, railY, n, railH, Shade(c, 1.18f));
        float ink = MathF.Max(1.5f, n / 170f);
        cv.Line(0, railY, n, railY, ink, Ink);
        cv.Line(0, railY + railH, n, railY + railH, ink, Ink);

        float skirtY = n * 0.86f;
        cv.FillRect(0, skirtY, n, n - skirtY, Shade(c, 0.60f));
        cv.Line(0, skirtY, n, skirtY, MathF.Max(2f, n / 130f), Ink);
        cv.Line(0, 1f, n, 1f, MathF.Max(2.5f, n / 110f), Ink);

        return new Surface3D(cv.ToRgba(), n, n);
    }

    /// A wall with a window. Windows are the single most recognisable feature of an Among Us room
    /// and the thing whose absence made the earlier attempts read as a corridor in a different game.
    public static Surface3D WallWithWindow(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(c);

        cv.VerticalBand(0, n * 0.30f, NfColor.White, 0.16f, 0f);
        cv.VerticalBand(n * 0.70f, n * 0.30f, NfColor.Black, 0f, 0.14f);

        float railY = n * 0.135f, railH = n * 0.05f;
        cv.FillRect(0, railY, n, railH, Shade(c, 1.18f));
        cv.Line(0, railY + railH, n, railY + railH, MathF.Max(1.5f, n / 170f), Ink);

        // The pane: night outside, with the diagonal sheen the game paints on every piece of glass.
        float mx = n * 0.14f, my = n * 0.28f, mw = n * 0.72f, mh = n * 0.36f;
        float r = MathF.Min(mw, mh) * 0.16f;
        var night = new NfColor(0.105f, 0.075f, 0.185f);
        cv.FillRoundRect(mx, my, mw, mh, r, night, 1f);
        cv.SetTintMask(mx, my, mw, mh, 0f);
        cv.FillQuad(mx + mw * 0.06f, my + mh,
                    mx + mw * 0.40f, my,
                    mx + mw * 0.58f, my,
                    mx + mw * 0.24f, my + mh,
                    NfColor.White, 0.16f);
        cv.StrokeRoundRect(mx, my, mw, mh, r, MathF.Max(2f, n / 110f), Shade(c, 1.30f));
        cv.StrokeRoundRect(mx, my, mw, mh, r, MathF.Max(1.5f, n / 150f), Ink, 0.85f);

        float skirtY = n * 0.86f;
        cv.FillRect(0, skirtY, n, n - skirtY, Shade(c, 0.60f));
        cv.Line(0, skirtY, n, skirtY, MathF.Max(2f, n / 130f), Ink);
        cv.Line(0, 1f, n, 1f, MathF.Max(2.5f, n / 110f), Ink);

        return new Surface3D(cv.ToRgba(), n, n);
    }

    /// Tiled floor. Among Us floors are extremely plain, and everything that tries to make them
    /// interesting reads as dirt.
    public static Surface3D Floor(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(c);
        float grout = MathF.Max(1.2f, n / 200f);
        var groutCol = Shade(c, 0.82f);
        for (int i = 0; i <= 2; i++)
        {
            float p = n * i / 2f;
            cv.Line(p, 0, p, n, grout, groutCol);
            cv.Line(0, p, n, p, grout, groutCol);
        }
        return new Surface3D(cv.ToRgba(), n, n);
    }

    public static Surface3D Ceiling(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(Shade(c, 0.70f));
        var seam = Shade(c, 0.55f);
        float t = MathF.Max(1.2f, n / 200f);
        cv.Line(n * 0.5f, 0, n * 0.5f, n, t, seam);
        cv.Line(0, n * 0.5f, n, n * 0.5f, t, seam);
        // A recessed light strip, the only bright thing on a ceiling.
        cv.FillRoundRect(n * 0.28f, n * 0.12f, n * 0.44f, n * 0.07f, n * 0.02f,
                         new NfColor(0.92f, 0.94f, 0.82f), 0.75f);
        return new Surface3D(cv.ToRgba(), n, n);
    }

    /// A closed door: frame, two leaves, centre seam, hazard band. Solid and opaque, which is
    /// precisely what the raycaster could never manage.
    public static Surface3D Door(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(Shade(c, 0.84f));

        cv.FillRect(n * 0.05f, n * 0.04f, n * 0.90f, n * 0.92f, Shade(c, 1.06f));
        cv.Line(n * 0.5f, n * 0.04f, n * 0.5f, n * 0.96f, MathF.Max(2f, n / 140f), Ink);

        float by = n * 0.44f, bh = n * 0.12f;
        cv.FillRect(n * 0.05f, by, n * 0.90f, bh, new NfColor(0.925f, 0.745f, 0.243f));
        cv.SetTintMask(n * 0.05f, by, n * 0.90f, bh, 0f);
        var dark = new NfColor(0.212f, 0.227f, 0.275f);
        for (float x = n * 0.05f; x < n * 0.95f; x += n * 0.10f)
            cv.FillQuad(x, by + bh, x + n * 0.035f, by, x + n * 0.07f, by, x + n * 0.035f, by + bh, dark);
        float ink = MathF.Max(1.2f, n / 200f);
        cv.Line(n * 0.05f, by, n * 0.95f, by, ink, Ink);
        cv.Line(n * 0.05f, by + bh, n * 0.95f, by + bh, ink, Ink);

        cv.StrokeRoundRect(n * 0.05f, n * 0.04f, n * 0.90f, n * 0.92f, n * 0.02f,
                           MathF.Max(3f, n / 90f), Ink);
        return new Surface3D(cv.ToRgba(), n, n);
    }

    // ================================================================================
    // Props
    // ================================================================================
    /// The front of a console: housing, a lit screen with readouts, two buttons. Emissive in the
    /// scene, so it glows in the dark and gives a room a landmark you can navigate by.
    public static Surface3D ConsoleFront(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(c);
        cv.FillRect(0, 0, n, n * 0.09f, Shade(c, 1.20f));

        float sx = n * 0.10f, sy = n * 0.16f, sw = n * 0.56f, sh = n * 0.48f;
        cv.FillRoundRect(sx, sy, sw, sh, n * 0.03f, new NfColor(0.102f, 0.376f, 0.431f));
        cv.SetTintMask(sx, sy, sw, sh, 0f);
        var glow = new NfColor(0.478f, 0.886f, 0.925f);
        for (int i = 0; i < 4; i++)
        {
            float ly = sy + sh * (0.16f + i * 0.20f);
            float lw = sw * (i % 2 == 0 ? 0.66f : 0.44f);
            cv.FillRect(sx + sw * 0.10f, ly, lw, sh * 0.075f, glow);
        }
        cv.StrokeRoundRect(sx, sy, sw, sh, n * 0.03f, MathF.Max(2f, n / 140f), Ink);

        float br = n * 0.045f;
        cv.FillEllipse(n * 0.80f, n * 0.26f, br, br, new NfColor(0.839f, 0.329f, 0.306f));
        cv.StrokeEllipse(n * 0.80f, n * 0.26f, br, br, MathF.Max(1.2f, n / 200f), Ink);
        cv.FillEllipse(n * 0.80f, n * 0.46f, br, br, new NfColor(0.376f, 0.745f, 0.424f));
        cv.StrokeEllipse(n * 0.80f, n * 0.46f, br, br, MathF.Max(1.2f, n / 200f), Ink);

        cv.FillRect(0, n * 0.72f, n, n * 0.28f, Shade(c, 0.80f));
        cv.Line(0, n * 0.72f, n, n * 0.72f, MathF.Max(1.5f, n / 170f), Ink);
        cv.StrokeRoundRect(1f, 1f, n - 2f, n - 2f, n * 0.015f, MathF.Max(3f, n / 90f), Ink);
        return new Surface3D(cv.ToRgba(), n, n);
    }

    /// The SIDE of a prop. Not a blank panel: Among Us objects have a lighter top edge, a darker
    /// base, a bevelled outline and usually one horizontal seam. Those four things are what make a
    /// crate read as a crate from any angle rather than as a coloured cube.
    public static Surface3D Plain(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(c);

        // Light along the top, shadow along the bottom: the object has volume from every side.
        cv.VerticalBand(0, n * 0.22f, NfColor.White, 0.17f, 0f);
        cv.VerticalBand(n * 0.74f, n * 0.26f, NfColor.Black, 0f, 0.20f);

        // A horizontal seam and a plinth, the two details almost every Among Us object has.
        float seam = MathF.Max(1.5f, n / 150f);
        cv.Line(0, n * 0.30f, n, n * 0.30f, seam, Shade(c, 0.72f));
        cv.Line(0, n * 0.80f, n, n * 0.80f, MathF.Max(2f, n / 120f), Ink, 0.85f);
        cv.FillRect(0, n * 0.80f, n, n * 0.20f, Shade(c, 0.66f));

        // Two rivets, the game's favourite way of saying "this is made of metal".
        float rr = MathF.Max(2f, n / 42f);
        cv.FillEllipse(n * 0.16f, n * 0.52f, rr, rr, Shade(c, 1.22f));
        cv.StrokeEllipse(n * 0.16f, n * 0.52f, rr, rr, MathF.Max(1f, n / 190f), Ink, 0.8f);
        cv.FillEllipse(n * 0.84f, n * 0.52f, rr, rr, Shade(c, 1.22f));
        cv.StrokeEllipse(n * 0.84f, n * 0.52f, rr, rr, MathF.Max(1f, n / 190f), Ink, 0.8f);

        cv.StrokeRoundRect(1f, 1f, n - 2f, n - 2f, n * 0.03f, MathF.Max(2.5f, n / 60f), Ink);
        return new Surface3D(cv.ToRgba(), n, n);
    }

    /// The TOP of a prop, seen from above at a shallow angle. A lighter face with an inset panel
    /// and a rim, so a box does not end in a flat lid of pure colour.
    public static Surface3D PropTop(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(Shade(c, 1.10f));
        float inset = n * 0.12f;
        cv.FillRoundRect(inset, inset, n - inset * 2f, n - inset * 2f, n * 0.05f, Shade(c, 1.02f));
        cv.StrokeRoundRect(inset, inset, n - inset * 2f, n - inset * 2f, n * 0.05f,
                           MathF.Max(1.5f, n / 150f), Shade(c, 0.74f));
        cv.StrokeRoundRect(1f, 1f, n - 2f, n - 2f, n * 0.03f, MathF.Max(2.5f, n / 60f), Ink);
        return new Surface3D(cv.ToRgba(), n, n);
    }

    /// Polus rock: a few large flat facets with ink creases and snow caps. Still flat colour, still
    /// outlined, because that is how the game draws its rocks.
    public static Surface3D Rock(NfColor c, int n = TexSize)
    {
        var cv = new Canvas2D(n, n);
        cv.Clear(c);
        cv.FillQuad(0, n * 0.35f, n * 0.42f, n * 0.10f, n * 0.55f, n, 0, n, Shade(c, 1.14f));
        cv.FillQuad(n * 0.55f, n * 0.20f, n, n * 0.42f, n, n, n * 0.62f, n, Shade(c, 0.84f));
        float ink = MathF.Max(1.5f, n / 150f);
        cv.Line(n * 0.42f, n * 0.10f, n * 0.55f, n, ink, Ink, 0.7f);
        cv.Line(n * 0.55f, n * 0.20f, n * 0.62f, n, ink, Ink, 0.55f);
        // Snow caught on the upward faces.
        var snow = new NfColor(0.878f, 0.906f, 0.949f);
        cv.FillQuad(0, n * 0.35f, n * 0.42f, n * 0.10f, n * 0.48f, n * 0.22f, 0, n * 0.47f, snow, 0.9f);
        return new Surface3D(cv.ToRgba(), n, n);
    }
}

public enum SurfaceRole
{
    Wall, WallWindow, Floor, Ceiling, Door, ConsoleFront, PropSide, PropTop, Rock,
}
