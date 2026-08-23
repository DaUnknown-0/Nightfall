// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * HandLight - everything drawn in SCREEN space after the world: the torch the player is holding,
 * the beast's forepaws, its red night vision, and the vignette.
 *
 * It sits in its own file because BOTH renderers need it and neither owns it. The raycaster grew
 * these four; when the triangle renderer replaced it they were simply left behind, and the first
 * playtest is a set of screenshots in which the player carries no torch at all - a first-person
 * view with nothing in frame reads as a camera, not as a person. Nothing here knows about geometry:
 * give it a pixel buffer and a view and it draws on top.
 */

using System;

namespace Nightfall.Core;

public static class HandLight
{
    /// Whether the two full-screen passes below may spread across cores (AUDIT-2026-08-23, L-27).
    ///
    /// Both renderers have their own `Multithreaded` field and honour it everywhere except here:
    /// these passes are static helpers and called Parallel.For unconditionally, so switching
    /// threading off for debugging or on a single-core machine left two full-screen loops still
    /// running parallel - exactly the case where a reproducible frame matters most. The renderers
    /// now mirror their own flag into this one before drawing.
    public static bool Multithreaded = true;

    /// Runs `body` over 0..count, honouring Multithreaded. Small counts stay serial regardless -
    /// the same threshold FrameRenderer.RunParallel uses, for the same reason.
    private static void ForRows(int count, System.Action<int> body)
    {
        if (!Multithreaded || count < 64)
        {
            for (int i = 0; i < count; i++) body(i);
            return;
        }
        System.Threading.Tasks.Parallel.For(0, count, body);
    }

    // ================================================================================
    /// The flashlight body at the bottom right, drawn in screen space because it is held, not
    /// placed. It swings slightly with the beam, which is what tells the player that the mouse
    /// controls the light and not the head.
    public static void Draw(byte[] Pixels, int Width, int Height, in ViewParams view)
    {
        if (view.PredatorVision) { DrawClaws(Pixels, Width, Height, view); return; }
        if (view.FlashlightPower <= 0.01f) return;

        // How far the beam is aimed away from where the body faces, as a fraction of half the field
        // of view. The torch leans that way, which is the cue that tells a player the mouse aims
        // the light and not the head.
        float sway = NfMath.WrapAngle(view.FlashlightDir - view.Heading) / MathF.Max(0.01f, view.Fov * 0.5f);
        sway = NfMath.Clamp(sway, -1.3f, 1.3f);

        // Held low and to the right, angled up and inward: the pose you hold a torch in when you
        // are walking, not the pose you hold it in when you are pointing at something.
        float baseX = Width * (0.855f + sway * 0.085f);
        float baseY = Height * 1.06f;
        float tipX = baseX - Width * (0.070f + sway * 0.050f);
        float tipY = Height * 0.585f;

        float len = baseY - tipY;
        float wid = Height * 0.044f;

        var bodyDark = new NfColor(0.07f, 0.075f, 0.09f);
        var bodyMid = new NfColor(0.20f, 0.22f, 0.26f);
        var bodyLit = new NfColor(0.55f, 0.58f, 0.64f);
        var ring = new NfColor(0.34f, 0.30f, 0.22f);
        var lensHot = new NfColor(1.0f, 0.96f, 0.80f);
        var lensRim = new NfColor(0.85f, 0.72f, 0.42f);

        float flicker = 0.9f + 0.1f * MathF.Sin(view.Time * 17.3f) * MathF.Sin(view.Time * 5.1f);
        float power = view.FlashlightPower * flicker;

        // Starting the loop AT the tip matters: t is clamped, so any row above it would keep
        // evaluating as t = 1 and smear the lens upwards into the scene.
        for (int y = (int)tipY; y < Height; y++)
        {
            float t = NfMath.Clamp01((baseY - y) / len);         // 0 at the grip, 1 at the lens
            float cx = baseX + (tipX - baseX) * t;

            // Slim barrel, a step up to the reflector head near the top.
            float halfW = wid * (0.86f + 0.10f * t);
            if (t > 0.80f) halfW = wid * (1.02f + 0.62f * NfMath.SmoothStep(0.80f, 0.93f, t));

            int x0 = (int)(cx - halfW) - 1, x1 = (int)(cx + halfW) + 1;
            for (int x = Math.Max(0, x0); x <= Math.Min(Width - 1, x1); x++)
            {
                float across = (x - cx) / MathF.Max(1e-3f, halfW);
                float aa = MathF.Abs(across);
                if (aa > 1.15f) continue;

                // Cylinder shading: a bright band left of centre, falling off to both edges.
                float shade = NfMath.Clamp01(1f - MathF.Abs(across + 0.35f) * 1.15f);
                var c = NfColor.Lerp(bodyDark, bodyMid, shade);
                c = NfColor.Lerp(c, bodyLit, shade * shade * 0.8f);

                if (t > 0.955f)
                {
                    // The lens: hot in the middle, rimmed, and brighter the stronger the beam.
                    float r = NfMath.Clamp01(aa / 0.9f);
                    c = NfColor.Lerp(lensHot, lensRim, r * r);
                    c = c * (0.45f + 0.55f * power);
                }
                else if (t > 0.90f && t <= 0.955f)
                {
                    c = NfColor.Lerp(bodyDark, ring, 0.7f);      // bezel around the lens
                }
                else if (t is > 0.26f and < 0.34f || t is > 0.40f and < 0.46f)
                {
                    c = c * 0.55f;                                // grip ridges
                }

                int o = (y * Width + x) * 4;
                var dst = new NfColor(Pixels[o] / 255f, Pixels[o + 1] / 255f, Pixels[o + 2] / 255f);

                // A soft halo of spilled light around the head, added rather than blended, so the
                // torch looks like it is emitting instead of being pasted on.
                if (t > 0.86f)
                {
                    float halo = NfMath.SmoothStep(1.15f, 0.55f, aa)
                                 * NfMath.SmoothStep(0.86f, 1.0f, t) * power * 0.35f;
                    dst = dst + lensHot * halo;
                }

                float edge = NfMath.SmoothStep(1.10f, 0.92f, aa);
                NfColor.Lerp(dst, c, edge).ToBytes(Pixels, o);
            }
        }
    }

    /// What the werewolf holds instead of a torch: its own forepaws, one at each lower corner,
    /// rising and falling with its gait.
    private static void DrawClaws(byte[] Pixels, int Width, int Height, in ViewParams view)
    {
        var fur = new NfColor(0.11f, 0.07f, 0.06f);
        var furLit = new NfColor(0.30f, 0.18f, 0.14f);
        var claw = new NfColor(0.88f, 0.86f, 0.79f);
        var clawDark = new NfColor(0.45f, 0.42f, 0.38f);

        for (int side = 0; side < 2; side++)
        {
            // The two paws swing out of phase, which reads as walking rather than floating.
            float bob = MathF.Sin(view.Time * 3.4f + side * NfMath.Pi) * Height * 0.035f;
            float cx = side == 0 ? Width * 0.13f : Width * 0.87f;
            float top = Height * 0.70f + bob;
            float lean = side == 0 ? 1f : -1f;      // paws angle inwards towards the centre

            for (int y = (int)top; y < Height; y++)
            {
                float t = NfMath.Clamp01((y - top) / MathF.Max(1f, Height - top));
                // Narrow at the knuckles, broad at the wrist.
                float halfW = Width * (0.075f + 0.075f * t);
                float centre = cx + lean * Width * 0.035f * (1f - t);

                for (int x = (int)(centre - halfW) - 1; x <= (int)(centre + halfW) + 1; x++)
                {
                    if (x < 0 || x >= Width) continue;
                    float across = (x - centre) / halfW;
                    float aa = MathF.Abs(across);
                    if (aa > 1.08f) continue;

                    // Rounded fur volume, lit from the inner edge.
                    float shade = NfMath.Clamp01(1f - MathF.Abs(across + lean * 0.3f) * 1.05f);
                    var c = NfColor.Lerp(fur, furLit, shade * shade);

                    // Three claws curling over the knuckles at the top.
                    if (t < 0.22f)
                    {
                        float phase = ((across + 1f) * 1.5f) % 1f;
                        float clawShape = NfMath.SmoothStep(0.42f, 0.12f, MathF.Abs(phase - 0.5f));
                        float reach = NfMath.SmoothStep(0.22f, 0.02f, t);
                        float k = clawShape * reach;
                        if (k > 0.05f) c = NfColor.Lerp(c, NfColor.Lerp(clawDark, claw, k), k);
                    }

                    int o = (y * Width + x) * 4;
                    float edge = NfMath.SmoothStep(1.06f, 0.90f, aa);
                    var dst = new NfColor(Pixels[o] / 255f, Pixels[o + 1] / 255f, Pixels[o + 2] / 255f);
                    NfColor.Lerp(dst, c, edge).ToBytes(Pixels, o);
                }
            }
        }
    }

    /// Darkens the corners. Cheap, and it does more for the feeling of a torch in a black corridor
    /// than any amount of texture detail.
    public static void Vignette(byte[] Pixels, int Width, int Height)
    {
        float cx = Width * 0.5f, cy = Height * 0.5f;
        float maxR = MathF.Sqrt(cx * cx + cy * cy);
        float k = maxR / MathF.Min(cx, cy);

        // Across cores and without the square root. The darkening is a smoothstep of the radius,
        // and squaring both of its edges gives the identical curve in r squared - which costs a
        // multiply instead of a sqrt on every pixel of the screen. Run as one serial loop it was
        // measurably more expensive than the entire sky behind it.
        const float e0 = 0.75f, e1 = 1.65f;
        float q0 = e0 * e0, q1 = e1 * e1;
        float inv = 1f / (q1 - q0);

        ForRows(Height, y =>
        {
            float dy = (y - cy) / maxR;
            float dy2 = dy * dy;
            int rowBase = y * Width;
            for (int x = 0; x < Width; x++)
            {
                float dx = (x - cx) / maxR;
                float r2 = (dx * dx + dy2) * k * k;
                if (r2 <= q0) continue;
                float t = NfMath.Clamp01((r2 - q0) * inv);
                float f = 1f - t * t * (3f - 2f * t) * 0.85f;
                int o = (rowBase + x) * 4;
                Pixels[o] = (byte)(Pixels[o] * f);
                Pixels[o + 1] = (byte)(Pixels[o + 1] * f);
                Pixels[o + 2] = (byte)(Pixels[o + 2] * f);
            }
        });
    }

    /// The beast does not see the world in colour. Luminance, pushed towards blood red, with the
    /// deep shadows kept blue-black so the picture does not turn into a single flat wash.
    public static void PredatorTint(byte[] Pixels)
    {
        var hot = new NfColor(1.15f, 0.30f, 0.22f);
        var cold = new NfColor(0.10f, 0.09f, 0.16f);

        ForRows(Pixels.Length / 4, j =>
        {
            int i = j * 4;
            float r = Pixels[i] / 255f, g = Pixels[i + 1] / 255f, b = Pixels[i + 2] / 255f;
            float lum = r * 0.299f + g * 0.587f + b * 0.114f;
            // Lift the mid-shadows before mapping (a parabola, no pow): the beast's problem was
            // never the bright middle of the picture but the murk around it, where the ramp
            // squashed everything to the same near-black and the room stopped reading.
            lum *= 1.55f - 0.55f * lum;
            var c = NfColor.Lerp(cold, hot, NfMath.Clamp01(lum * 1.15f));
            // A little of the original survives, so player colours are still just about readable:
            // the beast has to be able to tell its victims apart.
            c = NfColor.Lerp(c, new NfColor(r, g, b), 0.18f);
            c.ToBytes(Pixels, i);
        });
    }
}
