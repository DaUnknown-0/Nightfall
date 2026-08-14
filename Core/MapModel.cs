// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * MapModel - everything the first-person view needs to know about a map, in a form that has no
 * Unity in it.
 *
 * It is built from ColliderRecords, a deliberately dumb transport type that both hosts can fill:
 * the plugin walks the live scene, the offline render tool reads a survey JSON. Same builder, same
 * result, which is the entire point - what is checked in a PNG outside the game is what the game
 * will draw.
 *
 * WHAT AMONG US HANDS US FOR FREE
 * -------------------------------
 * Reading the survey of Polus made one thing obvious: the map already carries its own material
 * data, it just never had a reason to show it.
 *
 *   PolusShip(Clone)/<Room>/Walls          the room's real walls
 *   PolusShip(Clone)/<Room>/Sounds/Metal   a TRIGGER marking where footsteps sound like metal
 *                        /Sounds/Snow      ... snow, Tile, Carpet, Wood, Plastic
 *   PolusShip(Clone)/<Room>                a TRIGGER covering the whole room
 *
 * The footstep zones are a complete floor-material map of the station, authored by the developers
 * and shipped in every copy of the game. Nightfall reads them straight into the floor grid, so the
 * lab floor is tile because Among Us says it is tile, not because someone guessed. The room
 * triggers do the same job for "am I indoors": inside a room you get a ceiling, outside you get the
 * Polus night sky.
 *
 * THE FLOOR GRID
 * --------------
 * Point-in-polygon per floor pixel would be far too slow (the renderer touches tens of thousands of
 * floor pixels per frame). So the polygons are rasterised ONCE into a coarse grid of bytes, and the
 * renderer does an array lookup. At quarter-unit resolution the whole of Polus costs about 25 KB.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

/// Host-neutral description of one collider. The plugin fills this from a Collider2D, the offline
/// tool from the survey file.
public sealed class ColliderRecord
{
    public string Path = "";
    public string LayerName = "";
    public int Layer;
    public bool Trigger;
    public string Type = "";
    public bool Closed = true;
    /// Each entry is a flat [x0,y0,x1,y1,...] list in WORLD coordinates.
    public List<float[]> Paths = new();

    public string LeafName
    {
        get
        {
            int i = Path.LastIndexOf('/');
            return i >= 0 ? Path.Substring(i + 1) : Path;
        }
    }

    /// The room segment of the path: "PolusShip(Clone)/Laboratory/Walls" gives "Laboratory".
    /// Empty when the collider hangs directly off the ship root.
    public string RoomName
    {
        get
        {
            var parts = Path.Split('/');
            return parts.Length >= 3 ? parts[1] : "";
        }
    }
}

public sealed class RoomInfo
{
    public string Name = "";
    public string SystemType = "";
    public NfVec2 Min, Max;
    public NfVec2 Center => new((Min.X + Max.X) * 0.5f, (Min.Y + Max.Y) * 0.5f);
}

public sealed class MapModel
{
    public string MapKey = "";
    public GeometryModel Geometry = new();
    public List<RoomInfo> Rooms = new();

    // ---- floor grid ----
    public const float FloorCell = 0.25f;
    public NfVec2 FloorOrigin;
    public int FloorW, FloorH;
    /// Surface kind of the floor at each cell.
    public byte[] FloorMaterial = Array.Empty<byte>();
    /// 1 = inside a room (has a ceiling), 0 = outdoors (has the sky).
    public byte[] FloorInside = Array.Empty<byte>();

    public NfVec2 SpawnCenter;
    public NfVec2 MeetingSpawn;

    /// A photograph of the map's own artwork, taken at round start. When it is valid the renderer
    /// draws the real floor instead of a procedural one, which is the difference between "a Sci-Fi
    /// corridor" and "Polus".
    public MapAtlas Atlas = new();

    /// The map's furniture, cut out of the scene one object at a time. Empty when the map has not
    /// been harvested, in which case the rooms come out unfurnished rather than furnished wrongly.
    public List<PropPiece> Props = new();

    /// Where each door of the map stands, by the same source id SetDoorOpen takes.
    ///
    /// Kept separately from the segments because a Polus door is a TRIGGER on the ShortObjects
    /// layer, and triggers never become geometry - so the only trace of the Comms door in the
    /// segment list is the shadow strip beside it, nearly a unit away from the door itself. That
    /// was enough to make three of the sixteen built doors fail to find their counterpart.
    public List<(int SourceId, NfVec2 Centre)> DoorAnchors = new();

    public bool IsInside(NfVec2 p)
    {
        int i = FloorIndex(p);
        return i >= 0 && FloorInside[i] != 0;
    }

    public SurfaceKind FloorAt(NfVec2 p)
    {
        int i = FloorIndex(p);
        return i >= 0 ? (SurfaceKind)FloorMaterial[i] : SurfaceKind.Snow;
    }

    public int FloorIndex(NfVec2 p)
    {
        int x = (int)((p.X - FloorOrigin.X) / FloorCell);
        int y = (int)((p.Y - FloorOrigin.Y) / FloorCell);
        if (x < 0 || y < 0 || x >= FloorW || y >= FloorH) return -1;
        return y * FloorW + x;
    }
}

public static class MapModelBuilder
{
    // ================================================================================
    // Layer rules
    // ================================================================================
    // Verified against the live game via the survey: ShipOnlyMask = 512 (layer 9),
    // ShadowMask = 11264 (layers 10, 11, 13), ShipAndAllObjectsMask = 6656 (layers 9, 11, 12).
    private const string LayerShip = "Ship";              // 9  - real walls, blocks movement
    private const string LayerShadow = "Shadow";          // 10 - what the game's own light stops at
    private const string LayerObjects = "Objects";        // 11 - tall obstacles
    private const string LayerShortObjects = "ShortObjects"; // 12 - waist high: desks, crates, consoles

    private static WallHeight HeightFor(string layerName) => layerName switch
    {
        LayerShip => WallHeight.Full,
        LayerShadow => WallHeight.Full,
        LayerObjects => WallHeight.Tall,
        LayerShortObjects => WallHeight.Low,
        _ => WallHeight.Full,
    };

    private static bool IsGeometryLayer(string layerName) =>
        layerName is LayerShip or LayerShadow or LayerObjects or LayerShortObjects;

    // ================================================================================
    // Material rules - data, not code branches
    // ================================================================================
    /// Matched in order against the collider's full path, case-insensitively. First hit wins, so
    /// the specific entries come before the general ones. Adding a map later is a matter of adding
    /// rows here, which is the same map-agnostic discipline the rest of this mod family follows.
    private static readonly (string needle, SurfaceKind kind)[] WallRules =
    {
        // --- doors and openings, anywhere ---
        ("decondoor",     SurfaceKind.Glass),
        ("door",          SurfaceKind.Door),
        ("vent",          SurfaceKind.Vent),

        // --- Polus outdoors ---
        ("outside",       SurfaceKind.Rock),
        ("rocksnboxes",   SurfaceKind.Rock),
        ("bigrock",       SurfaceKind.Rock),
        ("boxclust",      SurfaceKind.Crate),
        ("boxcluster",    SurfaceKind.Crate),
        ("cliff",         SurfaceKind.Rock),
        ("snow",          SurfaceKind.Snow),
        ("bridge",        SurfaceKind.MetalRibbed),
        ("tube",          SurfaceKind.MetalRibbed),
        ("pod",           SurfaceKind.MetalRibbed),
        ("lava",          SurfaceKind.LavaRock),
        ("hole",          SurfaceKind.LavaRock),

        // --- rooms with an obvious material ---
        ("laboratory",    SurfaceKind.LabTile),
        ("science",       SurfaceKind.LabTile),
        ("medbay",        SurfaceKind.LabTile),
        ("specimen",      SurfaceKind.LabTile),
        ("decontamination", SurfaceKind.Glass),
        ("decon",         SurfaceKind.Glass),
        ("office",        SurfaceKind.Wood),
        ("admin",         SurfaceKind.Concrete),
        ("comms",         SurfaceKind.Concrete),
        ("communications", SurfaceKind.Concrete),
        ("weapons",       SurfaceKind.MetalRibbed),
        ("security",      SurfaceKind.MetalPanel),
        ("electrical",    SurfaceKind.MetalPanel),
        ("storage",       SurfaceKind.MetalPanel),
        ("dropship",      SurfaceKind.MetalRibbed),
        ("boiler",        SurfaceKind.LavaRock),
        ("lifesupp",      SurfaceKind.MetalPanel),
        ("o2",            SurfaceKind.MetalPanel),
    };

    /// The footstep triggers, which is how the game itself names its floors.
    private static readonly (string needle, SurfaceKind kind)[] FloorSoundRules =
    {
        ("sounds/metal",   SurfaceKind.MetalPanel),
        ("sounds/plastic", SurfaceKind.Concrete),
        ("sounds/tile",    SurfaceKind.LabTile),
        ("sounds/carpet",  SurfaceKind.Wood),
        ("sounds/wood",    SurfaceKind.Wood),
        ("sounds/snow",    SurfaceKind.Snow),
        ("sounds/grass",   SurfaceKind.Snow),
    };

    private static SurfaceKind WallMaterialFor(ColliderRecord rec)
    {
        string p = rec.Path.ToLowerInvariant();
        foreach (var (needle, kind) in WallRules)
            if (p.Contains(needle)) return kind;

        // Waist-high things that matched nothing are furniture, and furniture reads far better as a
        // console or a crate than as a slab of wall.
        if (rec.LayerName == LayerShortObjects) return SurfaceKind.Console;
        return SurfaceKind.MetalPanel;
    }

    private static SurfaceKind? FloorMaterialFor(ColliderRecord rec)
    {
        string p = rec.Path.ToLowerInvariant();
        foreach (var (needle, kind) in FloorSoundRules)
            if (p.Contains(needle)) return kind;
        return null;
    }

    // ================================================================================
    // Build
    // ================================================================================
    public static MapModel Build(string mapKey, IEnumerable<ColliderRecord> records,
                                 IEnumerable<RoomInfo> rooms = null,
                                 NfVec2 spawn = default, NfVec2 meetingSpawn = default)
    {
        var model = new MapModel { MapKey = mapKey, SpawnCenter = spawn, MeetingSpawn = meetingSpawn };
        if (rooms != null) model.Rooms.AddRange(rooms);

        var segments = new List<Segment>(2048);
        var floorZones = new List<(SurfaceKind kind, float[] poly)>();
        var roomZones = new List<float[]>();

        int sourceId = 0;
        foreach (var rec in records)
        {
            int myId = sourceId++;

            // A door's position, whether or not it will ever become geometry. The two consoles that
            // hang off a decontamination door are named after it and sit half a unit to either
            // side, so they are kept out by name rather than found later as a wrong match.
            string lower = rec.Path.ToLowerInvariant();
            if (lower.Contains("door") && !lower.Contains("console") && rec.Paths.Count > 0)
            {
                float sx = 0f, sy = 0f;
                int n = 0;
                foreach (var path in rec.Paths)
                    for (int i = 0; i + 1 < path.Length; i += 2) { sx += path[i]; sy += path[i + 1]; n++; }
                if (n > 0) model.DoorAnchors.Add((myId, new NfVec2(sx / n, sy / n)));
            }

            if (rec.Trigger)
            {
                // Triggers are never geometry, but two kinds of them are information.
                var floorKind = FloorMaterialFor(rec);
                foreach (var path in rec.Paths)
                {
                    if (path.Length < 6) continue;              // needs at least a triangle
                    if (floorKind.HasValue) floorZones.Add((floorKind.Value, path));
                    else if (IsRoomTrigger(rec)) roomZones.Add(path);
                }
                continue;
            }

            if (!IsGeometryLayer(rec.LayerName)) continue;      // Players, UI, Default: not our world

            var material = (byte)WallMaterialFor(rec);
            var height = HeightFor(rec.LayerName);

            foreach (var path in rec.Paths)
            {
                int n = path.Length / 2;
                if (n < 2) continue;
                int last = rec.Closed ? n : n - 1;
                for (int i = 0; i < last; i++)
                {
                    int j = (i + 1) % n;
                    var a = new NfVec2(path[i * 2], path[i * 2 + 1]);
                    var b = new NfVec2(path[j * 2], path[j * 2 + 1]);
                    // Zero-length segments come from colliders with duplicated points and would
                    // divide by zero in the intersection test.
                    if ((b - a).SqrLength < 1e-8f) continue;
                    segments.Add(new Segment
                    {
                        A = a, B = b,
                        Material = material,
                        Height = height,
                        SourceId = myId,
                    });
                }
            }
        }

        model.Geometry.SetSegments(segments);
        BuildFloorGrid(model, floorZones, roomZones);
        return model;
    }

    /// Names that sit directly under the ship root but are emphatically NOT rooms. "Outside" is the
    /// big one: on Polus it is a trigger covering the entire surface of the planet, and treating it
    /// as a room put a ceiling over 85% of the map and hid the night sky almost everywhere.
    /// Two of these were found the hard way, by rendering Polus and finding a ceiling over the
    /// whole planet: "Outside" and "OuterBoundary" are triggers spanning the entire map (the latter
    /// covers 1397 square units of a map that is 1400 square units in total).
    private static readonly string[] NotRooms =
    {
        "outside", "outerboundary", "boundary", "sounds", "shadows", "hull",
        "ambience", "surface", "sky", "background", "dummy",
    };

    /// A room trigger is the one that sits directly under the ship root: "PolusShip(Clone)/Office".
    /// Deeper triggers belong to a system (decon rooms, sound zones), shallower ones are loose
    /// scene objects such as the Freeplay dummies, and neither is a room.
    private static bool IsRoomTrigger(ColliderRecord rec)
    {
        int slashes = 0;
        foreach (char c in rec.Path) if (c == '/') slashes++;
        if (slashes != 1) return false;

        string leaf = rec.LeafName.ToLowerInvariant();
        foreach (var n in NotRooms)
            if (leaf.Contains(n)) return false;
        return true;
    }

    private static void BuildFloorGrid(MapModel model,
                                       List<(SurfaceKind kind, float[] poly)> floorZones,
                                       List<float[]> roomZones)
    {
        var geo = model.Geometry;
        model.FloorOrigin = geo.Min;
        model.FloorW = Math.Max(1, (int)MathF.Ceiling((geo.Max.X - geo.Min.X) / MapModel.FloorCell));
        model.FloorH = Math.Max(1, (int)MathF.Ceiling((geo.Max.Y - geo.Min.Y) / MapModel.FloorCell));

        int cells = model.FloorW * model.FloorH;
        model.FloorMaterial = new byte[cells];
        model.FloorInside = new byte[cells];

        // Default floor: outdoor snow. Anything the zones do not cover is Polus itself.
        for (int i = 0; i < cells; i++) model.FloorMaterial[i] = (byte)SurfaceKind.Snow;

        foreach (var (kind, poly) in floorZones)
            RasterisePolygon(model, poly, (idx) => model.FloorMaterial[idx] = (byte)kind);

        foreach (var poly in roomZones)
            RasterisePolygon(model, poly, (idx) => model.FloorInside[idx] = 1);
    }

    /// Scan-converts a polygon into the floor grid. Plain even-odd fill over the cell centres: the
    /// grid is coarse and the zones are simple convex-ish shapes, so nothing fancier earns its
    /// complexity here.
    private static void RasterisePolygon(MapModel model, float[] poly, Action<int> setCell)
    {
        int n = poly.Length / 2;
        if (n < 3) return;

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            minX = MathF.Min(minX, poly[i * 2]);
            maxX = MathF.Max(maxX, poly[i * 2]);
            minY = MathF.Min(minY, poly[i * 2 + 1]);
            maxY = MathF.Max(maxY, poly[i * 2 + 1]);
        }

        int x0 = Math.Max(0, (int)((minX - model.FloorOrigin.X) / MapModel.FloorCell));
        int x1 = Math.Min(model.FloorW - 1, (int)((maxX - model.FloorOrigin.X) / MapModel.FloorCell) + 1);
        int y0 = Math.Max(0, (int)((minY - model.FloorOrigin.Y) / MapModel.FloorCell));
        int y1 = Math.Min(model.FloorH - 1, (int)((maxY - model.FloorOrigin.Y) / MapModel.FloorCell) + 1);

        for (int gy = y0; gy <= y1; gy++)
        {
            float wy = model.FloorOrigin.Y + (gy + 0.5f) * MapModel.FloorCell;
            for (int gx = x0; gx <= x1; gx++)
            {
                float wx = model.FloorOrigin.X + (gx + 0.5f) * MapModel.FloorCell;
                if (PointInPolygon(wx, wy, poly, n)) setCell(gy * model.FloorW + gx);
            }
        }
    }

    private static bool PointInPolygon(float px, float py, float[] poly, int n)
    {
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = poly[i * 2], yi = poly[i * 2 + 1];
            float xj = poly[j * 2], yj = poly[j * 2 + 1];
            if (((yi > py) != (yj > py)) &&
                (px < (xj - xi) * (py - yi) / (yj - yi + 1e-12f) + xi))
                inside = !inside;
        }
        return inside;
    }
}
