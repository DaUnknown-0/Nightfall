// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightSky - the sky over Polus, baked once as a panorama and then only looked up.
 *
 * WHY A BAKED PANORAMA AND NOT A FORMULA
 * --------------------------------------
 * The sky used to be computed per pixel: a gradient, and a star wherever an integer hash of the
 * (azimuth, elevation) grid cell came out above a threshold. That has two faults, and the first
 * playtest reported both without knowing they were the same thing.
 *
 *   - A STAR WAS A GRID CELL, so it was a hard-edged rectangle whose size depended on how many
 *     screen pixels that cell happened to cover. Different sizes, square, and aliasing on every
 *     edge: "unterschiedlich große weiße Quadrate, ziemlich hart".
 *   - IT TWINKLED with a sine of time, and a field of hard squares flickering independently reads
 *     as noise rather than as a sky: "unruhig".
 *
 * A formula also has to be cheap, because it runs on up to half the screen. That budget is what
 * kept the old sky to one hash. Baking removes the budget entirely: the panorama is built once per
 * session and a pixel costs one bilinear fetch, so the sky can afford soft round stars with real
 * magnitudes and colours, a Milky Way, an aurora and haze at the horizon - all for less time than
 * the hash cost.
 *
 * THE PANORAMA'S COORDINATES are (azimuth 0..2pi, screen elevation 0..1), not (azimuth, altitude).
 * The vertical axis is deliberately the same fraction the renderer already has per row, so the
 * lookup needs no trigonometry: v is the row, u is the column, both worked out once per frame.
 *
 * IT DOES NOT MOVE. That is the point. Stars hold still in the world, so turning the head sweeps
 * past them; nothing pulses, nothing flickers. A sky that is doing something is a sky the player
 * looks at instead of listening for footsteps.
 */

using System;

namespace Nightfall.Core;

public static class NightSky
{
    /// 2048 across is about 5,7 texels per degree, so a star is a soft dot a pixel or two wide at
    /// any resolution this renderer runs at rather than a block. 320 down covers the sky from the
    /// horizon to straight up in the same fraction the renderer measures rows in.
    public const int W = 2048;
    public const int H = 320;

    private static byte[] px;

    public static byte[] Pixels { get { EnsureBuilt(); return px; } }

    public static void Clear() { px = null; }

    public static void EnsureBuilt()
    {
        if (px != null) return;

        var f = new float[W * H * 3];

        // ---- the gradient ------------------------------------------------------------
        // Deep indigo overhead, warmer violet at the horizon. Polus' own palette, one shade below
        // the map's so a star has somewhere to be brighter than.
        for (int y = 0; y < H; y++)
        {
            float elev = y / (float)(H - 1);           // 0 at the horizon, 1 overhead
            float r = Mix(0.128f, 0.026f, elev, 0.85f);
            float g = Mix(0.076f, 0.016f, elev, 0.85f);
            float b = Mix(0.186f, 0.062f, elev, 0.85f);
            for (int x = 0; x < W; x++)
            {
                int o = (y * W + x) * 3;
                f[o] = r; f[o + 1] = g; f[o + 2] = b;
            }
        }

        // ---- the Milky Way -----------------------------------------------------------
        // A great circle seen from inside is a sine wave on a panorama. It is drawn as haze first
        // and gets its extra stars below, because a band of light with no stars in it reads as a
        // cloud.
        for (int x = 0; x < W; x++)
        {
            float u = x / (float)W;
            float centre = 0.52f + 0.30f * MathF.Sin(u * NfMath.TwoPi + 0.9f);
            for (int y = 0; y < H; y++)
            {
                float elev = y / (float)(H - 1);
                float d = MathF.Abs(elev - centre) / 0.16f;
                if (d > 1f) continue;
                float k = (1f - d) * (1f - d);
                // Mottled, not a smooth ribbon: the real thing is torn by dust lanes.
                float mottle = 0.55f + 0.45f * NfMath.Fbm(u * 26f, elev * 9f, 3, 771);
                float a = k * mottle * 0.055f * Extinction(elev);
                int o = (y * W + x) * 3;
                f[o] += a * 0.85f; f[o + 1] += a * 0.82f; f[o + 2] += a;
            }
        }

        // ---- the aurora --------------------------------------------------------------
        // Low over the horizon, green shading to teal, in vertical curtains. Baked, so it is still:
        // a curtain that visibly moves is the brightest thing in a blackout and pulls the eye away
        // from everything the player is supposed to be watching for.
        for (int x = 0; x < W; x++)
        {
            float u = x / (float)W;
            float curtain = NfMath.Fbm(u * 9f, 0.5f, 3, 91);
            float reach = 0.10f + 0.26f * curtain;
            for (int y = 0; y < H; y++)
            {
                float elev = y / (float)(H - 1);
                if (elev > reach) continue;
                // Bright at the bottom of the curtain, fading upwards, and off at the very horizon
                // where the haze takes over.
                float k = NfMath.SmoothStep(reach, reach * 0.25f, elev)
                        * NfMath.SmoothStep(0f, 0.05f, elev);
                float rib = 0.4f + 0.6f * NfMath.Fbm(u * 55f, elev * 6f, 2, 313);
                float a = k * rib * curtain * 0.085f;
                int o = (y * W + x) * 3;
                f[o] += a * 0.10f; f[o + 1] += a * 0.95f; f[o + 2] += a * 0.55f;
            }
        }

        // ---- the stars ---------------------------------------------------------------
        // Placed at sub-texel positions and drawn as a small round falloff, which is the whole
        // difference between a star and a lit pixel. Magnitudes follow a steep power law, so the
        // field is mostly faint with a handful of bright ones - a uniform field looks synthetic.
        var rnd = new Rng(20260806);
        const int Count = 3400;
        for (int i = 0; i < Count; i++)
        {
            float sx = rnd.Next() * W;
            float sy = rnd.Next() * H;
            float elev = sy / (H - 1);

            // Extra density inside the Milky Way band.
            float u = sx / W;
            float centre = 0.52f + 0.30f * MathF.Sin(u * NfMath.TwoPi + 0.9f);
            float inBand = MathF.Abs(elev - centre) < 0.15f ? 1f : 0f;
            if (inBand == 0f && rnd.Next() > 0.62f) continue;

            float m = rnd.Next();
            float mag = m * m * m * m;                     // steep: nearly all of them are faint
            float bright = (0.16f + 1.25f * mag) * Extinction(elev);
            if (bright < 0.02f) continue;

            // Radius grows with brightness, but slowly: a bright star is bright, not big.
            float rad = 0.42f + 0.85f * mag + (bright > 0.9f ? 0.30f : 0f);

            // Star colour: mostly white, some blue-white, a few amber. Among Us' own sky is violet,
            // so the warm ones are what keeps the field from looking like grey dust.
            float t = rnd.Next();
            float cr = 1f, cg = 1f, cb = 1f;
            if (t < 0.22f) { cr = 0.72f; cg = 0.83f; cb = 1.00f; }
            else if (t > 0.86f) { cr = 1.00f; cg = 0.83f; cb = 0.62f; }

            Splat(f, sx, sy, rad, bright, cr, cg, cb);

            // The brightest few get a faint cross, the way a bright point reads through any optic.
            if (bright > 1.05f)
            {
                for (float d = 1f; d <= 3.4f; d += 0.7f)
                {
                    float a = bright * 0.13f * (1f - d / 4f);
                    Splat(f, sx - d, sy, 0.55f, a, cr, cg, cb);
                    Splat(f, sx + d, sy, 0.55f, a, cr, cg, cb);
                    Splat(f, sx, sy - d, 0.55f, a, cr, cg, cb);
                    Splat(f, sx, sy + d, 0.55f, a, cr, cg, cb);
                }
            }
        }

        px = new byte[W * H * 4];
        for (int i = 0, o = 0; i < W * H; i++, o += 4)
        {
            px[o] = NfMath.ToByteRaw(f[i * 3]);
            px[o + 1] = NfMath.ToByteRaw(f[i * 3 + 1]);
            px[o + 2] = NfMath.ToByteRaw(f[i * 3 + 2]);
            px[o + 3] = 255;
        }
    }

    /// Haze near the horizon. Real skies lose their stars in the last few degrees, and it is the
    /// single cheapest thing that makes a star field read as a sky rather than as a texture: it
    /// gives the horizon a place to BE.
    private static float Extinction(float elev) => NfMath.SmoothStep(0.0f, 0.13f, elev);

    private static float Mix(float a, float b, float t, float curve) =>
        a + (b - a) * MathF.Pow(NfMath.Clamp01(t), curve);

    /// Adds a round, soft dot. Wraps in azimuth, because a star cut in half at u = 0 is a seam that
    /// sweeps past every time the player turns all the way round.
    private static void Splat(float[] f, float cx, float cy, float rad, float amp,
                              float cr, float cg, float cb)
    {
        int x0 = (int)MathF.Floor(cx - rad - 1f), x1 = (int)MathF.Ceiling(cx + rad + 1f);
        int y0 = Math.Max(0, (int)MathF.Floor(cy - rad - 1f));
        int y1 = Math.Min(H - 1, (int)MathF.Ceiling(cy + rad + 1f));
        float inv = 1f / (rad * rad);

        for (int y = y0; y <= y1; y++)
        {
            float dy = y + 0.5f - cy;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx;
                float d2 = (dx * dx + dy * dy) * inv;
                if (d2 > 3.2f) continue;
                float k = amp * MathF.Exp(-d2 * 2.55f);
                if (k < 0.004f) continue;
                int xi = ((x % W) + W) % W;
                int o = (y * W + xi) * 3;
                f[o] += k * cr; f[o + 1] += k * cg; f[o + 2] += k * cb;
            }
        }
    }

    /// The same deterministic generator the surfaces use: the offline tool and the game must place
    /// the stars in identical places, or a sky checked in one is not the sky drawn in the other.
    private struct Rng
    {
        private uint s;
        public Rng(int seed) { s = (uint)(seed * 2654435761u + 12345u); }
        public float Next()
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
