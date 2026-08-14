// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * PolusRooms - the map, described by looking at it.
 *
 * WHY BY HAND
 * -----------
 * Everything automatic was tried first and each attempt failed for the same reason: the collision
 * data says where you cannot walk, and nothing else. It does not know that Security has a red
 * floor and dark green ribbed walls, that there is a desk with a monitor and a red filing shelf
 * against its north wall, that its lower corners are chamfered, or that a chair stands by the west
 * wall. All of that is plainly visible in the map artwork and completely absent from the colliders.
 *
 * So the rooms are described here, read off the real map image at a known scale. It is more work
 * per room and it is the only way the result is actually Polus rather than a plausible corridor.
 *
 * HOW POSITIONS WERE OBTAINED
 * ---------------------------
 * The mod photographs the map at a known world rectangle (MapCapture), so any pixel in that
 * photograph converts to a world coordinate exactly. Room extents come from the game's own room
 * bounds; the furniture inside them was measured off the photograph.
 *
 * Rooms not described here still work: Scene3D falls back to building them from colliders, which
 * gives correct walls and plain surfaces. Describing a room upgrades it; leaving it out never
 * breaks it.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

/// One piece of furniture: a box on the floor with a size, a colour and a role.
public sealed class RoomProp
{
    public NfVec2 Min, Max;
    public float Height = 0.55f;
    public NfColor Color;
    public SurfaceRole Role = SurfaceRole.PropSide;
    /// Which way the interesting face points, in radians. Screens and shelf fronts use it.
    public float Facing;
}

/// One room, as read off the map.
public sealed class RoomDef
{
    public string Key = "";
    public NfVec2 Min, Max;
    public NfColor FloorColor;
    public NfColor WallColor;
    /// Wall segments that carry a window, given as world-space pairs. Empty means none.
    public readonly List<(NfVec2 a, NfVec2 b)> Windows = new();
    public readonly List<RoomProp> Props = new();
}

public static class PolusRooms
{
    private static Dictionary<string, RoomDef> byKey;

    public static RoomDef Find(string systemType)
    {
        EnsureBuilt();
        return systemType != null && byKey.TryGetValue(systemType.ToLowerInvariant(), out var r) ? r : null;
    }

    public static IEnumerable<RoomDef> All
    {
        get { EnsureBuilt(); return byKey.Values; }
    }

    private static void EnsureBuilt()
    {
        if (byKey != null) return;
        byKey = new Dictionary<string, RoomDef>();
        Add(Security());
    }

    private static void Add(RoomDef r) => byKey[r.Key.ToLowerInvariant()] = r;

    private static NfColor Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

    // ================================================================================
    // Security
    // ================================================================================
    /// Read off the map photograph. Security is a small red-floored room with dark green ribbed
    /// walls, a desk with a monitor and a red filing shelf along the north wall, a chair at the
    /// west wall, and chamfered lower corners. The game's own bounds put it at
    /// [1.556, -13.02] .. [4.426, -11.095].
    private static RoomDef Security()
    {
        var r = new RoomDef
        {
            Key = "Security",
            Min = new NfVec2(1.556f, -13.02f),
            Max = new NfVec2(4.426f, -11.095f),
            // The floor really is this red, and it is the single most recognisable thing about
            // the room. Every earlier version painted it grey.
            FloorColor = Rgb(0x8E, 0x46, 0x5C),
            WallColor = Rgb(0x40, 0x5E, 0x4C),
        };

        // Desk with monitor, against the north wall towards the east side.
        r.Props.Add(new RoomProp
        {
            Min = new NfVec2(3.16f, -11.42f),
            Max = new NfVec2(3.92f, -11.10f),
            Height = 0.52f,
            Color = Rgb(0x8A, 0x6A, 0x46),
            Role = SurfaceRole.ConsoleFront,
            Facing = -NfMath.Pi * 0.5f,         // faces south, into the room
        });

        // The red filing shelf beside it.
        r.Props.Add(new RoomProp
        {
            Min = new NfVec2(3.94f, -11.44f),
            Max = new NfVec2(4.34f, -11.10f),
            Height = 0.78f,
            Color = Rgb(0x8E, 0x30, 0x30),
            Role = SurfaceRole.PropSide,
            Facing = -NfMath.Pi * 0.5f,
        });

        // Chair at the west wall.
        r.Props.Add(new RoomProp
        {
            Min = new NfVec2(1.62f, -11.85f),
            Max = new NfVec2(1.90f, -11.52f),
            Height = 0.42f,
            Color = Rgb(0x33, 0x3B, 0x40),
            Role = SurfaceRole.PropSide,
            Facing = 0f,
        });

        return r;
    }
}
