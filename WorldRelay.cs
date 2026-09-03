// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * WorldRelay - everything the ROLES put into the world, put back into the picture.
 *
 * THE PROBLEM, IN ONE LINE OF SOMEBODY ELSE'S CODE
 * ------------------------------------------------
 * NightfallView.HideWorld narrows the world camera's culling mask to the single layer the
 * full-screen sprite lives on. That was aimed at the vanilla sprites leaking over the first-person
 * picture (second playtest, pictures 33/34) and it hits far more than vanilla: a Saboteur's stun
 * trap, a Collector's relic, a Poltergeist's ghost hand, a Tesla's sparks, an Illusionist's clone,
 * The Other Roles' traps, portals, garlics, bombs and jack-in-the-boxes are all ordinary
 * SpriteRenderers standing in the world, and the camera simply stops being allowed to see them.
 * Nothing breaks and nothing logs; the abilities keep working and become invisible, which is the
 * worst of the two failure modes because the player cannot tell.
 *
 * WHY THIS IS ONE MECHANISM AND NOT THIRTY
 * ----------------------------------------
 * The obvious fix is to teach Nightfall about each ability: read Unknown's Collection's trap list,
 * read TOR's Portal list, and draw each one. That is thirty pieces of reflection against three mods
 * that are still being written, and it is wrong the day somebody adds a role. The information is
 * already in the scene in a uniform shape - "there is a sprite, here, this big, this colour" - and
 * the renderer already knows how to draw exactly that: a billboard.
 *
 * So the relay does not know what a trap is. It walks the ROOT objects of the scene, skips the ones
 * Nightfall already draws itself (the ship, the players, the pets, the bodies, the HUD), and turns
 * whatever is left into billboards. A new role's effect appears in the first-person view on the day
 * it appears in the game, without a line of code here.
 *
 * Two properties fall out of that for free, and both matter:
 *   - VISIBILITY RULES ARE INHERITED. A trap only the Saboteur may see is already SetActive(false)
 *     for everybody else; a Shade's body marker is only instantiated for the Shade. The relay reads
 *     the live renderers, so it can never show something the game had decided to hide. That is the
 *     same argument NightfallView.IsVisible makes for invisible players, and for the same reason.
 *   - ROOT LEVEL IS THE RIGHT FILTER. Every world effect in this mod family is a bare
 *     `new GameObject(...)`: Unknown's Collection funnels all of them through UCFx.NewFxRoot, and
 *     The Other Roles writes `new GameObject("Trap")`, `"Portal"`, `"Garlic"`, `"Bomb"`,
 *     `"JackInTheBox"`, `"NinjaTrace"`, `"Silhouette"`, `"FootprintHolder"` with no parent at all.
 *     Screen-space UI is the opposite: it hangs off HudManager, which is one skipped root.
 *
 * WHAT IS LOST
 * ------------
 * A billboard is upright and a trap lies flat on the floor. Drawing it as a standing card is the
 * same compromise the props of the photographed maps make ("Tafeln statt Kaesten"), and it is the
 * right one here too: a decal flat on the ground is nearly invisible from eye height, and a thing
 * you must not step on has to be seen from across the room.
 *
 * The photograph is refreshed a few times a second, not every frame, because a capture costs a
 * camera render and a readback. Positions are read every frame regardless, so anything that MOVES
 * moves correctly - only its picture is up to a third of a second old. For a pulsing glow that is
 * invisible; for a twelve-spark ring it is a slightly stiffer ring.
 */

using System;
using System.Collections.Generic;
using Nightfall.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nightfall;

public static class WorldRelay
{
    /// A layer nothing in Among Us uses, shared with AvatarCapture's own isolation pass.
    private const int IsolationLayer = 30;

    /// How often the scene is re-walked for new effect roots. Effects appear and vanish constantly,
    /// but a quarter second late is a quarter second nobody can act on.
    private const float ScanInterval = 0.25f;

    /// How often one relayed object is re-photographed, and how far apart two captures may be.
    private const float RefreshSeconds = 0.35f;
    private const float CaptureSpacing = 0.12f;

    /// Beyond this the object is not an effect but a backdrop (the lava plane, a full-screen quad).
    private const float MaxWorldSize = 8f;

    /// A hard ceiling, so a mod that spawns hundreds of pooled sprites can never stall the frame.
    private const int MaxRelayed = 64;

    private sealed class Entry
    {
        public GameObject Root;
        public readonly CapturedSprite Sprite = new();
        public float CapturedAt = -99f;
        public float LastSizeY;
        // AUDIT-2026-08-16: the SpriteRenderers under Root, resolved once per Scan() (every
        // ScanInterval) instead of once per Measure() call (every frame, for up to MaxRelayed
        // entries). Only the structural membership (which renderers exist, on which layer) is
        // cached here; enabled/sprite/alpha are still read fresh in Measure() every frame, because
        // those flip mid-interval (a blink, a fade) without the object itself changing.
        public readonly List<SpriteRenderer> Renderers = new();
    }

    private static readonly Dictionary<int, Entry> entries = new();
    private static readonly List<Entry> live = new(MaxRelayed);
    private static readonly List<SpriteRenderer> scratch = new(16);
    private static float lastScan = -99f;
    private static float lastCapture;

    /// Roots skipped by identity rather than by name, resolved fresh each scan.
    private static GameObject shipRoot, hudRoot;

    // AUDIT-2026-09-03 (Scan() perf fix): buffers reused across Scan() calls instead of two fresh
    // allocations (the root list and the seen-id set) every ScanInterval. See Scan() for how they
    // are used.
    private static readonly List<GameObject> rootScratch = new(64);
    private static readonly HashSet<int> seenScratch = new();
    private static readonly HashSet<int> rootSeenScratch = new();

    // AUDIT-2026-09-03 (Capture() perf fix): the same pattern AvatarCapture uses for its own
    // photographs - a persistent, disabled capture camera and render targets pooled by (rounded)
    // pixel size, instead of a fresh Camera GameObject / RenderTexture / readback Texture2D on
    // every single capture. Kept as its own copy rather than shared with AvatarCapture, same
    // reasoning as Capture()'s own header comment: this photographs an arbitrary world object, not
    // a player's cosmetics tree, and the two have never shared code.
    private static GameObject captureCamGo;
    private static Camera captureCam;

    private sealed class RtEntry
    {
        public RenderTexture Rt;
        public Texture2D Readback;
    }

    private static readonly Dictionary<(int w, int h), RtEntry> rtCache = new();
    /// Insertion order of `rtCache`, so a cache that somehow grows past `MaxRtCacheEntries` has
    /// something to evict by. See AvatarCapture.GetRt for the same pattern and reasoning.
    private static readonly List<(int w, int h)> rtOrder = new();
    /// A hard ceiling on how many distinct render-target sizes are pooled at once.
    private const int MaxRtCacheEntries = 24;
    private static byte[] rgbaScratch = Array.Empty<byte>();

    private static Camera GetCamera()
    {
        if (captureCam == null)
        {
            captureCamGo = new GameObject("NightfallRelayCam");
            UnityEngine.Object.DontDestroyOnLoad(captureCamGo);
            captureCam = captureCamGo.AddComponent<Camera>();
            captureCam.orthographic = true;
            captureCam.cullingMask = 1 << IsolationLayer;
            captureCam.clearFlags = CameraClearFlags.SolidColor;
            captureCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            captureCam.nearClipPlane = -100f;
            captureCam.farClipPlane = 100f;
            captureCam.enabled = false;
        }
        return captureCam;
    }

    private static void DestroyRt(RtEntry e)
    {
        try { if (e.Rt != null) { e.Rt.Release(); UnityEngine.Object.Destroy(e.Rt); } } catch { }
        try { if (e.Readback != null) UnityEngine.Object.Destroy(e.Readback); } catch { }
    }

    private static RtEntry GetRt(int texW, int texH)
    {
        var key = (texW, texH);
        if (rtCache.TryGetValue(key, out var e))
        {
            // Unity's overloaded `==` treats a destroyed-but-not-yet-nulled native object as
            // "fake null": the C# reference is still non-null but every native call on it throws.
            // A stale cache entry pointing at one would fail this object's capture every single
            // frame instead of just once, so discard it here and fall through to rebuild fresh.
            if (e.Rt == null || e.Readback == null)
            {
                DestroyRt(e);
                rtCache.Remove(key);
                rtOrder.Remove(key);
                e = null;
            }
        }

        if (e == null)
        {
            e = new RtEntry
            {
                Rt = new RenderTexture(texW, texH, 16, RenderTextureFormat.ARGB32),
                Readback = new Texture2D(texW, texH, TextureFormat.RGBA32, false),
            };
            e.Rt.Create();
            rtCache[key] = e;
            rtOrder.Add(key);

            // Cap how many distinct sizes are pooled at once: evict the oldest bucket rather than
            // let the pool grow for the whole round.
            if (rtOrder.Count > MaxRtCacheEntries)
            {
                var oldestKey = rtOrder[0];
                rtOrder.RemoveAt(0);
                if (rtCache.TryGetValue(oldestKey, out var oldest))
                {
                    rtCache.Remove(oldestKey);
                    DestroyRt(oldest);
                }
            }
        }

        if (!e.Rt.IsCreated()) e.Rt.Create();
        return e;
    }

    private static byte[] GetRgbaScratch(int length)
    {
        if (rgbaScratch.Length < length) rgbaScratch = new byte[length];
        return rgbaScratch;
    }

    private static int RoundUp32(int v) => (v + 31) / 32 * 32;

    public static void Clear()
    {
        entries.Clear();
        live.Clear();
        lastScan = -99f;
        rootScratch.Clear();
        rootSeenScratch.Clear();
        seenScratch.Clear();

        foreach (var e in rtCache.Values) DestroyRt(e);
        rtCache.Clear();
        rtOrder.Clear();

        try { if (captureCamGo != null) UnityEngine.Object.Destroy(captureCamGo); } catch { }
        captureCamGo = null;
        captureCam = null;
    }

    /// Adds a billboard for every world object of every mod that the culling mask has hidden.
    public static void Collect(List<Billboard> into, Scene3D scene)
    {
        if (scene == null) return;
        try
        {
            if (Time.time - lastScan >= ScanInterval) Scan();

            foreach (var e in live)
            {
                var root = e.Root;
                if (root == null || !root.activeInHierarchy) continue;

                if (!Measure(e.Renderers, out var centre, out float sizeY, out float depth, out float alpha))
                    continue;
                if (alpha <= 0.06f) continue;

                // Re-photograph on a slow rotation, and at once when the object has changed shape
                // (a burst that has grown, a clone that has turned round).
                bool stale = Time.time - e.CapturedAt > RefreshSeconds
                             || MathF.Abs(sizeY - e.LastSizeY) > e.LastSizeY * 0.15f;
                if ((stale || !e.Sprite.IsValid) && Time.time - lastCapture > CaptureSpacing)
                {
                    if (Capture(root, e)) { lastCapture = Time.time; e.LastSizeY = sizeY; }
                }
                if (!e.Sprite.IsValid) continue;

                /*
                 * HOW HIGH IT HANGS. A top-down game has no height axis, so there is nothing to
                 * read - except that Among Us and both mods use the sprite's Z for exactly the
                 * distinction that matters here. A decal on the floor is drawn just behind the
                 * players (z = y/1000, i.e. about zero); an effect that belongs ABOVE everything -
                 * an aura, a hex halo, a spark ring around someone's chest - is pushed to z = -1.2
                 * so it wins the 2D sort. So the depth the mod chose to sort by is also, by
                 * accident and reliably, its height off the ground.
                 */
                var pos = new NfVec2(centre.x, centre.y);
                float lift = depth < -0.5f ? 0.45f : 0f;

                into.Add(new Billboard
                {
                    Position = pos,
                    Facing = 0f,
                    Source = e.Sprite,
                    Height = Mathf.Clamp(sizeY, 0.12f, MaxWorldSize),
                    Color = new NfColor(1f, 1f, 1f),
                    ShadowColor = new NfColor(0.5f, 0.5f, 0.5f),
                    Base = scene.GroundAt(pos) + lift,
                    Fade = 1f - Mathf.Clamp01(alpha),
                    // Self-lit, and exempt from the visibility cone: everything relayed here is
                    // either an effect that emits its own light or a piece of game information the
                    // player has been given on purpose. The cone exists to hide PEOPLE.
                    Glow = 0.45f,
                });

                if (into.Count > 220) break;      // the renderer's own sanity limit
            }
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogWarning($"[Nightfall] World relay failed: {e.Message}");
        }
    }

    // ================================================================================
    // Finding the objects
    // ================================================================================
    private static void Scan()
    {
        lastScan = Time.time;
        live.Clear();
        try
        {
            shipRoot = ShipStatus.Instance != null ? ShipStatus.Instance.gameObject : null;
            hudRoot = HudManager.Instance != null ? HudManager.Instance.transform.root.gameObject : null;

            // NOT SceneManager.GetActiveScene().GetRootGameObjects(): that method is stripped from
            // this Il2Cpp build, and Il2CppInterop cannot rebuild it - every call threw "Method
            // unstripping failed" and took the WHOLE scan down with it (observed 2026-08-14: 76
            // failures in one session, so the relay never saw a single object).
            //
            // AUDIT-2026-09-03 (perf): the fix for THAT used to be enumerating every Transform in
            // the scene and keeping the parentless ones - the same root set, through an API that
            // survives stripping, but at the cost of visiting every child transform of every
            // sprite, every UI element and everything else in the scene, every ScanInterval, plus
            // a `.parent`/`.gameObject` Il2Cpp interop touch on each one. A relayable object is, by
            // construction (IsRelayable/HasWorldSprite below), one that carries a world
            // SpriteRenderer - so starting from FindObjectsOfType<SpriteRenderer>() and walking UP
            // to `.transform.root` reaches the exact same candidate roots from the other end,
            // without ever visiting a transform that could not possibly qualify. `rootScratch` and
            // the two seen-sets are reused buffers rather than fresh allocations per scan.
            //
            // NOTE: when there are more candidates than MaxRelayed, the ORDER objects are relayed
            // in can now differ from before - roots are discovered in sprite-enumeration order
            // rather than transform-hierarchy order, so which handful get dropped may not match.
            rootScratch.Clear();
            rootSeenScratch.Clear();
            foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
            {
                if (sr == null) continue;
                int l = sr.gameObject.layer;
                if (l == 5 || l == 15) continue;               // UI / UICollide: never the world

                var rootT = sr.transform.root;
                if (rootT == null) continue;
                var root = rootT.gameObject;
                if (root == null) continue;
                if (root == shipRoot || root == hudRoot) continue;

                if (!rootSeenScratch.Add(root.GetInstanceID())) continue;   // already have this root
                rootScratch.Add(root);
            }

            seenScratch.Clear();

            foreach (var go in rootScratch)
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!IsRelayable(go)) continue;

                int id = go.GetInstanceID();
                seenScratch.Add(id);
                if (!entries.TryGetValue(id, out var e))
                {
                    e = new Entry { Root = go };
                    entries[id] = e;
                }
                e.Root = go;
                CacheRenderers(go, e.Renderers);
                live.Add(e);
                if (live.Count >= MaxRelayed) break;
            }

            // Drop the cache of anything that has left the scene, or the dictionary grows for the
            // whole round: bursts are created and destroyed by the dozen.
            if (entries.Count > MaxRelayed * 3)
            {
                var dead = new List<int>();
                foreach (var kv in entries)
                    if (!seenScratch.Contains(kv.Key) || kv.Value.Root == null) dead.Add(kv.Key);
                foreach (var k in dead) entries.Remove(k);
            }
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogWarning($"[Nightfall] World relay scan failed: {e.Message}");
        }
    }

    /// The whole filter. Everything it lets through is drawn; everything it stops is either already
    /// drawn by Nightfall itself or is not in the world at all.
    private static bool IsRelayable(GameObject go)
    {
        if (shipRoot != null && go == shipRoot) return false;      // the map: drawn as geometry
        if (hudRoot != null && go == hudRoot) return false;        // the HUD: its own cameras
        if (go.GetComponent<Camera>() != null) return false;       // ... and the screen hangs off one
        if (go.GetComponent<PlayerControl>() != null) return false;// players: their own billboards
        if (go.GetComponent<DeadBody>() != null) return false;     // bodies: likewise
        if (go.GetComponent<PetBehaviour>() != null) return false; // pets: likewise

        string n = go.name;
        if (n == "NightfallScreen") return false;
        // Arrows are handled by NightfallView: parked on a dead layer and redrawn as pins, which is
        // the right answer for a DIRECTION and the wrong one for a thing.
        if (n == "Arrow") return false;

        return HasWorldSprite(go);
    }

    /// True when the object carries at least one enabled world sprite. Layers 5 (UI) and 15
    /// (UICollide) are screen furniture and never the world, whoever the parent is.
    private static bool HasWorldSprite(GameObject go)
    {
        try
        {
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r == null || !r.enabled || r.sprite == null) continue;
                int l = r.gameObject.layer;
                if (l == 5 || l == 15) continue;
                if (r.color.a < 0.06f) continue;
                return true;
            }
        }
        catch { }
        return false;
    }

    /// The SpriteRenderers under `go`, resolved once per Scan() and reused by Measure() every
    /// frame in between. Layers 5 (UI) and 15 (UICollide) are filtered here, structurally, same as
    /// HasWorldSprite; enabled/sprite/alpha are NOT filtered here on purpose (see the Entry field).
    private static void CacheRenderers(GameObject go, List<SpriteRenderer> into)
    {
        into.Clear();
        try
        {
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r == null) continue;
                int l = r.gameObject.layer;
                if (l == 5 || l == 15) continue;
                into.Add(r);
            }
        }
        catch { }
    }

    /// The object's world rectangle, its sort depth and its faintest-common alpha, in one pass.
    /// Reads the renderer list Scan() cached on the entry instead of re-resolving it: this used to
    /// be a GetComponentsInChildren call per relayed entry (up to MaxRelayed) EVERY FRAME, while
    /// Scan() and Capture() next to it were already gated on ScanInterval/CaptureSpacing.
    private static bool Measure(List<SpriteRenderer> renderers, out Vector2 centre, out float sizeY,
                                out float depth, out float alpha)
    {
        centre = default; sizeY = 0f; depth = 0f; alpha = 0f;
        try
        {
            Bounds b = default;
            bool any = false;
            float maxA = 0f, sumZ = 0f;
            int n = 0;

            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                // == null also catches renderers destroyed since the last scan (Unity's overload).
                if (r == null || !r.enabled || r.sprite == null) continue;
                float a = r.color.a;
                if (a < 0.06f) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                if (a > maxA) maxA = a;
                sumZ += r.transform.position.z;
                n++;
            }
            if (!any || n == 0) return false;

            var s = b.size;
            if (s.y > MaxWorldSize || s.x > MaxWorldSize) return false;   // a backdrop, not an effect

            centre = new Vector2(b.center.x, b.center.y);
            sizeY = Mathf.Max(s.y, s.x * 0.35f);   // a wide flat decal still needs to be seen
            depth = sumZ / n;
            alpha = maxA;
            return true;
        }
        catch { return false; }
    }

    // ================================================================================
    // Photographing one object
    // ================================================================================
    /*
     * The same isolation trick AvatarCapture uses, and deliberately a second copy of it rather than
     * a shared helper: that one photographs a PLAYER (cosmetics tree, pet excluded, name text
     * excluded, trimmed to the feet) and this one photographs an arbitrary object with none of
     * those rules. Merging them would mean a function with a flag for every difference.
     */
    private static bool Capture(GameObject root, Entry e)
    {
        var moved = new List<(GameObject go, int layer)>();
        RenderTexture rt = null, previous = null;
        // Whether `previous` was actually captured from RenderTexture.active before this method
        // set it to something else. Restoring on `previous != null` is wrong: a legitimately null
        // "no active render texture" state would then never be restored, and the render target this
        // method pooled would stay stuck as RenderTexture.active if ReadPixels/Apply throws.
        bool activeSet = false;
        Texture2D readback = null;

        try
        {
            scratch.Clear();
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r == null || !r.enabled || r.sprite == null) continue;
                int l = r.gameObject.layer;
                if (l == 5 || l == 15) continue;
                if (r.color.a < 0.06f) continue;
                scratch.Add(r);
            }
            if (scratch.Count == 0) return false;

            var bounds = scratch[0].bounds;
            for (int i = 1; i < scratch.Count; i++) bounds.Encapsulate(scratch[i].bounds);

            float frameH = Mathf.Clamp(bounds.size.y * 1.06f, 0.10f, MaxWorldSize);
            float frameW = Mathf.Clamp(bounds.size.x * 1.06f, 0.10f, MaxWorldSize);
            int texH = Mathf.Clamp(Mathf.RoundToInt(frameH * 128f), 16, 160);
            int texW = Mathf.Clamp(Mathf.RoundToInt(texH * frameW / frameH), 8, 320);

            // Round up to a multiple of 32 so the render-target cache (keyed by exact pixel size)
            // settles on a handful of buckets instead of one RenderTexture per effect's exact
            // bounds. The camera's aspect is matched to the ROUNDED size below, so the extra pixels
            // widen the frame slightly rather than stretching the picture into it. Both clamp
            // ceilings below (160, 320) are already multiples of 32, so rounding up never pushes a
            // capture past what the clamp allowed.
            texW = RoundUp32(texW);
            texH = RoundUp32(texH);
            frameW = frameH * texW / texH;

            foreach (var r in scratch)
            {
                var go = r.gameObject;
                moved.Add((go, go.layer));
                go.layer = IsolationLayer;
            }

            var cam = GetCamera();
            cam.orthographicSize = frameH * 0.5f;
            cam.aspect = frameW / frameH;
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -20f);

            var rtEntry = GetRt(texW, texH);
            rt = rtEntry.Rt;
            readback = rtEntry.Readback;
            cam.targetTexture = rt;
            cam.Render();

            previous = RenderTexture.active;
            activeSet = true;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, texW, texH), 0, 0, false);
            readback.Apply(false);

            var src = readback.GetPixels32();
            var rgba = GetRgbaScratch(texW * texH * 4);
            bool anyPixel = false;
            for (int y = 0; y < texH; y++)
            {
                int srcRow = (texH - 1 - y) * texW, dstRow = y * texW;
                for (int x = 0; x < texW; x++)
                {
                    var c = src[srcRow + x];
                    int o = (dstRow + x) * 4;
                    rgba[o] = c.r; rgba[o + 1] = c.g; rgba[o + 2] = c.b; rgba[o + 3] = c.a;
                    if (c.a >= 24) anyPixel = true;
                }
            }
            if (!anyPixel) return false;

            // `rgba` above is a shared scratch buffer (AUDIT-2026-09-03), reused by every relayed
            // object's capture in turn - but CapturedSprite.Set does not copy what it is given, it
            // just keeps the reference (see Core/CapturedSprite.cs), so each Entry's Sprite needs
            // its OWN array or the very next object captured would silently repaint this one's
            // picture too, the moment its capture runs. AvatarCapture never hits this because it
            // already builds a fresh `cropped` array out of its scratch buffer; there is no crop
            // step here, so the copy has to be made explicitly instead.
            var owned = new byte[texW * texH * 4];
            Array.Copy(rgba, owned, owned.Length);
            e.Sprite.Set(owned, texW, texH, frameH);
            e.CapturedAt = Time.time;
            return true;
        }
        catch (Exception ex)
        {
            NightfallPlugin.Logger?.LogWarning($"[Nightfall] Relay capture failed: {ex.Message}");
            e.CapturedAt = Time.time;      // do not hammer a failing object every frame
            return false;
        }
        finally
        {
            foreach (var (go, layer) in moved)
            {
                try { if (go != null) go.layer = layer; } catch { }
            }
            if (activeSet) { try { RenderTexture.active = previous; } catch { } }
            // The camera, its RenderTexture and the readback Texture2D are all persistent/pooled
            // now (AUDIT-2026-09-03) - nothing to destroy per capture. They are freed in Clear().
        }
    }
}
