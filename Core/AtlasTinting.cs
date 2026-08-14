// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * AtlasTinting - gives every wall the colour of the room it actually belongs to.
 *
 * The map photograph (MapAtlas) is a top-down image, so it says nothing about what the SIDE of a
 * wall looks like. What it does say, precisely, is what the floor immediately next to that wall
 * looks like, and in a game whose art has one colour scheme per room, that is enough: the wall
 * around the Laboratory should be the Laboratory's white-blue, the wall around Office its warm
 * brown, and the rock outside its violet.
 *
 * So every segment is sampled on BOTH sides, a short step along its normal, and the side that looks
 * more like a room (brighter and more saturated than bare Polus ground) wins. The resulting colour
 * then tints a procedurally structured surface, which supplies the panel lines, rivets and grime
 * that a floor plan can never contain. Real colours, invented relief.
 *
 * Runs once, when the model is built. Nothing here happens per frame.
 */

using System;

namespace Nightfall.Core;

public static class AtlasTinting
{
    /// How far from the wall the room colour is read. Half a unit is inside the room but clear of
    /// the wall's own dark outline in the artwork.
    private const float ProbeDistance = 0.55f;
    private const float ProbeRadius = 0.30f;

    public static void Apply(MapModel model)
    {
        var atlas = model.Atlas;
        if (atlas == null || !atlas.IsValid) return;

        var segs = model.Geometry.Segments;
        int tinted = 0;

        for (int i = 0; i < segs.Length; i++)
        {
            ref var s = ref segs[i];
            if (s.Length < 1e-4f) continue;

            var mid = new NfVec2((s.A.X + s.B.X) * 0.5f, (s.A.Y + s.B.Y) * 0.5f);
            // Unit normal of the segment.
            var n = new NfVec2(-s.Dir.Y / s.Length, s.Dir.X / s.Length);

            bool okA = atlas.SampleArea(mid.X + n.X * ProbeDistance, mid.Y + n.Y * ProbeDistance,
                                        ProbeRadius, out float ar, out float ag, out float ab);
            bool okB = atlas.SampleArea(mid.X - n.X * ProbeDistance, mid.Y - n.Y * ProbeDistance,
                                        ProbeRadius, out float br, out float bg, out float bb);

            float r, g, b;
            if (okA && okB)
            {
                // Prefer the more "built" side: interiors in Among Us are brighter and less violet
                // than the planet surface, so brightness plus distance from the ground hue is a
                // reliable discriminator without knowing anything about the map.
                float scoreA = Score(ar, ag, ab);
                float scoreB = Score(br, bg, bb);
                if (scoreA >= scoreB) { r = ar; g = ag; b = ab; }
                else { r = br; g = bg; b = bb; }
            }
            else if (okA) { r = ar; g = ag; b = ab; }
            else if (okB) { r = br; g = bg; b = bb; }
            else continue;

            // Walls read a touch darker than the floor they enclose: the photograph is lit from
            // straight above, a wall never is.
            const float wallDarken = 0.86f;
            s.TintR = NfMath.ToByte(r * wallDarken);
            s.TintG = NfMath.ToByte(g * wallDarken);
            s.TintB = NfMath.ToByte(b * wallDarken);
            s.HasTint = true;
            tinted++;
        }
    }

    /// Higher means "more likely to be the inside of a room". Brightness dominates; the violet cast
    /// of Polus' ground (blue and red high, green low) is penalised.
    private static float Score(float r, float g, float b)
    {
        float lum = r * 0.3f + g * 0.6f + b * 0.1f;
        float violet = MathF.Max(0f, (r + b) * 0.5f - g);
        return lum - violet * 0.8f;
    }
}

/// Mean brightness of each procedural surface, computed lazily and cached.
///
/// It exists so a surface can be used as pure RELIEF: dividing a texel by the surface's own mean
/// gives a number around 1.0 that says "brighter or darker than this material usually is", and
/// multiplying that into the room colour from the photograph keeps the panel lines and rivets while
/// throwing the invented colour away. Lives outside TextureBank on purpose, so the two can be
/// worked on independently.
public static class SurfaceStats
{
    private static float[] means;

    public static float Mean(Surface s)
    {
        if (s == null) return 1f;
        EnsureBuilt();
        int i = (int)s.Kind;
        return i >= 0 && i < means.Length ? means[i] : 1f;
    }

    public static void Invalidate() => means = null;

    private static void EnsureBuilt()
    {
        if (means != null) return;
        TextureBank.EnsureBuilt();
        var all = TextureBank.All;
        var m = new float[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            var px = all[i].Pixels;
            if (px == null || px.Length == 0) { m[i] = 1f; continue; }
            double sum = 0;
            // Every 33rd texel: the mean of a texture does not need every pixel, and this keeps the
            // first frame after a map load cheap.
            int step = 3 * 11;
            int n = 0;
            for (int o = 0; o + 2 < px.Length; o += step)
            {
                sum += px[o] * 0.3f + px[o + 1] * 0.6f + px[o + 2] * 0.1f;
                n++;
            }
            m[i] = n > 0 ? MathF.Max(0.04f, (float)(sum / n)) : 1f;
        }
        means = m;
    }
}
