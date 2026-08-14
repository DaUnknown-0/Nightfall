// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * SceneGeometry - builds the renderer's MapModel from the live Among Us scene.
 *
 * This is the in-game twin of the offline SurveyLoader: same output, same builder, different
 * source. The tool reads a JSON file because it has no game; the plugin reads the colliders
 * directly because it has no file. Everything downstream of this point is shared code, which is
 * what makes a frame checked in a PNG a promise about the frame the player will see.
 *
 * It runs ONCE per round, right after the map has settled. That is the entire reason the renderer
 * can raycast in pure C#: after this call, nothing the renderer does crosses the Il2Cpp boundary.
 */

using System;
using System.Collections.Generic;
using Nightfall.Core;
using UnityEngine;

namespace Nightfall;

public static class SceneGeometry
{
    public static MapModel Current { get; private set; }
    public static string CurrentMapKey { get; private set; } = "";

    /// Doors are the only geometry that moves. They are kept so the model can be told to open and
    /// close them without being rebuilt.
    private static readonly List<(OpenableDoor door, int sourceId)> doors = new();
    private static readonly Dictionary<int, bool> doorState = new();

    /// The DECONTAMINATION doors, tracked separately, because they can never be in the list above:
    /// ShipStatus.AllDoors is typed OpenableDoor[], and the Polus decon doors are ManualDoor,
    /// which extends SomeKindaDoor directly - no IsOpen, no network Id, driven purely by
    /// DeconSystem.UpdateDoorsViaState -> SetDoorway. Before this list existed their closed
    /// colliders were baked as permanent walls: the vanilla door opened underneath (the button
    /// sound plays, the collider goes off) while the first-person picture kept showing it shut.
    /// State is read from each baked collider itself ("enabled" means "blocks"), which is the
    /// physical truth SetDoorway writes and needs no knowledge of the door subclass at all.
    private static readonly List<(Collider2D col, int sourceId)> deconDoors = new();

    public static bool IsBuilt => Current != null;

    public static void Clear()
    {
        Current = null;
        CurrentMapKey = "";
        doors.Clear();
        doorState.Clear();
        deconDoors.Clear();
    }

    /// Walks the scene and builds the model. Costs a few dozen milliseconds on the biggest map,
    /// which is why it happens at the start of a round and never during one.
    public static bool Build()
    {
        try
        {
            var ship = ShipStatus.Instance;
            if (ship == null) return false;

            var started = DateTime.Now;
            var records = new List<ColliderRecord>(512);
            int sourceId = 0;
            var doorSources = new Dictionary<int, OpenableDoor>();

            // Map every door's collider to the door that owns it, so the model can toggle them.
            var doorLookup = new Dictionary<IntPtr, OpenableDoor>();
            try
            {
                var all = ship.AllDoors;
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        var d = all[i];
                        if (d == null) continue;
                        foreach (var c in d.GetComponentsInChildren<Collider2D>())
                            if (c != null) doorLookup[c.Pointer] = d;
                    }
                }
            }
            catch { }

            // The decon doors' colliders, by pointer. NOT reachable through AllDoors (see the
            // deconDoors field comment); found through the DeconSystem components that own them.
            var deconLookup = new HashSet<IntPtr>();
            try
            {
                foreach (var ds in UnityEngine.Object.FindObjectsOfType<DeconSystem>())
                {
                    if (ds == null) continue;
                    foreach (var d in new SomeKindaDoor[] { ds.UpperDoor, ds.LowerDoor })
                    {
                        if (d == null) continue;
                        foreach (var c in d.GetComponentsInChildren<Collider2D>())
                            if (c != null) deconLookup.Add(c.Pointer);
                    }
                }
            }
            catch { }

            var deconCols = new Dictionary<int, Collider2D>();
            var colliders = UnityEngine.Object.FindObjectsOfType<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null) continue;

                var rec = Convert(col);
                if (rec == null) continue;

                records.Add(rec);
                int myId = sourceId++;

                if (doorLookup.TryGetValue(col.Pointer, out var owner) && owner != null)
                    doorSources[myId] = owner;
                else if (deconLookup.Contains(col.Pointer))
                    deconCols[myId] = col;
            }

            var rooms = ReadRooms(ship);
            var model = MapModelBuilder.Build(MapKey(ship), records, rooms,
                                              V(ship.InitialSpawnCenter), V(ship.MeetingSpawnCenter));

            Current = model;
            CurrentMapKey = model.MapKey;

            doors.Clear();
            doorState.Clear();
            deconDoors.Clear();
            foreach (var kv in doorSources) doors.Add((kv.Value, kv.Key));
            foreach (var kv in deconCols) deconDoors.Add((kv.Value, kv.Key));

            var ms = (DateTime.Now - started).TotalMilliseconds;
            NightfallPlugin.Logger?.LogInfo(
                $"[Nightfall] Geometry for '{CurrentMapKey}' built in {ms:F0} ms: "
                + $"{records.Count} colliders -> {model.Geometry.SegmentCount} segments, "
                + $"{doors.Count} door colliders (+{deconDoors.Count} decon), "
                + $"grid {model.Geometry.GridWidth}x{model.Geometry.GridHeight}");
            return true;
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] Geometry build failed: {e}");
            Current = null;
            return false;
        }
    }

    /// Opens and closes door segments in the model to match the game. Called every frame; the
    /// dictionary compare makes that free when nothing has changed.
    public static void SyncDoors(Nightfall.Core.Scene3D scene = null)
    {
        if (Current == null || (doors.Count == 0 && deconDoors.Count == 0)) return;
        try
        {
            foreach (var (door, sourceId) in doors)
            {
                if (door == null) continue;
                ApplyDoorState(scene, sourceId, door.IsOpen);
            }

            // The decon doors have no IsOpen; the baked collider itself is the state. When
            // DeconSystem opens the real door it disables exactly this collider, so "disabled"
            // is "open" by definition - for the solid door panel and its shadow strip alike.
            foreach (var (col, sourceId) in deconDoors)
            {
                if (col == null) continue;
                bool open;
                try { open = !col.enabled; } catch { continue; }
                bool logged = doorState.TryGetValue(sourceId, out bool was) && was != open;
                ApplyDoorState(scene, sourceId, open);
                // Playtest evidence for the "decon door does not open" report: if these lines say
                // OPEN while the picture still shows a wall, the remaining gap is the visual door
                // panel match, not the state sync.
                if (logged)
                    NightfallPlugin.Logger?.LogInfo(
                        $"[Nightfall] decon door source {sourceId} -> {(open ? "OPEN" : "CLOSED")}");
            }
        }
        catch { }
    }

    private static void ApplyDoorState(Nightfall.Core.Scene3D scene, int sourceId, bool open)
    {
        if (doorState.TryGetValue(sourceId, out bool was) && was == open) return;
        doorState[sourceId] = open;
        Current.Geometry.SetSourceEnabled(sourceId, !open);
        // A closed door is real geometry in the model and an open one is simply absent.
        // That is the whole fix for being able to see through closed doors.
        scene?.SetDoorOpen(sourceId, open);
    }

    // ================================================================================
    // Conversion
    // ================================================================================
    private static ColliderRecord Convert(Collider2D col)
    {
        var tf = col.transform;
        var rec = new ColliderRecord
        {
            Path = HierarchyPath(tf),
            Layer = col.gameObject.layer,
            LayerName = LayerMask.LayerToName(col.gameObject.layer) ?? "",
            Trigger = col.isTrigger,
            Closed = true,
        };

        var box = Cast<BoxCollider2D>(col);
        if (box != null)
        {
            rec.Type = "box";
            Vector2 o = box.offset, s = box.size * 0.5f;
            rec.Paths.Add(new[]
            {
                W(tf, o.x - s.x, o.y - s.y), W2(tf, o.x - s.x, o.y - s.y),
                W(tf, o.x + s.x, o.y - s.y), W2(tf, o.x + s.x, o.y - s.y),
                W(tf, o.x + s.x, o.y + s.y), W2(tf, o.x + s.x, o.y + s.y),
                W(tf, o.x - s.x, o.y + s.y), W2(tf, o.x - s.x, o.y + s.y),
            });
            return rec;
        }

        var poly = Cast<PolygonCollider2D>(col);
        if (poly != null)
        {
            rec.Type = "polygon";
            for (int p = 0; p < poly.pathCount; p++)
            {
                var pts = poly.GetPath(p);
                if (pts == null || pts.Length < 2) continue;
                var flat = new float[pts.Length * 2];
                for (int k = 0; k < pts.Length; k++)
                {
                    var v = pts[k] + poly.offset;
                    flat[k * 2] = W(tf, v.x, v.y);
                    flat[k * 2 + 1] = W2(tf, v.x, v.y);
                }
                rec.Paths.Add(flat);
            }
            return rec.Paths.Count > 0 ? rec : null;
        }

        var edge = Cast<EdgeCollider2D>(col);
        if (edge != null)
        {
            rec.Type = "edge";
            rec.Closed = false;
            var pts = edge.points;
            if (pts == null || pts.Length < 2) return null;
            var flat = new float[pts.Length * 2];
            for (int k = 0; k < pts.Length; k++)
            {
                var v = pts[k] + edge.offset;
                flat[k * 2] = W(tf, v.x, v.y);
                flat[k * 2 + 1] = W2(tf, v.x, v.y);
            }
            rec.Paths.Add(flat);
            return rec;
        }

        var circle = Cast<CircleCollider2D>(col);
        if (circle != null)
        {
            rec.Type = "circle";
            const int seg = 16;
            var flat = new float[seg * 2];
            for (int k = 0; k < seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg;
                float lx = circle.offset.x + Mathf.Cos(a) * circle.radius;
                float ly = circle.offset.y + Mathf.Sin(a) * circle.radius;
                flat[k * 2] = W(tf, lx, ly);
                flat[k * 2 + 1] = W2(tf, lx, ly);
            }
            rec.Paths.Add(flat);
            return rec;
        }

        return null;   // capsules and anything exotic: not present on any Among Us map
    }

    private static List<RoomInfo> ReadRooms(ShipStatus ship)
    {
        var list = new List<RoomInfo>();
        try
        {
            var rooms = ship.AllRooms;
            if (rooms == null) return list;
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                if (r == null) continue;
                var info = new RoomInfo { Name = r.name };
                try { info.SystemType = r.RoomId.ToString(); } catch { }
                try
                {
                    var area = r.roomArea;
                    if (area != null)
                    {
                        var b = area.bounds;
                        info.Min = new NfVec2(b.min.x, b.min.y);
                        info.Max = new NfVec2(b.max.x, b.max.y);
                    }
                }
                catch { }
                list.Add(info);
            }
        }
        catch { }
        return list;
    }

    // ================================================================================
    // Helpers
    // ================================================================================
    /// TryCast that yields null for a type this Il2Cpp domain never registered, rather than
    /// throwing. CompositeCollider2D is exactly such a type in Among Us, and a straight-line
    /// sequence of casts once cost this project every collider on the map.
    private static T Cast<T>(Collider2D col) where T : Il2CppObjectBase
    {
        try { return col.TryCast<T>(); }
        catch { return null; }
    }

    private static float W(Transform tf, float lx, float ly) => tf.TransformPoint(new Vector3(lx, ly, 0f)).x;
    private static float W2(Transform tf, float lx, float ly) => tf.TransformPoint(new Vector3(lx, ly, 0f)).y;
    private static NfVec2 V(Vector2 v) => new(v.x, v.y);

    private static string HierarchyPath(Transform tf)
    {
        if (tf == null) return "";
        var sb = new System.Text.StringBuilder(tf.name);
        var cur = tf.parent;
        int guard = 0;
        while (cur != null && guard++ < 32)
        {
            sb.Insert(0, cur.name + "/");
            cur = cur.parent;
        }
        return sb.ToString();
    }

    private static string MapKey(ShipStatus ship)
    {
        try
        {
            string n = ship.gameObject.name.Replace("(Clone)", "").Trim();
            if (!string.IsNullOrEmpty(n))
            {
                var sb = new System.Text.StringBuilder(n.Length);
                foreach (char c in n)
                    sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
                return sb.ToString();
            }
        }
        catch { }
        return "map";
    }
}
