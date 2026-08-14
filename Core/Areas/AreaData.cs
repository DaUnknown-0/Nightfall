// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * THE AREA FORMAT, on the mod's side.
 *
 * These types are the C# shape of what Assets/NightfallWeb/src/areas/*.js contains: seventeen files
 * describing Polus room by room, every number read off a printed grid laid over the map photograph
 * and then checked on foot from eye height inside the prototype.
 *
 * WHY THIS REPLACES WHAT THE MOD USED TO DO
 * -----------------------------------------
 * Until now the mod worked the map out at runtime: it took the collision outlines out of the live
 * scene and extruded them into walls. A collider is not a wall. It is the line a crewmate stops at -
 * it walks into every door recess and back out, wraps around crates, ends in mid-air, and in
 * Electrical it follows a chain-link fence that is not a wall at all. It also has no windows, no
 * skirting, no door frames and no sills, because the game never needs to collide with those. So the
 * rooms came out as approximately the right boxes with none of the things that make a room readable.
 *
 * The area files are the opposite: a wall is written down as its PLAN FOOTPRINT (from the edge of
 * one floor to the edge of the next, which is why Polus' walls are 0.3 to 0.9 thick and every
 * doorway is a short tunnel), with its openings, its facing materials per compass side, and the
 * furniture that stands in front of it.
 *
 * ONE COORDINATE CONVENTION, AND IT IS AMONG US'
 * ----------------------------------------------
 * Every number in here and in the generated data is in Among Us world coordinates: x east, y north,
 * no height. The renderer's world is (x, height, y) - see AreaKit.Box, which is the single place
 * that conversion happens, exactly as src/coords.js is in the prototype.
 */

using System;

namespace Nightfall.Core;

/// An Among Us rectangle. Kept as written; `Norm` is what callers use.
public readonly struct AuRect
{
    public readonly float X0, Y0, X1, Y1;
    public AuRect(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }

    public float MinX => MathF.Min(X0, X1);
    public float MaxX => MathF.Max(X0, X1);
    public float MinY => MathF.Min(Y0, Y1);
    public float MaxY => MathF.Max(Y0, Y1);
    public float Width => MaxX - MinX;
    public float Depth => MaxY - MinY;
    public float Cx => (X0 + X1) * 0.5f;
    public float Cy => (Y0 + Y1) * 0.5f;
}

/// The material on each compass side of a box. Naming the compass side rather than "front" and
/// "back" is deliberate: whoever reads the map reads it with north up, and a wall's inner face is
/// on a compass side, not on a side of a mesh.
public sealed class Faces
{
    public string N, S, E, W, Top, Bottom, All;
    /// The two faces of a diagonal wall (see `Wl.Diag`): `In` is the room side, `Out` the hull
    /// side. Straight walls never set these.
    public string In, Out;

    public string Pick(char side) => side switch
    {
        'n' => N ?? All,
        's' => S ?? All,
        'e' => E ?? All,
        'w' => W ?? All,
        't' => Top ?? All,
        'b' => Bottom ?? All,
        'i' => In ?? All,
        'o' => Out ?? All,
        _ => All,
    };
}

/// An opening in a wall: a span in world coordinates along the wall's long axis.
///   door   - open from the floor to `Head`, with a frame around it
///   window - open from `Sill` to `Head`, glazed
///   gap    - open all the way up, no frame: the room simply continues
public sealed class Op
{
    public (float A, float B) Span;
    public string Kind = "door";
    public float? Sill, Head;
    public bool Frame = true;
}

/// A floor slab. A slab and not a plane, so that it has an EDGE: at a doorway one sees that the
/// deck is a thing the station is built out of, a hand's breadth above the planet.
public sealed class Fl
{
    public AuRect Rect;
    public string Mat, Rim;
    public float? Deck, Thick;
    /// A round deck (the seismic stabilisers): `AuRect` stays the bounding square.
    public bool Disc;
    /// A pit wins over the "highest deck under your feet" rule. Only the lava gorge is one.
    public bool Pit;
}

public sealed class Wl
{
    public AuRect Rect;
    public float? H, Deck, Y0;
    public string Mat, SkirtMat, Glass;
    public Faces Faces;
    /// Compass sides that get a skirting board, as a string of letters ("n", "we").
    public string Skirt;
    public float? SkirtDepth, SkirtHeight;
    public Op[] Openings;

    /// A DIAGONAL wall's footprint, in place of `Rect`: the INNER edge (the room side) as a line
    /// p0 -> p1 in Among Us coordinates. Thickness grows to the LEFT of that direction (see
    /// AreaKit.DiagWall) - an author orders the two points with the room on the right, exactly the
    /// contract src/kit.js' diagWall() uses. `T` is that thickness; `DiagSkirt` asks for a skirting
    /// board along the inner edge only (a diagonal wall never takes `Skirt`'s compass letters).
    public (float X0, float Y0, float X1, float Y1)? Diag;
    public float? T;
    public bool DiagSkirt;
}

public sealed class Sk
{
    public AuRect Rect;
    public float? Deck, H;
    public string Mat;
}

public sealed class Ce
{
    public AuRect Rect;
    public float? Deck, H;
    public string Mat;
}

/// One piece of furniture. `Kind` selects the builder in AreaKit; the rest are its parameters, and
/// which ones apply depends on the kind. Written flat rather than as a class per kind because the
/// data is generated, and a generated file with nineteen types in it is harder to read than one
/// with nineteen switch arms.
public sealed class Fx
{
    public string Kind;
    public AuRect? Rect;
    public (float X, float Y)? At;

    public float? H, Y0, Y1, R, W, D, Deck, Thick, Stem, Crown, Power, Range, Decay, Tilt, Depth;
    public string Mat, Top, Leg, Trim, Screen, Frame, Col, Leaf, Face;
    /// `cafTable`'s half-extents (`Rx`/`Ry`), bench height and gap-at-the-cardinals angle (degrees).
    public float? Rx, Ry, BenchH, Gap;
    /// `panel`'s rotation, degrees counter-clockwise seen from above, for one hung on a diagonal
    /// wall. Null/0 leaves the existing axis-aligned path untouched.
    public float? Rot;
    public bool Round, Fallen;
    public bool Snow = true;
    /// `trim: false` on a drum: a bare ring, like the band round the drill, has no lid.
    public bool NoTrim;
    /// `frame: false` on a door opening.
    public bool NoFrame;
    public Faces Faces;
}

public sealed class Area
{
    public string Id;
    public float Deck;
    public float H = 2.1f;
    public Fl[] Floors;
    public Wl[] Walls;
    public Sk[] Skirtings;
    public Ce[] Ceilings;
    public Fx[] Fixtures;

    /// Places worth standing, named, from the area file's own jump list. The prototype's HUD uses
    /// them and so does the render tool: an author picked these because they show the room, and a
    /// viewpoint worked out from the collision data instead put half the cameras outside the
    /// building looking at its back.
    public (string Name, float X, float Y)[] Look;
}
