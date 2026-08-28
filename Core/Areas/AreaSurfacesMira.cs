// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * MIRA HQ'S MATERIALS - the port of Assets/NightfallWeb/src/surfaces_mira_*.js.
 *
 * Everything AreaSurfaces.cs says about surfaces applies here unchanged: they are DRAWN and not
 * photographed, a texture declares its own size through `Unit`, and what is painted is ALBEDO -
 * a shade below what the map shows, because the room's lamps multiply it back up. Mira's own
 * files say so in their headers too, and several of them record a second pass where a value that
 * was right on the flat map read as salmon or as white once a torch was on it. Those corrected
 * values are the ones below.
 *
 * WHY A FILE OF ITS OWN. Mira contributes 215 materials, against 80 for Polus and the Skeld
 * together - the station has seventeen rooms and almost none of them share a palette, because the
 * map art gives each one its own floor. Merged into AreaSurfaces.cs they would bury the helpers
 * and the two smaller maps under them; here the class is simply continued (`partial`), so the
 * drawing helpers, `Spec` and `Rng` are the same ones, and AreaSurfaces' static constructor folds
 * this dictionary into the one catalogue every lookup goes through.
 *
 * NAMES ARE PREFIXED PER ROOM (mCafe*, mLp*, mMed*, ...), exactly as the prototype's files have
 * them - they were grep'd for collisions when they were written, and keeping them identical is
 * what lets a material be compared against its original by name.
 *
 * COLOURS CITE A WORLD COORDINATE. They were sampled in data/miraship/atlas.png at
 * px = (wx + 18.26) * 52, py = (35.525 - wy) * 52. Where a prototype comment records the measured
 * value AND the darker value actually painted, the darker one is here and the measurement is in
 * the comment, so a later correction can start from the reading rather than from the paint.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public static partial class AreaSurfaces
{
    /*
     * A 5x7 STENCIL ALPHABET, because Canvas2D has no text and Mira has four painted room signs -
     * STORAGE on the pier by the checker floor, DECONTAMINATION over the airlock, and REACTOR and
     * LABORATORY on the corridor's arrow plates. The prototype draws those with fillText, which is
     * the one canvas call that has no counterpart here; leaving the signs off would take away the
     * only lettering in the built world, and a corridor sign is exactly what a player standing in
     * a dark hallway reads first.
     *
     * The glyphs are written as pictures on purpose. Seven rows of five characters each, '#' for
     * ink: a letter can be checked by looking at it, and a new one can be added by drawing it.
     */
    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
        ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
        ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
        ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
        ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." },
        ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
        ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
        ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
        ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
        ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
        ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
        ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
        ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
        ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
        ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
        ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
        ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
    };

    /// Draws `text` CENTRED on (cx, cy) - the prototype's textAlign 'center' with textBaseline
    /// 'middle' - at cap height `capH`. `maxW`, when greater than zero, is fillText's own maxWidth:
    /// the whole line is scaled down to fit rather than clipped, which is what keeps
    /// DECONTAMINATION inside its plate.
    private static void Stencil(Canvas2D g, string text, float cx, float cy, float capH,
                                string col, float alpha = 1f, float maxW = 0f)
    {
        float px = capH / 7f;                       // one stencil pixel
        float adv = px * 6f;                        // 5 wide plus one of spacing
        float width = text.Length * adv - px;
        if (maxW > 0f && width > maxW)
        {
            float k = maxW / width;
            px *= k; adv *= k; width = maxW;
        }
        float x0 = cx - width * 0.5f, y0 = cy - px * 3.5f;
        var c = C(col);
        for (int i = 0; i < text.Length; i++)
        {
            char ch = char.ToUpperInvariant(text[i]);
            if (!Glyphs.TryGetValue(ch, out var rows)) continue;    // space and anything unmapped
            for (int r = 0; r < 7; r++)
                for (int q = 0; q < 5; q++)
                {
                    if (rows[r][q] != '#') continue;
                    // A hair of overlap, so neighbouring pixels of a stroke do not show a seam
                    // where the antialiased edges meet.
                    g.FillRect(x0 + i * adv + q * px, y0 + r * px, px + 0.5f, px + 0.5f, c, alpha);
                }
        }
    }

    /*
     * The Launchpad's tile field, shared by the plain floor and the landing disc.
     *
     * The disc draws it inline rather than layering two materials, because a cylinder cap shows
     * exactly ONE texture tile and cannot repeat a second one over it. A playtest read the pad's
     * concrete as pure white, so the ground tone is the measured #edeae4 taken down the usual
     * x 0.88 (it had been x 0.92) and the grain runs three close tones.
     */
    private static void PadTile(Canvas2D g)
    {
        Fill(g, "#ccc9c3");                         // from #edeae4 at the spawn tile
        g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#bab7b1"));   // grout, from #d6d3cd
        Grain(g, new[] { "#d4d1cb", "#bebbb5", "#b2afa9" }, 550, 0.12f, 550);
    }

    /*
     * A horizontal wavy band for the Cafeteria's MIRA stripe wall: a sine with an integer number
     * of periods per tile, so the repeat is seamless. `base`, `amp` and `thick` are fractions of
     * the tile height.
     *
     * The prototype fills one path down and back; here the band is stepped as quads over the same
     * 4-pixel interval, which draws the same shape without a path stack.
     */
    private static void CafeWave(Canvas2D g, float baseY, float amp, float phase, float thick,
                                 string col)
    {
        var c = C(col);
        float half = g.H * thick * 0.5f;
        float Y(float x) => g.H * (baseY + amp * MathF.Sin(2f * MathF.PI * (2f * x / g.W) + phase));
        for (float x = 0; x + 4 <= g.W; x += 4)
        {
            float y0 = Y(x), y1 = Y(x + 4);
            g.FillQuad(x, y0 - half, x + 4, y1 - half, x + 4, y1 + half, x, y0 + half, c);
        }
    }

    /// A filled triangle. Canvas2D has FillQuad and nothing smaller, and a quad with a repeated
    /// corner would give its convex SDF a zero-length edge - so the fourth point is the MIDPOINT
    /// of one edge, which is on the triangle and leaves every edge a real direction.
    private static void Tri(Canvas2D g, float x0, float y0, float x1, float y1,
                            float x2, float y2, string col, float alpha = 1f) =>
        g.FillQuad(x0, y0, x1, y1, (x1 + x2) * 0.5f, (y1 + y2) * 0.5f, x2, y2, C(col), alpha);

    /// How wide `text` comes out at this cap height, after `maxW` has been applied. Only
    /// StencilLeft needs it, but it is the same arithmetic Stencil does, in one place.
    private static float StencilWidth(string text, float capH, float maxW)
    {
        float px = capH / 7f;
        float width = text.Length * px * 6f - px;
        return maxW > 0f && width > maxW ? maxW : width;
    }

    /// Stencil with the prototype's textAlign 'left': `x` is the left edge, not the centre.
    private static void StencilLeft(Canvas2D g, string text, float x, float cy, float capH,
                                    string col, float alpha = 1f, float maxW = 0f) =>
        Stencil(g, text, x + StencilWidth(text, capH, maxW) * 0.5f, cy, capH, col, alpha, maxW);

    /*
     * ONE BAND OF DECONTAMINATION'S HALF-DISC CAP: the yellow field plus whichever arcs reach
     * this band.
     *
     * The disc's centre sits at canvas (W/2, H) on EVERY band - a top face maps canvas-y H to
     * north, and the disc opens south - so each arc is drawn as a full ellipse and the canvas
     * bounds do the clipping: the southern half is simply off the tile. That is also why the
     * bands can share one drawing at four different `Unit`s.
     */
    private static void CapBand(Canvas2D g, float[] radii, bool withSpoke, float bandW, int seed)
    {
        Fill(g, "#f4e84f");                         // measured, glowing
        float u = g.W / bandW;                      // world units -> canvas px (one tile = bandW)
        float cx = g.W / 2f, cy = g.H;
        float lw = MathF.Max(2f, g.H * 0.10f);
        foreach (var r in radii)
            g.StrokeEllipse(cx, cy, r * u, r * u, lw, C("#ef6306"));   // measured arc orange
        if (withSpoke) Line(g, cx, cy, cx, cy - 0.5f * u, "#ef6306", lw);
        Grain(g, new[] { "#f7ea52", "#e8dc46" }, 200, 0.08f, seed);
    }

    /// Mira HQ's half of the catalogue. Folded into `Catalogue` by AreaSurfaces' static
    /// constructor; never read directly.
    private static readonly Dictionary<string, Spec> MiraCatalogue = new()
    {
        // ============================================================ Carpet Hall (mCar*)
        // surfaces_mira_carpethall.js. The hall's lawn and fence are deliberately NOT its own
        // materials - it borrows Launchpad's mLpLawn/mLpFence so the seam at the pad and the
        // fence style run through.

        // The inner bay's floor: red-speckled terrazzo, one material over the whole length
        // (scans at y -0.8/-1.5/-2.1 from x 3.82 to 11.15 show no carpet change). Area mean
        // measured #9a5756 / median #9f5c5a. The FIRST paint, one step under that, read as salmon
        // pink under ambient plus the ceiling lamps - the albedo trap again - so the whole family
        // was pulled warm and dark: base #7c3f38, pebbles shifted with it.
        ["mCarTerrazzo"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#7c3f38");
            var r = new Rng(301);
            // the pebbles: irregular flecks in two tones, densely scattered
            for (int i = 0; i < 90; i++)
            {
                bool dark = i % 3 != 0;
                float s = 2f + r.Next() * 4f;
                Rect(g, r.Next() * g.W, r.Next() * g.H, s, s * (0.6f + r.Next() * 0.8f),
                     dark ? "#542824" : "#945448", dark ? 0.55f : 0.45f);
            }
            for (int i = 0; i < 50; i++)                                    // from #632c29
                Rect(g, r.Next() * g.W, r.Next() * g.H, 1f + r.Next() * 2f, 1f + r.Next() * 2f,
                     "#46211e", 0.45f);
            Grain(g, new[] { "#7a3d36", "#68332e" }, 400, 0.10f, 302);
        } },

        // The grey path of the outdoor stretch (x 0.06..3.69). Area mean #9ea1a6; the cool
        // light-grey first paint read as pure white under sky light, so it is concrete grey now,
        // one step under the reading and shifted warm.
        ["mCarPath"] = new Spec { Unit = 1.3f, Draw = g => {
            Fill(g, "#837e76");
            Grain(g, new[] { "#8d887f", "#78736b" }, 500, 0.10f, 303);
            var r = new Rng(304);
            for (int i = 0; i < 20; i++)
                Rect(g, r.Next() * g.W, r.Next() * g.H, 2f + r.Next() * 5f, 1f + r.Next() * 2f,
                     r.Next() < 0.5f ? "#8f8a81" : "#736e66", 0.14f);
        } },

        // West threshold at the indoor/outdoor step (x 3.688..3.823): dark steel blue, measured
        // as the stripe run #868d96,#526173,#7a8594,#4a5262,#697284,#848a9c,#576173 on the
        // y = -1.50 scan. One step darker: #4e586b.
        ["mCarSillW"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#4e586b");
            Line(g, 0, 2, g.W, 2, "#6a7585", 2);                            // bright front lip
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#39414f", 2);                // dark back edge
            for (float x = 6; x < g.W; x += 18) Line(g, x, 0, x, g.H, "#434c5c", 2, 0.5f);
            Grain(g, new[] { "#576274", "#454e5e" }, 300, 0.10f, 305);
        } },

        // East threshold into Wood Hall (x 11.15..11.225): #845658 transition, #76757f band,
        // #9fa2ad highlight, #4b5253 dark edge, planks from 11.227. One step darker: #8b8e99.
        ["mCarSillE"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#8b8e99");
            Line(g, 0, 2, g.W, 2, "#a6a9b3", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#3f4649", 2);                // dark edge to the wood
            Line(g, g.W * 0.3f, 0, g.W * 0.3f, g.H, "#6f727c", 3, 0.4f);    // the #76757f band
            Grain(g, new[] { "#9598a2", "#7f828c" }, 300, 0.10f, 306);
        } },

        // The inner south wall's corridor face. Not measurable in a plan view, so estimated from
        // the west threshold's steel family and built like mWooWall (bead under the cap, darker
        // plinth) - without that the hall reads as a hole.
        ["mCarWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#566274");
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#48536a", 2, 0.45f);
            Line(g, 0, 2, g.W, 2, "#77839a", 2);                            // bead under the cap
            Rect(g, 0, g.H * 0.8f, g.W, g.H * 0.2f, "#434d5e");             // darker plinth
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#3a4353", 3);
            Grain(g, new[] { "#5d697c", "#4d5869" }, 400, 0.10f, 307);
        } },

        // Ceiling: estimated (a top-down atlas shows no ceilings), a neutral light tone in the
        // hall's palette with mWooCeil's panel joints.
        ["mCarCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#c6c3bb");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#adaaa2", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#adaaa2", 3);
            Grain(g, new[] { "#d0cdc5", "#b9b6ae" }, 400, 0.10f, 308);
        } },

        // ============================================================ Wood Hallway (mWoo*)
        // surfaces_mira_woodhall.js.

        // The plank floor, the room's only furniture. Measured over the whole length: plank tones
        // #efebde..#d3cfc6, long joints #ada2a5 with a pale #e1d9d2 run-up, cross joints #a6a39e.
        // Grid: long joints every ~0.27, cross joints every ~0.575 in a staggered bond. One step
        // darker: base #c3bdb0.
        ["mWooPlank"] = new Spec { Unit = 1.08f, Draw = g => {
            Fill(g, "#c3bdb0");
            float cw = g.W / 4f;                                            // four planks per tile
            var tone = new[] { "#edeade", "#979183", "#d5cfc1", "#7a7468" };
            var toneA = new[] { 0.40f, 0.28f, 0.32f, 0.28f };
            for (int i = 0; i < 4; i++) Rect(g, i * cw + 1, 0, cw - 2, g.H, tone[i], toneA[i]);
            // cross joints: the tile edges are one, the middle only on the even planks
            Line(g, 0, 1, g.W, 1, "#918a8a", 2);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#918a8a", 2);
            foreach (int i in new[] { 0, 2 })
                Line(g, i * cw + 1, g.H / 2f, (i + 1) * cw - 1, g.H / 2f, "#918a8a", 2);
            // long joints: every other one strong with a pale shadow beside it, the rest faint
            for (int i = 0; i <= 4; i++)
            {
                float x = i * cw;
                if (i % 2 == 0) { Rect(g, x - 3, 0, 2, g.H, "#b7b0a8"); Line(g, x, 0, x, g.H, "#8f8789", 2); }
                else Line(g, x, 0, x, g.H, "#a19a9b", 1);
            }
            Grain(g, new[] { "#d2ccbe", "#b0aa9d" }, 500, 0.10f, 311);
        } },

        // The north threshold into SkyCarpetHall. Scan at x = 12.40: planks to 5.565, then
        // #545d6d, #3e4555, #373c4a/#313b4a, #495362, pale hem #787f89 at 5.70. One step darker:
        // #2e3644; the pale hem is the NORTH side, so it sits at the far edge here.
        ["mWooSill"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#2e3644");
            Line(g, 0, 2, g.W, 2, "#464e5c", 2);                            // edge to the planks
            for (float x = 0; x < g.W; x += 14) Line(g, x, 0, x, g.H, "#262c38", 2, 0.6f);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#646b74", 2);                // pale hem, north
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#232833", 1);
            Grain(g, new[] { "#3a4250", "#252b37" }, 350, 0.10f, 312);
        } },

        // The corridor's wall face. MedBay measured the same wall - "pale ice blue #aae0ff at the
        // west fin strip (13.62, 1.25)" - and that strip borders THIS hallway, so this is
        // deliberately the same paint as mMedWall: the run along the east side has to read as one
        // wall. One step darker: #95c5e0.
        ["mWooWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#95c5e0");
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#7faecb", 2, 0.45f);
            Line(g, 0, 2, g.W, 2, "#bce4fa", 2);
            Rect(g, 0, g.H * 0.8f, g.W, g.H * 0.2f, "#7fa8c4");
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#6f9cb8", 3);
            Grain(g, new[] { "#9ccbe4", "#88b4d0" }, 400, 0.10f, 313);
        } },

        // The dark steel band of the south cap (hull edge). Measured #212429 at (12.40, -2.32)
        // and #242429 in the link block at (13.62, 2.00). One step darker: #1b1e22. Plates in
        // courses with the odd rivet - from inside it is the wall, from above it is the cap.
        ["mWooHull"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#1b1e22");
            for (float y = 0; y < g.H; y += g.H / 3f) Line(g, 0, y, g.W, y, "#14161a", 2, 0.7f);
            for (float x = 0; x < g.W; x += g.W / 3f) Line(g, x, 0, x, g.H, "#14161a", 1, 0.7f);
            for (int i = 0; i < 6; i++)
                g.FillEllipse((i * 47 + 13) % g.W, (i * 83 + 19) % g.H, 1.4f, 1.4f, C("#101216"));
            Grain(g, new[] { "#23262b", "#15171b" }, 350, 0.10f, 314);
        } },

        // Ceiling: estimated, a warm light tone to match the planks, mMedCeil's panel joints.
        ["mWooCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#ccc5b8");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#b3ac9f", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#b3ac9f", 3);
            Grain(g, new[] { "#d6cfc2", "#bfb8ab" }, 400, 0.10f, 315);
        } },

        // ============================================================ Communications (mCom*)
        // surfaces_mira_comms.js. A dark electronics room - everything in it lies in the north
        // wall's shadow, so the whole palette is painted a step under an already dark reading.

        // The green carpet, in two bands at the room's north and south ends (atlas scan x = 15.50:
        // 4.48..5.47 and 2.46..3.40). Ground #263526 at (14.20, 2.60), wave lines #1e2d21/#213429
        // under it, pale flecks #293c29. One step darker: #1e2a1f.
        ["mComCarpet"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#1e2a1f");
            // the waves: shallow zig-zags, drawn segment by segment because Canvas2D has no path
            for (float y = 6; y < g.H; y += 9)
                for (float x = 0; x + 8 <= g.W; x += 8)
                {
                    float a = (((int)(x / 8)) % 2 == 0) ? -1.5f : 1.5f;
                    Line(g, x, y + a, x + 8, y - a, "#17211a", 2);
                }
            Line(g, 0, 10, g.W, 10, "#243527", 1, 0.5f);
            Line(g, 0, g.H - 14, g.W, g.H - 14, "#243527", 1, 0.5f);
            Grain(g, new[] { "#2c4130", "#1a271c" }, 500, 0.12f, 321);
        } },

        // The fine tiled floor across the room's middle band (3.40..4.48). Grey-brown, ground
        // #47413f, darker slabs #393431/#312c29, joints #423839. Grid ~0.29. One step darker:
        // #3a3533.
        ["mComTile"] = new Spec { Unit = 0.58f, Draw = g => {
            Fill(g, "#3a3533");
            Rect(g, 2, 2, g.W / 2f - 3, g.H / 2f - 3, "#443e3b");           // the lighter slab face
            Rect(g, g.W / 2f + 1, g.H / 2f + 1, g.W / 2f - 3, g.H / 2f - 3, "#443e3b");
            Rect(g, g.W / 2f + 1, 2, g.W / 2f - 3, g.H / 2f - 3, "#2e2a27"); // every other one darker
            Rect(g, 2, g.H / 2f + 1, g.W / 2f - 3, g.H / 2f - 3, "#2e2a27");
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#262322", 2, 0.8f);        // the joint cross
            Line(g, 0, g.H / 2f, g.W, g.H / 2f, "#262322", 2, 0.8f);
            Line(g, 0, 1, g.W, 1, "#262322", 1);
            Line(g, 1, 0, 1, g.H, "#262322", 1);
            Grain(g, new[] { "#443e3b", "#2e2a27" }, 400, 0.10f, 322);
        } },

        // Comms' wall face: the dark aubergine violet that wraps the whole room. Measured #392c3c
        // on the north band (13.90, 5.60) and the west band's outer edge (13.62, 4.90); the hull's
        // shadow sides are #212429/#242429 and are mComHull. One step darker: #2e2331.
        ["mComWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#2e2331");
            for (float x = 0; x < g.W; x += 34) Line(g, x, 0, x, g.H, "#241b29", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#3b2e40", 2);                            // bead under the cap
            Rect(g, 0, g.H * 0.78f, g.W, g.H * 0.22f, "#251c2b");           // dark plinth strip
            Line(g, 0, g.H * 0.78f, g.W, g.H * 0.78f, "#1c1522", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#120e16", 3);
            Grain(g, new[] { "#352940", "#271e30" }, 400, 0.10f, 323);
        } },

        // Hull and wall caps: the near-black steel band of the outer faces (east hull
        // 17.08..17.20, #212429 at (17.12, 3.00)). One step darker: #1a1c21. Deliberately plain -
        // from above the cap is all one ever sees of it.
        ["mComHull"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#1a1c21");
            for (float x = 0; x < g.W; x += 44) Line(g, x, 0, x, g.H, "#121317", 2, 0.6f);
            for (float y = 0; y < g.H; y += 30) Line(g, 0, y, g.W, y, "#121317", 1, 0.6f);
            Line(g, 0, 2, g.W, 2, "#24272d", 1);
            for (float x = 10; x < g.W; x += 44)
            {
                g.FillEllipse(x, 8, 1.4f, 1.4f, C("#101216"));
                g.FillEllipse(x, g.H - 8, 1.4f, 1.4f, C("#101216"));
            }
            Grain(g, new[] { "#20232a", "#14161b" }, 350, 0.10f, 324);
        } },

        // Ceiling: estimated. A cool panel tone with a faint violet undertone (an echo of
        // mComWall), one step darker than MedBay's mMedCeil so the room stays the small dark
        // electronics room.
        ["mComCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#b5afbc");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#98929f", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#98929f", 3);
            Grain(g, new[] { "#bfb9c6", "#a59fae" }, 400, 0.10f, 325);
        } },

        // ============================================================ MedBay (mMed*)
        // surfaces_mira_medbay.js.

        // The floor: a fine blue/white checker, cell ~0.19 with a LIGHT seam between cells.
        // Fields #dbe7ec / #e4f4fc, seam #e7f3ff. One tile = 2x2 cells.
        ["mMedChecker"] = new Spec { Unit = 0.38f, Draw = g => {
            Fill(g, "#c0cbd0");                                             // field A, from #dbe7ec
            Rect(g, g.W / 2f, 0, g.W / 2f, g.H / 2f, "#c8d7dd");            // field B, from #e4f4fc
            Rect(g, 0, g.H / 2f, g.W / 2f, g.H / 2f, "#c8d7dd");
            Rect(g, 0, 0, g.W, 2, "#e7f3ff", 0.55f);                        // the light seam
            Rect(g, 0, g.H / 2f - 1, g.W, 2, "#e7f3ff", 0.55f);
            Rect(g, 0, 0, 2, g.H, "#e7f3ff", 0.55f);
            Rect(g, g.W / 2f - 1, 0, 2, g.H, "#e7f3ff", 0.55f);
            Grain(g, new[] { "#c6d1d6", "#bcc7cc" }, 400, 0.08f, 331);
        } },

        // The room's wall panel: pale ice blue, MedBay's signature tint. Measured #aae0ff on the
        // north band's face (16.08, 2.20) and again on the west wall's face strip (13.62, 1.25) -
        // one material for every room-facing face, and mWooWall is deliberately the same paint.
        ["mMedWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#95c5e0");
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#7faecb", 2, 0.45f);
            Line(g, 0, 2, g.W, 2, "#bce4fa", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#6f9cb8", 3);
            Grain(g, new[] { "#9ccbe4", "#8abbd6" }, 500, 0.10f, 332);
        } },

        // The west doorway's threshold strip: blue-grey, distinctly NOT checker in the atlas
        // (#656d7d / #565d6e across x 13.58..13.72 at y = 0.10).
        ["mMedSill"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#5a606e");
            Line(g, 0, 1, g.W, 1, "#6d7482", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#4a505c", 2);
            Grain(g, new[] { "#636a78", "#525867" }, 400, 0.10f, 333);
        } },

        // Ceiling: bright cool panel with a seam grid, tone following the pale blue walls.
        ["mMedCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#c6cdd4");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#a6adb4", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#a6adb4", 3);
            Grain(g, new[] { "#d2d9df", "#bac1c8" }, 400, 0.10f, 334);
        } },

        // THE BEDS, one material per part. Measured down the middle of the north bed at x = 15.54:
        // frame rail #94aab5, mattress #c3c6d6, folded grey blanket #9597a5, pillow #e5edf0, pale
        // blue headboard #c2d2ff with #5e6fa8 trim. The horizontal and low beds carry the indigo
        // stripe #6375aa instead of the grey blanket.
        ["mMedBedFrame"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#82959f");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#6f828c", 2);
            Grain(g, new[] { "#8d9fa9", "#788a94" }, 400, 0.10f, 335);
        } },

        ["mMedBedMattress"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#abaebe");
            Line(g, 0, 1, g.W, 1, "#bcbfce", 2);
            Grain(g, new[] { "#b4b7c6", "#a2a5b5" }, 400, 0.10f, 336);
        } },

        ["mMedBedBlanket"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#838491");
            Line(g, 0, 2, g.W, 2, "#94959f", 2);                            // the fold
            Grain(g, new[] { "#8d8e9a", "#797a87" }, 400, 0.10f, 337);
        } },

        ["mMedBedPillow"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#c9d0d3");
            g.StrokeRoundRect(2, 2, g.W - 4, g.H - 4, 0, 2, C("#aab3b8"));
            Grain(g, new[] { "#d2d9dc", "#bfc7ca" }, 300, 0.10f, 338);
        } },

        ["mMedBedHead"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#aab8e0");
            Line(g, 0, g.H * 0.28f, g.W, g.H * 0.28f, "#526193", 2);        // the darker blue trim
            Line(g, 0, 2, g.W, 2, "#c3d1f2", 2);
            Grain(g, new[] { "#b2c0e6", "#9dadd4" }, 400, 0.10f, 339);
        } },

        ["mMedBedStripe"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#576795");
            Line(g, 0, g.H * 0.4f, g.W, g.H * 0.4f, "#9eacd5", 2);          // its light blue line
            Grain(g, new[] { "#5e6e9d", "#4d5c8a" }, 300, 0.10f, 340);
        } },

        // The tiny monitor on each headboard: dark housing, lit screen. Emissive, so it glows in
        // the half light - the sprite shows a bright face at every bed head.
        ["mMedBedMonitor"] = new Spec { Unit = 0.25f, Emissive = 0.9f, Draw = g => {
            Fill(g, "#101820");                                             // housing
            Rect(g, g.W * 0.08f, g.H * 0.08f, g.W * 0.84f, g.H * 0.84f, "#2a3c48");
            Rect(g, g.W * 0.16f, g.H * 0.20f, g.W * 0.68f, g.H * 0.48f, "#7fc4e8");   // lit screen
            Rect(g, g.W * 0.16f, g.H * 0.20f, g.W * 0.68f, g.H * 0.12f, "#bde6ff");
            Grain(g, new[] { "#16222c", "#0b1116" }, 200, 0.12f, 341);
        } },

        // THE SCANNER (Submit Scan). Teal ring #6399a2 with a faint self-glow - the pad reads
        // "switched on" in every reference; green pad #84d3a5 and the brighter cross #8ad4ad,
        // both emissive (a lit floor, not a lamp - the Skeld medScanPad lesson).
        ["mMedScanRing"] = new Spec { Unit = 0.6f, Emissive = 0.35f, Draw = g => {
            Fill(g, "#57878f");
            g.StrokeRoundRect(2, 2, g.W - 4, g.H - 4, 0, 3, C("#487078"));
            Grain(g, new[] { "#5e9098", "#4e7a82" }, 300, 0.10f, 342);
        } },

        ["mMedScanPad"] = new Spec { Unit = 0.5f, Emissive = 0.55f, Draw = g => {
            Fill(g, "#74ba91");
            Grain(g, new[] { "#7cc098", "#6cb088" }, 300, 0.10f, 343);
        } },

        ["mMedScanCross"] = new Spec { Unit = 0.4f, Emissive = 0.85f, Draw = g => {
            Fill(g, "#79bb98");
            Line(g, 0, 1, g.W, 1, "#a5dcb8", 2);                            // the highlight edge
            Grain(g, new[] { "#81c29f", "#6faf8d" }, 300, 0.10f, 344);
        } },

        // ============================================================ Locker Room (mLoc*)
        // surfaces_mira_lockerroom.js. Sampled in data/miraship/areas/_ref/lockerroom.png with
        // px = (wx - 1.24) * 52, py = (8.16 - wy) * 52.

        // The floor: warm white tile with a thin diamond lattice. Base #f4eeec, lattice #d6dbce
        // crossing every ~0.70 on both axes, so one texture unit is one diamond: the two
        // corner-to-corner diagonals tile seamlessly, plus the small centred outline the
        // reference shows at the intersections.
        ["mLocTile"] = new Spec { Unit = 1.4f, Draw = g => {
            Fill(g, "#d6d1d0");
            Line(g, 0, 0, g.W, g.H, "#bcc0b5", 1.5f);
            Line(g, 0, g.H, g.W, 0, "#bcc0b5", 1.5f);
            float cx = g.W / 2f, cy = g.H / 2f, s = g.W * 0.10f;            // the small diamond
            Line(g, cx, cy - s, cx + s, cy, "#bcc0b5", 1.5f);
            Line(g, cx + s, cy, cx, cy + s, "#bcc0b5", 1.5f);
            Line(g, cx, cy + s, cx - s, cy, "#bcc0b5", 1.5f);
            Line(g, cx - s, cy, cx, cy - s, "#bcc0b5", 1.5f);
            Grain(g, new[] { "#dcd7d6", "#cec9c8" }, 400, 0.08f, 351);
        } },

        // Wall faces. A top-down atlas shows no faces, so the tone is DERIVED from the lit wall
        // top #7f7488 at (3.60, 1.00): grey-violet sheets, bright bead under the cap, dark base.
        ["mLocWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#706678");
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#5a5064", 2, 0.45f);
            Line(g, 0, 2, g.W, 2, "#8d8098", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#4e4560", 3);
            Grain(g, new[] { "#7a7082", "#665c6e" }, 500, 0.10f, 352);
        } },

        // Wall tops away from the south band: the dark violet the reference reads everywhere
        // (#291738). A playtest called the NE mass "a black textureless block" - the flat
        // near-black was all its cap and end faces had. Panel joints with a fallen edge, one
        // course line and rivet pairs give the dark some structure; the GROUND TONE is unchanged
        // at #241431 = the measured #291738 x ~0.88.
        ["mLocWallTop"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#241431");
            for (float x = 0; x <= g.W; x += g.W / 2f)                      // joints, fallen edge
            {
                Line(g, x, 0, x, g.H, "#170c22", 2);
                if (x > 0 && x < g.W) Line(g, x + 2, 0, x + 2, g.H, "#372647", 1);
            }
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#1b0e28", 2);          // course line
            Line(g, 0, g.H * 0.5f + 3, g.W, g.H * 0.5f + 3, "#33224a", 1, 0.5f);
            for (float y = 10; y < g.H; y += 26)                            // rivet pairs
            {
                Rect(g, g.W / 2f - 5, y, 2, 2, "#150a1f");
                Rect(g, g.W / 2f + 3, y, 2, 2, "#150a1f");
            }
            Grain(g, new[] { "#2a1938", "#201130", "#190d26" }, 600, 0.14f, 353);
        } },

        // The south wall's top: the blue band with its lighter stripe (#4780d6 at (9.00, -0.30)).
        ["mLocWallTopBlue"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#3f71bc");
            Line(g, 0, g.H * 0.42f, g.W, g.H * 0.42f, "#ffffff", 3, 0.20f);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#2f5a9a", 3);                // the dark outer edge
            Grain(g, new[] { "#4579c4", "#3969ae" }, 400, 0.10f, 354);
        } },

        // Ceiling: neutral cool panel with a seam grid, following the MedBay convention.
        ["mLocCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#b9bfc4");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#9aa0a6", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#9aa0a6", 3);
            Grain(g, new[] { "#c4cacf", "#aeb4ba" }, 400, 0.10f, 355);
        } },

        // THE LOCKERS: tan/wood bank. Body wood #a88d73 with the darker segmentation #8c7158 and
        // the lighter door-panel tone #e4dbce. Two dark vent slots in the upper third.
        ["mLocLocker"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#947c65");
            Rect(g, g.W * 0.12f, g.H * 0.16f, g.W * 0.76f, g.H * 0.50f, "#c9c1b5");  // door panel
            Line(g, 0, g.H * 0.72f, g.W, g.H * 0.72f, "#7b634d", 2);        // the mid rail
            Rect(g, g.W * 0.30f, g.H * 0.22f, g.W * 0.40f, g.H * 0.05f, "#5f4c3c");  // vent slots
            Rect(g, g.W * 0.30f, g.H * 0.32f, g.W * 0.40f, g.H * 0.05f, "#5f4c3c");
            Grain(g, new[] { "#9d856c", "#8a7259" }, 400, 0.10f, 356);
        } },

        // THE BENCHES: violet cushion #7b6db5 with the lighter stitch highlight from #8976c7.
        // AreaKit adds the dark legs.
        ["mLocBench"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#6c609f");
            Line(g, 0, g.H * 0.35f, g.W, g.H * 0.35f, "#7968af", 2);        // the cushion seam
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#544878", 2);
            Grain(g, new[] { "#7568a8", "#63588f" }, 300, 0.10f, 357);
        } },

        // THE MATS: woven teal. Base #70a5a5, light weave #9ef4f3, dark border #42718c.
        ["mLocMat"] = new Spec { Unit = 0.45f, Draw = g => {
            Fill(g, "#639192");
            for (float y = 2; y < g.H - 2; y += 4) Line(g, 1, y, g.W - 1, y, "#8bd7d6", 1);
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#3a647b"));
            Grain(g, new[] { "#6d9d9e", "#598586" }, 300, 0.10f, 358);
        } },

        // The Decon airlock step: the lit yellow of the corridor mouth (#f4e84f along y = 2.40).
        // Slightly emissive - in the reference it glows against the room.
        ["mLocDeconYellow"] = new Spec { Unit = 0.6f, Emissive = 0.30f, Draw = g => {
            Fill(g, "#d7cd45");
            Line(g, 0, 1, g.W, 1, "#e6dc59", 2);
            Grain(g, new[] { "#ded44e", "#cec23c" }, 300, 0.10f, 359);
        } },

        // The sliding door leaf: pale ice blue-white (#d9ebec, the leaf parked in the east
        // pocket) with a vertical parting seam.
        ["mLocDoor"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#c0cfd0");
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#9fb4b6", 2);              // the parting line
            Line(g, 0, 1, g.W, 1, "#d3e2e3", 2);
            Grain(g, new[] { "#cad8d9", "#b4c5c6" }, 300, 0.10f, 360);
        } },

        // ============================================================ SkyBridge (mSky*)
        // surfaces_mira_skybridge.js.
        //
        // Five of these were carrying a call the prototype's helper does not have -
        // `fill(g, x, y, w, h, col)` against a `fill(g, w, h, col)` - so the parapet band, its
        // blue stripe, the roof's light strips and the Door Log's red rings were never drawn at
        // all. A plan view cannot show that: each material still had a plausible ground tone and
        // only its pattern was missing. The prototype now has a `band()` for those nine calls,
        // and what is below is the INTENDED drawing, not the silent one.

        // THE DECK: navy glass bridge tiles, 1.04 grid, with the long diagonal light streaks the
        // map paints across every bridge section and the occasional pale fleck.
        ["mSkyDeck"] = new Spec { Unit = 1.04f, Draw = g => {
            Fill(g, "#454b6e");                                             // from #4e557d
            Line(g, 0, 0, g.W, 0, "#2a3559", 2);                            // seams, from #303c65
            Line(g, 0, 0, 0, g.H, "#2a3559", 2);
            Line(g, g.W * 0.18f, g.H, g.W * 0.62f, 0, "#5f6a87", 5, 0.55f); // streaks
            Line(g, g.W * 0.55f, g.H, g.W * 0.95f, 0, "#7e8b9a", 3, 0.35f);
            Grain(g, new[] { "#4d5378", "#565c85", "#a3b4cb" }, 260, 0.10f, 361);
        } },

        // Neck plates north of the threshold: near-flat dark slate, big quiet plates.
        ["mSkyPlate"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#3b3b44");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#33333c", 2);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#33333c", 2);
            Line(g, 0, g.H * 0.45f, g.W, g.H * 0.45f, "#45454f", 1, 0.5f);
            Grain(g, new[] { "#404049", "#36363f" }, 300, 0.08f, 362);
        } },

        // The X-braced space-glass floor panel: frame all round, pale glass, one X per tile. The
        // panel is 1.06 x 2.88 and the map draws two stacked X's, so the unit is 1.44 - the rect
        // tiles exactly 1 x 2.
        ["mSkyGlassX"] = new Spec { Unit = 1.44f, Draw = g => {
            Fill(g, "#9eadb6");                                             // glass, from #b3c4ce
            Rect(g, g.W * 0.16f, g.H * 0.12f, g.W * 0.68f, g.H * 0.5f, "#abc3c9", 0.7f);  // sheen
            Line(g, 3, 3, g.W - 3, g.H - 3, "#506670", 7);                  // braces, from #5b747f
            Line(g, g.W - 3, 3, 3, g.H - 3, "#506670", 7);
            g.StrokeRoundRect(4, 4, g.W - 8, g.H - 8, 0, 8, C("#3d4c68"));  // frame, from #455676
        } },

        // Pale threshold strips (deck to plates, and the greenhouse doorway).
        ["mSkyThresh"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#8e8f93");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#7c7d81", 2);
            Grain(g, new[] { "#97989c", "#838488" }, 300, 0.10f, 363);
        } },

        // Neck walls and the door-band cheeks: blue-grey slate with vertical panel seams about
        // 0.72 apart and a horizontal course line. The lower neck reads darker in the atlas
        // (#1e2126) - that is baked shade, and the lamps divide it again.
        ["mSkyWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#363d48");
            foreach (float f in new[] { 0.25f, 0.75f })
            {
                Line(g, g.W * f, 0, g.W * f, g.H, "#2b313b", 2);
                Line(g, g.W * f + 2, 0, g.W * f + 2, g.H, "#3f4753", 1, 0.5f);
            }
            Line(g, 0, g.H * 0.52f, g.W, g.H * 0.52f, "#2b313b", 1);
            Grain(g, new[] { "#3b434f", "#31373f" }, 450, 0.10f, 364);
        } },

        // Outer parapets, OUTER face: dark rail with the pale MIRA band and the blue stripe
        // running its length. The band sits mid-height on the rail, at 0.31..0.45 of the tile so
        // it lands ~0.45..0.65 world - on a 1.05 rail either v direction reads as the band.
        ["mSkyRail"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#2e3340");                                             // rim, from #343a49
            Rect(g, 0, g.H * 0.31f, g.W, g.H * 0.14f, "#a7b4cc");           // band, from #bdcbe7
            Rect(g, 0, g.H * 0.345f, g.W, g.H * 0.055f, "#426fbd");         // stripe, from #4a7dd6
            Line(g, 0, g.H * 0.31f, g.W, g.H * 0.31f, "#1d212b", 1);
            Line(g, 0, g.H * 0.45f, g.W, g.H * 0.45f, "#1d212b", 1);
            Grain(g, new[] { "#333947", "#292e39" }, 350, 0.10f, 365);
        } },

        // Parapet INNER faces, the chevron and the apex nose: plain dark charcoal rail.
        ["mSkyRailDark"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#32313a");
            Line(g, 0, 1, g.W, 1, "#3c3b46", 2);                            // top-edge catchlight
            Grain(g, new[] { "#383742", "#2c2b34" }, 350, 0.10f, 366);
        } },

        // THE ROOF of the covered bridge - the one invented surface here, because the atlas draws
        // no roof over the bridge to copy. Dark plate, two light strips per tile; the strips ARE
        // the emissive map, so they glow cool white while the plate between them stays dark.
        ["mSkyRoof"] = new Spec { Unit = 1.45f, Emissive = 0.5f, Draw = g => {
            Fill(g, "#131316");                                             // from #161619
            Line(g, 0, 0, g.W, 0, "#0d0d10", 2);                            // panel joints
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#0d0d10", 1);
            Rect(g, 0, g.H * 0.26f, g.W, g.H * 0.18f, "#8d99b5", 0.4f);     // strip halo
            Rect(g, 0, g.H * 0.56f, g.W, g.H * 0.18f, "#8d99b5", 0.4f);
            Rect(g, 0, g.H * 0.30f, g.W, g.H * 0.10f, "#a7b4cc");           // strips, from #bdcbe7
            Rect(g, 0, g.H * 0.60f, g.W, g.H * 0.10f, "#a7b4cc");
            Grain(g, new[] { "#17171a", "#101013" }, 350, 0.08f, 367);
        } },

        // Door Log post: light grey steel, vertical brush.
        ["mSkyPost"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#888e94");
            for (float x = 4; x < g.W; x += 10) Line(g, x, 0, x, g.H, "#797f85", 2, 0.5f);
            Grain(g, new[] { "#93999f", "#7d838a" }, 250, 0.10f, 368);
        } },

        // Door Log cap: the red/white banding the sprite shows on top of the post.
        ["mSkyCap"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#ccc7bf");                                             // from #e8e4dc
            Rect(g, 0, 0, g.W, g.H * 0.34f, "#9b3831");                     // from #b04038
            Rect(g, 0, g.H * 0.67f, g.W, g.H * 0.33f, "#9b3831");
            Grain(g, new[] { "#d5d0c8", "#c2bdb5" }, 200, 0.08f, 369);
        } },

        // ============================================================ Sky Carpet Hall (mSca*)
        // surfaces_mira_skycarpethall.js. The room's name misleads: the floor is speckled deck
        // covering, NOT carpet, and it has no visible joint grid at all (fine scans F1-F4).
        //
        // The prototype computes its darker values through a `dark(hex, 0.84)` helper; they are
        // written out here, with the measured value in the comment, because a generated shade is
        // exactly the kind of thing that silently drifts when two implementations both round it.

        // THE FLOOR: pale beige, finely speckled, jointless. Measured #dbd4c9 at (12.40, 6.50)
        // and (23.00, 6.00), grain #d6cfc6/#dad3ca/#d2cac0. One step darker: #b8b2a9.
        ["mScaFloor"] = new Spec { Unit = 1.3f, Draw = g => {
            Fill(g, "#b8b2a9");
            Grain(g, new[] { "#b4aea6", "#b7b1aa", "#aea89f" }, 700, 0.12f, 371);
            // a hint of a slab joint every two units, barely visible (the atlas shows none)
            for (float x = 0; x <= g.W; x += g.W) Line(g, x, 0, x, g.H, "#a9a39a", 1, 0.10f);
        } },

        // THE THRESHOLDS: the striped band at both south seams. Measured on the x = 11.90 scan:
        // #818994 #6e7887 #495162 #7b8a9c #63697b #787f89, and the east one reads the same
        // family. Each a step darker; the pale hem #787f89 is NORTH, towards the hall.
        ["mScaSill"] = new Spec { Unit = 0.6f, Draw = g => {
            var cols = new[] { "#6f7887", "#5d6675", "#3e4653", "#697887", "#535a6a", "#666d78" };
            var frac = new[] { 0.10f, 0.20f, 0.10f, 0.20f, 0.20f, 0.20f };
            float v = 0;
            for (int i = 0; i < cols.Length; i++) { Rect(g, 0, v * g.H, g.W, frac[i] * g.H + 1, cols[i]); v += frac[i]; }
            Line(g, 0, 1, g.W, 1, "#4a5260", 2);                            // edge to the next sill
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#7d848e", 2);                // pale hem, north
            Grain(g, new[] { "#6b7482", "#49505c" }, 300, 0.10f, 372);
        } },

        // THE WALL FIELD: the blue-grey of the hall's inner faces. Measured #bacce7 at
        // (11.90, 7.80); cap and foot are the dark #242429. One step darker: #9cabc2. The plinth
        // replaces a kit skirting - at the west wall's south end a skirting would run into
        // nothing (see the area file's header).
        ["mScaWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#9cabc2");
            for (float x = 0; x <= g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#8b9ac0", 2, 0.5f);
            Line(g, 0, 2, g.W, 2, "#c8d5ec", 2);                            // bead under the cap
            Rect(g, 0, g.H * 0.82f, g.W, g.H * 0.18f, "#7683a1");           // darker plinth
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#71809f", 3);
            Grain(g, new[] { "#a3aec3", "#8d97b0" }, 400, 0.10f, 373);
        } },

        // THE MIRA MURAL: the east section's north wall and the jamb pier. Field as mScaWall,
        // over it the blue stripe #4780d6 and the rainbow waves - yellow #ffcc00, red #ad3300,
        // teal #009994 - running diagonally across field and stripe. The accent colours are
        // darkened only a quarter step: they are the SUBJECT, not the wall. The course of the
        // waves themselves is interpolated from the scan points.
        ["mScaMural"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#9cabc2");
            for (float x = 0; x <= g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#8b9ac0", 2, 0.5f);
            Rect(g, 0, g.H * 0.38f, g.W, g.H * 0.22f, "#4176c5");           // the blue glass band
            // The waves. Canvas2D has no path, so each is stepped as quads - the same 4-pixel
            // step the prototype's polyline uses, so the two draw the same curve.
            void Wave(string col, float yBase, float amp, float thick, float phase)
            {
                var c = C(col);
                for (float x = 0; x + 4 <= g.W; x += 4)
                {
                    float o0 = MathF.Sin(x / g.W * MathF.PI * 2f + phase) * amp * g.H;
                    float o1 = MathF.Sin((x + 4) / g.W * MathF.PI * 2f + phase) * amp * g.H;
                    float t0 = g.H * yBase + o0, t1 = g.H * yBase + o1;
                    g.FillQuad(x, t0, x + 4, t1, x + 4, t1 + thick * g.H, x, t0 + thick * g.H, c);
                }
            }
            Wave("#f5c400", 0.46f, 0.05f, 0.10f, 0.0f);                     // yellow, from #ffcc00
            Wave("#a63100", 0.56f, 0.06f, 0.09f, 2.1f);                     // red, from #ad3300
            Wave("#008985", 0.66f, 0.05f, 0.08f, 4.2f);                     // teal, from #009994
            Wave("#c9c5d1", 0.74f, 0.04f, 0.07f, 1.0f);                     // pale lavender band
            Line(g, 0, 2, g.W, 2, "#c8d5ec", 2);
            Rect(g, 0, g.H * 0.82f, g.W, g.H * 0.18f, "#7683a1");
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#71809f", 3);
            Grain(g, new[] { "#a3aec3", "#8d97b0" }, 350, 0.08f, 374);
        } },

        // DARK CAP / HULL: every wall outer face and cap. Measured #242429; one step darker:
        // #1e1e22. Plates in courses with the odd rivet, like mWooHull.
        ["mScaHull"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#1e1e22");
            for (float y = 0; y < g.H; y += g.H / 3f) Line(g, 0, y, g.W, y, "#141519", 2, 0.7f);
            for (float x = 0; x < g.W; x += g.W / 3f) Line(g, x, 0, x, g.H, "#141519", 1, 0.7f);
            for (int i = 0; i < 6; i++)
                g.FillEllipse((i * 47 + 13) % g.W, (i * 83 + 19) % g.H, 1.4f, 1.4f, C("#0f1013"));
            Grain(g, new[] { "#26272c", "#17181c" }, 350, 0.10f, 375);
        } },

        // Ceiling: estimated, a cool light tone to match the blue-grey field.
        ["mScaCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#c6ccd6");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#aeb4c0", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#aeb4c0", 3);
            Grain(g, new[] { "#d0d6e0", "#bac0cc" }, 400, 0.10f, 376);
        } },

        // ============================================================ Balcony (mBal*)
        // surfaces_mira_balcony.js.

        // THE HEX FLOOR (west wing, around the dish): warm cream hexagons with olive-brown grout.
        // Field #dbcdb5, grout #846c54..#9c846c. Autocorrelation gives a 13 px column period at
        // 52 px/u, so point-to-point is ~0.33 u, flat-top orientation. A seamless tiling would
        // need the sqrt(3) ratio, which 128 px does not give at this size, so the cells are drawn
        // 85.33 x 64 px on a 64 px grid: right width and orientation, ~12% squat.
        ["mBalHex"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#c1b49e");                                             // field, from #dbcdb5
            const float hw = 85.33f, hh = 64f;
            var shadeC = C("#b3a48d");
            void Hex(float cx, float cy, bool shade)
            {
                float x0 = cx + hw / 2f, x1 = cx + hw / 4f, x2 = cx - hw / 4f, x3 = cx - hw / 2f;
                float yt = cy - hh / 2f, yb = cy + hh / 2f;
                if (shade)
                {
                    // A flat-top hexagon as its two trapezoids: FillQuad is convex-only.
                    g.FillQuad(x3, cy, x2, yt, x1, yt, x0, cy, shadeC);
                    g.FillQuad(x3, cy, x0, cy, x1, yb, x2, yb, shadeC);
                }
                Line(g, x0, cy, x1, yt, "#857458", 5);                      // grout, from #93826a
                Line(g, x1, yt, x2, yt, "#857458", 5);
                Line(g, x2, yt, x3, cy, "#857458", 5);
                Line(g, x3, cy, x2, yb, "#857458", 5);
                Line(g, x2, yb, x1, yb, "#857458", 5);
                Line(g, x1, yb, x0, cy, "#857458", 5);
            }
            for (int i = -1; i <= 2; i++)
                for (int j = -1; j <= 2; j++)
                    Hex(i * 64, j * 64 + (System.Math.Abs(i) % 2) * 32, ((i + j) % 3 + 3) % 3 == 0);
            Grain(g, new[] { "#cbc0aa", "#b5a892" }, 500, 0.10f, 381);
        } },

        // THE PLANK FLOOR (main gallery, at both door tunnels): wide dark boards with a wavy
        // north-south figure. Field #4d4643/#393432, dark figure #2a2625, lighter streaks
        // #4d4643. Butt joints sparse and staggered.
        ["mBalPlank"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#3d3735");
            foreach (float bx in new[] { 20f, 78f })                        // lighter boards
                Rect(g, bx, 0, 26, g.H, "#463f3c", 0.5f);
            void Wig(float x0, string col, float lw)                        // the wavy N-S figure
            {
                for (float y = 0; y + 6 <= g.H; y += 6)
                {
                    float a = x0 + 5f * MathF.Sin(y / 14f + x0);
                    float b = x0 + 5f * MathF.Sin((y + 6f) / 14f + x0);
                    Line(g, a, y, b, y + 6, col, lw);
                }
            }
            Wig(12, "#272322", 5); Wig(38, "#2b2725", 3); Wig(60, "#262221", 6);
            Wig(86, "#2b2725", 3); Wig(110, "#272322", 5);
            Rect(g, 0, 34, 52, 3, "#242120");                               // two staggered joints
            Rect(g, 64, 96, 64, 3, "#242120");
            Grain(g, new[] { "#46403c", "#2e2927" }, 1400, 0.16f, 382);
        } },

        // The gallery's dark wall panel: the lining of Cafeteria's south band on the balcony side
        // (#3c3735 at (23.8, -1.40), #494240 at (27.5, -1.40)) and the hull faces. The same sheet
        // system as the cream rooms, in the ship's dark steel.
        ["mBalHullWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#3e3937");
            foreach (float f in new[] { 1f / 3f, 2f / 3f })
            {
                Line(g, 0, g.H * f, g.W, g.H * f, "#2c2927", 2);
                Line(g, 0, g.H * f + 3, g.W, g.H * f + 3, "#4a4441", 2, 0.5f);
            }
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#2f2b29", 2);                // butt joint
            Grain(g, new[] { "#464140", "#353130" }, 500, 0.10f, 383);
        } },

        // Balustrade posts, sills and handrail: dark warm grey, brushed. Posts #444241, rail top
        // #48413e.
        ["mBalFence"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#3b3836");
            for (float x = 3; x < g.W; x += 6) Line(g, x, 0, x, g.H, "#4a4643", 2, 0.25f);
            Line(g, 0, 1, g.W, 1, "#2c2a28", 2);
            Grain(g, new[] { "#444140", "#332f2d" }, 400, 0.10f, 384);
        } },

        // Ceiling: cool pale grey-blue panel, cooler than Cafeteria's warm white - the ejection
        // view wants this room cold.
        ["mBalCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#a7afb9");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#878f99", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#878f99", 3);
            Grain(g, new[] { "#b2bac4", "#9aa2ac" }, 400, 0.10f, 385);
        } },

        // THE WEATHER KIOSK's west face, one tile for the whole front (1.36 x 1.40, so the unit
        // is the height and it maps once). Grey-blue cabinet #677186 with a #9ca3b6 top, the
        // control half - button grid, keypad, dark dispense slot - and the two pale amber display
        // panels #e1c1a2/#cbb999, painted darker; the live screen glow comes from the panel
        // fixture on top. Canvas right is NORTH, where the drawn WeatherConsole strip sits.
        ["mBalWeatherFace"] = new Spec { Unit = 1.4f, Draw = g => {
            Fill(g, "#5f6878");                                             // body, from #677186
            Rect(g, 0, 0, g.W, g.H * 0.07f, "#89909e");                     // top cap, from #9ca3b6
            Line(g, g.W * 0.46f, g.H * 0.07f, g.W * 0.46f, g.H * 0.96f, "#3a3f4a", 4);  // mid frame
            for (int r = 0; r < 4; r++)                                     // control half, south
                for (int c = 0; c < 3; c++)
                {
                    Rect(g, g.W * (0.07f + c * 0.115f), g.H * (0.14f + r * 0.115f),
                         g.W * 0.085f, g.H * 0.075f, "#4a5162");
                    Rect(g, g.W * (0.085f + c * 0.115f), g.H * (0.155f + r * 0.115f),
                         g.W * 0.055f, g.H * 0.045f, "#9aa2b2");
                }
            Rect(g, g.W * 0.07f, g.H * 0.63f, g.W * 0.33f, g.H * 0.20f, "#3a3f4c");     // keypad
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                    Rect(g, g.W * (0.095f + c * 0.10f), g.H * (0.655f + r * 0.075f),
                         g.W * 0.07f, g.H * 0.045f, "#b8bfca");
            Rect(g, g.W * 0.07f, g.H * 0.87f, g.W * 0.33f, g.H * 0.07f, "#262a33");     // slot
            void Amber(float y0, float hh)                                  // display half, north
            {
                Rect(g, g.W * 0.52f, g.H * y0, g.W * 0.42f, g.H * hh, "#3a3f4c");
                Rect(g, g.W * 0.545f, g.H * (y0 + 0.025f), g.W * 0.37f, g.H * (hh - 0.05f), "#c6aa8e");
                for (int b = 0; b < 6; b++)
                {
                    float bh = g.H * (0.03f + ((b * 37) % 5) * 0.012f);
                    Rect(g, g.W * (0.56f + b * 0.055f), g.H * (y0 + hh - 0.035f) - bh,
                         g.W * 0.035f, bh, "#a8763e");
                }
            }
            Amber(0.12f, 0.34f);
            Amber(0.52f, 0.34f);
            Grain(g, new[] { "#6a7384", "#545d6e" }, 300, 0.08f, 386);
        } },

        // ============================================================ Storage (mSto*)
        // surfaces_mira_storage.js.
        //
        // The room's own logic, as measured: the floor is the dark green-grey checker, the south
        // wall and both door piers show a LIGHT warm-grey face into Storage, and every other wall
        // only ever shows its dark cap #2a2a35 in the top-down - so the unseen west, east and
        // north faces are built in that dark slate and the light panel is kept for the wall one
        // actually looks at.

        // THE FLOOR: the checker the room is known by. Tile 0.43 u. NOT a strict two-shade board -
        // the row scan reads dark, dark, light, mid-light, dark, mid, dark across seven tiles,
        // because the game jitters the shade per tile. The first row of the matrix is that
        // measured row; the other three are parity plus jitter.
        ["mStoChecker"] = new Spec { Unit = 1.72f, Draw = g => {
            float t = g.W / 4f;                                             // one 0.43 u tile
            // D dark #5a6a64, E dark-2 #51635d, L light #758982, M mid #647971, N mid-light #667b73
            var grid = new[]
            {
                new[] { "#4f5d58", "#4f5d58", "#677872", "#596c65" },        // the measured row
                new[] { "#677872", "#586a63", "#4f5d58", "#4f5d58" },
                new[] { "#4f5d58", "#677872", "#586a63", "#4f5d58" },
                new[] { "#596c65", "#4f5d58", "#677872", "#475751" },
            };
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    Rect(g, c * t, r * t, t, t, grid[r][c]);
            float grout = MathF.Max(2f, t * 0.045f);                        // the dark seam
            for (int i = 0; i <= 4; i++)
            {
                Line(g, i * t, 0, i * t, g.H, "#3d4741", grout);
                Line(g, 0, i * t, g.W, i * t, "#3d4741", grout);
            }
            Grain(g, new[] { "#57655f", "#6d7e75", "#4a5852" }, 700, 0.10f, 391);
        } },

        // The light wall panel of the south wall and both door piers, Storage side. Field #c6c1ba
        // to #d3ced6 nearer the west pier's lit end - the cafeteria's cream wall family, one baked
        // shade darker on this side. A playtest called the flat mass featureless, so it carries a
        // vertical plate joint with a light edge between the course lines and corner rivets at the
        // joint crossings; the albedo is unchanged at #aea89f (= #c6c1ba x 0.88).
        ["mStoWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#aea89f");
            foreach (float f in new[] { 1f / 3f, 2f / 3f })
            {
                Line(g, 0, g.H * f, g.W, g.H * f, "#a29a80", 2);
                Line(g, 0, g.H * f + 3, g.W, g.H * f + 3, "#c4beb4", 2, 0.5f);
            }
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#99937f", 2);          // the plate joint
            Line(g, g.W * 0.5f + 3, 0, g.W * 0.5f + 3, g.H, "#c4beb4", 2, 0.5f);  // with its light edge
            foreach (float f in new[] { 1f / 3f, 2f / 3f })                 // corner rivets
                foreach (float xf in new[] { 0.06f, 0.5f, 0.94f })
                {
                    Rect(g, g.W * xf - 1, g.H * f - 5, 2, 2, "#8f897b");
                    Rect(g, g.W * xf + 2, g.H * f + 3, 2, 2, "#8f897b");
                }
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#948e7f", 2);                // butt joint
            Grain(g, new[] { "#b8b2a8", "#aaa49a", "#a29c92" }, 500, 0.10f, 392);
        } },

        // The dark slate the other three walls are built in. No face of the west, east or north
        // wall is ever drawn unfolded in the map - only their caps, and those are consistently
        // #2a2a35. A playtest called it "almost black and flat", so it carries panel-joint pairs
        // (shadow seam plus light edge), a second course line and rivet groups; the ground tone
        // STAYS #25252e - it is the measured cap value x 0.88 and sits above the #1a1a20
        // threshold, so raising it would be inventing a lighter room than the map draws.
        ["mStoWallDark"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#25252e");
            for (float x = 0; x <= g.W; x += g.W / 3f)                      // joints, fallen edge
            {
                Line(g, x, 0, x, g.H, "#1b1b23", 2);
                if (x > 0 && x < g.W) Line(g, x + 2, 0, x + 2, g.H, "#333340", 1);
            }
            Line(g, 0, g.H * 0.34f, g.W, g.H * 0.34f, "#1e1e26", 2);        // course line
            Line(g, 0, g.H * 0.71f, g.W, g.H * 0.71f, "#1e1e26", 2);        // second course line
            Line(g, 0, g.H * 0.71f + 3, g.W, g.H * 0.71f + 3, "#34333f", 1, 0.5f);
            for (float y = 10; y < g.H; y += 26)                            // rivet groups
                for (float x = g.W / 3f; x < g.W - 1; x += g.W / 3f)
                {
                    Rect(g, x - 5, y, 2, 2, "#17171e");
                    Rect(g, x + 3, y, 2, 2, "#17171e");
                }
            Grain(g, new[] { "#2b2b35", "#23232c", "#1f1f28" }, 600, 0.12f, 393);
        } },

        // Ceiling: mid panel with a seam grid. Nothing is visible top-down, so the tone is
        // invented between the dark walls and the lamps' warm light.
        ["mStoCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#54575e");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#3f4249", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#3f4249", 3);
            Grain(g, new[] { "#5d6067", "#4a4d54" }, 400, 0.10f, 394);
        } },

        // The tall shelf's face on the north wall unit: nearly black field #182b3b with book
        // spines poking out of the dark - olive-gold #7a5f19, steel blue #2a3e4f, violet #605079.
        // Three shelf rows of spines, drawn once over the whole 1.39 x 1.5 face.
        ["mStoTopShelf"] = new Spec { Unit = 1.5f, Draw = g => {
            Fill(g, "#152533");
            var spines = new[] { "#6b5415", "#253746", "#54476a", "#355d53", "#79415d", "#a29d97" };
            foreach (float f in new[] { 0.04f, 0.38f, 0.72f })              // three shelf levels
            {
                float yTop = g.H * f, rowH = g.H * 0.26f;
                float x = g.W * 0.04f;
                int i = 0;
                while (x < g.W * 0.94f)                                     // the book row
                {
                    float bw = g.W * (0.035f + ((i * 7) % 4) * 0.012f);
                    Rect(g, x, yTop + rowH * 0.12f, bw, rowH * 0.82f,
                         spines[(int)(i + f * 10) % spines.Length]);
                    Rect(g, x + bw - 1, yTop + rowH * 0.12f, 1, rowH * 0.82f, "#000000", 0.35f);
                    x += bw + g.W * 0.012f;
                    i++;
                }
                Line(g, 0, yTop + rowH + g.H * 0.015f, g.W, yTop + rowH + g.H * 0.015f, "#0b141d", 3);
            }
            Grain(g, new[] { "#1b2d3e", "#101c29" }, 400, 0.12f, 395);
        } },

        // The mid-room shelf unit's face. Body field #25292f / #2b2c36, the same spine palette as
        // the north shelf, plus the magenta crate and the pale boxes the sprite carries on its
        // right-hand shelves.
        ["mStoMidShelf"] = new Spec { Unit = 1.4f, Draw = g => {
            Fill(g, "#202429");
            var spines = new[] { "#253746", "#54476a", "#6b5415", "#355d53", "#a29d97", "#79415d" };
            foreach (float f in new[] { 0.05f, 0.40f })                     // two book levels
            {
                float yTop = g.H * f, rowH = g.H * 0.24f;
                float x = g.W * 0.03f;
                int i = 0;
                while (x < g.W * 0.62f)
                {
                    float bw = g.W * (0.03f + ((i * 5) % 4) * 0.011f);
                    Rect(g, x, yTop + rowH * 0.14f, bw, rowH * 0.80f,
                         spines[(int)(i + f * 7) % spines.Length]);
                    x += bw + g.W * 0.010f;
                    i++;
                }
                Line(g, 0, yTop + rowH + g.H * 0.015f, g.W, yTop + rowH + g.H * 0.015f, "#12161b", 3);
            }
            Rect(g, g.W * 0.66f, g.H * 0.10f, g.W * 0.28f, g.H * 0.20f, "#79415d");  // magenta crate
            g.StrokeRoundRect(g.W * 0.66f, g.H * 0.10f, g.W * 0.28f, g.H * 0.20f, 0, 2, C("#5b3150"));
            Rect(g, g.W * 0.64f, g.H * 0.42f, g.W * 0.15f, g.H * 0.17f, "#a29d97");  // pale boxes
            Rect(g, g.W * 0.81f, g.H * 0.44f, g.W * 0.13f, g.H * 0.15f, "#a29d97");
            Rect(g, g.W * 0.64f, g.H * 0.42f, g.W * 0.15f, g.H * 0.03f, "#8f8a83");
            Rect(g, g.W * 0.81f, g.H * 0.44f, g.W * 0.13f, g.H * 0.03f, "#8f8a83");
            Grain(g, new[] { "#262b31", "#191d22" }, 400, 0.12f, 396);
        } },

        // The green cabinet on the north unit's right end: field #39615a / #3f615a, darker base
        // band #29484f. Locker doors with vent slats, one tile per door width.
        ["mStoLocker"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#32554f");
            Rect(g, 0, g.H * 0.86f, g.W, g.H * 0.14f, "#243f46");           // base band
            g.StrokeRoundRect(2, 2, g.W - 4, g.H - 4, 0, 3, C("#243f46"));
            for (int i = 0; i < 3; i++)                                     // the vent slats
                Rect(g, g.W * 0.22f, g.H * (0.16f + i * 0.09f), g.W * 0.56f, g.H * 0.045f, "#243f46");
            Rect(g, g.W * 0.44f, g.H * 0.55f, g.W * 0.12f, g.H * 0.035f, "#a8b4ac");  // handle tab
            Grain(g, new[] { "#38605a", "#2c4b45" }, 400, 0.10f, 397);
        } },

        // Cardboard, for every box in the room - the map draws them all from one sprite family.
        // Body #948373, lid/tape tone #b5aa9e. Flap seam across the middle, tape stripe down the
        // centre of the face.
        ["mStoCardboard"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#827365");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#6b5d4c", 2);          // the flap seam
            Rect(g, g.W * 0.42f, 0, g.W * 0.16f, g.H, "#9f958b");           // the tape stripe
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#5f5344"));
            Grain(g, new[] { "#8d7d6e", "#756553" }, 400, 0.10f, 398);
        } },

        // THE STORAGE SIGN, on the SE pier's north face (#009999 in the crop, #31a3a1 in the
        // atlas). The letters sit straight on the pier's light panel - there is no plate in the
        // drawing - so the field is the same #aea89f as mStoWall and the sign reads as paint.
        ["mStoSign"] = new Spec { Unit = 0.7f, Draw = g => {
            Fill(g, "#aea89f");                                             // pier panel
            g.StrokeRoundRect(2, 2, g.W - 4, g.H - 4, 0, 2, C("#8f897c"));
            Stencil(g, "STORAGE", g.W / 2f + g.H * 0.02f, g.H * 0.54f, g.H * 0.46f,
                    "#6f6a62", 1f, g.W * 0.86f);                            // letterpress shadow
            Stencil(g, "STORAGE", g.W / 2f, g.H * 0.52f, g.H * 0.46f,
                    "#008787", 1f, g.W * 0.86f);
            Grain(g, new[] { "#b8b2a8", "#a29c92" }, 200, 0.08f, 399);
        } },

        // Fix Wiring, stage 1 - the panel's Storage face. The west pier's light face carries a
        // grey plate #bab8b2 with a dark screen band; plate, screen, the pale cross and the four
        // wire studs of the wiring minigame.
        ["mStoFixWiring"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#a3a19c");                                             // plate, from #bab8b2
            Rect(g, g.W * 0.10f, g.H * 0.16f, g.W * 0.80f, g.H * 0.52f, "#2e3338");  // screen band
            Rect(g, g.W * 0.46f, g.H * 0.24f, g.W * 0.08f, g.H * 0.36f, "#8a95a0");  // the cross
            Rect(g, g.W * 0.30f, g.H * 0.36f, g.W * 0.40f, g.H * 0.10f, "#8a95a0");
            var wires = new[] { "#e0b400", "#b8433a", "#2e4e88", "#e0b400" };
            for (int i = 0; i < 4; i++)                                     // the wire studs
                Rect(g, g.W * (0.14f + i * 0.20f), g.H * 0.78f, g.W * 0.12f, g.H * 0.10f, wires[i]);
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#6f6d68"));
            Grain(g, new[] { "#aeaca6", "#93918c" }, 200, 0.08f, 400);
        } },

        // ============================================================ Greenhouse (mGre*)
        // surfaces_mira_greenhouse.js.
        //
        // TWO DELIBERATE DEPARTURES from the "one step darker" rule, both recorded in the
        // prototype's own header: the plan samples for soil, foliage and vines were taken THROUGH
        // the dome glass and are therefore pre-shaded almost to black (#185d05 foliage, #311810
        // soil), so those albedos are raised well above the reading; and the glass floor's ground
        // tone was lightened again in acceptance because it read as a black hole from above.
        //
        // THE GLASS IS OPAQUE HERE. The prototype's glass materials carry transparent/opacity/
        // depthWrite; the mod's rasteriser has no blending at all (see AreaKit's note - every
        // triangle writes depth), which is why every pane on Polus and the Skeld is an opaque dark
        // blue too. The drawings below are ported as-is and simply come out solid.

        // GLASS FLOOR with its X-bracing: translucent panes over a dark substructure ("The Sky
        // Below"). Measured around (16.4, 23.5): pale glass #c2dce4/#b0cadc, navy bracing
        // #4b536d/#353a4d.
        ["mGreGlassX"] = new Spec { Unit = 1.44f, Draw = g => {
            Fill(g, "#c3d5dc");                                             // from #c2dce4, lifted
            Rect(g, 0, g.H * 0.12f, g.W, g.H * 0.30f, "#dfeef2", 0.45f);    // light patch
            Rect(g, g.W * 0.55f, g.H * 0.6f, g.W * 0.4f, g.H * 0.28f, "#b7ccd6", 0.45f);
            // soft scuff marks in the glass - the prototype's quadratic curves, stepped
            float lw = MathF.Max(1f, g.W * 0.012f);
            for (int i = 0; i < 3; i++)
            {
                float y = g.H * (0.2f + i * 0.28f);
                float ax = g.W * 0.06f, ay = y, bx = g.W * 0.5f, by = y - g.H * 0.09f;
                float cx2 = g.W * 0.94f, cy2 = y + g.H * 0.05f;
                float px = ax, py = ay;
                for (int s = 1; s <= 8; s++)
                {
                    float t = s / 8f, u = 1f - t;
                    float qx = u * u * ax + 2 * u * t * bx + t * t * cx2;
                    float qy = u * u * ay + 2 * u * t * by + t * t * cy2;
                    Line(g, px, py, qx, qy, "#e6f2f6", lw, 0.4f);
                    px = qx; py = qy;
                }
            }
            float bw = MathF.Max(2f, g.W * 0.026f);                         // the X brace
            Line(g, 2, 2, g.W - 2, g.H - 2, "#48536b", bw);
            Line(g, g.W - 2, 2, 2, g.H - 2, "#48536b", bw);
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, MathF.Max(2f, g.W * 0.032f), C("#333c52"));
        } },

        // DOME GLASS (the clerestory band of the diagonal segments): pale blue-grey from the
        // drawn inside of the dome (#b2bbcb), dark glazing bars. A little self-emission sells the
        // daylight under the glass.
        ["mGreDomeGlass"] = new Spec { Unit = 1.45f, Emissive = 0.14f, Draw = g => {
            Fill(g, "#aeb9c6");                                             // from #b2bbcb
            Rect(g, 0, 0, g.W * 0.35f, g.H, "#c8d3dd", 0.35f);
            float bw = MathF.Max(2f, g.W * 0.02f);                          // two bars per tile
            foreach (float fx in new[] { 0.25f, 0.75f }) Line(g, g.W * fx, 0, g.W * fx, g.H, "#465064", bw);
            Line(g, 0, g.H * 0.52f, g.W, g.H * 0.52f, "#465064", bw);
            Line(g, g.W * 0.25f, g.H * 0.52f, g.W * 0.75f, g.H, "#525d72",  // the dome net's brace
                 MathF.Max(2f, g.W * 0.016f));
        } },

        // The dome's inner PLINTH band: blue-grey steel panels just inside the arc line, measured
        // #4b536d at the kink points. Lifted slightly (pre-shaded reading plus the lamps' own
        // shadow) so the panels stay readable.
        ["mGreBase"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#474f68");                                             // from #4b536d
            Grain(g, new[] { "#3d4459", "#525a74", "#414860" }, 300, 0.5f, 401);
            float lw = MathF.Max(1f, g.W * 0.012f);
            foreach (float fx in new[] { 1f / 3f, 2f / 3f }) Line(g, g.W * fx, 0, g.W * fx, g.H, "#363d52", lw);
            Line(g, 0, g.H * 0.72f, g.W, g.H * 0.72f, "#363d52", lw);
        } },

        // The dome's OUTER skin: deep space violet from just outside the arc (#291738). Left dark
        // on purpose - it is space.
        ["mGreHullOut"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#241430");                                             // from #291738
            Grain(g, new[] { "#1a0e26", "#33204a" }, 160, 0.6f, 402);
            float s = MathF.Max(1f, g.W * 0.012f);
            foreach (var p in new[] { (0.18f, 0.3f), (0.64f, 0.62f), (0.86f, 0.22f) })
                Rect(g, g.W * p.Item1, g.H * p.Item2, s, s, "#c9d4ea");
        } },

        // Dark carrier / edge: the south cap over the room's edge (#23232a), which is also the
        // glass floor's rim beam and its skirting.
        ["mGreFrame"] = new Spec { Unit = 0.72f, Draw = g => {
            Fill(g, "#20202a");                                             // from #23232a
            Grain(g, new[] { "#181820", "#2a2a35" }, 220, 0.5f, 403);
        } },

        // TERRACOTTA of the bed rings and pot rims. RAW READING #b56547 at the PlantConsole edge -
        // drawn THROUGH the dome glass, so pre-shaded: the first draft at one step darker rendered
        // as a black lump. Kept at the raw reading.
        ["mGrePot"] = new Spec { Unit = 0.36f, Draw = g => {
            Fill(g, "#b56547");
            Grain(g, new[] { "#9a5535", "#c06d4c", "#84462c" }, 320, 0.5f, 404);
        } },

        // BED SOIL: measured #311810 through the dome glass, i.e. nearly black. Raised to the
        // acceptance target #7a4a30.
        ["mGreSoil"] = new Spec { Unit = 0.30f, Draw = g => {
            Fill(g, "#7a4a30");
            Grain(g, new[] { "#69402a", "#8a5638", "#5c3826" }, 420, 0.6f, 405);
        } },

        // FOLIAGE GREEN: measured #185d05/#004a02 in the beds, again through the glass. Raised to
        // the acceptance target #3f7d3f.
        ["mGreLeaf"] = new Spec { Unit = 0.30f, Draw = g => {
            Fill(g, "#3f7d3f");
            Grain(g, new[] { "#356d35", "#4c8f49", "#2f6330" }, 460, 0.65f, 406);
        } },

        // The crown of the tree in the O2 cylinder: pale lilac-pink from the elevation of the
        // plant (#b5aac3/#a09bb0). The raw reading is kept, for the same pre-shading reason.
        ["mGrePinkLeaf"] = new Spec { Unit = 0.30f, Draw = g => {
            Fill(g, "#b5aac3");
            Grain(g, new[] { "#a89db8", "#c3b9cf", "#9f95ad" }, 420, 0.6f, 407);
        } },

        // HANGING VINES: the drawing's most saturated leaf green #074d07, a second green #2a5239 -
        // both measured through the glass. Raised to #2e6b38, kept a little deeper than mGreLeaf
        // so the two read as different depths.
        ["mGreVine"] = new Spec { Unit = 0.24f, Draw = g => {
            Fill(g, "#2e6b38");
            Grain(g, new[] { "#276031", "#3a7d43", "#215429" }, 400, 0.6f, 408);
        } },

        // Steel of the Divert Power podium: frame #7c909c at the stand. Lifted slightly, or it
        // read as black.
        ["mGreStand"] = new Spec { Unit = 0.42f, Draw = g => {
            Fill(g, "#7b8d97");
            Grain(g, new[] { "#6c7e88", "#8a9ca6" }, 260, 0.5f, 409);
            Line(g, 0, g.H * 0.3f, g.W, g.H * 0.3f, "#57676f", MathF.Max(1f, g.W * 0.014f));
        } },

        // The walk-off mat at the doorway: an even grey #909eae in the plan. Raised to the
        // acceptance target, a light grey-beige, so it reads against the dark plinth band.
        ["mGreWalk"] = new Spec { Unit = 0.60f, Draw = g => {
            Fill(g, "#cdc6b6");
            Grain(g, new[] { "#beb7a6", "#dad3c3" }, 340, 0.5f, 410);
            g.StrokeRoundRect(g.W * 0.06f, g.H * 0.06f, g.W * 0.88f, g.H * 0.88f, 0,
                              MathF.Max(1f, g.W * 0.012f), C("#a89f8c"));
        } },

        // ============================================================ Decontamination (mDec*)
        // surfaces_mira_decontamination.js. Painted one step darker than measured, EXCEPT the
        // things that give off light themselves - the floor strip, the call buttons, the half-disc
        // cap and the DECONTAMINATION sign - which keep their measured glow values.

        // Chamber floor: bright service yellow #f4e84f with wear patches #d6c842 nearer the west
        // wall and the dark diagonal hatch ticks.
        ["mDecFloorYellow"] = new Spec { Unit = 0.52f, Draw = g => {
            Fill(g, "#d7cb45");                                             // from #f4e84f
            float lw = MathF.Max(1f, g.H * 0.055f);
            for (float x = -g.H; x < g.W + g.H; x += g.W / 2f)              // hatch
                Line(g, x, g.H, x + g.H, 0, "#a89d38", lw);
            Rect(g, 0, 0, g.W * 0.22f, g.H, "#bcaf3a", 0.25f);              // wear patches
            Grain(g, new[] { "#e0d44a", "#cfc23f" }, 420, 0.10f, 421);
        } },

        // The walkway plate down the chamber axis: dark teal #142c35 centre, edge wells #082431.
        ["mDecWalk"] = new Spec { Unit = 0.645f, Draw = g => {
            Fill(g, "#152b34");
            Line(g, 1, 0, 1, g.H, "#082431", 2, 0.7f);                      // edge wells
            Line(g, g.W - 2, 0, g.W - 2, g.H, "#082431", 2, 0.7f);
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#1c3844", 1, 0.5f);    // quiet centre line
            Grain(g, new[] { "#183039", "#102831" }, 300, 0.10f, 422);
        } },

        // The 8-segment floor light strip. Cool decon mint, keeps its glow.
        ["mDecStrip"] = new Spec { Unit = 0.24f, Emissive = 0.9f, Draw = g => {
            Fill(g, "#5ecfc0");
            Rect(g, g.W * 0.18f, g.H * 0.2f, g.W * 0.64f, g.H * 0.6f, "#d9fff4");   // hot core
            Grain(g, new[] { "#8ae8da", "#4ec4b4" }, 120, 0.12f, 423);
        } },

        // The diamond floor mark at the chamber centre: dark outline on the walkway teal, lit pip.
        ["mDecDiamond"] = new Spec { Unit = 0.8f, Emissive = 0.35f, Draw = g => {
            Fill(g, "#152b34");
            float c = g.W / 2f, r = MathF.Min(g.W, g.H) * 0.44f, my = g.H / 2f;
            float lw = MathF.Max(2f, g.H * 0.07f);
            Line(g, c, my - r, c + r, my, "#0a1c22", lw);
            Line(g, c + r, my, c, my + r, "#0a1c22", lw);
            Line(g, c, my + r, c - r, my, "#0a1c22", lw);
            Line(g, c - r, my, c, my - r, "#0a1c22", lw);
            Rect(g, c - g.H * 0.08f, my - g.H * 0.08f, g.H * 0.16f, g.H * 0.16f, "#bfeee6");
        } },

        // Chamber inner walls: light grey-lavender tile #c9c4cd with panel seams #948a9b.
        ["mDecTileWall"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#b1acb9");                                             // from #c9c4cd
            for (float x = 0; x <= g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#827b8c", 2, 0.55f);
            for (float y = 0; y <= g.H; y += g.H / 2f) Line(g, 0, y, g.W, y, "#827b8c", 1, 0.55f);
            Line(g, 0, 2, g.W, 2, "#cfcad4", 2);                            // bead under the cap
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#948a9b", 3);
            Grain(g, new[] { "#b8b3bf", "#a49fae" }, 450, 0.09f, 424);
        } },

        // The door arches: decon mint #bbe3db with the chevron panel lines of the sprite.
        ["mDecArch"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#a4c8c1");                                             // from #bbe3db
            float lw = MathF.Max(1f, g.H * 0.03f);
            for (int i = 0; i < 3; i++)                                     // chevrons, leaning in
            {
                float t = (i + 1) / 4f;
                Line(g, g.W * t, 0, g.W * (1 - t), g.H, "#7c9d96", lw);
            }
            Line(g, 0, 1, g.W, 1, "#d7efe9", 2, 0.4f);                      // lit top edge
            Grain(g, new[] { "#afd4cf", "#98bcb6" }, 300, 0.09f, 425);
        } },

        // Door leaves: dark blue #214572 with the orange diamond motif at their meeting edges.
        // One unit = one leaf.
        ["mDecDoorLeaf"] = new Spec { Unit = 0.79f, Draw = g => {
            Fill(g, "#1d3d64");                                             // from #214572
            Line(g, 1, 0, 1, g.H, "#2c548a", 2, 0.5f);                      // leaf face lines
            Line(g, g.W - 2, 0, g.W - 2, g.H, "#12294a", 2, 0.5f);
            float cx = g.W / 2f, cy = g.H * 0.52f, r = MathF.Min(g.W, g.H) * 0.30f;
            float lw = MathF.Max(2f, g.H * 0.05f);                          // from #e77022
            Line(g, cx, cy - r, cx + r * 0.7f, cy, "#cb621e", lw);
            Line(g, cx + r * 0.7f, cy, cx, cy + r, "#cb621e", lw);
            Line(g, cx, cy + r, cx - r * 0.7f, cy, "#cb621e", lw);
            Line(g, cx - r * 0.7f, cy, cx, cy - r, "#cb621e", lw);
            Grain(g, new[] { "#234874", "#183556" }, 220, 0.10f, 426);
        } },

        // The DECONTAMINATION sign: dark plate #101c18 with the orange word. Keeps a soft glow.
        ["mDecSign"] = new Spec { Unit = 1.7f, Emissive = 0.35f, Draw = g => {
            Fill(g, "#101c18");
            Stencil(g, "DECONTAMINATION", g.W / 2f, g.H * 0.54f, g.H * 0.58f,
                    "#e8721c", 1f, g.W * 0.94f);
            Grain(g, new[] { "#16241f", "#0a1410" }, 140, 0.10f, 427);
        } },

        // Dead-end diagonal inner face: the brown wall band #66413c with its panel seams #4a2c29.
        ["mDecSignWall"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#593935");                                             // from #66413c
            for (float x = 0; x <= g.W; x += g.W / 2.5f) Line(g, x, 0, x, g.H, "#412824", 2, 0.6f);
            Line(g, 0, 2, g.W, 2, "#75524c", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#3a2420", 3);
            Grain(g, new[] { "#5f3d38", "#523430" }, 420, 0.09f, 428);
        } },

        // The long diagonal's inner face carries "<- REACTOR": orange #ff6d00 with the arrow left
        // of it. One unit = the 0.985 wall length.
        ["mDecSignReactor"] = new Spec { Unit = 0.985f, Draw = g => {
            Fill(g, "#593935");
            for (float x = 0; x <= g.W; x += g.W / 2.5f) Line(g, x, 0, x, g.H, "#412824", 2, 0.6f);
            float cy = g.H * 0.52f, aw = g.W * 0.22f, lw = MathF.Max(2f, g.H * 0.05f);
            Line(g, g.W * 0.10f, cy, g.W * 0.10f + aw, cy, "#ff6d00", lw);  // the left arrow
            Line(g, g.W * 0.10f + aw * 0.45f, cy - g.H * 0.13f, g.W * 0.10f, cy, "#ff6d00", lw);
            Line(g, g.W * 0.10f, cy, g.W * 0.10f + aw * 0.45f, cy + g.H * 0.13f, "#ff6d00", lw);
            StencilLeft(g, "REACTOR", g.W * 0.38f, cy, g.H * 0.26f, "#ff6d00", 1f, g.W * 0.58f);
            Grain(g, new[] { "#5f3d38", "#523430" }, 300, 0.09f, 429);
        } },

        // The sign wall's south face: pale "LABORATORY" plus an arrow, and the small lit wall box.
        // One unit = the whole 1.4597 wall width, so canvas-x sits at its measured world x
        // (canvas-x 0 = west, canvas-y 0 = wall top).
        ["mDecSignLabWall"] = new Spec { Unit = 1.4597f, Draw = g => {
            Fill(g, "#593935");
            float Ux(float x) => (x - 5.8953f) / 1.4597f * g.W;             // world x -> canvas px
            Line(g, Ux(6.10f), 0, Ux(6.10f), g.H, "#412824", 2, 0.6f);
            Line(g, Ux(6.65f), 0, Ux(6.65f), g.H, "#412824", 2, 0.6f);
            StencilLeft(g, "LABORATORY", Ux(6.05f), g.H * 0.34f, g.H * 0.16f,
                        "#9d8f8a", 1f, g.W * 0.42f);                        // from #846863, lifted
            float lw = MathF.Max(2f, g.H * 0.035f);                         // the pale east arrow
            Line(g, Ux(6.55f), g.H * 0.20f, Ux(7.15f), g.H * 0.20f, "#a9bcc8", lw);
            Line(g, Ux(7.15f), g.H * 0.20f, Ux(7.15f) - g.H * 0.08f, g.H * 0.12f, "#a9bcc8", lw);
            Line(g, Ux(7.15f), g.H * 0.20f, Ux(7.15f) - g.H * 0.08f, g.H * 0.28f, "#a9bcc8", lw);
            Rect(g, Ux(6.02f), g.H * 0.40f, Ux(6.22f) - Ux(6.02f), g.H * 0.22f, "#2a1a18");  // rim
            Rect(g, Ux(6.02f) + 2, g.H * 0.42f, Ux(6.22f) - Ux(6.02f) - 4, g.H * 0.18f, "#846563");
            Grain(g, new[] { "#5f3d38", "#523430" }, 500, 0.09f, 430);
        } },

        // Corridor floor: dark brown plate #473e3c with big quiet seams.
        ["mDecCorrFloor"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#3e3634");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#332c2a", 2, 0.6f);
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#332c2a", 2, 0.6f);
            Grain(g, new[] { "#443b39", "#38312f" }, 450, 0.10f, 431);
        } },

        // Call-button pedestal: teal #3a7f76 with the dark base #052a29.
        ["mDecPanel"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#336f68");
            Line(g, 0, g.H * 0.25f, g.W, g.H * 0.25f, "#265a55", 2, 0.6f);  // collar ring
            Line(g, 0, 2, g.W, 2, "#4a8a80", 2);
            Grain(g, new[] { "#3a7f76", "#2b615b" }, 160, 0.10f, 432);
        } },

        // The button itself: glowing orange #ef8415. Keeps its measured glow.
        ["mDecBtnGlow"] = new Spec { Unit = 0.2f, Emissive = 1.0f, Draw = g => {
            Fill(g, "#c65c11");
            float r = MathF.Min(g.W, g.H) * 0.32f;                          // hot top, from #ef8415
            g.FillEllipse(g.W / 2f, g.H * 0.42f, r, r, C("#ff9a2e"));
        } },

        // The green machine crate at the dead end: #3f615a body, lid edge #4b6d64.
        ["mDecCrate"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#37554f");
            Line(g, 0, g.H * 0.3f, g.W, g.H * 0.3f, "#2a443f", 2, 0.6f);    // lid seam
            Line(g, 0, 2, g.W, 2, "#4b6d64", 2);
            Grain(g, new[] { "#3f615a", "#2e4c44" }, 260, 0.10f, 433);
        } },

        // Structural dark: the door-housing mass and arch tops, from the east band #242429.
        ["mDecDark"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#1e1f24");
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#15161a", 2, 0.45f);
            Grain(g, new[] { "#24252b", "#18191d" }, 400, 0.10f, 434);
        } },

        // Ceiling (invented - a plan view has none): cool grey panel to sit over the mint and
        // yellow.
        ["mDecCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#565b62");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#474c53", 2, 0.6f);
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#474c53", 2, 0.6f);
            Grain(g, new[] { "#5c6169", "#4e535a" }, 400, 0.09f, 435);
        } },

        // THE HALF-DISC CAP at the south corridor mouth: glowing yellow #f4e84f with orange
        // #ef6306 concentric arcs, one material per stepped band. Arc radii 0.30 / 0.64 / 0.94.
        ["mDecCapA"] = new Spec { Unit = 1.8592f, Emissive = 0.55f,
            Draw = g => CapBand(g, new[] { 0.30f, 0.64f, 0.94f }, true, 1.8592f, 441) },
        ["mDecCapB"] = new Spec { Unit = 1.6628f, Emissive = 0.55f,
            Draw = g => CapBand(g, new[] { 0.64f, 0.94f }, true, 1.6628f, 442) },
        ["mDecCapC"] = new Spec { Unit = 1.27f, Emissive = 0.55f,
            Draw = g => CapBand(g, new[] { 0.94f }, false, 1.27f, 443) },
        ["mDecCapD"] = new Spec { Unit = 0.5484f, Emissive = 0.55f,
            Draw = g => CapBand(g, new[] { 0.94f }, false, 0.5484f, 444) },

        // ============================================================ Admin (mAdm*)
        // surfaces_mira_admin.js. The room's own art bakes a light gradient into the carpet (lit
        // cells #4a6952 in the south, shadowed #334d3d in the north); the LIT reading is the
        // material and the gradient belongs to the lamps.

        // THE FLOOR: dark green carpet with the diamond-grid rattle pattern. Dark crossings every
        // 0.25 map units on both lattice families, so one diamond cell is 0.25 x 0.25, drawn 2x2
        // per tile. Cell #4a6952; lines #273435 / #2f413e, broad - about a third of the period.
        ["mAdmCarpet"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#415c48");                                             // cell, from #4a6952
            float p = g.W / 2f;                                             // the diamond lattice
            foreach (var pass in new[] { ("#2a3a34", 22f, 0.5f), ("#222d2b", 13f, 1f) })
                for (int c = -1; c <= 3; c++)
                {
                    Line(g, c * p - p, -4, c * p + p, g.H + 4, pass.Item1, pass.Item2, pass.Item3);
                    Line(g, c * p + p, -4, c * p - p, g.H + 4, pass.Item1, pass.Item2, pass.Item3);
                }
            // the pale felt speckles the drawing scatters over the cells
            Grain(g, new[] { "#4d6e52", "#58795c", "#39543f" }, 700, 0.16f, 451);
        } },

        // Room wall panel: dark blue-grey steel. Field from the north band's lit niche face
        // #3e4a51 - the east/west folds read darker (#1a1a1f) because the art bakes them into
        // shadow; one material, and the lamps divide them again.
        ["mAdmWall"] = new Spec { Unit = 1.4f, Draw = g => {
            Fill(g, "#374147");                                             // from #3e4a51
            foreach (float f in new[] { 1f / 3f, 2f / 3f })
            {
                Line(g, 0, g.H * f, g.W, g.H * f, "#28303a", 2);
                Line(g, 0, g.H * f + 3, g.W, g.H * f + 3, "#414c55", 2, 0.5f);
            }
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#2b333c", 2);                // butt joint
            Grain(g, new[] { "#3d4850", "#2f3940" }, 500, 0.10f, 452);
        } },

        // Outer hull, wall caps and the corridor side's dark plinth: near-black violet-grey,
        // cap #23232a.
        ["mAdmHull"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#1f1f25");
            for (float x = 0; x < g.W; x += 32) Line(g, x, 0, x, g.H, "#17171c", 2, 0.6f);
            Grain(g, new[] { "#26262d", "#191920" }, 400, 0.10f, 453);
        } },

        // Ceiling: follows the room's steel - mid grey-green panel with a seam grid, bright
        // enough that the lamps do not swallow the room.
        ["mAdmCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#565e5a");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#454c4a", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#454c4a", 3);
            Grain(g, new[] { "#606864", "#4c5450" }, 400, 0.10f, 454);
        } },

        // THE OCTAGON TABLE's silver rim: #cfdaea on the lit north-west rim, #b4c3d2 mid, shading
        // to #8b9fb0 towards the south-east. Drawn as rimmed plates so the tiling reads as the
        // bevelled segments the sprite shows.
        ["mAdmTableSilver"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#a7b3c2");
            g.StrokeRoundRect(1.5f, 1.5f, g.W - 3, g.H - 3, 0, 3, C("#7e8b9a"));    // bevel edge
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#93a0b0", 2, 0.4f);        // segment seam
            Grain(g, new[] { "#b2bfcd", "#98a5b4" }, 400, 0.08f, 455);
        } },

        // THE FACILITY MAP on the table top - the glowing green disc. Field #84d394, a fine darker
        // grid and the Mira HQ floor plan as near-white schematic lines. ONE tile per disc: the
        // drum's cap maps the whole canvas onto the circle, so the square's corners are clipped
        // away exactly like the drawn disc.
        ["mAdmMapTop"] = new Spec { Unit = 4.0f, Emissive = 0.5f, Draw = g => {
            Fill(g, "#74ba82");                                             // field, from #84d394
            for (float i = 0; i <= g.W; i += 7.4f)                          // ~0.1 map units
            {
                Line(g, i, 0, i, g.H, "#1c5a30", 1, 0.28f);
                Line(g, 0, i, g.W, i, "#1c5a30", 1, 0.28f);
            }
            // the station schematic, white on green: corridors as a Y-tree, rooms as outlined
            // blobs - drawn after the close-up crop, not traced
            var run = new[] { (14f, 98f), (14f, 62f), (36f, 62f), (36f, 42f), (58f, 42f),
                              (58f, 18f), (88f, 18f), (88f, 46f), (102f, 46f), (102f, 68f),
                              (118f, 68f) };
            for (int i = 0; i + 1 < run.Length; i++)
                Line(g, run[i].Item1, run[i].Item2, run[i + 1].Item1, run[i + 1].Item2, "#e8f6e8", 2.5f);
            Line(g, 58, 42, 58, 58, "#e8f6e8", 2.5f); Line(g, 58, 58, 78, 58, "#e8f6e8", 2.5f);
            Line(g, 36, 62, 36, 78, "#e8f6e8", 2.5f); Line(g, 36, 78, 52, 78, "#e8f6e8", 2.5f);
            foreach (var r in new[] { (20f, 66f, 26f, 22f), (62f, 22f, 20f, 16f),
                                      (64f, 62f, 30f, 14f), (70f, 80f, 40f, 22f),
                                      (18f, 100f, 30f, 12f) })
                g.StrokeRoundRect(r.Item1, r.Item2, r.Item3, r.Item4, 0, 2, C("#e8f6e8"));
            Rect(g, 21, 67, 24, 20, "#ffffff", 0.10f);                      // faint floor fill
            Rect(g, 63, 63, 28, 12, "#ffffff", 0.10f);
            Rect(g, 71, 81, 38, 20, "#ffffff", 0.10f);
            Grain(g, new[] { "#7ec491", "#6cb178" }, 300, 0.08f, 456);
        } },

        // THE CHART SCREEN on the north wall: dark frame #27353b, blue-teal chart field #4988b0
        // with a darker grid and pale star specks / faint streaks. One tile per screen.
        ["mAdmChart"] = new Spec { Unit = 1.55f, Emissive = 0.5f, Draw = g => {
            Fill(g, "#233039");                                             // frame, from #27353b
            Rect(g, g.W * 0.045f, g.H * 0.075f, g.W * 0.91f, g.H * 0.85f, "#40789a");
            for (float x = g.W * 0.045f; x <= g.W * 0.955f; x += g.W * 0.065f)
                Line(g, x, g.H * 0.075f, x, g.H * 0.925f, "#2c5a7c", 1, 0.35f);
            for (float y = g.H * 0.075f; y <= g.H * 0.925f; y += g.H * 0.11f)
                Line(g, g.W * 0.045f, y, g.W * 0.955f, y, "#2c5a7c", 1, 0.35f);
            for (int i = 0; i < 46; i++)                                    // star specks
            {
                float s = 1 + (i % 3);
                Rect(g, g.W * (0.07f + 0.86f * ((i * 37) % 100) / 100f),
                     g.H * (0.11f + 0.78f * ((i * 61) % 100) / 100f), s, s,
                     i % 5 != 0 ? "#c8e0e8" : "#f0f8fa", i % 5 != 0 ? 0.75f : 0.9f);
            }
            Line(g, g.W * 0.12f, g.H * 0.72f, g.W * 0.34f, g.H * 0.50f, "#bedce6", 1, 0.5f);
            Line(g, g.W * 0.34f, g.H * 0.50f, g.W * 0.52f, g.H * 0.58f, "#bedce6", 1, 0.5f);
            Line(g, g.W * 0.60f, g.H * 0.30f, g.W * 0.80f, g.H * 0.42f, "#bedce6", 1, 0.5f);
        } },

        // THE GREEN SCHEMATIC BOARD under the chart screen: grey cabinet with two bright green
        // readout bars, dashes like text, a small dark sub-screen with a red lamp. Bars #5bb978 /
        // #7bd297; frame #1e2529.
        ["mAdmGreenBoard"] = new Spec { Unit = 0.84f, Emissive = 0.45f, Draw = g => {
            Fill(g, "#232b29");
            Rect(g, g.W * 0.04f, g.H * 0.05f, g.W * 0.92f, g.H * 0.90f, "#3a4644");
            foreach (var b in new[] { (0.14f, 0.30f), (0.52f, 0.30f) })     // the two green bars
            {
                Rect(g, g.W * 0.10f, g.H * b.Item1, g.W * 0.80f, g.H * b.Item2, "#50a269");
                Rect(g, g.W * 0.10f, g.H * b.Item1, g.W * 0.80f, 2, "#6cb985");   // lit top edge
                for (int i = 0; i < 9; i++)                                 // text-like dashes
                {
                    float dw = g.W * (0.04f + ((i * 29) % 7) * 0.012f);
                    Rect(g, g.W * (0.13f + i * 0.085f),
                         g.H * (b.Item1 + 0.06f + (i % 2) * 0.10f), dw, g.H * 0.07f, "#2e6a44");
                }
            }
            Rect(g, g.W * 0.045f, g.H * 0.18f, g.W * 0.055f, g.H * 0.5f, "#14201b");  // sub-screen
            Rect(g, g.W * 0.06f, g.H * 0.62f, 3, 3, "#c03830");             // its red lamp
        } },

        // PRIME SHIELDS - the hexagon panel on the table's south console. MEASURED, NOT the
        // brief's red: sprite and atlas both give BLUE-violet hexagons #3962b9 / #8da3d6 /
        // #203c7d / #162e66 on black. Seven hexes in a flower, lighter top facet.
        ["mAdmHex"] = new Spec { Unit = 0.37f, Emissive = 0.4f, Draw = g => {
            Fill(g, "#0b0e13");                                             // surround
            float R = g.W * 0.155f, cx = g.W / 2f, cy = g.H / 2f;
            // A pointy-top hexagon, filled as its two halves - FillQuad is convex-only, and any
            // four consecutive corners of a convex hexagon are a convex quad.
            void Hex(float x, float y, float r, string col, float a)
            {
                var vx = new float[6]; var vy = new float[6];
                for (int i = 0; i < 6; i++)
                {
                    float ang = MathF.PI / 6f + i * MathF.PI / 3f;
                    vx[i] = x + r * MathF.Cos(ang); vy[i] = y + r * MathF.Sin(ang);
                }
                var c = C(col);
                g.FillQuad(vx[0], vy[0], vx[1], vy[1], vx[2], vy[2], vx[3], vy[3], c, a);
                g.FillQuad(vx[3], vy[3], vx[4], vy[4], vx[5], vy[5], vx[0], vy[0], c, a);
            }
            void HexEdge(float x, float y, float r, string col, float t)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a0 = MathF.PI / 6f + i * MathF.PI / 3f, a1 = a0 + MathF.PI / 3f;
                    Line(g, x + r * MathF.Cos(a0), y + r * MathF.Sin(a0),
                         x + r * MathF.Cos(a1), y + r * MathF.Sin(a1), col, t);
                }
            }
            var ring = new System.Collections.Generic.List<(float, float)> { (0f, 0f) };
            for (int i = 0; i < 6; i++)                                     // six around one
            {
                float a = i * MathF.PI / 3f;
                ring.Add((MathF.Cos(a) * R * 1.72f, MathF.Sin(a) * R * 1.72f));
            }
            foreach (var (dx, dy) in ring)
            {
                Hex(cx + dx, cy + dy, R, "#2c4c96", 1f);                    // from #3962b9/#203c7d
                HexEdge(cx + dx, cy + dy, R, "#0a1228", 1.5f);
                Hex(cx + dx - R * 0.18f, cy + dy - R * 0.22f, R * 0.62f, "#8da3d6", 0.55f);
            }
        } },

        // ENTER ID CODE - the wallet on the table's west rim: green wallet #68c182, lighter card
        // slots #84d393, dark seams.
        ["mAdmWallet"] = new Spec { Unit = 0.31f, Draw = g => {
            Fill(g, "#4f9a63");                                             // leather, from #68c182
            g.StrokeRoundRect(2.5f, 2.5f, g.W - 5, g.H - 5, 0, 3, C("#24402e"));    // stitching
            foreach (var p in new[] { (0.18f, 0.12f), (0.56f, 0.12f), (0.18f, 0.56f), (0.56f, 0.56f) })
            {
                Rect(g, g.W * p.Item1, g.H * p.Item2, g.W * 0.26f, g.H * 0.32f, "#6fbd85");
                g.StrokeRoundRect(g.W * p.Item1, g.H * p.Item2, g.W * 0.26f, g.H * 0.32f, 0, 2,
                                  C("#2c4a34"));
            }
            Grain(g, new[] { "#5aa870", "#458a58" }, 250, 0.10f, 457);
        } },

        // The tall equipment cabinet on the north wall: grey-blue housing #738088 / #626c73, a row
        // of red/yellow/white buttons, a dark green inset screen, double doors below. One tile for
        // the whole 0.69 x 2.0 face.
        ["mAdmCab"] = new Spec { Unit = 2.0f, Draw = g => {
            Fill(g, "#657178");                                             // housing, from #738088
            g.StrokeRoundRect(2, 2, g.W - 4, g.H - 4, 0, 4, C("#4a545c"));
            g.StrokeRoundRect(g.W * 0.10f, g.H * 0.06f, g.W * 0.36f, g.H * 0.20f, 0, 2, C("#525d66"));
            Line(g, g.W * 0.10f, g.H * 0.06f, g.W * 0.46f, g.H * 0.26f, "#525d66", 2);
            Line(g, g.W * 0.46f, g.H * 0.06f, g.W * 0.10f, g.H * 0.26f, "#525d66", 2);
            var btn = new[] { "#b8433a", "#d8b23a", "#cfd6da", "#b8433a", "#d8b23a" };
            for (int i = 0; i < 5; i++)                                     // the button row
                Rect(g, g.W * (0.56f + i * 0.085f), g.H * 0.075f, g.W * 0.06f, g.H * 0.02f, btn[i]);
            Rect(g, g.W * 0.56f, g.H * 0.115f, g.W * 0.34f, g.H * 0.135f, "#2c4a3a");   // inset
            g.StrokeRoundRect(g.W * 0.585f, g.H * 0.135f, g.W * 0.29f, g.H * 0.05f, 0, 1, C("#3f5a4c"));
            for (int i = 0; i < 4; i++)                                     // louvre strip
                Line(g, g.W * 0.10f, g.H * (0.32f + i * 0.035f), g.W * 0.9f,
                     g.H * (0.32f + i * 0.035f), "#59646d", 2, 0.6f);
            Line(g, g.W / 2f, g.H * 0.48f, g.W / 2f, g.H * 0.96f, "#454f58", 3);   // door seam
            g.StrokeRoundRect(g.W * 0.10f, g.H * 0.48f, g.W * 0.36f, g.H * 0.48f, 0, 2, C("#525d66"));
            g.StrokeRoundRect(g.W * 0.54f, g.H * 0.48f, g.W * 0.36f, g.H * 0.48f, 0, 2, C("#525d66"));
            Rect(g, g.W * 0.42f, g.H * 0.68f, g.W * 0.03f, g.H * 0.08f, "#3a444c");   // handles
            Rect(g, g.W * 0.55f, g.H * 0.68f, g.W * 0.03f, g.H * 0.08f, "#3a444c");
            Grain(g, new[] { "#6d7980", "#5b666e" }, 400, 0.10f, 458);
        } },

        // ============================================================ Office (mOff*)
        // surfaces_mira_office.js. Screens carry Emissive where the drawing shows them lit.

        // THE FLOOR: white herringbone. Two alternating warm whites laid as big chevron bands -
        // light #e8e4df, dark #dbd8cf, joints #bcb8b4. Band height and zigzag period measured 0.52
        // in the atlas crop, slopes at 45 degrees. The joints stay discreet.
        ["mOffFloor"] = new Spec { Unit = 1.04f, Draw = g => {
            float band = g.H / 2f;
            Fill(g, "#c1bfb6");                                             // dark band
            Rect(g, 0, 0, g.W, band, "#cbc8c1");                            // light band
            for (int row = 0; row < 2; row++)                               // the zigzag joints
            {
                float y0 = row * band;
                for (float x = -band; x < g.W + band; x += band)
                {
                    Line(g, x, y0 + band, x + band / 2f, y0, "#a6a29e", 2, 0.75f);
                    Line(g, x + band / 2f, y0, x + band, y0 + band, "#a6a29e", 2, 0.75f);
                }
            }
            Line(g, 0, band, g.W, band, "#a6a29e", 2, 0.75f);
            Grain(g, new[] { "#d3d0c8", "#b8b5ac" }, 500, 0.10f, 461);
        } },

        // Room wall panel: the muted steel blue of the north band's face, #415574. The
        // east/west/south folds read darker in the atlas because the art bakes them into shadow -
        // one material, and the lamps divide them again.
        ["mOffWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#394b66");                                             // from #415574
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#2c3a52", 2);                // butt joint
            Line(g, 1, 0, 1, g.H, "#435676", 2, 0.4f);
            Grain(g, new[] { "#40536f", "#33435c" }, 450, 0.10f, 462);
        } },

        // The dark wall folds (west/east/south faces), #23232a.
        ["mOffWallDark"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#1f1f25");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#17171c", 2);
            Grain(g, new[] { "#24242b", "#1a1a20" }, 400, 0.10f, 463);
        } },

        // Wall caps, end faces, the south plinth: near-black, #161619.
        ["mOffHull"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#141416");
            for (float x = 0; x < g.W; x += 32) Line(g, x, 0, x, g.H, "#0f0f12", 2, 0.6f);
            Grain(g, new[] { "#1a1a1d", "#101013" }, 400, 0.10f, 464);
        } },

        // Ceiling: follows the room's steel panel, bright enough that the lamps do not swallow it.
        ["mOffCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#5a6058");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#494f48", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#494f48", 3);
            Grain(g, new[] { "#646a62", "#50564f" }, 400, 0.10f, 465);
        } },

        // Desk wood: mid warm brown, #524139 / #4a3a30.
        ["mOffDesk"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#483a32");
            for (float y = 6; y < g.H; y += 14) Line(g, 0, y, g.W, y, "#3c2f28", 1, 0.35f);
            Grain(g, new[] { "#52423a", "#3e312a" }, 450, 0.10f, 466);
        } },

        // Office chair upholstery: darker warm brown, from the desk family #42302b.
        ["mOffChair"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#3a2b26");
            Grain(g, new[] { "#44332c", "#31241f" }, 300, 0.12f, 467);
        } },

        // The pale chair mat under each bay chair, from the floor's joint tone.
        ["mOffMat"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#b5b3af");
            g.StrokeRoundRect(1.5f, 1.5f, g.W - 3, g.H - 3, 0, 3, C("#9b9894"));
            Grain(g, new[] { "#bfbdb9", "#a8a6a2" }, 300, 0.08f, 468);
        } },

        // THE EAST DOOR leaves: blue-grey steel #566174 with the dark parting seam #3d4552. The
        // dark canvas edges read as the parting seam where the leaves meet and as the jamb shadow
        // where they do not.
        ["mOffDoor"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#4c5666");
            Line(g, 0, 0, 0, g.H, "#363c48", 3);                            // parting seam / jamb
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#363c48", 3);
            foreach (float f in new[] { 0.3f, 0.7f }) Line(g, 0, g.H * f, g.W, g.H * f, "#404a58", 2);
            Grain(g, new[] { "#545e70", "#434c5c" }, 300, 0.10f, 469);
        } },

        // The floor vent's frame metal, #8098a1; AreaKit draws its own slats.
        ["mOffVent"] = new Spec { Unit = 0.45f, Draw = g => {
            Fill(g, "#71868d");
            g.StrokeRoundRect(1.5f, 1.5f, g.W - 3, g.H - 3, 0, 3, C("#5a6a72"));
            Grain(g, new[] { "#7b9098", "#65787f" }, 250, 0.10f, 470);
        } },

        // THE WOBBLE BALL: bright orange #ff6d00 with the horizontal wobble ridges the sprite
        // shows, shading #c74c03. One ridge row per eighth.
        ["mOffBall"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#e05f00");
            for (int i = 1; i < 8; i += 2)
            {
                Line(g, 0, g.H * i / 8f, g.W, g.H * i / 8f, "#ae4300", 3, 0.55f);
                Line(g, 0, g.H * i / 8f + 2, g.W, g.H * i / 8f + 2, "#f0781a", 2, 0.3f);
            }
            Grain(g, new[] { "#ef6c08", "#c85500" }, 300, 0.10f, 471);
        } },

        // Don Dew bottles: green #3f7533 and red #8f3e40, each with a pale label.
        ["mOffBottleGreen"] = new Spec { Unit = 0.14f, Draw = g => {
            Fill(g, "#3f7533");
            Rect(g, 0, g.H * 0.35f, g.W, g.H * 0.3f, "#5d9a4a");
            Grain(g, new[] { "#4a8540", "#356329" }, 150, 0.12f, 472);
        } },
        ["mOffBottleRed"] = new Spec { Unit = 0.14f, Draw = g => {
            Fill(g, "#8f3e40");                                             // from #a34648
            Rect(g, 0, g.H * 0.35f, g.W, g.H * 0.3f, "#b56a5e");
            Grain(g, new[] { "#9c474a", "#7c3336" }, 150, 0.12f, 473);
        } },

        // The Divert Power machine housing, blue-grey steel #525f73.
        ["mOffMachine"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#485365");
            g.StrokeRoundRect(2, 2, g.W - 4, g.H - 4, 0, 3, C("#3a4454"));
            Line(g, 0, g.H * 0.18f, g.W, g.H * 0.18f, "#3a4454", 2);
            Line(g, 0, g.H * 0.18f + 3, g.W, g.H * 0.18f + 3, "#5a6a82", 2, 0.4f);
            Grain(g, new[] { "#525f74", "#3f4858" }, 350, 0.10f, 474);
        } },

        // ACCEPT DIVERTED POWER - the machine's pale screen #cbdade with dark dash rows.
        ["mOffDivert"] = new Spec { Unit = 0.26f, Emissive = 0.5f, Draw = g => {
            Fill(g, "#b3c0c3");                                             // from #cbdade
            for (int i = 0; i < 4; i++)
                Rect(g, g.W * 0.12f, g.H * (0.16f + i * 0.2f),
                     g.W * (0.5f + (i % 2) * 0.2f), g.H * 0.09f, "#3c4a54");
            Rect(g, g.W * 0.76f, g.H * 0.2f, g.W * 0.1f, g.H * 0.1f, "#872c22");   // status lamp
            Grain(g, new[] { "#c2ced2", "#a2b0b5" }, 150, 0.08f, 475);
        } },

        // PROCESS DATA - the DataConsole monitor: blue field #2c68a5 with the small tan rectangle
        // #af9675, dark bezel.
        ["mOffData"] = new Spec { Unit = 0.52f, Emissive = 0.5f, Draw = g => {
            Fill(g, "#1a1e22");                                             // bezel
            Rect(g, g.W * 0.06f, g.H * 0.09f, g.W * 0.88f, g.H * 0.82f, "#275b91");
            Rect(g, g.W * 0.40f, g.H * 0.40f, g.W * 0.2f, g.H * 0.2f, "#998467");  // tan inset
            for (float y = g.H * 0.09f; y < g.H * 0.91f; y += g.H * 0.12f)
                Line(g, g.W * 0.06f, y, g.W * 0.94f, y, "#1d4570", 1, 0.5f);
        } },

        // The dashboard monitor (left bay, south desk): pale blue-grey field with the coloured
        // blocks of the drawing.
        ["mOffScreenDash"] = new Spec { Unit = 0.44f, Emissive = 0.45f, Draw = g => {
            Fill(g, "#1a1e22");                                             // bezel
            Rect(g, g.W * 0.07f, g.H * 0.1f, g.W * 0.86f, g.H * 0.8f, "#8fb4c4");  // pale field
            Rect(g, g.W * 0.13f, g.H * 0.5f, g.W * 0.5f, g.H * 0.16f, "#6aa84f");  // green row
            Rect(g, g.W * 0.13f, g.H * 0.28f, g.W * 0.22f, g.H * 0.14f, "#e8b33c"); // yellow block
            Rect(g, g.W * 0.4f, g.H * 0.28f, g.W * 0.14f, g.H * 0.14f, "#cc4b3c");  // red block
            Rect(g, g.W * 0.7f, g.H * 0.2f, g.W * 0.16f, g.H * 0.6f, "#37545e");    // sidebar
        } },

        // The code/terminal monitor (tilted in the drawing): near-black with green specks.
        ["mOffScreenCode"] = new Spec { Unit = 0.30f, Emissive = 0.4f, Draw = g => {
            Fill(g, "#161a1e");
            for (int i = 0; i < 60; i++)
                Rect(g, g.W * (0.08f + 0.84f * ((i * 37) % 100) / 100f),
                     g.H * (0.1f + 0.8f * ((i * 61) % 100) / 100f), 2, 1.5f,
                     i % 4 != 0 ? "#6ea06e" : "#d2e1d2", i % 4 != 0 ? 0.8f : 0.85f);
        } },

        // FixComms' green strip screens, #465d24 with brighter dash rows.
        ["mOffScreenGreen"] = new Spec { Unit = 0.22f, Emissive = 0.5f, Draw = g => {
            Fill(g, "#232a20");
            foreach (float yf in new[] { 0.2f, 0.55f })
                for (int i = 0; i < 6; i++)
                    Rect(g, g.W * (0.08f + i * 0.15f), g.H * yf, g.W * 0.1f, g.H * 0.22f, "#5f9a3c");
            Grain(g, new[] { "#4a6b30", "#2c3524" }, 120, 0.12f, 476);
        } },

        // The little side-table screen, dark blue-grey #293c52, barely lit.
        ["mOffScreenDim"] = new Spec { Unit = 0.28f, Emissive = 0.3f, Draw = g => {
            Fill(g, "#1a1e22");
            Rect(g, g.W * 0.08f, g.H * 0.12f, g.W * 0.84f, g.H * 0.76f, "#24354a");
            for (float y = g.H * 0.2f; y < g.H * 0.85f; y += g.H * 0.18f)
                Line(g, g.W * 0.12f, y, g.W * 0.8f, y, "#3a5474", 1, 0.5f);
        } },

        // FIX LIGHTS: the dark backplate #303b38 / #131715 with the lighter inner field #607068.
        ["mOffPanelDark"] = new Spec { Unit = 0.56f, Draw = g => {
            Fill(g, "#2a3431");
            Rect(g, g.W * 0.08f, g.H * 0.06f, g.W * 0.84f, g.H * 0.88f, "#55635c");  // inner field
            g.StrokeRoundRect(1.5f, 1.5f, g.W - 3, g.H - 3, 0, 3, C("#1c211e"));
            for (int i = 0; i < 3; i++)                                     // the switch column
                Rect(g, g.W * 0.14f, g.H * (0.16f + i * 0.26f), g.W * 0.72f, g.H * 0.12f, "#454f4a");
            Grain(g, new[] { "#5d6b64", "#4a5751" }, 250, 0.10f, 477);
        } },

        // The yellow warning sign on it, #f9ca42 - black triangle with a knocked-out mark.
        ["mOffSign"] = new Spec { Unit = 0.26f, Emissive = 0.3f, Draw = g => {
            Fill(g, "#dcb23a");                                             // from #f9ca42
            Tri(g, g.W * 0.5f, g.H * 0.16f, g.W * 0.86f, g.H * 0.78f, g.W * 0.14f, g.H * 0.78f,
                "#15130c");
            Rect(g, g.W * 0.47f, g.H * 0.34f, g.W * 0.06f, g.H * 0.26f, "#dcb23a");
            Rect(g, g.W * 0.47f, g.H * 0.64f, g.W * 0.06f, g.H * 0.07f, "#dcb23a");
        } },

        // THE HENRY STICKMIN POSTER, blue #245892: two red strokes up top, the big white round
        // head with its goggle band, the pale banner at the bottom - simplified from the crop.
        ["mOffPoster"] = new Spec { Unit = 0.37f, Draw = g => {
            Fill(g, "#1f4e80");                                             // field, from #245892
            Line(g, g.W * 0.14f, g.H * 0.26f, g.W * 0.34f, g.H * 0.10f, "#c03830", 4);
            Line(g, g.W * 0.86f, g.H * 0.26f, g.W * 0.62f, g.H * 0.08f, "#c03830", 4);
            g.FillEllipse(g.W * 0.5f, g.H * 0.42f, g.W * 0.26f, g.W * 0.26f, C("#e8ecee"));
            Rect(g, g.W * 0.28f, g.H * 0.36f, g.W * 0.44f, g.H * 0.07f, "#20242a");  // goggle band
            Rect(g, g.W * 0.38f, g.H * 0.5f, g.W * 0.05f, g.H * 0.05f, "#20242a");
            Rect(g, g.W * 0.57f, g.H * 0.5f, g.W * 0.05f, g.H * 0.05f, "#20242a");
            Rect(g, g.W * 0.06f, g.H * 0.76f, g.W * 0.88f, g.H * 0.14f, "#dfe4e6");  // banner
            Grain(g, new[] { "#2a5c92", "#1a4470" }, 200, 0.10f, 478);
        } },

        // The standing lamp's pale metal, #75828b, head tone a shade lighter.
        ["mOffLamp"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#8b9da4");
            Line(g, 0, g.H * 0.4f, g.W, g.H * 0.4f, "#a9bcc2", 2, 0.4f);
            Grain(g, new[] { "#97a9b0", "#7c8e95" }, 200, 0.10f, 479);
        } },

        // ============================================================ Laboratory (mLab*)
        // surfaces_mira_laboratory.js.
        //
        // THE FLOOR, in detail: a two-tone 45-degree diagonal tile, period 0.90 on both axes. Dark
        // field #dcf7f7, lit band #e4ffff, and the seam between them is simply the dark field
        // again - there is no third colour. The engine anchors a floor texture at its own rect
        // corner, so the seam's phase relative to the original's world grid cannot be honoured;
        // period and direction are.

        // THE FLOOR: pale ice-blue diagonals, one unit per 0.90 tile. Canvas up is north, so the
        // lit band runs SW->NE exactly as measured: lit where (canvas x + canvas y) mod U falls
        // outside the dark seam [0.65, 0.80], plus the grout row along the tile's north edge.
        ["mLabDiagTile"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#c1d9d9");                                             // dark field
            float a = 0.8f * g.W, b = 0.65f * g.W;
            // lit where t = (canvas x + canvas y) is in [0.80U, 1.65U] + [1.80U, 2U]
            g.FillQuad(a, 0, g.W, 0, g.W, g.H, 0, g.H, C("#c8e0e0"));
            Tri(g, a, 0, 0, g.H, 0, a, "#c8e0e0");
            Tri(g, 0, 0, b, 0, 0, b, "#c8e0e0");                            // t in [0, 0.65U]
            // the dark seam across the far corner: t in [1.65U, 1.80U]
            g.FillQuad(g.W, b, g.W, a, a, g.H, b, g.H, C("#c1d9d9"));
            Rect(g, 0, 0, g.W, MathF.Max(2f, MathF.Round(g.H * 0.05f)), "#c1d9d9");   // grout row
            Grain(g, new[] { "#c6dcdd", "#bcd2d3" }, 500, 0.08f, 481);
        } },

        // The room's wall panel: pale grey-teal, measured on the north band's face and over the
        // entrance - one material for every room-facing face.
        ["mLabWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#82a3a4");                                             // from #94babb
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#719092", 2, 0.45f);
            Line(g, 0, 2, g.W, 2, "#a5c4c5", 2);                            // bead under the cap
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#6d8b8c", 3);
            Grain(g, new[] { "#8babac", "#78999b" }, 500, 0.10f, 482);
        } },

        // The corridor side of the west sliver: the blue-grey panel the decon corridor looks at,
        // #427184, brighter centre #528294 near the top.
        ["mLabDeconPanel"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#3a6374");
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#2e5060", 2, 0.5f);
            Line(g, 0, 2, g.W, 2, "#4f7a8c", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#294a58", 3);
            Grain(g, new[] { "#40697a", "#355c6c" }, 400, 0.10f, 483);
        } },

        // Ceiling: bright cool panel with a seam grid, following the pale walls as in MedBay.
        ["mLabCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#c6cdd4");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#a6adb4", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#a6adb4", 3);
            Grain(g, new[] { "#d2d9df", "#bac1c8" }, 400, 0.10f, 484);
        } },

        // Counter tops: warm cream #efebde across the whole north run and the lab table.
        ["mLabCounter"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#d2cfc3");
            Line(g, 0, 1, g.W, 1, "#e0ddd1", 2);
            Grain(g, new[] { "#d8d5c9", "#c8c5b9" }, 400, 0.10f, 485);
        } },

        // Drawer fronts of the north bench: khaki #c6bead with the darker seam lines #8c8673, one
        // seam per 0.6 unit.
        ["mLabDrawer"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#aea798");
            Line(g, g.W - 2, 0, g.W - 2, g.H, "#7b7665", 2);                // the drawer gap
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#948c7c", 2);          // the pull rail
            Grain(g, new[] { "#b4ad9e", "#a29b8c" }, 400, 0.10f, 486);
        } },

        // The vitrine glass, blue #789bb5, and the sample tank's dark teal #39615a. Both are
        // transparent in the prototype so the trays behind them stay visible; here they come out
        // opaque, like every other pane in the mod (the rasteriser has no blending).
        ["mLabGlass"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#69889f");                                             // from #789bb5
            Line(g, g.W * 0.25f, 0, g.W * 0.25f, g.H, "#7d9cb2", 3, 0.35f); // the sheen streak
            Line(g, g.W * 0.65f, 0, g.W * 0.65f, g.H, "#7d9cb2", 2, 0.35f);
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 3, C("#55707f"));  // dark frame edge
        } },

        ["mLabTank"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#32554f");                                             // from #39615a
            Line(g, g.W * 0.3f, 0, g.W * 0.3f, g.H, "#41695f", 3, 0.4f);
            Grain(g, new[] { "#375c55", "#2d4e49" }, 300, 0.10f, 487);
        } },

        // Pale steel: the duct over the bench's east end (#dee7ef, bands #929fb3) and the grey
        // instrument bodies.
        ["mLabSteel"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#c3cbd2");
            Line(g, 0, g.H * 0.35f, g.W, g.H * 0.35f, "#808c9d", 3);
            Line(g, 0, g.H * 0.7f, g.W, g.H * 0.7f, "#98a4b1", 2);
            Grain(g, new[] { "#ccd4da", "#b6bec6" }, 400, 0.10f, 488);
        } },

        // Dark slate trim: vitrine caps, the artifact table, small crates (#3c4951).
        ["mLabDark"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#354047");
            Line(g, 0, 1, g.W, 1, "#45525c", 2);
            Grain(g, new[] { "#3b4750", "#2f3a41" }, 300, 0.10f, 489);
        } },

        // Tan fronts and crate boards: #b5ae9c.
        ["mLabTan"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#9f9888");
            Line(g, 0, 1, g.W, 1, "#aea794", 2);
            Grain(g, new[] { "#a7a08f", "#948d7d" }, 400, 0.10f, 490);
        } },

        // The south benches: brown #6b6858 with the dark #525142 edge.
        ["mLabBenchBrown"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#5e5b4d");
            Line(g, 0, 2, g.W, 2, "#6d6a5a", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#4b483c", 2);
            Grain(g, new[] { "#64614f", "#565344" }, 400, 0.10f, 491);
        } },

        // Cardboard: the boxes on the east arm, #82755a.
        ["mLabCardboard"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#726750");
            Line(g, 0, g.H * 0.45f, g.W, g.H * 0.45f, "#8a7c63", 3);        // the tape
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#5d553f"));  // the flap edge
            Grain(g, new[] { "#7a6f58", "#685e48" }, 400, 0.12f, 492);
        } },

        // Fire extinguishers: red #7b0400 body with the tan label band #855925.
        ["mLabExting"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#6c0400");
            Line(g, 0, g.H * 0.4f, g.W, g.H * 0.4f, "#754e20", MathF.Max(2f, MathF.Round(g.H * 0.16f)));
            Line(g, 0, 1, g.W, 1, "#8a2018", 2);
            Grain(g, new[] { "#760a04", "#5e0300" }, 300, 0.10f, 493);
        } },

        // The Sort Samples bins: three greys measured, #adadad (bins 1 and 3) and #8f8e8f (bin 2).
        ["mLabBin"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#989898");
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#1e1e1d"));  // the dark rim
            Grain(g, new[] { "#a2a2a2", "#8e8e8e" }, 250, 0.10f, 494);
        } },

        ["mLabBinDark"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#7d7c7d");
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#0f0f0f"));
            Grain(g, new[] { "#868586", "#737273" }, 250, 0.10f, 495);
        } },

        // Loose samples: the brown fossil #664d2e and the blue-grey gem #4d575b.
        ["mLabSampleBrown"] = new Spec { Unit = 0.2f, Draw = g => {
            Fill(g, "#5a4328");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#6d5436", 2);
            Grain(g, new[] { "#61492c", "#523d24" }, 200, 0.12f, 496);
        } },

        ["mLabSampleBlue"] = new Spec { Unit = 0.2f, Draw = g => {
            Fill(g, "#434c50");
            Line(g, 0, 1, g.W, 1, "#566267", 2);
            Grain(g, new[] { "#4a5459", "#3d4549" }, 200, 0.12f, 497);
        } },

        // Grey instrument bodies: the microscope (#848fa2 with #424952 trim) and the L-bench
        // device (#636563).
        ["mLabScope"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#747e8f");
            Line(g, 0, g.H * 0.3f, g.W, g.H * 0.3f, "#3a4048", 2);          // the dark trim band
            Line(g, 0, 1, g.W, 1, "#8a94a6", 2);
            Grain(g, new[] { "#7c8697", "#69737f" }, 300, 0.10f, 498);
        } },

        // Sample tray and blue tray on the lab table: #9c9694 / #5a6173.
        ["mLabTray"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#898480");
            g.StrokeRoundRect(1, 1, g.W - 2, g.H - 2, 0, 2, C("#6d6864"));
            Grain(g, new[] { "#918c88", "#7d7874" }, 250, 0.10f, 499);
        } },

        ["mLabTrayBlue"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#4f5565");
            Line(g, 0, 1, g.W, 1, "#5d6478", 2);
            Grain(g, new[] { "#565c6e", "#464c5a" }, 250, 0.10f, 500);
        } },

        // Mauve specimen trays on the L-bench case: #a48c9f.
        ["mLabSpecimen"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#907b8c");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#a08a99", 2);
            Grain(g, new[] { "#998395", "#84707f" }, 250, 0.10f, 501);
        } },

        // The amber crate top: #a06c05.
        ["mLabAmber"] = new Spec { Unit = 0.3f, Draw = g => {
            Fill(g, "#8d5f05");
            Line(g, 0, 1, g.W, 1, "#a87410", 2);
            Grain(g, new[] { "#96690a", "#7f5604" }, 250, 0.10f, 502);
        } },

        // The artifact pad: lit teal #5fffdf - emissive, a lit table and not a lamp (the
        // medScanCross lesson).
        ["mLabArtifactGlow"] = new Spec { Unit = 0.4f, Emissive = 0.85f, Draw = g => {
            Fill(g, "#54e2c4");
            Line(g, 0, 1, g.W, 1, "#7debd8", 2);
            Grain(g, new[] { "#5ce6c8", "#4cd6b8" }, 250, 0.10f, 503);
        } },

        // The gem pieces: purple #8712ac with a lighter facet line.
        ["mLabGem"] = new Spec { Unit = 0.2f, Draw = g => {
            Fill(g, "#771097");
            Line(g, 0, g.H * 0.35f, g.W * 0.6f, g.H * 0.35f, "#a844c4", 2);
            Grain(g, new[] { "#8218a4", "#680b85" }, 200, 0.12f, 504);
        } },

        // The hazard kerb at the wrap: yellow/black diagonals #d6b239 on #241c05.
        ["mLabHazard"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#201905");
            float s = g.W / 3f;
            var c = C("#bc9c32");
            for (int i = -1; i < 4; i++)
                g.FillQuad(i * s, g.H, i * s + s, g.H, i * s + s + g.H, 0, i * s + g.H, 0, c);
            Grain(g, new[] { "#c6a638", "#2a2108" }, 300, 0.12f, 505);
        } },

        // ============================================================ Reactor (mRea*)
        // surfaces_mira_reactor.js. Painted one step darker than measured, with ONE exception:
        // the core itself, which is the room's key light and keeps its measured glow values.

        // The south bay floor: pale grey tile #918e94 with the darker grout #6b6973, grout every
        // ~0.36..0.40 on both axes. One unit = two 0.38 tiles, second row in running bond.
        ["mReaTile"] = new Spec { Unit = 0.76f, Draw = g => {
            Fill(g, "#5e5c65");                                             // grout, from #6b6973
            Rect(g, 1, 1, g.W * 0.5f - 2, g.H * 0.5f - 2, "#807d82");       // tile, from #918e94
            Rect(g, g.W * 0.5f + 1, 1, g.W * 0.5f - 2, g.H * 0.5f - 2, "#807d82");
            Rect(g, g.W * 0.25f + 1, g.H * 0.5f + 1, g.W * 0.5f - 2, g.H * 0.5f - 2, "#807d82");
            Rect(g, 1, g.H * 0.5f + 1, g.W * 0.25f - 2, g.H * 0.5f - 2, "#807d82");
            Rect(g, g.W * 0.75f + 1, g.H * 0.5f + 1, g.W * 0.25f - 2, g.H * 0.5f - 2, "#807d82");
            Line(g, 1, 1, g.W * 0.5f - 2, 1, "#8d8a90", 1, 0.35f);          // lit top edge
            Line(g, g.W * 0.5f + 1, 1, g.W - 2, 1, "#8d8a90", 1, 0.35f);
            Grain(g, new[] { "#847f86", "#78747d" }, 400, 0.09f, 511);
        } },

        // The hall floor north of the tile: dark plate #55515a with big quiet panel seams.
        ["mReaFloorDark"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#4b474f");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#403d45", 2, 0.6f);    // plate seams
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#403d45", 2, 0.6f);
            Grain(g, new[] { "#504c55", "#44414b" }, 450, 0.10f, 512);
        } },

        // The decon-threshold strip: clean mint #bbe3db with grout #7c9d96. A playtest asked for
        // texture on this one flat pale slab, and the atlas has it - mint field with grey-teal
        // diagonal joints, a teal edge band west (#99bab5 with its dark line #65807e), a dark
        // divider seam #273636, an olive band #9dad80 east, and a warm orange warning stripe
        // #dc8d51 across the whole depth. The unit repeats 2x1 over the strip, so each tile
        // carries the sequence twice - it reads as a second threshold, which is intended.
        ["mReaThreshold"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#a4c8c1");                                             // mint field
            for (int i = -1; i < 4; i++)                                    // the diagonal joints
                Line(g, i * g.W / 3f, 0, i * g.W / 3f + g.H * 0.85f, g.H, "#87a49f", 3, 0.55f);
            Rect(g, 0, 0, g.W * 0.17f, g.H, "#87a49f");                     // teal edge band, west
            Line(g, g.W * 0.17f, 0, g.W * 0.17f, g.H, "#59716f", 2);        // its dark edge line
            Line(g, 0, 0, 0, g.H, "#4d615f", 2);                            // west shadow edge
            Rect(g, g.W * 0.56f, 0, g.W * 0.44f, g.H, "#8b9871");           // olive panel, east
            Rect(g, g.W * 0.53f, 0, g.W * 0.045f, g.H, "#222f2f");          // dark divider seam
            for (float y = 8; y < g.H; y += 22)                             // rivets on seam+edge
            {
                Rect(g, g.W * 0.545f - 1, y, 3, 3, "#182323");
                Rect(g, g.W * 0.155f, y + 10, 2, 2, "#182323");
            }
            Rect(g, 0, g.H * 0.30f, g.W, g.H * 0.16f, "#c27c47", 0.8f);     // the warning stripe
            Line(g, 1, 1, g.W - 2, 1, "#b4d4cd", 1, 0.3f);                  // the light top edge
            Grain(g, new[] { "#abcfc7", "#98bfb6" }, 300, 0.09f, 513);
        } },

        // The room wall: dark charcoal #212429. Butt-jointed sheets, bright cap bead.
        ["mReaWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#1d2024");
            for (float x = 0; x < g.W; x += g.W / 2f) Line(g, x, 0, x, g.H, "#16181c", 2, 0.45f);
            Line(g, 0, 2, g.W, 2, "#2e3238", 2);                            // bead under the cap
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#14161a", 3);
            Grain(g, new[] { "#22252a", "#181b1f" }, 500, 0.10f, 514);
        } },

        // THE WALLHANG FACE (north wall). One unit = the whole 4.97 wall width, so everything
        // sits at its measured x. Canvas y 0 = wall top. Near-black machinery #0d0d11..#1e1c24
        // with a steel panel left #3f4a4f, centre #535a64, right #3f4a52, and the red conduit
        // runs #6e0e0d / #860909.
        ["mReaHang"] = new Spec { Unit = 4.97f, Draw = g => {
            Fill(g, "#1a1920");                                             // band base
            // the dark upper machinery: the prototype's linear gradient, as a band that fades
            // from #0b0b0f at the top to nothing at 0.55 h - the base tone is already underneath
            g.VerticalBand(0, g.H * 0.55f, C("#0b0b0f"), 1f, 0f);
            float cw = MathF.Max(2f, g.W * 0.006f);                         // faint cable sweeps
            void Sweep(float ax, float ay, float bx, float by, float cx2, float cy2)
            {
                float px = ax, py = ay;
                for (int s = 1; s <= 6; s++)
                {
                    float t = s / 6f, u = 1f - t;
                    float qx = u * u * ax + 2 * u * t * bx + t * t * cx2;
                    float qy = u * u * ay + 2 * u * t * by + t * t * cy2;
                    Line(g, px, py, qx, qy, "#242230", cw, 0.5f);
                    px = qx; py = qy;
                }
            }
            Sweep(g.W * 0.18f, 0, g.W * 0.30f, g.H * 0.22f, g.W * 0.42f, g.H * 0.10f);
            Sweep(g.W * 0.82f, 0, g.W * 0.70f, g.H * 0.22f, g.W * 0.58f, g.H * 0.10f);
            void Panel(float a, float b, string col)                        // measured x spans
            {
                Rect(g, a * g.W, g.H * 0.62f, (b - a) * g.W, g.H * 0.30f, col);
                g.StrokeRoundRect(a * g.W, g.H * 0.62f, (b - a) * g.W, g.H * 0.30f, 0, 2, C("#121318"));
            }
            Panel(0.038f, 0.169f, "#374146");                               // x 0.2..0.85
            Panel(0.461f, 0.541f, "#494f58");                               // x 2.3..2.7
            Panel(0.823f, 0.964f, "#374148");                               // x 4.1..4.8
            Rect(g, 0.4205f * g.W, g.H * 0.10f, 0.0101f * g.W, g.H * 0.82f, "#760808");
            Rect(g, 0.5614f * g.W, g.H * 0.10f, 0.0100f * g.W, g.H * 0.82f, "#760808");
            Rect(g, 0.4205f * g.W, g.H * 0.10f, 0.0050f * g.W, g.H * 0.82f, "#610c0b");
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#0e0d12", 3);
            Grain(g, new[] { "#1e1d25", "#141319" }, 700, 0.12f, 515);
        } },

        // The machine fronts around the core nook: dark charcoal with the horizontal louver slats
        // of the core zoom crop (#211c29 field, slats to #2e3539).
        ["mReaLouver"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#1d1b24");
            for (float y = g.H * 0.12f; y < g.H; y += g.H * 0.2f)
            {
                Line(g, 0, y, g.W, y, "#282a31", MathF.Max(2f, MathF.Round(g.H * 0.07f)));
                Line(g, 0, y + 1, g.W, y + 1, "#121017", 1);
            }
            Grain(g, new[] { "#211f28", "#17151d" }, 350, 0.10f, 516);
        } },

        // Plain near-black for the socket back and band fillers (#1e1c24).
        ["mReaHangDark"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#1a1920");
            Line(g, 0, 2, g.W, 2, "#26242e", 2);
            Grain(g, new[] { "#1e1d24", "#151419" }, 400, 0.10f, 517);
        } },

        // Claw steel: #475258 body with the worn light edges #525d63.
        ["mReaClaw"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#3e484d");
            Line(g, 0, 1, g.W, 1, "#485257", 2);
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#333c41", 2);          // the joint seam
            Grain(g, new[] { "#454f55", "#363f44" }, 300, 0.10f, 518);
        } },

        // The rim arc around the core: the glass ring's lavender edge #9897af where it catches
        // the glow, down to #535170 in shadow.
        ["mReaRing"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#7b7890");
            Line(g, 0, 1, g.W, 1, "#8d8aa0", 2);                            // the lit rim edge
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#5e5b70", 2);
            Grain(g, new[] { "#82809a", "#6f6c84" }, 250, 0.10f, 519);
        } },

        // Cabinet towers: near-black violet-grey #3f3f49 with panel seams.
        ["mReaCabinet"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#373740");
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#2c2c34", 2, 0.5f);
            Line(g, 0, 1, g.W, 1, "#43434e", 2);
            Grain(g, new[] { "#3b3b45", "#31313a" }, 300, 0.10f, 520);
        } },

        // The cabinets' room-facing faces: the same dark plus the tiny glow screens the map draws
        // on every unit (#adb8c2, amber #f7db9c, green #568462, mauve #988fd9).
        ["mReaCabScreen"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#373740");
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#2c2c34", 2, 0.5f);
            void Dot(float a, float b, float s, string col) =>
                Rect(g, a * g.W, b * g.H, s * g.W, s * g.H, col);
            Dot(0.22f, 0.30f, 0.14f, "#98a2ab");
            Dot(0.42f, 0.30f, 0.14f, "#4c7456");
            Dot(0.22f, 0.52f, 0.14f, "#867ebf");
            Dot(0.42f, 0.52f, 0.14f, "#d9c189");
            g.StrokeRoundRect(0.16f * g.W, 0.24f * g.H, 0.46f * g.W, 0.48f * g.H, 0, 2, C("#1c1c22"));
            Grain(g, new[] { "#3b3b45", "#31313a" }, 300, 0.10f, 521);
        } },

        // The desks: light steel-blue #7b868c fronts and tops with the dark inset #3d454d.
        ["mReaDesk"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#6c767b");
            Rect(g, g.W * 0.08f, g.H * 0.30f, g.W * 0.84f, g.H * 0.22f, "#353d44");
            Line(g, 0, 1, g.W, 1, "#7c878d", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#586165", 2);
            Grain(g, new[] { "#737d83", "#626c71" }, 350, 0.10f, 522);
        } },

        // Console bank top: dark deck #444446 with the screen greebles (green #568462, blue
        // #22496e, grey #9a9b9a, dark slots).
        ["mReaConsole"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#3c3c3e");
            void Bay(float a, string col) =>
                Rect(g, a * g.W, g.H * 0.30f, g.W * 0.16f, g.H * 0.28f, col);
            Bay(0.06f, "#4c7456");
            Bay(0.30f, "#1e4061");
            Bay(0.54f, "#888988");
            Bay(0.78f, "#23272b");
            for (float a = 0.02f; a < 1f; a += 0.28f)
                g.StrokeRoundRect(a * g.W, g.H * 0.26f, g.W * 0.22f, g.H * 0.36f, 0, 2, C("#262628"));
            Grain(g, new[] { "#414143", "#353537" }, 350, 0.10f, 523);
        } },

        // Console bank south face: the light steel front band #7b868c under a dark screen strip
        // with the small bays.
        ["mReaConsoleFront"] = new Spec { Unit = 0.64f, Draw = g => {
            Fill(g, "#2e2e33");
            Rect(g, 0, g.H * 0.55f, g.W, g.H * 0.45f, "#6c767b");
            Line(g, 0, g.H * 0.55f, g.W, g.H * 0.55f, "#7d878d", 2);
            void Scr(float a, string col) =>
                Rect(g, a * g.W, g.H * 0.14f, g.W * 0.2f, g.H * 0.26f, col);
            Scr(0.06f, "#52798a");
            Scr(0.40f, "#31434f");
            Scr(0.72f, "#8b9295");
            g.StrokeRoundRect(0.03f * g.W, g.H * 0.10f, g.W * 0.94f, g.H * 0.36f, 0, 2, C("#1d1d21"));
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#565f64", 2);
            Grain(g, new[] { "#333338", "#62696e" }, 300, 0.09f, 524);
        } },

        // Hazard yellow/black diagonals: #d6b239 on #211c08. The same recipe as mLabHazard.
        ["mReaHazard"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#1d1907");
            float s = g.W / 3f;
            var c = C("#bc9d32");
            for (int i = -1; i < 4; i++)
                g.FillQuad(i * s, g.H, i * s + s, g.H, i * s + s + g.H, 0, i * s + g.H, 0, c);
            Grain(g, new[] { "#c6a838", "#26200a" }, 300, 0.12f, 525);
        } },

        // THE CORE - the one surface in the room that gives off light. Its own drawing is the
        // emissive map (the lava lesson), so the hot bands glow and the dark crust does not.
        // Measured heart #cdaa21/#c8a61d, hot ring to #c8b25a - values KEPT, not darkened. On the
        // drum the canvas wraps the side: bright equator band, crusty dark ends, vein streaks.
        //
        // MELTDOWN ALTERNATIVE: to glow red during the sabotage, swap the three glow stops for
        // #ff5030 / #c8281e / #8f1a12 and the crust for #4a1410 (Emissive up to ~1.1) - the
        // drawing stays a drop-in replacement.
        ["mReaCore"] = new Spec { Unit = 0.8f, Emissive = 0.85f, Draw = g => {
            Fill(g, "#3a3428");                                             // the dark crust ends
            void Band(float y0, float y1, string col) =>
                Rect(g, 0, y0 * g.H, g.W, (y1 - y0) * g.H, col);
            Band(0.14f, 0.86f, "#caa32b");
            Band(0.28f, 0.72f, "#f4cf55");                                  // hot ring
            Band(0.42f, 0.58f, "#fff6d8");                                  // white-hot heart
            for (int i = 0; i < 7; i++)                                     // vein streaks
            {
                float vx = ((i * 0.137f + 0.05f) % 1f) * g.W;
                Rect(g, vx, g.H * (0.2f + (i % 3) * 0.08f), MathF.Max(2f, g.W * 0.02f),
                     g.H * (0.14f + (i % 2) * 0.10f), i % 2 != 0 ? "#f4cf55" : "#fff6d8", 0.75f);
            }
            Grain(g, new[] { "#e5bd3d", "#4a4234" }, 300, 0.12f, 526);
        } },

        // Ceiling: near-black panel with a quiet seam grid, following the wallhang.
        ["mReaCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#141219");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#1d1b24", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#1d1b24", 3);
            Grain(g, new[] { "#18161e", "#100f15" }, 400, 0.10f, 527);
        } },

        // ============================================================ Cafeteria (mCafe*)
        // surfaces_mira_cafeteria.js.

        // THE FLOOR: wide cream ceramic tiles (0.5 u) with a LIGHTER grout than the field - that
        // way round here - scattered with small confetti squares. Confetti measured over the open
        // floor: teal #3db8b8, periwinkle #6688c4, orange #d76a47, yellow #ffdc4f, plus the pale
        // lavender #b9b4e6 off the close-up. Field #ece7df.
        ["mCafeFloor"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#d0ccc3");                                             // field, from #ece7df
            Rect(g, 0, 0, g.W, 3, "#c2beb6");                               // light grout
            Rect(g, 0, g.H / 2f - 2, g.W, 3, "#c2beb6");
            Rect(g, 0, 0, 3, g.H, "#c2beb6");
            Rect(g, g.W / 2f - 2, 0, 3, g.H, "#c2beb6");
            // the confetti: ~4 squares per tile, anchored to the tile grid the way the drawing
            // does it - corners and edge midpoints, never mid-field
            var sq = new (float X, float Y, string C)[]
            {
                (14, 10, "#36a3a3"), (70, 8, "#bd5e3e"), (104, 14, "#a4a0cc"),
                (8, 72, "#e3c346"), (56, 66, "#5a78ad"), (118, 74, "#36a3a3"),
                (30, 118, "#bd5e3e"), (92, 112, "#a4a0cc"),
            };
            foreach (var s in sq)
            {
                Rect(g, s.X, s.Y, 15, 15, s.C);
                Rect(g, s.X, s.Y + 13, 15, 2, "#000000", 0.14f);
            }
            Grain(g, new[] { "#dad5cc", "#c6c1b8" }, 500, 0.10f, 531);
        } },

        // The room's plain wall panel: warm off-white sheets with a course line every third of
        // the height. Field #e9e5dd, course seams #b9b092. The north wall's field reads a touch
        // cooler (#c6c1ba) - the same panel under different baked light, one material for all.
        ["mCafeWall"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#cfc9c1");                                             // from #e9e5dd
            foreach (float f in new[] { 1f / 3f, 2f / 3f })
            {
                Line(g, 0, g.H * f, g.W, g.H * f, "#a29a80", 2);
                Line(g, 0, g.H * f + 3, g.W, g.H * f + 3, "#ded8ce", 2, 0.5f);
            }
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#b5afa5", 2);                // butt joint
            Grain(g, new[] { "#d8d2c9", "#c2bcb3" }, 500, 0.10f, 532);
        } },

        // THE MIRA STRIPE WALL - the room's signature, running along the north wall's face. White
        // field #c6c1ba, a pale lavender-white ribbon #d7d2e0 from ~40% to ~92% wall height, and
        // four waving stripes - yellow #ffcc00, red-orange #a82900, blue #34599a, teal #009999 -
        // weaving across each other. unit = the full wall height, so the drawing maps once from
        // floor to ceiling.
        ["mCafeStripeWall"] = new Spec { Unit = 2.2f, Draw = g => {
            Fill(g, "#aea89f");                                             // field, from #c6c1ba
            for (float x = 0; x < g.W; x += 42) Line(g, x, 0, x, g.H, "#9c968c", 2, 0.35f);
            CafeWave(g, 0.66f, 0.13f, 1.1f, 0.50f, "#bdb8c5");              // the pale ribbon
            // the four stripes, back to front: blue, teal, red, yellow (yellow crosses on top)
            CafeWave(g, 0.50f, 0.11f, 0.0f, 0.070f, "#2e4e88");             // from #34599a
            CafeWave(g, 0.46f, 0.12f, 2.4f, 0.065f, "#008787");             // from #009999
            CafeWave(g, 0.70f, 0.10f, 4.1f, 0.075f, "#932400");             // from #a82900
            CafeWave(g, 0.60f, 0.17f, 5.5f, 0.080f, "#e0b400");             // from #ffcc00
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#3a3835", 3);                // the dark base line
            Grain(g, new[] { "#b8b2a8", "#a29c92" }, 500, 0.10f, 533);
        } },

        // The long dining tables: salmon-orange laminate #ef9a70 with a dark brown edge #521800
        // all round. The benches are drawn the same salmon - only their scale differs.
        ["mCafeTable"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#d28862");                                             // from #ef9a70
            g.StrokeRoundRect(1.5f, 1.5f, g.W - 3, g.H - 3, 0, 3, C("#481500"));
            Grain(g, new[] { "#dc926c", "#c67e5a" }, 400, 0.10f, 534);
        } },

        // The two wide sliding doors to the Balcony: pale blue-grey glass #abbbc5 in a darker
        // #687b88 frame grid, one tile per leaf, frame plus a brace diagonal. Translucent in the
        // prototype; opaque here, like every other pane in the mod.
        ["mCafeGlassDoor"] = new Spec { Unit = 1.8f, Draw = g => {
            Fill(g, "#97a8ad");                                             // glass, from #abbbc5
            for (float y = 0; y < g.H; y += 11) Line(g, 0, y, g.W, y, "#aabcc4", 1, 0.5f);
            g.StrokeRoundRect(3.5f, 3.5f, g.W - 7, g.H - 7, 0, 7, C("#5b6d7a"));   // frame
            Line(g, 6, g.H - 8, g.W - 6, 8, "#5b6d7a", 4);                  // the brace diagonal
            Line(g, 6, 8, g.W - 6, g.H - 8, "#5b6d7a", 4);
            Grain(g, new[] { "#a4b5ba", "#8a9ba0" }, 300, 0.12f, 535);
        } },

        // Vending machine body (Buy Beverage): warm grey cabinet #b0aa98.
        ["mCafeVend"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#9b9584");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#7d7768", 2);
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#8a8474", 2);
            Grain(g, new[] { "#a59f8e", "#8a8476" }, 400, 0.10f, 536);
        } },

        // The machine's front, one tile for the whole face: dark top housing, a 3x4 grid of drink
        // slots with coloured cans, the keypad panel on the right, the dispense slot at the
        // bottom - all as on the close-up crop.
        ["mCafeVendFront"] = new Spec { Unit = 1.4f, Draw = g => {
            Fill(g, "#bfbbae");                                             // cream body
            Rect(g, 0, 0, g.W, g.H * 0.20f, "#282927");                     // top housing
            Rect(g, g.W * 0.06f, g.H * 0.03f, g.W * 0.55f, g.H * 0.14f, "#333631");
            var cans = new[] { "#b8433a", "#e0b400", "#008787", "#2e4e88", "#b8433a", "#e0b400" };
            int ci = 0;
            for (int r = 0; r < 4; r++)                                     // the drink grid
                for (int c = 0; c < 3; c++)
                {
                    float x = g.W * (0.07f + c * 0.17f), y = g.H * (0.25f + r * 0.155f);
                    Rect(g, x, y, g.W * 0.13f, g.H * 0.115f, "#37393c");
                    Rect(g, x + g.W * 0.04f, y + g.H * 0.025f, g.W * 0.05f, g.H * 0.065f,
                         cans[ci++ % cans.Length]);
                }
            Rect(g, g.W * 0.66f, g.H * 0.25f, g.W * 0.27f, g.H * 0.34f, "#282927");   // keypad
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 2; c++)
                    Rect(g, g.W * (0.70f + c * 0.10f), g.H * (0.28f + r * 0.09f),
                         g.W * 0.055f, g.H * 0.045f, "#cf9036");
            Rect(g, g.W * 0.66f, g.H * 0.63f, g.W * 0.27f, g.H * 0.10f, "#37393c");   // notice
            Rect(g, g.W * 0.30f, g.H * 0.82f, g.W * 0.30f, g.H * 0.09f, "#282927");   // slot
            g.StrokeRoundRect(1.5f, 1.5f, g.W - 3, g.H - 3, 0, 3, C("#282927"));
            Grain(g, new[] { "#c9c5b8", "#b1ada0" }, 300, 0.08f, 537);
        } },

        // THE DRINK POSTER next to the machine: dark frame, pale ad sheet with the MIRA wave
        // swirl and a row of coloured buttons down the right edge. The figure is #490700.
        ["mCafePoster"] = new Spec { Unit = 0.7f, Draw = g => {
            Fill(g, "#282927");                                             // frame
            Rect(g, g.W * 0.08f, g.H * 0.06f, g.W * 0.84f, g.H * 0.88f, "#bdb8c5");   // sheet
            // the MIRA swirl: three thick wave arcs, teal over red over yellow
            void Arc(string col, float rad, float y0, float lw)
            {
                float cx = g.W * 0.38f, cy = g.H * y0, r = g.W * rad;
                float a0 = MathF.PI * 0.9f, a1 = MathF.PI * 1.9f;
                float px = cx + r * MathF.Cos(a0), py = cy + r * MathF.Sin(a0);
                for (int s = 1; s <= 14; s++)
                {
                    float a = a0 + (a1 - a0) * s / 14f;
                    float qx = cx + r * MathF.Cos(a), qy = cy + r * MathF.Sin(a);
                    Line(g, px, py, qx, qy, col, lw);
                    px = qx; py = qy;
                }
            }
            Arc("#008787", 0.30f, 0.52f, g.H * 0.10f);
            Arc("#932400", 0.30f, 0.58f, g.H * 0.07f);
            Arc("#e0b400", 0.30f, 0.64f, g.H * 0.05f);
            g.FillEllipse(g.W * 0.38f, g.H * 0.42f, g.W * 0.16f, g.H * 0.16f, C("#f2f0f6"));
            Rect(g, g.W * 0.33f, g.H * 0.34f, g.W * 0.10f, g.H * 0.16f, "#3d0600");   // crewmate
            Rect(g, g.W * 0.345f, g.H * 0.365f, g.W * 0.07f, g.H * 0.05f, "#9cc8dd");
            var pills = new[] { "#b83d9e", "#e0b400", "#008787", "#2e4e88" };
            for (int i = 0; i < 4; i++)                                     // the button column
                Rect(g, g.W * 0.76f, g.H * (0.14f + i * 0.19f), g.W * 0.14f, g.H * 0.10f, pills[i]);
            Grain(g, new[] { "#c7c2ce", "#b1acb9" }, 300, 0.08f, 538);
        } },

        // The emergency button console's sides: dark charcoal #2e2f2c edged with rows of amber
        // lights #e8a33d, inner panel blue-grey #6a707a.
        ["mCafeEmerg"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#282927");
            Rect(g, g.W * 0.18f, g.H * 0.18f, g.W * 0.64f, g.H * 0.64f, "#5d636b");   // inner panel
            for (int i = 0; i < 4; i++)                                     // amber lights
            {
                float p = 0.12f + i * 0.20f;
                Rect(g, g.W * p, g.H * 0.05f, g.W * 0.10f, g.H * 0.07f, "#cf9036");
                Rect(g, g.W * p, g.H * 0.88f, g.W * 0.10f, g.H * 0.07f, "#cf9036");
                Rect(g, g.W * 0.05f, g.H * p, g.W * 0.07f, g.H * 0.10f, "#cf9036");
                Rect(g, g.W * 0.88f, g.H * p, g.W * 0.07f, g.H * 0.10f, "#cf9036");
            }
            Grain(g, new[] { "#333531", "#1f201d" }, 300, 0.12f, 539);
        } },

        // The trash chute slot (Empty Garbage), mounted in the stripe band of the north wall:
        // dark frame, blue-grey plate #737994 with two tall chute slots and the amber lever knob.
        ["mCafeGarbage"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#1f2225");                                             // frame
            Rect(g, g.W * 0.08f, g.H * 0.08f, g.W * 0.84f, g.H * 0.84f, "#656b82");   // plate
            foreach (float f in new[] { 0.16f, 0.40f })                     // the two chute slots
                Rect(g, g.W * f, g.H * 0.16f, g.W * 0.14f, g.H * 0.68f, "#3d4661");
            Rect(g, g.W * 0.64f, g.H * 0.16f, g.W * 0.24f, g.H * 0.68f, "#4a5470");
            Rect(g, g.W * 0.70f, g.H * 0.30f, g.W * 0.12f, g.H * 0.10f, "#cf9036");   // lever knob
            Grain(g, new[] { "#6d7389", "#5c6276" }, 300, 0.10f, 540);
        } },

        // The east-wall shelf: blue-grey uprights with three shelf levels carrying pale yellow
        // bins (#f4ecb5).
        ["mCafeShelf"] = new Spec { Unit = 0.7f, Draw = g => {
            Fill(g, "#8591a8");                                             // body
            foreach (float f in new[] { 0.06f, 0.38f, 0.70f })
            {
                Line(g, 0, g.H * f, g.W, g.H * f, "#4f586a", 3);            // shelf lips
                foreach (float bx in new[] { 0.12f, 0.52f })                // bins
                    Rect(g, g.W * bx, g.H * (f + 0.05f), g.W * 0.26f, g.H * 0.20f, "#d7d09f");
            }
            Grain(g, new[] { "#8f9bb2", "#78849a" }, 400, 0.10f, 541);
        } },

        // cafe-misc1: a paper mat with a dark sketch and a red bottle, lying on table 1. Paper
        // #efebb5.
        ["mCafeMisc1"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#d1cf9f");
            g.StrokeRoundRect(4, 4, g.W - 8, g.H - 8, 0, 2, C("#8a8570"));
            // the scribble: two quadratic curves plus a straight tail, stepped
            void Q(float ax, float ay, float bx, float by, float cx2, float cy2)
            {
                float px = ax, py = ay;
                for (int s = 1; s <= 6; s++)
                {
                    float t = s / 6f, u = 1f - t;
                    float qx = u * u * ax + 2 * u * t * bx + t * t * cx2;
                    float qy = u * u * ay + 2 * u * t * by + t * t * cy2;
                    Line(g, px, py, qx, qy, "#4a4438", 3);
                    px = qx; py = qy;
                }
            }
            Q(g.W * 0.25f, g.H * 0.62f, g.W * 0.45f, g.H * 0.30f, g.W * 0.62f, g.H * 0.58f);
            Q(g.W * 0.62f, g.H * 0.58f, g.W * 0.72f, g.H * 0.72f, g.W * 0.55f, g.H * 0.74f);
            Line(g, g.W * 0.55f, g.H * 0.74f, g.W * 0.30f, g.H * 0.72f, "#4a4438", 3);
            Rect(g, g.W * 0.76f, g.H * 0.10f, g.W * 0.12f, g.H * 0.26f, "#a83636");   // the bottle
            Grain(g, new[] { "#dbd9ac", "#c4c291" }, 300, 0.10f, 542);
        } },

        // cafe-misc2: the second table's paper, pencil-grey sketch. Same paper stock, different
        // doodle.
        ["mCafeMisc2"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#d1cf9f");
            g.StrokeRoundRect(4, 4, g.W - 8, g.H - 8, 0, 2, C("#8a8570"));
            Line(g, g.W * 0.22f, g.H * 0.70f, g.W * 0.40f, g.H * 0.34f, "#5a564a", 3);
            Line(g, g.W * 0.40f, g.H * 0.34f, g.W * 0.58f, g.H * 0.66f, "#5a564a", 3);
            Line(g, g.W * 0.58f, g.H * 0.66f, g.W * 0.76f, g.H * 0.38f, "#5a564a", 3);
            Grain(g, new[] { "#dbd9ac", "#c4c291" }, 300, 0.10f, 543);
        } },

        // Ceiling: bright warm panel with a seam grid - Mira's interiors are lit bright white and
        // AreaKit warns that a dark ceiling swallows the room.
        ["mCafeCeil"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#c9c6bf");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#9f9b94", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#9f9b94", 3);
            Grain(g, new[] { "#d5d2cb", "#b8b5ae" }, 400, 0.10f, 544);
        } },

        // ============================================================ Launchpad (mLp*)
        // surfaces_mira_launchpad.js. The pad is OPEN AIR - the whole room lies inside
        // world.json's "Outside" trigger polygon - so there is no ceiling material here: the sky
        // is the ceiling.

        // THE PAD FLOOR: warm white square tiles, grout every ~1.15. The slab's rim is mLpCurb,
        // the dark fascia the drawing runs along every pad edge.
        ["mLpTile"] = new Spec { Unit = 1.15f, Draw = PadTile },

        // THE LANDING DISC: one 7.40-world-unit tile (unit 23 > r*6 forces a 1x1 repeat, the same
        // trick the Skeld's shield hex field uses). A cylinder cap shows the inscribed circle of
        // the tile, so the painting is laid out around the centre at 128 px / 7.40 units:
        //   grey hover-shadow ring  r 2.75..3.60   #52514e
        //   yellow ring             r 2.08..2.57   #fcdf22
        //   marker ring             r 0.44..0.53   #4c4b49
        //   marker dot              r 0.17         yellow
        // Ring centre and radii come from a least-squares fit of 7485 yellow pixels. The north
        // half of both rings disappears under the Dropship hull, as drawn.
        ["mLpPadDisc"] = new Spec { Unit = 23.0f, Draw = g => {
            PadTile(g);
            float cx = g.W / 2f, cy = g.H / 2f, u = g.W / 7.4f;
            g.StrokeEllipse(cx, cy, (2.75f + 3.60f) / 2f * u, (2.75f + 3.60f) / 2f * u,
                            (3.60f - 2.75f) * u, C("#4b4a48"));             // from #52514e
            g.StrokeEllipse(cx, cy, (2.08f + 2.57f) / 2f * u, (2.08f + 2.57f) / 2f * u,
                            (2.57f - 2.08f) * u, C("#e3c71f"));             // from #fcdf22
            Grain(g, new[] { "#e6da55", "#d8bd1d" }, 500, 0.08f, 551);
            // the small landing marker at the centre, half of it hidden by the ship's bumper
            float mx = cx + 0.01f * u, my = cy - 0.08f * u;
            g.StrokeEllipse(mx, my, (0.44f + 0.53f) / 2f * u, (0.44f + 0.53f) / 2f * u,
                            (0.53f - 0.44f) * u, C("#383836"));
            g.FillEllipse(mx, my, 0.17f * u, 0.17f * u, C("#e3c71f"));
            Rect(g, mx - 1.5f, my, 3, 0.55f * u, "#f2f0ec");                // the paint drip
        } },

        // The lawn the pad's two south walk-outs meet. A playtest read it as NEON green: the atlas
        // measures pure #006600 there, and pure green is exactly what tips into neon under lamps.
        // So the albedo is a desaturated dark green instead, with a fine blade grain.
        ["mLpLawn"] = new Spec { Unit = 1.3f, Draw = g => {
            Fill(g, "#3a6530");
            var r = new Rng(552);
            for (int i = 0; i < 90; i++)                                    // the blades
                Rect(g, r.Next() * g.W, r.Next() * g.H, 1, 2f + r.Next() * 3f,
                     i % 2 != 0 ? "#2c5126" : "#4d7c40", 0.30f);
            Grain(g, new[] { "#416e37", "#33592b" }, 450, 0.10f, 553);
        } },

        // Slab rim / kerb: the dark fascia band around the pad (#404143 on the east edge, #222225
        // on the south face).
        ["mLpCurb"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#38393c");
            Line(g, 0, 2, g.W, 2, "#4a4b4e", 2);                            // lighter top edge
            Grain(g, new[] { "#434447", "#2e2f32" }, 400, 0.12f, 554);
        } },

        // Chain-link fence frames: light steel #c9cbd6. AreaKit adds the rail posts and top bar.
        ["mLpFence"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#b2b4bf");
            Line(g, 0, 1, g.W, 1, "#cdd0da", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#8f92a0", 2);
            Grain(g, new[] { "#bcbfc9", "#a4a7b4" }, 300, 0.10f, 555);
        } },

        // THE DROPSHIP's hull. The game's own colliders for it are off; here it is solid so nobody
        // walks through a ship. A playtest asked for texture on the flat mass: vertical panel
        // joints every ~0.8 unit with a fallen light edge, horizontal deck lines and rivet dots.
        // The albedo stays the measured #293331.
        ["mLpHull"] = new Spec { Unit = 1.6f, Draw = g => {
            Fill(g, "#242d2b");
            void Joint(float x)                                             // shadow seam + light
            {
                Line(g, x, 0, x, g.H, "#161d1b", 3);
                Line(g, x + 3, 0, x + 3, g.H, "#2e3836", 1);
            }
            Joint(g.W * 0.25f); Joint(g.W * 0.5f); Joint(g.W * 0.75f);
            Line(g, 0, g.H * 0.34f, g.W, g.H * 0.34f, "#1a2220", 2);        // deck lines
            Line(g, 0, g.H * 0.34f + 3, g.W, g.H * 0.34f + 3, "#313b39", 1);
            Line(g, 0, g.H * 0.72f, g.W, g.H * 0.72f, "#1a2220", 2);
            Line(g, 0, g.H * 0.72f + 3, g.W, g.H * 0.72f + 3, "#313b39", 1);
            for (float y = 6; y < g.H; y += 24)                             // rivet groups
                foreach (float x in new[] { g.W * 0.5f - 8, g.W * 0.5f + 8, g.W * 0.25f - 6,
                                            g.W * 0.25f + 6, g.W * 0.75f - 6, g.W * 0.75f + 6 })
                {
                    Rect(g, x, y, 2, 2, "#151c1a", 0.65f);
                    Rect(g, x + 1, y + 1, 2, 2, "#151c1a", 0.65f);          // the blurred double
                }
            Grain(g, new[] { "#2b3533", "#1e2624" }, 500, 0.12f, 556);
        } },

        // Upper hull deck: the lighter grey-green panel #3c4a4a, slightly cleaner than the bare
        // hull below it.
        ["mLpHullTop"] = new Spec { Unit = 1.6f, Draw = g => {
            Fill(g, "#354242");
            for (float x = 0; x < g.W; x += 34) Line(g, x, 0, x, g.H, "#2c3838", 2, 0.4f);
            Line(g, 0, 2, g.W, 2, "#465454", 2);
            Grain(g, new[] { "#3d4b4b", "#2d3939" }, 500, 0.12f, 557);
        } },

        // Pod bodies: the same grey-green with the teal-blue trim the drawing runs along the pod
        // edges (#1e3c4d).
        ["mLpPod"] = new Spec { Unit = 1.6f, Draw = g => {
            Fill(g, "#354242");
            Rect(g, 0, 0, g.W, 5, "#1a3846");
            Rect(g, 0, g.H - 5, g.W, 5, "#1a3846");
            for (float y = 10; y < g.H; y += 24) Line(g, 0, y, g.W, y, "#2c3838", 2, 0.5f);
            Grain(g, new[] { "#3c4a4a", "#2d3939" }, 500, 0.12f, 558);
        } },

        // Pod south face: the 2x2 engine bells, one tile per face, circles laid out as measured on
        // the left pod (bell r ~0.33, #687c7b rims, near-black centres).
        ["mLpPodFace"] = new Spec { Unit = 1.9f, Draw = g => {
            Fill(g, "#454f51");                                             // from #4e5a5c
            g.StrokeRoundRect(2, 2, g.W - 4, g.H - 4, 0, 5, C("#1a3846"));  // the teal border
            foreach (var p in new[] { (0.30f, 0.68f), (0.70f, 0.68f), (0.30f, 0.34f), (0.70f, 0.34f) })
            {
                float bx = g.W * p.Item1, by = g.H * p.Item2, br = g.W * 0.155f;
                g.FillEllipse(bx, by, br, br, C("#0e1a18"));                // the dark bell throat
                g.StrokeEllipse(bx, by, br, br, 4, C("#5b6d6c"));           // the bell rim
                for (int a = 0; a < 6; a++)                                 // turbine spokes
                    Line(g, bx, by, bx + MathF.Cos(a * 1.047f) * g.W * 0.13f,
                         by + MathF.Sin(a * 1.047f) * g.W * 0.13f, "#2a3a38", 2, 0.5f);
            }
            Grain(g, new[] { "#4d575a", "#3d474a" }, 300, 0.10f, 559);
        } },

        // The cargo ramp face on the ship's rear: pale ribbed plate #849294, ribs running with the
        // drawing's horizontal slats.
        ["mLpRamp"] = new Spec { Unit = 0.55f, Draw = g => {
            Fill(g, "#788587");
            for (float y = 0; y < g.H; y += 7) Line(g, 0, y, g.W, y, "#677478", 2, 0.55f);
            Line(g, 0, 1, g.W, 1, "#8b989b", 2);
            Grain(g, new[] { "#839093", "#6d7a7d" }, 400, 0.12f, 560);
        } },

        // THE FUEL TANK's side: a fat yellow cylinder lying north-south, drawn as a pipe (around
        // the tank = x of the tile, along it = y). The end-cap rings sit in the tile centre so the
        // pipe's south cap shows them; on the side they read as a weld band round the middle.
        // Body #d7ae25, cap rings #6b5216/#57420f. A playtest asked for seam structure on the flat
        // cylinder: weld joints every ~0.8 unit and longitudinal deck lines with rivets.
        ["mLpTank"] = new Spec { Unit = 2.0f, Draw = g => {
            Fill(g, "#bd9920");                                             // from #d7ae25
            foreach (float x in new[] { 12f, 63f, 114f })                   // the round-the-tank joints
            {
                Line(g, x, 0, x, g.H, "#a5841a", 2);
                Line(g, x + 2, 0, x + 2, g.H, "#8f7015", 1);                // the seam's shadow
            }
            Line(g, 0, g.H * 0.16f, g.W, g.H * 0.16f, "#a8871a", 2);        // deck lines
            Line(g, 0, g.H * 0.84f, g.W, g.H * 0.84f, "#a8871a", 2);
            for (float x = 8; x < g.W; x += 24)                             // rivets on the seams
            {
                Rect(g, x, g.H * 0.16f - 4, 2, 2, "#7c6212", 0.65f);
                Rect(g, x, g.H * 0.84f + 2, 2, 2, "#7c6212", 0.65f);
            }
            float cx = g.W / 2f, cy = g.H / 2f;                             // the end cap
            g.StrokeEllipse(cx, cy, 41, 41, 5, C("#6b5216"));
            g.StrokeEllipse(cx, cy, 24, 24, 4, C("#57420f"));
            g.FillEllipse(cx, cy, 9, 9, C("#6b5216"));
            Grain(g, new[] { "#c9a524", "#a8871a" }, 500, 0.12f, 561);
        } },

        // Gas pump cabinet: slate blue-grey #8a95a0.
        ["mLpPump"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#77828c");
            Line(g, 0, 2, g.W, 2, "#8b96a0", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#5d6872", 2);
            Grain(g, new[] { "#82909a", "#6b7680" }, 300, 0.10f, 562);
        } },

        // The pump's east face: dark dispenser slots and the red fuel screen. The screen is drawn
        // bright and the entry carries a faint red glow, so the screen reads lit while the cabinet
        // stays matte.
        ["mLpPumpFace"] = new Spec { Unit = 1.5f, Emissive = 0.5f, Draw = g => {
            Fill(g, "#77828c");
            Rect(g, g.W * 0.14f, g.H * 0.10f, g.W * 0.72f, g.H * 0.34f, "#8a1410");   // red screen
            g.StrokeRoundRect(g.W * 0.14f, g.H * 0.10f, g.W * 0.72f, g.H * 0.34f, 0, 3, C("#4a545e"));
            Rect(g, g.W * 0.16f, g.H * 0.56f, g.W * 0.28f, g.H * 0.34f, "#252e36");    // slots
            Rect(g, g.W * 0.56f, g.H * 0.56f, g.W * 0.28f, g.H * 0.34f, "#252e36");
            Rect(g, g.W * 0.05f, g.H * 0.30f, g.W * 0.06f, g.H * 0.08f, "#3a72c8");    // buttons
            Rect(g, g.W * 0.05f, g.H * 0.42f, g.W * 0.06f, g.H * 0.08f, "#d8c838");
            Grain(g, new[] { "#82909a", "#6b7680" }, 300, 0.10f, 563);
        } },

        // Run Diagnostics cabinet body: light grey #b9bcc0.
        ["mLpConsole"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#a2a6ab");
            Line(g, 0, 2, g.W, 2, "#b6babf", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#84888d", 2);
            Grain(g, new[] { "#adb1b6", "#94989d" }, 300, 0.10f, 564);
        } },

        // Its west face: the orange-red button panel #98301d in a grey frame, with the lighter
        // button grid and the small display window at the top.
        ["mLpCalibFace"] = new Spec { Unit = 1.5f, Draw = g => {
            Fill(g, "#a2a6ab");
            Rect(g, g.W * 0.16f, g.H * 0.22f, g.W * 0.68f, g.H * 0.62f, "#862a19");
            for (int j = 0; j < 4; j++)                                     // the button grid
                for (int i = 0; i < 3; i++)
                    Rect(g, g.W * (0.22f + i * 0.20f), g.H * (0.28f + j * 0.13f),
                         g.W * 0.12f, g.H * 0.07f, "#a83a1f");
            Rect(g, g.W * 0.30f, g.H * 0.06f, g.W * 0.40f, g.H * 0.10f, "#c8d4dc");   // display
            g.StrokeRoundRect(g.W * 0.16f, g.H * 0.22f, g.W * 0.68f, g.H * 0.62f, 0, 3, C("#6e7277"));
            Grain(g, new[] { "#adb1b6", "#94989d" }, 300, 0.10f, 565);
        } },

        // Crates: dark sea-green #2e4e48 with #1a2620 straps, the slate-blue one #3a4d5e, and the
        // tan boxes (estimated off the launch-front sprite - no clean atlas texel).
        ["mLpCrateGreen"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#2a4742");
            g.StrokeRoundRect(3, 3, g.W - 6, g.H - 6, 0, 5, C("#1a2620"));
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#1a2620", 4);
            Grain(g, new[] { "#35564f", "#243e3a" }, 400, 0.12f, 566);
        } },

        ["mLpCrateBlue"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#354655");
            g.StrokeRoundRect(3, 3, g.W - 6, g.H - 6, 0, 5, C("#232f3a"));
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#232f3a", 4);
            Grain(g, new[] { "#405364", "#2c3c4a" }, 400, 0.12f, 567);
        } },

        ["mLpCrateTan"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#7d6e50");
            g.StrokeRoundRect(3, 3, g.W - 6, g.H - 6, 0, 5, C("#5d5140"));
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#5d5140", 4);
            Grain(g, new[] { "#8a7a5a", "#6e6046" }, 400, 0.12f, 568);
        } },

    };
}
