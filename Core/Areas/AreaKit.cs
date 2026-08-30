// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * THE BUILDING KIT - the port of Assets/NightfallWeb/src/kit.js and src/build.js.
 *
 * Everything in the world is made of these pieces. They all take Among Us rectangles - the same
 * numbers an author reads off the printed grid - and they are the only place in the mod where a map
 * coordinate turns into a renderer coordinate.
 *
 * ONE PLACE FOR THE COORDINATE CHANGE
 * -----------------------------------
 * The renderer's world is (x, height, z) with z running north, so an Among Us (x, y) becomes
 * (x, height, y): the two agree, and unlike the prototype's three.js target there is no sign to get
 * wrong. It is still funnelled through `Box` and `Cyl` alone, for the same reason the prototype
 * funnels it through coords.js - the day that stops being true is the day half the map is mirrored
 * and nobody can see it, because a station looks like a station either way round.
 *
 * WHY WALLS HAVE THICKNESS
 * ------------------------
 * Among Us draws a wall as a band: a face, folded down towards the camera over the floor in front of
 * it, and a darker cap above it, which is the top of the wall seen from above. Neither band is
 * floor. So a wall's footprint is the GAP BETWEEN THE TWO FLOORS IT SEPARATES. That is why walls
 * here are 0.3 to 0.9 thick and why a doorway is a short tunnel, which is exactly how a door on
 * Polus feels.
 *
 * THE VERTICAL SCALE, AND WHY IT IS NOT 1
 * ---------------------------------------
 * Every height in the area data is written in Among Us' own drawing scale: a crewmate 0.7 tall, an
 * interior wall 2.1. Built one to one that is wrong in a way a plan view can never show. Polus'
 * rooms are SHALLOW - the meeting half of Office is 7.5 wide and 1.7 deep, Comms is 3.2 by 2.7 -
 * and giving those 2.1 of wall puts you in a shaft taller than the room is deep. So every height is
 * multiplied by V on the way into the geometry and nowhere else. The floor plan stays exactly as
 * measured; only the vertical is squashed.
 *
 * WHAT IS DELIBERATELY DIFFERENT FROM THE PROTOTYPE
 * ------------------------------------------------
 *  - No point lights. The prototype lights its rooms with about ninety of them; this is a blackout
 *    with a torch in it, so a ceiling lamp is EMISSIVE GEOMETRY - it glows, it does not illuminate.
 *    That is also the only version of it that costs nothing per frame.
 *  - Round things are coarser. A drum is ten sides here and sixteen there; a boulder is three lumps
 *    of a six-by-four sphere rather than five of a seven-by-five one. At torchlight, forty metres
 *    from anywhere, the silhouette is what survives and the segment count is not.
 *  - Glass is opaque dark blue. The rasteriser has no blending: every triangle writes depth. A
 *    window at night is nearly that anyway.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public sealed class AreaBuilder
{
    /// The vertical scale. See the note at the top of the file.
    public const float V = 0.66f;

    /// The planet's surface, in area units. Everything the station stands on is above it.
    public const float PlanetDeck = -0.28f;

    private readonly List<Tri3> outp;
    private readonly List<(AuRect rect, float y, bool pit)> decks = new();

    /// Every door/gap opening's footprint, collected while the walls are built and sealed in one
    /// pass afterwards. See SealThresholds for why this cannot happen during the wall itself.
    private readonly List<AuRect> thresholds = new();

    public AreaBuilder(List<Tri3> target) { outp = target; }

    public int TriangleCount => outp.Count;

    // ================================================================================
    // Ground height
    // ================================================================================
    /// Height of the ground at an Among Us position, in world units.
    ///
    /// A PIT WINS OUTRIGHT instead of losing the "highest deck under your feet" comparison.
    /// Everywhere on Polus the ground is the planet and the decks sit above it, so "stand on the
    /// highest thing under your feet" is the right rule. The lava gorge east of the Laboratory is
    /// the one place where the ground goes DOWN, and under that rule it was invisible to whoever
    /// walked across it.
    public float GroundAt(float x, float y)
    {
        float best = PlanetDeck * V;
        float pit = float.MaxValue;
        bool inPit = false;
        foreach (var d in decks)
        {
            if (x < d.rect.MinX || x > d.rect.MaxX || y < d.rect.MinY || y > d.rect.MaxY) continue;
            if (d.pit) { inPit = true; pit = MathF.Min(pit, d.y * V); }
            else if (d.y * V > best) best = d.y * V;
        }
        return inPit ? pit : best;
    }

    /// Whether any deck at all lies under the point. GroundAt falls back to the planet where there
    /// is none, and on a map whose ground is not the planet (the Fungle's highlands sit metres
    /// above it) that fallback is a hole in the description, not a place: the view's hole guard
    /// uses this to tell the two apart.
    public bool DeckUnder(float x, float y)
    {
        foreach (var d in decks)
            if (x >= d.rect.MinX && x <= d.rect.MaxX && y >= d.rect.MinY && y <= d.rect.MaxY) return true;
        return false;
    }

    /// Removes `hole` from every rectangle in `pieces`, replacing each affected one by the up to
    /// four rectangles that remain around it. Rectangles that miss the hole pass through untouched.
    private static void SubtractRect(List<AuRect> pieces, AuRect hole)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var r = pieces[i];
            if (hole.MinX >= r.MaxX || hole.MaxX <= r.MinX || hole.MinY >= r.MaxY || hole.MaxY <= r.MinY) continue;
            pieces.RemoveAt(i);
            const float eps = 1e-4f;
            // the band below and above the hole, full width
            if (hole.MinY - r.MinY > eps) pieces.Add(new AuRect(r.MinX, r.MinY, r.MaxX, hole.MinY));
            if (r.MaxY - hole.MaxY > eps) pieces.Add(new AuRect(r.MinX, hole.MaxY, r.MaxX, r.MaxY));
            // the strips left and right of the hole, within its y-span
            float y0 = MathF.Max(r.MinY, hole.MinY), y1 = MathF.Min(r.MaxY, hole.MaxY);
            if (hole.MinX - r.MinX > eps) pieces.Add(new AuRect(r.MinX, y0, hole.MinX, y1));
            if (r.MaxX - hole.MaxX > eps) pieces.Add(new AuRect(hole.MaxX, y0, r.MaxX, y1));
        }
    }

    /// LAYS A SILL OF FLOOR UNDER EVERY DOORWAY THAT LACKS ONE - after all areas are built,
    /// because "lacks one" is a question about the neighbours' decks and those may belong to an
    /// area that has not been built yet while the wall is going up.
    ///
    /// The room floors on either side of a wall stop at the wall's faces, so inside the wall's own
    /// footprint there is usually no deck at all: under a door opening GroundAt fell through to
    /// the planet, 0.185 below deck. Walking through any door the camera took a quick dip and
    /// recovered - and at the upper-decon door onto the lava bridge, with the river glowing ahead,
    /// that dip was reported as "I teleport down onto the lava" (fourth playtest). Eight doorways
    /// had the hole; the render tool never showed it because a camera is never placed inside a
    /// wall.
    ///
    /// The sill takes the LOWER of the two neighbouring grounds (a door between two levels is a
    /// step at one end, not a ramp), sits 0.01 area units below it so an abutting floor top never
    /// has to fight it for pixels, and is skipped where either side is no higher than the hole -
    /// a porch door onto bare planet needs no sill, and building one there would put a step where
    /// the game has none.
    public void SealThresholds()
    {
        foreach (var rc in thresholds)
        {
            bool depthIsX = (rc.MaxX - rc.MinX) < (rc.MaxY - rc.MinY);
            float cx = (rc.MinX + rc.MaxX) * 0.5f, cy = (rc.MinY + rc.MaxY) * 0.5f;
            float half = depthIsX ? (rc.MaxX - rc.MinX) * 0.5f : (rc.MaxY - rc.MinY) * 0.5f;
            float d = half + 0.18f;
            float g0 = depthIsX ? GroundAt(cx - d, cy) : GroundAt(cx, cy - d);
            float g1 = depthIsX ? GroundAt(cx + d, cy) : GroundAt(cx, cy + d);
            float gIn = GroundAt(cx, cy);
            float target = MathF.Min(g0, g1);
            if (target <= gIn + 0.05f) continue;   // no hole, or a genuine step down outdoors

            float topArea = target / V - 0.01f;
            Box(rc, topArea - 0.28f, topArea, new Faces { All = "panelSteel", Top = "metalDeck" });
            decks.Add((rc, topArea, false));
        }
    }

    // ================================================================================
    // Primitives
    // ================================================================================
    private void Quad(NfVec3 a, NfVec3 b, NfVec3 c, NfVec3 d,
                      Surface3D tex, float uRep, float vRep, float shade, float emissive)
    {
        var t1 = new Tri3
        {
            A = new Vtx3(a, 0f, vRep), B = new Vtx3(b, uRep, vRep), C = new Vtx3(c, uRep, 0f),
            Tex = tex, Tint = NfColor.White, Shade = shade, Emissive = emissive,
        };
        var t2 = new Tri3
        {
            A = new Vtx3(a, 0f, vRep), B = new Vtx3(c, uRep, 0f), C = new Vtx3(d, 0f, 0f),
            Tex = tex, Tint = NfColor.White, Shade = shade, Emissive = emissive,
        };
        t1.Finish(); t2.Finish();
        outp.Add(t1); outp.Add(t2);
    }

    /// How many tiles of `mat` fit across `units` world units, at least one.
    private static float Rep(string mat, float units)
    {
        float u = AreaSurfaces.UnitOf(mat);
        return MathF.Max(1f, MathF.Round(units / MathF.Max(0.05f, u)));
    }

    /// Shading per face, so the planes of a box read apart even in flat light. The renderer's own
    /// facing term does most of this work; these are the last few percent that stop a corner from
    /// disappearing when the torch is pointed straight at it.
    private const float ShadeTop = 1.06f, ShadeSide = 0.94f, ShadeFace = 1.0f, ShadeBottom = 0.78f;

    /// A box standing on an Among Us rectangle, between two heights (in AREA units - they get
    /// multiplied by V here, which is the only place it happens).
    ///
    /// `omitBottom` drops the underside. Almost every box in the world stands on something, and its
    /// underside is two triangles nobody will ever see; there are a few thousand boxes.
    public void Box(AuRect r, float y0, float y1, Faces f, bool omitBottom = true)
    {
        float x0 = r.MinX, x1 = r.MaxX, ya = r.MinY, yb = r.MaxY;
        float sx = x1 - x0, sy = yb - ya, h0 = y0 * V, h1 = y1 * V;
        float sh = h1 - h0;
        if (sx <= 1e-4f || sy <= 1e-4f || sh <= 1e-4f) return;

        // north (+y), south (-y), east (+x), west (-x), top, bottom
        Face('n', new NfVec3(x1, h0, yb), new NfVec3(x0, h0, yb), new NfVec3(x0, h1, yb), new NfVec3(x1, h1, yb), sx, sh, ShadeFace);
        Face('s', new NfVec3(x0, h0, ya), new NfVec3(x1, h0, ya), new NfVec3(x1, h1, ya), new NfVec3(x0, h1, ya), sx, sh, ShadeFace);
        Face('e', new NfVec3(x1, h0, ya), new NfVec3(x1, h0, yb), new NfVec3(x1, h1, yb), new NfVec3(x1, h1, ya), sy, sh, ShadeSide);
        Face('w', new NfVec3(x0, h0, yb), new NfVec3(x0, h0, ya), new NfVec3(x0, h1, ya), new NfVec3(x0, h1, yb), sy, sh, ShadeSide);
        Face('t', new NfVec3(x0, h1, ya), new NfVec3(x1, h1, ya), new NfVec3(x1, h1, yb), new NfVec3(x0, h1, yb), sx, sy, ShadeTop);
        if (!omitBottom)
            Face('b', new NfVec3(x0, h0, yb), new NfVec3(x1, h0, yb), new NfVec3(x1, h0, ya), new NfVec3(x0, h0, ya), sx, sy, ShadeBottom);

        void Face(char side, NfVec3 p0, NfVec3 p1, NfVec3 p2, NfVec3 p3, float w, float hh, float shade)
        {
            string m = f?.Pick(side);
            // A side the author did not name falls back to the structural steel every building is
            // skinned in. It used to fall through to a light-grey placeholder, and it showed:
            // beside Storage's west door the two flanking walls' cut ends filled half the view.
            m ??= "panelSteel";
            Quad(p0, p1, p2, p3, AreaSurfaces.Get(m), Rep(m, w), Rep(m, hh),
                 shade, AreaSurfaces.EmissiveOf(m));
        }
    }

    public void Box(AuRect r, float y0, float y1, string mat, bool omitBottom = true) =>
        Box(r, y0, y1, new Faces { All = mat }, omitBottom);

    /// An upright cylinder on a point: pillars, pots, pipes, drums. No bottom cap - it stands on
    /// something - and the top cap is a fan.
    /// `radiusY` makes it an ELLIPTICAL cylinder (x radius stays `radius`); below zero means
    /// circular. The prototype gets the same effect by non-uniformly scaling a unit cylinder.
    public void Cyl(float cx, float cy, float radius, float y0, float y1, string mat, int seg = 12,
                    bool cap = true, float emissiveOverride = -1f, float radiusY = -1f)
    {
        seg = Math.Clamp(seg, 5, 24);
        float h0 = y0 * V, h1 = y1 * V;
        if (h1 - h0 <= 1e-4f || radius <= 1e-4f) return;
        float ry = radiusY > 0f ? radiusY : radius;
        var tex = AreaSurfaces.Get(mat);
        float emis = emissiveOverride >= 0f ? emissiveOverride : AreaSurfaces.EmissiveOf(mat);
        float uRep = Rep(mat, NfMath.Pi * (radius + ry));
        float vRep = Rep(mat, h1 - h0);

        var ring = new NfVec3[seg + 1];
        for (int i = 0; i <= seg; i++)
        {
            float a = i * 2f * NfMath.Pi / seg;
            ring[i] = new NfVec3(cx + MathF.Cos(a) * radius, 0f, cy + MathF.Sin(a) * ry);
        }
        for (int i = 0; i < seg; i++)
        {
            var p = ring[i]; var q = ring[i + 1];
            Quad(new NfVec3(p.X, h0, p.Z), new NfVec3(q.X, h0, q.Z),
                 new NfVec3(q.X, h1, q.Z), new NfVec3(p.X, h1, p.Z),
                 tex, uRep / seg, vRep, ShadeSide, emis);
        }
        if (!cap) return;
        for (int i = 1; i < seg - 1; i++)
        {
            var t = new Tri3
            {
                A = new Vtx3(new NfVec3(ring[0].X, h1, ring[0].Z), 0f, 0f),
                B = new Vtx3(new NfVec3(ring[i].X, h1, ring[i].Z), 1f, 0f),
                C = new Vtx3(new NfVec3(ring[i + 1].X, h1, ring[i + 1].Z), 1f, 1f),
                Tex = tex, Tint = NfColor.White, Shade = ShadeTop, Emissive = emis,
            };
            t.Finish();
            outp.Add(t);
        }
    }

    /// A squashed sphere on a point. `h` is its centre height in AREA units, the radii are in world
    /// units for x and z and area units for y - the same split the prototype uses, because a ball's
    /// radius is not squashed by V while a height is.
    ///
    /// Six segments round and four up, and that is deliberate: a smooth sphere in the planet's own
    /// violet is a dome, and a dome on Polus reads as a bunker rather than a stone. Facets are what
    /// say "rock" at a glance, and they cost a third of what smoothness costs.
    public void Blob(float cx, float cy, float centreH, float rx, float ry, float rz, string mat,
                     int seg = 6, int rings = 4)
    {
        var tex = AreaSurfaces.Get(mat);
        float emis = AreaSurfaces.EmissiveOf(mat);
        float cH = centreH * V;

        NfVec3 P(int i, int j)
        {
            float th = i * 2f * NfMath.Pi / seg;
            float ph = j * NfMath.Pi / rings;
            float s = MathF.Sin(ph);
            return new NfVec3(cx + MathF.Cos(th) * s * rx, cH + MathF.Cos(ph) * ry,
                              cy + MathF.Sin(th) * s * rz);
        }

        for (int j = 0; j < rings; j++)
        {
            for (int i = 0; i < seg; i++)
            {
                var a = P(i, j); var b = P(i + 1, j);
                var c = P(i + 1, j + 1); var d = P(i, j + 1);
                if (j == 0) Tri(a, c, d);
                else if (j == rings - 1) Tri(a, b, c);
                else { Tri(a, b, c); Tri(a, c, d); }
            }
        }

        void Tri(NfVec3 a, NfVec3 b, NfVec3 c)
        {
            var t = new Tri3
            {
                A = new Vtx3(a, 0f, 0f), B = new Vtx3(b, 1f, 0f), C = new Vtx3(c, 1f, 1f),
                Tex = tex, Tint = NfColor.White, Shade = ShadeFace, Emissive = emis,
            };
            t.Finish();
            outp.Add(t);
        }
    }

    /// THE ENVELOPE of an airship: an ellipsoid with an airship's profile - a blunt bow and a
    /// long tapering stern, instead of the symmetrical cigar a plain ellipsoid gives. `rx`/`rz`
    /// are world units, `ry` area units (the same split Blob uses); `centreH` is the centre in
    /// area units. `bow` is the -x end. The profile is the prototype's, world.js airshipBody:
    /// p(x) = (1+x)^0.34 * (1-x)^0.80, normalised, applied to the circular cross-section.
    public void Envelope(float cx, float cy, float centreH, float rx, float ry, float rz,
                         string mat, int seg = 18, int rings = 12)
    {
        var tex = AreaSurfaces.Get(mat);
        float emis = AreaSurfaces.EmissiveOf(mat);
        float cH = centreH * V;
        float pmax = 0f;
        for (float x = -0.999f; x < 1f; x += 0.002f) pmax = MathF.Max(pmax, Prof(x));
        static float Prof(float x) => MathF.Pow(1f + x, 0.34f) * MathF.Pow(1f - x, 0.80f);

        NfVec3 P(int i, int j)
        {
            float th = i * 2f * NfMath.Pi / seg;
            float ph = j * NfMath.Pi / rings;
            float ax = MathF.Cos(ph);                        // -1 (stern) .. 1 (bow), along x
            float s0 = MathF.Max(1e-3f, MathF.Sin(ph));
            float f = Prof(Math.Clamp(-ax, -0.9999f, 0.9999f)) / pmax / s0;
            return new NfVec3(cx + ax * rx, cH + MathF.Cos(th) * s0 * f * ry,
                              cy + MathF.Sin(th) * s0 * f * rz);
        }

        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                var a = P(i, j); var b = P(i + 1, j);
                var c = P(i + 1, j + 1); var d = P(i, j + 1);
                Quad(a, b, c, d, tex, 1f, 1f, ShadeSide, emis);
            }
    }

    /// A single horizontal slab of one material - the planet, a shore, a decal.
    public void Slab(AuRect r, float height, string mat, float shade = 1f)
    {
        float h = height * V;
        var tex = AreaSurfaces.Get(mat);
        Quad(new NfVec3(r.MinX, h, r.MinY), new NfVec3(r.MaxX, h, r.MinY),
             new NfVec3(r.MaxX, h, r.MaxY), new NfVec3(r.MinX, h, r.MaxY),
             tex, Rep(mat, r.Width), Rep(mat, r.Depth), shade, AreaSurfaces.EmissiveOf(mat));
    }

    // ================================================================================
    // An area
    // ================================================================================
    public void BuildArea(Area a)
    {
        float deck = a.Deck;
        float h = a.H;

        foreach (var f in a.Floors ?? Array.Empty<Fl>())
        {
            float d = f.Deck ?? deck;
            float t = f.Thick ?? 0.28f;
            string mat = f.Mat ?? "metalDeck";
            if (f.Disc)
            {
                Cyl(f.Rect.Cx, f.Rect.Cy, MathF.Min(f.Rect.Width, f.Rect.Depth) * 0.5f,
                    d - t, d, mat, 16);
            }
            else
            {
                /*
                 * A SLAB NEVER ROOFS A PIT.
                 *
                 * The Airship's hull lays its underbody at -0.02 under EVERY room, as one closed
                 * surface (hull.js: "der Unterbau laeuft UNTER den Raeumen durch"). Under a room at
                 * deck 0 that is exactly right - the room's floor wins by height and the hull is the
                 * unseen underside. Over the Gap Room's pit it was the opposite: the pit floor sits
                 * at -1.795, the hull slab at -0.02 lay ABOVE it, and from the deck the pit read as a
                 * red floor level with the tiles, with the drums poking through it; from the pit's
                 * own ledge (a real place, reached by ladder_gap) the same slab was a red ceiling a
                 * hand's breadth over one's head. Both were reported, as "a floor that does not
                 * belong there" and as "glitching under the map".
                 *
                 * So a non-pit floor that lies BELOW deck 0 - a slab, never a room floor - is cut
                 * around every pit already registered beneath it. Room floors at deck 0 or above
                 * are left alone on purpose: Polus's lava bridges are floors over a pit and meant
                 * to be. Only the hull area comes last enough, and low enough, to hit this.
                 */
                var pieces = new List<AuRect> { f.Rect };
                if (!f.Pit && d < 0f)
                    foreach (var pd in decks)
                        if (pd.pit && pd.y < d) SubtractRect(pieces, pd.rect);

                foreach (var piece in pieces)
                {
                    // The rim is the visible EDGE of the deck: at a doorway one sees that the
                    // station is built out of slabs sitting a hand's breadth above the planet.
                    Box(piece, d - t, d, new Faces { All = f.Rim ?? "panelSteel", Top = mat },
                        omitBottom: !f.Pit);
                    if (pieces.Count != 1) decks.Add((piece, d, f.Pit));
                }
                // Cut into pieces (or cut away entirely): the decks were registered per piece
                // above, and a slab that lies wholly inside a pit registers nothing at all.
                if (pieces.Count != 1) continue;
            }
            decks.Add((f.Rect, d, f.Pit));
        }

        foreach (var w in a.Walls ?? Array.Empty<Wl>()) Wall(w, deck, h);

        foreach (var s in a.Skirtings ?? Array.Empty<Sk>())
            Box(s.Rect, s.Deck ?? deck, (s.Deck ?? deck) + (s.H ?? 0.13f), s.Mat ?? "darkTrim");

        foreach (var c in a.Ceilings ?? Array.Empty<Ce>())
        {
            float y = (c.Deck ?? deck) + (c.H ?? h);
            // Bright inside, dark plated roof outside: a Polus building seen from the planet is
            // metal. The underside is the only face anyone standing in the room will see.
            Box(c.Rect, y, y + 0.14f,
                new Faces { All = "panelSteel", Bottom = c.Mat ?? "ceilingPanel" }, omitBottom: false);
        }

        foreach (var fx in a.Fixtures ?? Array.Empty<Fx>()) Fixture(fx, fx.Deck ?? deck);
    }

    // ================================================================================
    // Walls
    // ================================================================================
    private void Wall(Wl spec, float areaDeck, float areaH)
    {
        // A diagonal wall (the Skeld's octagons and chamfers) is a completely different shape -
        // no compass sub-pieces, no openings, no soffit - so it is built by its own method and the
        // rest of this one never runs for it.
        if (spec.Diag.HasValue) { DiagWall(spec, areaDeck, areaH); return; }

        var r = spec.Rect;
        float h = spec.H ?? areaH;
        float y0 = spec.Y0 ?? spec.Deck ?? areaDeck;
        bool alongX = (r.MaxX - r.MinX) >= (r.MaxY - r.MinY);
        float a0 = alongX ? r.MinX : r.MinY;
        float a1 = alongX ? r.MaxX : r.MaxY;

        var faces = spec.Faces ?? new Faces { All = spec.Mat ?? "panelCream" };
        faces.Top ??= "panelSteel";
        faces.All ??= "panelSteel";

        AuRect Sub(float b0, float b1) => alongX
            ? new AuRect(b0, r.MinY, b1, r.MaxY)
            : new AuRect(r.MinX, b0, r.MaxX, b1);

        // An opening's span runs along the wall's LONG axis, and which axis that is comes from the
        // rectangle. A near-square wall can flip from x to y on a change of a few centimetres, and
        // then an author's x-values are read as y-values - which once grew an 18-unit beam out of a
        // 0.7-long wall in LifeSupport, straight through Electrical two rooms away. So a span that
        // does not overlap the wall at all is dropped and said out loud.
        var cuts = new List<(float a, float b, string kind, float sill, float head, bool frame)>();
        foreach (var o in spec.Openings ?? Array.Empty<Op>())
        {
            float oa = MathF.Min(o.Span.A, o.Span.B), ob = MathF.Max(o.Span.A, o.Span.B);
            if (ob <= a0 || oa >= a1)
            {
                Scene3D.NightfallLog($"[Nightfall] wall ({r.X0},{r.Y0},{r.X1},{r.Y1}): opening "
                    + $"[{oa:0.##},{ob:0.##}] lies outside its range [{a0:0.##},{a1:0.##}] - ignored");
                continue;
            }
            string kind = o.Kind ?? "door";
            float sill = o.Sill ?? (kind == "window" ? 0.8f : 0f);
            float head = o.Head ?? (kind == "window" ? 1.7f : kind == "gap" ? h : 1.75f);
            cuts.Add((MathF.Max(oa, a0), MathF.Min(ob, a1), kind, sill, head, o.Frame));
        }
        cuts.Sort((p, q) => p.a.CompareTo(q.a));

        // The solid pieces between the openings.
        var pieces = new List<(float, float)>();
        float cur = a0;
        foreach (var c in cuts)
        {
            if (c.a > cur) pieces.Add((cur, MathF.Min(c.a, a1)));
            cur = MathF.Max(cur, c.b);
        }
        if (cur < a1) pieces.Add((cur, a1));
        foreach (var (b0, b1) in pieces) Box(Sub(b0, b1), y0, y0 + h, faces);

        // Skirting on the named sides, stopped at every opening: a skirting board that runs straight
        // through a doorway is the surest sign that a room was extruded rather than built.
        if (!string.IsNullOrEmpty(spec.Skirt))
        {
            float t = spec.SkirtDepth ?? 0.07f, sh = spec.SkirtHeight ?? 0.13f;
            foreach (char side in spec.Skirt)
            {
                foreach (var (b0, b1) in pieces)
                {
                    var p = Sub(b0, b1);
                    var rr = side switch
                    {
                        's' => new AuRect(p.MinX, p.MinY - t, p.MaxX, p.MinY),
                        'n' => new AuRect(p.MinX, p.MaxY, p.MaxX, p.MaxY + t),
                        'w' => new AuRect(p.MinX - t, p.MinY, p.MinX, p.MaxY),
                        _ => new AuRect(p.MaxX, p.MinY, p.MaxX + t, p.MaxY),
                    };
                    Box(rr, y0, y0 + sh, spec.SkirtMat ?? "darkTrim");
                }
            }
        }

        // THE SOFFIT: the underside of the piece of wall over an opening, which is the ceiling of
        // the short tunnel one walks through. It used to fall through to the structural-steel
        // fallback, and so every doorway on Polus had a black slot overhead - invisible from above,
        // which is why nobody noticed for so long.
        string reveal = faces.N ?? faces.S ?? faces.E ?? faces.W ?? faces.All;

        foreach (var c in cuts)
        {
            var rc = Sub(MathF.Max(c.a, a0), MathF.Min(c.b, a1));
            if (c.sill > 0f) Box(rc, y0, y0 + c.sill, faces);
            if (c.head < h)
            {
                var lintel = new Faces
                {
                    N = faces.N, S = faces.S, E = faces.E, W = faces.W,
                    Top = faces.Top, All = faces.All, Bottom = reveal,
                };
                Box(rc, y0 + c.head, y0 + h, lintel, omitBottom: false);
            }

            // A doorway is a place a player WALKS, so its footprint is remembered for
            // SealThresholds: the room floors on either side usually stop at the wall's faces,
            // which leaves a strip of bare planet 0.185 below deck inside every doorway.
            if (c.kind != "window" && c.sill <= 0f)
                thresholds.Add(rc);

            if (c.kind == "window")
            {
                // Glass sits in the middle of the wall's depth so the reveal is visible from both
                // sides. A pane flush with the surface reads as a hole.
                float th = alongX ? rc.MaxY - rc.MinY : rc.MaxX - rc.MinX;
                float mid = alongX ? (rc.MinY + rc.MaxY) * 0.5f : (rc.MinX + rc.MaxX) * 0.5f;
                var pane = alongX
                    ? new AuRect(rc.MinX, mid - th * 0.06f, rc.MaxX, mid + th * 0.06f)
                    : new AuRect(mid - th * 0.06f, rc.MinY, mid + th * 0.06f, rc.MaxY);
                Box(pane, y0 + c.sill + 0.05f, y0 + c.head - 0.05f, Glass(spec.Glass), false);
                Box(pane, y0 + c.sill, y0 + c.sill + 0.05f, "darkTrim");
                Box(pane, y0 + c.head - 0.05f, y0 + c.head, "darkTrim");
            }
            else if (c.kind == "door" && c.frame)
            {
                DoorFrame(rc, alongX, y0, c.head);
            }
        }
    }

    /// Window glass. The rasteriser writes depth for every triangle and has no blending, so a pane
    /// is a solid dark surface rather than a transparent one - which at night, from a dark room, is
    /// very nearly what a window is anyway.
    private static string Glass(string col) => col ?? "#1b2740";

    /// The metal ring around a doorway. Cheap, and it is what makes an opening read as a DOOR.
    ///
    /// A RING AT EACH MOUTH, not a lining of the whole tunnel. The first version built each jamb as
    /// a post running the wall's full DEPTH, and a Polus wall is 0.3 to 0.9 thick, so the post
    /// covered the entire reveal: every doorway in the world became a tunnel panelled in dark steel
    /// however carefully its area file had named the reveal material.
    private void DoorFrame(AuRect rc, bool alongX, float y0, float head)
    {
        const float t = 0.1f;
        const string m = "panelSteel";
        float thick = alongX ? rc.MaxY - rc.MinY : rc.MaxX - rc.MinX;
        float d = MathF.Min(0.09f, thick * 0.5f);
        var ends = alongX
            ? new[] { (rc.MinY, rc.MinY + d), (rc.MaxY - d, rc.MaxY) }
            : new[] { (rc.MinX, rc.MinX + d), (rc.MaxX - d, rc.MaxX) };

        foreach (var (p0, p1) in ends)
        {
            if (alongX)
            {
                Box(new AuRect(rc.MinX, p0, rc.MinX + t, p1), y0, y0 + head, m);
                Box(new AuRect(rc.MaxX - t, p0, rc.MaxX, p1), y0, y0 + head, m);
                Box(new AuRect(rc.MinX, p0, rc.MaxX, p1), y0 + head - t, y0 + head, m, false);
            }
            else
            {
                Box(new AuRect(p0, rc.MinY, p1, rc.MinY + t), y0, y0 + head, m);
                Box(new AuRect(p0, rc.MaxY - t, p1, rc.MaxY), y0, y0 + head, m);
                Box(new AuRect(p0, rc.MinY, p1, rc.MaxY), y0 + head - t, y0 + head, m, false);
            }
        }
    }

    /// A DIAGONAL wall - the Skeld's octagonal Cafeteria, its hull chamfers, the cut corners of
    /// Comms and Shields. Port of src/kit.js' diagWall(): `spec.Diag` is the wall's INNER edge (the
    /// room's own floor line) as p0 -> p1, and the thickness grows to the LEFT of that direction -
    /// so the room itself is on the RIGHT, which is the contract every area file's `diag` entry was
    /// written against. No openings: every doorway on both ships sits in a straight wall, and a
    /// window that needs one is built as three separate diagonal slices instead (see nav.js,
    /// weapons.js) rather than teaching this method to punch a hole.
    private void DiagWall(Wl spec, float areaDeck, float areaH)
    {
        var (x0, y0, x1, y1) = spec.Diag.Value;
        float t = spec.T ?? 0.5f;
        float h = spec.H ?? areaH;
        float y0base = spec.Y0 ?? spec.Deck ?? areaDeck;
        float dx = x1 - x0, dy = y1 - y0, len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6f) return;

        // Left normal of travel p0 -> p1: the direction the WALL'S THICKNESS grows into, i.e. the
        // hull/outside. The room sits on the opposite side (the right of travel), which is why an
        // author orders a diagonal's two points with the room on their right.
        float nx = -dy / len, ny = dx / len;

        var f = spec.Faces ?? new Faces();
        float hh0 = y0base * V, hh1 = (y0base + h) * V;

        // Inner edge (the room's floor line) and outer edge (t further along the normal).
        float qx0 = x1 + nx * t, qy0 = y1 + ny * t;   // outer, at p1's end
        float qx1 = x0 + nx * t, qy1 = y0 + ny * t;   // outer, at p0's end

        // A face of the DiagWall "box": four corners already at their final height, one material
        // lookup, one Quad(). Mirrors Box()'s local Face() helper, including its fallback - a side
        // nobody named still gets the structural steel every hull is skinned in.
        void Face(NfVec3 a, NfVec3 b, NfVec3 c, NfVec3 d, string mat, float w, float hh, float shade)
        {
            mat ??= "panelSteel";
            Quad(a, b, c, d, AreaSurfaces.Get(mat), Rep(mat, w), Rep(mat, hh),
                 shade, AreaSurfaces.EmissiveOf(mat));
        }

        /*
         * A WINDOW BAND (spec.Window, kit.js diagWall): parapet - glass - lintel, instead of one
         * closed face. The Airship's bow is a chain of seven chords with sky-blue glass over the
         * console bank; built closed, the cockpit was a room with no view, and built open (the
         * first attempt) it was a room with no walls.
         */
        if (spec.Window is var win && win.HasValue)
        {
            float sill = MathF.Max(0f, win.Value.Sill), head = MathF.Min(h, win.Value.Head);
            void Band(float a, float b, string inMat, string outMat, float emis)
            {
                if (b - a <= 1e-4f) return;
                float ba = (y0base + a) * V, bb = (y0base + b) * V;
                Face(new NfVec3(x1, ba, y1), new NfVec3(x0, ba, y0), new NfVec3(x0, bb, y0), new NfVec3(x1, bb, y1),
                     inMat, len, b - a, ShadeFace);
                Face(new NfVec3(qx1, ba, qy1), new NfVec3(qx0, ba, qy0), new NfVec3(qx0, bb, qy0), new NfVec3(qx1, bb, qy1),
                     outMat, len, b - a, ShadeSide);
                _ = emis;
            }
            Band(0f, sill, f.Pick('i'), f.Pick('o'), 0f);                      // parapet
            Band(head, h, f.Pick('i'), f.Pick('o'), 0f);                       // lintel
            // The pane itself, both faces in the glass colour (the rasteriser has no blending, so
            // glass is a dark blue plate - the same call a straight wall's window makes).
            Band(sill, head, Glass(spec.Glass), Glass(spec.Glass), 0f);
        }
        else
        {
            // 1. IN - the room side, along the inner edge p0 -> p1.
            Face(new NfVec3(x1, hh0, y1), new NfVec3(x0, hh0, y0), new NfVec3(x0, hh1, y0), new NfVec3(x1, hh1, y1),
                 f.Pick('i'), len, h, ShadeFace);
            // 2. OUT - the hull side, along the outer edge.
            Face(new NfVec3(qx1, hh0, qy1), new NfVec3(qx0, hh0, qy0), new NfVec3(qx0, hh1, qy0), new NfVec3(qx1, hh1, qy1),
                 f.Pick('o'), len, h, ShadeSide);
        }
        // 3+4. The two end caps, one at each mouth of the wall, in the same "cut end is structure"
        // material as a straight wall's own ends.
        Face(new NfVec3(x0, hh0, y0), new NfVec3(qx1, hh0, qy1), new NfVec3(qx1, hh1, qy1), new NfVec3(x0, hh1, y0),
             f.All, t, h, ShadeSide);
        Face(new NfVec3(qx0, hh0, qy0), new NfVec3(x1, hh0, y1), new NfVec3(x1, hh1, y1), new NfVec3(qx0, hh1, qy0),
             f.All, t, h, ShadeSide);
        // 5. TOP - the roof of the wall, seen from above. Same fallback as a straight wall's cap.
        Face(new NfVec3(x0, hh1, y0), new NfVec3(x1, hh1, y1), new NfVec3(qx0, hh1, qy0), new NfVec3(qx1, hh1, qy1),
             f.Top ?? "panelSteel", len, t, ShadeTop);

        // The skirting board, if asked for: a single low, thin face along the inner edge only - no
        // top, no ends, no outer face, because nobody ever sees the back of a skirting board. It sits
        // ROOM-WARD of the inner edge by half its own depth, the same way a straight wall's skirting
        // stands proud of the wall it is nailed to rather than centred on the wall's own face.
        if (spec.DiagSkirt)
        {
            float st = spec.SkirtDepth ?? 0.07f, sh = spec.SkirtHeight ?? 0.13f;
            float ox = -nx * st * 0.5f, oy = -ny * st * 0.5f;
            float sh0 = y0base * V, sh1 = (y0base + sh) * V;
            Face(new NfVec3(x1 + ox, sh0, y1 + oy), new NfVec3(x0 + ox, sh0, y0 + oy),
                 new NfVec3(x0 + ox, sh1, y0 + oy), new NfVec3(x1 + ox, sh1, y1 + oy),
                 spec.SkirtMat ?? "darkTrim", len, sh, ShadeFace);
        }
    }

    // ================================================================================
    // Fixtures
    // ================================================================================
    /// Anything that gives off its own light in a room with no lights: a screen, a lamp tube, a
    /// warning strip, the slots down the side of the gun. `Emissive` on a triangle is a floor under
    /// the lighting, so the surface is visible whether or not the torch is on it.
    private const float GlowLevel = 0.80f;

    /// A LIT FACE, not a lit box.
    ///
    /// There are about two hundred and fifty of these on Polus - every screen, every ceiling tube,
    /// every slot down the side of the gun - and each one was six faces, of which exactly one is
    /// ever seen: a lamp is looked at from underneath, a wall screen from the front. Twelve
    /// triangles became two, which is two thousand five hundred off the map for nothing.
    ///
    /// A single quad is not one-sided here: the rasteriser draws both faces of a triangle, because
    /// a room is a box seen from the inside. So the screen is still there from every angle, it just
    /// has no thickness - and a lit screen has no thickness worth drawing.
    private void Glowing(AuRect r, float y0, float y1, string col, char face)
    {
        var tex = AreaSurfaces.Get(col ?? "#4fc3e8");
        float x0 = r.MinX, x1 = r.MaxX, ya = r.MinY, yb = r.MaxY;
        float h0 = y0 * V, h1 = y1 * V;
        if (h1 - h0 <= 1e-5f) h1 = h0 + 0.005f;
        switch (face)
        {
            case 'n': Quad(new NfVec3(x1, h0, yb), new NfVec3(x0, h0, yb), new NfVec3(x0, h1, yb), new NfVec3(x1, h1, yb), tex, 1, 1, 1f, GlowLevel); break;
            case 's': Quad(new NfVec3(x0, h0, ya), new NfVec3(x1, h0, ya), new NfVec3(x1, h1, ya), new NfVec3(x0, h1, ya), tex, 1, 1, 1f, GlowLevel); break;
            case 'e': Quad(new NfVec3(x1, h0, ya), new NfVec3(x1, h0, yb), new NfVec3(x1, h1, yb), new NfVec3(x1, h1, ya), tex, 1, 1, 1f, GlowLevel); break;
            case 'w': Quad(new NfVec3(x0, h0, yb), new NfVec3(x0, h0, ya), new NfVec3(x0, h1, ya), new NfVec3(x0, h1, yb), tex, 1, 1, 1f, GlowLevel); break;
            case 'b': Quad(new NfVec3(x0, h0, yb), new NfVec3(x1, h0, yb), new NfVec3(x1, h0, ya), new NfVec3(x0, h0, ya), tex, 1, 1, 1f, GlowLevel); break;
            default: Quad(new NfVec3(x0, h1, ya), new NfVec3(x1, h1, ya), new NfVec3(x1, h1, yb), new NfVec3(x0, h1, yb), tex, 1, 1, 1f, GlowLevel); break;
        }
    }

    /// Every sliding door in the world and the run of triangles it owns, so that the game's own
    /// doors can be matched to them and opened. `To` is exclusive.
    public readonly List<(AuRect Rect, int From, int To)> Doors = new();

    private void Fixture(Fx s, float deck)
    {
        float Y0 = (s.Y0 ?? 0f) + deck;

        switch (s.Kind)
        {
            case "block":
            {
                var f = s.Faces ?? new Faces { All = s.Mat ?? "plasticWhite", Top = s.Top ?? s.Mat ?? "plasticWhite" };
                Box(s.Rect.Value, Y0, Y0 + (s.H ?? 1f), f);
                break;
            }

            // Worktop: a recessed body with a slab on top that overhangs. Reads as furniture at a
            // glance, which a plain box never does.
            case "counter":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 0.75f, i = 0.05f, t = 0.07f;
                Box(new AuRect(r.MinX + i, r.MinY + i, r.MaxX - i, r.MaxY - i), Y0, Y0 + h - t, s.Mat ?? "plasticWhite");
                Box(r, Y0 + h - t, Y0 + h, s.Top ?? "darkTrim");
                break;
            }

            case "table":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 0.45f, t = 0.06f;
                if (s.Round)
                {
                    /*
                     * AN ELLIPSE, NOT A DISC OF THE SHORTER RADIUS. `round` means "the drawn
                     * shape is an ellipse over this rectangle" (kit.js scales a unit cylinder by
                     * both half-axes); porting it as min(w,d)/2 shrank Office's 3.7-long meeting
                     * table to a 0.78 bistro table under the full-length hitbox - reported in the
                     * third playtest as "der Tisch ist viel zu klein fuer die Hitbox".
                     */
                    float rx = r.Width * 0.5f, ryE = r.Depth * 0.5f;
                    Cyl(r.Cx, r.Cy, rx, Y0 + h - t, Y0 + h, s.Top ?? s.Mat ?? "woodDark", 20,
                        radiusY: ryE);
                    Cyl(r.Cx, r.Cy, MathF.Min(rx, ryE) * 0.28f, Y0, Y0 + h - t,
                        s.Leg ?? "darkTrim", 8);
                }
                else
                {
                    Box(r, Y0 + h - t, Y0 + h, s.Top ?? s.Mat ?? "woodDark");
                    const float l = 0.07f;
                    foreach (var (a, b) in new[] { (r.MinX, r.MinY), (r.MaxX - l, r.MinY), (r.MinX, r.MaxY - l), (r.MaxX - l, r.MaxY - l) })
                        Box(new AuRect(a, b, a + l, b + l), Y0, Y0 + h - t, s.Leg ?? "darkTrim");
                }
                break;
            }

            // Shipping crate: a body, four corner posts and a lid rim. The first version put a trim
            // plate along each of the four sides, which from outside covered the whole face - every
            // crate in Storage was a black cube.
            case "crate":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 0.7f;
                const float t = 0.07f;
                string trim = s.Trim ?? "#3a5439";
                Box(r, Y0, Y0 + h - 0.06f, s.Mat ?? "crateGreen");
                Box(new AuRect(r.MinX - 0.015f, r.MinY - 0.015f, r.MaxX + 0.015f, r.MaxY + 0.015f),
                    Y0 + h - 0.06f, Y0 + h, trim);
                foreach (var (a, b) in new[] { (r.MinX, r.MinY), (r.MaxX - t, r.MinY), (r.MinX, r.MaxY - t), (r.MaxX - t, r.MaxY - t) })
                    Box(new AuRect(a - 0.012f, b - 0.012f, a + t, b + t), Y0, Y0 + h - 0.06f, trim);
                break;
            }

            // A task console: a pedestal with a lit screen on top.
            case "console":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 0.6f;
                Box(new AuRect(r.MinX + 0.04f, r.MinY + 0.04f, r.MaxX - 0.04f, r.MaxY - 0.04f), Y0, Y0 + h - 0.12f, s.Mat ?? "darkTrim");
                Box(r, Y0 + h - 0.12f, Y0 + h, s.Mat ?? "darkTrim");
                const float i = 0.07f;
                // Capped at 0.55 x 0.45 like kit.js: on a 1 x 0.85 pedestal the full-size screen
                // read as a slab of light, not a screen (review 2026-08-30, cockpit code desk).
                float sw = MathF.Min(r.Width - i * 2f, 0.55f), sd = MathF.Min(r.Depth - i * 2f, 0.45f);
                Glowing(new AuRect(r.Cx - sw * 0.5f, r.Cy - sd * 0.5f, r.Cx + sw * 0.5f, r.Cy + sd * 0.5f),
                        Y0 + h, Y0 + h + 0.02f, s.Screen ?? "#4fc3e8", 't');
                break;
            }

            // A floor vent: the flat grille the game's vents are drawn as. A box with slats, not a
            // sprite, so it still reads as a vent when someone stands on it.
            case "vent":
            {
                var r = s.Rect.Value;
                float b = Y0;
                Box(r, b, b + 0.05f, s.Mat ?? "#5a666b");
                const int n = 3;
                const float ins = 0.07f;
                float lw = (r.MaxY - r.MinY - ins * 2f) / (n * 2f - 1f);
                for (int i = 0; i < n; i++)
                {
                    float ya = r.MinY + ins + i * 2f * lw;
                    Box(new AuRect(r.MinX + ins, ya, r.MaxX - ins, ya + lw), b + 0.05f, b + 0.07f, "#2c3538");
                }
                break;
            }

            /*
             * THE CAFETERIA TABLE: a round top on a pedestal with four curved bench segments round
             * it, port of src/kit.js' cafTable(). `Rx`/`Ry` are the half-extents of the WHOLE unit
             * (benches included), read off the table's own collider bounds - Among Us draws its
             * round tables squashed, so a true circle here would block floor the game lets you
             * walk on.
             *
             * The prototype extrudes the bench ring from a THREE.Shape; this renderer has no
             * extrusion, so each of the four segments is built as a fan of `Steps` flat quads
             * instead - an outward-facing wall quad per step plus a seat-top ring strip per step.
             * No inner face and no floor: both stand on the deck and are never seen (the same "the
             * silhouette is what survives" call the rest of this file makes for coarse geometry).
             */
            case "cafTable":
            {
                var (cx, cy) = s.At.Value;
                float rx = s.Rx ?? 1.42f, ry = s.Ry ?? 1.12f;
                float h = s.H ?? 0.5f;
                string blue = s.Mat ?? "#427da2";
                /*
                 * HALF OF WHAT THE PROTOTYPE'S NUMBERS LOOK LIKE, BECAUSE THEY ARE SCALES, NOT
                 * RADII. kit.js builds these three discs from a cylinder of radius 0.5 and then
                 * overrides the mesh scale - `top.scale.set(rx * 1.24, ...)` on a 0.5 radius is a
                 * disc of rx * 0.62, which is what its own comment says ("2 * 0.62 of the
                 * half-extents") and what cafeteria.js measured ("top out to 0.60 of the
                 * half-extent, bench ring 0.80..1.00"). Ported as `Cyl(..., rx * 1.24f, ...)` -
                 * where the argument IS the radius - every table top came out twice its size and
                 * swallowed the bench ring it is supposed to sit inside: reported from the game as
                 * "die Tischflaeche ist viel zu gross". Leg 0.44 -> 0.22, top 1.24 -> 0.62,
                 * underlip 1.28 -> 0.64.
                 */
                Cyl(cx, cy, rx * 0.22f, Y0, Y0 + h - 0.06f, s.Leg ?? "#3b4750", 24, radiusY: ry * 0.22f);
                Cyl(cx, cy, rx * 0.62f, Y0 + h - 0.06f, Y0 + h, blue, 40, radiusY: ry * 0.62f);
                Cyl(cx, cy, rx * 0.64f, Y0 + h - 0.075f, Y0 + h - 0.06f, "#1b272b", 40, radiusY: ry * 0.64f);

                float bh = s.BenchH ?? 0.30f;
                float gap = (s.Gap ?? 26f) * NfMath.Pi / 180f;
                float seg = NfMath.Pi / 2f - gap;
                var benchTex = AreaSurfaces.Get(blue);
                float benchEmis = AreaSurfaces.EmissiveOf(blue);
                float bh0 = Y0 * V, bh1 = (Y0 + bh) * V;
                const int Steps = 10;
                (float X, float Z) Pt(float a, float scale) =>
                    (cx + rx * scale * MathF.Cos(a), cy + ry * scale * MathF.Sin(a));

                for (int i = 0; i < 4; i++)
                {
                    float ca = NfMath.Pi / 4f + i * NfMath.Pi / 2f;
                    float a0 = ca - seg * 0.5f, a1 = ca + seg * 0.5f;
                    for (int k = 0; k < Steps; k++)
                    {
                        float ta0 = a0 + (a1 - a0) * k / Steps, ta1 = a0 + (a1 - a0) * (k + 1) / Steps;
                        var oa = Pt(ta0, 1.0f); var ob = Pt(ta1, 1.0f);
                        var ia = Pt(ta0, 0.80f); var ib = Pt(ta1, 0.80f);
                        float arc = (rx + ry) * 0.5f * (ta1 - ta0);

                        // The outward vertical face of this step, from the deck up to seat height.
                        Quad(new NfVec3(oa.X, bh0, oa.Z), new NfVec3(ob.X, bh0, ob.Z),
                             new NfVec3(ob.X, bh1, ob.Z), new NfVec3(oa.X, bh1, oa.Z),
                             benchTex, Rep(blue, arc), Rep(blue, bh), ShadeSide, benchEmis);
                        // The seat itself: a ring strip from the outer edge in to 0.80 of it.
                        Quad(new NfVec3(oa.X, bh1, oa.Z), new NfVec3(ob.X, bh1, ob.Z),
                             new NfVec3(ib.X, bh1, ib.Z), new NfVec3(ia.X, bh1, ia.Z),
                             benchTex, Rep(blue, arc), Rep(blue, (rx + ry) * 0.10f), ShadeTop, benchEmis);
                    }
                }
                break;
            }

            // A screen or a picture ON a wall.
            case "panel":
            {
                // `rot` hangs the panel on a DIAGONAL wall: turned about its own `at` point instead
                // of standing axis-aligned. AreaKit.Box() only builds axis-aligned boxes, so the
                // rotated path is a separate method built from Quad() directly (see DiagWall for
                // the same trick). Null/zero leaves this exact axis-aligned path untouched.
                if (s.Rot.HasValue && s.Rot.Value != 0f) { RotatedPanel(s, deck); break; }

                float d = s.D ?? 0.035f;
                var (x, y) = s.At.Value;
                // A panel hangs at 1.0 unless it says otherwise - and "otherwise" includes zero, so
                // this cannot be written as a comparison against the default.
                float w = s.W ?? 0.7f, hh = s.H ?? 0.45f, y0 = (s.Y0 ?? 1.0f) + deck;
                string face = s.Face ?? "s";
                bool horiz = face == "n" || face == "s";
                var rect = horiz
                    ? new AuRect(x - w * 0.5f, y - d * 0.5f, x + w * 0.5f, y + d * 0.5f)
                    : new AuRect(x - d * 0.5f, y - w * 0.5f, x + d * 0.5f, y + w * 0.5f);
                Box(rect, y0, y0 + hh, s.Frame ?? "darkTrim");
                const float inset = 0.05f;
                float o = face == "s" ? d : face == "n" ? -d : 0f;
                float o2 = face == "e" ? d : face == "w" ? -d : 0f;
                var fr = horiz
                    ? new AuRect(x - w * 0.5f + inset, y - d * 0.25f - o * 0.5f, x + w * 0.5f - inset, y + d * 0.25f - o * 0.5f)
                    : new AuRect(x - d * 0.25f + o2 * 0.5f, y - w * 0.5f + inset, x + d * 0.25f + o2 * 0.5f, y + w * 0.5f - inset);
                Glowing(fr, y0 + inset, y0 + hh - inset, s.Screen ?? "#4fc3e8", face[0]);
                break;
            }

            // Potted plant. Polus' offices are full of them and they break up a long wall.
            case "pot":
            {
                var (x, y) = s.At.Value;
                float r = s.R ?? 0.16f, h = s.H ?? 0.3f, stem = s.Stem ?? 0.22f, cr = s.Crown ?? 0.19f;
                string pm = s.Mat ?? "#a8407f", leaf = s.Leaf ?? "#3f8f4c";
                Cyl(x, y, r * 0.82f, Y0, Y0 + h * 0.8f, pm, 10);
                Cyl(x, y, r, Y0 + h * 0.8f, Y0 + h, pm, 10);
                Cyl(x, y, 0.035f, Y0 + h, Y0 + h + stem, "#6b5236", 5);
                // Three overlapping balls read as a shrub; one reads as a bowling ball on a stick.
                Blob(x, y, Y0 + h + stem + cr * 0.7f, cr, cr, cr, leaf);
                Blob(x - cr * 0.6f, y + cr * 0.2f, Y0 + h + stem + cr * 0.4f, cr * 0.72f, cr * 0.72f, cr * 0.72f, leaf);
                break;
            }

            // Water cooler: bottle on a stand. One of the few objects the map draws unmistakably.
            case "cooler":
            {
                var (x, y) = s.At.Value;
                float r = s.R ?? 0.13f;
                Cyl(x, y, r, Y0, Y0 + 0.42f, "#dfe3e6", 10);
                Cyl(x, y, r * 0.95f, Y0 + 0.42f, Y0 + 0.78f, "#7fc8e8", 10);
                break;
            }

            // A drum or gas bottle.
            case "drum":
            {
                var (x, y) = s.At.Value;
                float h = s.H ?? 0.55f, r = s.R ?? 0.2f;
                Cyl(x, y, r, Y0, Y0 + h, s.Mat ?? "#4c6b4a", 10);
                if (!s.NoTrim) Cyl(x, y, r * 1.03f, Y0 + h - 0.06f, Y0 + h, "darkTrim", 10);
                break;
            }

            // Pipe run along a wall. Built as a box rather than a lying cylinder: at torchlight and
            // at seven centimetres of radius the two are the same picture, and one costs a sixth.
            case "pipe":
            {
                var r = s.Rect.Value;
                float rad = s.R ?? 0.07f, y = s.Y0 ?? 1.9f;
                bool alongX = r.Width >= r.Depth;
                var rr = alongX
                    ? new AuRect(r.MinX, r.Cy - rad, r.MaxX, r.Cy + rad)
                    : new AuRect(r.Cx - rad, r.MinY, r.Cx + rad, r.MaxY);
                Box(rr, deck + y - rad, deck + y + rad, s.Mat ?? "#7d8a93", false);
                break;
            }

            case "bench":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 0.32f;
                Box(new AuRect(r.MinX, r.MinY, r.MaxX, r.MaxY), Y0 + h - 0.06f, Y0 + h, s.Mat ?? "woodDark");
                Box(new AuRect(r.MinX + 0.04f, r.MinY + 0.04f, r.MinX + 0.12f, r.MaxY - 0.04f), Y0, Y0 + h - 0.06f, "darkTrim");
                Box(new AuRect(r.MaxX - 0.12f, r.MinY + 0.04f, r.MaxX - 0.04f, r.MaxY - 0.04f), Y0, Y0 + h - 0.06f, "darkTrim");
                break;
            }

            // Tall cupboard / locker bank.
            case "locker":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 1.1f;
                Box(r, Y0, Y0 + h, s.Mat ?? "#5c6a72");
                int n = Math.Max(1, (int)MathF.Round(r.Width / 0.45f));
                for (int i = 1; i < n; i++)
                {
                    float x = r.MinX + r.Width * i / n;
                    // A hair deeper than the body on both sides: flush with it, divider and door
                    // fight for the same pixels and the seam renders as a broken dashed line.
                    Box(new AuRect(x - 0.015f, r.MinY - 0.008f, x + 0.015f, r.MaxY + 0.008f),
                        Y0 + 0.05f, Y0 + h - 0.05f, "darkTrim");
                }
                break;
            }

            // A ceiling lamp. In the prototype this also emits a point light; here it is emissive
            // geometry only - Nightfall is a blackout with a torch in it, and a room lit by its own
            // ceiling would undo the whole feature.
            case "lamp":
            {
                var (x, y) = s.At.Value;
                float w = s.W ?? 0.5f, d = s.D ?? 0.22f, h = (s.Y0 ?? 2.0f) + deck;
                // `post: true` (kit.js): the lamp stands on a pole from the deck up to its housing.
                // Outdoors on the Fungle there is no ceiling to hang it from.
                if (s.Post) Cyl(x, y, s.PostR ?? 0.045f, deck, h - 0.06f, s.PostMat ?? "darkTrim", 8);
                // `housing: false`: only the light. A campfire lights itself, and the housing sat
                // over it as a floating lid (review 2026-08-30).
                if (!s.NoHousing)
                {
                    Box(new AuRect(x - w * 0.5f, y - d * 0.5f, x + w * 0.5f, y + d * 0.5f), h - 0.06f, h, "darkTrim", false);
                    Glowing(new AuRect(x - w * 0.5f + 0.04f, y - d * 0.5f + 0.03f, x + w * 0.5f - 0.04f, y + d * 0.5f - 0.03f),
                            h - 0.09f, h - 0.06f, s.Col ?? "#fff3d8", 'b');
                }
                break;
            }

            // A sliding door in a wall opening, as two leaves that part in the middle. The map draws
            // Polus' doors CLOSED, as a pale panel sitting inside the wall - which is why the first
            // pass built it as a solid block and Office had no way out to the east at all.
            case "door":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 1.78f;
                bool alongX = r.Width >= r.Depth;
                float a0 = alongX ? r.MinX : r.MinY, a1 = alongX ? r.MaxX : r.MaxY;
                float mid = (a0 + a1) * 0.5f;
                int from = outp.Count;
                foreach (int sgn in new[] { -1, 1 })
                {
                    float lo = sgn < 0 ? a0 : mid + 0.01f;
                    float hi = sgn < 0 ? mid - 0.01f : a1;
                    var rr = alongX ? new AuRect(lo, r.MinY, hi, r.MaxY) : new AuRect(r.MinX, lo, r.MaxX, hi);
                    Box(rr, Y0, Y0 + h, new Faces { All = s.Mat ?? "#8ea08f", Top = "darkTrim" });
                }
                Doors.Add((r, from, outp.Count));
                break;
            }

            // Weapons' gun: a cylinder lying on its side, with the lit slots the map draws down the
            // side that faces INTO the room - the map paints them on the south flank because every
            // elevation is folded towards its southern camera, but the room is on the north side.
            case "gun":
            {
                var r = s.Rect.Value;
                bool alongX = r.Width >= r.Depth;
                float len = alongX ? r.Width : r.Depth;
                float rad = (alongX ? r.Depth : r.Width) * 0.5f;
                float h = (s.H ?? 1.1f) - rad;
                var body = alongX
                    ? new AuRect(r.MinX, r.Cy - rad, r.MaxX, r.Cy + rad)
                    : new AuRect(r.Cx - rad, r.MinY, r.Cx + rad, r.MaxY);
                Box(body, deck + h - rad, deck + h + rad, s.Mat ?? "#8f8fc0", false);
                for (int i = 0; i < 3; i++)
                {
                    float f = 0.24f + i * 0.22f;
                    var a = alongX
                        ? new AuRect(r.MinX + len * f, r.Cy + rad * 0.88f, r.MinX + len * (f + 0.16f), r.Cy + rad * 1.03f)
                        : new AuRect(r.Cx + rad * 0.88f, r.MinY + len * f, r.Cx + rad * 1.03f, r.MinY + len * (f + 0.16f));
                    Glowing(a, deck + h - rad * 0.3f / V, deck + h + rad * 0.3f / V, "#8ff0e8", alongX ? 'n' : 'e');
                }
                Box(new AuRect(r.MinX + 0.1f, body.MinY + rad * 0.4f, r.MinX + 0.32f, body.MaxY - rad * 0.4f), deck, deck + h - rad * 0.5f, "darkTrim");
                Box(new AuRect(r.MaxX - 0.32f, body.MinY + rad * 0.4f, r.MaxX - 0.1f, body.MaxY - rad * 0.4f), deck, deck + h - rad * 0.5f, "darkTrim");
                break;
            }

            // The message dish north of the Comms hut. The map has no sprite for it - it is drawn
            // straight into the room sheet - so it is built: a bowl on a mast.
            case "dishAntenna":
            {
                var (x, y) = s.At.Value;
                float r = s.R ?? 0.40f, h = (s.Y0 ?? 1.9f) + deck;
                Cyl(x, y, 0.06f, deck, h, "#9b9483", 8);
                Blob(x, y, h + r * 0.3f, r, r * 0.55f, r * 0.75f, s.Mat ?? "#9b9483", 10, 3);
                break;
            }

            // A SLOPING slab: the dropship's cargo ramp. Built as sixteen level slabs first, and at
            // that pitch each riser is only 0.03 high - and it still read from the snow as a grand
            // staircase up to a ship, because a riser catches the light quite differently from a
            // tread.
            case "ramp":
            {
                var r = s.Rect.Value;
                float ya = r.MinY, yb = r.MaxY;
                float lo = (s.Y0 ?? 0f) + deck, hi = (s.Y1 ?? 1f) + deck;
                var tex = AreaSurfaces.Get(s.Mat ?? "metalDeck");
                float l0 = lo * V, l1 = hi * V;
                Quad(new NfVec3(r.MinX, l0, ya), new NfVec3(r.MaxX, l0, ya),
                     new NfVec3(r.MaxX, l1, yb), new NfVec3(r.MinX, l1, yb),
                     tex, Rep(s.Mat, r.Width), Rep(s.Mat, r.Depth), ShadeTop, 0f);
                break;
            }

            // A BOULDER, and Polus is covered in them. `AuRect` is the footprint the map draws it on.
            // Three overlapping lumps rather than one: one is a dome, and a dome on Polus reads as a
            // bunker. On top sits a cap of snow, which every rock on this map has and which is most
            // of what makes it look like Polus and not like a quarry.
            case "rock":
            {
                var r = s.Rect.Value;
                float rx = r.Width * 0.5f, ry = r.Depth * 0.5f;
                float h = s.H ?? MathF.Min(rx, ry) * 1.55f;
                string m = s.Mat ?? "bedrock";
                foreach (var (ox, oy, f, fh) in new[] { (0f, 0f, 1.00f, 1.00f), (-0.52f, 0.26f, 0.66f, 0.74f), (0.50f, -0.20f, 0.58f, 0.62f) })
                    Blob(r.Cx + ox * rx, r.Cy + oy * ry, deck, rx * f, h * fh * V, ry * f, m);
                if (s.Snow)
                    Blob(r.Cx - rx * 0.08f, r.Cy + ry * 0.22f, deck + h * 0.58f,
                         rx * 0.70f, h * 0.50f * V, ry * 0.70f, "snow");
                break;
            }

            // A SNOWMAN. Ten of them out on the ice, in twos and threes, and one lying on its side.
            // They are the only thing on Polus somebody clearly put there for fun.
            case "snowman":
            {
                var (x, y) = s.At.Value;
                float r = s.R ?? 0.22f;
                const string SNOW = "snow";
                // Worked out in WORLD units and converted back, because a ball's radius is not
                // squashed by V while a height is. Mixing the two once floated the carrot and both
                // eyes a hand's breadth above the head.
                float Up(float worldY) => worldY / V + deck;
                if (s.Fallen)
                {
                    Blob(x, y, Up(r * 0.95f), r, r, r, SNOW);
                    Blob(x + r * 1.55f, y - r * 0.30f, Up(r * 0.72f), r * 0.66f, r * 0.66f, r * 0.66f, SNOW);
                    Box(new AuRect(x + r * 2.05f, y - r * 0.42f, x + r * 2.55f, y - r * 0.26f), Up(r * 0.66f), Up(r * 0.80f), "#e08a2a", false);
                    break;
                }
                Blob(x, y, Up(r * 0.86f), r, r, r, SNOW);
                Blob(x, y, Up(r * 1.88f), r * 0.70f, r * 0.70f, r * 0.70f, SNOW);
                Box(new AuRect(x - 0.028f, y - r * 1.02f, x + 0.028f, y - r * 0.58f), Up(r * 1.84f), Up(r * 1.99f), "#e08a2a", false);
                foreach (float dx in new[] { -r * 0.30f, r * 0.30f })
                    Box(new AuRect(x + dx - 0.025f, y - r * 0.70f, x + dx + 0.025f, y - r * 0.62f), Up(r * 2.06f), Up(r * 2.16f), "#20242a", false);
                break;
            }

            // A railing: posts and a top rail, see-through, which a wall is not.
            /*
             * A RIBBON: rows of polylines at different heights, joined into one skin - the port of
             * kit.js' ribbon(). The Airship needs two of them: the hull FLANK (the bright band the
             * map folds down south of the rooms is the ship's side, not a floor) and, since the
             * exterior pass, the KEEL - the bulging board between deck edge and belly. Built as
             * quads between neighbouring rows; the winding is chosen per quad so the normal points
             * away from the ship's centre, because this renderer lights by normal and a skin turned
             * inside out goes black.
             */
            case "ribbon":
            {
                var rows = s.Rows; var hs = s.Heights;
                if (rows == null || hs == null || rows.Length < 2 || hs.Length != rows.Length) break;
                string mat = s.Mat ?? "panelSteel";
                var tex = AreaSurfaces.Get(mat);
                float emis = AreaSurfaces.EmissiveOf(mat);
                float unit = s.Unit ?? 2f;
                int n = rows[0].Length / 2;
                // The ship's centre in plan, so "outward" has a meaning for the winding test.
                float ccx = 0f, ccy = 0f, cn = 0f;
                for (int r = 0; r < rows.Length; r++)
                    for (int i = 0; i < n; i++) { ccx += rows[r][i * 2]; ccy += rows[r][i * 2 + 1]; cn++; }
                ccx /= MathF.Max(1f, cn); ccy /= MathF.Max(1f, cn);

                for (int r = 0; r < rows.Length - 1; r++)
                {
                    if (rows[r].Length != rows[r + 1].Length) break;
                    float ha = hs[r] * V, hb = hs[r + 1] * V;
                    for (int i = 0; i < n - 1; i++)
                    {
                        var a = new NfVec3(rows[r][i * 2], ha, rows[r][i * 2 + 1]);
                        var b = new NfVec3(rows[r][(i + 1) * 2], ha, rows[r][(i + 1) * 2 + 1]);
                        var c = new NfVec3(rows[r + 1][(i + 1) * 2], hb, rows[r + 1][(i + 1) * 2 + 1]);
                        var d = new NfVec3(rows[r + 1][i * 2], hb, rows[r + 1][i * 2 + 1]);
                        float seg = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                        var nrm = NfVec3.Cross(b - a, d - a);
                        float mx = (a.X + c.X) * 0.5f - ccx, mz = (a.Z + c.Z) * 0.5f - ccy;
                        if (nrm.X * mx + nrm.Z * mz < 0f) Quad(a, d, c, b, tex, MathF.Max(1f, seg / unit), 1f, ShadeSide, emis);
                        else Quad(a, b, c, d, tex, MathF.Max(1f, seg / unit), 1f, ShadeSide, emis);
                    }
                }
                break;
            }

            case "rail":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 0.55f;
                const float t = 0.05f;
                bool horiz = r.Width >= r.Depth;
                string m = s.Mat ?? "darkTrim";
                Box(r, Y0 + h - t, Y0 + h, m);
                float len = horiz ? r.Width : r.Depth;
                int n = Math.Max(2, (int)MathF.Round(len / 0.7f));
                for (int i = 0; i <= n; i++)
                {
                    float f = i / (float)n;
                    float px = horiz ? r.MinX + r.Width * f : r.Cx;
                    float py = horiz ? r.Cy : r.MinY + r.Depth * f;
                    Box(new AuRect(px - t * 0.5f, py - t * 0.5f, px + t * 0.5f, py + t * 0.5f), Y0, Y0 + h - t, m);
                }
                break;
            }

            /// A LADDER: two rails and rungs, standing upright. The twin of kit.js' `ladder`,
            /// and it has to stay a twin - the offline render tool and the browser are compared
            /// against each other, so a difference here shows up as the picture disagreeing with
            /// itself rather than as an error.
            ///
            /// Among Us draws a ladder as a path NORTHWARD: foot and head marker share an x and
            /// differ in y, so its drawn length is a HEIGHT here, not a distance. The walkable
            /// ground under it is a steep flight of steps over the same band, because GroundAt has
            /// to stay single-valued; this fixture is the ladder that band actually is. Nobody
            /// sees the two disagree - every one of the seven bands is walled off at both ends.
            ///
            /// The 0.1154 rung spacing is measured off the game's own ladder sprites (6 px at
            /// ~52 px per unit, identical on Airship and Fungle), not chosen. Defaults are
            /// duplicated from kit.js rather than exported, so a plain `rect`/`y0`/`h`/`mat`
            /// fixture round-trips through the exporter untouched.
            case "ladder":
            {
                var r = s.Rect.Value;
                float h = s.H ?? 1f;
                const float rail = 0.06f, step = 0.1154f, t = 0.032f;
                string m = s.Mat ?? "darkTrim";
                float x0 = r.MinX, x1 = r.MaxX;
                Box(new AuRect(x0, r.MinY, MathF.Min(x0 + rail, x1), r.MaxY), Y0, Y0 + h, m);
                Box(new AuRect(MathF.Max(x1 - rail, x0), r.MinY, x1, r.MaxY), Y0, Y0 + h, m);
                float ix0 = MathF.Min(x0 + rail, x1), ix1 = MathF.Max(x1 - rail, x0);
                if (ix1 > ix0)
                {
                    for (float yy = Y0 + step; yy + t <= Y0 + h + 1e-4f; yy += step)
                        Box(new AuRect(ix0, r.MinY, ix1, r.MaxY), yy, yy + t, m);
                }
                break;
            }

            default:
                Scene3D.NightfallLog($"[Nightfall] unknown fixture kind \"{s.Kind}\" - skipped");
                break;
        }
    }

    /// A wall panel turned `Rot` degrees counter-clockwise (seen from above) about its own `At`
    /// point, for one hung on a diagonal wall. The frame and the glowing screen are each built as
    /// ONE flat quad rather than as a box: this object's depth is a few centimetres and it is
    /// never seen edge-on from inside the room, which is the same "a lit face, not a lit box" call
    /// Glowing() already makes for every screen in the game - it just was not turned before now.
    /// Every corner is rotated by hand around `(x, y)` rather than through a matrix, so the
    /// un-rotated path above stays the one place that shape is defined.
    private void RotatedPanel(Fx s, float deck)
    {
        float d = s.D ?? 0.035f;
        var (x, y) = s.At.Value;
        float w = s.W ?? 0.7f, hh = s.H ?? 0.45f, y0 = (s.Y0 ?? 1.0f) + deck;
        string face = s.Face ?? "s";
        bool horiz = face == "n" || face == "s";

        float rad = s.Rot.Value * NfMath.Pi / 180f;
        float ct = MathF.Cos(rad), st = MathF.Sin(rad);
        // A point given as an offset from (x, y), turned about (x, y) and dropped back into world
        // space - the by-hand rotation the doc comment promises.
        (float X, float Y) Turn(float lx, float ly) => (x + lx * ct - ly * st, y + lx * st + ly * ct);

        // The frame: the same rectangle the axis-aligned path builds via `Box(rect, ...)`, as two
        // corners (bottom-left/bottom-right in the panel's own local space) instead of an AuRect.
        float flx0 = horiz ? -w * 0.5f : -d * 0.5f, fly0 = horiz ? -d * 0.5f : -w * 0.5f;
        float flx1 = horiz ? w * 0.5f : d * 0.5f, fly1 = horiz ? d * 0.5f : w * 0.5f;
        var p0 = Turn(flx0, fly0); var p1 = Turn(flx1, fly0);
        string frameMat = s.Frame ?? "darkTrim";
        float fh0 = y0 * V, fh1 = (y0 + hh) * V;
        Quad(new NfVec3(p1.X, fh0, p1.Y), new NfVec3(p0.X, fh0, p0.Y),
             new NfVec3(p0.X, fh1, p0.Y), new NfVec3(p1.X, fh1, p1.Y),
             AreaSurfaces.Get(frameMat), Rep(frameMat, w), Rep(frameMat, hh),
             ShadeFace, AreaSurfaces.EmissiveOf(frameMat));

        // The screen: the same offset arithmetic the axis-aligned path uses for `fr`, worked out in
        // the panel's own local space (relative to `at`) before the turn is applied.
        const float inset = 0.05f;
        float o = face == "s" ? d : face == "n" ? -d : 0f;
        float o2 = face == "e" ? d : face == "w" ? -d : 0f;
        float slx0, sly0, slx1, sly1;
        if (horiz)
        {
            slx0 = -w * 0.5f + inset; slx1 = w * 0.5f - inset;
            float cy = -o * 0.5f;
            sly0 = cy - d * 0.25f; sly1 = cy + d * 0.25f;
        }
        else
        {
            float cx = o2 * 0.5f;
            slx0 = cx - d * 0.25f; slx1 = cx + d * 0.25f;
            sly0 = -w * 0.5f + inset; sly1 = w * 0.5f - inset;
        }
        var q0 = Turn(slx0, sly0); var q1 = Turn(slx1, sly0);
        var tex = AreaSurfaces.Get(s.Screen ?? "#4fc3e8");
        float sh0 = (y0 + inset) * V, sh1 = (y0 + hh - inset) * V;
        if (sh1 - sh0 <= 1e-5f) sh1 = sh0 + 0.005f;
        Quad(new NfVec3(q0.X, sh0, q0.Y), new NfVec3(q1.X, sh0, q1.Y),
             new NfVec3(q1.X, sh1, q1.Y), new NfVec3(q0.X, sh1, q0.Y),
             tex, 1f, 1f, 1f, GlowLevel);
    }

    // ================================================================================
    // The planet
    // ================================================================================
    /// The rectangles the ground is NOT laid in: the two lava basins and the bank above each of
    /// them. They are the basins' bounding boxes, not the lava - the lake is a lens and the strand
    /// a taper, and the difference between box and lava is filled in areas/outside.js with shore
    /// slabs of dust whose own sides are the bank down to it. So the shoreline stays the drawn one
    /// without a second row of walls having to keep step with it.
    ///
    /// The gap between the two, x 37.94 to 40.17, is the width of the upper tube including both its
    /// walls. The strand does run through there on the map - it is simply underneath the walkway,
    /// which is the whole reason that walkway is a GRATING for those three units and plate elsewhere.
    private static readonly AuRect[] Gorges =
    {
        new(30.90f, -17.95f, 37.94f, -15.05f),   // the lake, west of the walkway
        new(40.17f, -15.80f, 44.56f, -15.00f),   // the strand, east of it
        // ... and the BANK above each of them. The escarpment is the ground going down, not a ridge
        // standing up, so the plane has to be cut back to its rim as well - or the ledge half way
        // down is a ledge under a lid.
        new(31.30f, -15.05f, 31.80f, -14.45f),
        new(31.80f, -15.05f, 32.40f, -14.00f),
        new(32.40f, -15.05f, 33.10f, -13.75f),
        new(33.10f, -15.05f, 37.94f, -13.58f),
        new(40.17f, -15.00f, 44.56f, -13.50f),

        /*
         * THE WEST RAVINE, around the left seismic stabiliser (third playtest, picture 35): the
         * map paints a canyon there and the game FENCES it (the `bridgeLeft` edge collider - the
         * stair is literally named a bridge - plus `Dropship/Walls`, which pens the whole spawn
         * plateau). The stair strip keeps a spine of ground beneath it (a shore slab in
         * outside.js), and the platform disc is excluded so GroundAt never sees a pit under a
         * walkable deck.
         *
         * EVERY SOUTH EDGE HERE IS A MEASURED REACHABILITY RIM, not a guess at the fence. The note
         * that used to stand here said the pocket west of x 2.70 was walkable in vanilla and that
         * the holes therefore had to stay timid; a flood fill from the spawn over the real collider
         * dump (..\..\..\tmp\reach.mjs) says it is not - Electrical's north wall and bridgeLeft's
         * west end seal it. In this whole canyon the only reachable ground is the stair corridor
         * and the platform. The rims below are the northernmost transform position a crewmate can
         * occupy per x span; the bank starts there and the drop follows 0.30 later, which puts the
         * edge about a quarter unit beyond the last reachable collider centre.
         *
         * The matching floors are in areas/outside.js and must be changed with these together.
         */
        new(2.70f, -7.35f, 4.98f, -5.00f),    // south band, west of + under the stair foot
        new(2.70f, -5.00f, 3.80f, -3.30f),    // beside the platform (3.85 overlapped its disc)
        new(4.28f, -5.00f, 4.98f, -4.40f),    // the stair strip's top piece
        new(2.70f, -3.30f, 5.50f, 2.00f),     // north of platform and stair
        new(5.15f, -7.30f, 5.50f, -3.30f),    // the tongue east of the stair's east rail
        new(5.50f, -7.20f, 7.90f, 2.00f),     // east of the stair, segment by segment along the fence
        new(7.90f, -7.15f, 10.30f, 2.00f),
        new(10.30f, -6.90f, 10.75f, 2.00f),
        new(10.75f, -6.05f, 11.55f, 2.00f),   // the ledge the dropship's valve box stands on
        new(11.55f, -6.65f, 12.10f, 2.00f),
        new(12.10f, -5.50f, 13.90f, 2.00f),   // the box cluster keeps its wider shelf

        /*
         * THE EAST RAVINE, around the right seismic stabiliser. Built in this pass, from the same
         * measurement, and against the previous survey's warning that the painted canyon there was
         * "mostly walkable in vanilla (hidden pockets!)". It is not: between x 19.5 and 28.3 the
         * only reachable ground north of the fence is the stair corridor (x 23.65..24.60) and its
         * platform. The west end clears the dropship's hull (which reaches x 19.44) and its rim
         * runs north of the boxclust2 crates instead of under them; the east end stops at 28.30 and
         * is closed with rocks on solid ground rather than more holes, because the plain north of
         * the laboratory starts just past it.
         */
        new(19.50f, -6.05f, 21.30f, 2.30f),
        new(21.30f, -6.75f, 21.90f, 2.30f),
        new(21.90f, -6.45f, 23.60f, 2.30f),
        new(23.60f, -7.20f, 24.75f, 2.30f),   // includes the spine under the stair
        new(24.75f, -6.90f, 26.20f, 2.30f),
        new(26.20f, -6.55f, 27.40f, 2.30f),
        new(27.40f, -7.00f, 28.30f, 2.30f),
    };

    /// The planet, WITH HOLES IN IT.
    ///
    /// The ground used to be one plane. That is right everywhere but along one line: the lava is the
    /// only place on Polus where the ground is not flat, and a pit underneath an unbroken plane is a
    /// pit nobody can see. So the surface is cut into horizontal BANDS at every y-edge of every
    /// hole; inside a band the set of holes is constant, so each band is laid as the gaps between
    /// their x-ranges.
    ///
    /// It is then cut again into patches, which the prototype does not need to do: the renderer
    /// buckets a triangle by its CENTRE, so one quad the size of the planet would be findable only
    /// from the middle of the map and the ground would vanish from every corner of it.
    public void BuildPlanet(float x0, float y0, float x1, float y1, float patch = 6f)
    {
        var edges = new List<float> { y0, y1 };
        foreach (var g in Gorges)
        {
            if (g.MinY > y0 && g.MinY < y1) edges.Add(g.MinY);
            if (g.MaxY > y0 && g.MaxY < y1) edges.Add(g.MaxY);
        }
        edges.Sort();

        for (int i = 0; i < edges.Count - 1; i++)
        {
            float ya = edges[i], yb = edges[i + 1];
            if (yb - ya < 1e-4f) continue;
            float mid = (ya + yb) * 0.5f;

            var spans = new List<(float a, float b)>();
            foreach (var g in Gorges)
                if (g.MinY < mid && g.MaxY > mid) spans.Add((g.MinX, g.MaxX));
            spans.Sort((p, q) => p.a.CompareTo(q.a));

            float x = x0;
            foreach (var (a, b) in spans)
            {
                if (a > x) Patches(x, ya, a, yb, patch);
                x = MathF.Max(x, b);
            }
            if (x < x1) Patches(x, ya, x1, yb, patch);
        }
    }

    private void Patches(float x0, float y0, float x1, float y1, float patch)
    {
        int nx = Math.Max(1, (int)MathF.Ceiling((x1 - x0) / patch));
        int ny = Math.Max(1, (int)MathF.Ceiling((y1 - y0) / patch));
        for (int j = 0; j < ny; j++)
        {
            float a = y0 + (y1 - y0) * j / ny, b = y0 + (y1 - y0) * (j + 1) / ny;
            for (int i = 0; i < nx; i++)
                Slab(new AuRect(x0 + (x1 - x0) * i / nx, a, x0 + (x1 - x0) * (i + 1) / nx, b),
                     PlanetDeck, "dust");
        }
    }
}
