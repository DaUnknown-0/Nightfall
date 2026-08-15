// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * Raster3D - the renderer that replaced the raycaster.
 *
 * WHY THE CHANGE
 * --------------
 * A raycaster can only ever draw vertical slabs textured with whatever the top-down map artwork
 * happens to contain, and Among Us' artwork contains no wall faces, no prop sides and no doors seen
 * edge on. Every complaint from testing traced back to that one limitation: smeared colour columns,
 * lava as a yellow blur, props lying flat on the floor, doors you could see straight through.
 *
 * A triangle model has none of those limits. A wall is a quad with a drawn texture, a console is a
 * box with a lit screen on its front, a door is a door, and a chasm is a hole with an edge. The
 * cost is that the world must be BUILT (Scene3D) rather than traced, which is the price of it
 * looking like the room it is meant to be.
 *
 * WHAT IS DIFFERENT FROM THE PROTOTYPE
 * ------------------------------------
 *  - Lighting is per PIXEL, not per face. In the prototype a whole wall was lit by the distance to
 *    its centre, so the torch never striped across it. Here the world position is interpolated to
 *    every pixel and the beam is evaluated there.
 *  - Triangles are bucketed into a grid, so a map with tens of thousands of them only pays for the
 *    handful near the player.
 *  - It runs across cores, like the raycaster did.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nightfall.Core;

public struct NfVec3
{
    public float X, Y, Z;
    public NfVec3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public static NfVec3 operator +(NfVec3 a, NfVec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static NfVec3 operator -(NfVec3 a, NfVec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static NfVec3 operator *(NfVec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public NfVec3 Normalized { get { float l = Length; return l > 1e-6f ? this * (1f / l) : this; } }

    public static float Dot(NfVec3 a, NfVec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static NfVec3 Cross(NfVec3 a, NfVec3 b) => new(
        a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}

public struct Vtx3
{
    public NfVec3 P;
    public float U, V;
    public Vtx3(NfVec3 p, float u, float v) { P = p; U = u; V = v; }
}

public sealed class Tri3
{
    public Vtx3 A, B, C;
    public Surface3D Tex;
    /// When set, this surface takes its colour from the PHOTOGRAPH OF THE MAP, looked up at the
    /// world position of each pixel rather than through UV coordinates.
    ///
    /// This is what the floor is made of. The alternative - one flat colour per patch, sampled as a
    /// median - threw away everything that makes an Among Us floor recognisable: the tiling, the
    /// hazard stripes at Storage, the carpet edge in the office, the snow drifts outside. Those are
    /// drawn, they exist in the photograph, and a per-pixel lookup simply puts them back. A floor
    /// built this way is not an approximation of the map, it is the map.
    public MapAtlas Atlas;
    /// The room colour this surface wears. The texture supplies structure in neutral grey and this
    /// supplies the colour, which is why one texture serves the whole map.
    public NfColor Tint = new(1f, 1f, 1f);
    /// Flat multiplier per face, so the sides of a box read as different planes.
    public float Shade = 1f;
    /// Self-lit surfaces: console screens, lava, ceiling strips.
    public float Emissive;
    /// Precomputed normal and centre, for lighting and culling.
    public NfVec3 Normal;
    public NfVec3 Centre;
    public float Radius;

    public void Finish()
    {
        Normal = NfVec3.Cross(B.P - A.P, C.P - A.P).Normalized;
        Centre = (A.P + B.P + C.P) * (1f / 3f);
        Radius = MathF.Max((A.P - Centre).Length,
                  MathF.Max((B.P - Centre).Length, (C.P - Centre).Length));
    }
}

public sealed class Raster3D
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public byte[] Pixels { get; private set; } = Array.Empty<byte>();
    private float[] depth = Array.Empty<float>();

    public bool Multithreaded = true;

    /// How many triangles the last frame handed to the rasteriser, and how many pixels those
    /// triangles' bounding boxes covered. Diagnostics only, written once per frame: guessing which
    /// of the two a frame is spending its time on has been wrong twice.
    public int LastVisible;
    public long LastCoverage;

    public void Resize(int w, int h)
    {
        if (Width == w && Height == h && Pixels.Length > 0) return;
        Width = Math.Max(16, w);
        Height = Math.Max(16, h);
        Pixels = new byte[Width * Height * 4];
        depth = new float[Width * Height];
    }

    // ================================================================================
    public void Render(Scene3D scene, in ViewParams view, IReadOnlyList<Billboard> billboards = null)
    {
        if (Pixels.Length == 0) Resize(640, 360);
        var v = view;

        float aspect = Width / (float)Height;
        float f = 1f / MathF.Tan(v.Fov * 0.5f);
        float cy = MathF.Cos(v.Heading), sy = MathF.Sin(v.Heading);
        DrawBackground(v, f, aspect);
        PrepareBeam(v);

        // Triangles near enough to matter. The scene's grid does the heavy lifting.
        // The HORIZONTAL field of view, which is not what ViewParams.Fov holds: the projection
        // divides x by the aspect ratio, so a "75 degree" view is 75 vertically and about 107
        // across. Culling to the narrower of the two would eat the edges of the picture.
        float hFov = 2f * MathF.Atan(aspect * MathF.Tan(v.Fov * 0.5f));
        var visible = scene.Query(v.Position, v.ViewDistance, v.Heading, hFov);

        // Rows are split across cores; splitting by ROW rather than by triangle keeps the depth
        // buffer race free without a single lock.
        //
        // The screen-space Y EXTENT of every triangle is worked out once, up front, and a band
        // skips anything that does not reach into it. Before that, each of sixteen bands walked the
        // entire visible set and re-transformed every triangle: the same work sixteen times over,
        // and by far the biggest cost in a frame.
        int bands = Multithreaded ? Math.Min(Environment.ProcessorCount, 16) : 1;
        int bandH = (Height + bands - 1) / bands;

        PrepareExtents(visible, v, f, aspect, cy, sy);
        LastVisible = visible.Count;
        LastCoverage = 0;
        for (int i = 0; i < visible.Count; i++)
            LastCoverage += Math.Max(0, extentBottom[i] - extentTop[i] + 1);

        void RenderBand(int band)
        {
            int y0 = band * bandH, y1 = Math.Min(Height, y0 + bandH);
            if (y0 >= y1) return;
            for (int i = 0; i < visible.Count; i++)
            {
                if (extentTop[i] >= y1 || extentBottom[i] < y0) continue;
                DrawTri(visible[i], v, f, aspect, cy, sy, y0, y1);
            }
        }

        if (bands <= 1) RenderBand(0);
        else Parallel.For(0, bands, RenderBand);

        // Furniture before players: a prop writes depth, so a crewmate standing behind a transformer
        // is hidden by it rather than painted over it.
        DrawStandingProps(scene, v, f, aspect, cy, sy);

        if (billboards != null && billboards.Count > 0)
            DrawBillboards(v, f, aspect, cy, sy, billboards);

        // WHAT THE PLAYER IS HOLDING. The raycaster drew a torch in the lower right and the beast's
        // forepaws in both corners; the triangle renderer that replaced it drew neither, and the
        // whole first playtest was played with empty hands. It is not decoration: the torch LEANS
        // towards where the beam points, and that lean is the only thing that says the mouse aims
        // the light rather than the head.
        if (v.PredatorVision) HandLight.PredatorTint(Pixels);
        HandLight.Draw(Pixels, Width, Height, v);
        HandLight.Vignette(Pixels, Width, Height);
    }

    // ================================================================================
    /// The map's furniture: each object a vertical panel of its own artwork, turned to face the
    /// camera and standing on the floor where the map puts it.
    ///
    /// WHY A PANEL AND NOT A BOX
    /// Among Us has never drawn the back of anything. A box would need five faces and the artwork
    /// supplies one, so four of them would have to be invented - which is exactly what the guessed
    /// props did, and exactly why they looked like a different game. A panel shows the drawing the
    /// game itself shows, from every angle, which is also how Among Us' own world works.
    private void DrawStandingProps(Scene3D scene, in ViewParams v, float f, float aspect,
                                   float cy, float sy)
    {
        var list = scene.Standing;
        if (list.Count == 0) return;

        var eye = v.Position;
        propOrder.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i].Ground - eye;
            float sq = d.SqrLength;
            if (sq > v.ViewDistance * v.ViewDistance) continue;
            propOrder.Add((sq, i));
        }
        // Back to front, so a nearer object paints over a further one where their edges interleave.
        propOrder.Sort((a, b) => b.sq.CompareTo(a.sq));

        foreach (var (_, idx) in propOrder)
        {
            var p = list[idx];
            float dx = p.Ground.X - eye.X, dz = p.Ground.Y - eye.Y;
            float zf = dx * cy + dz * sy;
            float xr = dx * sy - dz * cy;
            if (zf <= 0.12f) continue;

            float iz = 1f / zf;
            float cxs = (xr * f / aspect) * iz * 0.5f * Width + Width * 0.5f;
            float footY = Height * 0.5f + ((v.EyeHeight - p.Base) * f) * iz * 0.5f * Height;
            float topY = Height * 0.5f - ((p.Base + p.Height - v.EyeHeight) * f) * iz * 0.5f * Height;

            float hPx = footY - topY;
            float wPx = (p.Width * f / aspect) * iz * 0.5f * Width;
            if (hPx < 1.5f || wPx < 1.5f) continue;

            float left = cxs - wPx * 0.5f;
            int x0 = Math.Max(0, (int)MathF.Floor(left));
            int x1 = Math.Min(Width - 1, (int)MathF.Ceiling(left + wPx));
            int y0 = Math.Max(0, (int)MathF.Floor(topY));
            int y1 = Math.Min(Height - 1, (int)MathF.Ceiling(footY));
            if (x1 < x0 || y1 < y0) continue;

            // Lit at its own middle, with a normal turned towards the eye - the same treatment the
            // crewmates get, so a prop and a player standing beside each other match.
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            float lit = Light(v, new NfVec3(p.Ground.X, p.Base + p.Height * 0.5f, p.Ground.Y),
                              new NfVec3(-dx / MathF.Max(0.001f, dist), 0f, -dz / MathF.Max(0.001f, dist)));

            // A panel is axis aligned on screen, so its texel-to-pixel ratio is one number for the
            // whole object. Without it a harvested prop twenty metres off is a fistful of stray
            // texels that flicker as the player walks.
            float propLod = NfMath.FastLog2(MathF.Max(p.Tex.W / MathF.Max(1f, wPx),
                                                      p.Tex.H / MathF.Max(1f, hPx)));

            for (int x = x0; x <= x1; x++)
            {
                float u = (x + 0.5f - left) / wPx;
                if (u < 0f || u >= 1f) continue;
                for (int y = y0; y <= y1; y++)
                {
                    int di = y * Width + x;
                    if (zf >= depth[di]) continue;

                    float vv = (y + 0.5f - topY) / hPx;
                    if (vv < 0f || vv >= 1f) continue;

                    p.Tex.SampleLod(u, vv, propLod, out float r, out float g, out float b, out float a);
                    // A HARD cutout, not a blend. Among Us' edges are crisp and antialiased into
                    // whatever was behind them when the object was photographed; feathering them a
                    // second time here puts a grey fringe around every object in the dark.
                    if (a < 0.5f) continue;

                    depth[di] = zf;
                    var col = Fog(new NfColor(r * lit, g * lit, b * lit), zf, v);
                    col.ToBytes(Pixels, di * 4);
                }
            }
        }
    }

    private readonly List<(float sq, int idx)> propOrder = new();

    // Screen-space vertical extent per visible triangle, so a thread band can reject one with a
    // single integer compare instead of transforming it again.
    private int[] extentTop = Array.Empty<int>();
    private int[] extentBottom = Array.Empty<int>();

    private void PrepareExtents(List<Tri3> vis, in ViewParams v, float f, float aspect,
                                float cy, float sy)
    {
        if (extentTop.Length < vis.Count)
        {
            extentTop = new int[vis.Count + 256];
            extentBottom = new int[vis.Count + 256];
        }

        // ACROSS CORES, because this is the one part of a frame whose cost does NOT fall when the
        // resolution does. Measured on the 89-viewpoint sweep, a frame at 480x270 cost 8,9 ms and
        // one at 854x480 cost 19,6: fitting a straight line through the pixel counts leaves about
        // 4,4 ms that every frame pays regardless of size, and this loop is most of it. Each
        // iteration writes two array slots of its own and reads nothing that changes, so splitting
        // it needs no lock - the same argument that makes the bands safe.
        const float near = 0.06f;
        var pos = v.Position; float eyeH = v.EyeHeight; int h = Height;
        var top = extentTop; var bot = extentBottom;

        void One(int i)
        {
            var t = vis[i];
            var a = ToView(t.A, pos, eyeH, cy, sy);
            var b = ToView(t.B, pos, eyeH, cy, sy);
            var c = ToView(t.C, pos, eyeH, cy, sy);

            // Anything crossing the near plane can end up anywhere on screen after clipping, so it
            // is conservatively marked as covering everything.
            if (a.P.Z <= near || b.P.Z <= near || c.P.Z <= near)
            {
                top[i] = 0;
                bot[i] = h - 1;
                return;
            }

            float ya = h * 0.5f - (a.P.Y * f) / a.P.Z * 0.5f * h;
            float yb = h * 0.5f - (b.P.Y * f) / b.P.Z * 0.5f * h;
            float yc = h * 0.5f - (c.P.Y * f) / c.P.Z * 0.5f * h;

            top[i] = Math.Max(0, (int)MathF.Floor(MathF.Min(ya, MathF.Min(yb, yc))));
            bot[i] = Math.Min(h - 1, (int)MathF.Ceiling(MathF.Max(ya, MathF.Max(yb, yc))));
        }

        if (!Multithreaded || vis.Count < 512) { for (int i = 0; i < vis.Count; i++) One(i); return; }

        // In chunks rather than one task per triangle: the body is a dozen multiplies, so the
        // scheduling would cost more than the work.
        int chunks = Math.Min(Environment.ProcessorCount, 16);
        int per = (vis.Count + chunks - 1) / chunks;
        Parallel.For(0, chunks, k =>
        {
            int i0 = k * per, i1 = Math.Min(vis.Count, i0 + per);
            for (int i = i0; i < i1; i++) One(i);
        });
    }

    /// Sky above the horizon, void below it. Anything the model does not cover shows this, so it
    /// doubles as the "outdoors" background.
    ///
    /// The sky itself is a baked panorama (NightSky) rather than a formula, so a pixel of it costs
    /// one bilinear fetch. Both of its axes are separable - u depends only on the column, v only on
    /// the row - so the four texel addresses and the two blend weights are worked out once per
    /// column and once per row instead of once per pixel.
    private int[] skyX0 = Array.Empty<int>(), skyX1 = Array.Empty<int>();
    private float[] skyTx = Array.Empty<float>();

    private void DrawBackground(in ViewParams v, float f, float aspect)
    {
        NightSky.EnsureBuilt();
        var sky = NightSky.Pixels;
        float horizon = Height * 0.5f;

        if (skyX0.Length < Width)
        {
            skyX0 = new int[Width]; skyX1 = new int[Width]; skyTx = new float[Width];
        }

        // The TRUE bearing of each column, not a linear ramp across the field of view. The linear
        // version drifts by several degrees at the edges of a 107 degree view, which shows up as
        // the stars sliding against the buildings when the player turns.
        float cy = MathF.Cos(v.Heading), sy = MathF.Sin(v.Heading);
        for (int x = 0; x < Width; x++)
        {
            float xz = ((x + 0.5f) - Width * 0.5f) / (0.5f * Width) * aspect / f;
            float ang = MathF.Atan2(sy - xz * cy, cy + xz * sy);
            float u = ang / NfMath.TwoPi;
            u -= MathF.Floor(u);
            float fx = u * NightSky.W - 0.5f;
            int x0 = (int)MathF.Floor(fx);
            skyTx[x] = fx - x0;
            skyX0[x] = ((x0 % NightSky.W) + NightSky.W) % NightSky.W;
            skyX1[x] = (skyX0[x] + 1) % NightSky.W;
        }

        var voidCol = new NfColor(0.020f, 0.014f, 0.035f);

        Parallel.For(0, Height, y =>
        {
            int rowBase = y * Width;
            if (y > horizon)
            {
                for (int x = 0; x < Width; x++)
                {
                    depth[rowBase + x] = float.MaxValue;
                    voidCol.ToBytes(Pixels, (rowBase + x) * 4);
                }
                return;
            }

            float elev = NfMath.Clamp01((horizon - y) / MathF.Max(1f, horizon));
            float fy = elev * (NightSky.H - 1);
            int y0 = (int)fy;
            int y1 = Math.Min(NightSky.H - 1, y0 + 1);
            float ty = fy - y0;
            int r0 = y0 * NightSky.W, r1 = y1 * NightSky.W;

            for (int x = 0; x < Width; x++)
            {
                int a = (r0 + skyX0[x]) * 4, b = (r0 + skyX1[x]) * 4;
                int c = (r1 + skyX0[x]) * 4, d = (r1 + skyX1[x]) * 4;
                float tx = skyTx[x];

                float top = sky[a] + (sky[b] - sky[a]) * tx;
                float bot = sky[c] + (sky[d] - sky[c]) * tx;
                float rr = (top + (bot - top) * ty) * (1f / 255f);
                top = sky[a + 1] + (sky[b + 1] - sky[a + 1]) * tx;
                bot = sky[c + 1] + (sky[d + 1] - sky[c + 1]) * tx;
                float gg = (top + (bot - top) * ty) * (1f / 255f);
                top = sky[a + 2] + (sky[b + 2] - sky[a + 2]) * tx;
                bot = sky[c + 2] + (sky[d + 2] - sky[c + 2]) * tx;
                float bb = (top + (bot - top) * ty) * (1f / 255f);

                depth[rowBase + x] = float.MaxValue;
                new NfColor(rr, gg, bb).ToBytes(Pixels, (rowBase + x) * 4);
            }
        });
    }

    // ================================================================================
    private void DrawTri(Tri3 t, in ViewParams v, float f, float aspect, float cy, float sy,
                         int bandY0, int bandY1)
    {
        var a = ToView(t.A, v.Position, v.EyeHeight, cy, sy);
        var b = ToView(t.B, v.Position, v.EyeHeight, cy, sy);
        var c = ToView(t.C, v.Position, v.EyeHeight, cy, sy);

        // NEAR PLANE: CLIP, DO NOT DISCARD.
        //
        // Dropping any triangle with a vertex behind the eye is the easy answer and it costs the
        // floor: the player is standing ON a floor patch, so that patch always has a corner behind
        // them, and the ground under their feet simply vanished. Clipping against the plane turns
        // such a triangle into the one or two triangles that are actually in front of the camera.
        const float near = 0.06f;
        int behind = (a.P.Z <= near ? 1 : 0) + (b.P.Z <= near ? 1 : 0) + (c.P.Z <= near ? 1 : 0);
        if (behind == 3) return;
        if (behind > 0)
        {
            ClipAndDraw(a, b, c, t, v, f, aspect, near, bandY0, bandY1);
            return;
        }

        DrawClipped(a, b, c, t, v, f, aspect, bandY0, bandY1);
    }

    /// Draws a triangle whose vertices are already in view space and in front of the near plane.
    /// World positions for lighting are reconstructed from the view-space ones, so clipped pieces
    /// light exactly like unclipped ones.
    private void DrawClipped(Vtx3 a, Vtx3 b, Vtx3 c, Tri3 t, in ViewParams v,
                             float f, float aspect, int bandY0, int bandY1)
    {
        // Copied out of the `in` parameter: a local function cannot capture one.
        float cy2 = MathF.Cos(v.Heading), sy2 = MathF.Sin(v.Heading);
        float eyeX = v.Position.X, eyeY = v.Position.Y, eyeH = v.EyeHeight;
        Vtx3 Back(Vtx3 vt)
        {
            // Inverse of ToView: rotate the view-space offset back into the world and put the eye
            // height back on.
            float wx = vt.P.Z * cy2 + vt.P.X * sy2 + eyeX;
            float wz = vt.P.Z * sy2 - vt.P.X * cy2 + eyeY;
            return new Vtx3(new NfVec3(wx, vt.P.Y + eyeH, wz), vt.U, vt.V);
        }
        var wa = Back(a); var wb = Back(b); var wc = Back(c);

        (float sx, float sy, float iz, float u, float vv, float wx, float wy, float wz) P(Vtx3 vt, Vtx3 src)
        {
            float iz = 1f / vt.P.Z;
            float sx = (vt.P.X * f / aspect) * iz * 0.5f * Width + Width * 0.5f;
            float sy2 = Height * 0.5f - (vt.P.Y * f) * iz * 0.5f * Height;
            return (sx, sy2, iz, vt.U * iz, vt.V * iz,
                    src.P.X * iz, src.P.Y * iz, src.P.Z * iz);
        }

        var pa = P(a, wa); var pb = P(b, wb); var pc = P(c, wc);

        int minX = Math.Max(0, (int)MathF.Floor(Math.Min(pa.sx, Math.Min(pb.sx, pc.sx))));
        int maxX = Math.Min(Width - 1, (int)MathF.Ceiling(Math.Max(pa.sx, Math.Max(pb.sx, pc.sx))));
        int minY = Math.Max(bandY0, (int)MathF.Floor(Math.Min(pa.sy, Math.Min(pb.sy, pc.sy))));
        int maxY = Math.Min(bandY1 - 1, (int)MathF.Ceiling(Math.Max(pa.sy, Math.Max(pb.sy, pc.sy))));
        if (minX > maxX || minY > maxY) return;

        float area = Edge(pa.sx, pa.sy, pb.sx, pb.sy, pc.sx, pc.sy);
        if (MathF.Abs(area) < 1e-6f) return;
        float invArea = 1f / area;

        /*
         * SPANS, NOT BOUNDING BOXES.
         *
         * The barycentric weights are linear in x, so the run of pixels a scanline actually covers
         * can be SOLVED FOR instead of found by testing every pixel of the triangle's bounding box
         * and throwing most of them away. That waste is not a constant: a wall seen head on fills
         * half its box, a floor patch seen at a grazing angle is a sliver in a box the width of the
         * screen, and the floor is most of what a first-person frame is made of.
         *
         * The weights are divided by the signed area, so "inside" is all three at or above zero
         * whichever way round the triangle was wound - two-sidedness comes for free and the old
         * double-ended sign test is gone with it.
         */
        float b0 = (pc.sy - pb.sy) * invArea;
        float b1 = (pa.sy - pc.sy) * invArea;
        float b2 = (pb.sy - pa.sy) * invArea;

        /*
         * HOW BIG IS A TEXEL HERE? - the derivative of the UVs in screen space.
         *
         * Bilinear filtering has no answer to this and needs none as long as a texel is bigger than
         * a pixel. As soon as it is smaller - a wall running away from the eye, a floor at a grazing
         * angle - two neighbouring pixels land on unrelated texels and the surface breaks into
         * stripes. The fix is a mip pyramid, and choosing a level from it needs exactly this number.
         *
         * It is nearly free here. The barycentric weights are linear in x AND in y with constant
         * slopes (b* across, c* down), and u = (w.uz)/(w.iz), so
         *
         *     du/dx = (duz/dx - u * diz/dx) * z
         *
         * with both numerator terms constant per triangle. Four multiplies and a subtraction per
         * pixel, no divisions beyond the 1/z the pixel already computes.
         */
        float c0 = (pb.sx - pc.sx) * invArea;
        float c1 = (pc.sx - pa.sx) * invArea;
        float c2 = (pa.sx - pb.sx) * invArea;

        float dizX = b0 * pa.iz + b1 * pb.iz + b2 * pc.iz;
        float duzX = b0 * pa.u + b1 * pb.u + b2 * pc.u;
        float dvzX = b0 * pa.vv + b1 * pb.vv + b2 * pc.vv;
        float dizY = c0 * pa.iz + c1 * pb.iz + c2 * pc.iz;
        float duzY = c0 * pa.u + c1 * pb.u + c2 * pc.u;
        float dvzY = c0 * pa.vv + c1 * pb.vv + c2 * pc.vv;

        float texW = t.Tex?.W ?? 1, texH = t.Tex?.H ?? 1;

        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            float px0 = minX + 0.5f;
            float a0 = Edge(pb.sx, pb.sy, pc.sx, pc.sy, px0, py) * invArea;
            float a1 = Edge(pc.sx, pc.sy, pa.sx, pa.sy, px0, py) * invArea;
            float a2 = Edge(pa.sx, pa.sy, pb.sx, pb.sy, px0, py) * invArea;

            // The x range where all three weights are non-negative, in pixels from minX.
            float lo = 0f, hi = maxX - minX;
            if (!Clip(a0, b0, ref lo, ref hi)) continue;
            if (!Clip(a1, b1, ref lo, ref hi)) continue;
            if (!Clip(a2, b2, ref lo, ref hi)) continue;

            int xs = minX + Math.Max(0, (int)MathF.Ceiling(lo - 1e-4f));
            int xe = minX + Math.Min(maxX - minX, (int)MathF.Floor(hi + 1e-4f));

            for (int x = xs; x <= xe; x++)
            {
                float d = x - minX;
                float w0 = a0 + b0 * d;
                float w1 = a1 + b1 * d;
                float w2 = a2 + b2 * d;

                float iz = w0 * pa.iz + w1 * pb.iz + w2 * pc.iz;
                if (iz <= 0f) continue;
                float z = 1f / iz;

                int di = y * Width + x;
                if (z >= depth[di]) continue;

                // World position of this pixel. Needed for the lighting in every case, and for the
                // colour too when the surface reads out of the map photograph.
                var wp = new NfVec3(
                    (w0 * pa.wx + w1 * pb.wx + w2 * pc.wx) / iz,
                    (w0 * pa.wy + w1 * pb.wy + w2 * pc.wy) / iz,
                    (w0 * pa.wz + w1 * pb.wz + w2 * pc.wz) / iz);

                float r, g, bl;
                if (t.Atlas != null)
                {
                    if (!t.Atlas.SampleBilinear(wp.X, wp.Z, out r, out g, out bl))
                    {
                        r = t.Tint.R; g = t.Tint.G; bl = t.Tint.B;
                    }
                }
                else
                {
                    float u = (w0 * pa.u + w1 * pb.u + w2 * pc.u) * z;
                    float vv = (w0 * pa.vv + w1 * pb.vv + w2 * pc.vv) * z;

                    float dudx = (duzX - u * dizX) * z * texW;
                    float dvdx = (dvzX - vv * dizX) * z * texH;
                    float dudy = (duzY - u * dizY) * z * texW;
                    float dvdy = (dvzY - vv * dizY) * z * texH;
                    float rho2 = MathF.Max(dudx * dudx + dvdx * dvdx, dudy * dudy + dvdy * dvdy);

                    t.Tex.SampleLod(u, vv, 0.5f * NfMath.FastLog2(rho2),
                                    out r, out g, out bl, out float al);
                    if (t.Tex.HasCutout && al < 0.02f) continue;

                    // Alpha is the tint mask, not opacity: 1 takes the room colour, 0 keeps the
                    // colour the texture was drawn in (window glass, hazard yellow, a lit screen).
                    if (al > 0.002f)
                    {
                        r = r * (1f - al) + r * t.Tint.R * al;
                        g = g * (1f - al) + g * t.Tint.G * al;
                        bl = bl * (1f - al) + bl * t.Tint.B * al;
                    }
                }

                depth[di] = z;

                Light2(v, wp, t.Normal, out float amb, out float beam);
                NfColor col;
                if (t.Emissive > 0f)
                {
                    float lit = MathF.Max(amb + beam, t.Emissive);
                    col = new NfColor(r * lit, g * lit, bl * lit);
                }
                else
                {
                    // Two coloured terms rather than one grey one. `Shade` is the per-face
                    // multiplier that keeps the sides of a box apart, so it applies to both.
                    float ka = amb * t.Shade, kb = beam * t.Shade;
                    col = new NfColor(
                        r * (ka * AmbientTint.R + kb * BeamTint.R),
                        g * (ka * AmbientTint.G + kb * BeamTint.G),
                        bl * (ka * AmbientTint.B + kb * BeamTint.B));
                }
                col = Fog(col, z, v);
                col.ToBytes(Pixels, di * 4);
            }
        }
    }

    /// Clips a triangle against the near plane and draws the visible remainder. One vertex behind
    /// leaves a quad (two triangles); two behind leave a single smaller triangle.
    private void ClipAndDraw(Vtx3 a, Vtx3 b, Vtx3 c, Tri3 t, in ViewParams v,
                             float f, float aspect, float near, int bandY0, int bandY1)
    {
        Span<Vtx3> inFront = stackalloc Vtx3[4];
        int n = 0;

        void Emit(Vtx3 cur, Vtx3 nxt, ref int count, Span<Vtx3> outp)
        {
            bool curIn = cur.P.Z > near, nxtIn = nxt.P.Z > near;
            if (curIn) outp[count++] = cur;
            if (curIn != nxtIn)
            {
                float tt = (near - cur.P.Z) / (nxt.P.Z - cur.P.Z);
                outp[count++] = Lerp(cur, nxt, tt);
            }
        }

        Emit(a, b, ref n, inFront);
        Emit(b, c, ref n, inFront);
        Emit(c, a, ref n, inFront);
        if (n < 3) return;

        // The clipped polygon is convex, so a fan from its first vertex is correct.
        for (int i = 1; i + 1 < n; i++)
            DrawClipped(inFront[0], inFront[i], inFront[i + 1], t, v, f, aspect, bandY0, bandY1);
    }

    private static Vtx3 Lerp(Vtx3 x, Vtx3 y, float t)
    {
        return new Vtx3(
            new NfVec3(x.P.X + (y.P.X - x.P.X) * t,
                       x.P.Y + (y.P.Y - x.P.Y) * t,
                       x.P.Z + (y.P.Z - x.P.Z) * t),
            x.U + (y.U - x.U) * t,
            x.V + (y.V - x.V) * t);
    }

    /// World to view. The world is the game's XY plus a height in Y; the view looks along its own
    /// +Z with the eye at the origin.
    ///
    /// Subtracting the EYE HEIGHT is not optional and was missing at first. Without it the camera
    /// sits at floor level, the floor lies exactly on the horizon, and the ground collapses into a
    /// single line: the render came out with the whole lower half of the screen empty.
    private static Vtx3 ToView(Vtx3 v, NfVec2 eye2, float eyeH, float cy, float sy)
    {
        float dx = v.P.X - eye2.X;
        float dz = v.P.Z - eye2.Y;
        float zf = dx * cy + dz * sy;
        // RIGHT is heading minus ninety degrees. Using plus ninety mirrors the world: the picture
        // still looks plausible, because corridors are symmetric, but A and D feel swapped and the
        // mouse pulls the wrong way. This is the second time that sign has bitten this project.
        float xr = dx * sy - dz * cy;
        return new Vtx3(new NfVec3(xr, v.P.Y - eyeH, zf), v.U, v.V);
    }

    // ================================================================================
    /*
     * A PLAYER IS ONLY THERE IF THE TORCH IS ON THEM.
     *
     * Everything else in the world is lit and drawn: a wall the beam has not reached is a dark wall,
     * and that is right, because a room's shape has to be readable or one cannot walk through it.
     * A PERSON is the opposite case. Left at the same rule they came out as grey shapes at ambient
     * brightness anywhere in view - which turns a blackout into a radar and takes the game away from
     * the torch. Aiming the light is supposed to BE the search.
     *
     * So visibility is a separate question from brightness, and it is asked of the cone alone: not
     * of the ambient term (which is everywhere) and not of the near-field spill (which would light
     * anyone within a couple of metres regardless of where the beam points, and so hand back exactly
     * what this is meant to take away). The value below is the cone times its distance falloff, the
     * same numbers the per-pixel lighting uses, so "lit enough to see" and "looks lit" agree.
     *
     * Two consequences that are the point rather than side effects: the werewolf can stand still in
     * the dark two metres away and not be seen, and a crewmate who sweeps the beam across it does
     * see it. And it is symmetric - the beast has no torch, so its own night vision (PredatorVision,
     * a plain distance falloff) decides what IT sees, and that is deliberately more generous.
     *
     * The edges are soft: a figure fades in over the width of the cone's rim rather than blinking
     * into existence, because a hard edge on a moving beam reads as a rendering fault.
     */
    /// 0 = not there at all, 1 = plainly in the beam.
    ///
    /// It is asked of the CORE of the cone, widened by half again for a soft rim, and NOT of the
    /// corona. The corona reaches 3.2 core angles - seventy degrees to a side, most of the screen -
    /// because a room has to be readable; measured against that, every crewmate in view came out
    /// fully lit and the first attempt at this rule changed nothing at all. What a player calls
    /// "the beam" is the bright spot, and the bright spot is the core.
    private float SeenFactor(in ViewParams v, NfVec3 world)
    {
        float dx = world.X - v.Position.X, dz = world.Z - v.Position.Y;
        float dy = world.Y - v.EyeHeight;
        float dist2 = dx * dx + dy * dy + dz * dz;
        float dist = MathF.Sqrt(dist2);
        if (dist < 1e-3f) return 1f;
        float invDist = 1f / dist;

        float dot = (dx * beamX + dz * beamZ) * invDist;
        float t = NfMath.Clamp01((dot - cosSeenOut) * invSeen);
        float angle = t * t * (3f - 2f * t);

        // Twice the torch's range as the hard limit, full strength to seven tenths of it. Past that
        // a figure in the beam is a smudge anyway, and letting it fade there rather than at the
        // rasteriser's view distance keeps the far end of a corridor honestly empty.
        float reach = v.FlashlightRange * 2f;
        float far = NfMath.SmoothStep(reach, reach * 0.7f, dist);

        float seen = angle * far * v.FlashlightPower;

        // ARM'S LENGTH. Someone close enough to touch is not invisible because the torch happens to
        // point elsewhere - one would be looking straight at them, and a shape that walks through
        // you unseen reads as a broken renderer rather than as good cover. Under a metre and never
        // more than half strength, so it is "a shape" and not "identified".
        float near = NfMath.Clamp01((1.0f - dist) / 0.7f) * 0.55f;
        return MathF.Max(seen, near);
    }

    private void DrawBillboards(in ViewParams v, float f, float aspect, float cy, float sy,
                                IReadOnlyList<Billboard> list)
    {
        var eye = v.Position;
        // Persistent buffer, same as propOrder above: this used to be a fresh List<int> plus a
        // capturing sort lambda allocated every frame (AUDIT-2026-08-15).
        billboardOrder.Clear();
        for (int i = 0; i < list.Count; i++) billboardOrder.Add(i);
        billboardSortList = list;
        billboardSortEye = eye;
        billboardSortComparison ??= CompareBillboardsBackToFront;   // one delegate for the whole run
        billboardOrder.Sort(billboardSortComparison);

        foreach (int idx in billboardOrder)
        {
            var bb = list[idx];
            if (bb.Source == null || bb.Fade >= 1f) continue;

            float dx = bb.Position.X - eye.X, dz = bb.Position.Y - eye.Y;
            float zf = dx * cy + dz * sy;
            float xr = dx * sy - dz * cy;
            if (zf <= 0.08f || zf > v.ViewDistance) continue;

            float iz = 1f / zf;
            float cxs = (xr * f / aspect) * iz * 0.5f * Width + Width * 0.5f;
            // Feet at world height bb.Base (a deck, a stair, or the air for a marker), head at
            // Base + Height, both relative to the eye.
            float footY = Height * 0.5f + ((v.EyeHeight - bb.Base) * f) * iz * 0.5f * Height;
            float topY = Height * 0.5f - ((bb.Base + bb.Height - v.EyeHeight) * f) * iz * 0.5f * Height;

            float hPx = footY - topY;
            if (hPx < 2f) continue;
            float wPx = hPx * bb.Source.Width / MathF.Max(1f, bb.Source.Height);

            float viewAngle = MathF.Atan2(-dz, -dx);
            int frame = bb.Source.FrameForAngle(NfMath.WrapAngle(bb.Facing - viewAngle));

            float dist = MathF.Sqrt(dx * dx + dz * dz);
            // Chest height, not the feet: the torch is carried at hip height and a figure lit only
            // where the beam meets the floor is a pair of boots.
            var lookAt = new NfVec3(bb.Position.X, bb.Base + bb.Height * 0.55f, bb.Position.Y);
            float lit = Light(v, lookAt,
                              new NfVec3(-dx / MathF.Max(0.001f, dist), 0f, -dz / MathF.Max(0.001f, dist)));
            if (bb.Glow > 0f) lit = MathF.Max(lit, bb.Glow);
            // Prey runs warm. In predator vision a living figure is lifted to full brightness,
            // so the red tint afterwards maps it to the hot end of the ramp: a heat signature
            // against the cold room, which is what a hunting sense is for.
            if (v.PredatorVision && bb.Glow <= 0f) lit = MathF.Max(lit, 1.12f);

            // Seen at all? See the note above ConeOn. Markers are exempt: they are game
            // information, not people, and a hint that vanishes outside the beam is no hint.
            float alpha = 1f - NfMath.Clamp01(bb.Fade);
            if (!v.PredatorVision && bb.Glow <= 0f) alpha *= SeenFactor(v, lookAt);
            if (alpha <= 0.02f) continue;

            int x0 = Math.Max(0, (int)(cxs - wPx * 0.5f));
            int x1 = Math.Min(Width - 1, (int)(cxs + wPx * 0.5f));
            int y0 = Math.Max(0, (int)topY);
            int y1 = Math.Min(Height - 1, (int)footY);

            for (int x = x0; x <= x1; x++)
            {
                int tx = (int)((x - (cxs - wPx * 0.5f)) * bb.Source.Width / MathF.Max(1f, wPx));
                if (tx < 0 || tx >= bb.Source.Width) continue;

                for (int y = y0; y <= y1; y++)
                {
                    int di = y * Width + x;
                    if (zf >= depth[di]) continue;

                    int ty = (int)((y - topY) * bb.Source.Height / MathF.Max(1f, hPx));
                    if (ty < 0 || ty >= bb.Source.Height) continue;
                    if (!bb.Source.Sample(frame, tx, ty, out var texel, out float mask, out float shadow))
                        continue;

                    var col = texel;
                    if (mask > 0f) col = NfColor.Lerp(col, bb.Color, mask);
                    if (shadow > 0f) col = NfColor.Lerp(col, bb.ShadowColor, shadow);
                    col = Fog(col * lit, zf, v);

                    if (alpha < 0.995f)
                    {
                        int o = di * 4;
                        var dst = new NfColor(Pixels[o] / 255f, Pixels[o + 1] / 255f, Pixels[o + 2] / 255f);
                        col = NfColor.Lerp(dst, col, alpha);
                        // Depth only once the figure is solid enough to hide what is behind it.
                        // A half-faded shape that writes depth punches a hole in the wall it is
                        // standing in front of.
                        if (alpha > 0.5f) depth[di] = zf;
                    }
                    else depth[di] = zf;
                    col.ToBytes(Pixels, di * 4);
                }
            }
        }
    }

    private readonly List<int> billboardOrder = new();
    // `list` and `eye` held as fields, not lambda captures, so billboardSortComparison can be built
    // once and reused every frame instead of closing over them per call (AUDIT-2026-08-15).
    private IReadOnlyList<Billboard> billboardSortList;
    private NfVec2 billboardSortEye;
    private Comparison<int> billboardSortComparison;   // cached on first use, see DrawBillboards

    // Back to front: the farther billboard is drawn first. A named instance method rather than a
    // field initializer, which C# does not allow to read other instance fields.
    private int CompareBillboardsBackToFront(int x, int y) =>
        (billboardSortList[y].Position - billboardSortEye).SqrLength.CompareTo(
        (billboardSortList[x].Position - billboardSortEye).SqrLength);

    // ================================================================================
    /*
     * THE TORCH IS A CONE, NOT A CURTAIN.
     *
     * The beam used to be worked out from the AZIMUTH alone - the compass bearing from the eye to
     * the lit point - and an angle with no elevation in it describes an infinite vertical wedge.
     * That is exactly what the first playtest photographed: a hard-edged bright STRIPE running from
     * the ceiling down the far wall and across the floor, the same width everywhere, in nearly
     * every screenshot. A torch does not do that. A torch throws an ellipse.
     *
     * The full angle needs the height difference as well, and asking for it in radians would mean
     * an acos per pixel. It is not needed: the cosine of the angle IS the dot product of the beam
     * axis with the unit vector to the point, and the cone's thresholds can be held as cosines
     * instead of as angles. So the round cone is CHEAPER than the wedge it replaces - the old one
     * paid an atan2 per pixel, this one pays two multiplies and an add.
     *
     * Cosine runs backwards to angle (a bigger dot is a smaller angle), so every ramp below reads
     * "dark at the outer cosine, bright at the inner one".
     */
    private float beamX, beamZ;
    private float cosOut, cosSkirtOut, invCore, invSkirt;
    /// The separate, much tighter cone that decides whether a PERSON is visible. See SeenFactor.
    private float cosSeenOut, invSeen;

    /// The beam's colour, and the colour of everything the beam does not reach.
    ///
    /// A torch bulb is warm and Polus' night is not, and drawing both with the same white scalar
    /// throws that away: the room came back as one grey, brighter in the middle. Splitting the two
    /// terms costs three multiplies and buys the single most recognisable thing about a torch in
    /// the dark - a warm pool with cold blue-grey around it.
    private static readonly NfColor BeamTint = new(1.00f, 0.955f, 0.870f);
    private static readonly NfColor AmbientTint = new(0.70f, 0.78f, 1.05f);

    private void PrepareBeam(in ViewParams v)
    {
        beamX = MathF.Cos(v.FlashlightDir);
        beamZ = MathF.Sin(v.FlashlightDir);
        float a = MathF.Max(0.02f, v.FlashlightAngle);
        float cosIn = MathF.Cos(a * 0.45f);
        cosOut = MathF.Cos(a);
        // A REAL TORCH IS A HOTSPOT INSIDE A WIDE, DIM CORONA, and the corona is what makes a room
        // readable instead of a keyhole. It used to reach 2,3 times the core angle; that was tuned
        // against the old wedge, which lit a whole floor-to-ceiling stripe and so needed no help.
        // A round cone lights a small fraction of that, so the corona was widened to 3,2 times.
        cosSkirtOut = MathF.Cos(MathF.Min(1.45f, a * 3.2f));
        invCore = 1f / MathF.Max(1e-5f, cosIn - cosOut);
        invSkirt = 1f / MathF.Max(1e-5f, cosOut - cosSkirtOut);

        // Being SEEN needs the core and half again, no more: 22 degrees full, gone by 33.
        cosSeenOut = MathF.Cos(MathF.Min(1.3f, a * 1.5f));
        invSeen = 1f / MathF.Max(1e-5f, cosIn - cosSeenOut);
    }

    /// Splits the light at a point into the part that is ambient (and takes the night's colour) and
    /// the part that comes out of the torch (and takes the bulb's).
    private void Light2(in ViewParams v, NfVec3 world, NfVec3 normal, out float amb, out float beam)
    {
        float dx = world.X - v.Position.X, dz = world.Z - v.Position.Y;
        float dy = world.Y - v.EyeHeight;
        float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (dist < 0.01f) dist = 0.01f;
        float invDist = 1f / dist;

        if (v.PredatorVision)
        {
            // Wider and brighter than a torch could ever be - the beast's whole advantage is
            // that it needs no lamp. The curve radius 3.0 (was 2.2) pushes the readable zone
            // out by roughly a quarter at the same per-pixel cost, and the floor of 0.17 keeps
            // even the far end of a corridor a silhouette rather than a void.
            float pf = 1f / (1f + dist * dist / (v.FlashlightRange * v.FlashlightRange * 3.0f));
            amb = NfMath.Clamp(0.17f + pf * 1.02f, 0f, 1.45f);
            beam = 0f;
            return;
        }

        // cos of the angle between the beam axis and the line of sight to this point.
        float dot = (dx * beamX + dz * beamZ) * invDist;

        float t = NfMath.Clamp01((dot - cosOut) * invCore);
        float core = t * t * (3f - 2f * t);
        float sk = NfMath.Clamp01((dot - cosSkirtOut) * invSkirt);
        float skirt = sk * sk * (3f - 2f * sk) * 0.30f;
        float cone = MathF.Max(core, skirt);

        /*
         * A FLATTER FALLOFF WITH A LOWER PEAK, which is not the same trade as "dimmer".
         *
         * The old curve multiplied by 2,3 and fell as 1/(1+d^2/(R^2/4)). Inside about two metres it
         * therefore ran past the ceiling of 1,45 everywhere at once, and a round cone turns that
         * into a flat white DISC with no shading in it at all - the wall could have been any colour.
         * Halving the peak and doubling the curve's radius keeps almost exactly the same brightness
         * at six to thirteen metres (0,86 against 0,92 at eight) while leaving the near field below
         * the ceiling, so a wall two metres away is bright AND still has its panels.
         */
        float r2 = v.FlashlightRange * v.FlashlightRange * 0.5f;
        float falloff = 1f / (1f + dist * dist / r2);

        // Surfaces turned away from the beam are darker: this is what separates the two walls of a
        // corner without any real lighting model.
        float facing = 0.55f + 0.45f * MathF.Abs(
            (normal.X * dx + normal.Y * dy + normal.Z * dz) * invDist);

        // Spill: the lamp also lights what it is standing in, or the doorway being walked through
        // is invisible. It dies fast enough to give nothing away at a distance.
        float spill = 0.42f / (1f + dist * dist * 0.7f) * v.FlashlightPower;

        amb = v.Ambient;
        beam = MathF.Min(1.45f, spill + cone * falloff * facing * v.FlashlightPower * 1.5f);
    }

    /// Flat scalar, for the few things lit once rather than per pixel.
    private float Light(in ViewParams v, NfVec3 world, NfVec3 normal)
    {
        Light2(v, world, normal, out float a, out float b);
        return NfMath.Clamp(a + b, 0f, 1.45f);
    }

    private static NfColor Fog(NfColor c, float dist, in ViewParams v)
    {
        float t = NfMath.SmoothStep(v.ViewDistance * 0.45f, v.ViewDistance, dist);
        return t <= 0f ? c : NfColor.Lerp(c, v.FogColor, t);
    }

    private static float Edge(float ax, float ay, float bx, float by, float cx, float cy) =>
        (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

    /// Narrows [lo, hi] to where `a + b*t >= 0`. False when nothing is left, which is a scanline
    /// the triangle does not reach at all.
    private static bool Clip(float a, float b, ref float lo, ref float hi)
    {
        if (b > 1e-9f)
        {
            float t = -a / b;
            if (t > lo) lo = t;
        }
        else if (b < -1e-9f)
        {
            float t = -a / b;
            if (t < hi) hi = t;
        }
        else if (a < 0f) return false;      // the whole row is on the wrong side of this edge
        return lo <= hi;
    }
}
