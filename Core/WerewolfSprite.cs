// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * WerewolfSprite - the beast, from eight directions.
 *
 * Built the same parametric way as the crewmate, because the beast has to read as the SAME KIND of
 * thing seen from the same eight angles: it is standing in the same corridor, at the same scale,
 * under the same torch. What separates it is deliberately not detail but silhouette, because
 * silhouette is all that survives at twelve pixels tall in the dark:
 *
 *   - it is half again as tall as a crewmate and noticeably broader,
 *   - it has ears, which nothing else in Among Us has, and they are the first thing that clears
 *     the top of a crate or a console when it comes around a corner,
 *   - it has EYES rather than a visor, and they are emissive: they hold their brightness when
 *     everything else falls into the dark, so what a victim sees first is two points of light at
 *     the wrong height. The eyes vanish when it turns away, which is the only mercy in the design.
 *
 * The player colour mask still exists but is fed with fur tones instead, so the same renderer path
 * draws it with no special cases.
 */

using System;

namespace Nightfall.Core;

public sealed class WerewolfSprite : IBillboardSource
{
    public const int Frames = 8;
    public const int TexW = 64;
    public const int TexH = 72;

    /// Stride 6: r, g, b, colourMask, shadowMask, alpha.
    private readonly float[] data = new float[Frames * TexW * TexH * 6];

    public int Width => TexW;
    public int Height => TexH;

    /// Fur tones, handed to the renderer as the "player colour" so no special case is needed.
    public static readonly NfColor Fur = new(0.155f, 0.105f, 0.095f);
    public static readonly NfColor FurShadow = new(0.055f, 0.035f, 0.040f);

    private static readonly NfColor Outline = new(0.02f, 0.015f, 0.02f);
    private static readonly NfColor EyeCore = new(1.0f, 0.86f, 0.30f);
    private static readonly NfColor EyeRim = new(0.95f, 0.28f, 0.10f);
    private static readonly NfColor Fang = new(0.92f, 0.90f, 0.84f);

    public WerewolfSprite()
    {
        for (int f = 0; f < Frames; f++) BuildFrame(f);
    }

    public int FrameForAngle(float relativeAngle)
    {
        float t = (relativeAngle + NfMath.Pi) / NfMath.TwoPi;
        int f = (int)MathF.Round(t * Frames) % Frames;
        if (f < 0) f += Frames;
        return f;
    }

    public bool Sample(int frame, int x, int y, out NfColor color, out float colorMaskWeight,
                       out float shadowMaskWeight)
    {
        color = default; colorMaskWeight = 0f; shadowMaskWeight = 0f;
        if (frame < 0 || frame >= Frames || x < 0 || y < 0 || x >= TexW || y >= TexH) return false;

        int o = ((frame * TexH + y) * TexW + x) * 6;
        if (data[o + 5] < 0.5f) return false;

        color = new NfColor(data[o], data[o + 1], data[o + 2]);
        colorMaskWeight = data[o + 3];
        shadowMaskWeight = data[o + 4];
        return true;
    }

    // ================================================================================
    private void BuildFrame(int frame)
    {
        float a = (frame / (float)Frames) * NfMath.TwoPi - NfMath.Pi;
        float sin = MathF.Sin(a), cos = MathF.Cos(a);
        float facing = NfMath.Clamp01((cos + 0.25f) / 1.25f);      // 1 = looking at us
        float widthScale = 0.88f + 0.12f * MathF.Abs(cos);

        for (int y = 0; y < TexH; y++)
        {
            float ny = (y + 0.5f) / TexH;
            for (int x = 0; x < TexW; x++)
            {
                float nx = ((x + 0.5f) / TexW) * 2f - 1f;
                float sx = nx / widthScale;

                float bodyD = BodyDistance(sx, ny);
                float earD = EarDistance(sx, ny, sin, facing);
                float legD = LegDistance(sx, ny);
                float armD = ArmDistance(sx, ny, sin, cos);

                float d = MathF.Min(MathF.Min(bodyD, earD), MathF.Min(legD, armD));
                if (d > 1f) continue;

                int o = ((frame * TexH + y) * TexW + x) * 6;

                if (d > 0.88f) { Write(o, Outline, 0f, 0f); continue; }

                // ---- eyes: the whole point of the design ----
                float eye = EyeWeight(sx, ny, sin, facing);
                if (eye > 0f)
                {
                    // Written as a plain colour with no mask, so the lighting multiplies it but the
                    // fur tint never touches it. Bright enough to survive the darkest corner.
                    var c = NfColor.Lerp(EyeRim, EyeCore, eye);
                    Write(o, c * (1.6f + eye * 1.4f), 0f, 0f);
                    continue;
                }

                // ---- muzzle and fangs, only when it is facing us ----
                if (facing > 0.45f)
                {
                    float fang = FangWeight(sx, ny, sin, facing);
                    if (fang > 0f) { Write(o, Fang * (0.55f + 0.45f * fang), 0f, 0f); continue; }
                }

                bool onEar = earD <= bodyD && earD <= legD && earD <= armD;
                bool onLeg = legD < bodyD && legD < earD && legD < armD;
                bool onArm = armD < bodyD && armD < earD && armD < legD;

                // Fur shading: shaggier than the crewmate's smooth shell, so the two silhouettes do
                // not read as the same material at a distance.
                float shag = NfMath.Fbm(sx * 9f, ny * 16f, 3, 77) - 0.5f;
                float vertical = NfMath.SmoothStep(0.10f, 0.98f, ny);
                float shadow = NfMath.Clamp01(vertical * 0.6f + shag * 0.35f + 0.1f);

                if (onEar) shadow = NfMath.Clamp01(shadow * 0.7f);
                if (onLeg) shadow = NfMath.Clamp01(shadow * 0.5f + 0.5f);
                if (onArm) shadow = NfMath.Clamp01(shadow * 0.7f + 0.28f);

                float hump = NfMath.SmoothStep(0.75f, 0.18f,
                    MathF.Sqrt((sx + 0.10f) * (sx + 0.10f) * 1.3f + (ny - 0.24f) * (ny - 0.24f) * 4.5f));

                Write(o, NfColor.White * (0.05f + hump * 0.30f), 1f - shadow * 0.5f, shadow);
            }
        }
    }

    private void Write(int offset, NfColor c, float colourMask, float shadowMask)
    {
        data[offset] = c.R;
        data[offset + 1] = c.G;
        data[offset + 2] = c.B;
        data[offset + 3] = NfMath.Clamp01(colourMask);
        data[offset + 4] = NfMath.Clamp01(shadowMask);
        data[offset + 5] = 1f;
    }

    // ================================================================================
    // Shape
    // ================================================================================
    /// Hunched: narrow at the shoulders, heavy through the chest, the head carried low and forward.
    private static float BodyDistance(float nx, float ny)
    {
        const float cx = 0f, cy = 0.46f;
        const float rx = 0.52f, ry = 0.40f;
        float dx = (nx - cx) / rx, dy = (ny - cy) / ry;
        float e = MathF.Pow(MathF.Abs(dx), 2.3f) + MathF.Pow(MathF.Abs(dy), 2.1f);
        float body = MathF.Pow(e, 1f / 2.2f);

        // The skull, sunk into the shoulders.
        float hx = (nx - 0f) / 0.34f, hy = (ny - 0.20f) / 0.20f;
        float head = MathF.Pow(MathF.Pow(MathF.Abs(hx), 2.2f) + MathF.Pow(MathF.Abs(hy), 2.2f), 1f / 2.2f);

        return MathF.Min(body, head);
    }

    /// Two pointed ears. They swing with the viewing angle and stay visible from behind, which is
    /// what makes the beast recognisable even when it is walking away.
    private static float EarDistance(float nx, float ny, float sin, float facing)
    {
        if (ny > 0.28f) return 99f;
        float best = 99f;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float cx = side * 0.25f + sin * 0.06f;
            float dx = (nx - cx) / 0.115f;
            float dy = (ny - 0.115f) / 0.135f;
            // Sharper exponent than the body: ears are triangles, not blobs.
            float e = MathF.Pow(MathF.Abs(dx), 1.35f) + MathF.Pow(MathF.Abs(dy), 1.35f);
            best = MathF.Min(best, MathF.Pow(e, 1f / 1.35f));
        }
        return best;
    }

    private static float LegDistance(float nx, float ny)
    {
        if (ny < 0.76f) return 99f;
        float best = 99f;
        for (int i = 0; i < 2; i++)
        {
            float cx = i == 0 ? -0.25f : 0.25f;
            float dx = (nx - cx) / 0.20f;
            float dy = (ny - 0.885f) / 0.135f;
            float e = MathF.Pow(MathF.Abs(dx), 2.4f) + MathF.Pow(MathF.Abs(dy), 2.4f);
            best = MathF.Min(best, MathF.Pow(e, 1f / 2.4f));
        }
        return best;
    }

    /// One long forelimb hanging at the side it is turned towards. Hidden head-on, where it would
    /// only widen the silhouette into a blob.
    private static float ArmDistance(float nx, float ny, float sin, float cos)
    {
        float visible = NfMath.Clamp01(MathF.Abs(sin) * 1.2f);
        if (visible < 0.15f) return 99f;

        float side = sin > 0f ? 1f : -1f;
        float cx = side * (0.46f + 0.06f * visible);
        float dx = (nx - cx) / (0.15f * visible);
        float dy = (ny - 0.60f) / 0.30f;
        if (0.15f * visible < 0.02f) return 99f;
        float e = MathF.Pow(MathF.Abs(dx), 2.0f) + MathF.Pow(MathF.Abs(dy), 2.2f);
        return MathF.Pow(e, 1f / 2.1f);
    }

    private static float EyeWeight(float nx, float ny, float sin, float facing)
    {
        if (facing <= 0.12f) return 0f;                 // turned away: no eyes, no warning
        float best = 0f;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            // The eyes converge towards the near side as the head turns.
            float cx = side * 0.135f * (0.45f + 0.55f * facing) + sin * 0.13f;
            float rx = 0.075f * (0.5f + 0.5f * facing);
            if (rx < 0.012f) continue;
            float dx = (nx - cx) / rx, dy = (ny - 0.205f) / 0.055f;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < 1f) best = MathF.Max(best, 1f - d);
        }
        return best;
    }

    private static float FangWeight(float nx, float ny, float sin, float facing)
    {
        float cx = sin * 0.10f;
        float dx = (nx - cx) / 0.16f, dy = (ny - 0.315f) / 0.045f;
        if (MathF.Abs(dx) > 1f || MathF.Abs(dy) > 1f) return 0f;
        // Four teeth across the muzzle.
        float phase = MathF.Abs(((dx + 1f) * 2f) % 1f - 0.5f);
        float tooth = NfMath.SmoothStep(0.34f, 0.10f, phase);
        float row = NfMath.SmoothStep(1f, 0.2f, MathF.Abs(dy));
        return tooth * row * facing;
    }
}
