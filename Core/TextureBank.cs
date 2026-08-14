// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * TextureBank - every surface in the first-person view, drawn from arithmetic.
 *
 * WHY PROCEDURAL AND NOT EMBEDDED IMAGES
 * --------------------------------------
 * Among Us has no wall textures, because it has no walls: it is a top-down game whose art is all
 * floor plan. Nothing in the game files, and nothing in any asset dump of it, shows what the side
 * of a Polus corridor looks like. Those pixels have to be invented either way.
 *
 * Inventing them in code rather than in a paint program buys three things that matter here:
 *   1. the game and the offline render tool produce byte-identical textures, because both compile
 *      this same file and every value comes from an integer hash rather than a seeded RNG,
 *   2. the mod ships no image data at all, which keeps it in line with the rest of this mod family
 *      (procedural sprites, no asset bundles),
 *   3. a texture can be re-tuned by changing two numbers and re-rendering a PNG offline, which is
 *      the only reason it is realistic to iterate on the look without launching the game.
 *
 * THE PALETTE IS NOT INVENTED
 * ---------------------------
 * The colours are sampled from the real Polus map render, via the tracker's least-squares world-to
 * -image calibration, so the metal in Electrical is the metal of Electrical and the lab tile is the
 * lab's own white-blue. What is invented is only the third dimension: how that material looks when
 * it stands up in front of you.
 *
 * RESOLUTION AND FILTERING, AND WHY BOTH HAD TO CHANGE
 * ----------------------------------------------------
 * The first version stored every surface as one 128x128 image and point sampled it, on the theory
 * that a coarse texture cannot alias. Inside the actual view both halves of that were wrong:
 *
 *   - Close up a wall is not sampled at 128 texels, it is stretched. The projection puts a 1,75
 *     unit wall across 1,75 * 208 / distance screen rows, so at 0,6 units it covers some 600 rows:
 *     one texel is five screen pixels tall and the wall reads as a field of soft coloured blocks.
 *     That is what "the texture resolution is bad" looks like from the inside.
 *   - Far away the opposite happens. The same wall at twelve units is thirty rows tall, so each
 *     screen pixel swallows four texels and keeps whichever one it landed on. Walking changes which
 *     one that is, and the surface boils.
 *
 * So the bank now keeps a mip chain per surface, the level 0 size is per surface rather than a
 * constant (512 for the two big rock faces, 128 for the railing, 256 for the rest), and the sampler
 * derives the pixel footprint itself - see Surface.Sample, which is where the interesting part is.
 *
 * The content changed for the same reason. A texture built out of smooth fbm has all its energy in
 * the frequencies that magnification smears and minification eats. Every surface here now carries
 * two structural scales on purpose: a large layout (plates, strata, planks, tiles) and a middle one
 * (rivets, grooves, knots, cracks), with the contrast in the middle scale, where it survives both
 * ends. Noise is periodic, so nothing seams at the tile border any more.
 *
 * WHAT THESE TEXTURES ARE FOR NOW
 * -------------------------------
 * Since the renderer started photographing the map, a wall takes its COLOUR from the photograph and
 * only its RELIEF from here: the renderer divides the sampled luminance by the surface mean and
 * damps what is left to a fifth. So a colour decision in this file barely reaches the screen, while
 * every hard light-to-dark edge does.
 *
 * That kills fine grain outright. Isotropic high frequency noise was never structure, it was the
 * illusion of it, and once it is the only thing riding on top of a real photograph it reads as
 * exactly what it is: dirt on the lens. What is left is directional and edged - seams, bevels, ribs,
 * grout, board joints, rivets - which is what survives both the damping and the mip chain.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public enum SurfaceKind : byte
{
    MetalPanel = 0,   // the default station wall: painted steel plate, rivets, grime
    MetalRibbed,      // corridor wall with vertical ribs
    LabTile,          // Laboratory / MedBay: white-blue tiling
    Concrete,         // Office, Admin: matte cast walls
    Rock,             // Polus outdoor rock face
    Snow,             // snow-covered ground edge
    Glass,            // windows, lab glass
    Door,             // sliding door leaf
    Vent,             // vent shaft mouth
    Console,          // low: a task console seen from the side
    Crate,            // low: crates, boxes
    Railing,          // low: railings and pipes, mostly see-through
    LavaRock,         // the hot side near the lava river
    Wood,             // Office desks, Dropship interior trim
    Count
}

public sealed class Surface
{
    public SurfaceKind Kind;
    public string Name;
    /// How many world units one tile of this texture covers. Bigger walls get bigger tiles so the
    /// pattern does not turn into noise at a distance.
    public float WorldScale = 1f;
    /// Multiplied onto the sampled colour when the ray hit the back of the segment. Wall faces that
    /// point away from you read as darker, which is what gives corners a visible edge with no
    /// lighting model at all.
    public float BackfaceTint = 0.82f;
    /// 0 = opaque. Above zero the surface is see-through (railings, glass), which the renderer uses
    /// to keep casting behind it.
    public float Transparency;

    /// Edge length of mip 0, always a power of two. Per surface rather than one constant: the rock
    /// faces are the only ones a player ever stands nose-first against with no seam to hide behind,
    /// and a railing is four bars and a lot of nothing.
    public int Size { get; private set; }

    /// The whole mip chain in one array, RGB bytes, level 0 first.
    ///
    /// Bytes, not the floats this used to hold. The renderer still multiplies light in float, which
    /// is what the old comment was really defending; what it stored was a source image, and a source
    /// image quantised to 1/255 is indistinguishable after that multiply. What it is not
    /// indistinguishable in is cache: filtering reads eight texels per pixel now, and a quarter of
    /// the bytes per texel is worth more here than the last bit of precision on a wall.
    private byte[] texels;
    private int[] mipOffset;
    private int[] mipShift;
    private int maxLevel;

    /// The last uv this surface was asked for, and the two footprints that followed from it. See
    /// Sample: `alongRun` is measured between consecutive pixels of a wall column or a floor row,
    /// `acrossRun` between one wall column and the next.
    private float lastU, lastV, alongRun = 1f, acrossRun;
    private bool inColumn;

    /// Level 0 as plain floats in 0..1, row-major RGB - the shape this class used to store outright.
    ///
    /// It is a COPY, built on the spot, because the bank itself now keeps bytes and a mip chain. It
    /// is here for the consumers that want the picture rather than the sampler (SurfaceStats reads
    /// it once at startup to get each material's mean brightness). It allocates the full image every
    /// time, so nothing may call it per frame.
    public float[] Pixels
    {
        get
        {
            var outp = new float[Size * Size * 3];
            for (int i = 0; i < outp.Length; i++) outp[i] = texels[i] * Inv255;
            return outp;
        }
    }

    private const float Inv255 = 1f / 255f;
    /// The chain runs all the way down to 2x2. Stopping at 16x16 seemed safe and was not: a wall far
    /// enough away still repeats its tile several times per screen column, and no mip level can blur
    /// ACROSS a repeat - only converging the level itself to a flat colour makes that stop striping.
    private const int SmallestMip = 1;
    /// Half a mip level of headroom. Trilinear picks the level whose texel matches the footprint,
    /// which means the coarser half of every blend is still sampled a little under its own Nyquist;
    /// the classic remedy is to ask for slightly more blur than the maths says.
    private const float LodBias = 1.5f;

    /// Samples the surface at uv, filtered for however large this pixel's footprint is.
    ///
    /// HOW THE FOOTPRINT IS KNOWN WITHOUT BEING TOLD
    /// ---------------------------------------------
    /// Choosing a mip level needs the derivative of uv with respect to the screen, and no caller
    /// here has it: the renderer walks a wall column row by row and a floor row column by column and
    /// passes one uv at a time. But that walk is exactly what makes the derivative available: two
    /// consecutive calls are two neighbouring screen pixels, so the step between them IS the
    /// footprint of one pixel. The state below is that memory, kept per surface so that two
    /// materials alternating along a floor row do not overwrite each other's history.
    ///
    /// A step larger than a quarter of the texture is not a neighbouring pixel: it is uv wrapping
    /// back to zero at a tile border, or the first sample after another material owned the column.
    /// Believing those would punch a razor-sharp or a fully blurred pixel into an otherwise even
    /// surface, so the previous footprint is kept instead - it came from a pixel a step away and is
    /// right to within a fraction of a mip level.
    ///
    /// THE SECOND AXIS, WHICH IS THE ONE THAT ACTUALLY ALIASED
    /// -------------------------------------------------------
    /// A wall column has ONE u for its whole height, so stepping down it measures only how fast the
    /// texture runs vertically. On a wall seen edge on the horizontal rate is the larger of the two
    /// by an order of magnitude, and taking the mip level from the vertical one alone left every
    /// rivet row and panel seam undersampled sideways: they came out as bands of dither, which was
    /// worse than the blur this whole change was meant to remove.
    ///
    /// That horizontal rate is measurable too, just not inside a column: it is the u step from the
    /// last pixel of one column to the first pixel of the next. Those two calls are consecutive, and
    /// they are recognisable because v wraps from the bottom of the wall back to the top. A floor
    /// row is the other case: there u advances per pixel like v does, so one step already carries
    /// both axes and the remembered column step must be dropped.
    public void Sample(float u, float v, out float r, out float g, out float b)
    {
        float du = u - lastU; if (du < 0f) du = -du;
        float dv = v - lastV; if (dv < 0f) dv = -dv;
        lastU = u; lastV = v;
        // uv arrives already wrapped into 0..1, so a step across a tile border reads as 0,98 rather
        // than as 0,02 and the footprint comes out fifty times too large. That is one poisoned
        // column per tile repeat, and on a wall it drew a vertical stripe every WorldScale units,
        // which looked exactly like the aliasing this sampler exists to remove.
        if (du > 0.5f) du = 1f - du;
        if (dv > 0.5f) dv = 1f - dv;

        if (du == 0f)
        {
            // Still walking down a wall column: u is literally the same float, which makes this an
            // exact test rather than a guess at a threshold.
            if (dv > 0f && dv < 0.25f) alongRun = dv * Size;
            inColumn = true;
        }
        else if (inColumn)
        {
            // The step that leaves a wall column. Whatever u did across it is how far the texture
            // slides sideways per screen column, and no guard on its size: a step too big to
            // believe saturates the mip chain, which for this axis is the right answer anyway.
            acrossRun = du * Size;
            inColumn = false;
        }
        else if (du < 0.25f && dv < 0.25f)
        {
            // A floor row. Both axes advance per pixel here, so one step carries the whole
            // footprint and any column step remembered from a wall is stale.
            alongRun = (du > dv ? du : dv) * Size;
            acrossRun = 0f;
        }

        // A wall column addresses v from the top of the wall to the bottom, ONCE, so wrapping v
        // would fold the ceiling row into the floor row. On a 256 texture that is half a texel and
        // nobody sees it; four mip levels down it is an eighth of the wall height and a bright band
        // along the skirting. Whether this is a wall is exactly what acrossRun records.
        bool wall = acrossRun > 0f;

        float dens = alongRun > acrossRun ? alongRun : acrossRun;
        if (dens <= 1f)
        {
            // Magnification. Plain bilinear here would be the mush this whole file is trying to get
            // away from, so the interpolation ramp is squeezed to about one screen pixel wide: texel
            // interiors stay flat and crisp and only their borders are smoothed. Retro, not blurry.
            Fetch(0, u, v, dens < 0.10f ? 0.10f : dens, wall, out r, out g, out b);
            return;
        }
        dens *= LodBias;

        // log2 straight out of the exponent field, with the mantissa taken as its own fraction. The
        // error against a real log2 peaks at 0,086 of a mip level, which is invisible, and it costs
        // a shift instead of a library call on a path that runs sixty thousand times a frame.
        int bits = BitConverter.SingleToInt32Bits(dens);
        int level = ((bits >> 23) & 0xFF) - 127;
        if (level >= maxLevel)
        {
            Fetch(maxLevel, u, v, 1f, wall, out r, out g, out b);
            return;
        }
        float frac = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000) - 1f;

        Fetch(level, u, v, 1f, wall, out r, out g, out b);
        // Blending the two levels rather than snapping to the nearer one: a snap draws a visible
        // ring on the floor where the level changes, and the floor is where the eye follows it.
        // Within a tenth of a level there is nothing left to blend, so that case skips the second
        // fetch entirely, which is most of the horizon.
        if (frac > 0.06f)
        {
            Fetch(level + 1, u, v, 1f, wall, out float r2, out float g2, out float b2);
            r += (r2 - r) * frac;
            g += (g2 - g) * frac;
            b += (b2 - b) * frac;
        }
    }

    /// Bilinear fetch from one mip level. `width` is how wide the interpolation ramp is in texels:
    /// 1 is textbook bilinear, less than 1 sharpens it towards point sampling with antialiased
    /// texel borders. `clampV` addresses v edge to edge instead of wrapping it, for wall columns.
    private void Fetch(int level, float u, float v, float width, bool clampV,
                       out float r, out float g, out float b)
    {
        int sh = mipShift[level];
        int n = 1 << sh;
        float fx = u * n - 0.5f, fy = v * n - 0.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float tx = fx - x0, ty = fy - y0;
        if (width < 1f)
        {
            float inv = 1f / width;
            tx = NfMath.Clamp01((tx - 0.5f) * inv + 0.5f);
            ty = NfMath.Clamp01((ty - 0.5f) * inv + 0.5f);
        }

        int m = n - 1;
        int ya, yb;
        if (clampV)
        {
            ya = y0 < 0 ? 0 : (y0 > m ? m : y0);
            yb = y0 + 1 < 0 ? 0 : (y0 + 1 > m ? m : y0 + 1);
        }
        else
        {
            ya = y0 & m;
            yb = (y0 + 1) & m;
        }
        int rowA = mipOffset[level] + ((ya << sh) * 3);
        int rowB = mipOffset[level] + ((yb << sh) * 3);
        int ca = (x0 & m) * 3, cb = ((x0 + 1) & m) * 3;

        float ix = 1f - tx, iy = 1f - ty;
        float w0 = ix * iy, w1 = tx * iy, w2 = ix * ty, w3 = tx * ty;
        int i0 = rowA + ca, i1 = rowA + cb, i2 = rowB + ca, i3 = rowB + cb;

        var t = texels;
        r = (t[i0] * w0 + t[i1] * w1 + t[i2] * w2 + t[i3] * w3) * Inv255;
        g = (t[i0 + 1] * w0 + t[i1 + 1] * w1 + t[i2 + 1] * w2 + t[i3 + 1] * w3) * Inv255;
        b = (t[i0 + 2] * w0 + t[i1 + 2] * w1 + t[i2 + 2] * w2 + t[i3 + 2] * w3) * Inv255;
    }

    /// Takes the finished float image, packs it to bytes and box-filters the mip chain off it.
    internal void Commit(float[] rgb, int size)
    {
        Size = size;
        int shift0 = 0;
        while ((1 << shift0) < size) shift0++;
        maxLevel = 0;
        while ((size >> (maxLevel + 1)) >= SmallestMip) maxLevel++;

        mipOffset = new int[maxLevel + 1];
        mipShift = new int[maxLevel + 1];
        int total = 0;
        for (int l = 0; l <= maxLevel; l++)
        {
            mipShift[l] = shift0 - l;
            mipOffset[l] = total;
            int n = 1 << mipShift[l];
            total += n * n * 3;
        }
        texels = new byte[total];

        var cur = rgb;
        int curN = size;
        Pack(0, cur, curN);
        for (int l = 1; l <= maxLevel; l++)
        {
            int n = curN >> 1;
            var next = new float[n * n * 3];
            for (int y = 0; y < n; y++)
            {
                int s0 = (y * 2) * curN * 3, s1 = (y * 2 + 1) * curN * 3;
                for (int x = 0; x < n; x++)
                {
                    int a = s0 + x * 2 * 3, bIdx = s1 + x * 2 * 3, d = (y * n + x) * 3;
                    for (int ch = 0; ch < 3; ch++)
                        next[d + ch] = 0.25f * (cur[a + ch] + cur[a + 3 + ch]
                                              + cur[bIdx + ch] + cur[bIdx + 3 + ch]);
                }
            }
            Pack(l, next, n);
            cur = next; curN = n;
        }
    }

    private void Pack(int level, float[] rgb, int n)
    {
        int o = mipOffset[level];
        for (int i = 0; i < n * n * 3; i++) texels[o + i] = NfMath.ToByte(rgb[i]);
    }
}

public static class TextureBank
{
    private static Surface[] surfaces;

    public static Surface Get(SurfaceKind kind)
    {
        EnsureBuilt();
        return surfaces[(int)kind];
    }

    public static Surface Get(byte materialIndex)
    {
        EnsureBuilt();
        int i = materialIndex < surfaces.Length ? materialIndex : 0;
        return surfaces[i];
    }

    public static IReadOnlyList<Surface> All
    {
        get { EnsureBuilt(); return surfaces; }
    }

    public static void EnsureBuilt()
    {
        if (surfaces != null) return;
        var list = new Surface[(int)SurfaceKind.Count];
        for (int i = 0; i < list.Length; i++) list[i] = Build((SurfaceKind)i);
        surfaces = list;
    }

    // ================================================================================
    // Palette, sampled from the real Polus render
    // ================================================================================
    private static readonly NfColor SteelBase = NfColor.FromBytes(0x6E, 0x76, 0x84);
    private static readonly NfColor SteelDark = NfColor.FromBytes(0x3E, 0x45, 0x52);
    private static readonly NfColor SteelLight = NfColor.FromBytes(0x98, 0xA2, 0xB0);
    private static readonly NfColor LabWhite = NfColor.FromBytes(0xDE, 0xE6, 0xEE);
    private static readonly NfColor LabBlue = NfColor.FromBytes(0x7E, 0xA8, 0xCC);
    private static readonly NfColor ConcreteBase = NfColor.FromBytes(0x7A, 0x74, 0x80);
    private static readonly NfColor RockBase = NfColor.FromBytes(0x5B, 0x3F, 0x63);
    private static readonly NfColor RockLight = NfColor.FromBytes(0x8A, 0x64, 0x92);
    private static readonly NfColor SnowBase = NfColor.FromBytes(0xE4, 0xE8, 0xF2);
    private static readonly NfColor GlassBase = NfColor.FromBytes(0x5E, 0x86, 0x9E);
    private static readonly NfColor DoorBase = NfColor.FromBytes(0x55, 0x5E, 0x6C);
    private static readonly NfColor WoodBase = NfColor.FromBytes(0x8A, 0x6A, 0x46);
    private static readonly NfColor LavaGlow = NfColor.FromBytes(0xE8, 0x62, 0x1E);
    private static readonly NfColor VentDark = NfColor.FromBytes(0x24, 0x28, 0x30);
    private static readonly NfColor Rust = NfColor.FromBytes(0x84, 0x46, 0x24);
    private static readonly NfColor Hazard = NfColor.FromBytes(0xE0, 0xB8, 0x30);

    // ================================================================================
    // Build-time canvas and noise
    //
    // None of this runs per frame: the whole bank is built once, so it can afford cellular noise
    // and domain warping. What it must not do is produce anything that does not tile, because a
    // surface repeats every WorldScale units and a noise seam would draw a hard line down the wall
    // at every one of them. Every generator below is periodic by construction.
    // ================================================================================
    private sealed class Canvas
    {
        public readonly int N;
        public readonly float[] Rgb;
        public Canvas(int n) { N = n; Rgb = new float[n * n * 3]; }

        public void Put(int x, int y, NfColor c)
        {
            int i = (y * N + x) * 3;
            Rgb[i] = NfMath.Clamp01(c.R);
            Rgb[i + 1] = NfMath.Clamp01(c.G);
            Rgb[i + 2] = NfMath.Clamp01(c.B);
        }
    }

    private static int Wrap(int v, int period)
    {
        v %= period;
        return v < 0 ? v + period : v;
    }

    /// Value noise on a lattice that wraps after fx cells across and fy cells down, so it tiles.
    /// Separate frequencies per axis because almost every real material is anisotropic: brushed
    /// steel streaks along the plate, wood grain runs down the plank, rock stratifies horizontally.
    private static float PNoise(float u, float v, int fx, int fy, int seed)
    {
        if (fx < 1) fx = 1;
        if (fy < 1) fy = 1;
        float x = u * fx, y = v * fy;
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float ax = x - xi, ay = y - yi;
        ax = ax * ax * (3f - 2f * ax);
        ay = ay * ay * (3f - 2f * ay);
        int x0 = Wrap(xi, fx), x1 = Wrap(xi + 1, fx);
        int y0 = Wrap(yi, fy), y1 = Wrap(yi + 1, fy);
        float a = NfMath.Hash(x0, y0, seed), b = NfMath.Hash(x1, y0, seed);
        float c = NfMath.Hash(x0, y1, seed), d = NfMath.Hash(x1, y1, seed);
        float top = a + (b - a) * ax, bot = c + (d - c) * ax;
        return top + (bot - top) * ay;
    }

    private static float PFbm(float u, float v, int fx, int fy, int octaves, int seed,
                              float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += PNoise(u, v, fx, fy, seed + i * 131) * amp;
            norm += amp;
            amp *= gain;
            fx <<= 1;
            fy <<= 1;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    /// Cellular noise: distance to the nearest and second nearest of a jittered point per cell.
    /// f2 - f1 is small exactly on the border between two cells, which is the cheapest way to get a
    /// crack that is a LINE rather than a smudge - the thing the old rock textures were missing.
    private static void PWorley(float u, float v, int fx, int fy, int seed,
                                out float f1, out float f2, out float tone)
    {
        if (fx < 1) fx = 1;
        if (fy < 1) fy = 1;
        float x = u * fx, y = v * fy;
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        f1 = 9f; f2 = 9f;
        int bx = 0, by = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = xi + dx, cy = yi + dy;
                int wx = Wrap(cx, fx), wy = Wrap(cy, fy);
                float jx = cx + 0.15f + 0.7f * NfMath.Hash(wx, wy, seed);
                float jy = cy + 0.15f + 0.7f * NfMath.Hash(wx, wy, seed + 5077);
                float ex = jx - x, ey = jy - y;
                float d = ex * ex + ey * ey;
                if (d < f1) { f2 = f1; f1 = d; bx = wx; by = wy; }
                else if (d < f2) f2 = d;
            }
        }
        // Squared while searching, rooted once at the end: the ordering is the same either way and
        // the whole bank is nine cells of this per texel.
        f1 = MathF.Sqrt(f1);
        f2 = MathF.Sqrt(f2);
        tone = NfMath.Hash(bx, by, seed + 9151);
    }

    private static float Sat(float v) => NfMath.Clamp01(v);

    /// 1 at the ridge, 0 at the flanks. Turns a value field into a set of lines.
    private static float Ridge(float f) => 1f - MathF.Abs(f * 2f - 1f);

    // ================================================================================
    // Builders
    // ================================================================================
    /// Level 0 edge length per surface. 256 is the working default: it puts one texel at roughly
    /// six millimetres of wall, which is finer than the 320x180 view can resolve until you are
    /// closer than a metre, and that is the case this whole change is about.
    private static int SizeFor(SurfaceKind kind) => kind switch
    {
        // The two rock faces are the biggest continuous surfaces on Polus, they carry no man-made
        // grid to hide the repeat behind, and outdoors is where the player walks right up to a face
        // with nothing else in frame.
        SurfaceKind.Rock or SurfaceKind.LavaRock => 512,
        // Four bars and a lot of nothing. More texels would only store more nothing.
        SurfaceKind.Railing => 128,
        _ => 256,
    };

    private static Surface Build(SurfaceKind kind)
    {
        var s = new Surface { Kind = kind, Name = kind.ToString() };
        var c = new Canvas(SizeFor(kind));
        switch (kind)
        {
            case SurfaceKind.MetalPanel: BuildMetalPanel(s, c); break;
            case SurfaceKind.MetalRibbed: BuildMetalRibbed(s, c); break;
            case SurfaceKind.LabTile: BuildLabTile(s, c); break;
            case SurfaceKind.Concrete: BuildConcrete(s, c); break;
            case SurfaceKind.Rock: BuildRock(s, c); break;
            case SurfaceKind.Snow: BuildSnow(s, c); break;
            case SurfaceKind.Glass: BuildGlass(s, c); break;
            case SurfaceKind.Door: BuildDoor(s, c); break;
            case SurfaceKind.Vent: BuildVent(s, c); break;
            case SurfaceKind.Console: BuildConsole(s, c); break;
            case SurfaceKind.Crate: BuildCrate(s, c); break;
            case SurfaceKind.Railing: BuildRailing(s, c); break;
            case SurfaceKind.LavaRock: BuildLavaRock(s, c); break;
            case SurfaceKind.Wood: BuildWood(s, c); break;
        }
        s.Commit(c.Rgb, c.N);
        return s;
    }

    /// Rivet centres inside one 128x64 steel plate.
    private static readonly int[] PlateRivets = { 11, 10, 64, 10, 116, 10, 11, 53, 64, 53, 116, 53 };

    /// The default station wall: rolled steel plates on a frame, rivetted at the edges, with the
    /// grime a station accumulates. Every plate is one of four builds picked by its own hash, which
    /// is what stops a corridor from being the same rectangle sixty times.
    private static void BuildMetalPanel(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.6f;
        const int pw = 128, ph = 64;      // 0,80 x 0,44 world units per plate

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int px = x % pw, py = y % ph;
                float plate = NfMath.Hash(x / pw, y / ph, 101);

                // Rolled plate has a grain along it, so what noise there is runs sixteen to one along
                // the plate. Faint: the plate-to-plate step below is the part that reads.
                float brush = PFbm(u, v, 6, 96, 2, 11) - 0.5f;
                var col = SteelBase * (1f + brush * 0.06f + (plate - 0.5f) * 0.12f);

                // --- plate interior features, before the frame is drawn over them ---
                int ix = px - pw / 2, iy = py - ph / 2;
                if (plate < 0.34f)
                {
                    // A recessed inner panel: two grooves and a bevel, no more, but it puts a
                    // rectangle inside the rectangle and that is the middle scale the eye reads.
                    int dx = Math.Abs(ix), dy = Math.Abs(iy);
                    bool inside = dx < 44 && dy < 19;
                    bool edge = (dx >= 42 && dx <= 44 && dy <= 19) || (dy >= 17 && dy <= 19 && dx <= 44);
                    if (inside && !edge) col = col * 0.94f;
                    if (edge) col = NfColor.Lerp(col, SteelDark, iy < 0 ? 0.62f : 0.2f);
                }
                else if (plate < 0.62f)
                {
                    // Louvre slots: a cooling panel. Hard black slots with a lit top lip.
                    if (Math.Abs(ix) < 40 && Math.Abs(iy) < 17)
                    {
                        int slot = Wrap(iy + 17, 11);
                        if (slot < 7) col = NfColor.Lerp(VentDark, SteelDark, slot * 0.09f);
                        else if (slot == 7) col = NfColor.Lerp(col, SteelLight, 0.55f);
                    }
                }
                else if (plate < 0.80f)
                {
                    // A bolted hatch with a raised lid.
                    int dx = Math.Abs(ix), dy = Math.Abs(iy);
                    if (dx < 26 && dy < 20)
                    {
                        col = col * 1.06f;
                        if (dx > 23 || dy > 17) col = NfColor.Lerp(col, ix + iy < 0 ? SteelLight : SteelDark, 0.5f);
                        float hd = MathF.Sqrt(ix * ix + iy * iy);
                        if (hd < 5.5f) col = NfColor.Lerp(col, SteelDark, 0.6f);
                        if (hd < 4f) col = NfColor.Lerp(SteelDark, SteelLight, 0.55f - (ix + iy) * 0.06f);
                    }
                }
                else
                {
                    // A hazard flash in the corner of the plate. One in five plates, so a corridor
                    // gets a spot of colour without turning into a warning sign.
                    if (px > pw - 46 && px < pw - 12 && py > 12 && py < 26)
                    {
                        int band = Wrap((px + py * 2) / 7, 2);
                        col = band == 0 ? Hazard * 0.85f : SteelDark;
                    }
                }

                // --- the frame around every plate ---
                int ex = Math.Min(px, pw - 1 - px);
                int ey = Math.Min(py, ph - 1 - py);
                int e = Math.Min(ex, ey);
                bool horizontal = ey < ex;
                bool lightSide = horizontal ? py < ph / 2 : px < pw / 2;
                if (e < 2) col = SteelDark * 0.55f;
                else if (e < 4) col = NfColor.Lerp(col, lightSide ? SteelLight : SteelDark, 0.62f);
                else if (e < 6) col = NfColor.Lerp(col, lightSide ? SteelLight : SteelDark, 0.22f);

                // --- rivets ---
                float rd = 1e9f;
                int rx = 0, ry = 0;
                for (int k = 0; k < PlateRivets.Length; k += 2)
                {
                    float ddx = px - PlateRivets[k], ddy = py - PlateRivets[k + 1];
                    float d = MathF.Sqrt(ddx * ddx + ddy * ddy);
                    if (d < rd) { rd = d; rx = PlateRivets[k]; ry = PlateRivets[k + 1]; }
                }
                if (rd < 5f) col = col * (0.72f + 0.28f * Sat(rd - 3.6f));        // contact shadow
                if (rd < 3.6f)
                {
                    // A dome, lit from the upper left like everything else in the frame.
                    float nx = (px - rx) / 3.6f, ny = (py - ry) / 3.6f;
                    float lam = Sat(0.55f - (nx + ny) * 0.5f);
                    col = NfColor.Lerp(SteelDark, SteelLight, 0.2f + lam * 0.95f);
                }

                // --- ageing, over the top of everything ---
                // Rust, in a few patches rather than everywhere. It is mostly a colour, and colour
                // is the part of this file the renderer now throws away, so it stays small.
                PWorley(u, v, 4, 4, 313, out float f1, out _, out float rustTone);
                float rustMask = Sat((rustTone - 0.74f) * 6f) * Sat(1.2f - f1);
                col = NfColor.Lerp(col, Rust, rustMask * 0.6f);

                // Streaks running down the plate. Vertical by construction, so they read as runs of
                // dirt and not as noise, and they stop well short of black.
                float streak = PFbm(u, v, 40, 3, 2, 77);
                float grime = NfMath.SmoothStep(0.35f, 1f, v) * (0.08f + Sat(streak * 1.7f - 0.85f) * 0.3f);
                col = NfColor.Lerp(col, SteelDark, Sat(grime));

                c.Put(x, y, col);
            }
        }
    }

    /// Corridor wall: vertical ribs, a service rail and a kick plate. Reads as motion when you walk
    /// past it, which is what sells a corridor as a corridor in a raycaster.
    private static void BuildMetalRibbed(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.2f;
        const int rib = 32;               // 15 cm per rib

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int p = x % rib;
                float brush = PFbm(u, v, 5, 80, 2, 19) - 0.5f;
                // Each rib is its own piece of steel and picks up its own share of the light. Without
                // this the wall is a picket fence stamped from one rib eight times.
                float ribTone = NfMath.Hash(x / rib, 0, 373) - 0.5f;
                var col = SteelBase * (1f + brush * 0.05f + ribTone * 0.14f);

                // Rib profile: a raised face with a lit left edge, a shaded right edge and a hard
                // shadow gap. Trapezoid rather than a sine, because a sine is a gradient and a
                // gradient is the thing that disappears at two metres.
                if (p < 2) col = NfColor.Lerp(col, SteelLight, 0.72f);
                else if (p < 20) col = col * (1.06f - (p - 2) * 0.004f);
                else if (p < 22) col = NfColor.Lerp(col, SteelDark, 0.55f);
                else col = NfColor.Lerp(col, SteelDark, 0.78f - (p - 22) * 0.02f);

                // A conduit crossing the ribs near the top, with clamps.
                float pipe = (v - 0.16f) / 0.05f;
                if (pipe > -1f && pipe < 1f)
                {
                    float shade = 0.55f + 0.75f * Sat(1f - MathF.Abs(pipe + 0.45f));
                    col = SteelBase * shade;
                    if (MathF.Abs(pipe) > 0.86f) col = SteelDark * 0.7f;
                    if (Wrap(x, 64) < 7) col = NfColor.Lerp(col, SteelLight, 0.45f);   // clamp
                }

                // The service rail: a bright horizontal band with bolts, at the height a hand rail
                // would be. It gives the eye a horizon inside the texture itself.
                if (v > 0.615f && v < 0.665f)
                {
                    col = NfColor.Lerp(SteelBase, SteelLight, v < 0.63f ? 0.75f : 0.25f);
                    if (Wrap(x, 32) < 3) col = col * 0.7f;
                }
                else if (v >= 0.665f && v < 0.68f) col = SteelDark * 0.6f;

                // Kick plate: darker, with the raised diamond of a real chequer plate rather than a
                // hatch, which at three texels a line was reading as fabric.
                if (v > 0.845f)
                {
                    col = NfColor.Lerp(col, SteelDark, 0.55f);
                    int dxi = Wrap(x, 24), dyi = Wrap(y - (x / 24) * 6, 12);
                    bool stud = dxi > 3 && dxi < 19 && dyi > 2 && dyi < 8
                                && Math.Abs(dxi - 11) * 3 + Math.Abs(dyi - 5) * 8 < 40;
                    if (stud) col = col * (dyi < 5 ? 1.35f : 0.82f);
                }
                else if (v > 0.83f) col = NfColor.Lerp(col, SteelLight, 0.4f);

                float grime = NfMath.SmoothStep(0.55f, 1f, v) * 0.2f;
                col = NfColor.Lerp(col, SteelDark, grime);
                c.Put(x, y, col);
            }
        }
    }

    /// Laboratory and MedBay: small glazed tiles, a dado band at chest height, a scuffed skirting.
    private static void BuildLabTile(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.4f;
        const int tile = 32;              // 17 cm tiles

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int px = x % tile, py = y % tile;
                float t = NfMath.Hash(x / tile, y / tile, 211);

                // Every tile is fired slightly differently. Per TILE, not per texel: the variation
                // belongs to the grid, and a noise field laid across the grid is just grain.
                var col = NfColor.Lerp(LabWhite, LabBlue, 0.10f + (t - 0.5f) * 0.26f);
                if (t > 0.93f) col = NfColor.Lerp(col, LabBlue, 0.55f);      // an accent tile

                int e = Math.Min(Math.Min(px, tile - 1 - px), Math.Min(py, tile - 1 - py));
                if (e < 2) col = NfColor.Lerp(LabBlue, SteelDark, 0.55f);    // grout
                else if (e < 4)
                {
                    bool lit = (py < tile / 2 && py <= px && py <= tile - 1 - px)
                               || (px < tile / 2 && px < py && px <= tile - 1 - py);
                    col = NfColor.Lerp(col, lit ? NfColor.White : SteelDark, lit ? 0.5f : 0.22f);
                }
                else
                {
                    // Glaze highlight in the upper left of each tile: these are shiny.
                    float sheen = Sat(1f - (px + py) / 26f);
                    col = NfColor.Lerp(col, NfColor.White, sheen * 0.28f);
                }

                // Dado band: two metal edges and a darker field between them. The single strongest
                // horizontal landmark in the room, and it costs eight rows of texels.
                if (v > 0.54f && v < 0.635f)
                {
                    col = NfColor.Lerp(LabBlue, SteelDark, 0.35f);
                    if (v < 0.556f || v > 0.62f) col = NfColor.Lerp(SteelLight, LabWhite, 0.4f);
                    else if (v < 0.572f) col = col * 0.72f;
                }

                // Skirting, and the scuffs that collect just above it.
                if (v > 0.9f) col = NfColor.Lerp(SteelBase, SteelDark, 0.35f + (v - 0.9f) * 2f);
                else if (v > 0.885f) col = SteelLight * 0.9f;
                else if (v > 0.78f)
                {
                    // Scuffs, wiped sideways the way a boot wipes them. Two octaves: a third one
                    // only adds the per texel speckle the renderer now shows as dirt on the lens.
                    float scuff = PFbm(u, v, 16, 5, 2, 137);
                    col = NfColor.Lerp(col, SteelDark, Sat(scuff * 1.9f - 1.1f) * 0.45f);
                }

                c.Put(x, y, col);
            }
        }
    }

    /// Office and Admin: cast concrete straight out of the formwork. Board marks, tie holes,
    /// aggregate and the cracks that follow it.
    private static void BuildConcrete(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 2.0f;
        const int board = 42;             // one shuttering board, about 29 cm

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                float bTone = NfMath.Hash(0, y / board, 353);
                float blotch = PFbm(u, v, 4, 4, 3, 7) - 0.5f;
                var col = ConcreteBase * (1f + blotch * 0.14f + (bTone - 0.5f) * 0.10f);

                // Board marks: the seam between two shuttering boards, one dark line and one lit lip.
                int by = y % board;
                if (by < 2) col = col * 0.72f;
                else if (by == 2) col = NfColor.Lerp(col, NfColor.White, 0.2f);
                else if (by > board - 3) col = col * 0.92f;
                // The pour joint where two panels of formwork met. Once per tile and shallower than
                // the board marks: two seams of equal weight crossing each other read as brickwork,
                // which is not what a cast wall looks like.
                int jx = Wrap(x - 20, 256);
                if (jx < 2) col = col * 0.86f;

                // Tie rod holes: one every second board, a recess with a lit lower rim. Sparse on
                // purpose - a hole every board in both directions reads as pegboard.
                int hx = Wrap(x - 60, 128) - 64, hy = by - board / 2;
                float hd = MathF.Sqrt(hx * hx + hy * hy * 1.1f);
                if (hd < 6.5f && (y / board) % 2 == 0)
                {
                    col = col * (0.5f + 0.12f * hd / 6.5f);
                    if (hd > 4.6f && hy > 0) col = NfColor.Lerp(col, NfColor.White, 0.32f);
                }

                // No aggregate speckle here any more. Scattered independent dots are the one kind
                // of detail that cannot survive being laid over a photograph: they have no direction
                // and no edge to line up with, so they read as sensor noise rather than as a wall.
                // What is left is the joint pattern, which does have both.

                // Cracks: cell borders, but only where a slow mask lets them through, so they run in
                // a few places instead of covering the wall in a net.
                PWorley(u, v, 6, 6, 823, out float g1, out float g2, out _);
                float crackMask = Sat(PFbm(u, v, 3, 3, 2, 941) * 2.4f - 1.15f);
                float crack = Sat((0.045f - (g2 - g1)) * 34f) * crackMask;
                col = NfColor.Lerp(col, ConcreteBase * 0.38f, crack);

                // Water stains bleeding down from the top edge.
                float drip = PFbm(u, v, 36, 2, 3, 1013);
                col = NfColor.Lerp(col, ConcreteBase * 0.6f,
                                   Sat(drip * 2.1f - 1.15f) * NfMath.SmoothStep(0.0f, 0.55f, v) * 0.55f);
                c.Put(x, y, col);
            }
        }
    }

    /// Polus rock: the violet cliff the whole base is bolted to.
    ///
    /// The trap here is cellular noise. Drawing a dark line along every cell border gives a perfect
    /// crack net and the result is unmistakably dried mud, not stone - the first version of this
    /// texture was exactly that. Rock is layered first and broken second, so this is built the same
    /// way round: bedding planes carry the structure, the cells only change the SHADE of each facet,
    /// and the actual fissures come from ridged noise, which branches, varies in width and stops.
    private static void BuildRock(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 2.4f;
        s.BackfaceTint = 0.9f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;

                // One warp, reused by everything below. Enough to stop the strata running dead
                // straight; not enough to make the face look like draped cloth, which is where the
                // second attempt at this ended up.
                float wx = PFbm(u, v, 3, 3, 2, 5) - 0.5f;
                float wu = u + wx * 0.07f, wv = v + wx * 0.045f;

                float bandf = wv * 7f + wx * 0.9f;
                int band = (int)MathF.Floor(bandf);
                float within = bandf - band;
                var col = NfColor.Lerp(RockBase, RockLight, 0.16f + NfMath.Hash(0, band, 197) * 0.5f);

                // Facets, quantised. A continuous cell tone is a soft blob; five flat steps with a
                // hard boundary between them is a broken rock face, and the boundary is the only
                // part of it that survives being damped to a fifth on screen.
                PWorley(wu, wv, 6, 5, 733, out _, out _, out float facet);
                PWorley(wu, wv, 15, 12, 907, out _, out _, out float chip);
                col = col * (0.80f + MathF.Floor(facet * 5f) * 0.105f)
                          * (0.94f + MathF.Floor(chip * 3f) * 0.045f);

                // Bedding plane: a hard shadow under the overhang, a lit ledge on top of it. This is
                // the feature that has to survive four mip levels, so it gets the most contrast.
                if (within < 0.05f) col = col * 0.42f;
                else if (within < 0.10f) col = NfColor.Lerp(col, RockLight, 0.55f);
                else col = col * (1.03f - within * 0.16f);

                // Fissures: ridged noise thresholded hard, so they come out as LINES. Thresholded
                // softly they come out as continents, which is what the second attempt produced.
                float fis = Ridge(PFbm(wu, wv, 7, 6, 3, 311));
                float crack = Sat((fis - 0.90f) * 16f)
                              * Sat(PFbm(u, v, 3, 3, 2, 337) * 2.4f - 0.9f);
                col = NfColor.Lerp(col, RockBase * 0.3f, Sat(crack));

                // Snow only where a ledge could hold it: the up-facing top of a bedding plane, and
                // more of it towards the top of the face.
                float ledge = Sat((0.13f - within) * 9f);
                float snow = Sat(ledge * (PFbm(u, v, 7, 5, 2, 271) * 2.6f - 1.0f))
                             * NfMath.SmoothStep(0.72f, 0.04f, v);
                col = NfColor.Lerp(col, SnowBase, Sat(snow * 1.5f) * 0.85f);

                c.Put(x, y, col);
            }
        }
    }

    /// Wind-packed snow: carved into steps by the wind, rippled across them, with the odd patch of
    /// blue ice and grit blown in from the rock.
    private static void BuildSnow(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 2.2f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                float drift = PFbm(u, v, 3, 5, 4, 13);
                float step = drift * 5f + PFbm(u, v, 9, 14, 2, 29) * 0.9f;
                int level = (int)MathF.Floor(step);
                float within = step - level;

                var col = SnowBase * (0.9f + NfMath.Hash(0, level, 331) * 0.12f);
                // The cut face of a sastruga: a blue shadow wall and a bright lip over it. This is
                // the contrast the old snow had none of, and snow with no contrast is a white
                // rectangle no matter how much noise is poured into it.
                if (within < 0.10f) col = NfColor.Lerp(col, LabBlue, 0.62f) * 0.88f;
                else if (within < 0.20f) col = NfColor.Lerp(col, NfColor.White, 0.8f);
                else col = col * (1.02f - within * 0.13f);

                // Wind ripples across the drift.
                float ripple = Ridge(PFbm(u, v, 44, 8, 2, 53));
                col = NfColor.Lerp(col, LabBlue, Sat(ripple - 0.62f) * 0.75f);

                // Ice: smoother, bluer, with a bright rim where it was walked on.
                PWorley(u, v, 6, 6, 419, out float f1, out _, out float tone);
                if (tone > 0.80f)
                {
                    float ice = Sat((0.44f - f1) * 4f);
                    col = NfColor.Lerp(col, NfColor.Lerp(LabBlue, SnowBase, 0.5f), ice * 0.8f);
                    if (f1 > 0.36f && f1 < 0.44f) col = NfColor.Lerp(col, NfColor.White, 0.45f);
                }

                c.Put(x, y, col);
            }
        }
    }

    /// Window glass in a steel frame. Everything readable about glass is a hard-edged reflection,
    /// so that is what this is: the base colour barely matters.
    private static void BuildGlass(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.6f;
        s.Transparency = 0.55f;
        const int pane = 128;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int px = x % pane, py = y % pane;
                var col = GlassBase * 0.62f;

                // Two reflection bands at different widths, both with hard edges: a wide one for the
                // sky and a narrow bright one beside it.
                float diag = (x * 0.75f + y) / 96f;
                float d1 = diag - MathF.Floor(diag);
                if (d1 > 0.10f && d1 < 0.30f) col = NfColor.Lerp(col, NfColor.White, 0.30f);
                if (d1 > 0.33f && d1 < 0.37f) col = NfColor.Lerp(col, NfColor.White, 0.62f);
                if (d1 > 0.62f && d1 < 0.66f) col = NfColor.Lerp(col, NfColor.White, 0.18f);

                // Rain film and dust, faint, so the pane is not a flat colour between the bands.
                float film = PFbm(u, v, 30, 6, 3, 173) - 0.5f;
                col = col * (1f + film * 0.08f);

                int e = Math.Min(Math.Min(px, pane - 1 - px), Math.Min(py, pane - 1 - py));
                if (e < 4) col = SteelDark * 0.75f;                                  // frame
                else if (e < 7) col = NfColor.Lerp(SteelBase, SteelLight, py < pane / 2 ? 0.6f : 0.1f);
                else if (e < 9) col = SteelDark;                                     // frame shadow on glass

                // Corner brackets with a bolt.
                int bx = Math.Min(px, pane - 1 - px), by2 = Math.Min(py, pane - 1 - py);
                if (bx < 18 && by2 < 18)
                {
                    col = NfColor.Lerp(SteelBase, SteelDark, 0.25f);
                    float bd = MathF.Sqrt((bx - 9) * (bx - 9) + (by2 - 9) * (by2 - 9));
                    if (bd < 4f) col = NfColor.Lerp(SteelDark, SteelLight, 0.7f - bd * 0.12f);
                }

                c.Put(x, y, col);
            }
        }
    }

    /// A sliding door: two leaves, a hazard band, a vision slit and a treaded kick plate.
    private static void BuildDoor(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.0f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int leaf = x < N / 2 ? 0 : 1;
                int lx = leaf == 0 ? x : N - 1 - x;          // mirrored, so the door is symmetric
                float brush = PFbm(u, v, 5, 60, 3, 31) - 0.5f;
                var col = DoorBase * (1f + brush * 0.05f);

                // Recessed inner panel per leaf.
                if (lx > 16 && lx < N / 2 - 10 && v > 0.08f && v < 0.9f)
                {
                    bool edge = lx < 20 || lx > N / 2 - 14 || v < 0.10f || v > 0.88f;
                    col = edge ? NfColor.Lerp(col, v < 0.5f ? SteelLight : SteelDark, 0.5f) : col * 0.93f;
                }

                // Vision slit: dark glass with a lit frame.
                if (lx > 30 && lx < N / 2 - 24 && v > 0.20f && v < 0.30f)
                {
                    bool frame = lx < 34 || lx > N / 2 - 28 || v < 0.212f || v > 0.288f;
                    col = frame ? SteelLight * 0.85f
                                : NfColor.Lerp(GlassBase * 0.4f, NfColor.White, Sat((0.3f - v) * 6f) * 0.35f);
                }

                // Hazard band across both leaves. 45 degrees, hard edges, period 24.
                if (v > 0.44f && v < 0.575f)
                {
                    int band = Wrap((x + y) / 12, 2);
                    col = band == 0 ? Hazard : SteelDark * 0.8f;
                    if (v < 0.452f || v > 0.567f) col = SteelDark * 0.5f;
                    col = col * (1f + brush * 0.1f);
                }

                // Kick plate.
                if (v > 0.9f)
                {
                    col = NfColor.Lerp(SteelBase, SteelDark, 0.45f);
                    if (Wrap(x + (int)(v * N), 8) < 4) col = col * 1.15f;
                }
                else if (v > 0.885f) col = SteelLight * 0.8f;

                // Bolts down the leading edge of each leaf.
                if (lx > 4 && lx < 12)
                {
                    int by = Wrap(y - 12, 40);
                    float dd = MathF.Sqrt((lx - 8) * (lx - 8) + (by - 20) * (by - 20));
                    if (dd < 3.4f) col = NfColor.Lerp(SteelDark, SteelLight, 0.75f - dd * 0.13f);
                }

                // The centre gap: black, with a lit edge on each leaf.
                int gap = Math.Abs(x - N / 2);
                if (gap < 3) col = NfColor.Black;
                else if (gap < 6) col = NfColor.Lerp(SteelLight, SteelBase, 0.4f);

                // Outer jamb.
                if (x < 4 || x > N - 5) col = SteelLight * 0.65f;
                c.Put(x, y, col);
            }
        }
    }

    /// Vent mouth: louvres with real depth, a bolted frame and mesh behind the slots.
    private static void BuildVent(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.0f;
        const int louvre = 22;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int p = y % louvre;
                NfColor col;
                if (p < 2) col = NfColor.Lerp(SteelLight, SteelBase, 0.25f);       // blade edge, lit
                else if (p < 9) col = NfColor.Lerp(SteelBase, SteelDark, 0.15f + (p - 2) * 0.10f);
                else
                {
                    // The slot. Not flat black: a gradient into it plus the mesh behind, which is
                    // what tells you it is a hole and not a stripe.
                    float depth = Sat((p - 9) / 5f);
                    col = NfColor.Lerp(SteelDark * 0.5f, VentDark * 0.55f, depth);
                    if (((x / 3) + (y / 3)) % 2 == 0) col = col * 1.35f;
                }

                float grime = PFbm(u, v, 20, 20, 3, 191) - 0.5f;
                col = col * (1f + grime * 0.06f);

                // Frame with corner bolts.
                int e = Math.Min(Math.Min(x, N - 1 - x), Math.Min(y, N - 1 - y));
                if (e < 10)
                {
                    col = NfColor.Lerp(SteelBase, e < 3 ? SteelDark : SteelLight, e < 3 ? 0.5f : 0.3f);
                    int cx = Math.Min(x, N - 1 - x), cy = Math.Min(y, N - 1 - y);
                    float bd = MathF.Sqrt((cx - 5) * (cx - 5) + (cy - 5) * (cy - 5));
                    if (bd < 3.2f) col = NfColor.Lerp(SteelDark, SteelLight, 0.8f - bd * 0.15f);
                }
                c.Put(x, y, col);
            }
        }
    }

    /// A task console seen from the side: the only surface in the map that emits its own light, so
    /// it is the most useful landmark in a blackout and gets the most detail per texel.
    private static void BuildConsole(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.0f;
        var screenDark = NfColor.FromBytes(0x0C, 0x24, 0x30);
        var screenLit = NfColor.FromBytes(0x6E, 0xE0, 0xF0);
        var screenMid = NfColor.FromBytes(0x24, 0x88, 0x9E);

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                var col = NfColor.Lerp(SteelBase, SteelDark, 0.35f);
                col = col * (1f + (PFbm(u, v, 6, 40, 2, 55) - 0.5f) * 0.06f);

                if (v < 0.06f) col = NfColor.Lerp(col, SteelLight, 0.55f);           // top edge, lit
                else if (v < 0.09f) col = col * 0.8f;

                bool inScreen = v > 0.11f && v < 0.47f && u > 0.10f && u < 0.90f;
                if (inScreen)
                {
                    bool bezel = v < 0.135f || v > 0.445f || u < 0.13f || u > 0.87f;
                    if (bezel) col = NfColor.Lerp(SteelDark, SteelLight, v < 0.3f ? 0.4f : 0.12f);
                    else
                    {
                        col = screenDark;
                        // A bar readout: hashed column heights, hard edges, plus scanlines.
                        float bf = (u - 0.13f) / 0.030f;
                        int bar = (int)bf;
                        float h = 0.15f + NfMath.Hash(bar, 0, 733) * 0.72f;
                        float rel = (0.44f - v) / 0.30f;
                        if (bf - bar < 0.78f && rel < h)
                            col = NfColor.Lerp(screenMid, screenLit, rel / MathF.Max(h, 0.01f));
                        // A trace line running across the readout.
                        float trace = 0.30f + PNoise(u, 0f, 12, 1, 811) * 0.12f;
                        if (MathF.Abs(v - trace) < 0.008f) col = screenLit;
                        if (y % 3 == 0) col = col * 0.72f;                            // scanline
                    }
                }
                else if (v > 0.50f && v < 0.78f)
                {
                    // Button field: a grid of bevelled keys, a few of them lit.
                    int bx = (int)((u - 0.06f) / 0.10f), by = (int)((v - 0.52f) / 0.11f);
                    float fx = (u - 0.06f) / 0.10f - bx, fy = (v - 0.52f) / 0.11f - by;
                    if (u > 0.06f && u < 0.94f && fx < 0.78f && fy < 0.72f)
                    {
                        float key = NfMath.Hash(bx, by, 977);
                        col = NfColor.Lerp(SteelBase, SteelDark, 0.2f);
                        if (fx < 0.08f || fy < 0.10f) col = NfColor.Lerp(col, SteelLight, 0.55f);
                        else if (fx > 0.70f || fy > 0.64f) col = col * 0.6f;
                        if (key > 0.82f) col = NfColor.Lerp(col, screenLit, 0.75f);
                        else if (key > 0.72f) col = NfColor.Lerp(col, Hazard, 0.7f);
                        else if (key > 0.66f) col = NfColor.Lerp(col, NfColor.FromBytes(0xD0, 0x30, 0x30), 0.7f);
                    }
                }
                else if (v >= 0.78f && v < 0.9f)
                {
                    // Speaker grille.
                    col = NfColor.Lerp(SteelDark, SteelBase, 0.3f);
                    if (Wrap(x, 6) < 3) col = col * 0.55f;
                }
                else if (v >= 0.9f) col = NfColor.Lerp(SteelDark, NfColor.Black, 0.35f);

                if (u < 0.02f || u > 0.98f) col = SteelDark * 0.7f;
                c.Put(x, y, col);
            }
        }
    }

    /// Supply crate. Drawn wider than tall in texels on purpose: low furniture is projected over
    /// LowHeight, so the texture is squeezed vertically by about 1,6 in the view.
    private static void BuildCrate(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.0f;
        var crateGreen = NfColor.FromBytes(0x4E, 0x6B, 0x4A);

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                float wear = PFbm(u, v, 10, 6, 4, 61) - 0.5f;
                var col = crateGreen * (1f + wear * 0.10f);

                // Boards across the body: a groove and a lit lip every eighth of the height. Without
                // them the crate is a green rectangle with a frame drawn on it.
                int bw = Wrap(y, 32);
                if (bw < 2) col = col * 0.6f;
                else if (bw == 2) col = NfColor.Lerp(col, SnowBase, 0.18f);
                else col = col * (1.04f - bw * 0.003f);

                // A recessed centre field bounded by raised ribs.
                bool rib = (u > 0.18f && u < 0.24f) || (u > 0.76f && u < 0.82f)
                           || (v > 0.44f && v < 0.56f);
                if (!rib && u > 0.24f && u < 0.76f) col = col * 0.88f;
                if (rib)
                {
                    col = NfColor.Lerp(col, crateGreen, 0.4f) * 1.12f;
                    bool lit = (u > 0.18f && u < 0.20f) || (u > 0.76f && u < 0.78f)
                               || (v > 0.44f && v < 0.47f);
                    col = NfColor.Lerp(col, lit ? SteelLight : SteelDark, lit ? 0.35f : 0.3f);
                }

                // Stencil block: three bars, the shape of a shipping mark without pretending to be
                // letters at a resolution that could not carry them.
                if (u > 0.32f && u < 0.66f && v > 0.16f && v < 0.36f)
                {
                    int bar = (int)((v - 0.16f) / 0.068f);
                    float len = 0.5f + NfMath.Hash(bar, 3, 1223) * 0.5f;
                    float fy = (v - 0.16f) / 0.068f - bar;
                    if (fy < 0.62f && u < 0.32f + 0.34f * len)
                        col = NfColor.Lerp(col, SnowBase, 0.72f - wear * 0.9f);
                }

                // Steel corner brackets with bolts. Taller than wide, to survive the squeeze.
                float cu = MathF.Min(u, 1f - u), cv = MathF.Min(v, 1f - v);
                if (cu < 0.09f || cv < 0.10f)
                {
                    col = NfColor.Lerp(SteelBase, SteelDark, 0.3f + wear * 0.4f);
                    bool lit = (u < 0.09f && u > 0.06f) || (v < 0.10f && v > 0.075f);
                    if (lit) col = NfColor.Lerp(col, SteelLight, 0.4f);
                    if (cu < 0.012f || cv < 0.014f) col = col * 0.55f;
                    if (cu < 0.09f && cv < 0.10f)
                    {
                        float bd = MathF.Sqrt((cu - 0.045f) * (cu - 0.045f) * 4f
                                              + (cv - 0.05f) * (cv - 0.05f) * 4f) * 22f;
                        if (bd < 3.2f) col = NfColor.Lerp(SteelDark, SteelLight, 0.75f - bd * 0.14f);
                    }
                }

                // A hazard corner, so the crate is not one colour from across a room.
                if (u > 0.86f && v > 0.62f && v < 0.78f)
                {
                    int band = Wrap((int)((u + v) * N) / 10, 2);
                    col = band == 0 ? Hazard * 0.9f : SteelDark;
                }
                c.Put(x, y, col);
            }
        }
    }

    /// Railings are mostly air. The transparency tells the renderer to keep going behind them, so
    /// you really do see the room through the bars.
    private static void BuildRailing(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.0f;
        s.Transparency = 0.62f;
        const int bay = 32;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int p = x % bay;
                var col = NfColor.Black;

                bool post = p < 7;
                bool topRail = v < 0.10f;
                bool midRail = v > 0.46f && v < 0.57f;
                // A diagonal brace in every bay: the difference between a railing and a comb.
                bool brace = MathF.Abs((p / (float)bay) - (1f - (v - 0.10f) / 0.36f)) < 0.09f
                             && v > 0.10f && v < 0.46f;

                if (post || topRail || midRail || brace)
                {
                    // Round bar: bright on the left/top, dark on the right/bottom.
                    float shade;
                    if (topRail) shade = 0.5f + Sat(1f - v / 0.10f) * 0.7f;
                    else if (midRail) shade = 0.5f + Sat(1f - (v - 0.46f) / 0.11f) * 0.7f;
                    else if (post) shade = 0.45f + Sat(1f - p / 7f) * 0.8f;
                    else shade = 0.85f;
                    col = SteelLight * (shade * 0.75f);
                    if (v > 0.9f && post) col = col * 0.6f;                 // foot
                }
                c.Put(x, y, col);
            }
        }
    }

    /// The hot side, near the lava river: a crust of cooled plates with the heat still showing
    /// through the joints. The glow lives in the cell borders rather than in a blob of noise, which
    /// is what makes it read as cracks instead of as fog.
    private static void BuildLavaRock(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 2.6f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                float wu = u + (PFbm(u, v, 3, 3, 3, 5) - 0.5f) * 0.11f;
                float wv = v + (PFbm(u, v, 3, 4, 3, 37) - 0.5f) * 0.08f;

                PWorley(wu, wv, 10, 8, 449, out float f1, out float f2, out float plate);
                var crust = NfColor.Lerp(RockBase * 0.30f, RockBase * 0.72f, plate);
                float grain = PFbm(u, v, 80, 64, 3, 157) - 0.5f;
                var col = crust * (1f + grain * 0.07f);

                // Plate edges lift slightly: a lit shoulder just outside the joint.
                float edge = f2 - f1;
                if (edge > 0.07f && edge < 0.13f) col = col * 1.18f;

                // The joints themselves, glowing hotter towards the floor where the lava runs.
                float heat = NfMath.SmoothStep(0.20f, 1f, v);
                float joint = Sat((0.075f - edge) * 16f);
                if (joint > 0f)
                {
                    float t = joint * joint * (0.35f + heat * 0.85f);
                    var hot = NfColor.Lerp(NfColor.FromBytes(0x8E, 0x1E, 0x10), LavaGlow, Sat(joint * 1.4f * (0.3f + heat)));
                    if (joint > 0.82f && heat > 0.55f)
                        hot = NfColor.Lerp(hot, NfColor.FromBytes(0xFF, 0xD8, 0x70), (joint - 0.82f) * 5f * heat);
                    col = NfColor.Lerp(col, hot, Sat(t));
                }

                // Hairline cracks over the plates, much dimmer.
                PWorley(wu * 2.3f, wv * 2.3f, 14, 11, 1051, out float h1, out float h2, out _);
                float hair = Sat((0.05f - (h2 - h1)) * 22f);
                col = NfColor.Lerp(col, NfColor.Lerp(RockBase * 0.2f, LavaGlow, heat * 0.55f), hair * 0.5f);

                // Ash settled on the up-facing plates.
                float ash = Sat(PFbm(u, v, 6, 5, 3, 691) * 2.2f - 1.1f) * (1f - heat) * 0.5f;
                col = NfColor.Lerp(col, NfColor.FromBytes(0x3A, 0x34, 0x3C), ash);
                c.Put(x, y, col);
            }
        }
    }

    /// Office desks and Dropship trim. Planks with real ends, grain that follows them, knots where
    /// the hash puts a branch.
    private static void BuildWood(Surface s, Canvas c)
    {
        int N = c.N;
        s.WorldScale = 1.5f;
        const int plank = 32;             // 15 cm boards

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int row = y / plank;
                int py = y % plank;

                // Boards are not endless: each row is broken into lengths, offset per row, and the
                // butt joint is a hard vertical line. Without it the floor is six long stripes.
                int seedShift = (int)(NfMath.Hash(0, row, 1451) * 200f);
                int seg = (x + seedShift) / 96;
                int segX = Wrap(x + seedShift, 96);
                float tone = NfMath.Hash(seg, row, 1471);

                float grain = PFbm(u + tone * 0.3f, v, 4, 34, 4, 17);
                float rings = Ridge(PFbm(u * 1.4f, v, 3, 26, 3, 23 + row));
                var col = WoodBase * (0.82f + tone * 0.26f);
                col = NfColor.Lerp(col, WoodBase * 0.62f, Sat(rings * 1.5f - 0.55f) * 0.55f);
                col = col * (1f + (grain - 0.5f) * 0.26f);

                // Hairline grain, dense along the board.
                float hair = PFbm(u, v, 3, 120, 2, 43);
                col = col * (1f + (hair - 0.5f) * 0.07f);

                // A knot, on some boards.
                if (tone > 0.55f)
                {
                    float kx = (segX - 30f - tone * 40f) / 13f;
                    float ky = (py - plank * 0.5f) / 9f;
                    float kd = MathF.Sqrt(kx * kx + ky * ky);
                    if (kd < 1.35f)
                    {
                        float ring = Ridge((kd * 3.4f) % 1f);
                        col = NfColor.Lerp(col, WoodBase * 0.45f, Sat(ring * 1.3f - 0.35f) * 0.7f);
                        if (kd < 0.42f) col = NfColor.Lerp(col, WoodBase * 0.3f, 0.85f);
                    }
                }

                // Board seams: a dark groove with a lit lip below it.
                if (py < 2) col = WoodBase * 0.4f;
                else if (py == 2) col = NfColor.Lerp(col, NfColor.White, 0.16f);
                else if (py > plank - 3) col = col * 0.82f;
                if (segX < 2) col = WoodBase * 0.45f;                              // butt joint

                // Nail heads at the board ends.
                if (segX > 5 && segX < 13 && py > 5 && py < 13)
                {
                    float nd = MathF.Sqrt((segX - 9) * (segX - 9) + (py - 9) * (py - 9));
                    if (nd < 2.6f) col = NfColor.Lerp(SteelDark, SteelLight, 0.6f - nd * 0.15f);
                }
                c.Put(x, y, col);
            }
        }
    }
}
