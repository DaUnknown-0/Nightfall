// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * Scene3D - turns the measured map into a model that can be walked through.
 *
 * The inputs are the two things the mod already has: the collision geometry lifted out of the live
 * scene, and the photograph of the map. From those it builds:
 *
 *   WALLS      every full-height collision segment becomes a quad from floor to ceiling, textured
 *              with a drawn wall surface tinted to the colour the photograph shows beside it.
 *   DOORS      segments belonging to a door get a door surface and can be switched off when the
 *              door opens, which is what "you could see through closed doors" was missing.
 *   PROPS      waist-high colliders are grouped by the object they came from and rebuilt as BOXES:
 *              a front, three sides and a top. Consoles get a lit screen on the side facing into
 *              the room. This is what turns a prop from a sticker on the floor into a thing.
 *   FLOOR      a grid of quads carrying the real floor colours out of the photograph.
 *   CEILING    the same grid, but only over the cells the map marks as indoors.
 *
 * Everything is bucketed into a coarse grid so a frame only pays for the triangles near the player.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public sealed class Scene3D
{
    public readonly List<Tri3> All = new();

    /// Height of a full wall, in world units. A crewmate is about 0.7 tall, so this is a room that
    /// reads as a room rather than a crawlspace.
    public const float WallTop = 1.9f;
    public const float PropHeight = 0.62f;
    public const float TallHeight = 1.45f;

    /// The floor is one big sheet cut into patches. The patches used to be small because each one
    /// carried a single sampled colour and the detail of the ground came from having many of them.
    /// It does not any more - the floor reads the photograph per pixel - so they can be large, and
    /// being large is worth real money: at one unit the floor alone was six thousand triangles and
    /// three quarters of the cost of a frame.
    private const float FloorPatch = 3.0f;

    /// The ceiling stays fine-grained, because its patches are also its OUTLINE: a patch is only
    /// roofed when all four of its corners are indoors, so a coarse grid would eat the edge of
    /// every room and roof over the walkways outside.
    private const float CeilingPatch = 0.75f;

    // ---- spatial index ----
    private float cell = 4f;
    private NfVec2 min, max;
    private int gw, gh;
    private List<Tri3>[] buckets = Array.Empty<List<Tri3>>();

    /// Triangles belonging to a door, so they can be hidden when it opens.
    private readonly Dictionary<int, List<Tri3>> doorTris = new();
    private readonly HashSet<int> openDoors = new();

    public int TriangleCount => All.Count;

    /// GUESSING FURNITURE OUT OF THE FLOOR PLAN IS OFF, AND STAYS OFF.
    ///
    /// PropFinder looks for patches of the artwork that differ from the floor around them and calls
    /// them objects. Measured against the map it gets a minority of them right and is confidently
    /// wrong about the rest: it built a box the size of the laboratory, gave the dropship's ramp a
    /// wall of grey crates, and put a second console on top of every console that already had a
    /// collider. Side by side, the same room with the guessing switched off reads immediately as
    /// Polus - the ramp is the ramp, with its yellow grid, and the crew stand on it.
    ///
    /// An empty room is a smaller lie than a wrong one, and the real furniture is coming from
    /// SpriteHarvest, which does not guess: it cuts the objects out of the scene.
    public static bool GuessPropsFromArtwork;

    // ================================================================================
    /// THE HAND-BUILT WORLD WINS WHERE THERE IS ONE.
    ///
    /// Polus has been described room by room in Assets/NightfallWeb/src/areas - walls with their
    /// real thickness, their openings, their facing materials, and the furniture in front of them.
    /// That is a better map than anything this file can work out at runtime from collision outlines
    /// and a photograph taken from above, and where it exists it is used instead.
    ///
    /// The old path stays for the four maps that have not been described yet, and the render tool
    /// can ask for it with --colliders to put the two side by side.
    public static bool UseAreas = true;

    public static Scene3D Build(MapModel map) => Build(map, default);

    /// <param name="platform">The Airship's moving platform, if the game has one and the caller
    /// could read it: its two end positions and its radius. Nothing in the map data describes it,
    /// because it is not a place, it is a thing that moves - see BuildPlatformSlots.</param>
    public static Scene3D Build(MapModel map, PlatformSpec platform)
    {
        // The sky is baked, and baking it takes long enough to be felt. Doing it here puts the cost
        // where the round is already paying one, instead of on the first frame after the
        // transformation - which is the single worst moment in the game to drop one.
        NightSky.EnsureBuilt();

        if (UseAreas && MapAreaRegistry.AppliesTo(map.MapKey)) return BuildFromAreas(map, platform);
        return BuildFromColliders(map);
    }

    /// The moving platform as the game reports it, in Among Us world units.
    public struct PlatformSpec
    {
        public NfVec2 Left, Right;
        public float Radius;
        public bool Valid;
    }

    // ================================================================================
    /// The built world: seventeen area files, plus the planet they stand on.
    private static Scene3D BuildFromAreas(MapModel map, PlatformSpec platform)
    {
        var s = new Scene3D();
        var b = new AreaBuilder(s.All);

        var lo = map.Geometry.Min;
        var hi = map.Geometry.Max;
        /*
         * THE GROUND HAS TO OUTRUN THE FOG, and 14 did not. The geometry bounds are the collider
         * hull plus two, which is only about eight units past the drawn edge of the map - and the
         * fog does not even START until 18.9 (0.45 of the 42 view distance). Standing anywhere near
         * the rim one looked over a sharp edge of ground at black space with stars UNDER the
         * horizon: the second playtest's "the world just stops".
         *
         * 48 puts the edge past the full fog distance from every standable spot, which is the same
         * reasoning behind the prototype's 140-by-120 plain. The old worry that every extra
         * triangle "still costs a bucket to walk" stopped being true when Query gained its distance
         * cap and per-cell frustum test: cells past the view distance are never visited at all, so
         * the far plain costs a few hundred triangles of memory and nothing per frame.
         */
        MapAreaRegistry.BuildExterior(map.MapKey, b, lo.X - 48f, lo.Y - 48f, hi.X + 48f, hi.Y + 48f);

        foreach (var a in MapAreaRegistry.Build(map.MapKey)) b.BuildArea(a);
        // After ALL areas: whether a doorway is missing its floor depends on the decks of both
        // rooms beside it, and one of the two may be built later than the wall.
        b.SealThresholds();

        if (platform.Valid) s.BuildPlatformSlots(b, platform);

        s.areas = b;
        s.MatchDoors(map, b);
        s.BuildIndex(map);
        // The material catalogue said out loud, because it is the biggest allocation the mod
        // makes and it varies by a factor of six between maps: Polus draws 32 materials
        // (7,2 MB), Mira HQ 215 (46,4 MB). Among Us is a 32-bit process, and the first build
        // that ever ran on Mira exhausted its address space - a number here turns the next
        // such case from guesswork into a reading.
        NightfallLog($"built world from area data: {s.All.Count} triangles, "
                     + $"{AreaSurfaces.Count} materials holding "
                     + $"{AreaSurfaces.RetainedBytes_ / 1048576.0:0.0} MB");
        return s;
    }

    /// TIE EACH BUILT DOOR TO THE GAME'S OWN DOOR.
    ///
    /// The area files draw sixteen sliding doors, because the map draws them closed and a solid
    /// panel is what they are most of the time. But a door that never opens is worse than no door:
    /// the werewolf and its prey both walk through them, and if the model keeps them shut the view
    /// shows a wall where the player has just run through an opening.
    ///
    /// The game's doors arrive as colliders, which is exactly what a collider IS good for - a door
    /// is a moving thing with a position and an IsOpen. So each built door is matched to the door
    /// collider whose outline it overlaps, and from then on the existing SetDoorOpen path works
    /// unchanged. Matching by overlap rather than by nearest centre because two doors of a
    /// decontamination lock stand a metre and a half apart and the nearest centre is a coin toss.
    private void MatchDoors(MapModel map, AreaBuilder b)
    {
        // Nearest anchor wins, each anchor only once. The two doors of a decontamination lock stand
        // a metre and a half apart, so "nearest" has to be resolved in order of how good the match
        // is, not in the order the doors happen to be built.
        var pairs = new List<(float d2, int door, int anchor)>();
        for (int i = 0; i < b.Doors.Count; i++)
        {
            var r = b.Doors[i].Rect;
            for (int j = 0; j < map.DoorAnchors.Count; j++)
            {
                float dx = map.DoorAnchors[j].Centre.X - r.Cx;
                float dy = map.DoorAnchors[j].Centre.Y - r.Cy;
                float d2 = dx * dx + dy * dy;
                // A door drawn at the outer end of a porch and its collider inside the wall are up
                // to a unit apart (Comms and Weapons both are). Past two units it is a different
                // door.
                if (d2 <= 4f) pairs.Add((d2, i, j));
            }
        }
        pairs.Sort((p, q) => p.d2.CompareTo(q.d2));

        var doorTaken = new bool[b.Doors.Count];
        var anchorTaken = new bool[map.DoorAnchors.Count];
        int matched = 0;
        foreach (var (_, di, ai) in pairs)
        {
            if (doorTaken[di] || anchorTaken[ai]) continue;
            doorTaken[di] = anchorTaken[ai] = true;
            int src = map.DoorAnchors[ai].SourceId;
            if (!doorTris.TryGetValue(src, out var l)) doorTris[src] = l = new List<Tri3>();
            for (int i = b.Doors[di].From; i < b.Doors[di].To; i++) l.Add(All[i]);
            matched++;
        }

        for (int i = 0; i < b.Doors.Count; i++)
        {
            if (doorTaken[i]) continue;
            var rect = b.Doors[i].Rect;
            // Not every panel the map draws as a door is one the game can open: the dropship's
            // hatch does not move. Said out loud all the same, because the other reason for a miss
            // is a door built in the wrong place.
            NightfallLog($"[Nightfall] built door at ({rect.Cx:0.##}, {rect.Cy:0.##}) has no "
                         + "door in the game under it - it will stay shut");
        }
        NightfallLog($"{matched} of {b.Doors.Count} built doors matched to a door in the game");
    }

    private AreaBuilder areas;

    /// Height of the ground under a point, in world units. Zero for the collider-built maps, where
    /// the floor is one plane; the built world has decks, a planet below them and one pit.
    public float GroundAt(NfVec2 p) => areas?.GroundAt(p.X, p.Y) ?? 0f;

    private static Scene3D BuildFromColliders(MapModel map)
    {
        var s = new Scene3D();
        var atlas = map.Atlas;

        // Furniture that is painted into the room's own drawing and has no sprite to harvest. Done
        // FIRST, because it decides which colliders still need a box built for them.
        var placed = new List<PropPiece>(map.Props);
        if (map.Props.Count > 0) placed.AddRange(BakedProps.Extract(map, map.Props));

        s.BuildWallsAndProps(map, atlas);
        // Guessing is only reached when the map has NOT been harvested. Real objects and guessed
        // ones must never be in the same room: the guess would put a grey box on top of the table
        // whose picture is already standing there.
        if (GuessPropsFromArtwork && map.Props.Count == 0) s.BuildFoundProps(map);
        s.BuildHarvestedProps(placed);

        // The floor is the photograph MINUS everything now standing in front of it, so no object
        // appears twice - once upright and once as its own silhouette smeared across the ground.
        s.BuildFloorAndCeiling(map, FloorRepair.Without(atlas, placed));
        s.BuildIndex(map);
        return s;
    }

    // ================================================================================
    /// One object of the map, standing on the floor as a panel of its own artwork.
    public sealed class StandingProp
    {
        public NfVec2 Ground;
        public float Base, Width, Height;
        public Surface3D Tex;
    }

    public readonly List<StandingProp> Standing = new();

    /// How much of a prop's drawn rectangle is its FOOTPRINT rather than its elevation.
    ///
    /// Among Us draws a prop from slightly above and in front, so the bottom sliver of the drawing
    /// is the piece of floor the object stands on, seen from above, and everything over it is the
    /// object itself. Standing the whole rectangle up puts every table a hand's width too high and
    /// leaves a visible gap under it.
    private const float FootprintFraction = 0.18f;

    private void BuildHarvestedProps(List<PropPiece> props)
    {
        if (props.Count == 0) return;
        int standing = 0, flat = 0;

        foreach (var p in props)
        {
            if (p.WorldWidth < 0.02f || p.WorldHeight < 0.02f) continue;
            var tex = p.GetSurface();

            if (p.Stance == PropStance.Flat)
            {
                // Painted on the ground, a whisker above the floor so the two never fight.
                AddDecal(tex, p.Min.X, p.Min.Y, p.Max.X, p.Max.Y, 0.02f);
                flat++;
                continue;
            }

            float foot = p.WorldHeight * FootprintFraction;
            Standing.Add(new StandingProp
            {
                Ground = new NfVec2(p.Centre.X, p.Min.Y + foot * 0.5f),
                Base = 0f,
                Width = p.WorldWidth,
                Height = p.WorldHeight - foot,
                Tex = tex,
            });
            propFootprints.Add((p.Min.X, p.Min.Y, p.Max.X, p.Max.Y));
            standing++;
        }

        NightfallLog($"{standing} objects standing, {flat} lying flat, from the map's own artwork");
    }

    /// A horizontal quad carrying one sprite, mapped once across its own rectangle.
    private void AddDecal(Surface3D tex, float x0, float z0, float x1, float z1, float y)
    {
        // Image rows run top-down and world z runs upwards, so v is 1 at the low-z edge.
        var a = new Vtx3(new NfVec3(x0, y, z0), 0f, 1f);
        var b = new Vtx3(new NfVec3(x1, y, z0), 1f, 1f);
        var c = new Vtx3(new NfVec3(x1, y, z1), 1f, 0f);
        var d = new Vtx3(new NfVec3(x0, y, z1), 0f, 0f);
        var t1 = new Tri3 { A = a, B = b, C = c, Tex = tex, Tint = NfColor.White, Shade = 1f };
        var t2 = new Tri3 { A = a, B = c, C = d, Tex = tex, Tint = NfColor.White, Shade = 1f };
        t1.Finish(); t2.Finish();
        All.Add(t1); All.Add(t2);
    }

    // ================================================================================
    private void BuildWallsAndProps(MapModel map, MapAtlas atlas)
    {
        var segs = map.Geometry.Segments;

        // Group by the collider each segment came from: a prop is one collider, and it has to be
        // rebuilt as one box rather than as a set of unrelated walls.
        var bySource = new Dictionary<int, List<int>>();
        for (int i = 0; i < segs.Length; i++)
        {
            if (!bySource.TryGetValue(segs[i].SourceId, out var list))
                bySource[segs[i].SourceId] = list = new List<int>();
            list.Add(i);
        }

        foreach (var kv in bySource)
        {
            var idx = kv.Value;
            var h = segs[idx[0]].Height;

            // Waist-high colliders are objects, not architecture, and become boxes - but ONLY on a
            // map that has never been harvested. Once the real artwork is available, a grey box
            // beside a photographed console is worse than nothing there at all: it is the one
            // surface in the room that is visibly not Among Us.
            if (h == WallHeight.Low)
            {
                if (map.Props.Count == 0) BuildPropBox(map, atlas, segs, idx, kv.Key);
                else NoteFootprint(segs, idx);
                continue;
            }

            float top = h == WallHeight.Tall ? TallHeight : WallTop;
            foreach (int i in idx)
            {
                // ONE WALL, ONE QUAD.
                //
                // Among Us traces most walls TWICE: once on layer Ship, where it stops the player,
                // and once on layer Shadow, where it stops the light. Both arrive here as full-
                // height walls in exactly the same place, and two coplanar quads fight for the
                // depth buffer pixel by pixel. That fight was the speckled grey static covering
                // half the walls on Polus - it looked like a broken texture and was in fact two
                // correct ones interleaved.
                if (!wallsBuilt.Add(SegmentKey(segs[i]))) continue;
                BuildWallQuad(map, atlas, segs[i], top, kv.Key);
            }
        }
    }

    /// Walls already built, keyed on their endpoints. See the comment at the call site.
    private readonly HashSet<long> wallsBuilt = new();

    /// Records where a collider stands without drawing anything for it, so the offline tool still
    /// knows not to put a camera inside it.
    private void NoteFootprint(Segment[] segs, List<int> idx)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (int i in idx)
        {
            minX = MathF.Min(minX, MathF.Min(segs[i].A.X, segs[i].B.X));
            maxX = MathF.Max(maxX, MathF.Max(segs[i].A.X, segs[i].B.X));
            minY = MathF.Min(minY, MathF.Min(segs[i].A.Y, segs[i].B.Y));
            maxY = MathF.Max(maxY, MathF.Max(segs[i].A.Y, segs[i].B.Y));
        }
        if (maxX > minX && maxY > minY) propFootprints.Add((minX, minY, maxX, maxY));
    }

    /// A segment's identity: both endpoints quantised to five centimetres, in a fixed order so that
    /// a wall traced backwards by the second collider still matches the first.
    private static long SegmentKey(in Segment s)
    {
        long a = Quant(s.A), b = Quant(s.B);
        if (a > b) (a, b) = (b, a);
        return a * 2654435761L + b;
    }

    private static long Quant(NfVec2 p) =>
        ((long)MathF.Round(p.X * 20f) + 32768L) * 65536L + ((long)MathF.Round(p.Y * 20f) + 32768L);

    private void BuildWallQuad(MapModel map, MapAtlas atlas, in Segment seg, float top, int sourceId)
    {
        var col = TintFor(atlas, seg);
        bool isDoor = (SurfaceKind)seg.Material == SurfaceKind.Door
                      || (SurfaceKind)seg.Material == SurfaceKind.Glass;
        bool isRock = (SurfaceKind)seg.Material == SurfaceKind.Rock
                      || (SurfaceKind)seg.Material == SurfaceKind.Snow
                      || (SurfaceKind)seg.Material == SurfaceKind.LavaRock;

        // THE WALL'S OWN PIXELS, WHEREVER THERE ARE ANY.
        //
        // WallSkin reads the drawn band of this exact wall out of the photograph and turns it into
        // this wall's texture, so the door frames, colour changes and window bands sit where the
        // artwork puts them instead of where a heuristic guessed. Doors and rocks keep their drawn
        // surfaces: a door is a moving object the photograph catches in one arbitrary state, and a
        // rock's band is the ground around it rather than a face.
        Surface3D tex = isDoor || isRock ? null : WallSkin.Build(atlas, seg, top);
        bool skinned = tex != null;
        // The skin is already in the wall's own colours, so it must not be tinted a second time.
        var tint = skinned ? NfColor.White : col;
        if (!skinned)
            tex = AuSurfaces.Get(isDoor ? SurfaceRole.Door : isRock ? SurfaceRole.Rock : SurfaceRole.Wall);

        // A skin spans its segment exactly once, because it IS that segment. A drawn surface has no
        // particular place on the wall and repeats every two units, so a twenty-metre wall does not
        // smear one panel across the horizon.
        float uRep = skinned ? 1f : MathF.Max(0.35f, seg.Length / 2.0f);

        var a = new NfVec3(seg.A.X, 0f, seg.A.Y);
        var b = new NfVec3(seg.B.X, 0f, seg.B.Y);
        var c = new NfVec3(seg.B.X, top, seg.B.Y);
        var d = new NfVec3(seg.A.X, top, seg.A.Y);

        var q1 = new Tri3 { A = new Vtx3(a, 0f, 1f), B = new Vtx3(b, uRep, 1f), C = new Vtx3(c, uRep, 0f), Tex = tex, Tint = tint, Shade = 0.95f };
        var q2 = new Tri3 { A = new Vtx3(a, 0f, 1f), B = new Vtx3(c, uRep, 0f), C = new Vtx3(d, 0f, 0f), Tex = tex, Tint = tint, Shade = 0.95f };
        q1.Finish(); q2.Finish();
        All.Add(q1); All.Add(q2);

        if (isDoor)
        {
            if (!doorTris.TryGetValue(sourceId, out var dl)) doorTris[sourceId] = dl = new List<Tri3>();
            dl.Add(q1); dl.Add(q2);
        }
    }

    /// A waist-high collider rebuilt as a box. Its footprint is the collider's bounding box, which
    /// is what Among Us' own props are anyway: rectangles seen from above.
    private void BuildPropBox(MapModel map, MapAtlas atlas, Segment[] segs, List<int> idx, int sourceId)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (int i in idx)
        {
            minX = MathF.Min(minX, MathF.Min(segs[i].A.X, segs[i].B.X));
            maxX = MathF.Max(maxX, MathF.Max(segs[i].A.X, segs[i].B.X));
            minY = MathF.Min(minY, MathF.Min(segs[i].A.Y, segs[i].B.Y));
            maxY = MathF.Max(maxY, MathF.Max(segs[i].A.Y, segs[i].B.Y));
        }
        if (maxX - minX < 0.05f || maxY - minY < 0.05f) return;
        propFootprints.Add((minX, minY, maxX, maxY));

        // A prop's colour is the colour of the prop, read from its own footprint in the map art.
        // Using the room's colour made every console, crate and tank the same shade as the floor.
        var col = TintFor(atlas, segs[idx[0]]);
        if (atlas != null && atlas.IsValid
            && atlas.SampleMedian(minX, minY, maxX, maxY, out float pr, out float pg, out float pb))
            col = new NfColor(pr, pg, pb);

        var side = AuSurfaces.Get(SurfaceRole.PropSide);
        var front = AuSurfaces.Get(SurfaceRole.ConsoleFront);

        // The face pointing at the middle of the room gets the console front, so a screen never
        // ends up buried in a wall.
        var centre = new NfVec2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        var toRoom = (map.SpawnCenter - centre).Normalized;
        int frontFace = MathF.Abs(toRoom.X) > MathF.Abs(toRoom.Y)
            ? (toRoom.X > 0 ? 1 : 3)
            : (toRoom.Y > 0 ? 2 : 0);

        float h = PropHeight;
        void Face(int id, NfVec3 p0, NfVec3 p1, NfVec3 p2, NfVec3 p3, float shade)
        {
            var tex = id == frontFace ? front : side;
            float emis = id == frontFace ? 0.30f : 0f;
            var q1 = new Tri3 { A = new Vtx3(p0, 0f, 1f), B = new Vtx3(p1, 1f, 1f), C = new Vtx3(p2, 1f, 0f), Tex = tex, Tint = col, Shade = shade, Emissive = emis };
            var q2 = new Tri3 { A = new Vtx3(p0, 0f, 1f), B = new Vtx3(p2, 1f, 0f), C = new Vtx3(p3, 0f, 0f), Tex = tex, Tint = col, Shade = shade, Emissive = emis };
            q1.Finish(); q2.Finish();
            All.Add(q1); All.Add(q2);
        }

        // south, east, north, west, top
        Face(0, new NfVec3(minX, 0, minY), new NfVec3(maxX, 0, minY), new NfVec3(maxX, h, minY), new NfVec3(minX, h, minY), 0.92f);
        Face(1, new NfVec3(maxX, 0, minY), new NfVec3(maxX, 0, maxY), new NfVec3(maxX, h, maxY), new NfVec3(maxX, h, minY), 0.86f);
        Face(2, new NfVec3(maxX, 0, maxY), new NfVec3(minX, 0, maxY), new NfVec3(minX, h, maxY), new NfVec3(maxX, h, maxY), 0.80f);
        Face(3, new NfVec3(minX, 0, maxY), new NfVec3(minX, 0, minY), new NfVec3(minX, h, minY), new NfVec3(minX, h, maxY), 0.88f);

        // A real lid, not the side texture laid flat: from eye height the top of a waist-high
        // object is very visible, and a side texture up there reads as a mistake.
        var topTex = AuSurfaces.Get(SurfaceRole.PropTop);
        var tt1 = new Tri3 { A = new Vtx3(new NfVec3(minX, h, minY), 0, 0), B = new Vtx3(new NfVec3(maxX, h, minY), 1, 0), C = new Vtx3(new NfVec3(maxX, h, maxY), 1, 1), Tex = topTex, Tint = col, Shade = 1.08f };
        var tt2 = new Tri3 { A = new Vtx3(new NfVec3(minX, h, minY), 0, 0), B = new Vtx3(new NfVec3(maxX, h, maxY), 1, 1), C = new Vtx3(new NfVec3(minX, h, maxY), 0, 1), Tex = topTex, Tint = col, Shade = 1.08f };
        tt1.Finish(); tt2.Finish();
        All.Add(tt1); All.Add(tt2);
    }

    /// Furniture found by looking at the map rather than at the colliders. Most Among Us props
    /// have no collider at all, which is why rooms full of consoles and crates were coming out
    /// empty.
    /// The largest a guessed prop is allowed to be, per side, in world units.
    ///
    /// PropFinder works by looking for patches of the map artwork that differ from the floor around
    /// them, and in a room whose floor is patterned - the laboratory's blue tiling, the office
    /// carpet - that test says "different" almost everywhere and the patches merge into one blob
    /// the size of the room. What came out was a single box filling the laboratory from wall to
    /// wall, wearing a console screen: the room read as a solid grey block with a monitor on it.
    ///
    /// The guess is kept for the small objects it does get right, and anything the size of a piece
    /// of furniture or larger is thrown away. The real fix is not a better threshold, it is not
    /// guessing: SpriteHarvest cuts the actual objects out of the scene, and once a map has been
    /// harvested this whole path is skipped.
    private const float MaxGuessedPropSide = 2.2f;

    private void BuildFoundProps(MapModel map)
    {
        var found = PropFinder.Find(map);
        int rejected = 0;
        foreach (var p in found)
        {
            if (p.Max.X - p.Min.X > MaxGuessedPropSide || p.Max.Y - p.Min.Y > MaxGuessedPropSide)
            {
                rejected++;
                continue;
            }

            // NO TWO BOXES IN THE SAME PLACE.
            //
            // A console that HAS a collider is built from the collider, and then found a second
            // time in the artwork and built again a few centimetres away. Two boxes that nearly
            // coincide do not look like one box: they tear at each other pixel by pixel wherever
            // their faces are within a hair of the same depth, and the result is the speckled
            // static that covered half the props on Polus.
            if (OverlapsExistingProp(p.Min.X, p.Min.Y, p.Max.X, p.Max.Y))
            {
                rejected++;
                continue;
            }
            propFootprints.Add((p.Min.X, p.Min.Y, p.Max.X, p.Max.Y));
            var side = AuSurfaces.Get(SurfaceRole.PropSide);
            var front = AuSurfaces.Get(p.Lit ? SurfaceRole.ConsoleFront : SurfaceRole.PropSide);

            var centre = new NfVec2((p.Min.X + p.Max.X) * 0.5f, (p.Min.Y + p.Max.Y) * 0.5f);
            var toRoom = (map.SpawnCenter - centre).Normalized;
            int frontFace = MathF.Abs(toRoom.X) > MathF.Abs(toRoom.Y)
                ? (toRoom.X > 0 ? 1 : 3)
                : (toRoom.Y > 0 ? 2 : 0);

            AddBox(p.Min.X, p.Min.Y, p.Max.X, p.Max.Y, p.Height, p.Color,
                   side, front, frontFace, p.Lit ? 0.32f : 0f);
        }
        NightfallLog($"{found.Count - rejected} props guessed from the map artwork "
                     + $"({rejected} rejected as too large to be an object)");
    }

    /// Logging hook the plugin fills in; the core has no logger of its own.
    public static Action<string> NightfallLog = _ => { };

    /// Ground footprints of every box already standing, so a second one is never put on top of it.
    private readonly List<(float x0, float y0, float x1, float y1)> propFootprints = new();

    /// True when nothing solid stands at this spot. The offline tool uses it to place its cameras:
    /// a shot taken from inside a desk is a shot of the inside of a desk.
    public bool IsClearOfProps(NfVec2 p, float radius)
    {
        foreach (var f in propFootprints)
            if (p.X > f.x0 - radius && p.X < f.x1 + radius
                && p.Y > f.y0 - radius && p.Y < f.y1 + radius) return false;
        return true;
    }

    /// True when the rectangle shares a fifth of its area with a box that already exists. A fifth
    /// rather than any overlap at all: props legitimately stand shoulder to shoulder along a wall,
    /// and rejecting a neighbour for touching would empty the rooms again.
    private bool OverlapsExistingProp(float x0, float y0, float x1, float y1)
    {
        float area = MathF.Max(1e-4f, (x1 - x0) * (y1 - y0));
        foreach (var f in propFootprints)
        {
            float ox = MathF.Min(x1, f.x1) - MathF.Max(x0, f.x0);
            float oy = MathF.Min(y1, f.y1) - MathF.Max(y0, f.y0);
            if (ox <= 0f || oy <= 0f) continue;
            if (ox * oy / area > 0.2f) return true;
        }
        return false;
    }

    /// A box standing on the floor, five faces.
    private void AddBox(float minX, float minY, float maxX, float maxY, float h, NfColor col,
                        Surface3D side, Surface3D front, int frontFace, float emissive)
    {
        void Face(int id, NfVec3 p0, NfVec3 p1, NfVec3 p2, NfVec3 p3, float shade)
        {
            var tex = id == frontFace ? front : side;
            float emis = id == frontFace ? emissive : 0f;
            var q1 = new Tri3 { A = new Vtx3(p0, 0f, 1f), B = new Vtx3(p1, 1f, 1f), C = new Vtx3(p2, 1f, 0f), Tex = tex, Tint = col, Shade = shade, Emissive = emis };
            var q2 = new Tri3 { A = new Vtx3(p0, 0f, 1f), B = new Vtx3(p2, 1f, 0f), C = new Vtx3(p3, 0f, 0f), Tex = tex, Tint = col, Shade = shade, Emissive = emis };
            q1.Finish(); q2.Finish();
            All.Add(q1); All.Add(q2);
        }

        Face(0, new NfVec3(minX, 0, minY), new NfVec3(maxX, 0, minY), new NfVec3(maxX, h, minY), new NfVec3(minX, h, minY), 0.92f);
        Face(1, new NfVec3(maxX, 0, minY), new NfVec3(maxX, 0, maxY), new NfVec3(maxX, h, maxY), new NfVec3(maxX, h, minY), 0.86f);
        Face(2, new NfVec3(maxX, 0, maxY), new NfVec3(minX, 0, maxY), new NfVec3(minX, h, maxY), new NfVec3(maxX, h, maxY), 0.80f);
        Face(3, new NfVec3(minX, 0, maxY), new NfVec3(minX, 0, minY), new NfVec3(minX, h, minY), new NfVec3(minX, h, maxY), 0.88f);

        var top = AuSurfaces.Get(SurfaceRole.PropTop);
        var t1 = new Tri3 { A = new Vtx3(new NfVec3(minX, h, minY), 0, 0), B = new Vtx3(new NfVec3(maxX, h, minY), 1, 0), C = new Vtx3(new NfVec3(maxX, h, maxY), 1, 1), Tex = top, Tint = col, Shade = 1.10f };
        var t2 = new Tri3 { A = new Vtx3(new NfVec3(minX, h, minY), 0, 0), B = new Vtx3(new NfVec3(maxX, h, maxY), 1, 1), C = new Vtx3(new NfVec3(minX, h, maxY), 0, 1), Tex = top, Tint = col, Shade = 1.10f };
        t1.Finish(); t2.Finish();
        All.Add(t1); All.Add(t2);
    }

    private void BuildFloorAndCeiling(MapModel map, MapAtlas atlas)
    {
        var lo = map.Geometry.Min;
        var hi = map.Geometry.Max;
        bool photo = atlas != null && atlas.IsValid;

        // ---- the floor: the photograph itself, laid flat ----
        var floorTex = AuSurfaces.Get(SurfaceRole.Floor);
        int fx = Math.Max(1, (int)((hi.X - lo.X) / FloorPatch));
        int fz = Math.Max(1, (int)((hi.Y - lo.Y) / FloorPatch));
        for (int iz = 0; iz < fz; iz++)
        {
            float z0 = lo.Y + (hi.Y - lo.Y) * iz / fz;
            float z1 = lo.Y + (hi.Y - lo.Y) * (iz + 1) / fz;
            for (int ix = 0; ix < fx; ix++)
            {
                float x0 = lo.X + (hi.X - lo.X) * ix / fx;
                float x1 = lo.X + (hi.X - lo.X) * (ix + 1) / fx;

                // The fallback colour matters only where the photograph is missing, which is
                // outside its rectangle: Polus' ground violet is the honest guess there.
                var col = new NfColor(0.34f, 0.24f, 0.42f);
                if (photo) atlas.SampleMedian(x0, z0, x1, z1, out col.R, out col.G, out col.B);

                AddHorizontal(floorTex, col, x0, z0, x1, z1, 0f, 1.0f,
                              photo ? atlas : null);
            }
        }

        // ---- the ceiling: invented, because no photograph of a map contains one ----
        var ceilTex = AuSurfaces.Get(SurfaceRole.Ceiling);
        int cx = Math.Max(1, (int)((hi.X - lo.X) / CeilingPatch));
        int cz = Math.Max(1, (int)((hi.Y - lo.Y) / CeilingPatch));
        for (int iz = 0; iz < cz; iz++)
        {
            float z0 = lo.Y + (hi.Y - lo.Y) * iz / cz;
            float z1 = lo.Y + (hi.Y - lo.Y) * (iz + 1) / cz;
            for (int ix = 0; ix < cx; ix++)
            {
                float x0 = lo.X + (hi.X - lo.X) * ix / cx;
                float x1 = lo.X + (hi.X - lo.X) * (ix + 1) / cx;
                var mid = new NfVec2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);

                // Roofed when the middle is indoors and at most one corner is not.
                //
                // Requiring the CENTRE alone put a roof over Decontamination 2, which is an open
                // walkway across a lava fissure. Requiring ALL FOUR corners went as far wrong the
                // other way: every patch touching a wall failed, so each room lost a metre of
                // ceiling all the way round its edge and the night sky showed through the gap
                // between the wall and the roof. Three of four keeps the walkway open and closes
                // the rooms.
                if (!map.IsInside(mid)) continue;
                int corners = 0;
                if (map.IsInside(new NfVec2(x0 + 0.1f, z0 + 0.1f))) corners++;
                if (map.IsInside(new NfVec2(x1 - 0.1f, z0 + 0.1f))) corners++;
                if (map.IsInside(new NfVec2(x0 + 0.1f, z1 - 0.1f))) corners++;
                if (map.IsInside(new NfVec2(x1 - 0.1f, z1 - 0.1f))) corners++;
                if (corners < 3) continue;

                // A CEILING IS NOT A FLOOR SEEN FROM BELOW.
                //
                // Taking the ceiling's colour from the photograph underneath it put a magenta roof
                // over Specimens and a blue-tiled one over the laboratory, because that is what is
                // painted on their floors. Among Us' interiors are roofed, when they are drawn at
                // all, in the station's own dark panelling, so the floor colour is only a hint of
                // the room and is pulled most of the way to that.
                var col = StationWall;
                if (photo && atlas.SampleMedian(x0, z0, x1, z1, out float mr, out float mg, out float mb))
                    col = NfColor.Lerp(new NfColor(mr, mg, mb), StationWall, 0.78f);
                AddHorizontal(ceilTex, col, x0, z0, x1, z1, WallTop, 0.68f, null);
            }
        }
    }

    private void AddHorizontal(Surface3D tex, NfColor tint, float x0, float z0, float x1, float z1,
                               float y, float shade, MapAtlas atlas)
    {
        // World-space UVs, so the tiling runs continuously across patch seams.
        var a = new Vtx3(new NfVec3(x0, y, z0), x0, z0);
        var b = new Vtx3(new NfVec3(x1, y, z0), x1, z0);
        var c = new Vtx3(new NfVec3(x1, y, z1), x1, z1);
        var d = new Vtx3(new NfVec3(x0, y, z1), x0, z1);
        var t1 = new Tri3 { A = a, B = b, C = c, Tex = tex, Tint = tint, Shade = shade, Atlas = atlas };
        var t2 = new Tri3 { A = a, B = c, C = d, Tex = tex, Tint = tint, Shade = shade, Atlas = atlas };
        t1.Finish(); t2.Finish();
        All.Add(t1); All.Add(t2);
    }

    /// The colour of a wall.
    ///
    /// A review of every room found walls wearing the colour of whatever lay next to them: the
    /// Comms wall came out in the violet of the Polus ground outside, the Boiler Room wall the
    /// same. Sampling "near the wall" picks up the neighbour whenever the wall stroke is thin.
    ///
    /// So the sample is taken ON the wall line itself, and the result is only accepted if it looks
    /// like a wall: Among Us draws interior walls dark and desaturated, never in the bright violet
    /// of the planet surface. Anything that fails that test falls back to the station's own wall
    /// grey, which is always closer than a wrong neighbour colour.
    private static NfColor TintFor(MapAtlas atlas, in Segment seg)
    {
        if (seg.HasTint)
        {
            var t = NfColor.FromBytes(seg.TintR, seg.TintG, seg.TintB);
            if (LooksLikeWall(t)) return t;
        }
        if (atlas != null && atlas.IsValid)
        {
            var mid = new NfVec2((seg.A.X + seg.B.X) * 0.5f, (seg.A.Y + seg.B.Y) * 0.5f);
            if (atlas.SampleArea(mid.X, mid.Y, 0.16f, out float r, out float g, out float b))
            {
                var c = new NfColor(r, g, b);
                if (LooksLikeWall(c)) return c;
            }
        }
        return StationWall;
    }

    /// Polus' own interior wall colour: dark, desaturated, slightly green. Used whenever the
    /// sampled colour is implausible for a wall.
    private static readonly NfColor StationWall = new(0.255f, 0.325f, 0.290f);

    /// Rejects the violet of the planet surface and anything too bright to be a wall in a station
    /// the game keeps deliberately dim.
    private static bool LooksLikeWall(NfColor c)
    {
        float lum = c.R * 0.3f + c.G * 0.6f + c.B * 0.1f;
        if (lum > 0.72f) return false;                    // too bright for an interior wall
        float violet = (c.R + c.B) * 0.5f - c.G;          // the Polus ground signature
        return violet <= 0.06f;
    }

    // ================================================================================
    // Spatial index
    // ================================================================================
    private void BuildIndex(MapModel map)
    {
        /*
         * THE INDEX COVERS THE TRIANGLES, NOT THE COLLIDERS. It used to be the collider hull plus
         * four, which was ten units narrower than the planet on every side: the whole outer ring of
         * ground was clamped into the border cells, whose centres then lied to the per-cell frustum
         * and distance tests. Measuring the model itself cannot be wrong that way, whichever path
         * built it.
         */
        min = new NfVec2(map.Geometry.Min.X - 4f, map.Geometry.Min.Y - 4f);
        max = new NfVec2(map.Geometry.Max.X + 4f, map.Geometry.Max.Y + 4f);
        foreach (var t in All)
        {
            if (t.Centre.X - t.Radius < min.X) min.X = t.Centre.X - t.Radius;
            if (t.Centre.Z - t.Radius < min.Y) min.Y = t.Centre.Z - t.Radius;
            if (t.Centre.X + t.Radius > max.X) max.X = t.Centre.X + t.Radius;
            if (t.Centre.Z + t.Radius > max.Y) max.Y = t.Centre.Z + t.Radius;
        }
        gw = Math.Max(1, (int)((max.X - min.X) / cell) + 1);
        gh = Math.Max(1, (int)((max.Y - min.Y) / cell) + 1);
        buckets = new List<Tri3>[gw * gh];

        foreach (var t in All)
        {
            int cx = NfMath.ClampInt((int)((t.Centre.X - min.X) / cell), 0, gw - 1);
            int cz = NfMath.ClampInt((int)((t.Centre.Z - min.Y) / cell), 0, gh - 1);
            int i = cz * gw + cx;
            (buckets[i] ??= new List<Tri3>()).Add(t);
        }
    }

    private readonly List<Tri3> queryResult = new();

    /// Triangles within `range` of a point AND inside the view. Rebuilt per frame into one reused
    /// list, which is cheap and keeps the renderer allocation free.
    ///
    /// THE FRUSTUM TEST IS PER CELL, AND IT IS WHAT PAYS FOR THE BUILT WORLD.
    ///
    /// A square of cells around the player is four times the area of a 75-degree wedge, so three
    /// quarters of everything handed to the renderer used to be behind the camera or off to one
    /// side. That was free enough when a map was three thousand triangles; the built world is ten
    /// times that, and every one of them was being transformed to find its screen extent before
    /// being thrown away.
    ///
    /// The test is on the CELL, not the triangle: one dot product per cell instead of per triangle,
    /// and it is conservative - the cell's circumscribed radius is added to the half-plane distance,
    /// so a triangle whose centre sits just outside the wedge but which reaches into it is kept. It
    /// is only correct with a margin, and getting the margin wrong shows up as geometry popping in
    /// at the edge of the screen when the head turns.
    public List<Tri3> Query(NfVec2 eye, float range, float heading, float fov)
    {
        queryResult.Clear();
        int span = Math.Max(1, (int)(range / cell) + 1);
        int cx = (int)((eye.X - min.X) / cell);
        int cz = (int)((eye.Y - min.Y) / cell);

        // Half-plane normals of the wedge, pointing INWARDS. A point is in view when it is on the
        // positive side of both. Widened a little past the true field of view because a triangle is
        // clipped, not culled, and one crossing the near plane can land anywhere on screen.
        // Two half-planes only describe a wedge narrower than 180 degrees. Wider than that the test
        // would be nonsense rather than merely useless, so it is switched off instead.
        float half = fov * 0.5f + 0.35f;
        bool cull = half < 1.45f;
        float lx = MathF.Cos(heading + half - NfMath.Pi * 0.5f);
        float ly = MathF.Sin(heading + half - NfMath.Pi * 0.5f);
        float rx = MathF.Cos(heading - half + NfMath.Pi * 0.5f);
        float ry = MathF.Sin(heading - half + NfMath.Pi * 0.5f);
        // Half the diagonal of a cell: how far outside the wedge a cell's centre may sit and still
        // hold something inside it.
        float margin = cell * 0.7072f;

        // ---- which cells, and in what order ----
        cellOrder.Clear();
        for (int z = cz - span; z <= cz + span; z++)
        {
            if (z < 0 || z >= gh) continue;
            float wz = min.Y + (z + 0.5f) * cell - eye.Y;
            for (int x = cx - span; x <= cx + span; x++)
            {
                if (x < 0 || x >= gw) continue;
                if (buckets[z * gw + x] == null) continue;

                float wx = min.X + (x + 0.5f) * cell - eye.X;
                float d2 = wx * wx + wz * wz;
                // The cell the eye is in, and its neighbours, are never culled: the player is
                // standing on that floor patch and inside that room.
                if (cull && d2 > cell * cell * 2f)
                {
                    if (wx * lx + wz * ly < -margin) continue;
                    if (wx * rx + wz * ry < -margin) continue;
                }
                cellOrder.Add((d2, z * gw + x));
            }
        }

        /*
         * NEAREST CELL FIRST, AND IT IS WORTH THE SORT.
         *
         * Standing outside a building, everything inside it is drawn and then covered over by the
         * roof - which the depth buffer handles correctly and expensively: a pixel that loses the
         * depth test has already had its texture sampled, its torch cone evaluated and its fog
         * mixed, and only then is it thrown away. Walking the cells from near to far means the
         * roof is drawn FIRST and every one of those pixels fails the depth test before any of that
         * work happens.
         *
         * The sort is a few hundred entries against tens of thousands of triangles, so it is free
         * in comparison; and the renderer's threads split the screen by ROWS, so each of them walks
         * this list in the order it is given, front to back.
         */
        cellOrder.Sort((p, q) => p.d2.CompareTo(q.d2));

        foreach (var (_, ci) in cellOrder)
        {
            foreach (var t in buckets[ci])
            {
                if (hiddenAny && t.Emissive < 0f) continue;

                // AND ONCE MORE PER TRIANGLE. A cell is four units across and the wedge is a
                // wedge, so a kept cell still holds a good deal that is out of view or out of
                // range. Fourteen floating point operations here save a full view transform and
                // sixteen band comparisons in the renderer, which is what this list is for.
                float tx = t.Centre.X - eye.X, tz = t.Centre.Z - eye.Y;
                float rad = t.Radius;
                float reach = range + rad;
                if (tx * tx + tz * tz > reach * reach) continue;
                if (cull)
                {
                    if (tx * lx + tz * ly < -rad) continue;
                    if (tx * rx + tz * ry < -rad) continue;
                }
                queryResult.Add(t);
            }
        }
        return queryResult;
    }

    private readonly List<(float d2, int cell)> cellOrder = new();

    /// Opening a door removes its quads from the world. Closing puts them back. This is the whole
    /// fix for "you can see through closed doors": a closed door is real geometry, an open one is
    /// simply not there.
    public void SetDoorOpen(int sourceId, bool open)
    {
        if (!doorTris.TryGetValue(sourceId, out var list)) return;
        if (open == openDoors.Contains(sourceId)) return;

        if (open)
        {
            openDoors.Add(sourceId);
            foreach (var t in list) t.Emissive = -1f;      // sentinel: skipped by Query
        }
        else
        {
            openDoors.Remove(sourceId);
            foreach (var t in list) t.Emissive = 0f;
        }
        hiddenAny = openDoors.Count > 0 || platformSlots.Count > 0;
    }

    /// True while any triangle carries the -1 sentinel: open doors, or platform slots (all but one
    /// of which are hidden at any time). Query checks the flag first so the per-triangle test costs
    /// nothing on the maps that have neither.
    private bool hiddenAny;

    // ================================================================================
    // The moving platform (Airship, Gap Room)
    // ================================================================================
    /*
     * A THING THAT MOVES, IN A WORLD THAT CANNOT.
     *
     * The renderer walks a spatial index built once per round; nothing in it can change position.
     * Doors get away with it because they only ever exist or not - a quad is either there or it
     * carries the sentinel. The Gap Room's platform is the one object in the game that carries
     * the player somewhere, and without it the first-person view had a hole where the ride is: the
     * player crossed the pit at deck height while the ground under them, by the pit rule, was the
     * pit floor, so the eye dropped 1.8 units mid-ride and looked at the machinery from the inside.
     * That is the "glitching under the map" report.
     *
     * The same trick as the doors, applied along a line: the platform's disc is built once at
     * every one of SlotCount positions between its two ends, all hidden, and each frame the slot
     * nearest the real platform is the one shown. Twenty-four slots over the ~10-unit ride is a
     * step of under half a unit, which at the raycaster's resolution reads as motion.
     */
    private readonly List<(int from, int to, NfVec2 at)> platformSlots = new();
    private int platformSlot = -1;
    private const int SlotCount = 24;

    private void BuildPlatformSlots(AreaBuilder b, PlatformSpec spec)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            float t = SlotCount == 1 ? 0f : i / (float)(SlotCount - 1);
            var at = new NfVec2(spec.Left.X + (spec.Right.X - spec.Left.X) * t,
                                spec.Left.Y + (spec.Right.Y - spec.Left.Y) * t);
            int from = All.Count;
            // A drum fixture, not a disc floor: a floor would register a deck, and the ground
            // under the ride is decided by NightfallRides from the platform's real position, not
            // by twenty-four phantom decks over the pit. Base 0.14 below the deck so the top sits
            // flush with the floor on either side; the trim ring is the platform's own rim.
            b.BuildArea(new Area
            {
                Id = $"platform_slot_{i}",
                Deck = 0f,
                Fixtures = new[]
                {
                    new Fx { Kind = "drum", At = (at.X, at.Y), H = 0.14f, R = spec.Radius,
                             Deck = -0.14f, Mat = "#5a646c" },
                },
            });
            platformSlots.Add((from, All.Count, at));
            for (int k = from; k < All.Count; k++) All[k].Emissive = -1f;
        }
        hiddenAny = openDoors.Count > 0 || platformSlots.Count > 0;
    }

    /// Shows the slot nearest to where the platform actually is, hides the previous one.
    public void SetPlatformPosition(NfVec2 pos)
    {
        if (platformSlots.Count == 0) return;
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < platformSlots.Count; i++)
        {
            float dx = platformSlots[i].at.X - pos.X, dy = platformSlots[i].at.Y - pos.Y;
            float d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best == platformSlot) return;
        if (platformSlot >= 0)
            for (int k = platformSlots[platformSlot].from; k < platformSlots[platformSlot].to; k++) All[k].Emissive = -1f;
        for (int k = platformSlots[best].from; k < platformSlots[best].to; k++) All[k].Emissive = 0f;
        platformSlot = best;
    }

    /// Is there any deck (floor) under this point at all, or only the planet fallback? The
    /// hole guard in NightfallView asks this before believing a sudden drop.
    public bool DeckUnder(NfVec2 p) => areas?.DeckUnder(p.X, p.Y) ?? true;
}
