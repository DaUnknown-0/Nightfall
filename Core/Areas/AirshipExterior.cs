// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

using System;

namespace Nightfall.Core;

/// WHAT THE AIRSHIP LOOKS LIKE FROM OUTSIDE: the gas envelope over the deck, its tail fins, four
/// engine nacelles, the mooring lines - and a bank of cloud below, because the ship is flying.
///
/// This is the mod's half of the prototype's `airshipBody()`/`airshipClouds()` (world.js). The
/// rooms themselves arrive through AirshipAreas.g.cs like every other map; the hull's own flank
/// and keel come with them, as `ribbon` fixtures of the 22nd area (hull.js). Everything in THIS
/// file has no counterpart in the area files, for one reason: it is not part of the map. Among Us
/// never draws it. The map drawing stops at the ship's red silhouette, and above that silhouette
/// the game shows sky - so an envelope drawn from the map data would be an invention with a
/// measurement's authority. Here it is plainly what it is: the thing the drawing implies, built
/// once, in one file, where it can be turned off by deleting one registry line.
///
/// WHY THE NUMBERS ARE THE PROTOTYPE'S. Every dimension below is the one that was looked at from
/// eye height and from outside in Assets/NightfallWeb (world.js, `airshipBody`): centre, radii,
/// fin span, nacelle positions. The prototype is where the shape was judged; copying its numbers
/// is what keeps the two ships the same ship. The one conversion: the prototype writes heights in
/// WORLD units (its `V` squash already applied), the kit takes AREA units, so every height here is
/// the prototype's number divided by V.
public static class AirshipExterior
{
    // Centre of the envelope in Among Us coordinates. The prototype's CX/CZ, with three.z = -au.y
    // undone: its CZ = -3.45 is au y = +3.45.
    private const float Cx = 9.0f, Cy = 3.45f;

    // Half-axes. Length and width in world units (they are plan distances, never squashed);
    // height in world units too, converted where it is used.
    private const float Rx = 50f, RyWorld = 13f, Rz = 22f;

    // Underside of the envelope 4.5 world units over the deck, so the Meeting Room's superstructure
    // (its roof reaches 4.147 + 2.1 area units) passes under it.
    private const float CentreWorld = 4.5f + RyWorld;

    private const string Skin = "aAirBodySkin";
    private const string Dark = "aAirHullRim";
    private const string Nacelle = "aAirBodyNacelle";
    private const string Cloud = "aAirCloudPuff";

    /// Builds the outside. Signature matches MapAreaRegistry's BuildExterior hook; the bounds are
    /// the map's own extent grown by Scene3D, and only the cloud bank uses them.
    public static void Build(AreaBuilder b, float x0, float y0, float x1, float y1)
    {
        float cy = CentreWorld / AreaBuilder.V;              // centre height in AREA units
        float ry = RyWorld / AreaBuilder.V;

        /* 28 x 20 rather than the 12 rings a rock gets: the envelope's BLUNT bow goes from a
         * point to half its radius within a twentieth of its length, and at twelve rings that
         * step read as a star of flat spikes when the ship was seen end-on (offline render,
         * 2026-08-30). Facets say "rock"; an airship has to be smooth. 1 120 quads. */
        b.Envelope(Cx, Cy, cy, Rx, ry, Rz, Skin, 28, 20);

        /*
         * TAIL FINS. The prototype extrudes four trapezoids around the long axis; this kit has no
         * rotation, so the vertical pair is built as two thin upright boxes at the stern and the
         * horizontal pair as two flat ones - the same silhouette from every direction anyone sees
         * the ship from, at a twentieth of the geometry.
         */
        float finA = Cx + Rx * 0.55f, finB = Cx + Rx * 0.88f;   // along the tapering tail
        const float FinT = 0.5f;                                 // fin thickness
        float finHalfT = FinT * 0.5f / AreaBuilder.V;
        b.Box(new AuRect(finA, Cy - FinT * 0.5f, finB, Cy + FinT * 0.5f),
              cy - 0.5f, cy + ry * 0.72f, Dark, false);           // upper fin
        b.Box(new AuRect(finA, Cy - FinT * 0.5f, finB, Cy + FinT * 0.5f),
              cy - ry * 0.72f, cy + 0.5f, Dark, false);           // lower fin
        b.Box(new AuRect(finA, Cy - Rz * 0.62f, finB, Cy + Rz * 0.62f),
              cy - finHalfT, cy + finHalfT, Dark, false);         // the horizontal pair, one plate

        /*
         * ENGINE NACELLES. Two under the south flank, two beside the north skirt, at the
         * prototype's stations. A nacelle is a stretched blob with a strut into the hull; the
         * prototype's turning propellers are left out - nothing else in this renderer moves, and a
         * still three-bladed disc reads as a smear at any distance the ship is seen from.
         */
        foreach (var (nx, ny) in new[] { (-4f, -19.6f), (22f, -19.6f), (-4f, 30.6f), (22f, 30.6f) })
        {
            float nh = ny < 0f ? -6.8f / AreaBuilder.V : -2.8f / AreaBuilder.V;
            b.Blob(nx, ny, nh, 2.6f, 1.2f / AreaBuilder.V, 1.2f, Nacelle, 8, 5);
            // The strut, running from the nacelle's back up into the hull rather than into the air.
            float top = ny < 0f ? -3.0f / AreaBuilder.V : 0.4f / AreaBuilder.V;
            b.Box(new AuRect(nx - 0.2f, ny - 0.2f, nx + 0.2f, ny + 0.2f), nh, top, Dark, false);
        }

        /*
         * MOORING LINES from the bulwark up to the envelope - the detail that makes the deck hang
         * FROM the balloon instead of floating under it. Four stations along each side, each line a
         * thin box from the rail to the hull's surface at that station.
         */
        foreach (float x in new[] { -6f, 6f, 18f, 30f })
        {
            float t = (x - Cx) / Rx;
            float shrink = MathF.Sqrt(MathF.Max(0.02f, 1f - t * t));
            foreach (float y in new[] { 29.0f, -13.55f })
            {
                float dy = (y - Cy) / (Rz * shrink);
                float under = cy - ry * shrink * MathF.Sqrt(MathF.Max(0.04f, 1f - dy * dy));
                b.Box(new AuRect(x - 0.06f, y - 0.06f, x + 0.06f, y + 0.06f), 0.55f, under, Dark, false);
            }
        }

        Clouds(b, x0, y0, x1, y1);
    }

    /*
     * THE CLOUD SEA. The prototype scatters ~300 puffs; here it is 64, because every one of them
     * is triangles a software rasteriser walks over, and the ones that matter are the near ones -
     * a cloud two hundred units out is four pixels. They sit 24 to 34 units below the deck, well
     * under the abyss the fall-through starts at, and are laid out on a deterministic grid jitter
     * so the game and the offline render tool see the same sky.
     */
    private static void Clouds(AreaBuilder b, float x0, float y0, float x1, float y1)
    {
        float mx = (x0 + x1) * 0.5f, my = (y0 + y1) * 0.5f;
        float spanX = MathF.Max(120f, (x1 - x0) * 1.6f), spanY = MathF.Max(120f, (y1 - y0) * 1.6f);
        uint seed = 0x51ED270Bu;
        float Rnd()
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xFFFFFF) / 16777216f;
        }

        const int N = 64;
        for (int i = 0; i < N; i++)
        {
            float u = (i % 8 + Rnd()) / 8f - 0.5f;
            float v = (i / 8 + Rnd()) / 8f - 0.5f;
            float x = mx + u * spanX, y = my + v * spanY;
            // Nothing directly under the ship: a cloud there pokes through the belly.
            if (MathF.Abs(x - Cx) < Rx * 0.6f && MathF.Abs(y - Cy) < Rz * 0.9f) continue;
            float h = (-24f - Rnd() * 10f) / AreaBuilder.V;
            float r = 4f + Rnd() * 7f;
            b.Blob(x, y, h, r, r * 0.45f / AreaBuilder.V, r * 0.8f, Cloud, 7, 4);
            b.Blob(x + r * 0.7f, y + r * 0.2f, h - 0.4f / AreaBuilder.V, r * 0.6f,
                   r * 0.34f / AreaBuilder.V, r * 0.5f, Cloud, 6, 3);
        }
    }
}
