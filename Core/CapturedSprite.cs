// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * CapturedSprite - a billboard whose pixels are a photograph of a real game object.
 *
 * The procedural crewmate was a reasonable first answer to a real problem (Among Us only draws its
 * characters from the side, so there is no artwork for "seen from behind"). It was also, visibly,
 * a drawing of a crewmate rather than a crewmate: no hat, no skin, no pet, no visor shine, none of
 * the cosmetics a player actually recognises their friends by.
 *
 * So instead of drawing one, the mod photographs the real thing. The same trick the map atlas uses:
 * point a camera at the object, render once, keep the pixels. What comes back is the player exactly
 * as Among Us draws them, hat and all, with an alpha channel, ready to be scaled into the scene at
 * whatever distance they happen to be standing.
 *
 * The one thing a photograph cannot give is a view from another angle. That is handled where it
 * belongs, in the capture: the sprite is taken facing right, and the renderer mirrors it for
 * anyone facing left. It is the same compromise Among Us itself makes in its own top-down view,
 * so it can never look wrong in a way the game does not already look.
 */

using System;

namespace Nightfall.Core;

public sealed class CapturedSprite : IBillboardSource
{
    /// RGBA, row-major from the TOP row down.
    private byte[] pixels = Array.Empty<byte>();
    private int w, h;

    /// How tall the photographed object is in world units, so the renderer can size it correctly
    /// without anyone hard-coding a scale.
    public float WorldHeight { get; private set; } = 0.7f;

    /// Frame 0 is the object as photographed, frame 1 is the mirrored version.
    public int Width => w;
    public int Height => h;

    public bool IsValid => pixels.Length > 0 && w > 0 && h > 0;

    public void Set(byte[] rgba, int width, int height, float worldHeight)
    {
        pixels = rgba;
        w = width;
        h = height;
        WorldHeight = worldHeight > 0.01f ? worldHeight : 0.7f;
    }

    public void Clear()
    {
        pixels = Array.Empty<byte>();
        w = h = 0;
    }

    /// Two frames: facing right, and facing left. Anything within a right angle of straight at the
    /// viewer keeps the near side towards them.
    public int FrameForAngle(float relativeAngle) => relativeAngle > 0f ? 1 : 0;

    public bool Sample(int frame, int x, int y, out NfColor color, out float colorMaskWeight,
                       out float shadowMaskWeight)
    {
        color = default;
        colorMaskWeight = 0f;      // the photograph already carries the player's colour
        shadowMaskWeight = 0f;

        if (!IsValid || y < 0 || y >= h) return false;
        if (frame == 1) x = w - 1 - x;              // mirrored view
        if (x < 0 || x >= w) return false;

        int o = (y * w + x) * 4;
        if (pixels[o + 3] < 24) return false;       // transparent

        color = new NfColor(pixels[o] / 255f, pixels[o + 1] / 255f, pixels[o + 2] / 255f);
        return true;
    }
}
