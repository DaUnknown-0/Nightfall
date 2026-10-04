// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * EyeGlowSprite - the werewolf's two lit eyes, as a billboard of their own.
 *
 * WerewolfSprite draws the beast with eyes that stay lit in the dark, but in the game the wolf is
 * almost always drawn from its PHOTOGRAPH (AvatarCapture), and a photograph has no light of its
 * own: the eyes only ever showed in the moment before the first capture (audit 2026-10-04). The
 * User's decision: put the glowing eyes over the photo. So they travel as a separate, self-lit
 * billboard (Billboard.Glow > 0, exempt from the torch cone) at the wolf's head height, and they
 * show only while the beast faces the viewer: "the eyes vanish when it turns away".
 *
 * Unity-free like the rest of Core, so the offline renderer can draw it too.
 */

using System;

namespace Nightfall.Core;

public sealed class EyeGlowSprite : IBillboardSource
{
    private const int W = 24, H = 6;
    private static readonly NfColor EyeCore = new(1.0f, 0.86f, 0.30f);
    private static readonly NfColor EyeRim = new(0.95f, 0.28f, 0.10f);

    public int Width => W;
    public int Height => H;

    /// Frame 0: facing the viewer (eyes lit). Frame 1: turned away (nothing). Relative angle 0 is
    /// "looks at the viewer", as for CrewmateSprite; past about 100 degrees the eyes are gone.
    public int FrameForAngle(float relativeAngle) => MathF.Abs(NfMath.WrapAngle(relativeAngle)) < 1.75f ? 0 : 1;

    private static float Eye(int x, int y, float cx)
    {
        float dx = (x + 0.5f - cx) / 3.0f, dy = (y + 0.5f - H * 0.5f) / 2.6f;
        float d = dx * dx + dy * dy;
        return d < 1f ? 1f - d : 0f;
    }

    public bool Sample(int frame, int x, int y, out NfColor color, out float colorMaskWeight,
                       out float shadowMaskWeight)
    {
        color = default;
        colorMaskWeight = 0f;
        shadowMaskWeight = 0f;
        if (frame != 0 || x < 0 || x >= W || y < 0 || y >= H) return false;

        // Two round eyes, 10 px apart, a hot core with an ember rim.
        float best = MathF.Max(Eye(x, y, 7f), Eye(x, y, 17f));
        if (best <= 0.05f) return false;
        color = NfColor.Lerp(EyeRim, EyeCore, best) * (1.4f + best * 1.2f);
        return true;
    }
}
