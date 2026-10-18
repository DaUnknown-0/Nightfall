// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * GpuWorld - the map's 3D model drawn by the graphics card (User 2026-10-18).
 *
 * WHERE THE MODEL COMES FROM
 * --------------------------
 * The maps were rebuilt room by room in Blender (Assets/BlenderMaps). pack_nf.py turns each into one
 * file, <map>.nfm, in BepInEx/nightfall-models: textures (PNG), materials, the triangles in 8 m
 * tiles with a normal and a baked shading value per corner, and a floor height raster. Without a
 * package for the current map nothing here runs and the software view (Raster3D) is used as before.
 *
 * TWO PASSES, BECAUSE OF THE SHADERS AMONG US SHIPS
 * -------------------------------------------------
 * A mod cannot compile shaders, and the game contains no textured shader that writes depth.
 * "Hidden/Internal-Colored" writes depth (its _ZWrite is a material property) but has no texture;
 * "Sprites/Default" has the texture but never writes depth. So every opaque tile is drawn twice
 * with the same mesh: first Internal-Colored fills the depth buffer, then Sprites/Default paints
 * the texture with ZTest LEqual - identical vertices give identical depth, so exactly the front
 * surface passes. Plain coloured tiles need only the first pass (the colour is in the vertices),
 * see-through ones (fences, cut-outs) only the second.
 *
 * FAIR, NOT BRIGHTER (User 2026-10-18)
 * ------------------------------------
 * Hardware may change how sharp the picture is, never what can be seen. The light of every corner
 * is asked of the software renderer itself (Raster3D.LitColor: ambient, torch cone, reach), the
 * fog uses its curve, and a person is drawn with its Seen() factor and PersonRange cut - the same
 * numbers a player on the software view gets. Tiles beyond the view distance are switched off,
 * so the picture ends exactly where the fog does. The werewolf's predator vision is a full-screen
 * colour mapping no game shader can do; while it is on, NightfallView keeps the software view.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nightfall.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nightfall;

internal static class GpuWorld
{
    /// A layer no Among Us camera draws (world camera {0,1,2,4,8,9,11,12,13,14,16}, UI 5/15/19,
    /// shadow 9-12), so the model is seen by our camera alone.
    private const int Layer = 31;

    // ---------------------------------------------------------------- package data (any thread)
    private sealed class PackChunk
    {
        public int Mat, TileX, TileY;
        public float[] Pos;        // x, y, z (Among Us: east, north, height) per corner
        public float[] Uv;
        public sbyte[] Nrm;
        public byte[] Baked;
        public int[] Idx;
    }

    private sealed class PackMat
    {
        public int Kind;           // 0 tex, 1 colour, 2 see-through tex, 3 outline
        public int Tex;
        public byte R, G, B;
        public bool Emit;
    }

    private sealed class Pack
    {
        public string Map;
        public float WallH, Tile;
        public bool Outdoor;
        public Color32 World;
        public List<(string name, bool repeat, bool point, byte[] png)> Textures = new();
        public List<PackMat> Mats = new();
        public List<PackChunk> Chunks = new();
        public float GridX0, GridY0, GridCell;
        public int GridW, GridH;
        public short[] Grid;
    }

    // ---------------------------------------------------------------- built world (main thread)
    private sealed class Chunk
    {
        public GameObject Go;
        public Mesh Mesh;
        public PackMat Mat;
        public float[] Pos;
        public sbyte[] Nrm;
        public byte[] Baked;
        public Color32[] Cols;
        public Il2CppStructArray<Color32> NativeCols;
        public float Cx, Cy, Radius;
        public bool Active;
    }

    private static Pack pack;
    private static Task<Pack> loading;
    private static string loadingKey;
    private static string failedKey;
    private static readonly List<Chunk> chunks = new();
    private static readonly List<Texture2D> textures = new();
    private static readonly List<Material> materials = new();
    private static GameObject root;
    private static GameObject camGo;
    private static Camera cam;

    public static string LoadedKey { get; private set; }
    public static bool Ready => root != null && pack != null;
    public static bool Visible { get; private set; }

    // ================================================================ which package
    /// The package name for the map being played, or null. Unknown's Atlas maps are recognised
    /// through its public AtlasMaps.ActiveKey (read by reflection, no compile-time reference).
    public static string PackKeyForCurrentMap()
    {
        try
        {
            var t = AtlasMapsType();
            if (t != null)
            {
                var p = t.GetProperty("ActiveKey");
                if (p?.GetValue(null) is string ak && !string.IsNullOrEmpty(ak)) return ak;
            }
        }
        catch { }
        return SceneGeometry.CurrentMapKey switch
        {
            "skeldship" => "skeld",
            "miraship" => "mira",
            "polusship" => "polus",
            "airship" => "airship",
            "fungleship" => "fungle",
            _ => null,
        };
    }

    private static Type atlasMaps;
    private static bool atlasLooked;

    private static Type AtlasMapsType()
    {
        if (atlasLooked) return atlasMaps;
        atlasLooked = true;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType("UnknownsAtlas.AtlasMaps", false);
            if (t != null) { atlasMaps = t; break; }
        }
        return atlasMaps;
    }

    // ================================================================ loading
    /// Starts (or continues) getting the model of the current map ready. Cheap to call every frame:
    /// the file is read on a worker thread, the Unity objects are built on the first call after.
    public static void Prepare()
    {
        if (NightfallPlugin.GpuWorldEnabled?.Value == false) return;
        string key = PackKeyForCurrentMap();
        if (key == null || key == failedKey) return;
        if (Ready && LoadedKey == key) return;
        if (Ready && LoadedKey != key) Release();

        if (loading == null)
        {
            string path = GpuModels.PathFor(key);
            if (path == null) return;
            loadingKey = key;
            loading = Task.Run(() => Read(path));
            return;
        }
        if (!loading.IsCompleted) return;
        var task = loading;
        loading = null;
        if (task.IsFaulted || task.Result == null || loadingKey != key)
        {
            NightfallPlugin.Logger?.LogWarning(
                $"[Nightfall] GPU model '{loadingKey}' unreadable: {task.Exception?.GetBaseException().Message}");
            failedKey = loadingKey;
            return;
        }
        try
        {
            var t0 = DateTime.Now;
            Build(task.Result);
            LoadedKey = key;
            NightfallPlugin.Logger?.LogInfo(
                $"[Nightfall] GPU model '{key}' built in {(DateTime.Now - t0).TotalMilliseconds:F0} ms: "
                + $"{chunks.Count} tiles, {textures.Count} textures.");
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] GPU model '{key}' failed: {e}");
            failedKey = key;
            Release();
        }
    }

    private static string ReadStr(BinaryReader r)
    {
        int n = r.ReadUInt16();
        return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n));
    }

    private static Pack Read(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        using var r = new BinaryReader(fs);
        if (new string(r.ReadChars(4)) != "NFM2") throw new InvalidDataException("not an NFM2 package");
        if (r.ReadUInt32() != 2) throw new InvalidDataException("unknown package version");
        var p = new Pack { Map = ReadStr(r) };
        p.WallH = r.ReadSingle();
        p.World = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), 255);
        p.Outdoor = r.ReadByte() != 0;
        p.Tile = r.ReadSingle();
        int nt = (int)r.ReadUInt32();
        for (int i = 0; i < nt; i++)
        {
            string name = ReadStr(r);
            bool rep = r.ReadByte() != 0, pt = r.ReadByte() != 0;
            int len = (int)r.ReadUInt32();
            p.Textures.Add((name, rep, pt, r.ReadBytes(len)));
        }
        int nm = (int)r.ReadUInt32();
        for (int i = 0; i < nm; i++)
            p.Mats.Add(new PackMat
            {
                Kind = r.ReadByte(), Tex = r.ReadInt32(), R = r.ReadByte(), G = r.ReadByte(), B = r.ReadByte(),
                Emit = r.ReadByte() != 0,
            });
        int nc = (int)r.ReadUInt32();
        for (int i = 0; i < nc; i++)
        {
            var c = new PackChunk { Mat = (int)r.ReadUInt32(), TileX = r.ReadInt16(), TileY = r.ReadInt16() };
            int nv = (int)r.ReadUInt32(), ni = (int)r.ReadUInt32();
            c.Pos = new float[nv * 3];
            Buffer.BlockCopy(r.ReadBytes(nv * 12), 0, c.Pos, 0, nv * 12);
            c.Uv = new float[nv * 2];
            Buffer.BlockCopy(r.ReadBytes(nv * 8), 0, c.Uv, 0, nv * 8);
            c.Nrm = new sbyte[nv * 3];
            Buffer.BlockCopy(r.ReadBytes(nv * 3), 0, c.Nrm, 0, nv * 3);
            c.Baked = r.ReadBytes(nv);
            c.Idx = new int[ni];
            Buffer.BlockCopy(r.ReadBytes(ni * 4), 0, c.Idx, 0, ni * 4);
            p.Chunks.Add(c);
        }
        if (r.ReadByte() != 0)
        {
            p.GridX0 = r.ReadSingle(); p.GridY0 = r.ReadSingle(); p.GridCell = r.ReadSingle();
            p.GridW = (int)r.ReadUInt32(); p.GridH = (int)r.ReadUInt32();
            p.Grid = new short[p.GridW * p.GridH];
            Buffer.BlockCopy(r.ReadBytes(p.Grid.Length * 2), 0, p.Grid, 0, p.Grid.Length * 2);
        }
        return p;
    }

    private static Material depthMat, outlineMat;

    private static Material NewColored(int cull, int queue)
    {
        var sh = Shader.Find("Hidden/Internal-Colored");
        if (sh == null) throw new InvalidOperationException("Hidden/Internal-Colored not found");
        var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        m.SetInt("_SrcBlend", (int)BlendMode.One);
        m.SetInt("_DstBlend", (int)BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        m.SetInt("_Cull", cull);
        m.renderQueue = queue;
        materials.Add(m);
        return m;
    }

    private static void Build(Pack p)
    {
        Release();
        pack = p;
        root = new GameObject("NightfallGpuWorld") { layer = Layer };
        UnityEngine.Object.DontDestroyOnLoad(root);
        root.SetActive(false);

        depthMat = NewColored((int)CullMode.Off, 2000);
        outlineMat = NewColored((int)CullMode.Front, 2001);

        var sprite = Shader.Find("Sprites/Default");
        if (sprite == null) throw new InvalidOperationException("Sprites/Default not found");
        var texMats = new Material[p.Textures.Count];
        var alphaMats = new Material[p.Textures.Count];
        for (int i = 0; i < p.Textures.Count; i++)
        {
            var (name, rep, pt, png) = p.Textures[i];
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "nf_" + name };
            ImageConversion.LoadImage(tex, png, false);
            tex.wrapMode = rep ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = pt ? FilterMode.Point : FilterMode.Trilinear;
            tex.anisoLevel = 4;
            try { tex.Compress(false); } catch { }
            tex.Apply(true, true);
            tex.hideFlags = HideFlags.HideAndDontSave;
            textures.Add(tex);
        }
        Material TexMat(int t, bool alpha)
        {
            var arr = alpha ? alphaMats : texMats;
            if (arr[t] != null) return arr[t];
            var m = new Material(sprite) { hideFlags = HideFlags.HideAndDontSave, mainTexture = textures[t] };
            m.renderQueue = alpha ? 3000 : 2450;     // after every depth write; see-through with the sprites
            materials.Add(m);
            return arr[t] = m;
        }

        foreach (var pc in p.Chunks)
        {
            var mat = p.Mats[pc.Mat];
            int nv = pc.Pos.Length / 3;
            var verts = new Vector3[nv];
            var uvs = new Vector2[nv];
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < nv; i++)
            {
                float x = pc.Pos[i * 3], y = pc.Pos[i * 3 + 1], z = pc.Pos[i * 3 + 2];
                verts[i] = new Vector3(x, z, y);                 // Unity: X east, Y up, Z north
                uvs[i] = new Vector2(pc.Uv[i * 2], pc.Uv[i * 2 + 1]);
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
            var mesh = new Mesh { name = $"nf_{pc.TileX}_{pc.TileY}_{pc.Mat}", hideFlags = HideFlags.HideAndDontSave };
            if (nv > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            var cols = new Color32[nv];
            for (int i = 0; i < nv; i++) cols[i] = new Color32(0, 0, 0, 255);
            var native = new Il2CppStructArray<Color32>(cols);
            mesh.colors32 = native;
            mesh.triangles = pc.Idx;
            mesh.RecalculateBounds();
            mesh.MarkDynamic();

            var go = new GameObject(mesh.name) { layer = Layer };
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterials = mat.Kind switch
            {
                0 when mat.Tex >= 0 => new[] { depthMat, TexMat(mat.Tex, false) },
                2 when mat.Tex >= 0 => new[] { TexMat(mat.Tex, true) },
                3 => new[] { outlineMat },
                _ => new[] { depthMat },
            };
            chunks.Add(new Chunk
            {
                Go = go, Mesh = mesh, Mat = mat, Pos = pc.Pos, Nrm = pc.Nrm, Baked = pc.Baked, Cols = cols,
                NativeCols = native, Cx = (x0 + x1) * 0.5f, Cy = (y0 + y1) * 0.5f,
                Radius = 0.5f * MathF.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) + 0.1f,
                Active = true,
            });
        }
        BuildDoors();
    }

    // ================================================================ doors
    // The model has no doors (they move); every door of the round is a dark slab here that exists
    // while the door is shut - the same rule SceneGeometry gives the software world.
    private sealed class Door
    {
        public OpenableDoor Owner;
        public Collider2D Col;
        public Chunk Chunk;
    }

    private static readonly List<Door> doors = new();

    private static void BuildDoors()
    {
        doors.Clear();
        var ship = ShipStatus.Instance;
        if (ship == null) return;
        var found = new List<(OpenableDoor owner, Collider2D col)>();
        try
        {
            var all = ship.AllDoors;
            if (all != null)
                foreach (var d in all)
                {
                    if (d == null) continue;
                    foreach (var c in d.GetComponentsInChildren<Collider2D>())
                        if (c != null && (!c.isTrigger || c.bounds.size.x * c.bounds.size.y < 4f)) found.Add((d, c));
                }
        }
        catch { }
        try
        {
            foreach (var ds in UnityEngine.Object.FindObjectsOfType<DeconSystem>())
                foreach (var d in new SomeKindaDoor[] { ds.UpperDoor, ds.LowerDoor })
                    if (d != null)
                        foreach (var c in d.GetComponentsInChildren<Collider2D>())
                            if (c != null && !c.isTrigger) found.Add((null, c));
        }
        catch { }
        var steel = new PackMat { Kind = 1, Tex = -1, R = 88, G = 96, B = 106 };
        foreach (var (owner, col) in found)
        {
            Bounds b;
            try { b = col.bounds; } catch { continue; }
            float x0 = b.min.x, x1 = b.max.x, y0 = b.min.y, y1 = b.max.y;
            if (x1 - x0 < 0.12f) { float m = (x0 + x1) * 0.5f; x0 = m - 0.06f; x1 = m + 0.06f; }
            if (y1 - y0 < 0.12f) { float m = (y0 + y1) * 0.5f; y0 = m - 0.06f; y1 = m + 0.06f; }
            float z0 = GroundAt((x0 + x1) * 0.5f, (y0 + y1) * 0.5f) ?? 0f, z1 = z0 + (pack?.WallH ?? 1.39f);
            float[] pos =
            {
                x0, y0, z0, x1, y0, z0, x1, y1, z0, x0, y1, z0,
                x0, y0, z1, x1, y0, z1, x1, y1, z1, x0, y1, z1,
            };
            int[] idx = { 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7, 4, 5, 6, 4, 6, 7 };
            var verts = new Vector3[8];
            for (int i = 0; i < 8; i++) verts[i] = new Vector3(pos[i * 3], pos[i * 3 + 2], pos[i * 3 + 1]);
            var cols = new Color32[8];
            var native = new Il2CppStructArray<Color32>(cols);
            var mesh = new Mesh { name = "nf_door", hideFlags = HideFlags.HideAndDontSave, vertices = verts };
            mesh.colors32 = native;
            mesh.triangles = idx;
            mesh.RecalculateBounds();
            var go = new GameObject("nf_door") { layer = Layer };
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.sharedMaterials = new[] { depthMat };
            var nrm = new sbyte[24];
            for (int i = 0; i < 8; i++) nrm[i * 3 + 2] = 127;
            var ch = new Chunk
            {
                Go = go, Mesh = mesh, Mat = steel, Pos = pos, Nrm = nrm, Baked = new byte[] { 200, 200, 200, 200, 230, 230, 230, 230 },
                Cols = cols, NativeCols = native, Cx = (x0 + x1) * 0.5f, Cy = (y0 + y1) * 0.5f,
                Radius = MathF.Max(x1 - x0, y1 - y0), Active = true,
            };
            doors.Add(new Door { Owner = owner, Col = col, Chunk = ch });
        }
    }

    private static bool DoorClosed(Door d, in ViewParams v)
    {
        try
        {
            // Open by any of the game's conventions: IsOpen on the door, the collider switched off
            // (DeconSystem.SetDoorway) or turned into a trigger. And never a slab around the camera
            // itself - the player cannot stand inside a shut door, so that slab is stale state
            // (report 18.10.: a steel wall filling the picture in a doorway).
            if (d.Col == null) return false;
            if (!d.Col.enabled || d.Col.isTrigger) return false;
            if (d.Owner != null && d.Owner.IsOpen) return false;
            var c = d.Chunk;
            float px = v.Position.X, py = v.Position.Y;
            if (px > c.Pos[0] - 0.35f && px < c.Pos[3] + 0.35f && py > c.Pos[1] - 0.35f && py < c.Pos[7] + 0.35f)
                return false;
            return true;
        }
        catch { return false; }
    }

    // ================================================================ floor height
    /// Height of the model's top floor under a point, or null where the model has no floor.
    public static float? GroundAt(float x, float y)
    {
        var p = pack;
        if (p?.Grid == null) return null;
        int i = (int)MathF.Floor((x - p.GridX0) / p.GridCell);
        int j = (int)MathF.Floor((y - p.GridY0) / p.GridCell);
        if (i < 0 || j < 0 || i >= p.GridW || j >= p.GridH) return null;
        short v = p.Grid[j * p.GridW + i];
        return v == short.MinValue ? null : v / 100f;
    }

    // ================================================================ per frame
    public static void Hide()
    {
        if (!Visible) return;
        Visible = false;
        try
        {
            if (root != null) root.SetActive(false);
            if (camGo != null) camGo.SetActive(false);
            HideBillboards(0);
        }
        catch { }
    }

    private static void EnsureCamera()
    {
        if (camGo != null) return;
        camGo = new GameObject("NightfallGpuCamera");
        UnityEngine.Object.DontDestroyOnLoad(camGo);
        cam = camGo.AddComponent<Camera>();
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // Between the world camera (-1) and the HUD cameras (99/100): the model replaces the map,
        // every button, task and chat stays on top of it.
        cam.depth = 0f;
        cam.orthographic = false;
        cam.nearClipPlane = 0.03f;
        cam.allowHDR = false;
        cam.useOcclusionCulling = false;
        EnsureOverlay();
    }

    /// Draws the model for this frame. renderer supplies the light (same formulas as the software
    /// view), billboards the people, markers and role objects collected by NightfallView.
    public static void Render(in ViewParams v, Raster3D renderer, IReadOnlyList<Billboard> billboards)
    {
        if (!Ready) return;
        lightSource = renderer;
        EnsureCamera();
        if (!root.activeSelf) root.SetActive(true);
        if (!camGo.activeSelf) camGo.SetActive(true);
        Visible = true;

        var fog = v.FogColor;
        cam.backgroundColor = new Color(fog.R, fog.G, fog.B, 1f);
        cam.fieldOfView = v.Fov * Mathf.Rad2Deg;
        cam.farClipPlane = MathF.Max(2f, v.ViewDistance + 0.5f);
        float ch = MathF.Cos(v.Heading), sh = MathF.Sin(v.Heading);
        float cp = MathF.Cos(v.Pitch), sp = MathF.Sin(v.Pitch);
        camGo.transform.position = new Vector3(v.Position.X, v.EyeHeight, v.Position.Y);
        camGo.transform.rotation = Quaternion.LookRotation(new Vector3(ch * cp, sp, sh * cp), Vector3.up);

        renderer.PrepareLight(v);
        float hFov = 2f * MathF.Atan(cam.aspect * MathF.Tan(v.Fov * 0.5f));
        float cone = hFov * 0.5f + 0.25f;

        foreach (var c in chunks) Light(c, v, ch, sh, cone);
        foreach (var d in doors)
        {
            bool shut = DoorClosed(d, v);
            if (d.Chunk.Go.activeSelf != shut) d.Chunk.Go.SetActive(shut);
            if (shut) Light(d.Chunk, v, ch, sh, cone);
        }
        DrawBillboards(v, renderer, billboards, ch, sh);
        DrawOverlay(v);
    }

    private static void Light(Chunk c, in ViewParams v, float ch, float sh, float cone)
    {
        float dx = c.Cx - v.Position.X, dy = c.Cy - v.Position.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        // Past the fog nothing is drawn at all: the picture ends where the software view's does.
        bool on = dist - c.Radius < v.ViewDistance;
        if (on != c.Active) { c.Go.SetActive(on); c.Active = on; }
        if (!on) return;
        // Behind the viewer: left as it is until it turns into view (it is lit again that frame).
        if (dist > c.Radius + 1.5f)
        {
            float ang = MathF.Abs(NfMath.WrapAngle(MathF.Atan2(dy, dx) - v.Heading));
            if (ang - MathF.Asin(MathF.Min(1f, c.Radius / dist)) > cone) return;
        }

        var m = c.Mat;
        float mr = m.Kind == 0 || m.Kind == 2 ? 1f : m.R / 255f;
        float mg = m.Kind == 0 || m.Kind == 2 ? 1f : m.G / 255f;
        float mb = m.Kind == 0 || m.Kind == 2 ? 1f : m.B / 255f;
        var pos = c.Pos;
        var nrm = c.Nrm;
        var cols = c.Cols;
        var native = c.NativeCols;
        int n = cols.Length;
        for (int i = 0; i < n; i++)
        {
            float x = pos[i * 3], y = pos[i * 3 + 1], z = pos[i * 3 + 2];
            float fx = x - v.Position.X, fy = y - v.Position.Y;
            float zf = fx * ch + fy * sh;
            float fogT = Raster3D.FogAmount(MathF.Max(0f, zf), v);
            NfColor lit;
            if (m.Emit) lit = new NfColor(1f, 1f, 1f);
            else
            {
                lit = renderer_LitColor(v, x, y, z, nrm[i * 3] / 127f, nrm[i * 3 + 1] / 127f, nrm[i * 3 + 2] / 127f);
                float shade = 0.62f + 0.38f * (c.Baked[i] / 255f);
                lit = lit * shade;
                if (m.Kind == 3) lit = new NfColor(MathF.Min(1f, lit.R * 2f), MathF.Min(1f, lit.G * 2f), MathF.Min(1f, lit.B * 2f));
            }
            if (v.PredatorVision) lit = Predator(lit);
            float keep = 1f - fogT;
            var col = new Color32(NfMath.ToByte(lit.R * mr * keep), NfMath.ToByte(lit.G * mg * keep),
                                  NfMath.ToByte(lit.B * mb * keep), 255);
            cols[i] = col;
            native[i] = col;
        }
        c.Mesh.colors32 = native;
    }

    /// The beast's heat picture (HandLight.PredatorTint maps the luminance of every PIXEL from a cold
    /// violet to a hot red). No game shader can remap a texture, so the light that multiplies it
    /// carries the ramp instead: dark stays cold, lit turns red - the same reading, done per corner.
    private static NfColor Predator(NfColor lit)
    {
        float l = lit.R * 0.299f + lit.G * 0.587f + lit.B * 0.114f;
        l *= 1.55f - 0.55f * MathF.Min(1f, l);
        var cold = new NfColor(0.10f, 0.09f, 0.16f);
        var hot = new NfColor(1.15f, 0.30f, 0.22f);
        return NfColor.Lerp(cold, hot, NfMath.Clamp01(l * 1.15f)) * 1.6f;
    }

    // Raster3D is per NightfallView; held here for the vertex loop (set at the top of Render).
    private static Raster3D lightSource;

    private static NfColor renderer_LitColor(in ViewParams v, float x, float y, float z, float nx, float ny, float nz) =>
        lightSource.LitColor(v, new NfVec3(x, z, y), new NfVec3(nx, nz, ny));

    // ================================================================ people, markers, role objects
    private sealed class BillboardTex
    {
        public Texture2D Tex;
        public Sprite Sprite;
        public float Built;
        public float Used;
    }

    private static readonly Dictionary<(IBillboardSource, int, int), BillboardTex> bbCache = new();
    private static readonly List<SpriteRenderer> bbPool = new();

    private static int ColorKey(in Billboard b) =>
        HashCode.Combine((int)(b.Color.R * 255), (int)(b.Color.G * 255), (int)(b.Color.B * 255),
                         (int)(b.ShadowColor.R * 255), (int)(b.ShadowColor.G * 255), (int)(b.ShadowColor.B * 255));

    private static BillboardTex TexFor(in Billboard b, int frame)
    {
        var key = (b.Source, frame, ColorKey(b));
        float now = Time.time;
        // Photographs are retaken by AvatarCapture (outfit, hat, morph); procedural sprites never change.
        bool photo = b.Source is CapturedSprite;
        if (bbCache.TryGetValue(key, out var e) && (!photo || now - e.Built < 1.0f))
        {
            e.Used = now;
            return e;
        }
        int w = b.Source.Width, h = b.Source.Height;
        if (w <= 0 || h <= 0) return null;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!b.Source.Sample(frame, x, y, out var t, out float mask, out float shadow)) continue;
                if (mask > 0f) t = NfColor.Lerp(t, b.Color, mask);
                if (shadow > 0f) t = NfColor.Lerp(t, b.ShadowColor, shadow);
                // Sample counts rows from the top, Unity textures from the bottom.
                px[(h - 1 - y) * w + x] = new Color32(NfMath.ToByte(t.R), NfMath.ToByte(t.G), NfMath.ToByte(t.B), 255);
            }
        if (e == null || e.Tex == null || e.Tex.width != w || e.Tex.height != h)
        {
            e ??= new BillboardTex();
            e.Tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            e.Sprite = Sprite.Create(e.Tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
            e.Sprite.hideFlags = HideFlags.HideAndDontSave;
        }
        e.Tex.SetPixels32(px);
        e.Tex.Apply(false);
        e.Built = e.Used = now;
        bbCache[key] = e;
        if (bbCache.Count > 160) Trim(now);
        return e;
    }

    private static void Trim(float now)
    {
        var drop = new List<(IBillboardSource, int, int)>();
        foreach (var kv in bbCache) if (now - kv.Value.Used > 10f) drop.Add(kv.Key);
        foreach (var k in drop)
        {
            try
            {
                var e = bbCache[k];
                if (e.Sprite != null) UnityEngine.Object.Destroy(e.Sprite);
                if (e.Tex != null) UnityEngine.Object.Destroy(e.Tex);
            }
            catch { }
            bbCache.Remove(k);
        }
    }

    private static void DrawBillboards(in ViewParams v, Raster3D renderer, IReadOnlyList<Billboard> list, float ch, float sh)
    {
        int used = 0;
        if (list != null)
        {
            float yaw = 90f - v.Heading * Mathf.Rad2Deg;
            for (int i = 0; i < list.Count; i++)
            {
                var bb = list[i];
                if (bb.Source == null || bb.Fade >= 1f) continue;
                float dx = bb.Position.X - v.Position.X, dz = bb.Position.Y - v.Position.Y;
                float zf = dx * ch + dz * sh;
                if (zf <= 0.08f || zf > v.ViewDistance) continue;
                if (v.PersonRange > 0f && bb.Glow <= 0f && zf > v.PersonRange) continue;
                float dist = MathF.Sqrt(dx * dx + dz * dz);
                var lookAt = new NfVec3(bb.Position.X, bb.Base + bb.Height * 0.55f, bb.Position.Y);
                var lit = renderer.LitColor(v, lookAt, new NfVec3(-dx / MathF.Max(0.001f, dist), 0f, -dz / MathF.Max(0.001f, dist)));
                float l = (lit.R + lit.G + lit.B) / 3f;
                if (bb.Glow > 0f && bb.Glow > l) lit = new NfColor(bb.Glow, bb.Glow, bb.Glow);
                // Prey runs warm (Raster3D: lifted to 1.12, then the red ramp) - a heat signature.
                if (v.PredatorVision) lit = bb.Glow > 0f ? Predator(lit) : new NfColor(1f, 0.42f, 0.32f);
                float alpha = 1f - NfMath.Clamp01(bb.Fade);
                if (!v.PredatorVision && bb.Glow <= 0f) alpha *= renderer.Seen(v, lookAt);
                if (alpha <= 0.02f) continue;
                float keep = 1f - Raster3D.FogAmount(zf, v);

                float viewAngle = MathF.Atan2(-dz, -dx);
                int frame = bb.Source.FrameForAngle(NfMath.WrapAngle(bb.Facing - viewAngle));
                var tex = TexFor(bb, frame);
                if (tex == null) continue;

                SpriteRenderer sr;
                if (used < bbPool.Count) sr = bbPool[used];
                else
                {
                    var go = new GameObject("nf_billboard") { layer = Layer };
                    go.transform.SetParent(root.transform, false);
                    sr = go.AddComponent<SpriteRenderer>();
                    bbPool.Add(sr);
                }
                used++;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                sr.sprite = tex.Sprite;
                sr.color = new Color(MathF.Min(1f, lit.R * keep), MathF.Min(1f, lit.G * keep), MathF.Min(1f, lit.B * keep), alpha);
                var t = sr.transform;
                t.position = new Vector3(bb.Position.X, bb.Base, bb.Position.Y);
                t.rotation = Quaternion.Euler(0f, yaw, 0f);
                t.localScale = new Vector3(bb.Height, bb.Height, 1f);
            }
        }
        HideBillboards(used);
    }

    private static void HideBillboards(int from)
    {
        for (int i = from; i < bbPool.Count; i++)
            if (bbPool[i] != null && bbPool[i].gameObject.activeSelf) bbPool[i].gameObject.SetActive(false);
    }

    // ================================================================ the torch in the hand, the vignette
    // Drawn by the software view's own HandLight into a small transparent picture in front of the
    // lens. HandLight paints over whatever is there, so it is painted twice - on black and on white -
    // and the difference gives the coverage of every pixel exactly.
    private const int OvW = 480, OvH = 270;
    private static GameObject overlayGo;
    private static Texture2D overlayTex;
    private static readonly byte[] onBlack = new byte[OvW * OvH * 4], onWhite = new byte[OvW * OvH * 4];
    private static Color32[] overlayPx;

    private static void EnsureOverlay()
    {
        overlayGo = new GameObject("nf_overlay") { layer = Layer };
        overlayGo.transform.SetParent(camGo.transform, false);
        overlayTex = new Texture2D(OvW, OvH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
        };
        var sp = Sprite.Create(overlayTex, new Rect(0, 0, OvW, OvH), new Vector2(0.5f, 0.5f), OvH);
        var sr = overlayGo.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = 32000;
        overlayPx = new Color32[OvW * OvH];
    }

    private static void DrawOverlay(in ViewParams v)
    {
        if (overlayGo == null) return;
        // Fit the picture to the frustum a hair in front of the near plane.
        const float d = 0.05f;
        float hgt = 2f * d * MathF.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
        overlayGo.transform.localPosition = new Vector3(0f, 0f, d);
        overlayGo.transform.localRotation = Quaternion.identity;
        overlayGo.transform.localScale = new Vector3(hgt * cam.aspect / ((float)OvW / OvH) * 1.02f, hgt * 1.02f, 1f);

        Array.Clear(onBlack, 0, onBlack.Length);
        for (int i = 0; i < onWhite.Length; i++) onWhite[i] = 255;
        HandLight.Draw(onBlack, OvW, OvH, v);
        HandLight.Draw(onWhite, OvW, OvH, v);
        // The vignette of the software view, as black with coverage (same curve as HandLight.Vignette).
        float cx = OvW * 0.5f, cy = OvH * 0.5f, maxR = MathF.Sqrt(cx * cx + cy * cy), k = maxR / MathF.Min(cx, cy);
        const float q0 = 0.75f * 0.75f, q1 = 1.65f * 1.65f;
        for (int y = 0; y < OvH; y++)
        {
            float ddy = (y - cy) / maxR;
            for (int x = 0; x < OvW; x++)
            {
                int o = (y * OvW + x) * 4;
                // coverage of the hand: a = 1 - (white - black)
                float a = 1f - (onWhite[o] - onBlack[o] + onWhite[o + 1] - onBlack[o + 1] + onWhite[o + 2] - onBlack[o + 2]) / 765f;
                a = a < 0f ? 0f : a > 1f ? 1f : a;
                float r = 0f, g = 0f, b = 0f;
                if (a > 0.004f) { r = onBlack[o] / 255f / a; g = onBlack[o + 1] / 255f / a; b = onBlack[o + 2] / 255f / a; }
                float ddx = (x - cx) / maxR;
                float r2 = (ddx * ddx + ddy * ddy) * k * k;
                float va = 0f;
                if (r2 > q0)
                {
                    float t = NfMath.Clamp01((r2 - q0) / (q1 - q0));
                    va = t * t * (3f - 2f * t) * 0.85f;
                }
                float A = a + va * (1f - a);
                if (A > 0.004f) { r = r * a / A; g = g * a / A; b = b * a / A; }
                // Texture rows start at the bottom, HandLight's at the top.
                overlayPx[(OvH - 1 - y) * OvW + x] = new Color32((byte)(MathF.Min(1f, r) * 255), (byte)(MathF.Min(1f, g) * 255),
                                                                (byte)(MathF.Min(1f, b) * 255), (byte)(A * 255));
            }
        }
        overlayTex.SetPixels32(overlayPx);
        overlayTex.Apply(false);
    }

    // ================================================================ teardown
    public static void Release()
    {
        Hide();
        foreach (var c in chunks) { try { UnityEngine.Object.Destroy(c.Mesh); } catch { } }
        foreach (var d in doors) { try { UnityEngine.Object.Destroy(d.Chunk.Mesh); } catch { } }
        foreach (var t in textures) { try { UnityEngine.Object.Destroy(t); } catch { } }
        foreach (var m in materials) { try { UnityEngine.Object.Destroy(m); } catch { } }
        chunks.Clear();
        doors.Clear();
        textures.Clear();
        materials.Clear();
        bbPool.Clear();
        foreach (var e in bbCache.Values)
        {
            try { if (e.Sprite != null) UnityEngine.Object.Destroy(e.Sprite); if (e.Tex != null) UnityEngine.Object.Destroy(e.Tex); } catch { }
        }
        bbCache.Clear();
        if (root != null) { try { UnityEngine.Object.Destroy(root); } catch { } }
        root = null;
        pack = null;
        LoadedKey = null;
    }

    /// Round end / lobby: everything of the round goes, the failure memory too (a package may have
    /// been fixed or downloaded meanwhile).
    public static void Reset()
    {
        Release();
        failedKey = null;
        loading = null;
    }
}
