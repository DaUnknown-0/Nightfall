// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * FrameRenderer - draws one first-person frame of an Among Us map into an RGBA byte buffer.
 *
 * It is a raycaster in the Wolfenstein sense, chosen over real 3D geometry for reasons that are
 * specific to this environment rather than nostalgia:
 *
 *   - It needs exactly one capability from the host: "put this byte array on the screen". That
 *     capability is proven three times over in this mod family (procedural Texture2D on a full
 *     screen element). Real 3D would need runtime meshes, a shader that survives Il2Cpp stripping,
 *     depth ordering against a game that has none, and an asset pipeline. Every one of those is an
 *     unknown; none of them is an unknown here.
 *   - It brings its own depth buffer, in the form of one float per screen column. Sprite occlusion,
 *     which is the hard part of drawing players into a 3D scene, becomes a single comparison.
 *   - The flashlight is not a light source at all, it is a brightness function of angle and
 *     distance evaluated per pixel. Cone, falloff, flicker and the werewolf's red night vision are
 *     all the same three lines of arithmetic.
 *
 * PIPELINE PER FRAME
 * ------------------
 *   1. sky and floor, row by row (horizontal coherence: one distance per row, world position
 *      interpolated across it, so the expensive part happens 180 times instead of 57.600),
 *   2. walls, column by column (one ray each, plus a second ray for waist-high furniture that
 *      stands in front of the wall behind it),
 *   3. sprites, back to front, tested against the column depth buffer,
 *   4. the held flashlight and the vignette, which are pure screen-space.
 *
 * COORDINATES
 * -----------
 * World space is Among Us world space: X right, Y up, one unit is about one crewmate width. The
 * view lives at EyeHeight above the floor and walls rise to WallHeight, both in those same units.
 * Screen space is y-down, origin top left, which is what both Unity's RGBA32 upload and the offline
 * PNG writer expect.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nightfall.Core;

public struct ViewParams
{
    public NfVec2 Position;
    /// Where the camera looks, in radians, counter-clockwise from +X.
    public float Heading;

    /// How far the view is tilted, in radians, positive UP. It is not a camera rotation: the
    /// renderer shifts the HORIZON by tan(Pitch) instead (Raster3D.HorizonY), the way the
    /// software renderers of the nineties did, because every column of this rasteriser is an
    /// azimuth and a real rotation would have to give that up. Vertical edges stay vertical.
    public float Pitch;
    /// Horizontal field of view in radians.
    public float Fov;
    /// Camera height above the floor, in world units.
    public float EyeHeight;
    /// How tall a full wall stands, in world units.
    public float WallHeight;
    /// How tall waist-high furniture stands.
    public float LowHeight;

    /// Absolute direction the torch points, in radians. Separate from Heading because the mouse
    /// aims the beam independently of where the body faces.
    public float FlashlightDir;
    /// Half angle of the bright core of the beam, in radians.
    public float FlashlightAngle;
    public float FlashlightRange;
    /// 0..1 overall strength. Drops as the beam flickers.
    public float FlashlightPower;

    /// How much light there is with the torch off. Very small: this is a blackout.
    public float Ambient;
    /// Everything past this is fog and then nothing.
    public float ViewDistance;
    /// Colour the fog fades into. Outdoors this is the night sky, indoors near black.
    public NfColor FogColor;

    /// Seconds since the transformation, used for flicker and for the sky.
    public float Time;

    /// The beast sees differently: no torch, a wide red-shifted night vision that reaches further.
    public bool PredatorVision;

    public static ViewParams Default => new()
    {
        Fov = 75f * NfMath.Pi / 180f,
        EyeHeight = 0.62f,
        WallHeight = 1.75f,
        LowHeight = 0.62f,
        FlashlightAngle = 22f * NfMath.Pi / 180f,
        FlashlightRange = 13f,
        FlashlightPower = 1f,
        Ambient = 0.075f,
        ViewDistance = 42f,
        FogColor = new NfColor(0.05f, 0.04f, 0.09f),
    };
}

/// One player (or other actor) drawn into the scene as an upright sprite.
public struct Billboard
{
    public NfVec2 Position;
    /// Which way the actor faces, so the eight-direction sprite set can pick a view.
    public float Facing;
    /// Sprite source. Height is in world units.
    public IBillboardSource Source;
    public float Height;
    /// Tint applied to the sprite's colour mask (the player colour).
    public NfColor Color;
    /// Secondary tint (the darker shade Among Us uses for the crewmate's underside).
    public NfColor ShadowColor;
    /// 0 = fully visible, 1 = invisible. Used for fading corpses and the transformation itself.
    public float Fade;
    /// World height of the actor's feet. 0 is the old behaviour (feet on the reference floor);
    /// anything standing on a deck, a stair or hovering as a marker sets its real base here.
    public float Base;
    /// 0 = a person: lit by the torch, subject to the visibility cone. Above 0 = a self-lit
    /// marker: never darker than this, and exempt from the cone - a direction hint is game
    /// information the player owns, not a figure that must disappear in the dark.
    public float Glow;
}

/// Anything that can supply pixels for a billboard. Implemented by the procedural crewmate, and
/// deliberately an interface so a future actor (a corpse, a vent, a relic) can be drawn the same
/// way without the renderer growing a special case.
public interface IBillboardSource
{
    int Width { get; }
    int Height { get; }
    /// Picks the frame for a given view angle (the angle from the viewer to the actor's facing).
    int FrameForAngle(float relativeAngle);
    /// Samples one texel. Returns false when the texel is transparent.
    bool Sample(int frame, int x, int y, out NfColor color, out float colorMaskWeight,
                out float shadowMaskWeight);
}

public sealed class FrameRenderer
{
    /// How much of the procedural surface survives once the real map colour is available. Low on
    /// purpose: the artwork is the subject, the relief is the lighting.
    /// How much of the procedural surface survives once the real map colour is available.
    ///
    /// Raised back up after testing proved the honest limit of the photograph: Among Us has NO
    /// artwork for a vertical wall face, because a top-down game never needs one. What looks like a
    /// wall face in the map art is a stroke a few pixels wide, which stretched over a full wall is
    /// a smear. So the division of labour is: the photograph supplies the COLOUR, which is real and
    /// per-room, and the procedural surface supplies the STRUCTURE, which has to be built because
    /// the game simply does not contain it.
    private const float ReliefStrength = 0.78f;

    /// How far out from a wall's line its drawn face extends in the map artwork, in world units.
    /// Among Us draws its walls at a consistent apparent thickness, so one number covers every map.
    private const float WallFaceDepth = 0.42f;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// RGBA32, row-major from the TOP row down.
    public byte[] Pixels { get; private set; } = Array.Empty<byte>();

    /// Perpendicular wall distance per column. The depth buffer for sprites.
    private float[] columnDepth = Array.Empty<float>();
    /// Screen row where the floor starts for each column, so sprites can be planted on the floor.
    private float[] columnFloorY = Array.Empty<float>();

    private float[] light = Array.Empty<float>();   // scratch: per-pixel light, reused each frame

    // ---- ray results, kept between the two phases of a frame ----
    // Casting happens on ONE thread and shading on many. The raycaster carries a per-segment visit
    // stamp that is not thread safe, and splitting the frame this way avoids that entirely: after
    // the cast phase there is nothing shared left to write, only pixels at disjoint addresses.
    private RayHit[] wallHits = Array.Empty<RayHit>();
    private RayHit[] lowHits = Array.Empty<RayHit>();
    private bool[] hasWall = Array.Empty<bool>();
    private bool[] hasLow = Array.Empty<bool>();
    private float[] rayLen = Array.Empty<float>();
    /// Flashlight cone strength per screen COLUMN.
    ///
    /// Every point drawn in a column lies on that column's ray, so the angle between it and the
    /// beam is the same for all of them. Computing it per pixel meant half a million atan2 calls a
    /// frame at 960 wide and was by far the most expensive thing in the renderer; computing it
    /// once per column makes it 960.
    private float[] coneFactor = Array.Empty<float>();

    /// Rendering across threads. Off means one core, which is what the offline tool wants for
    /// reproducible timings and what a single-core machine wants for stability.
    public bool Multithreaded = true;

    public void Resize(int width, int height)
    {
        if (Width == width && Height == height && Pixels.Length > 0) return;
        Width = Math.Max(16, width);
        Height = Math.Max(16, height);
        Pixels = new byte[Width * Height * 4];
        columnDepth = new float[Width];
        columnFloorY = new float[Width];
        light = new float[Width * Height];
        wallHits = new RayHit[Width];
        lowHits = new RayHit[Width];
        hasWall = new bool[Width];
        hasLow = new bool[Width];
        rayLen = new float[Width];
        coneFactor = new float[Width];
    }

    // ================================================================================
    // Render
    // ================================================================================
    public void Render(MapModel map, in ViewParams view, IReadOnlyList<Billboard> billboards = null)
    {
        TextureBank.EnsureBuilt();
        if (Pixels.Length == 0) Resize(320, 180);

        // The projection: half the screen height corresponds to tan(vfov/2) at unit distance.
        // Deriving the vertical fov from the horizontal one and the aspect keeps the picture from
        // stretching when the host window is not 16:9.
        float aspect = Width / (float)Height;
        float tanHalfH = MathF.Tan(view.Fov * 0.5f);
        float tanHalfV = tanHalfH / aspect;
        float projScale = (Height * 0.5f) / tanHalfV;

        var dir = NfVec2.FromAngle(view.Heading);
        // The camera plane, scaled so that camX in -1..1 spans exactly the horizontal fov. Because
        // the ray is dir + plane*camX (and NOT normalised), the intersection parameter t comes back
        // as the PERPENDICULAR distance already: the fisheye correction is built into the geometry
        // instead of being applied afterwards.
        //
        // The sign matters and was wrong at first. (-dir.Y, dir.X) rotates the heading LEFT, so
        // camX = -1 (the left edge of the screen) produced a ray pointing right: the whole picture
        // came out mirrored. Nothing looked broken - corridors are symmetric - but A and D felt
        // swapped and the mouse pulled the wrong way, because the world really was inside out.
        // (dir.Y, -dir.X) rotates right, which puts the left edge of the screen on the player's left.
        var plane = new NfVec2(dir.Y, -dir.X) * tanHalfH;

        float horizon = Height * 0.5f;

        // Phase 1, single threaded: every ray is cast and its result stored. This is the only part
        // of a frame that touches shared mutable state (the raycaster's visit stamps), and it is
        // also the cheap part - a few hundred rays against a grid-indexed segment list.
        CastRays(map, view, dir, plane);

        // Phases 2 and 3, parallel: shading. Each row of floor and each column of wall writes to
        // its own pixels and reads nothing that changes, so the work splits across cores with no
        // locking at all. This is what makes a 960 wide render affordable.
        DrawSkyAndFloor(map, view, dir, plane, projScale, horizon);
        DrawWalls(map, view, projScale, horizon);
        if (billboards != null && billboards.Count > 0)
            DrawBillboards(map, view, dir, plane, projScale, horizon, billboards);
        // Keep the shared helper in step with this renderer's own threading switch (AUDIT L-27).
        HandLight.Multithreaded = Multithreaded;
        if (view.PredatorVision) HandLight.PredatorTint(Pixels);
        DrawHeldLight(view);
        DrawVignette();
    }

    // ================================================================================
    // 1. Sky and floor
    // ================================================================================
    private void DrawSkyAndFloor(MapModel map, in ViewParams view, NfVec2 dir, NfVec2 plane,
                                 float projScale, float horizon)
    {
        // The leftmost and rightmost rays. Every row's world span is a lerp between these two, which
        // is what turns per-pixel floor casting into per-row floor casting.
        var rayLeft = dir - plane;
        var rayRight = dir + plane;

        bool viewerInside = map.IsInside(view.Position);
        var v = view;                                  // `in` cannot be captured by a lambda

        RunParallel(Height, y =>
        {
            if (y <= horizon)
            {
                DrawCeilingRow(map, v, dir, plane, projScale, horizon, y, viewerInside, rayLeft, rayRight);
                return;
            }

            // Distance to the floor point on this row: the camera sits EyeHeight above the floor,
            // and a row p pixels below the horizon looks down at angle p/projScale.
            float p = y - horizon;
            float rowDistance = v.EyeHeight * projScale / p;
            if (rowDistance > v.ViewDistance) rowDistance = v.ViewDistance;

            var worldLeft = v.Position + rayLeft * rowDistance;
            var worldRight = v.Position + rayRight * rowDistance;
            float stepX = (worldRight.X - worldLeft.X) / Width;
            float stepY = (worldRight.Y - worldLeft.Y) / Width;

            float wx = worldLeft.X, wy = worldLeft.Y;
            int rowBase = y * Width;

            // The floor is the one surface the map photograph describes EXACTLY: it is a top-down
            // image of exactly this. Where the photograph reaches, it wins over anything procedural,
            // and that single fact is most of what makes the view read as the real Polus rather
            // than as a corridor that resembles it.
            var atlas = map.Atlas;
            bool useAtlas = atlas != null && atlas.IsValid;

            // One sampler state per ROW, owned by this call (AUDIT M-20). A row is exactly one
            // coherent run of samples - the footprint Sample derives comes from the step between
            // consecutive pixels of this row - and rows are what the parallel split hands out, so
            // no two threads can ever touch the same state.
            var floorSampler = SamplerState.Fresh;

            for (int x = 0; x < Width; x++, wx += stepX, wy += stepY)
            {
                var wp = new NfVec2(wx, wy);
                float r, g, b;

                if (!useAtlas || !atlas.SampleBilinear(wx, wy, out r, out g, out b))
                {
                    var surf = TextureBank.Get(map.FloorAt(wp));
                    float u = wx / surf.WorldScale;
                    float v = wy / surf.WorldScale;
                    surf.Sample(u - MathF.Floor(u), v - MathF.Floor(v), ref floorSampler, out r, out g, out b);
                }

                // Floors are lit a touch darker than walls: the torch points forward, not down, and
                // a fully lit floor makes the whole scene read as flat.
                float lit = LightAt(v, coneFactor[x], rowDistance) * 0.85f;
                var c = ApplyFog(new NfColor(r * lit, g * lit, b * lit), rowDistance, v);
                c.ToBytes(Pixels, (rowBase + x) * 4);
            }
        });
    }

    private void DrawCeilingRow(MapModel map, ViewParams view, NfVec2 dir, NfVec2 plane,
                                float projScale, float horizon, int y, bool viewerInside,
                                NfVec2 rayLeft, NfVec2 rayRight)
    {
        int rowBase = y * Width;

        // Indoors the ceiling mirrors the floor: same cast, same textures, dimmer. Outdoors there
        // is no ceiling at all, and Polus is a night sky - which is most of the map's area, and the
        // reason the outdoor case gets its own treatment rather than a black band.
        float p = horizon - y;
        float rowDistance = p < 0.5f ? view.ViewDistance
                                     : (view.WallHeight - view.EyeHeight) * projScale / p;
        bool useSky = !viewerInside || rowDistance > view.ViewDistance;

        if (!useSky)
        {
            var worldLeft = view.Position + rayLeft * rowDistance;
            var worldRight = view.Position + rayRight * rowDistance;
            float stepX = (worldRight.X - worldLeft.X) / Width;
            float stepY = (worldRight.Y - worldLeft.Y) / Width;
            float wx = worldLeft.X, wy = worldLeft.Y;

            // Per-row sampler state, same reasoning as the floor row above (AUDIT M-20).
            var ceilingSampler = SamplerState.Fresh;

            for (int x = 0; x < Width; x++, wx += stepX, wy += stepY)
            {
                var wp = new NfVec2(wx, wy);
                // A ceiling over open ground would float in mid air, so the sky shows through
                // wherever the point below is outdoors.
                if (!map.IsInside(wp))
                {
                    SkyPixel(view, dir, plane, x, y, horizon).ToBytes(Pixels, (rowBase + x) * 4);
                    continue;
                }
                var surf = TextureBank.Get(SurfaceKind.MetalPanel);
                float u = wx / (surf.WorldScale * 2f), v = wy / (surf.WorldScale * 2f);
                surf.Sample(u - MathF.Floor(u), v - MathF.Floor(v), ref ceilingSampler,
                            out float r, out float g, out float b);
                float lit = LightAt(view, coneFactor[x], rowDistance) * 0.45f;
                var c = ApplyFog(new NfColor(r * lit, g * lit, b * lit), rowDistance, view);
                c.ToBytes(Pixels, (rowBase + x) * 4);
            }
            return;
        }

        for (int x = 0; x < Width; x++)
            SkyPixel(view, dir, plane, x, y, horizon).ToBytes(Pixels, (rowBase + x) * 4);
    }

    /// The Polus night: a deep violet gradient, a scatter of stars fixed to the world (so turning
    /// your head really does sweep past them) and a faint aurora band near the horizon.
    private NfColor SkyPixel(in ViewParams view, NfVec2 dir, NfVec2 plane, int x, int y, float horizon)
    {
        float camX = 2f * x / Width - 1f;
        var ray = dir + plane * camX;
        float angle = MathF.Atan2(ray.Y, ray.X);

        float elevation = NfMath.Clamp01((horizon - y) / MathF.Max(1f, horizon));

        var deep = new NfColor(0.035f, 0.02f, 0.075f);
        var low = new NfColor(0.14f, 0.08f, 0.20f);
        var c = NfColor.Lerp(low, deep, elevation);

        // Aurora, just above the horizon.
        float aurora = NfMath.SmoothStep(0f, 0.35f, elevation) * NfMath.SmoothStep(0.7f, 0.3f, elevation);
        float wave = NfMath.Fbm(angle * 3.5f + view.Time * 0.06f, elevation * 4f, 3, 91);
        c = c + new NfColor(0.02f, 0.10f, 0.07f) * (aurora * wave * 0.8f);

        // Stars: hashed on a grid in (angle, elevation) so they hold still in the world.
        int sx = (int)(angle * 220f), sy = (int)(elevation * 260f);
        float h = NfMath.Hash(sx, sy, 4711);
        if (h > 0.9955f)
        {
            float twinkle = 0.65f + 0.35f * MathF.Sin(view.Time * 2.3f + h * 100f);
            float mag = (h - 0.9955f) / 0.0045f;
            c = c + NfColor.White * (0.35f + 0.65f * mag) * twinkle;
        }
        return c;
    }

    // ================================================================================
    // 2. Walls
    // ================================================================================
    /// Phase 1: cast every column's rays and remember what they found. Single threaded by design.
    private void CastRays(MapModel map, in ViewParams view, NfVec2 dir, NfVec2 plane)
    {
        var geo = map.Geometry;

        for (int x = 0; x < Width; x++)
        {
            float camX = 2f * x / Width - 1f;
            var ray = dir + plane * camX;
            rayLen[x] = ray.Length;
            coneFactor[x] = ConeAt(view, MathF.Atan2(ray.Y, ray.X));

            columnDepth[x] = view.ViewDistance;
            columnFloorY[x] = Height;

            hasWall[x] = geo.Raycast(view.Position, ray, view.ViewDistance, out wallHits[x],
                                     ignoreLow: true);
            if (hasWall[x]) columnDepth[x] = wallHits[x].Distance;

            // Then anything waist-high standing in front of it.
            hasLow[x] = geo.Raycast(view.Position, ray, MathF.Min(view.ViewDistance, columnDepth[x]),
                                    out lowHits[x], ignoreLow: false)
                        && lowHits[x].Height == WallHeight.Low
                        && lowHits[x].Distance < columnDepth[x];
            if (hasLow[x]) columnDepth[x] = lowHits[x].Distance;
        }
    }

    /// Phase 3: shade the columns from the stored hits.
    private void DrawWalls(MapModel map, in ViewParams view, float projScale, float horizon)
    {
        var v = view;                                  // `in` cannot be captured by a lambda
        RunParallelWithSampler(Width, (x, sampler) =>
        {
            if (hasWall[x])
                DrawWallColumn(map, v, x, wallHits[x], wallHits[x].Distance, rayLen[x],
                               projScale, horizon, v.WallHeight, isLow: false, sampler);
            if (hasLow[x])
                DrawWallColumn(map, v, x, lowHits[x], lowHits[x].Distance, rayLen[x],
                               projScale, horizon, v.LowHeight, isLow: true, sampler);
        });
    }

    /// One place that decides whether work is split across cores, so switching it off for
    /// debugging or on a single-core machine is one field rather than a rewrite.
    private void RunParallel(int count, Action<int> body)
    {
        if (!Multithreaded || count < 64)
        {
            for (int i = 0; i < count; i++) body(i);
            return;
        }
        Parallel.For(0, count, body);
    }

    /// A mutable SamplerState with an identity, so a worker can carry it from one wall column to
    /// the next. A bare struct cannot be captured by a lambda and passed by ref; a one-field class
    /// can, and it is allocated once per WORKER, not per column.
    private sealed class SamplerBox { public SamplerState S = SamplerState.Fresh; }

    /// RunParallel for work that samples textures (AUDIT-2026-08-23, M-20).
    ///
    /// Surface.Sample derives its texture footprint from the step between consecutive uv, and for
    /// wall columns the horizontal half of that footprint is measured BETWEEN columns - so the state
    /// has to survive from one column to the next, and may not be shared between threads. Both at
    /// once is exactly what a thread-local gives: Parallel.For hands each worker its own box via
    /// localInit and keeps it for the whole range that worker processes.
    ///
    /// The remaining imprecision is at partition boundaries, where a worker's first column has no
    /// predecessor and falls back to the default footprint - one column per worker, against the
    /// previous behaviour of every thread corrupting every other thread's measurement.
    private void RunParallelWithSampler(int count, Action<int, SamplerBox> body)
    {
        if (!Multithreaded || count < 64)
        {
            var single = new SamplerBox();
            for (int i = 0; i < count; i++) body(i, single);
            return;
        }
        Parallel.For(0, count,
                     () => new SamplerBox(),
                     (i, _, box) => { body(i, box); return box; },
                     _ => { });
    }

    private void DrawWallColumn(MapModel map, in ViewParams view, int x, in RayHit hit, float perpDist,
                                float rayLen, float projScale, float horizon,
                                float wallHeight, bool isLow, SamplerBox sampler)
    {
        if (perpDist < 0.01f) perpDist = 0.01f;

        // Screen extent of a wall standing from the floor up to wallHeight.
        float top = horizon - (wallHeight - view.EyeHeight) * projScale / perpDist;
        float bottom = horizon + view.EyeHeight * projScale / perpDist;

        int y0 = Math.Max(0, (int)MathF.Floor(top));
        int y1 = Math.Min(Height - 1, (int)MathF.Ceiling(bottom));
        if (y1 < y0) return;

        if (!isLow) columnFloorY[x] = bottom;

        var surf = TextureBank.Get(hit.Material);
        float u = hit.U / surf.WorldScale;
        u -= MathF.Floor(u);

        // COLOUR COMES FROM THE MAP, PER COLUMN.
        //
        // The first version tinted a whole wall with one averaged colour, and it showed: long walls
        // came out as flat slabs and the windows, hazard stripes and painted panels that are drawn
        // ON those walls were averaged away. Sampling the photograph at the exact point the ray hit
        // means a window in the artwork is a window on the wall, and a stripe is a stripe, because
        // the picture is being read at the same place the player is looking.
        //
        // The sample is nudged a little back along the ray, towards the viewer, so it lands on the
        // near face of the wall stroke rather than on whatever lies behind it.
        // THE WALL'S REAL FACE.
        //
        // Among Us does not draw its maps straight down: they are drawn from slightly in front, so
        // every wall has a visible FACE in the artwork, a band of pixels lying next to its line
        // showing panels, windows, hazard stripes and trim. That band is a picture of the very
        // surface this column is about to draw.
        //
        // So the column is not tinted with one colour: it READS that band. Walking outwards along
        // the wall's normal by up to WallFaceDepth maps the drawn face onto the standing wall, top
        // of the wall at the line itself and bottom of the wall at the outer edge of the band. The
        // result is not a texture that resembles Polus, it is Polus' own wall art.
        // Colour is read ONCE per column, from inside the wall's own stroke in the map artwork.
        // Reading it per pixel walked across the stroke and picked up whatever lay behind the wall
        // at the bottom of the sweep; one sample at a fixed depth stays on the wall itself and is
        // both stabler and cheaper.
        var atlas = map.Atlas;
        float meanLum = SurfaceStats.Mean(surf);
        bool tinted = false;
        float tr = 0f, tg = 0f, tb = 0f;

        if (atlas != null && atlas.IsValid)
        {
            const float intoWall = -WallFaceDepth * 0.45f;
            tinted = atlas.SampleArea(hit.Point.X + hit.Normal.X * intoWall,
                                      hit.Point.Y + hit.Normal.Y * intoWall,
                                      0.12f, out tr, out tg, out tb);
        }
        if (!tinted && hit.HasTint)
        {
            tinted = true;
            tr = hit.TintR / 255f; tg = hit.TintG / 255f; tb = hit.TintB / 255f;
        }

        // The true distance to the surface, for lighting. perpDist is the projected one, which is
        // right for the geometry and wrong for the falloff.
        float trueDist = perpDist * rayLen;
        float lit = LightAt(view, coneFactor[x], trueDist);
        if (hit.Backface) lit *= surf.BackfaceTint;
        // Low furniture catches the beam from above, so it reads slightly brighter than a wall at
        // the same distance. Small touch, but it is what stops consoles from melting into the wall.
        if (isLow) lit *= 1.12f;

        float span = bottom - top;
        for (int y = y0; y <= y1; y++)
        {
            float v = (y - top) / MathF.Max(1e-4f, span);
            if (v < 0f || v > 1f) continue;

            surf.Sample(u, v, ref sampler.S, out float r, out float g, out float b);

            if (tinted)
            {
                // The invented surface is now only a whisper of relief on top of the real colour.
                // At full strength it was the loudest thing in the picture and read as noise laid
                // over the map, which is the opposite of "the map, one to one".
                float relief = (r * 0.3f + g * 0.6f + b * 0.1f) / meanLum;
                relief = 1f + (NfMath.Clamp(relief, 0.4f, 1.8f) - 1f) * ReliefStrength;

                // A vertical gradient does the work the relief used to: darker where the wall meets
                // the floor, a touch brighter at eye height. It is what gives a flat colour the
                // feeling of a surface standing in a room, and it costs one multiply.
                float shade = 0.80f + 0.30f * NfMath.SmoothStep(1f, 0.15f, v);

                float k = relief * shade;
                r = tr * k;
                g = tg * k;
                b = tb * k;
            }

            var c = ApplyFog(new NfColor(r * lit, g * lit, b * lit), trueDist, view);
            c.ToBytes(Pixels, (y * Width + x) * 4);
        }
    }

    // ================================================================================
    // 3. Billboards
    // ================================================================================
    // `list` and `eye` are held as fields, not lambda captures, so billboardSortComparison can be
    // built once instead of once per frame. Mirrors Raster3D's own billboard sort.
    private readonly List<int> billboardOrder = new List<int>();
    private IReadOnlyList<Billboard> billboardSortList;
    private NfVec2 billboardSortEye;
    private Comparison<int> billboardSortComparison;

    private int CompareBillboardsBackToFront(int x, int y) =>
        (billboardSortList[y].Position - billboardSortEye).SqrLength.CompareTo(
        (billboardSortList[x].Position - billboardSortEye).SqrLength);

    private void DrawBillboards(MapModel map, in ViewParams view, NfVec2 dir, NfVec2 plane,
                                float projScale, float horizon, IReadOnlyList<Billboard> list)
    {
        // Back to front, so a nearer crewmate covers a further one.
        //
        // Persistent buffer and a cached comparison delegate (AUDIT-2026-08-23, L-27) - the same
        // shape Raster3D.DrawBillboards already uses. This used to allocate a fresh List<int> AND a
        // capturing sort lambda on every single frame; the capture is what made it unavoidable, so
        // the two captured values (the list and the eye) are held as fields instead and the
        // delegate is built once for the whole run.
        billboardSortList = list;
        billboardSortEye = view.Position;
        billboardOrder.Clear();
        for (int i = 0; i < list.Count; i++) billboardOrder.Add(i);
        billboardSortComparison ??= CompareBillboardsBackToFront;
        billboardOrder.Sort(billboardSortComparison);

        float invDet = 1f / (plane.X * dir.Y - dir.X * plane.Y);

        foreach (int idx in billboardOrder)
        {
            var bb = list[idx];
            if (bb.Source == null || bb.Fade >= 1f) continue;

            var rel = bb.Position - view.Position;

            // Into camera space: transformX is the sideways offset, transformY the depth. Standard
            // inverse of the [plane | dir] matrix.
            float transformX = invDet * (dir.Y * rel.X - dir.X * rel.Y);
            float transformY = invDet * (-plane.Y * rel.X + plane.X * rel.Y);
            if (transformY <= 0.05f) continue;                 // behind the camera
            if (transformY > view.ViewDistance) continue;

            int screenX = (int)((Width * 0.5f) * (1f + transformX / transformY));

            float spriteWorldH = bb.Height;
            int spriteH = (int)(spriteWorldH * projScale / transformY);
            if (spriteH < 2) continue;
            int spriteW = (int)(spriteH * bb.Source.Width / (float)bb.Source.Height);
            if (spriteW < 1) continue;

            // Feet at bb.Base above the floor: the bottom edge sits where that height is at this
            // distance (Base is 0 for people on this renderer's flat world; markers hover).
            float bottom = horizon + (view.EyeHeight - bb.Base) * projScale / transformY;
            int y0 = (int)(bottom - spriteH);
            int y1 = (int)bottom;

            // Which of the eight views: the angle between where the actor faces and where we see
            // it from. Subtracting the viewing angle is what makes a crewmate walking away show
            // you its back.
            float viewAngle = MathF.Atan2(-rel.Y, -rel.X);
            int frame = bb.Source.FrameForAngle(NfMath.WrapAngle(bb.Facing - viewAngle));

            float trueDist = rel.Length;
            // One cone evaluation per figure rather than per column: a crewmate is small enough on
            // screen that the angle across it is negligible, and it keeps the sprite lit evenly
            // instead of banding down its side.
            float lit = LightAt(view, ConeAt(view, MathF.Atan2(rel.Y, rel.X)), trueDist);
            if (bb.Glow > 0f) lit = MathF.Max(lit, bb.Glow);
            // Prey runs warm in predator vision: see Raster3D.DrawBillboards.
            if (view.PredatorVision && bb.Glow <= 0f) lit = MathF.Max(lit, 1.12f);

            int xStart = Math.Max(0, screenX - spriteW / 2);
            int xEnd = Math.Min(Width - 1, screenX + spriteW / 2);

            for (int x = xStart; x <= xEnd; x++)
            {
                // The depth buffer: a sprite behind a wall is simply not drawn.
                if (transformY >= columnDepth[x]) continue;

                int tx = (int)((x - (screenX - spriteW / 2f)) * bb.Source.Width / (float)spriteW);
                if (tx < 0 || tx >= bb.Source.Width) continue;

                for (int y = Math.Max(0, y0); y <= Math.Min(Height - 1, y1); y++)
                {
                    int ty = (int)((y - y0) * bb.Source.Height / (float)spriteH);
                    if (ty < 0 || ty >= bb.Source.Height) continue;

                    if (!bb.Source.Sample(frame, tx, ty, out var texel, out float maskW, out float shadowW))
                        continue;

                    // The sprite carries a colour MASK rather than baked colours, so one procedural
                    // crewmate serves all twelve player colours plus anything a mod invents.
                    var col = texel;
                    if (maskW > 0f) col = NfColor.Lerp(col, bb.Color, maskW);
                    if (shadowW > 0f) col = NfColor.Lerp(col, bb.ShadowColor, shadowW);

                    col = col * lit;
                    col = ApplyFog(col, trueDist, view);

                    int o = (y * Width + x) * 4;
                    if (bb.Fade > 0f)
                    {
                        var dst = new NfColor(Pixels[o] / 255f, Pixels[o + 1] / 255f, Pixels[o + 2] / 255f);
                        col = NfColor.Lerp(col, dst, bb.Fade);
                    }
                    col.ToBytes(Pixels, o);
                }
            }
        }
    }

    // ================================================================================
    // 4. Screen space: the torch in hand, and the vignette. Both live in HandLight now, because
    // Raster3D needs exactly the same two and a held torch drawn twice is a held torch that drifts.
    // ================================================================================
    private void DrawHeldLight(in ViewParams view) => HandLight.Draw(Pixels, Width, Height, view);
    private void DrawVignette() => HandLight.Vignette(Pixels, Width, Height);

    // ================================================================================
    // Lighting
    // ================================================================================
    /// The whole lighting model: a cone, a falloff, and a floor of ambient. It is evaluated per
    /// pixel and costs an atan2 and a few multiplies, which is why the torch can be a real torch
    /// instead of a texture overlay.
    /// The angular half of the lighting model, evaluated once per column.
    private static float ConeAt(in ViewParams view, float rayAngle)
    {
        if (view.PredatorVision) return 1f;
        float off = MathF.Abs(NfMath.WrapAngle(rayAngle - view.FlashlightDir));

        // Bright core, then a soft skirt that reaches about twice as wide at a quarter of the
        // strength. Without the skirt the beam looks like a stencil rather than a lamp.
        float core = NfMath.SmoothStep(view.FlashlightAngle, view.FlashlightAngle * 0.45f, off);
        float skirt = NfMath.SmoothStep(view.FlashlightAngle * 2.3f, view.FlashlightAngle, off) * 0.28f;
        return MathF.Max(core, skirt);
    }

    /// The distance half, evaluated per pixel with the column's cone handed in.
    private static float LightAt(in ViewParams view, float cone, float distance)
    {
        if (view.PredatorVision)
        {
            // The beast needs no torch: it sees everywhere, just less far and with a hard falloff,
            // so it still hunts by movement rather than reading the room at a glance.
            // Same curve as the built world's renderer: radius 3.0, floor 0.17.
            float pf = 1f / (1f + distance * distance / (view.FlashlightRange * view.FlashlightRange * 3.0f));
            return NfMath.Clamp(0.17f + pf * 1.02f, 0f, 1.45f);
        }

        // The falloff curve was retuned after the first offline renders came out uniformly murky.
        // A physically even falloff is dramatically wrong: a real torch BLOWS OUT what is close and
        // loses everything far, and it is that contrast, not the average brightness, that makes a
        // corridor frightening. So the curve falls off faster and the peak is allowed to overshoot
        // into clipping.
        float range = view.FlashlightRange;
        float falloff = 1f / (1f + (distance * distance) / (range * range * 0.25f));

        // Spill: a real torch also lights what it is standing in. Without it, everything outside
        // the beam is equally black no matter how close, which reads as a stencil laid over the
        // screen rather than as a lamp in a room - and it makes the doorway you are walking through
        // invisible. It falls off fast enough to give nothing away at a distance.
        float spill = 0.5f / (1f + distance * distance * 0.7f) * view.FlashlightPower;

        return NfMath.Clamp(view.Ambient + spill
                            + cone * falloff * view.FlashlightPower * 2.3f, 0f, 1.45f);
    }

    private static NfColor ApplyFog(NfColor c, float distance, in ViewParams view)
    {
        float t = NfMath.SmoothStep(view.ViewDistance * 0.45f, view.ViewDistance, distance);
        if (t <= 0f) return c;
        return NfColor.Lerp(c, view.FogColor, t);
    }
}
