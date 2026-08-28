// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * Canvas2D - a tiny vector drawing surface, so textures can be DRAWN instead of computed.
 *
 * WHY THIS EXISTS
 * ---------------
 * Among Us' art is vector art: flat fills, thick dark outlines, rounded rectangles, circles, clean
 * antialiased edges. Reproducing that needs a pen and a brush, not noise functions. The prototype
 * used System.Drawing, which does not exist inside Il2Cpp, so the same primitives are reimplemented
 * here in about the space they deserve.
 *
 * HOW
 * ---
 * Every shape is a SIGNED DISTANCE FUNCTION: a formula giving the distance from a point to the
 * shape's edge, negative inside. Filling is then "colour in wherever the distance is below zero",
 * and antialiasing is a single smoothstep across one pixel of that distance. Outlining is the same
 * function again with an offset. This gives edges as clean as any graphics library, in a fraction
 * of the code, with no dependencies at all - and it is resolution independent, so the same texture
 * can be generated at 256 or 1024 by changing one number.
 */

using System;

namespace Nightfall.Core;

public sealed class Canvas2D
{
    /*
     * A CANVAS HAS TWO SIZES, AND THE DRAWING CODE ONLY EVER SEES ONE.
     *
     * Every surface in the world is described in a fixed 128-unit square: a seam is two units wide,
     * a rivet has a radius of three, a rib repeats every eleven. Those numbers are the DESIGN, and
     * they were tuned against the map artwork. Rendering the same design at 256 pixels by simply
     * enlarging the canvas would halve every one of them in relative terms - the seams would thin
     * out, the grain would shrink to dust, and the whole map would come back a shade different.
     *
     * So `W` and `H` stay the design's own coordinate system, `Detail` says how many device pixels
     * one of those units becomes, and every shape is rasterised at the finer grid. The catalogue is
     * untouched; the texture is simply drawn more precisely. The antialiasing follows: one device
     * pixel is 1/Detail design units wide, so the coverage ramp narrows by the same factor.
     */
    /// The design's coordinate system - what every Draw lambda measures in.
    public readonly int W, H;
    /// Device pixels per design unit, and the real size of the buffer.
    public readonly int Detail;
    public readonly int PW, PH;
    /// RGBA, row-major from the top, in DEVICE pixels.
    public readonly float[] Px;

    public Canvas2D(int w, int h, int detail = 1)
    {
        W = w; H = h;
        Detail = Math.Max(1, detail);
        PW = w * Detail; PH = h * Detail;
        Px = new float[PW * PH * 4];
    }

    /// Puts the canvas back into the state the constructor leaves it in - every channel zero - so
    /// that ONE canvas can draw one texture after another.
    ///
    /// That matters more than it sounds. This buffer is four floats per device pixel: a megabyte at
    /// 256 square. Drawing a map's catalogue with a fresh canvas each time therefore threw a
    /// megabyte at the allocator per material, which nobody noticed while a map had thirty of them
    /// and was 217 megabytes of churn the first time a map had two hundred (Mira HQ; measured 304 MB
    /// allocated in total to build its catalogue). Among Us is a 32-bit process, so that is not just
    /// garbage-collector work, it is address space - and it ran out.
    public void Reset() => Array.Clear(Px, 0, Px.Length);

    public void Clear(NfColor c, float alpha = 1f)
    {
        for (int i = 0; i < Px.Length; i += 4)
        {
            Px[i] = c.R; Px[i + 1] = c.G; Px[i + 2] = c.B; Px[i + 3] = alpha;
        }
    }

    /// Converts to the byte layout the renderer samples.
    public byte[] ToRgba()
    {
        var outp = new byte[PW * PH * 4];
        for (int i = 0; i < Px.Length; i++) outp[i] = NfMath.ToByteRaw(Px[i]);
        return outp;
    }

    // ================================================================================
    // Compositing
    // ================================================================================
    /// One pixel of coverage, alpha blended.
    private void Blend(int x, int y, NfColor c, float a)
    {
        if (a <= 0.001f || x < 0 || y < 0 || x >= PW || y >= PH) return;
        if (a > 1f) a = 1f;
        int o = (y * PW + x) * 4;
        Px[o] = Px[o] + (c.R - Px[o]) * a;
        Px[o + 1] = Px[o + 1] + (c.G - Px[o + 1]) * a;
        Px[o + 2] = Px[o + 2] + (c.B - Px[o + 2]) * a;
        Px[o + 3] = Px[o + 3] + (1f - Px[o + 3]) * a;
    }

    /// Fills wherever `sdf` is negative, with one pixel of antialiasing across the boundary.
    private void FillSdf(Func<float, float, float> sdf, NfColor c, float alpha,
                         int x0, int y0, int x1, int y1)
    {
        // The bounds arrive in design units; the loop runs over device pixels.
        x0 = Math.Max(0, x0 * Detail); y0 = Math.Max(0, y0 * Detail);
        x1 = Math.Min(PW - 1, x1 * Detail + Detail - 1);
        y1 = Math.Min(PH - 1, y1 * Detail + Detail - 1);
        float inv = 1f / Detail;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float d = sdf((x + 0.5f) * inv, (y + 0.5f) * inv);
                // Coverage: one device pixel spans 1/Detail design units, so the ramp is that wide.
                float cov = NfMath.Clamp01(0.5f - d * Detail);
                if (cov > 0f) Blend(x, y, c, cov * alpha);
            }
        }
    }

    // ================================================================================
    // Shapes
    // ================================================================================
    public void FillRect(float x, float y, float w, float h, NfColor c, float alpha = 1f)
    {
        FillSdf((px, py) => RectSdf(px, py, x, y, w, h), c, alpha,
                (int)x - 2, (int)y - 2, (int)(x + w) + 2, (int)(y + h) + 2);
    }

    public void FillRoundRect(float x, float y, float w, float h, float r, NfColor c, float alpha = 1f)
    {
        FillSdf((px, py) => RoundRectSdf(px, py, x, y, w, h, r), c, alpha,
                (int)x - 2, (int)y - 2, (int)(x + w) + 2, (int)(y + h) + 2);
    }

    /// Outline of a rounded rectangle, `thickness` wide, centred on the edge.
    public void StrokeRoundRect(float x, float y, float w, float h, float r, float thickness,
                                NfColor c, float alpha = 1f)
    {
        float half = thickness * 0.5f;
        FillSdf((px, py) => MathF.Abs(RoundRectSdf(px, py, x, y, w, h, r)) - half, c, alpha,
                (int)(x - thickness) - 2, (int)(y - thickness) - 2,
                (int)(x + w + thickness) + 2, (int)(y + h + thickness) + 2);
    }

    public void FillEllipse(float cx, float cy, float rx, float ry, NfColor c, float alpha = 1f)
    {
        FillSdf((px, py) => EllipseSdf(px, py, cx, cy, rx, ry), c, alpha,
                (int)(cx - rx) - 2, (int)(cy - ry) - 2, (int)(cx + rx) + 2, (int)(cy + ry) + 2);
    }

    public void StrokeEllipse(float cx, float cy, float rx, float ry, float thickness,
                              NfColor c, float alpha = 1f)
    {
        float half = thickness * 0.5f;
        FillSdf((px, py) => MathF.Abs(EllipseSdf(px, py, cx, cy, rx, ry)) - half, c, alpha,
                (int)(cx - rx - thickness) - 2, (int)(cy - ry - thickness) - 2,
                (int)(cx + rx + thickness) + 2, (int)(cy + ry + thickness) + 2);
    }

    public void Line(float ax, float ay, float bx, float by, float thickness, NfColor c,
                     float alpha = 1f)
    {
        float half = thickness * 0.5f;
        int x0 = (int)MathF.Min(ax, bx) - (int)thickness - 2;
        int y0 = (int)MathF.Min(ay, by) - (int)thickness - 2;
        int x1 = (int)MathF.Max(ax, bx) + (int)thickness + 2;
        int y1 = (int)MathF.Max(ay, by) + (int)thickness + 2;
        FillSdf((px, py) => SegmentSdf(px, py, ax, ay, bx, by) - half, c, alpha, x0, y0, x1, y1);
    }

    /// A convex quad, given as four points in order. Used for the angled stripes on hazard bands.
    public void FillQuad(float x0, float y0, float x1, float y1, float x2, float y2,
                         float x3, float y3, NfColor c, float alpha = 1f)
    {
        float minX = MathF.Min(MathF.Min(x0, x1), MathF.Min(x2, x3));
        float maxX = MathF.Max(MathF.Max(x0, x1), MathF.Max(x2, x3));
        float minY = MathF.Min(MathF.Min(y0, y1), MathF.Min(y2, y3));
        float maxY = MathF.Max(MathF.Max(y0, y1), MathF.Max(y2, y3));

        FillSdf((px, py) =>
        {
            // Inside a convex polygon means "on the same side of every edge". The distance is the
            // largest of the per-edge signed distances, which is exactly the convex SDF.
            float d = float.MinValue;
            d = MathF.Max(d, EdgeSdf(px, py, x0, y0, x1, y1));
            d = MathF.Max(d, EdgeSdf(px, py, x1, y1, x2, y2));
            d = MathF.Max(d, EdgeSdf(px, py, x2, y2, x3, y3));
            d = MathF.Max(d, EdgeSdf(px, py, x3, y3, x0, y0));
            return d;
        }, c, alpha, (int)minX - 2, (int)minY - 2, (int)maxX + 2, (int)maxY + 2);
    }

    /// A soft vertical gradient band, for the light and shade Among Us paints along walls.
    public void VerticalBand(float y, float h, NfColor c, float alphaTop, float alphaBottom)
    {
        int y0 = Math.Max(0, (int)(y * Detail)), y1 = Math.Min(PH - 1, (int)((y + h) * Detail));
        for (int py = y0; py <= y1; py++)
        {
            float t = h <= 0.001f ? 0f : ((py + 0.5f) / Detail - y) / h;
            float a = alphaTop + (alphaBottom - alphaTop) * NfMath.Clamp01(t);
            for (int px = 0; px < PW; px++) Blend(px, py, c, a);
        }
    }

    /// Stamps the tint mask over a rectangle. The alpha channel of a surface is not opacity but
    /// "does this texel take the room's colour": glass, hazard yellow and lit screens set it to
    /// zero so they keep the colour they were drawn in.
    public void SetTintMask(float x, float y, float w, float h, float mask)
    {
        int x0 = Math.Max(0, (int)(x * Detail)), y0 = Math.Max(0, (int)(y * Detail));
        int x1 = Math.Min(PW - 1, (int)((x + w) * Detail));
        int y1 = Math.Min(PH - 1, (int)((y + h) * Detail));
        for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
                Px[(py * PW + px) * 4 + 3] = mask;
    }

    // ================================================================================
    // Distance functions
    // ================================================================================
    private static float RectSdf(float px, float py, float x, float y, float w, float h)
    {
        float dx = MathF.Max(x - px, px - (x + w));
        float dy = MathF.Max(y - py, py - (y + h));
        // Outside distance is the length of the positive part; inside it is the largest negative.
        float ox = MathF.Max(dx, 0f), oy = MathF.Max(dy, 0f);
        return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(dx, dy), 0f);
    }

    private static float RoundRectSdf(float px, float py, float x, float y, float w, float h, float r)
    {
        r = MathF.Max(0f, MathF.Min(r, MathF.Min(w, h) * 0.5f));
        return RectSdf(px, py, x + r, y + r, w - r * 2f, h - r * 2f) - r;
    }

    private static float EllipseSdf(float px, float py, float cx, float cy, float rx, float ry)
    {
        rx = MathF.Max(0.001f, rx);
        ry = MathF.Max(0.001f, ry);
        float nx = (px - cx) / rx, ny = (py - cy) / ry;
        float k = MathF.Sqrt(nx * nx + ny * ny);
        // Scaled back into pixels, which is exact for circles and a good approximation otherwise.
        return (k - 1f) * MathF.Min(rx, ry);
    }

    private static float SegmentSdf(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay;
        float wx = px - ax, wy = py - ay;
        float len2 = vx * vx + vy * vy;
        float t = len2 < 1e-6f ? 0f : NfMath.Clamp01((wx * vx + wy * vy) / len2);
        float dx = wx - vx * t, dy = wy - vy * t;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// Signed distance to the line through a and b, positive on the left.
    private static float EdgeSdf(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay;
        float len = MathF.Sqrt(vx * vx + vy * vy);
        if (len < 1e-6f) return 0f;
        return ((px - ax) * vy - (py - ay) * vx) / len;
    }
}
