// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * THE SURFACES OF THE BUILT WORLD - the port of Assets/NightfallWeb/src/surfaces.js.
 *
 * SURFACES ARE DRAWN, NOT PHOTOGRAPHED. The mod's older path laid the map photograph on the ground
 * as one big plate. Seen from above that is flawless - it IS the photograph - and at eye height it
 * is a picture of a floor rather than a floor: no tile has an edge, no plank has a seam, and every
 * object in the room stands on a second, flat copy of itself. (The mod even carried a repair pass,
 * FloorRepair, whose whole job was to paint the furniture back out of that photograph.)
 *
 * So each surface here is generated: a few rectangles, a grout line, a little speckle. The COLOURS
 * were read off the map photograph while the prototype was built - that is what the map is for -
 * and the STRUCTURE is drawn.
 *
 * A TEXTURE DECLARES ITS OWN SIZE. `Unit` is how many Among Us units one tile of it covers, so a
 * floor tile is the same physical size in every room. AreaKit turns that into UV coordinates, which
 * is cheaper than the prototype's approach of cloning a texture per repeat count: here every
 * material in the world exists exactly once, 128 by 128, and one wall's UVs run 0..7 while
 * another's run 0..2.
 *
 * A TEXTURE IS ALBEDO, NOT THE FINISHED COLOUR. Several entries are painted a shade below what the
 * map shows, and the comments say so where it matters - a value that is right on a flat, unlit map
 * clips to white as soon as a torch is pointed at it.
 *
 * THE ALPHA CHANNEL IS THE TINT MASK, not opacity (see Surface3D). Everything here is drawn in its
 * own final colour, so it is stamped to zero: these surfaces are never tinted by a room colour.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public static partial class AreaSurfaces
{
    /// The DESIGN grid of a texture tile: every number in the catalogue below is measured in these
    /// units, and they were tuned against the map artwork. It stays at the prototype's 128.
    public const int PX = 128;

    /// The FINEST resolution a material is drawn at: how many real pixels one design unit
    /// becomes. See `DetailFor` for which materials actually get it.
    ///
    /// It used to be one, and one is what the first playtest found too soft: a wall you stand at
    /// fills most of the screen, so a 128-pixel tile is magnified four to six times and every seam
    /// is a gradient. Two is the whole difference between a drawn panel and a blurred one, and it
    /// costs almost nothing at render time because the mip pyramid means a distant wall still reads
    /// a level whose texels are pixel sized. What it does cost is memory: one surface at 256 square
    /// with its pyramid is 349 KB, against 87 KB at 128.
    public const int Detail = 2;

    /// Below this `Unit`, a material is drawn at half resolution.
    ///
    /// A TEXTURE'S SHARPNESS IS TEXELS PER WORLD UNIT, NOT TEXELS. `Unit` says how much world one
    /// tile covers, so the same 256-pixel tile is 177 texels per unit on a 1.45-unit wall panel and
    /// 1280 texels per unit on a 0.2-unit gem. The wall is what the playtest was complaining about;
    /// the gem was never anywhere near soft, and paying wall prices for it is simply waste.
    ///
    /// So the rule is one density for everything: match what Detail 2 gives the wall, 256/1.45 =
    /// 177 texels per unit. At 128 pixels a tile reaches that density up to Unit = 128/177 = 0.72,
    /// which is where the line sits. Nothing gets SOFTER than the softest thing today; the small
    /// stuff just stops being four times sharper than it needs to be.
    ///
    /// Measured on Mira HQ's 215 materials: 71,7 MB of retained pixels before, 46,4 MB after. The
    /// offline renderer's 67 viewpoints come out with a mean absolute error of 0,00 to 0,02 of 255
    /// and a largest single-pixel difference of 3 - which is to say the change is invisible - and
    /// the measured frame cost is the same to within run-to-run noise.
    public const float HalfDetailBelowUnit = 0.72f;

    /// The device resolution for a material of this `Unit`.
    public static int DetailFor(float unit) => unit < HalfDetailBelowUnit ? 1 : Detail;

    private sealed class Spec
    {
        public float Unit = 1f;
        public float Emissive;
        public Action<Canvas2D> Draw;
    }

    private static readonly Dictionary<string, Surface3D> cache = new();
    private static readonly Dictionary<string, Surface3D> plainCache = new();

    /// ONE drawing buffer per resolution, reused for every material.
    ///
    /// The canvas is a scratch pad: `ToRgba` copies the finished pixels out, and nothing keeps a
    /// reference to it afterwards. Handing every material its own was therefore pure churn - see
    /// the note on `Canvas2D.Reset`. Two entries at most, 1,25 MB together, held for the round.
    private static readonly Dictionary<int, Canvas2D> scratch = new();

    private static Canvas2D Scratch(int detail)
    {
        if (scratch.TryGetValue(detail, out var cv)) { cv.Reset(); return cv; }
        cv = new Canvas2D(PX, PX, detail);
        scratch[detail] = cv;
        return cv;
    }

    /// What the catalogue built so far is holding, in bytes. Reported at the end of a world build:
    /// the map that ran a 32-bit Among Us out of address space did it here, and a number in the log
    /// is the difference between seeing that and inferring it afterwards.
    private static long retainedBytes;

    public static long RetainedBytes_ => retainedBytes;
    public static int Count => cache.Count;

    /// Base image plus its mip pyramid, which converges to 4/3 of the base.
    private static long RetainedBytes(int w, int h) => (long)(w * h * 4L * 4 / 3);

    public static void ClearCache()
    {
        cache.Clear();
        plainCache.Clear();
        // The scratch buffers go too. They are small, but Reset is a round-end teardown and a
        // megabyte held across a lobby the player has left is a megabyte held for nothing.
        scratch.Clear();
        retainedBytes = 0;
    }

    /// How many world units one tile of this material covers.
    public static float UnitOf(string name) =>
        name != null && Catalogue.TryGetValue(name, out var s) ? s.Unit : 1f;

    /// How much light this material gives off by itself. Only the lava and the specimen fluids do.
    public static float EmissiveOf(string name) =>
        name != null && Catalogue.TryGetValue(name, out var s) ? s.Emissive : 0f;

    /// Resolves a material name, a "#rrggbb" colour, or null (the placeholder grey).
    public static Surface3D Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return Plain(new NfColor(0.54f, 0.54f, 0.54f));
        if (name[0] == '#') return Plain(Hex(name));
        if (cache.TryGetValue(name, out var s)) return s;
        if (!Catalogue.TryGetValue(name, out var spec))
        {
            // A misspelt material must be LOUD, not silently grey: in a night scene a wrong grey
            // wall is indistinguishable from a right one until someone shines a torch on it.
            Scene3D.NightfallLog($"[Nightfall] unknown surface \"{name}\" - drawn as magenta");
            s = Plain(new NfColor(1f, 0f, 0.8f));
            cache[name] = s;
            return s;
        }
        var cv = Scratch(DetailFor(spec.Unit));
        spec.Draw(cv);
        cv.SetTintMask(0, 0, PX, PX, 0f);
        s = new Surface3D(cv.ToRgba(), cv.PW, cv.PH);
        cache[name] = s;
        retainedBytes += RetainedBytes(cv.PW, cv.PH);
        return s;
    }

    /// A flat colour, for the odd part that has no pattern (a lamp housing, a pipe, a screen).
    public static Surface3D Plain(NfColor c)
    {
        string key = $"{c.R:0.###},{c.G:0.###},{c.B:0.###}";
        if (plainCache.TryGetValue(key, out var s)) return s;
        // 2x2 rather than 128x128: a flat colour needs four texels, and there are a few dozen of
        // these once every #rrggbb in the area files has been resolved.
        var cv = new Canvas2D(2, 2);
        cv.Clear(c, 0f);
        s = new Surface3D(cv.ToRgba(), 2, 2);
        plainCache[key] = s;
        return s;
    }

    public static NfColor Hex(string s)
    {
        if (s == null || s.Length < 7 || s[0] != '#') return new NfColor(0.6f, 0.6f, 0.6f);
        int r = Convert.ToInt32(s.Substring(1, 2), 16);
        int g = Convert.ToInt32(s.Substring(3, 2), 16);
        int b = Convert.ToInt32(s.Substring(5, 2), 16);
        return new NfColor(r / 255f, g / 255f, b / 255f);
    }

    // ================================================================================
    // Drawing helpers
    // ================================================================================
    private static NfColor C(string hex) => Hex(hex);

    private static void Fill(Canvas2D g, string col) => g.Clear(C(col), 1f);

    private static void Line(Canvas2D g, float x0, float y0, float x1, float y1, string col,
                             float w = 1f, float a = 1f) => g.Line(x0, y0, x1, y1, w, C(col), a);

    private static void Rect(Canvas2D g, float x, float y, float w, float h, string col,
                             float a = 1f) => g.FillRect(x, y, w, h, C(col), a);

    /// A DETERMINISTIC speckle: a few hundred barely-visible dots that kill the "plastic sheet" look
    /// of a flat fill.
    ///
    /// The prototype uses Math.random(); here the sequence is hashed from a seed, because the
    /// offline render tool and the game have to produce the same texture. A texture that differs
    /// between the two makes the tool's whole purpose - "what is checked outside the game is what
    /// the game draws" - quietly untrue.
    private static void Grain(Canvas2D g, string[] cols, int n = 900, float a = 0.10f, int seed = 1)
    {
        var rnd = new Rng(seed);
        for (int i = 0; i < n; i++)
        {
            var c = C(cols[(int)(rnd.Next() * cols.Length) % cols.Length]);
            float s = 1f + rnd.Next() * 3f;
            g.FillRect(rnd.Next() * g.W, rnd.Next() * g.H, s, s, c, a);
        }
    }

    /// An ellipse drawn nine times, offset by the tile, so a blotch that runs off one edge comes
    /// back in on the other. Without this every blotch is cut off at the tile edge and the repeat
    /// reads as a chequerboard across the whole planet.
    private static void WrapEllipse(Canvas2D g, float cx, float cy, float rx, float ry,
                                    string col, float a)
    {
        var c = C(col);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                g.FillEllipse(cx + dx * g.W, cy + dy * g.H, rx, ry, c, a);
    }

    /// Small deterministic generator, so a texture is the same everywhere. Not a good PRNG and does
    /// not need to be: it scatters dots.
    private struct Rng
    {
        private uint s;
        public Rng(int seed) { s = (uint)(seed * 2654435761u + 12345u); }
        public float Next()
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// Folds the per-map halves of the catalogue into the one dictionary every lookup uses.
    ///
    /// Mira HQ contributes 215 materials - more than Polus and the Skeld together - and they live
    /// in AreaSurfacesMira.cs so that neither file has to be scrolled past to reach the other. A
    /// static constructor rather than a partial dictionary literal because a dictionary can only
    /// be initialised in one place; both halves stay plain `["name"] = ...` lists this way.
    static AreaSurfaces()
    {
        foreach (var kv in MiraCatalogue) Catalogue[kv.Key] = kv.Value;
    }

    // ================================================================================
    // The catalogue
    // ================================================================================
    private static readonly Dictionary<string, Spec> Catalogue = new()
    {
        // ---------------------------------------------------------------- floors
        // Office / meeting room: dark blue slabs with a lighter, finely hatched face.
        ["tileBlue"] = new Spec { Unit = 0.55f, Draw = g => {
            Fill(g, "#22406f");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#3c5a9e");
            for (float i = -g.H; i < g.W; i += 7) Line(g, i, 0, i + g.H, g.H, "#4a6cbb", 3, 0.35f);
            Grain(g, new[] { "#294984", "#4d70c0" }, 900, 0.10f, 11);
        } },

        // Corridors outside the rooms: the same slab, one shade brighter and larger.
        ["tileHall"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#4a63a0");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#5d78b5");
            for (float i = -g.H; i < g.W; i += 9) Line(g, i, 0, i + g.H, g.H, "#6f89c4", 3, 0.3f);
            Grain(g, new[] { "#5d78b5", "#7089c0" }, 900, 0.10f, 12);
        } },

        // Office east half: floorboards. Seams run east-west, as they are drawn on the map.
        ["wood"] = new Spec { Unit = 0.62f, Draw = g => {
            Fill(g, "#a89270");
            for (int i = 0; i < 3; i++) Line(g, 0, g.H * (i + 1) / 3f, g.W, g.H * (i + 1) / 3f, "#8d6f52", 2);
            Line(g, g.W * 0.42f, 0, g.W * 0.42f, g.H / 3f, "#8d6f52", 2);
            Line(g, g.W * 0.78f, g.H / 3f, g.W * 0.78f, g.H * 2f / 3f, "#8d6f52", 2);
            Line(g, g.W * 0.2f, g.H * 2f / 3f, g.W * 0.2f, g.H, "#8d6f52", 2);
            Grain(g, new[] { "#b6a081", "#96805f" }, 700, 0.16f, 13);
        } },

        // Admin: a woven carpet, the darkest floor on the map.
        ["carpet"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#6a5b7d");
            for (float i = 0; i < g.W; i += 4) Line(g, i, 0, i, g.H, "#7d6d92", 2, 0.22f);
            for (float i = 0; i < g.H; i += 4) Line(g, 0, i, g.W, i, "#5b4d6d", 2, 0.22f);
            Grain(g, new[] { "#7a6a8e", "#5c4e6e" }, 1400, 0.18f, 14);
        } },

        // Security: the only magenta floor on Polus. The atlas measures #7e3f63 and it looks far
        // brighter there because the camera bank's own glow is painted over it. Drawn darker: a
        // texture is albedo, and at the map's own value the room came out neon.
        ["carpetPink"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#613048");
            for (float i = 0; i < g.W; i += 4) Line(g, i, 0, i, g.H, "#6f3a54", 2, 0.20f);
            for (float i = 0; i < g.H; i += 4) Line(g, 0, i, g.W, i, "#52273c", 2, 0.20f);
            Grain(g, new[] { "#6c3752", "#54293e" }, 1400, 0.18f, 15);
        } },

        // Laboratory / Decontamination: pale ceramic, wide grout. Drawn below the map's #c1d2d2 for
        // the albedo reason above - at that value the laboratory was a lightbox.
        ["tileWhite"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#8b9698");
            Rect(g, 3, 3, g.W - 6, g.H - 6, "#a6b2b4");
            Grain(g, new[] { "#b6c1c2", "#98a3a5" }, 500, 0.12f, 16);
        } },

        // The dropship's cargo deck: grey slabs with a wide ochre joint. The one floor on Polus with
        // a coloured grout, and what makes the hold read as a ship rather than a store room.
        ["shipDeck"] = new Spec { Unit = 0.80f, Draw = g => {
            Fill(g, "#9a7f3e");
            Rect(g, 4, 4, g.W - 8, g.H - 8, "#5d6670");
            Rect(g, 7, 7, g.W - 14, g.H - 14, "#69727c");
            Grain(g, new[] { "#727b85", "#5a636d" }, 500, 0.12f, 17);
        } },

        // The dropship's ramp: pale plate with fine ribs across it, for grip.
        ["rampTread"] = new Spec { Unit = 0.55f, Draw = g => {
            Fill(g, "#a3a9ad");
            for (float i = 0; i < g.H; i += 7) Line(g, 0, i, g.W, i, "#868c90", 2, 0.55f);
            Line(g, 0, 1, g.W, 1, "#c2c8cc", 2);
            Grain(g, new[] { "#b0b6ba", "#949a9e" }, 400, 0.12f, 18);
        } },

        // Storage / Weapons: riveted steel deck plate.
        ["metalDeck"] = new Spec { Unit = 0.7f, Draw = g => {
            Fill(g, "#333c42");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#434c4f");
            foreach (var (x, y) in new[] { (7f, 7f), (g.W - 9f, 7f), (7f, g.H - 9f), (g.W - 9f, g.H - 9f) })
                g.FillEllipse(x, y, 2.4f, 2.4f, C("#2a3236"));
            Grain(g, new[] { "#4e5960", "#2c3438" }, 600, 0.14f, 19);
        } },

        // Grated walkway, the metal that rings the outdoor bridges.
        ["grate"] = new Spec { Unit = 0.4f, Draw = g => {
            Fill(g, "#2c333c");
            for (float i = 0; i < g.W; i += 8) Rect(g, i, 0, 5, g.H, "#59626f");
            for (float i = 0; i < g.H; i += 16) Rect(g, 0, i, g.W, 3, "#3e4650");
        } },

        // Storage's annexe: pale scuffed plastic sheet. The survey calls its footstep material
        // "plastic", which is the only place on the map that does.
        ["plasticFloor"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#8595b4");
            var rnd = new Rng(20);
            for (int i = 0; i < 7; i++)
                Rect(g, rnd.Next() * g.W, 0, 3 + rnd.Next() * 9, g.H, i % 2 == 1 ? "#a3b0c9" : "#6f7d9f", 0.35f);
            Grain(g, new[] { "#9aa8c4", "#71809f" }, 700, 0.16f, 21);
        } },

        // ---------------------------------------------------------------- walls
        // Corrugated sheet: the outside skin of Storage's annexe, ribs running up the wall.
        ["corrugated"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#5e6d6d");
            for (float i = 0; i < g.W; i += 11)
            {
                Rect(g, i, 0, 4, g.H, "#314141");
                Rect(g, i + 5, 0, 2, g.H, "#7d8c8c");
            }
            Grain(g, new[] { "#536262", "#6d7c7c" }, 400, 0.12f, 22);
        } },

        // The standard interior wall of Polus: cream panels with a horizontal seam and a joint.
        ["panelCream"] = new Spec { Unit = 1.1f, Draw = g => {
            Fill(g, "#bdbea5");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#9b9c84", 2);
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#a5a68e", 2);
            Line(g, 0, g.H * 0.5f + 3, g.W, g.H * 0.5f + 3, "#d3d4bc", 2, 0.5f);
            Grain(g, new[] { "#c9cab2", "#adae95" }, 500, 0.12f, 23);
        } },

        // Structural steel: the outside of every building, and the frames around the doors.
        ["panelSteel"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#3c4a4a");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#2b3636", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#2b3636", 3);
            Line(g, 2, 2, g.W - 3, 2, "#4d5d5d", 2);
            Grain(g, new[] { "#465555", "#33403f" }, 400, 0.12f, 24);
        } },

        // The ceiling: bright panels with a seam, because a dark ceiling swallows the whole room.
        ["ceilingPanel"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#b4b9bd");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#8e9498", 3);
            Line(g, 0, g.H - 1, g.W, g.H - 1, "#8e9498", 3);
            Grain(g, new[] { "#c2c7cb", "#a3a8ac" }, 400, 0.1f, 25);
        } },

        // Office east half: the patterned wallpaper behind the screens. The map draws it light with
        // a darker curl - reading it the other way round turned the room olive green.
        ["wallpaper"] = new Spec { Unit = 0.55f, Draw = g => {
            Fill(g, "#b9bd9c");
            // The prototype's quadratic curves, as three chords each. At 128 pixels the difference
            // is under a pixel and Canvas2D has no curve primitive.
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? 0 : g.W;
                float mx = g.W * 0.5f;
                Line(g, x, 0, (x + mx) * 0.5f, g.H * 0.25f, "#8b9075", 3);
                Line(g, (x + mx) * 0.5f, g.H * 0.25f, (x + mx) * 0.5f, g.H * 0.75f, "#8b9075", 3);
                Line(g, (x + mx) * 0.5f, g.H * 0.75f, x, g.H, "#8b9075", 3);
            }
            Grain(g, new[] { "#c3c7a6", "#adb190" }, 500, 0.12f, 26);
        } },

        // THE OUTSIDE OF A POLUS BUILDING AT GROUND LEVEL. Everywhere the map shows the south face
        // of an outer wall it draws the same thing: rough grey blockwork with icicles hanging off
        // the courses. Using the structural steel for it made every building look like a shipping
        // container standing on the ice.
        ["rockWall"] = new Spec { Unit = 1.3f, Draw = g => {
            Fill(g, "#7c8085");
            for (int r = 0; r < 3; r++)
            {
                float y = g.H * (r + 1) / 3f;
                Line(g, 0, y, g.W, y, "#5b6065", 2);
                float off = r % 2 == 1 ? 0.5f : 0f;
                for (int c = 0; c < 3; c++)
                {
                    float x = g.W * ((c + off) / 3f);
                    Line(g, x, y - g.H / 3f, x, y, "#5b6065", 2);
                }
            }
            for (int i = 0; i < 7; i++)
            {
                float x = 6 + i * (g.W - 12) / 6f + (i % 2) * 4;
                float l = 8 + (i * 37) % 22;
                g.FillQuad(x - 3, 0, x + 3, 0, x, l, x, l, C("#dfeaf2"));
            }
            Grain(g, new[] { "#8a8e93", "#6c7075" }, 500, 0.14f, 27);
        } },

        // The dark red plinth the blockwork stands on.
        ["plinthRed"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#7a2f34");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#5f2429", 2);
            Grain(g, new[] { "#8a373c", "#69282d" }, 300, 0.14f, 28);
        } },

        // Big diamond-mesh floor plate: the one west of Admin, and the walkway grids.
        ["meshPlate"] = new Spec { Unit = 0.75f, Draw = g => {
            Fill(g, "#3d4a5c");
            for (float i = -g.H; i < g.W + g.H; i += 16)
            {
                Line(g, i, 0, i + g.H, g.H, "#5a6b80", 3, 0.8f);
                Line(g, i, g.H, i + g.H, 0, "#5a6b80", 3, 0.8f);
            }
            Grain(g, new[] { "#4a586c", "#334053" }, 400, 0.14f, 29);
        } },

        // THE INSIDE OF A POD - Specimens and the tube heads: smooth pale grey-blue sheets with a
        // butt joint every metre, quite unlike the cream panelling of the huts on the west side.
        ["podPanel"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#7b848c");
            Rect(g, 0, 2, g.W, g.H - 8, "#868f97");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#636c74", 3);
            Line(g, 0, g.H - 5, g.W, g.H - 5, "#636c74", 2);
            Line(g, 0, 3, g.W, 3, "#a0a9b0", 3, 0.55f);
            Grain(g, new[] { "#8b949b", "#727b83" }, 400, 0.12f, 30);
        } },

        // Tiled wall, half height, of the laboratory and the wet rooms.
        ["tiledWall"] = new Spec { Unit = 0.45f, Draw = g => {
            Fill(g, "#7d8d92");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#a3b1b4");
            Grain(g, new[] { "#b2bec1", "#94a1a4" }, 400, 0.1f, 31);
        } },

        // ---------------------------------------------------------------- objects
        ["crateGreen"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#4c6b4a");
            g.StrokeRoundRect(3, 3, g.W - 6, g.H - 6, 0, 6, C("#3a5439"));
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#3a5439", 4);
            Grain(g, new[] { "#597956", "#405f3f" }, 400, 0.14f, 32);
        } },

        ["plasticWhite"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#d8dbd2");
            Grain(g, new[] { "#e6e8e1", "#c4c7bd" }, 400, 0.12f, 33);
        } },

        ["darkTrim"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#2c3336");
            Grain(g, new[] { "#3a4245", "#232a2c" }, 300, 0.15f, 34);
        } },

        ["woodDark"] = new Spec { Unit = 0.7f, Draw = g => {
            Fill(g, "#7e6852");
            for (int i = 0; i < 3; i++)
                Line(g, 0, g.H * (i + 0.5f) / 3f, g.W, g.H * (i + 0.5f) / 3f, "#69553f", 2);
            Grain(g, new[] { "#8d7660", "#6b5744" }, 400, 0.16f, 35);
        } },

        // THE LIQUID IN A SPECIMEN TUBE. Specimens' whole point is the row of lit tubes on the
        // bench, and the map has a separate sprite per tube for the bubbles climbing through them
        // (samplebubs1..5) - so the bubbles are not decoration, they are the object.
        ["brewGreen"] = Brew("#5aa85e", "#4a8f4e", "#79c87c", "#d8f4d0", 41),
        ["brewCyan"] = Brew("#5f9fb8", "#4d88a0", "#87c3d8", "#e2f4fa", 42),
        ["brewViolet"] = Brew("#8f56b4", "#7a48a0", "#b06fd0", "#f0dcfa", 43),

        // ---------------------------------------------------------------- the planet
        ["dust"] = new Spec { Unit = 7.0f, Draw = g => {
            Fill(g, "#745387");
            var rnd = new Rng(51);
            for (int i = 0; i < 26; i++)
            {
                float bx = rnd.Next() * g.W, by = rnd.Next() * g.H;
                float rx = 10 + rnd.Next() * 26, ry = 8 + rnd.Next() * 18;
                WrapEllipse(g, bx, by, rx, ry, i % 2 == 1 ? "#8a67a1" : "#634676", 0.10f);
            }
            Grain(g, new[] { "#826295", "#674a79", "#82619a" }, 2600, 0.22f, 52);
        } },

        // A TRODDEN PATH in the dust: the same regolith with the loose top layer walked off it,
        // measured in the atlas at #a37ba8 against the plain's #745387. Drawn as dust with the big
        // blotches removed and a third of the speckle - what is left reads as WORN rather than as a
        // strip of a different rock.
        ["dustPath"] = new Spec { Unit = 9.0f, Draw = g => {
            Fill(g, "#a37ba8");
            var rnd = new Rng(53);
            for (int i = 0; i < 14; i++)
            {
                float bx = rnd.Next() * g.W, by = rnd.Next() * g.H;
                WrapEllipse(g, bx, by, 16 + rnd.Next() * 34, 3 + rnd.Next() * 7,
                            i % 2 == 1 ? "#b48cb8" : "#946d9a", 0.07f);
            }
            Grain(g, new[] { "#ad86b2", "#96709c" }, 900, 0.12f, 54);
        } },

        // THE LAVA in the gorge east of the Laboratory: the brightest thing on the map, and the one
        // surface that gives off light instead of taking it. Painted darker than it looks on
        // purpose - it is emissive as well as lit, and at the map's values it came out as a
        // lightbox rather than molten rock.
        ["lava"] = new Spec { Unit = 2.6f, Emissive = 0.34f, Draw = g => {
            Fill(g, "#cf450a");
            var rnd = new Rng(55);
            for (int i = 0; i < 18; i++)
                WrapEllipse(g, rnd.Next() * g.W, rnd.Next() * g.H, 8 + rnd.Next() * 30,
                            5 + rnd.Next() * 16, i % 4 != 0 ? "#e8791a" : "#f6b626", 0.45f);
            for (int i = 0; i < 18; i++)
                WrapEllipse(g, rnd.Next() * g.W, rnd.Next() * g.H, 7 + rnd.Next() * 16,
                            4 + rnd.Next() * 10, i % 3 != 0 ? "#521907" : "#3a1105", 0.55f);
        } },

        // The planet's bedrock, the stuff the gorge is cut into: the same violet as the dust but
        // darker and layered, so a cliff face reads as strata rather than as a painted wall.
        ["bedrock"] = new Spec { Unit = 1.6f, Draw = g => {
            Fill(g, "#5b4169");
            for (int i = 0; i < 5; i++)
            {
                float y = g.H * (i + 0.5f) / 5f;
                Line(g, 0, y, g.W, y, "#4a3457", 3);
                Line(g, 0, y + 4, g.W, y + 4, "#6d4f7d", 2, 0.4f);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = 8 + i * (g.W - 16) / 5f;
                float l = 6 + (i * 29) % 18;
                g.FillQuad(x - 4, 0, x + 4, 0, x, l, x, l, C("#a897b4"));
            }
            Grain(g, new[] { "#66497a", "#4d3859" }, 900, 0.2f, 56);
        } },

        ["snow"] = new Spec { Unit = 3.0f, Draw = g => {
            Fill(g, "#cddfec");
            Grain(g, new[] { "#e4eff7", "#b6cbdc" }, 1800, 0.35f, 57);
        } },

        // ================================================================================
        // Skeld - colours read off Nightfall/skeldship_atlas.png (3087x2122, calibrated by
        // skeldship_atlas.txt). Every entry below cites the world coordinate its colour was read
        // at. Where a Polus material already fit a Skeld room (Storage's floor reads as shipDeck,
        // give or take a texel) no new entry was added - see the Phase 2 report for that list.
        // ---------------------------------------------------------------- Skeld floors
        // Cafeteria: the octagonal mess floor, a 45-degree diamond checker in warm sandy beige.
        // Nothing on Polus uses a diamond check, so this is the one new drawing shape in the set -
        // see DiamondFloor below. Atlas reads #98a090 (dark diamond) and #b0b0a0 (light diamond) at
        // world (-4.75, 0.65).
        ["tileCafeteria"] = DiamondFloor("#767e6f", "#8f9082", 0.6f, 61),

        // Every corridor segment on the ship (the seven Hallway rooms plus the tiny one by
        // Cafeteria's east door): a cool grey-blue plate with the same fine horizontal ribbing as
        // Polus' rampTread, just a different colour and a shallower rib - the atlas shows barely
        // any contrast between rib and plate. Measured #80a0a8 at world (-0.50, -6.50), in the
        // hallway south of Admin; the same tone reads at every other corridor sampled.
        ["tileCorridor"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#5d7d85");
            for (float i = 0; i < g.H; i += 7) Line(g, 0, i, g.W, i, "#4f6d74", 2, 0.4f);
            Line(g, 0, 1, g.W, 1, "#6f929a", 2, 0.5f);
            Grain(g, new[] { "#6a8b93", "#527076" }, 400, 0.1f, 62);
        } },

        // Admin: a dusty rose-mauve carpet, the same weave as Polus' carpet/carpetPink but a
        // brownish red neither of those two mixes to. Measured #78515a at world (6.59, -8.60).
        ["carpetMauve"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#5e3f47");
            for (float i = 0; i < g.W; i += 4) Line(g, i, 0, i, g.H, "#6f4a53", 2, 0.20f);
            for (float i = 0; i < g.H; i += 4) Line(g, 0, i, g.W, i, "#50353d", 2, 0.20f);
            Grain(g, new[] { "#684750", "#52363e" }, 1400, 0.18f, 63);
        } },

        // Nav: a plain slate blue-grey deck, cooler and greyer than Polus' tileBlue. Measured
        // #6e8a9a at world (16.82, -4.53).
        ["tileNav"] = new Spec { Unit = 0.55f, Draw = g => {
            Fill(g, "#5f7583");
            Rect(g, 3, 3, g.W - 6, g.H - 6, "#6e8a9a");
            Grain(g, new[] { "#7a96a4", "#5c7684" }, 500, 0.12f, 64);
        } },

        // Weapons and Shields: the same pale cream/tan console-room floor in both rooms. Measured
        // #c6c6aa at world (9.05, 0.30) in Weapons; Shields reads the same tone (#c0c0a8 dominant
        // across the room) at its own console pad.
        ["tileConsole"] = new Spec { Unit = 0.85f, Draw = g => {
            Fill(g, "#a8a890");
            Rect(g, 3, 3, g.W - 6, g.H - 6, "#c0c0a8");
            Grain(g, new[] { "#cccab4", "#a8a894" }, 400, 0.12f, 65);
        } },

        // LifeSupp: a muted sage-teal deck, ribbed like tileCorridor above but its own colour.
        // Measured #688177 at world (5.88, -3.80).
        ["gratedTeal"] = new Spec { Unit = 0.55f, Draw = g => {
            Fill(g, "#708078");
            for (float i = 0; i < g.H; i += 7) Line(g, 0, i, g.W, i, "#5e6e66", 2, 0.4f);
            Line(g, 0, 1, g.W, 1, "#869890", 2, 0.5f);
            Grain(g, new[] { "#7c8c84", "#647266" }, 400, 0.12f, 66);
        } },

        // Electrical: a flat olive-khaki floor under the wire clutter. The room is criss-crossed
        // with wire sprites, so no single texel is clean; #585848 is the dominant colour over the
        // room's floor area (45% of samples), centred near world (-7.5, -10.1).
        ["floorOlive"] = new Spec { Unit = 0.85f, Draw = g => {
            Fill(g, "#4b4b3d");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#585848");
            Grain(g, new[] { "#666a4c", "#4b4e38" }, 400, 0.12f, 67);
        } },

        // Reactor: a violet-blue tiled floor, quartered by a seam cross the way MedBay's tile is
        // (see tileMedBay). Measured #8586a6 (panel) at world (-21.37, -6.74); the seam colour
        // reuses the room's own wall tone, #4f496b (see wallReactorPanel), which reads as the same
        // dark violet in the floor's grout.
        ["tileReactor"] = new Spec { Unit = 0.7f, Draw = g => {
            Fill(g, "#2a2740");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#464360");
            Line(g, g.W * 0.5f, 2, g.W * 0.5f, g.H - 2, "#393554", 2, 0.5f);
            Line(g, 2, g.H * 0.5f, g.W - 2, g.H * 0.5f, "#393554", 2, 0.5f);
            Grain(g, new[] { "#4e4a6a", "#33304c" }, 400, 0.12f, 68);
        } },

        // Lower and Upper Engine: the same taupe-grey rivet deck in both rooms - riveted like
        // metalDeck but a warmer, browner grey. Measured #6e6963 at world (-15.89, -10.03) in Lower
        // Engine; Upper Engine's floor reads the same tone.
        ["tileEngine"] = new Spec { Unit = 0.7f, Draw = g => {
            Fill(g, "#57534d");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#6e6963");
            foreach (var (x, y) in new[] { (7f, 7f), (g.W - 9f, 7f), (7f, g.H - 9f), (g.W - 9f, g.H - 9f) })
                g.FillEllipse(x, y, 2.4f, 2.4f, C("#413e39"));
            Grain(g, new[] { "#7a756e", "#4c4842" }, 500, 0.12f, 69);
        } },

        // Comms: a flat blue-teal floor. Measured #577881 at world (2.39, -16.89).
        ["tileComms"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#4a666e");
            Rect(g, 3, 3, g.W - 6, g.H - 6, "#577881");
            Grain(g, new[] { "#658793", "#456670" }, 400, 0.12f, 70);
        } },

        // Security: a saturated forest-green tile, quartered by a faint grid the way the room's
        // camera-bank floor is drawn on the map. Measured #306858 at world (-13.35, -4.39) - 70% of
        // the room's floor pixels read this one value. A lighter #5d9279 shows up near world
        // (-13.67, -3.76), but that is the console's own glow falling across the tile, not a second
        // material - it is not reproduced here.
        ["tileSecurity"] = new Spec { Unit = 0.55f, Draw = g => {
            Fill(g, "#224c40");
            for (int i = 1; i < 3; i++)
            {
                float x = g.W * i / 3f; Line(g, x, 0, x, g.H, "#1c4237", 2, 0.6f);
                float y = g.H * i / 3f; Line(g, 0, y, g.W, y, "#1c4237", 2, 0.6f);
            }
            Grain(g, new[] { "#2a5c4e", "#193b31" }, 400, 0.12f, 71);
        } },

        // MedBay: a blue-grey clinical tile, quartered by a seam cross - the atlas shows four large
        // panels meeting at the middle of the room rather than a fine repeat. Measured #6d8086 at
        // world (-9.43, -4.02).
        ["tileMedBay"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#6d8086");
            Line(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#57686d", 2);
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#57686d", 2);
            Grain(g, new[] { "#7c8f95", "#5e7176" }, 400, 0.1f, 72);
        } },

        // ---------------------------------------------------------------- Skeld walls
        // The console backdrop in Admin (and, from the same reading, Weapons): a deep wine-red
        // panel behind the monitor row, unlike anything on Polus' cooler palette. Measured #8c223c
        // at world (5.75, -6.55) in Admin.
        ["wallMaroon"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#8c223c");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#6e1a2f", 2);
            Grain(g, new[] { "#9c2c46", "#7a1d33" }, 300, 0.14f, 73);
        } },

        // Reactor's console wall: a smooth lavender-violet panel, built like Polus' podPanel (a
        // butt-jointed sheet with a bright top bead) but in a colour podPanel doesn't have. Measured
        // #4f496b at world (-20.13, -3.70).
        ["wallReactorPanel"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#2a2740");
            Rect(g, 0, 2, g.W, g.H - 8, "#332f4a");
            Line(g, g.W - 1, 0, g.W - 1, g.H, "#282442", 3);
            Line(g, 0, g.H - 5, g.W, g.H - 5, "#282442", 2);
            Line(g, 0, 3, g.W, 3, "#4e4a6c", 3, 0.55f);
            Grain(g, new[] { "#433e60", "#2c2842" }, 400, 0.12f, 74);
        } },

        // The Skeld's outer hull, #3d4a4a everywhere the drawing shows the ship from outside -
        // between rooms, around every cap, behind every window. Plated in long courses with a rare
        // rivet, so a wall of it reads as a hull and not as a flat colour. src/surfaces.js.
        ["hullPlate"] = new Spec { Unit = 1.1f, Draw = g => {
            Fill(g, "#3d4a4a");
            for (float y = 0; y < g.H; y += 34) {
                Line(g, 0, y, g.W, y, "#313c3c", 2);
                Line(g, 0, y + 2, g.W, y + 2, "#4a5858", 1, 0.5f);
            }
            for (float x = 0; x < g.W; x += 64) Line(g, x, 0, x, g.H, "#354141", 1);
            for (int i = 0; i < 8; i++)
                g.FillEllipse((i * 53 + 17) % g.W, (i * 91 + 23) % g.H, 1.6f, 1.6f, C("#2d3737"));
            Grain(g, new[] { "#465454", "#344040" }, 500, 0.10f, 102);
        } },

        // The Cafeteria's own wall: a teal panel with a wine-red wainscot along its foot, measured
        // (not guessed) off the atlas - see src/surfaces.js for the scanline reading. `Unit` is the
        // full wall height so the drawing maps once from floor to ceiling.
        ["wallCafPanel"] = new Spec { Unit = 1.45f, Draw = g => {
            Fill(g, "#528694");
            for (float x = 0; x < g.W; x += 42) Line(g, x, 0, x, g.H, "#436f7c", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#5f97a5", 2);
            Rect(g, 0, g.H * 0.70f, g.W, g.H * 0.30f, "#89455a");
            Line(g, 0, g.H * 0.70f, g.W, g.H * 0.70f, "#6e3648", 2);
            for (float x = 21; x < g.W; x += 42) Line(g, x, g.H * 0.70f, x, g.H, "#75394c", 2, 0.4f);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#313031", 3);
            Grain(g, new[] { "#5d909e", "#477682" }, 500, 0.10f, 103);
        } },

        // ---------------------------------------------------------------- Skeld nw cluster
        // The cross hallway (Reactor / Upper Engine / Lower Engine / Security): big taupe deck
        // plates, no rivets. Measured #6b6963 at world (-16.90,-7.20). surfaces_skeld_nw.js.
        ["tileCrossHall"] = new Spec { Unit = 0.95f, Draw = g => {
            Fill(g, "#5b5955");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#6b6963");
            Line(g, 0, 2, g.W, 2, "#7c7a72", 2, 0.55f);
            Line(g, 2, 0, 2, g.H, "#7c7a72", 2, 0.55f);
            Grain(g, new[] { "#75736b", "#565450" }, 450, 0.12f, 75);
        } },

        // MedBay's central aisle: large stone slabs, unlike the fine clinical tile either side of
        // it. Measured #849a9c at world (-9.40,-3.00). surfaces_skeld_nw.js.
        ["tileMedAisle"] = new Spec { Unit = 0.9f, Draw = g => {
            Fill(g, "#6f8388");
            Rect(g, 3, 3, g.W - 6, g.H - 6, "#7d9297");
            Line(g, g.W * 0.5f, 3, g.W * 0.5f, g.H - 3, "#6c7e84", 2, 0.5f);
            Grain(g, new[] { "#8a9ea1", "#66787d" }, 400, 0.12f, 76);
        } },

        // The warm khaki-grey panelling of Upper Engine and the cross hallway, with its olive
        // equipment band. Measured #8c877b at world y=3.35, x=-17.4. surfaces_skeld_nw.js.
        ["wallEngineKhaki"] = new Spec { Unit = 1.5f, Draw = g => {
            Fill(g, "#8c877b");
            for (float x = 0; x < g.W; x += 38) Line(g, x, 0, x, g.H, "#746f65", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#a09a8c", 2);
            Rect(g, 0, g.H * 0.44f, g.W, g.H * 0.30f, "#5d533b");
            for (float x = 19; x < g.W; x += 38)
                Line(g, x, g.H * 0.44f, x, g.H * 0.74f, "#4b432f", 2, 0.45f);
            Line(g, 0, g.H * 0.44f, g.W, g.H * 0.44f, "#4b432f", 2);
            Line(g, 0, g.H * 0.74f, g.W, g.H * 0.74f, "#4b432f", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#2a3031", 3);
            Grain(g, new[] { "#948f82", "#7b766c" }, 500, 0.10f, 77);
        } },

        // The north hallway's own wall, and every other corridor's on this side: cool teal panel,
        // a dusty-red service line and a lit lozenge strip along the foot. Painted well under the
        // measured #528694/#b0245e - this panel covers half the ship's corridors and rendered neon
        // at the atlas value (src/surfaces_skeld_nw.js' own note). surfaces_skeld_nw.js.
        ["wallHallTeal"] = new Spec { Unit = 1.5f, Draw = g => {
            Fill(g, "#375963");
            Rect(g, 0, 4, g.W, g.H - 16, "#3e6470");
            for (float x = 0; x < g.W; x += 34) Line(g, x, 0, x, g.H, "#32525c", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#4a7885", 2);
            Line(g, 0, g.H * 0.40f, g.W, g.H * 0.40f, "#7e2444", 2);
            Rect(g, 0, g.H - 13, g.W, 13, "#1b2a2f");
            for (float x = 4; x < g.W; x += 22) Rect(g, x, g.H - 10, 14, 5, "#9fc9d4");
            Grain(g, new[] { "#456f7c", "#34535c" }, 400, 0.10f, 78);
        } },

        // MedBay's wall: ice-pale clinical panel over a blue dado, with the medicine-cabinet crosses
        // painted into the band. Measured at world x=-7.5. surfaces_skeld_nw.js.
        ["wallMedPanel"] = new Spec { Unit = 1.5f, Draw = g => {
            Fill(g, "#9db9c1");
            for (float x = 0; x < g.W; x += 32) Line(g, x, 0, x, g.H, "#8aa5ad", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#b5cfd6", 2);
            Rect(g, 0, g.H * 0.30f, g.W, g.H * 0.26f, "#2f7099");
            for (float x = 16; x < g.W; x += 44) {
                Rect(g, x + 8, g.H * 0.36f, 8, 20, "#cfe6ef");
                Rect(g, x + 2, g.H * 0.36f + 6, 20, 8, "#cfe6ef");
            }
            Rect(g, 0, g.H * 0.72f, g.W, g.H * 0.28f, "#39596b");
            Line(g, 0, g.H * 0.72f, g.W, g.H * 0.72f, "#2b4453", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#1e2c33", 3);
            Grain(g, new[] { "#a7c2ca", "#8ea9b1" }, 400, 0.10f, 79);
        } },

        // Security's wall: pale sage-grey the camera bank is set into, over the room's own dark
        // green dado. Painted two steps under the measured #89a197 - the 2.5-by-4.5 room came out
        // as a bathroom at the atlas value (playtest note in surfaces_skeld_nw.js).
        ["wallSecPanel"] = new Spec { Unit = 1.5f, Draw = g => {
            Fill(g, "#4a615a");
            for (float x = 0; x < g.W; x += 36) Line(g, x, 0, x, g.H, "#3e534d", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#587068", 2);
            Rect(g, 0, g.H * 0.74f, g.W, g.H * 0.26f, "#3b524b");
            Line(g, 0, g.H * 0.74f, g.W, g.H * 0.74f, "#2e433d", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#1a2622", 3);
            Grain(g, new[] { "#526a63", "#405953" }, 400, 0.10f, 80);
        } },

        // The engine's own housing: dusty salmon-red plate, ribbed and riveted much harder than a
        // flat fill - the block's sides are the biggest single faces on the ship. surfaces_skeld_nw.js.
        ["engineHousingLeft"] = new Spec { Unit = 0.85f, Draw = g => {
            Fill(g, "#54352e");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#64423a");
            for (float y = 8; y < g.H; y += 14) {
                Line(g, 2, y, g.W - 2, y, "#4a2d27", 2);
                Line(g, 2, y + 2, g.W - 2, y + 2, "#7a5247", 1, 0.5f);
            }
            for (float x = 24; x < g.W; x += 42) Line(g, x, 2, x, g.H - 2, "#4a2d27", 2);
            for (float x = 10; x < g.W; x += 21) {
                g.FillEllipse(x, 11, 1.8f, 1.8f, C("#3d241f"));
                g.FillEllipse(x + 10, g.H - 11, 1.8f, 1.8f, C("#3d241f"));
            }
            Grain(g, new[] { "#6f4a3f", "#472b25" }, 450, 0.12f, 81);
        } },

        // The reactor core's shell: brushed steel with vertical light slots between its ribs -
        // the one object on this half of the ship that lights itself (used with an emissive collar
        // over it, so no Emissive here). surfaces_skeld_nw.js.
        ["reactorCoreShell"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#8b90a8");
            for (float x = 0; x < g.W; x += 16) Line(g, x, 0, x, g.H, "#767c96", 2, 0.5f);
            for (float x = 6; x < g.W; x += 16) Rect(g, x, 6, 6, g.H - 12, "#b7bbce");
            Line(g, 0, 2, g.W, 2, "#c9cddd", 2);
            Grain(g, new[] { "#9ba0b6", "#7d8298" }, 300, 0.10f, 82);
        } },

        // THE MEDBAY SCAN PAD, the raised disc a crewmate stands on. Faintly emissive rather than
        // lit - a scan surface waiting for someone. 0.16 against Polus' lava (0.34, molten rock):
        // at 0.5 the pad blew out white under the scanner lamp two units above it. The JS entry's
        // own emissive colour (#2a6f47) is a shade of the albedo and not modelled separately here -
        // only the intensity feeds AreaSurfaces.EmissiveOf(). surfaces_skeld_nw.js.
        ["medScanPad"] = new Spec { Unit = 1.3f, Emissive = 0.16f, Draw = g => {
            Fill(g, "#3f8a62");
            for (int i = 0; i < 3; i++) {
                float r = g.W * (0.16f + i * 0.16f);
                g.StrokeEllipse(g.W * 0.5f, g.H * 0.5f, r, r, 2f, C("#8fdcae"), 0.45f);
            }
            Line(g, g.W * 0.16f, g.H * 0.5f, g.W * 0.84f, g.H * 0.5f, "#cdf2dc", 3);
            Line(g, g.W * 0.5f, g.H * 0.16f, g.W * 0.5f, g.H * 0.84f, "#cdf2dc", 3);
            Grain(g, new[] { "#5eae82", "#3f8a62" }, 250, 0.10f, 83);
        } },

        // ---------------------------------------------------------------- Skeld sw cluster
        // Lower Engine's wall: warm taupe panel in tall butt-jointed courses. Measured #8c877b at
        // world y=-9.20. surfaces_skeld_sw.js.
        ["wallLowerEngineTaupe"] = new Spec { Unit = 1.25f, Draw = g => {
            Fill(g, "#625d54");
            for (float x = 0; x < g.W; x += 38) Line(g, x, 0, x, g.H, "#514c45", 2, 0.55f);
            Line(g, 0, 3, g.W, 3, "#736d63", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#2d2c2b", 4);
            Grain(g, new[] { "#6b655c", "#565048" }, 500, 0.11f, 84);
        } },

        // Electrical's wall: grey-olive sheet, the same family as its floor but lighter. Measured
        // #8e907c at world (-9.50,-7.40). surfaces_skeld_sw.js.
        ["wallElecOlive"] = new Spec { Unit = 1.15f, Draw = g => {
            Fill(g, "#64664f");
            Rect(g, 0, 2, g.W, g.H * 0.55f, "#6c6e57");
            for (float x = 0; x < g.W; x += 40) Line(g, x, 0, x, g.H, "#51533f", 2, 0.5f);
            Line(g, 0, g.H * 0.55f, g.W, g.H * 0.55f, "#51533f", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#2b2c26", 4);
            Grain(g, new[] { "#6e7059", "#585a46" }, 450, 0.11f, 85);
        } },

        // Storage's wall: pale grey plate, the brightest interior surface in this cluster. Measured
        // #84868c at world (-2.60,-8.70). surfaces_skeld_sw.js.
        ["wallStoragePanel"] = new Spec { Unit = 1.10f, Draw = g => {
            Fill(g, "#5b5e64");
            for (float x = 0; x < g.W; x += 44) Line(g, x, 0, x, g.H, "#4b4e54", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#686c72", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#2a2e31", 4);
            Grain(g, new[] { "#797c82", "#63666c" }, 500, 0.11f, 86);
        } },

        // Storage's cargo deck: big blue-grey plates in a mortar of ochre grout, quartered because
        // a surface has one `Unit` and the drawn grid is not square (see surfaces_skeld_sw.js).
        // Plate #5a656b, grout #84795a in the atlas; both painted two steps down.
        ["deckStorage"] = new Spec { Unit = 1.53f, Draw = g => {
            Fill(g, "#565144");
            for (int j = 0; j < 2; j++) Rect(g, 3, j * g.H / 2f + 3, g.W - 6, g.H / 2f - 6, "#4d5760");
            for (int j = 0; j < 2; j++) Rect(g, 6, j * g.H / 2f + 6, g.W - 12, g.H / 2f - 12, "#566169");
            Grain(g, new[] { "#5d686f", "#48525a" }, 600, 0.12f, 87);
        } },

        // Lower Engine's thruster ring: the crescent of cyan light at the machine's east end - the
        // only surface in this cluster that gives off light. The drawing is its own emissive map
        // (like Polus' lava), so the streaks glow and the dark body between them does not.
        ["lowerEngineGlow"] = new Spec { Unit = 1.50f, Emissive = 1.0f, Draw = g => {
            Fill(g, "#123a44");
            for (int i = 0; i < 5; i++) {
                float y = g.H * (0.10f + i * 0.20f);
                Rect(g, g.W * 0.12f, y, g.W * 0.76f, g.H * 0.09f, "#7fe8ff");
            }
            for (int i = 0; i < 5; i++)
                Rect(g, g.W * 0.20f, g.H * (0.11f + i * 0.20f), g.W * 0.42f, g.H * 0.05f, "#dcfbff");
        } },

        // The floor light bar of the Electrical hallway: a dark band at the foot of every corridor
        // wall with a row of pale lozenges in it - the only light the corridor's own drawing shows.
        ["ehallFloorLight"] = new Spec { Unit = 0.9f, Emissive = 0.85f, Draw = g => {
            Fill(g, "#22383c");
            g.FillEllipse(g.W * 0.5f, g.H * 0.5f, g.W * 0.34f, g.H * 0.30f, C("#c6f3ff"));
        } },

        // The garbage airlock's warning paint: a dark base with amber chevrons over it, so the
        // object reads dark with warning stripes rather than as a slab of yellow. `Unit` is small
        // (0.28) so the bars come out the size the map actually draws them, not four times as wide.
        ["hazardStripe"] = new Spec { Unit = 0.28f, Draw = g => {
            Fill(g, "#1c1f23");
            for (float x = -g.H; x < g.W + g.H; x += 26)
                g.FillQuad(x, g.H, x + 13, g.H, x + 13 + g.H, 0, x + g.H, 0, C("#8f7018"));
            Grain(g, new[] { "#9c7d26", "#25292e" }, 300, 0.10f, 88);
        } },

        // The chute floor at the bottom of the airlock: a coarse dark grating one looks straight
        // through into space. Measured #3a4145 at world (0.10,-18.60). surfaces_skeld_sw.js.
        ["chuteGrate"] = new Spec { Unit = 0.62f, Draw = g => {
            Fill(g, "#14181b");
            for (int i = 0; i < 5; i++) Rect(g, 0, g.H * (0.04f + i * 0.20f), g.W, g.H * 0.11f, "#3a4145");
            Rect(g, 0, 0, 4, g.H, "#2a3033");
            Rect(g, g.W - 4, 0, 4, g.H, "#2a3033");
            Grain(g, new[] { "#434b4f", "#1b1f22" }, 300, 0.12f, 89);
        } },

        // The Skeld's shipping crate: a colder, greener box than Polus' crateGreen. Measured
        // #3f615a at world (-1.70,-12.40). surfaces_skeld_sw.js.
        ["crateStorageTeal"] = new Spec { Unit = 0.6f, Draw = g => {
            Fill(g, "#37564f");
            g.StrokeRoundRect(3, 3, g.W - 6, g.H - 6, 0, 6, C("#2a423d"));
            Line(g, g.W / 2f, 0, g.W / 2f, g.H, "#2a423d", 4);
            Grain(g, new[] { "#43665e", "#2f4a44" }, 400, 0.14f, 90);
        } },

        // ---------------------------------------------------------------- Skeld ost cluster
        // LifeSupport's own wall: the ship's teal panel, plain - no service line, no lit lozenges
        // (that is `wallHallTeal`, the corridor's own). Measured #528694, painted two steps under
        // it: two lamps and a pale counter run turned it bright turquoise at the atlas value.
        ["wallLifeSage"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#395e69");
            for (float x = 0; x < g.W; x += 40) Line(g, x, 0, x, g.H, "#2f4e57", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#4a707c", 2);
            Line(g, 0, g.H - 3, g.W, g.H - 3, "#223338", 3);
            Grain(g, new[] { "#40676f", "#325660" }, 500, 0.10f, 91);
        } },

        // Weapons' and Shields' own wall: a flat olive-tan sheet with a brighter strip at the foot.
        // Measured at world (8.90,3.90) in Weapons. surfaces_skeld_ost.js.
        ["wallWeapPanel"] = new Spec { Unit = 1.2f, Draw = g => {
            Fill(g, "#6b6957");
            for (float x = 0; x < g.W; x += 44) Line(g, x, 0, x, g.H, "#5b5a4a", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#7d7b66", 2);
            Rect(g, 0, g.H - 9, g.W, 6, "#7a7864");
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#2e2e28", 3);
            Grain(g, new[] { "#75735f", "#5f5d4d" }, 400, 0.10f, 92);
        } },

        // Navigation's dark slate panelling, and every corridor mouth's reveal on this side of the
        // ship. Measured #39515a at world (16.40,-2.45). surfaces_skeld_ost.js.
        ["wallNavPanel"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#32444d");
            Rect(g, 0, 3, g.W, g.H - 9, "#3a4f59");
            for (float x = 0; x < g.W; x += 36) Line(g, x, 0, x, g.H, "#2a3940", 2, 0.5f);
            Line(g, 0, 2, g.W, 2, "#4a636f", 2);
            Grain(g, new[] { "#3f5560", "#2c3c44" }, 400, 0.12f, 93);
        } },

        // The machinery bays behind Weapons' red railings, and Navigation's own service alcoves: a
        // dark blue-grey plate with a coarse tread. Measured #476168 at world (8.20,-0.40).
        ["deckWeapBay"] = new Spec { Unit = 0.75f, Draw = g => {
            Fill(g, "#3a4e54");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#41575e");
            for (float i = 0; i < g.H; i += 9) Line(g, 0, i, g.W, i, "#31434a", 2, 0.45f);
            Grain(g, new[] { "#48606a", "#33454b" }, 400, 0.12f, 94);
        } },

        // Navigation's bow deck: a colder, more violet plate than the tiled hall behind it. Measured
        // #506286 at world (18.05,-3.50). surfaces_skeld_ost.js.
        ["tileNavBow"] = new Spec { Unit = 0.8f, Draw = g => {
            Fill(g, "#414f6d");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#485778");
            Line(g, 0, g.H * 0.5f, g.W, g.H * 0.5f, "#394560", 2, 0.4f);
            Grain(g, new[] { "#4f5f83", "#3b4864" }, 400, 0.12f, 95);
        } },

        // The circuit boards behind Shields' railings: a dark blue-grey board with amber conductor
        // tracks at right angles - the one thing in that room drawn as a pattern rather than as an
        // object. Measured board #4a5963, track #b69a3b at world (11.20,-12.40).
        ["panelShieldCircuit"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#404e57");
            void Track((float, float)[] pts) {
                for (int i = 0; i < pts.Length - 1; i++)
                    Line(g, pts[i].Item1 * g.W, pts[i].Item2 * g.H,
                         pts[i + 1].Item1 * g.W, pts[i + 1].Item2 * g.H, "#9c8430", 4);
            }
            Track(new (float, float)[] { (0.05f, 0.10f), (0.45f, 0.10f), (0.45f, 0.42f), (0.92f, 0.42f) });
            Track(new (float, float)[] { (0.10f, 0.95f), (0.10f, 0.62f), (0.62f, 0.62f), (0.62f, 0.24f), (0.95f, 0.24f) });
            Track(new (float, float)[] { (0.30f, 0.98f), (0.30f, 0.80f), (0.85f, 0.80f) });
            Rect(g, g.W * 0.66f, g.H * 0.52f, g.W * 0.16f, g.H * 0.14f, "#5b6b74");
            Rect(g, g.W * 0.16f, g.H * 0.28f, g.W * 0.14f, g.H * 0.10f, "#5b6b74");
            Grain(g, new[] { "#485660", "#38444c" }, 300, 0.12f, 96);
        } },

        // The shield emitters: seven tall glowing capsules behind Shields' railings, the brightest
        // thing on this half of the ship. Turned way down from the map's flat white after the
        // second Skeld playtest, which read the nearest capsule as a blown-out column.
        ["shieldsGlowTube"] = new Spec { Unit = 1.0f, Emissive = 0.30f, Draw = g => {
            Fill(g, "#a79e86");
            Rect(g, 0, 0, g.W * 0.5f, g.H, "#8d846d");
            Rect(g, g.W * 0.5f, 0, g.W * 0.5f, g.H, "#c8bfa4", 0.6f);
        } },

        // The shield field itself, on the hull south-east of the room - the hexagonal energy panels
        // the whole room exists for. `Unit` is far bigger than anything this is drawn on so the
        // repeat always resolves to a single hexagon. The translucent interior wash is six FillQuad
        // "triangles" fanning out from the centre, each with its last corner repeated - the same
        // trick rockWall's icicles and bedrock's drips already use for a triangle on this canvas;
        // the repeated corner is a zero-length edge, which caps the fill at half the given alpha,
        // so it is asked for at double strength here to land where the drawing puts it.
        ["shieldsHexField"] = new Spec { Unit = 11.0f, Emissive = 0.28f, Draw = g => {
            Fill(g, "#123a44");
            float cx = g.W * 0.5f, cy = g.H * 0.5f, r = MathF.Min(g.W, g.H) * 0.46f;
            var wash = C("#2aa6b2");
            for (int i = 0; i < 6; i++)
            {
                float a0 = MathF.PI / 6f + i * MathF.PI / 3f, a1 = a0 + MathF.PI / 3f;
                float p0x = cx + MathF.Cos(a0) * r, p0y = cy + MathF.Sin(a0) * r;
                float p1x = cx + MathF.Cos(a1) * r, p1y = cy + MathF.Sin(a1) * r;
                g.FillQuad(cx, cy, p0x, p0y, p1x, p1y, p1x, p1y, wash, 0.70f);
            }
            foreach (var (f, col, lw) in new (float, string, float)[] {
                (1.0f, "#4fbcc4", 5f), (0.66f, "#2f8a94", 3f), (0.33f, "#7fd4da", 3f) })
            {
                for (int i = 0; i < 6; i++)
                {
                    float a0 = MathF.PI / 6f + i * MathF.PI / 3f, a1 = a0 + MathF.PI / 3f;
                    Line(g, cx + MathF.Cos(a0) * r * f, cy + MathF.Sin(a0) * r * f,
                         cx + MathF.Cos(a1) * r * f, cy + MathF.Sin(a1) * r * f, col, lw);
                }
            }
        } },

        // The lit slots down the gun breech in Weapons' north-west corner: ten slits (five as the
        // map draws them - `Cyl`'s single texture repeat means only the facing half of the drum is
        // ever seen, so the drawing needs twice as many). Housing measured #525d73, slit #b62a30.
        ["weapGunSlot"] = new Spec { Unit = 1.5f, Emissive = 0.14f, Draw = g => {
            Fill(g, "#333a4b");
            for (int i = 0; i < 10; i++)
            {
                float x = g.W * (0.04f + i * 0.096f);
                Rect(g, x - g.W * 0.026f, g.H * 0.08f, g.W * 0.076f, g.H * 0.84f, "#3e4659");
                Rect(g, x, g.H * 0.14f, g.W * 0.024f, g.H * 0.72f, "#7d2a18");
            }
            Grain(g, new[] { "#3d4557", "#2a303e" }, 200, 0.08f, 97);
        } },

        // Every window in the hull on the east side: the little hallway's six panes, Weapons' long
        // diagonal slot, the cockpit's front screens. Deep space-glass with a scatter of stars and
        // one faint reflection streak. THE RASTERISER HAS NO BLENDING (see the file banner), so the
        // `transparent`/`opacity` the JS entry declares has no counterpart here - the pane renders
        // as the opaque dark sheet every other window in this project already is, which is also
        // what kit.js' own note says a window at night is anyway.
        ["ostHullWindow"] = new Spec { Unit = 1.4f, Emissive = 0.30f, Draw = g => {
            Fill(g, "#16283b");
            Rect(g, 2, 2, g.W - 4, g.H - 4, "#1a2c40");
            var rnd = new Rng(98);
            for (int i = 0; i < 14; i++)
                g.FillEllipse(4 + rnd.Next() * (g.W - 8), 4 + rnd.Next() * (g.H - 8), 0.8f, 0.8f,
                              C("#cfd6ff"), 0.35f + rnd.Next() * 0.5f);
            Line(g, g.W * 0.28f, 2, g.W * 0.20f, g.H - 2, "#9fd0e0", 4, 0.14f);
            Line(g, 0, 2, g.W, 2, "#0c1620", 3);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#0c1620", 3);
        } },

        // ---------------------------------------------------------------- Skeld sued cluster
        // Admin's own wall: a cool grey-blue panel. Measured #768791 at world (2.9,-6.2).
        ["wallAdminPanel"] = new Spec { Unit = 1.1f, Draw = g => {
            Fill(g, "#66757e");
            for (float x = 0; x < g.W; x += 38) Line(g, x, 0, x, g.H, "#57666e", 2, 0.5f);
            Line(g, 0, 3, g.W, 3, "#809199", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#2b3337", 3);
            Grain(g, new[] { "#71818a", "#5b6a72" }, 500, 0.10f, 99);
        } },

        // Comms' wall: a pale sage green, the one warm-grey box on this half of the ship. Measured
        // #849a94 at world (6.2,-14.3). surfaces_skeld_sued.js.
        ["wallCommsPanel"] = new Spec { Unit = 1.0f, Draw = g => {
            Fill(g, "#71857f");
            for (float x = 0; x < g.W; x += 30) Line(g, x, 0, x, g.H, "#637570", 2, 0.45f);
            Line(g, 0, 3, g.W, 3, "#8b9f98", 2);
            Line(g, 0, g.H - 2, g.W, g.H - 2, "#2c3634", 3);
            Grain(g, new[] { "#7d9189", "#667a74" }, 400, 0.10f, 100);
        } },

        // The chequer plate in Admin's north-east corner, under the O2 console: studs on a
        // half-offset grid the way tread plate is pressed. Each stud is a 45-degree square, drawn
        // as a diamond (four rotated-by-hand corners) rather than through a transform stack Canvas2D
        // does not have. Measured #849694 at world (6.5,-6.8). surfaces_skeld_sued.js.
        ["plateAdminTread"] = new Spec { Unit = 0.5f, Draw = g => {
            Fill(g, "#737f7d");
            const float s = 16f, dq = 2.6f * 1.41421356f;
            for (int j = 0; j * s < g.H + s; j++)
                for (int i = 0; i * s < g.W + s; i++)
                {
                    float cx = i * s + (s * 0.3f) + ((j & 1) != 0 ? s * 0.5f : 0f), cy = j * s + s * 0.3f;
                    g.FillQuad(cx, cy - dq, cx + dq, cy, cx, cy + dq, cx - dq, cy, C("#616c6a"));
                }
            Grain(g, new[] { "#7e8a88", "#68726f" }, 400, 0.12f, 101);
        } },
    };

    private static Spec Brew(string bas, string lo, string hi, string bub, int seed) =>
        new Spec { Unit = 0.42f, Emissive = 0.28f, Draw = g => {
            Fill(g, bas);
            for (int i = 0; i < 7; i++)
                Rect(g, 0, (int)(i * g.H / 7f + 3), g.W, 4, i % 2 == 1 ? lo : hi, 0.35f);
            // The bubbles, wrapped in x so the column of fluid has no visible seam.
            var rnd = new Rng(seed);
            for (int i = 0; i < 60; i++)
            {
                float bx = rnd.Next() * g.W, by = rnd.Next() * g.H, r = 0.8f + rnd.Next() * 2.0f;
                for (int dx = -1; dx <= 1; dx++)
                    g.FillEllipse(bx + dx * g.W, by, r, r, C(bub), 0.85f);
            }
            Grain(g, new[] { hi, lo }, 500, 0.16f, seed + 1);
        } };

    /// A 45-degree diamond checker: alternating tiles of `dark` and `light`, half of them a
    /// FillQuad rotated square. Nothing on Polus uses this shape - Cafeteria's mess floor is the
    /// only one on the whole map drawn as a check instead of a stripe or a seam.
    private static Spec DiamondFloor(string dark, string light, float unit, int seed) =>
        new Spec { Unit = unit, Draw = g => {
            Fill(g, dark);
            const float s = 16f;
            int cols = (int)(g.W / s) + 3, rows = (int)(g.H / s) + 3;
            var lightC = C(light);
            for (int j = -1; j < rows; j++)
                for (int i = -1; i < cols; i++)
                {
                    if (((i + j) & 1) == 0) continue;
                    float cx = i * s, cy = j * s;
                    g.FillQuad(cx, cy - s, cx + s, cy, cx, cy + s, cx - s, cy, lightC);
                }
            Grain(g, new[] { light, dark }, 500, 0.08f, seed);
        } };
}
