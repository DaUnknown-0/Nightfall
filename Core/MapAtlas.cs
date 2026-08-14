// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * MapAtlas - the map's own artwork, usable as a texture in world coordinates.
 *
 * WHY THIS EXISTS
 * ---------------
 * Procedural walls and floors can be made to look good, but they can never look like POLUS. The
 * lab's blue-white tiling, the office carpet, the yellow hazard stripes at Storage, the window
 * fronts, the stains and cable runs and the exact violet of the ground outside are hand-drawn art
 * that no amount of noise functions reproduces. If the brief is "the map, one to one", the only
 * honest source for those pixels is the map itself.
 *
 * So Nightfall photographs it. At the start of a round the plugin points an orthographic camera
 * straight down at the whole map, renders it once into a texture, and hands the result here as a
 * plain byte array plus the world rectangle it covers. From then on any world coordinate can be
 * turned into the colour Among Us itself draws there.
 *
 * Three things fall out of that, all of them the point:
 *   - nothing is embedded in the mod. No copied art, no asset bundle, no licence question: the
 *     pixels come from the player's own copy of the game, in memory, and are gone when the round
 *     ends.
 *   - it works on EVERY map, present and future, with no per-map work. Airship, Fungle, and
 *     anything Innersloth ships next are photographed the same way.
 *   - texture packs and mods that change the map's art are picked up automatically, because the
 *     photograph is taken of whatever is actually there.
 *
 * The atlas is a TOP-DOWN image, so it is exactly right for the floor and only an approximation for
 * walls: a wall is sampled slightly to its interior side to learn the colour of the room it belongs
 * to, and that colour then tints the procedurally structured wall surface. A drawn floor plan
 * simply does not contain what the side of a wall looks like.
 */

using System;

namespace Nightfall.Core;

public sealed class MapAtlas
{
    /// RGB, three bytes per pixel, row-major from the BOTTOM row up (the order a rendered texture
    /// arrives in, and the order world Y maps to naturally).
    public byte[] Pixels = Array.Empty<byte>();
    public int Width, Height;

    /// World rectangle the image covers.
    public NfVec2 Min, Max;

    public bool IsValid => Pixels.Length >= 3 && Width > 0 && Height > 0
                           && Max.X > Min.X && Max.Y > Min.Y;

    public float PixelsPerUnit => Width / MathF.Max(0.001f, Max.X - Min.X);

    public void Set(byte[] rgb, int width, int height, NfVec2 min, NfVec2 max)
    {
        Pixels = rgb;
        Width = width;
        Height = height;
        Min = min;
        Max = max;
    }

    public void Clear()
    {
        Pixels = Array.Empty<byte>();
        Width = Height = 0;
    }

    /// Nearest-neighbour sample at a world position. Used for the floor, where the caller is
    /// already walking neighbouring pixels and the extra filtering would cost more than it shows.
    public bool Sample(float wx, float wy, out float r, out float g, out float b)
    {
        r = g = b = 0f;
        if (!IsValid) return false;

        float u = (wx - Min.X) / (Max.X - Min.X);
        float v = (wy - Min.Y) / (Max.Y - Min.Y);
        if (u < 0f || u >= 1f || v < 0f || v >= 1f) return false;

        int x = (int)(u * Width);
        int y = (int)(v * Height);
        int o = (y * Width + x) * 3;
        r = Pixels[o] / 255f;
        g = Pixels[o + 1] / 255f;
        b = Pixels[o + 2] / 255f;
        return true;
    }

    /// Bilinear sample. Used where the image is magnified hard - the floor immediately in front of
    /// the player, where a nearest sample turns into visible tiles of flat colour.
    public bool SampleBilinear(float wx, float wy, out float r, out float g, out float b)
    {
        r = g = b = 0f;
        if (!IsValid) return false;

        float u = (wx - Min.X) / (Max.X - Min.X) * Width - 0.5f;
        float v = (wy - Min.Y) / (Max.Y - Min.Y) * Height - 0.5f;

        int x0 = (int)MathF.Floor(u), y0 = (int)MathF.Floor(v);
        float fx = u - x0, fy = v - y0;

        if (x0 < 0 || y0 < 0 || x0 + 1 >= Width || y0 + 1 >= Height)
            return Sample(wx, wy, out r, out g, out b);

        int o00 = (y0 * Width + x0) * 3;
        int o10 = o00 + 3;
        int o01 = o00 + Width * 3;
        int o11 = o01 + 3;

        for (int c = 0; c < 3; c++)
        {
            float top = Pixels[o00 + c] + (Pixels[o10 + c] - Pixels[o00 + c]) * fx;
            float bot = Pixels[o01 + c] + (Pixels[o11 + c] - Pixels[o01 + c]) * fx;
            float val = (top + (bot - top) * fy) / 255f;
            if (c == 0) r = val; else if (c == 1) g = val; else b = val;
        }
        return true;
    }

    /// The MEDIAN colour over a rectangle, which is what "what colour is this surface" actually
    /// means on a hand-drawn map.
    ///
    /// An average is dragged around by whatever happens to be drawn on top: a mean over a floor
    /// tile that contains half a console comes out as neither floor nor console. The median lands
    /// on the colour that covers most of the area, which is the surface itself.
    public bool SampleMedian(float x0, float y0, float x1, float y1,
                             out float r, out float g, out float b)
    {
        r = g = b = 0f;
        if (!IsValid) return false;

        int px0 = PxX(x0), px1 = PxX(x1), py0 = PxY(y0), py1 = PxY(y1);
        if (px1 < px0) (px0, px1) = (px1, px0);
        if (py1 < py0) (py0, py1) = (py1, py0);

        // At most a few hundred samples: this runs once per surface when the model is built, and
        // sampling every pixel of a large room would be pointlessly slow.
        int stepX = Math.Max(1, (px1 - px0) / 16);
        int stepY = Math.Max(1, (py1 - py0) / 16);

        Span<float> lum = stackalloc float[289];
        Span<int> off = stackalloc int[289];
        int n = 0;

        for (int y = py0; y <= py1 && n < 289; y += stepY)
        {
            if (y < 0 || y >= Height) continue;
            for (int x = px0; x <= px1 && n < 289; x += stepX)
            {
                if (x < 0 || x >= Width) continue;
                int o = (y * Width + x) * 3;
                lum[n] = Pixels[o] * 0.3f + Pixels[o + 1] * 0.6f + Pixels[o + 2] * 0.1f;
                off[n] = o;
                n++;
            }
        }
        if (n == 0) return false;

        // Partial selection sort up to the middle: n is small and this avoids allocating.
        int mid = n / 2;
        for (int i = 0; i <= mid; i++)
        {
            int best = i;
            for (int j = i + 1; j < n; j++) if (lum[j] < lum[best]) best = j;
            (lum[i], lum[best]) = (lum[best], lum[i]);
            (off[i], off[best]) = (off[best], off[i]);
        }

        int m = off[mid];
        r = Pixels[m] / 255f;
        g = Pixels[m + 1] / 255f;
        b = Pixels[m + 2] / 255f;
        return true;
    }

    private int PxX(float wx) => (int)((wx - Min.X) / (Max.X - Min.X) * Width);
    private int PxY(float wy) => (int)((wy - Min.Y) / (Max.Y - Min.Y) * Height);

    /// Average colour over a small disc, used to give a wall segment the colour of the room it
    /// belongs to. A single sample would land on whatever prop happened to be drawn at that exact
    /// spot; a small average finds the surface.
    public bool SampleArea(float wx, float wy, float radiusUnits,
                           out float r, out float g, out float b)
    {
        r = g = b = 0f;
        if (!IsValid) return false;

        float ppu = PixelsPerUnit;
        int rad = Math.Max(1, (int)(radiusUnits * ppu));
        int cx = (int)((wx - Min.X) / (Max.X - Min.X) * Width);
        int cy = (int)((wy - Min.Y) / (Max.Y - Min.Y) * Height);

        float sr = 0f, sg = 0f, sb = 0f;
        int n = 0;
        int step = Math.Max(1, rad / 3);
        for (int y = cy - rad; y <= cy + rad; y += step)
        {
            if (y < 0 || y >= Height) continue;
            for (int x = cx - rad; x <= cx + rad; x += step)
            {
                if (x < 0 || x >= Width) continue;
                int o = (y * Width + x) * 3;
                sr += Pixels[o];
                sg += Pixels[o + 1];
                sb += Pixels[o + 2];
                n++;
            }
        }
        if (n == 0) return false;
        r = sr / n / 255f;
        g = sg / n / 255f;
        b = sb / n / 255f;
        return true;
    }
}
