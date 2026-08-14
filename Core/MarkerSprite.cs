// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * THE OBJECTIVE PIN - the in-world replacement for every flat screen arrow.
 *
 * Among Us and The Other Roles both point at things with a 2D arrow glued to the lens
 * (ArrowBehaviour, layer UI): tasks, sabotages, the tracker. In a first-person picture a sticker
 * on the lens breaks the perspective the same way the leaking vanilla sprites did, so the arrows
 * are taken off the screen and their information is put INTO the world instead: a small glowing
 * pin that hovers in the direction of the target, tinted with the arrow's own colour (yellow for
 * a task, red for a sabotage). It is emissive - a marker is game information the player is
 * entitled to, not a person that must vanish outside the torch cone.
 *
 * The shape is the universal objective pin: a round head on a downward point. It reads at twelve
 * pixels tall, which a rotated arrow glyph does not.
 */

using System;

namespace Nightfall.Core;

public sealed class MarkerSprite : IBillboardSource
{
    private const int W = 40, H = 56;
    /// 0 = transparent, 1 = outline, 2 = fill (takes the tint via the colour mask), 3 = highlight.
    private readonly byte[] cells = new byte[W * H];

    public int Width => W;
    public int Height => H;
    public int FrameForAngle(float relativeAngle) => 0;   // a pin looks the same from everywhere

    public MarkerSprite()
    {
        // Head: a disc. Point: the triangle from the disc's flanks down to the tip.
        const float cxF = W * 0.5f, cyF = 17f, r = 13f;
        const float tipY = H - 2f;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float dx = x + 0.5f - cxF, dy = y + 0.5f - cyF;
                float dDisc = MathF.Sqrt(dx * dx + dy * dy) - r;

                // The point, as a signed distance: a triangle that narrows linearly to the tip.
                float t = (y + 0.5f - cyF) / (tipY - cyF);
                float halfWidth = (1f - t) * (r - 1.5f);
                float dPoint = (t < 0f || t > 1f) ? 999f : MathF.Abs(dx) - MathF.Max(0.8f, halfWidth);

                float d = MathF.Min(dDisc, dPoint);
                if (d > 0f) continue;
                cells[y * W + x] = d > -2.2f ? (byte)1 : (byte)2;
            }
        }

        // A small fixed highlight on the head's upper left, so the pin reads as a lit object
        // rather than a flat decal.
        for (int y = 8; y < 15; y++)
            for (int x = 12; x < 19; x++)
            {
                float dx = x - 15f, dy = y - 11f;
                if (dx * dx + dy * dy < 9f && cells[y * W + x] == 2) cells[y * W + x] = 3;
            }
    }

    public bool Sample(int frame, int x, int y, out NfColor color, out float colorMaskWeight,
                       out float shadowMaskWeight)
    {
        color = default; colorMaskWeight = 0f; shadowMaskWeight = 0f;
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        switch (cells[y * W + x])
        {
            case 1: color = new NfColor(0.08f, 0.09f, 0.11f); return true;
            case 2: color = new NfColor(1f, 1f, 1f); colorMaskWeight = 1f; return true;
            case 3: color = new NfColor(1f, 1f, 1f); colorMaskWeight = 0.35f; return true;
            default: return false;
        }
    }
}
