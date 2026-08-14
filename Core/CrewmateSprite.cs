// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * CrewmateSprite - the crewmate seen from eight directions, drawn from a description of its shape.
 *
 * WHY NOT USE THE GAME'S OWN SPRITE
 * ---------------------------------
 * Among Us has exactly one view of a crewmate: from the side. In a top-down game that is all you
 * ever need, and it is why every player sprite in the game files faces right. Standing in the same
 * room as one, that single view falls apart immediately: a crewmate walking away from you would
 * still show you its face, and you could not tell whether the shape in the beam is looking at you
 * or has not noticed you yet - which, in a game about not being noticed, is the whole point.
 *
 * So the figure is rebuilt here as a parametric body: one description, evaluated at eight viewing
 * angles. The visor swings around the head and disappears when the crewmate turns away, the
 * backpack swings the opposite way and is hidden head-on, and the silhouette narrows towards the
 * three-quarter views. That is enough for a player to read direction at a glance, at eight metres,
 * in the dark.
 *
 * COLOUR MASKS, NOT COLOURS
 * -------------------------
 * Nothing here stores a player colour. Each texel carries a base colour plus two weights: how much
 * of the player's colour belongs there, and how much of its darker shade. The renderer mixes them
 * per pixel. One sprite set therefore serves all twelve vanilla colours, every modded colour, and
 * the werewolf, without generating a single extra texture.
 */

using System;

namespace Nightfall.Core;

public sealed class CrewmateSprite : IBillboardSource
{
    public const int Frames = 8;
    public const int TexW = 48;
    public const int TexH = 64;

    /// Stride 6: r, g, b, colourMask, shadowMask, alpha.
    private readonly float[] data = new float[Frames * TexW * TexH * 6];

    public int Width => TexW;
    public int Height => TexH;

    // ---- fixed parts of the palette ----
    private static readonly NfColor Outline = new(0.06f, 0.05f, 0.09f);
    private static readonly NfColor VisorBase = new(0.62f, 0.78f, 0.86f);
    private static readonly NfColor VisorDark = new(0.32f, 0.46f, 0.58f);
    private static readonly NfColor VisorGlint = new(0.94f, 0.98f, 1.0f);

    public CrewmateSprite()
    {
        for (int f = 0; f < Frames; f++) BuildFrame(f);
    }

    /// Frame 0 faces the viewer, frame 4 faces away, and the frames in between turn clockwise. The
    /// renderer hands in the angle between the actor's facing and the direction it is being seen
    /// from, so this mapping is all that is needed to pick a view.
    public int FrameForAngle(float relativeAngle)
    {
        float t = (relativeAngle + NfMath.Pi) / NfMath.TwoPi;   // 0..1
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
    // Construction
    // ================================================================================
    /*
     * WHAT THE SECOND PLAYTEST CHANGED ABOUT THIS FIGURE.
     *
     * The first version was a pill: 24 texels across and 52 tall for the body, a nearly circular
     * visor, a backpack that never got wider than a bump, and its colour laid on as a smooth
     * gradient from "a bit of the player's colour" at the top to "a bit of the shadow colour" at
     * the bottom. Beside the game's own artwork it read as a red suppository, and beside the
     * photographed avatars (AvatarCapture, which is what one normally sees) it read as a different
     * game. Four things were wrong, and all four are about Among Us' own drawing rules:
     *
     *  1. THE PROPORTIONS. A crewmate is squat - its body is about two thirds as wide as it is
     *     tall and its legs are a fifth of the whole. Built at 0.46 wide it was a bullet.
     *  2. TWO FLAT COLOURS, NOT A GRADIENT. Among Us paints a crewmate in exactly two fills: the
     *     player's colour, and the darker shade of it (`Palette.ShadowColors`) across the bottom of
     *     the body, the legs and the backpack, with a CURVED, hard boundary between them. That
     *     boundary is most of what makes the silhouette read as a crewmate; a smooth ramp between
     *     the two is the one thing the game never does. It is also what "matching the real player
     *     colours" means literally - a texel in the fill is now the palette entry exactly, not a
     *     blend of it with grey.
     *  3. THE VISOR IS WIDE. It is a lozenge about half the body's width and two thirds as tall as
     *     it is wide, with a dark rim, a pale blue glass and one hard white glint - not a circle.
     *  4. EVERY PART IS OUTLINED, including where two parts meet. The backpack has a line against
     *     the body and the legs have one between them. Without the inner lines the figure is a
     *     blob with a window in it as soon as the torch flattens it out.
     *
     * The outline width is chosen PER PART rather than as one threshold on the distance field: the
     * field's gradient depends on a part's radius, so the single old threshold gave the body a
     * three-texel line and the legs half of one.
     */
    private void BuildFrame(int frame)
    {
        // The angle this frame is seen from. 0 = face on, PI = from behind.
        float a = (frame / (float)Frames) * NfMath.TwoPi - NfMath.Pi;
        float sin = MathF.Sin(a), cos = MathF.Cos(a);

        // Seen from an angle, a body is narrower across than it is head-on. Not a real projection,
        // just enough foreshortening that the three-quarter views do not look like the front view
        // with the visor slid sideways.
        float widthScale = 0.88f + 0.12f * MathF.Abs(cos);

        for (int y = 0; y < TexH; y++)
        {
            float ny = (y + 0.5f) / TexH;                    // 0 top .. 1 bottom
            for (int x = 0; x < TexW; x++)
            {
                float nx = ((x + 0.5f) / TexW) * 2f - 1f;    // -1 .. 1
                float sx = nx / widthScale;

                // ---- silhouette ----
                float bodyD = BodyDistance(sx, ny);
                float packD = BackpackDistance(sx, ny, sin, cos);
                float legD = LegDistance(sx, ny);

                bool inBody = bodyD <= 1f, inPack = packD <= 1f, inLeg = legD <= 1f;
                if (!inBody && !inPack && !inLeg) continue;   // outside the figure

                int o = ((frame * TexH + y) * TexW + x) * 6;

                /*
                 * WHICH PART A TEXEL BELONGS TO IS A PRIORITY, NOT A NEAREST-DISTANCE.
                 *
                 * Taking the smallest of the three fields looks right and is not: a normalised
                 * distance is relative to that shape's own radius, so a small shape NESTED inside a
                 * large one always loses. Seen from behind, the backpack sits wholly inside the
                 * body's outline and therefore vanished completely - the one view in which it is
                 * the whole picture. Pack over legs over body, plainly stated.
                 */
                bool onPack = inPack;
                bool onLeg = inLeg && !onPack;
                bool onBody = !onPack && !onLeg;

                /*
                 * ---- outline, outer and inner ----
                 * Each part's band is measured in its own field, so all three come out about two
                 * texels wide despite their different radii. Then:
                 *  - the backpack is ALWAYS outlined, including where it lies inside the body: that
                 *    line is the seam that makes it a pack rather than a bulge;
                 *  - the legs are outlined only where they stand clear of the body, because Among
                 *    Us draws body and legs as one closed silhouette with a notch between the feet
                 *    and no line across the hips;
                 *  - the body's own line stops wherever a leg or the pack has taken over.
                 */
                const float BodyBand = 0.13f, PackBand = 0.17f, LegBand = 0.20f;
                bool edge = (inPack && packD > 1f - PackBand)
                         || (onLeg && legD > 1f - LegBand && bodyD > 1f - BodyBand)
                         || (onBody && bodyD > 1f - BodyBand && legD > 1f - LegBand);
                if (edge) { Write(o, Outline, 0f, 0f); continue; }

                // ---- visor ----
                float visorW = VisorWeight(sx, ny, sin, cos);
                if (visorW > 0f && onBody)
                {
                    // Glass: pale at the top, deeper towards the chin, one hard glint up and left,
                    // and its own dark rim. The rim is not decoration - at eight metres in a torch
                    // beam it is the only thing that says which way the figure is facing.
                    float shade = NfMath.SmoothStep(0.20f, 0.38f, ny);
                    var v = NfColor.Lerp(VisorBase, VisorDark, shade);
                    float gx = sx - sin * 0.26f + 0.16f, gy = ny - 0.225f;
                    float glint = NfMath.SmoothStep(0.30f, 0.06f,
                        MathF.Sqrt(gx * gx * 5.5f + gy * gy * 55f));
                    v = NfColor.Lerp(v, VisorGlint, glint * 0.9f);
                    if (visorW < 1f) v = NfColor.Lerp(Outline, v, visorW);   // rim
                    Write(o, v, 0f, 0f);
                    continue;
                }

                /*
                 * ---- the two fills ----
                 * The dividing line runs across the body at about three quarters of its height and
                 * curves UP towards the sides, because it is the terminator of a rounded body lit
                 * from above. Legs and backpack are wholly in the dark fill, which is what makes
                 * them read as separate volumes without any lighting at all.
                 */
                float split = 0.620f - 0.070f * sx * sx;
                float shadowW = onLeg || onPack ? 1f : (ny > split ? 1f : 0f);

                // A quiet highlight over the shoulder, up and to the left. Kept to a seventh of
                // white so the fill is still recognisably the lobby's colour, and wide enough that
                // it has no edge of its own at 48 texels across.
                float dome = NfMath.SmoothStep(1.05f, 0.12f,
                    MathF.Sqrt((sx + 0.34f) * (sx + 0.34f) * 1.1f + (ny - 0.20f) * (ny - 0.20f) * 3.2f));
                float lightW = onBody && shadowW <= 0f ? dome * 0.14f : 0f;

                // colourMask 1 and shadowMask 0 puts the palette entry down EXACTLY; the highlight
                // is the only thing that ever dilutes it, and only above.
                Write(o, NfColor.White, 1f - lightW, shadowW);
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
    // Shape functions - each returns a normalised distance: <=1 is inside, >1 is outside,
    // and the band just under 1 is where the outline goes.
    // ================================================================================
    /*
     * A NOTE ON THE UNITS, because they are not square. nx runs -1..1 over 48 texels and ny runs
     * 0..1 over 64, so one unit of nx is 24 texels and one unit of ny is 64. A radius of 0.66 in x
     * and 0.375 in y is therefore 31.7 by 48 texels: two thirds as wide as tall, which is the
     * crewmate's own proportion. Read as if the numbers were comparable it looks far too wide.
     */

    /// The body: a superellipse that is flat-bottomed and a little wider low down, which is the
    /// crewmate's whole silhouette from the shoulders to where the legs start.
    private static float BodyDistance(float nx, float ny)
    {
        const float cy = 0.415f, ry = 0.375f;
        // Widening towards the bottom is what separates a crewmate from an egg: the shape is a
        // bean standing on its wide end.
        float rx = 0.66f * (0.94f + 0.09f * NfMath.SmoothStep(0.05f, 0.85f, ny));
        float dx = (nx) / rx;
        float dy = (ny - cy) / ry;
        // Exponents above 2 pull the outline towards a rounded rectangle.
        float e = MathF.Pow(MathF.Abs(dx), 2.7f) + MathF.Pow(MathF.Abs(dy), 2.4f);
        return MathF.Pow(e, 1f / 2.55f);
    }

    /// The backpack, swinging out to whichever side we are seeing the crewmate from. Hidden when
    /// looking straight at the face, fully visible from behind or from the side.
    private static float BackpackDistance(float nx, float ny, float sin, float cos)
    {
        // Hidden head on, a narrow lump at the side, and its FULL width from behind - a box has a
        // width and a depth, and which of the two is across the picture depends on the angle. The
        // first version scaled one number by "how visible" and so was at its narrowest exactly
        // where it should have been widest: from behind, where the pack IS the back.
        float show = NfMath.Clamp01((0.45f - cos) / 0.90f);
        if (show < 0.05f) return 99f;

        float cx = -sin * 0.46f;                 // opposite side from the visor
        const float cy = 0.455f;
        float rx = 0.29f * MathF.Abs(cos) + 0.12f * MathF.Abs(sin);
        const float ry = 0.205f;
        if (rx < 0.02f) return 99f;

        float dx = (nx - cx) / rx, dy = (ny - cy) / ry;
        float e = MathF.Pow(MathF.Abs(dx), 2.6f) + MathF.Pow(MathF.Abs(dy), 2.6f);
        return MathF.Pow(e, 1f / 2.6f);
    }

    /// Two stubby legs, a fifth of the figure's height. They stop it from looking like it is
    /// floating, and in the dark they are often the first part of a crewmate the torch finds.
    private static float LegDistance(float nx, float ny)
    {
        if (ny < 0.64f) return 99f;
        float best = 99f;
        for (int i = 0; i < 2; i++)
        {
            float cx = i == 0 ? -0.300f : 0.300f;
            float dx = (nx - cx) / 0.245f;
            // Reaching well up INTO the body: the two shapes have to overlap by more than the width
            // of both their outlines, or the legs hang under the crewmate with a black slot above
            // them, which is what the first attempt looked like.
            float dy = (ny - 0.845f) / 0.165f;
            float e = MathF.Pow(MathF.Abs(dx), 3.0f) + MathF.Pow(MathF.Abs(dy), 3.0f);
            best = MathF.Min(best, MathF.Pow(e, 1f / 3.0f));
        }
        return best;
    }

    /// The visor: a wide lozenge that slides around the head with the viewing angle and vanishes
    /// once the crewmate has turned more than about three quarters away. Returns 1 inside the
    /// glass, a fraction on its dark rim, 0 outside.
    private static float VisorWeight(float nx, float ny, float sin, float cos)
    {
        // cos = 1 means facing us. Fade the visor out as it rotates past the side.
        float facing = NfMath.Clamp01((cos + 0.35f) / 1.35f);
        if (facing <= 0.02f) return 0f;

        // Slides towards the side we are seeing, and narrows as it approaches the profile. Its
        // HEIGHT does not change: a visor seen edge on is a slot, not a smaller visor.
        float cx = sin * 0.26f;
        const float cy = 0.255f;
        float rx = 0.44f * (0.30f + 0.70f * facing);
        const float ry = 0.115f;

        float dx = (nx - cx) / rx, dy = (ny - cy) / ry;
        float e = MathF.Pow(MathF.Abs(dx), 3.0f) + MathF.Pow(MathF.Abs(dy), 2.4f);
        float d = MathF.Pow(e, 1f / 2.7f);
        if (d > 1f) return 0f;
        // The outer band is the visor's own dark rim, and it is generous: a one-texel rim on a
        // twenty-texel visor disappears the moment the mip pyramid takes over.
        return d > 0.80f ? (1f - d) / 0.20f * 0.9f : 1f;
    }
}
