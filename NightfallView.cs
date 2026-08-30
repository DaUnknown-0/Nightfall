// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightfallView - puts the rendered frame on the screen.
 *
 * HOW THE PICTURE GETS THERE
 * --------------------------
 * A single full-screen SpriteRenderer, parented to the main camera. Not a ScreenSpaceOverlay
 * canvas, which was the obvious choice and the wrong one: an overlay canvas draws after every
 * camera in the game and would bury the HUD along with the world. Among Us renders the world with
 * "Main Camera" and the entire HUD with a separate "UI Camera" at a higher depth (verified in the
 * survey: depth -1 versus 99/100), so a sprite parented to the world camera covers the world
 * completely and leaves every button, task, chat and meeting untouched on top of it. That is
 * exactly the split this feature needs: a new world, the same game.
 *
 * The texture is uploaded with LoadRawTextureData straight from the renderer's own byte buffer,
 * which is why nothing is copied or converted per frame. SetPixels32 is kept as a fallback for the
 * case where that entry point is not available in a future Il2Cpp build.
 *
 * WHAT IS DRAWN INTO IT
 * ---------------------
 * The renderer needs the world, not the game: a position, a heading, and a list of billboards. This
 * file is where Among Us gets translated into those, which means it owns the two pieces of state
 * the game does not have:
 *   - a FACING for every player. Among Us only knows "flipped left" or "flipped right", because a
 *     top-down game never needs more. Standing in the room with someone, the difference between
 *     them walking towards you and away from you is the whole game, so the direction is tracked per
 *     player from their movement and held when they stop.
 *   - a colour PAIR per player, taken from the game's own palette, so a crewmate in the beam is the
 *     same red the lobby promised.
 */

using System;
using System.Collections.Generic;
using Nightfall.Core;
using UnityEngine;

namespace Nightfall;

public static class NightfallView
{
    // ---- scene objects ----
    private static GameObject holder;
    private static SpriteRenderer screen;
    private static Texture2D texture;
    private static Sprite sprite;

    // ---- renderer ----
    // The raycaster is gone. It could only draw vertical slabs textured from the top-down map art,
    // and Among Us has no art for a wall face, a prop side or a door edge on: every complaint from
    // testing traced back to that. Raster3D draws a real model instead.
    private static readonly Raster3D renderer = new();
    private static Scene3D scene;
    private static readonly CrewmateSprite crewSprite = new();
    private static readonly WerewolfSprite wolfSprite = new();
    private static readonly List<Billboard> billboards = new(24);

    // ---- The screen arrows, taken off the lens and put into the world -------------------------
    /// The pin drawn instead of every flat ArrowBehaviour arrow (task, sabotage, TOR tracker).
    private static readonly MarkerSprite markerSprite = new();
    /// Arrow targets and colours collected this frame from the suppressed 2D arrows.
    private static readonly List<(NfVec2 target, NfColor color)> arrowMarkers = new();
    /// Arrows moved off their layer while the view is on, with the layer they came from.
    private static readonly Dictionary<int, (GameObject go, int layer)> hiddenArrows = new();
    /// "Ignore Raycast": no Among Us camera has this layer in its culling mask, so an arrow
    /// parked here is invisible without fighting the per-frame enabled-flags that the game and
    /// TOR both toggle. Disabling the renderer instead lost that fight every other frame.
    private const int HiddenArrowLayer = 2;

    private static int texW, texH;
    private static bool rawUploadFailed;

    // AUDIT-2026-08-16: ArrowBehaviour and DeadBody instances are collected on the same
    // ScanInterval-style cadence WorldRelay already uses for its own FindObjectsOfType sweep
    // (see WorldRelay.ScanInterval), instead of every frame. Arrows only appear/disappear on task
    // and sabotage state changes; bodies only appear on a kill and disappear at a meeting - both
    // far coarser than a frame, so a quarter second of staleness in WHICH objects exist is
    // invisible, unlike the per-frame FindObjectsOfType cost this replaces.
    private const float ArrowScanInterval = 0.25f;
    private const float BodyScanInterval = 0.25f;
    private static readonly List<ArrowBehaviour> cachedArrows = new(16);
    private static readonly List<DeadBody> cachedBodies = new(8);
    private static float lastArrowScan = -99f;
    private static float lastBodyScan = -99f;

    /// The world camera's own culling mask, kept so the game can have its world back.
    /// int.MinValue means "nothing saved yet" - 0 and -1 are both legitimate masks.
    private static int savedCullingMask = int.MinValue;

    /// Per-player facing, in radians, remembered across frames. Among Us has no such value.
    private static readonly Dictionary<byte, float> facings = new();
    private static readonly Dictionary<byte, Vector2> lastPos = new();

    public static bool IsActive { get; private set; }

    /// Where the camera stands and looks. Owned by NightfallControls, read here.
    public static ViewParams View = ViewParams.Default;

    /// How high the eye sits above the floor it is standing on. A crewmate is 0.7 tall.
    public const float EyeAboveFloor = 0.62f;

    /// The smoothed eye height, kept OUTSIDE View on purpose: BuildView assembles View afresh
    /// from ViewParams.Default every frame, so any state stored inside it lives for exactly one
    /// frame. Smoothing against View.EyeHeight therefore always restarted from the default -
    /// the camera gained a sixth of any step and forgot it again, which on the stabiliser
    /// stairs meant it never climbed at all (second playtest, picture 30). NaN = "no height
    /// yet, take the first measurement as it is".
    private static float eyeSmooth = float.NaN;

    // ================================================================================
    // Lifecycle
    // ================================================================================
    public static void Activate()
    {
        if (IsActive) return;
        try
        {
            if (!SceneGeometry.IsBuilt && !SceneGeometry.Build())
            {
                NightfallPlugin.Logger?.LogWarning("[Nightfall] No geometry - view not activated.");
                return;
            }

            if (scene == null)
            {
                var map = SceneGeometry.Current;
                if (map == null) return;
                var t0 = DateTime.Now;
                // MEMORY (2026-08-29): the process, not the catalogue. AreaSurfaces reports what it
                // retains, but the crash that started all this was the 32-bit address space running
                // out, and only the process total says how close that is. Private bytes before and
                // after, so the log reads "world build: +58 MB, 1244 MB now" next to the triangle
                // count - the number CrashDiagnostics' 30-second heartbeat cannot isolate.
                float mbBefore = PrivateMb();
                // The rides first: the platform's two ends go INTO the build (its disc is built at
                // every slot along the ride), the ladders and the zipline are kept for the ground.
                var platform = NightfallRides.Discover();
                scene = Scene3D.Build(map, platform);
                float mbAfter = PrivateMb();
                NightfallPlugin.Logger?.LogInfo(
                    $"[Nightfall] Model built in {(DateTime.Now - t0).TotalMilliseconds:F0} ms: "
                    + $"{scene.TriangleCount} triangles; process +{mbAfter - mbBefore:0} MB "
                    + $"({mbAfter:0} MB private now).");
            }

            EnsureScreen();
            eyeSmooth = float.NaN;      // fresh view, no stale height to glide away from
            lastGround = float.NaN;
            IsActive = true;
            NightfallPlugin.Logger?.LogInfo("[Nightfall] First-person view ON.");
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] Activate failed: {e}");
            Deactivate();
        }
    }

    public static void Deactivate()
    {
        IsActive = false;
        try
        {
            if (holder != null) holder.SetActive(false);
        }
        catch { }
        RestoreWorld();
        RestoreArrows();
        NightfallControls.ReleaseCursor();
    }

    // ================================================================================
    // The flat screen arrows (tasks, sabotages, TOR's tracker - everything built on
    // ArrowBehaviour, which TOR's own Arrow wraps too). A 2D arrow on the lens breaks the
    // first-person picture the same way the leaking sprites did, so while the view is on they
    // are parked on a layer no camera draws, and their information - target and colour - is
    // collected and drawn INTO the world as glowing pins (see CollectBillboards).
    // ================================================================================
    private static void SuppressArrows()
    {
        arrowMarkers.Clear();
        try
        {
            if (Time.time - lastArrowScan >= ArrowScanInterval)
            {
                lastArrowScan = Time.time;
                cachedArrows.Clear();
                foreach (var ab in UnityEngine.Object.FindObjectsOfType<ArrowBehaviour>())
                    cachedArrows.Add(ab);
            }

            for (int i = 0; i < cachedArrows.Count; i++)
            {
                var ab = cachedArrows[i];
                if (ab == null) continue;
                var go = ab.gameObject;
                if (go.layer != HiddenArrowLayer)
                {
                    hiddenArrows[go.GetInstanceID()] = (go, go.layer);
                    go.layer = HiddenArrowLayer;
                }

                // Only arrows the game is actually showing become pins: the enabled flag stays
                // meaningful because the layer trick never touches it.
                var img = ab.image;
                if (img == null || !img.enabled || !go.activeInHierarchy) continue;
                var c = img.color;
                if (c.a < 0.1f) continue;
                var t = ab.target;
                arrowMarkers.Add((new NfVec2(t.x, t.y), new NfColor(c.r, c.g, c.b)));
            }
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogWarning($"[Nightfall] Arrow suppression failed: {e.Message}");
        }
    }

    private static void RestoreArrows()
    {
        foreach (var kv in hiddenArrows.Values)
        {
            try { if (kv.go != null) kv.go.layer = kv.layer; } catch { }
        }
        hiddenArrows.Clear();
        arrowMarkers.Clear();
    }

    // ================================================================================
    // Hiding the game's own world
    // ================================================================================
    /*
     * WHY THE CULLING MASK AND NOT THE SORTING ORDER.
     *
     * The full-screen sprite was supposed to cover the world by sitting on the game's topmost
     * SORTING LAYER at order 30000. The second playtest shows it does not: the player's own
     * crewmate, a boulder, a thermometer and a door are all painted straight over the first-person
     * picture. The log says why - the line "Screen on sorting layer ..." never appears in it, so
     * `SortingLayer.layers` threw inside Il2Cpp and the catch swallowed it. The sprite therefore
     * stayed on "Default" at order 30000, and Unity sorts by LAYER before order: anything the game
     * puts on a later sorting layer wins no matter how large the order is.
     *
     * Chasing that with a better layer lookup would still be a race against every sorting layer
     * Among Us and every other mod define. The camera's CULLING MASK is not a race: it decides what
     * the camera is allowed to see at all, before any sorting happens. The world camera renders
     * layers {0,1,2,4,8,9,11,12,13,14,16} (read out of the running game by the survey); narrowed to
     * layer 1 alone it renders exactly one thing - this sprite - and the entire vanilla world,
     * players included, is simply not submitted.
     *
     * The HUD is untouched, because it is not this camera's work: Among Us draws it with separate
     * cameras at depth 99 and 100 (masks 32800 and 524288, i.e. UI, UICollide and Notifications),
     * and tasks, chat, meetings and buttons all live there. That split is the whole reason a sprite
     * on the world camera was the right vehicle in the first place.
     *
     * Re-applied every frame rather than once: the game resizes and re-configures its camera on
     * resolution changes and at every scene load, and one frame of the top-down map showing through
     * during the werewolf's hunt is one frame too many.
     */
    private static void HideWorld(Camera cam)
    {
        if (cam == null) return;
        int want = 1 << (holder != null ? holder.layer : 1);
        int have = cam.cullingMask;
        if (have == want) return;
        if (savedCullingMask == int.MinValue)
        {
            savedCullingMask = have;
            NightfallPlugin.Logger?.LogInfo(
                $"[Nightfall] World camera mask {have} -> {want} (vanilla world hidden).");
        }
        cam.cullingMask = want;
    }

    private static void RestoreWorld()
    {
        if (savedCullingMask == int.MinValue) return;
        try
        {
            var cam = Camera.main;
            if (cam != null) cam.cullingMask = savedCullingMask;
        }
        catch { }
        savedCullingMask = int.MinValue;
    }

    /// Full teardown for round end / lobby change. The texture is deliberately kept alive between
    /// rounds: rebuilding it is the one genuinely expensive allocation here.
    public static void Reset()
    {
        Deactivate();
        facings.Clear();
        lastPos.Clear();
        billboards.Clear();
        // These hold references into the current scene (arrows, bodies); a stale entry after a
        // lobby change or round end would point at objects that no longer exist. Clearing the scan
        // clocks too, so the next Tick() re-scans immediately instead of waiting out the interval
        // against a mostly-empty cache.
        cachedArrows.Clear();
        cachedBodies.Clear();
        lastArrowScan = -99f;
        lastBodyScan = -99f;
        AvatarCapture.Clear();
        WorldRelay.Clear();
        scene = null;
        NightfallRides.Clear();
        lastGround = float.NaN;
        AuSurfaces.ClearCache();
        // The built world's material catalogue as well: thirty-odd 128x128 textures is not much,
        // but a lobby that is left and rejoined a dozen times keeps every one of them otherwise.
        AreaSurfaces.ClearCache();
        // Same yardstick as the build line: what the process is at after letting go. The managed
        // part only returns on the next collection, so this reads slightly high until then; the
        // heartbeat thirty seconds later shows the settled value.
        NightfallPlugin.Logger?.LogInfo($"[Nightfall] World released; {PrivateMb():0} MB private now.");
    }

    private static float PrivateMb()
    {
        try
        {
            using var p = System.Diagnostics.Process.GetCurrentProcess();
            return p.PrivateMemorySize64 / 1048576f;
        }
        catch { return 0f; }
    }

    private static void EnsureScreen()
    {
        var cam = Camera.main;
        if (cam == null) throw new InvalidOperationException("no main camera");

        int wantW = NightfallPlugin.RenderWidth?.Value ?? 320;
        int wantH = Mathf.Max(90, Mathf.RoundToInt(wantW * 9f / 16f));

        if (holder == null)
        {
            holder = new GameObject("NightfallScreen");
            // Layer 1, "TransparentFX". Chosen from the culling masks the survey read out of the
            // running game rather than by habit: the world camera renders layers
            // {0,1,2,4,8,9,11,12,13,14,16} and the ShadowCamera renders {9,10,11,12}, so layer 1 is
            // seen by the camera that matters and is invisible to the one that bakes Among Us'
            // darkness texture. The obvious choice, layer 11 ("Objects"), would have had this
            // full-screen sprite rendered into the shadow map every frame.
            holder.layer = 1;
            screen = holder.AddComponent<SpriteRenderer>();
            // Above everything the world camera draws, below everything the UI camera draws.
            //
            // sortingOrder alone is NOT enough: Unity sorts by sorting LAYER first, so any sprite
            // on a later layer wins regardless of order. Among Us puts map props on their own
            // layers, and the first build had Polus rocks floating on top of the first-person view
            // because of exactly that. So the highest layer the game defines is looked up and used.
            //
            // AND IT IS NOT ENOUGH EITHER, which the second playtest proved: this lookup threw in
            // Il2Cpp - the log has no "Screen on sorting layer" line at all - and the swallowed
            // exception left the sprite on "Default". Everything the game draws on a later sorting
            // layer went straight over the top of it. The real guard is now the camera's culling
            // mask (see HideWorld); this stays as a second line of defence for anything that ends
            // up on layer 1 with us, and it says out loud when it cannot do its job.
            screen.sortingOrder = 30000;
            //
            // THE SortingLayer.layers LOOKUP IS GONE (2026-08-29), AND HERE IS WHY IT HAD TO GO.
            //
            // It used to sit here in a try/catch as a "second line of defence". The log across
            // every Nightfall session on this machine shows what it actually did: it threw on EVERY
            // world build, on every map, nine times out of nine - eight times an
            // OutOfMemoryException, once "Arithmetic operation resulted in an overflow". The last
            // of those came at 874 MB private bytes in a process that can address 4 GB, so it was
            // never memory running out: SortingLayer is a struct with a string in it, and the
            // Il2Cpp interop path that turns the native SortingLayer[] into a managed array gets
            // its length wrong, overflows, and asks for an impossible allocation. The message
            // "OutOfMemory" was a symptom of a corrupt length, not of a full heap - and the note in
            // AreaSurfaces.cs that read it as the catalogue exhausting the address space was
            // reading it backwards.
            //
            // A call that fails deterministically inside the interop layer, on the exact frame the
            // first-person view comes up, is not a defence, it is a suspect: the 2026-08-28 crash
            // dump (coreclr, main thread, minutes after "First-person view ON" on Mira) sits right
            // behind one of those throws. Whether the failed marshal leaves something torn behind
            // cannot be proven from here, but the call has no job left to do - the culling mask
            // above (HideWorld) is what keeps the vanilla world off the screen, and has been since
            // the second playtest - so it is simply not made any more.
        }

        holder.transform.SetParent(cam.transform, false);
        holder.transform.localPosition = new Vector3(0f, 0f, 1f);   // just in front of the camera
        holder.transform.localRotation = Quaternion.identity;
        holder.SetActive(true);

        if (texture == null || texW != wantW || texH != wantH)
        {
            texW = wantW;
            texH = wantH;
            renderer.Resize(texW, texH);

            texture = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                // Point, not Bilinear. Bilinear was tried (magnifies the internal frame onto the
                // screen without GPU cost) and reverted: at 854 wide it read as a soft haze over
                // the whole view, not a sharpened image - the torch's edge in particular went
                // from a cone to a fog bank. Raising RenderWidth (see its own config comment) is
                // the real lever for a crisper picture; it costs CPU, Bilinear does not, but
                // "free and blurry" was not the trade the playtest wanted.
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            // Pixels-per-unit of 100 is arbitrary; the sprite is scaled to the camera every frame
            // anyway, and a round number keeps the scale factors readable in a debugger.
            sprite = Sprite.Create(texture, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), 100f);
            screen.sprite = sprite;
            rawUploadFailed = false;
        }

        FitToCamera(cam);
    }

    /// The sprite has to cover the camera exactly, and the camera's size changes: Among Us resizes
    /// it on resolution changes, and other mods zoom it. Re-fitting every frame is the rule this
    /// mod family adopted after several overlays turned out to be correct only at 16:9.
    ///
    /// The vertical flip is not cosmetic: the renderer writes its buffer top row first (what a PNG
    /// wants), Unity textures start at the bottom row, so without it the world is upside down.
    private static void FitToCamera(Camera cam)
    {
        if (screen == null || cam == null) return;
        float worldH = cam.orthographicSize * 2f;
        float worldW = worldH * cam.aspect;

        // A hair of overscan so no seam of the real world can peek out at the edge.
        const float overscan = 1.02f;
        holder.transform.localScale = new Vector3(
            worldW / (texW / 100f) * overscan,
            -worldH / (texH / 100f) * overscan,
            1f);
    }

    // ================================================================================
    // Per frame
    // ================================================================================
    public static void Tick()
    {
        if (!IsActive) return;
        try
        {
            var cam = Camera.main;
            if (cam == null || holder == null) return;

            // The vent-to-vent buttons (Vent.Left/Center/Right, clicked via ButtonBehavior) are
            // real-world colliders the game positions for its OWN top-down camera. They stay
            // clickable while this view hides the world - the culling mask only stops them being
            // DRAWN - but the picture the player is looking at is a completely different
            // projection, so a click aimed at what is on screen would not land on what is actually
            // there. Rather than reimplement vent traversal, the real picture is handed back for
            // exactly as long as the player is inside a vent, a state nobody can see or attack them
            // in anyway, and the first-person view resumes the instant they step back out.
            if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.inVent)
            {
                RestoreWorld();
                RestoreArrows();
                if (holder.activeSelf) holder.SetActive(false);
                return;
            }
            if (!holder.activeSelf) holder.SetActive(true);

            if (holder.transform.parent != cam.transform)
                holder.transform.SetParent(cam.transform, false);
            FitToCamera(cam);
            HideWorld(cam);
            SuppressArrows();

            SceneGeometry.SyncDoors(scene);

            if (scene == null) return;

            NightfallRides.SyncPlatform(scene);
            CollectBillboards();
            // THE STATION STANDS ON THE PLANET, and in the built world that is a real difference:
            // the decks sit a hand's breadth above the ground and the lava gorge is below it. So
            // the eye is 0.62 above whatever is under the feet, not 0.62 above zero. Left out, the
            // camera floats outdoors and is buried in the gorge - and neither reads as a bug, only
            // as "the outside looks wrong".
            //
            // SMOOTHED, because the ground is a STEP FUNCTION. GroundAt answers "which deck is
            // under this point", and a deck edge is a hand's breadth high: walking off the planet
            // onto Storage's floor moved the camera 0.19 up in a single frame, which reads as the
            // picture jolting rather than as a step. A short lag makes it a stride.
            // The smoothing memory is eyeSmooth, NOT View.EyeHeight: View was just rebuilt from
            // Default by BuildView, so its EyeHeight is the constant 0.62 again every frame.
            float want = EyeAboveFloor + GroundUnderPlayer();
            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
            eyeSmooth = float.IsNaN(eyeSmooth) || Mathf.Abs(want - eyeSmooth) > 0.9f
                ? want                                            // a teleport, not a step
                : Mathf.Lerp(eyeSmooth, want, 1f - Mathf.Exp(-12f * dt));
            View.EyeHeight = eyeSmooth;
            renderer.Render(scene, View, billboards);
            Upload();
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] Tick failed: {e}");
            Deactivate();
        }
    }

    /*
     * THE GROUND UNDER THE PLAYER, with two things the plain deck lookup gets wrong.
     *
     * 1. RIDES. On the Gap Room platform, on a ladder, on the zipline, the player's position is
     *    over something that is not what their feet are on. NightfallRides knows the three from
     *    the game's own objects and answers with the ride's ground instead (see that file).
     *
     * 2. HOLES. Where the description has no deck at all, GroundAt falls back to the planet.
     *    On Polus that is a real place a hand's breadth below the decks; on the Fungle it is
     *    metres below the highland the player is actually walking on, and every gap between two
     *    described areas sent the eye down through the island. A drop of more than half a unit
     *    onto NOTHING (no deck under the point) is therefore not believed: the last real ground
     *    is held until a deck turns up again. Onto a deck or a pit - a described place, however
     *    low - the drop is real and taken. Half a unit is well above any deck-to-planet step
     *    (0.19) and well below any level the maps stack (the smallest ladder band is 1.35).
     */
    private static float lastGround = float.NaN;

    private static float GroundUnderPlayer()
    {
        var pos = new Vector2(View.Position.X, View.Position.Y);
        float? ride = NightfallRides.GroundOverride(pos, scene);
        if (ride.HasValue)
        {
            lastGround = ride.Value;
            return ride.Value;
        }

        float g = scene.GroundAt(View.Position);
        if (!float.IsNaN(lastGround) && lastGround - g > 0.5f && !scene.DeckUnder(View.Position))
            return lastGround;   // a hole in the description, not a drop
        lastGround = g;
        return g;
    }

    private static void Upload()
    {
        if (texture == null) return;
        var pixels = renderer.Pixels;

        if (!rawUploadFailed)
        {
            try
            {
                unsafe
                {
                    fixed (byte* p = pixels)
                    {
                        texture.LoadRawTextureData((IntPtr)p, pixels.Length);
                    }
                }
                texture.Apply(false);
                return;
            }
            catch (Exception e)
            {
                rawUploadFailed = true;
                NightfallPlugin.Logger?.LogWarning(
                    $"[Nightfall] Raw texture upload unavailable, falling back to SetPixels32: {e.Message}");
            }
        }

        // Fallback path: one conversion per frame. Slower and allocates, but it works everywhere.
        var buf = new Color32[texW * texH];
        for (int i = 0, o = 0; i < buf.Length; i++, o += 4)
            buf[i] = new Color32(pixels[o], pixels[o + 1], pixels[o + 2], 255);
        texture.SetPixels32(buf);
        texture.Apply(false);
    }

    // ================================================================================
    // Billboards
    // ================================================================================
    private static void CollectBillboards()
    {
        billboards.Clear();
        var me = PlayerControl.LocalPlayer;
        if (me == null) return;

        // Resolved once per frame rather than per player: the reflection into Unknown's Collection
        // is cheap but not free, and there are up to fifteen players.
        var wolf = NightfallState.TheWerewolf();
        byte? wolfId = wolf != null ? wolf.PlayerId : (byte?)null;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;
            if (p.PlayerId == me.PlayerId) continue;            // you are not in your own view
            if (p.Data.Disconnected) continue;
            if (p.Data.IsDead) continue;                        // ghosts are invisible to the living

            // Vanished by another mod's ability (Shade, Scout, Illusionist): if the game has hidden
            // the body, the first-person view must hide it too, or Nightfall becomes a wallhack.
            if (!IsVisible(p)) continue;

            var pos = p.GetTruePosition();

            // The beast is not a crewmate in a costume: different silhouette, different scale, and
            // eyes that stay lit when everything else has gone dark. Unknown's Collection already
            // scales it to 1.5x in the top-down view, and it keeps that proportion here.
            bool isWolf = wolfId.HasValue && p.PlayerId == wolfId.Value;

            // The real thing first: a photograph of the actual character, hat, skin, pet and all.
            // The drawn crewmate is only the fallback for the moments before the first capture has
            // happened, or if capturing fails on some future Among Us version.
            var shot = AvatarCapture.For(p);

            billboards.Add(new Billboard
            {
                Position = new NfVec2(pos.x, pos.y),
                Facing = TrackFacing(p, pos),
                Source = shot ?? (isWolf ? (IBillboardSource)wolfSprite : crewSprite),
                // AUDIT-2026-08-15: no extra 1.5x here for the photo path. Unknown's Collection already
                // scales the player transform by 1.5x for the transformation, and AvatarCapture shoots the
                // SpriteRenderer's world-space bounds, so WorldHeight has that factor baked in already.
                // Only the procedural fallback below (no photo yet) still needs it applied by hand.
                Height = shot != null ? shot.WorldHeight
                                      : (isWolf ? 1.08f : 0.72f),
                Color = isWolf ? WerewolfSprite.Fur : ColorOf(p, false),
                ShadowColor = isWolf ? WerewolfSprite.FurShadow : ColorOf(p, true),
                // Feet on whatever they are standing on - a stair, the dropship deck - not on
                // the reference floor. Same rule as the eye height.
                Base = scene.GroundAt(new NfVec2(pos.x, pos.y)),
            });

            // The pet, as its own billboard at its own position: it trails its owner by up to a
            // metre, so baked into the owner's photograph it would hover at their hip. It faces
            // the way its owner faces, which is what PetBehaviour's own FlipX does.
            var petShot = AvatarCapture.ForPet(p, out var petPos);
            if (petShot != null)
            {
                billboards.Add(new Billboard
                {
                    Position = new NfVec2(petPos.x, petPos.y),
                    Facing = TrackFacing(p, pos),
                    Source = petShot,
                    Height = petShot.WorldHeight,
                    Color = ColorOf(p, false),
                    ShadowColor = ColorOf(p, true),
                    Base = scene.GroundAt(new NfVec2(petPos.x, petPos.y)),
                });
            }
        }

        // Bodies. Finding one in the dark is the single most important thing a crewmate can do
        // during the wolf phase, so they are drawn low and wide rather than as a standing figure.
        try
        {
            if (Time.time - lastBodyScan >= BodyScanInterval)
            {
                lastBodyScan = Time.time;
                cachedBodies.Clear();
                foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
                    cachedBodies.Add(body);
            }

            for (int i = 0; i < cachedBodies.Count; i++)
            {
                var body = cachedBodies[i];
                if (body == null) continue;
                var bp = body.TruePosition;
                var owner = Helpers_PlayerById(body.ParentId);
                var ownerShot = owner != null ? AvatarCapture.For(owner) : null;
                billboards.Add(new Billboard
                {
                    Position = new NfVec2(bp.x, bp.y),
                    Facing = 0f,
                    Source = (IBillboardSource)ownerShot ?? crewSprite,
                    Height = 0.34f,
                    Color = owner != null ? ColorOf(owner, false) : new NfColor(0.5f, 0.5f, 0.5f),
                    ShadowColor = owner != null ? ColorOf(owner, true) : new NfColor(0.25f, 0.25f, 0.25f),
                    Base = scene.GroundAt(new NfVec2(bp.x, bp.y)),
                });
            }
        }
        catch { }

        CollectMarkers(me);

        // Everything the ROLES put into the world - traps, relics, clones, portals, sparks - which
        // the culling mask hides along with the vanilla map. See WorldRelay.
        WorldRelay.Collect(billboards, scene);
    }

    /// The world-anchored replacements for the flat screen arrows, plus the neighbour hints
    /// while sitting in a vent. A pin hovers in the DIRECTION of its target, at most a stride
    /// or two ahead, and converges on the target itself as one gets close - so walking towards
    /// it is walking towards the task. Behind you it is simply behind you: a world-anchored
    /// sign is found by looking around, which is the grammar of this whole mod.
    private static void CollectMarkers(PlayerControl me)
    {
        var mp = me.GetTruePosition();
        var myPos = new NfVec2(mp.x, mp.y);

        foreach (var (target, color) in arrowMarkers)
        {
            var d = target - myPos;
            float dist = d.Length;
            if (dist < 0.7f) continue;                   // standing on it needs no pin in the face
            float md = MathF.Min(dist, 2.4f);
            var pos = myPos + d * (md / dist);
            billboards.Add(new Billboard
            {
                Position = pos,
                Facing = 0f,
                Source = markerSprite,
                Height = 0.42f,
                Color = color,
                ShadowColor = color * 0.55f,
                Base = scene.GroundAt(pos) + 0.92f,
                Glow = 0.85f,
            });
        }

        // In a vent, the game's own way onward is the neighbour vents (directional keys /
        // Vent.TryMoveToVent); the flat arrows for that sit in the hidden world. So the
        // neighbours get pins at their real positions, low over the lids.
        try
        {
            if (me.inVent && Vent.currentVent != null)
            {
                var cur = Vent.currentVent;
                foreach (var nb in new[] { cur.Left, cur.Right, cur.Center })
                {
                    if (nb == null) continue;
                    var vp = nb.transform.position;
                    var pos = new NfVec2(vp.x, vp.y);
                    billboards.Add(new Billboard
                    {
                        Position = pos,
                        Facing = 0f,
                        Source = markerSprite,
                        Height = 0.34f,
                        Color = new NfColor(0.45f, 0.9f, 1f),
                        ShadowColor = new NfColor(0.2f, 0.45f, 0.55f),
                        Base = scene.GroundAt(pos) + 0.25f,
                        Glow = 0.9f,
                    });
                }
            }
        }
        catch { }
    }

    /// True while the player is actually rendered by the game. Reading the renderer rather than
    /// guessing from role state means every current and future invisibility ability is honoured for
    /// free.
    private static bool IsVisible(PlayerControl p)
    {
        try
        {
            // Among Us moved the player's renderer under `cosmetics` several versions ago; there is
            // no PlayerControl.myRend any more.
            var body = p.cosmetics?.currentBodySprite?.BodySprite;
            if (body == null) return true;
            if (!body.enabled) return false;
            return body.color.a > 0.15f;
        }
        catch { return true; }
    }

    /// Among Us stores no heading, only a horizontal flip. The direction is therefore integrated
    /// from movement: while a player moves, that is where they face; when they stop, the last
    /// direction is held rather than snapped to a default, because a crewmate that spins to face
    /// north every time it pauses would be both wrong and unsettling.
    private static float TrackFacing(PlayerControl p, Vector2 pos)
    {
        byte id = p.PlayerId;
        float facing = facings.TryGetValue(id, out float f) ? f : 0f;

        if (lastPos.TryGetValue(id, out var prev))
        {
            var delta = pos - prev;
            // The threshold is per frame, so it has to be small; below it the player is standing
            // still or being nudged by physics.
            if (delta.sqrMagnitude > 0.000004f)
            {
                float target = Mathf.Atan2(delta.y, delta.x);
                // Ease towards the new heading so a sidestep does not snap the sprite around.
                float diff = NfMath.WrapAngle(target - facing);
                facing = NfMath.WrapAngle(facing + diff * Mathf.Clamp01(Time.deltaTime * 14f));
                facings[id] = facing;
            }
        }
        lastPos[id] = pos;
        return facing;
    }

    private static NfColor ColorOf(PlayerControl p, bool shadow)
    {
        try
        {
            int id = p.Data.DefaultOutfit.ColorId;
            var arr = shadow ? Palette.ShadowColors : Palette.PlayerColors;
            if (id >= 0 && id < arr.Length)
            {
                var c = arr[id];
                return new NfColor(c.r, c.g, c.b);
            }
        }
        catch { }
        return shadow ? new NfColor(0.3f, 0.3f, 0.3f) : new NfColor(0.7f, 0.7f, 0.7f);
    }

    private static PlayerControl Helpers_PlayerById(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p != null && p.PlayerId == id) return p;
        return null;
    }
}
