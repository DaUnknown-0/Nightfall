// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * PropSet - the map's furniture, as the map itself draws it.
 *
 * The input is one entry per SpriteRenderer, cut out of the live scene with its own alpha and its
 * own rectangle in world coordinates (SpriteHarvest in the game, the dump loader in the tool).
 * What comes out is a list of things that can be put into a 3D scene.
 *
 * TWO DECISIONS ARE MADE HERE, AND ONLY TWO
 * -----------------------------------------
 * 1. WHAT BELONGS TOGETHER. Among Us builds a prop out of several renderers: the admin map table
 *    is `mapTable` plus `mapTable/map_admin` plus two `panel_map` children. Kept apart, each child
 *    would be stood up on the floor as an object of its own and the map screen would end up lying
 *    at the table's feet. So a piece whose PARENT is also a kept prop is composited into that
 *    parent, in the order the game itself draws them.
 *
 * 2. WHETHER IT STANDS UP OR LIES DOWN. Among Us draws its props in near-front elevation - what you
 *    see of a table is the front of the table - so the honest reading of a prop's drawn rectangle
 *    is: the bottom edge is where it meets the floor, the height is its height. Some things are
 *    genuinely painted flat on the ground (the dropship ramp, the contact shadows under the sample
 *    posts, the water in Life Support), and those keep lying down.
 *
 * WHAT IS DELIBERATELY THROWN AWAY
 * --------------------------------
 *   the parallax sky      already drawn as sky, and it is sixty units behind the map
 *   `<Room>/Walls`        the room's own floor-and-wall artwork: that IS the floor photograph and
 *                         the wall bands, and standing it up would put a copy of the whole room
 *                         on its edge in the middle of itself
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public enum PropStance
{
    /// A vertical, camera-facing panel standing on the floor.
    Upright,
    /// A horizontal panel lying on the floor: ramps, decals, contact shadows, water.
    Flat,
}

/// One drawn object, ready to be placed.
public sealed class PropPiece
{
    public string Path = "";
    public string Name = "";
    public int UnityLayer;
    public float Z;
    public bool HasCollider;

    /// The collider this object was cut out around, when it had no sprite of its own. -1 otherwise.
    /// Scene3D uses it to stop building its own box for the same collider.
    public int SourceId = -1;

    /// World rectangle of the drawn pixels, in map coordinates.
    public NfVec2 Min, Max;

    /// RGBA, top row first.
    public byte[] Rgba = Array.Empty<byte>();
    public int W, H;

    public PropStance Stance = PropStance.Upright;

    public float WorldWidth => Max.X - Min.X;
    public float WorldHeight => Max.Y - Min.Y;
    public NfVec2 Centre => new((Min.X + Max.X) * 0.5f, (Min.Y + Max.Y) * 0.5f);

    /// Texture built lazily, once, when the scene is assembled.
    public Surface3D Surface;

    public Surface3D GetSurface() => Surface ??= new Surface3D(Rgba, W, H, cutout: true);
}

public static class PropSet
{
    // ================================================================================
    // Classification
    // ================================================================================
    /// Path fragments that are not part of the walkable world at all.
    private static readonly string[] Ignored =
    {
        "/ParallaxBg/",       // the sky, drawn as sky, sixty units behind everything
        "ParallaxBg/",
    };

    /// Objects painted flat on the ground. Matched on the object's own name, lower-cased.
    ///
    /// This list is short on purpose. Everything Among Us draws is in elevation unless it is
    /// obviously a surface, and guessing from the aspect ratio got the Weapons gun (long and thin,
    /// and very much standing) wrong in both directions.
    private static readonly string[] FlatNames =
    {
        "ramp",             // the dropship ramp: the yellow grid you walk down
        "shadow",           // shadowDirt, shadowSnow: contact shadows under the sample posts
        "water1",           // the pool in Life Support
        "watergrate",
        "snowmanded",       // a snowman lying in the snow, drawn from above
        "dirt", "snowpatch",
    };

    private static bool IsIgnored(string path)
    {
        foreach (var frag in Ignored)
            if (path.Contains(frag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// The room's own artwork. It is the floor photograph and the wall bands, both of which the
    /// scene already builds from, so it must not become an object as well.
    private static bool IsRoomArt(string name) =>
        name.Equals("Walls", StringComparison.OrdinalIgnoreCase);

    private static PropStance StanceFor(string name)
    {
        string n = name.ToLowerInvariant();
        foreach (var f in FlatNames)
            if (n.Contains(f, StringComparison.Ordinal)) return PropStance.Flat;
        return PropStance.Upright;
    }

    private static string NameOf(string path)
    {
        int i = path.LastIndexOf('/');
        return i < 0 ? path : path.Substring(i + 1);
    }

    private static string ParentOf(string path)
    {
        int i = path.LastIndexOf('/');
        return i < 0 ? "" : path.Substring(0, i);
    }

    // ================================================================================
    // Assembly
    // ================================================================================
    /// Turns the raw harvest into placeable props. `raw` must be in the order the game returned
    /// the renderers, which is parent before child - that is also the order they are drawn in.
    public static List<PropPiece> Build(IReadOnlyList<PropPiece> raw)
    {
        // ---- which pieces survive at all ----
        var kept = new List<PropPiece>();
        var keptByPath = new Dictionary<string, int>();
        foreach (var p in raw)
        {
            if (IsIgnored(p.Path)) continue;
            string name = NameOf(p.Path);
            if (IsRoomArt(name)) continue;
            if (p.W < 2 || p.H < 2) continue;

            p.Name = name;
            p.Stance = StanceFor(name);
            keptByPath[p.Path] = kept.Count;
            kept.Add(p);
        }

        // ---- who is a child of whom ----
        //
        // A piece merges into its nearest KEPT ancestor. "Kept" matters: `Comms/Walls/commstable`
        // has a parent in the harvest, but that parent is the room's artwork and was thrown away,
        // so the table is a root in its own right rather than a decal on a discarded sprite.
        var rootOf = new int[kept.Count];
        for (int i = 0; i < kept.Count; i++)
        {
            rootOf[i] = i;
            string parent = ParentOf(kept[i].Path);
            int guard = 0;
            while (parent.Length > 0 && guard++ < 24)
            {
                if (keptByPath.TryGetValue(parent, out int pi) && pi != i)
                {
                    rootOf[i] = rootOf[pi];
                    break;
                }
                parent = ParentOf(parent);
            }
        }

        // ---- composite each family into one image ----
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < kept.Count; i++)
        {
            if (!groups.TryGetValue(rootOf[i], out var list))
                groups[rootOf[i]] = list = new List<int>();
            list.Add(i);
        }

        var result = new List<PropPiece>(groups.Count);
        foreach (var kv in groups)
        {
            var members = kv.Value;
            result.Add(members.Count == 1 ? kept[members[0]] : Composite(kept, members));
        }
        return result;
    }

    /// Draws a family of pieces into one image, in order, at the resolution of the harvest.
    private static PropPiece Composite(List<PropPiece> all, List<int> members)
    {
        var root = all[members[0]];

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (int i in members)
        {
            var p = all[i];
            minX = MathF.Min(minX, p.Min.X); minY = MathF.Min(minY, p.Min.Y);
            maxX = MathF.Max(maxX, p.Max.X); maxY = MathF.Max(maxY, p.Max.Y);
        }

        // Pixels per unit, taken from the root so the family keeps the harvest's own detail.
        float ppu = root.W / MathF.Max(0.001f, root.WorldWidth);
        int w = Math.Clamp((int)MathF.Round((maxX - minX) * ppu), 2, 2048);
        int h = Math.Clamp((int)MathF.Round((maxY - minY) * ppu), 2, 2048);
        var px = new byte[w * h * 4];

        foreach (int i in members)
        {
            var p = all[i];
            // Where this member lands in the composite. Image y grows downwards, world y upwards,
            // so the TOP of the member's world rectangle is its first row.
            int dx = (int)MathF.Round((p.Min.X - minX) * ppu);
            int dy = (int)MathF.Round((maxY - p.Max.Y) * ppu);
            int dw = Math.Max(1, (int)MathF.Round(p.WorldWidth * ppu));
            int dh = Math.Max(1, (int)MathF.Round(p.WorldHeight * ppu));

            for (int y = 0; y < dh; y++)
            {
                int ty = dy + y;
                if (ty < 0 || ty >= h) continue;
                int sy = Math.Clamp(y * p.H / dh, 0, p.H - 1);
                for (int x = 0; x < dw; x++)
                {
                    int tx = dx + x;
                    if (tx < 0 || tx >= w) continue;
                    int sx = Math.Clamp(x * p.W / dw, 0, p.W - 1);

                    int so = (sy * p.W + sx) * 4;
                    float a = p.Rgba[so + 3] / 255f;
                    if (a <= 0.004f) continue;

                    int to = (ty * w + tx) * 4;
                    float da = px[to + 3] / 255f;
                    float outA = a + da * (1f - a);
                    for (int c = 0; c < 3; c++)
                        px[to + c] = NfMath.ToByte(
                            (p.Rgba[so + c] / 255f * a + px[to + c] / 255f * da * (1f - a))
                            / MathF.Max(0.0001f, outA));
                    px[to + 3] = NfMath.ToByte(outA);
                }
            }
        }

        return new PropPiece
        {
            Path = root.Path,
            Name = root.Name,
            UnityLayer = root.UnityLayer,
            Z = root.Z,
            HasCollider = root.HasCollider,
            Min = new NfVec2(minX, minY),
            Max = new NfVec2(maxX, maxY),
            Rgba = px,
            W = w,
            H = h,
            Stance = root.Stance,
        };
    }
}
