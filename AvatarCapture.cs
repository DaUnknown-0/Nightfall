// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * AvatarCapture - photographs each player so the first-person view shows the real Among Us
 * character, cosmetics and all, instead of a drawing of one.
 *
 * HOW A SINGLE OBJECT IS PHOTOGRAPHED
 * -----------------------------------
 * Rendering one specific object and nothing else needs a way to separate it from the scene, and
 * Unity offers exactly one cheap way to do that: layers. The player's cosmetics are moved to an
 * unused layer for the duration of a single manual camera render, the camera is set to see only
 * that layer, and everything is put back. The move is undone in a `finally`, because a crewmate
 * left on the wrong layer would turn invisible to the game's own cameras for the rest of the round.
 *
 * Layer 30 is used: the survey lists Among Us' named layers up to 19, so 30 belongs to nobody.
 *
 * WHY NOT EVERY FRAME
 * -------------------
 * A capture costs a render plus a readback, which is far too much to repeat per player per frame.
 * Players are re-photographed on a slow rotation and whenever something about them changes that
 * the picture would miss. The cost of that is that walking animation is frozen between captures,
 * which at the distances this view is played at is invisible; what is very visible, and what this
 * buys, is that the shape in the beam is wearing the hat you know them by.
 */

using System;
using System.Collections.Generic;
using Nightfall.Core;
using UnityEngine;

namespace Nightfall;

public static class AvatarCapture
{
    /// A layer no Among Us object uses (the game names layers up to 19).
    private const int IsolationLayer = 30;

    /// Resolution of one avatar photograph. A crewmate is about 0.7 world units tall and is drawn
    /// at most a couple of hundred pixels high on screen, so this is generous.
    private const int TexHeight = 192;

    private sealed class Entry
    {
        public CapturedSprite Sprite = new();
        public float CapturedAt = -99f;
        public int ColorId = -1;
        public string Cosmetics = "";
    }

    private static readonly Dictionary<byte, Entry> entries = new();
    /// Pets are photographed separately, under the same player id. See ForPet.
    private static readonly Dictionary<byte, Entry> petEntries = new();

    /// One player is re-photographed at most this often. Spread across players, so the cost is one
    /// capture every few frames at worst rather than fifteen at once.
    private const float RefreshSeconds = 2.5f;
    private static float lastAnyCapture;

    // AUDIT-2026-09-03: CaptureInto used to build a fresh Camera GameObject, RenderTexture, readback
    // Texture2D, Color32[] and byte[] on EVERY capture. With up to fifteen players and a pet each
    // re-photographed every RefreshSeconds, that was one full set of Unity/GC garbage every couple
    // of seconds for the whole round. The camera is now a single persistent, disabled object reused
    // for every capture; render targets are pooled by (rounded) pixel size instead of one per shot;
    // the row-flip scratch buffer is a single reused array grown on demand. None of this changes
    // what gets photographed or how it looks - only what gets thrown away afterwards.
    private static GameObject captureCamGo;
    private static Camera captureCam;

    private sealed class RtEntry
    {
        public RenderTexture Rt;
        public Texture2D Readback;
    }

    /// Render targets, keyed by their exact (already-rounded) pixel size. Rounding the requested
    /// size up to a multiple of 32 before the lookup keeps this small - a handful of buckets rather
    /// than one entry per crewmate's exact silhouette width.
    private static readonly Dictionary<(int w, int h), RtEntry> rtCache = new();
    /// Insertion order of `rtCache`, so a cache that somehow grows past `MaxRtCacheEntries` (an
    /// odd mix of silhouette sizes across a round with many players) has something to evict by.
    private static readonly List<(int w, int h)> rtOrder = new();
    /// A hard ceiling on how many distinct render-target sizes are pooled at once. Each entry is a
    /// RenderTexture plus a readback Texture2D, so this bounds the pool's GPU/CPU memory rather
    /// than letting it grow for the whole round.
    private const int MaxRtCacheEntries = 24;

    private static byte[] rgbaScratch = Array.Empty<byte>();

    private static Camera GetCamera()
    {
        if (captureCam == null)
        {
            captureCamGo = new GameObject("NightfallAvatarCam");
            UnityEngine.Object.DontDestroyOnLoad(captureCamGo);
            captureCam = captureCamGo.AddComponent<Camera>();
            captureCam.orthographic = true;
            captureCam.cullingMask = 1 << IsolationLayer;
            captureCam.clearFlags = CameraClearFlags.SolidColor;
            captureCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent: alpha carries the cutout
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
            // A stale cache entry pointing at one would fail this player's capture every single
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
        petEntries.Clear();

        foreach (var e in rtCache.Values) DestroyRt(e);
        rtCache.Clear();
        rtOrder.Clear();

        try { if (captureCamGo != null) UnityEngine.Object.Destroy(captureCamGo); } catch { }
        captureCamGo = null;
        captureCam = null;
    }

    /// The sprite for a player, photographing them first if needed. Returns null while no valid
    /// photograph exists, which the caller treats as "fall back to the drawn crewmate".
    public static CapturedSprite For(PlayerControl p)
    {
        if (p == null || p.Data == null) return null;

        if (!entries.TryGetValue(p.PlayerId, out var e))
        {
            e = new Entry();
            entries[p.PlayerId] = e;
        }

        bool stale = Time.time - e.CapturedAt > RefreshSeconds;
        bool changed = e.ColorId != ColorIdOf(p) || e.Cosmetics != CosmeticsKeyOf(p);

        // Only one capture per frame across all players: the readback stalls the render thread and
        // fifteen of them in a row on the frame a werewolf transforms would be felt as a hitch.
        if ((stale || changed || !e.Sprite.IsValid) && Time.time - lastAnyCapture > 0.05f)
        {
            if (CaptureInto(p, e, false)) lastAnyCapture = Time.time;
        }

        return e.Sprite.IsValid ? e.Sprite : null;
    }

    /*
     * THE PET IS NOT PART OF THE PLAYER, and treating it as part of the photograph was wrong twice
     * over. It is a separate object in the scene with its own physics: `PetBehaviour` walks after
     * its owner with a lag, a minimum distance and a snap distance, so at any moment it is up to a
     * metre away and often on the other side. Baked into the owner's sprite it would either be
     * missing (it is not under the cosmetics object) or, worse, pasted rigidly at the owner's hip.
     *
     * So it gets its own photograph and its own billboard, planted where the pet actually is. That
     * is also the only version in which the first-person view can be honest about it: a pet is a
     * second thing moving in the dark, and a crewmate hunting a werewolf should be able to mistake
     * one for the other.
     */
    public static CapturedSprite ForPet(PlayerControl p, out Vector2 position)
    {
        position = default;
        if (p == null || p.Data == null) return null;

        var pet = PetOf(p);
        if (pet == null) return null;
        var go = pet.gameObject;
        if (go == null || !go.activeInHierarchy) return null;

        var t = pet.transform;
        position = new Vector2(t.position.x, t.position.y);

        if (!petEntries.TryGetValue(p.PlayerId, out var e))
        {
            e = new Entry();
            petEntries[p.PlayerId] = e;
        }

        bool stale = Time.time - e.CapturedAt > RefreshSeconds;
        bool changed = e.ColorId != ColorIdOf(p) || e.Cosmetics != CosmeticsKeyOf(p);
        if ((stale || changed || !e.Sprite.IsValid) && Time.time - lastAnyCapture > 0.05f)
        {
            if (CaptureInto(p, e, true)) lastAnyCapture = Time.time;
        }

        return e.Sprite.IsValid ? e.Sprite : null;
    }

    private static PetBehaviour PetOf(PlayerControl p)
    {
        try { return p.cosmetics != null ? p.cosmetics.currentPet : null; }
        catch { return null; }
    }

    private static int ColorIdOf(PlayerControl p)
    {
        try { return CurrentOutfit(p).ColorId; } catch { return -1; }
    }

    /*
     * The CURRENT outfit, not the default one. The photograph always shows whatever the renderers
     * are showing, so a morphed or disguised player already looks right - but this key is what
     * decides WHEN to take a new photograph, and against `DefaultOutfit` a Morphling keeps the old
     * picture for as long as the refresh timer runs. Among Us keeps the active outfit in
     * `PlayerControl.CurrentOutfit`; the default is the fallback for anything that does not have it.
     */
    private static NetworkedPlayerInfo.PlayerOutfit CurrentOutfit(PlayerControl p)
    {
        try
        {
            var o = p.CurrentOutfit;
            if (o != null) return o;
        }
        catch { }
        return p.Data.DefaultOutfit;
    }

    private static string CosmeticsKeyOf(PlayerControl p)
    {
        try
        {
            var o = CurrentOutfit(p);
            return $"{o.HatId}|{o.SkinId}|{o.VisorId}|{o.PetId}";
        }
        catch { return ""; }
    }

    // ================================================================================
    private static bool CaptureInto(PlayerControl p, Entry e, bool petOnly)
    {
        var moved = new List<(GameObject go, int layer)>();
        var masked = new List<(SpriteRenderer r, SpriteMaskInteraction mode)>();
        RenderTexture rt = null;
        RenderTexture previous = null;
        // Whether `previous` was actually captured from RenderTexture.active before this method
        // set it to something else. Restoring on `previous != null` is wrong: a legitimately null
        // "no active render texture" state would then never be restored, and the render target this
        // method pooled would stay stuck as RenderTexture.active if ReadPixels/Apply throws.
        bool activeSet = false;
        Texture2D readback = null;

        try
        {
            var pet = PetOf(p);
            var petGo = pet != null ? pet.gameObject : null;

            GameObject root;
            if (petOnly)
            {
                root = petGo;
                if (root == null || !root.activeInHierarchy) return false;
            }
            else
            {
                root = p.cosmetics != null ? p.cosmetics.gameObject : p.gameObject;
                if (root == null) return false;
            }

            /*
             * ISOLATE - but only the SPRITE renderers.
             *
             * `CosmeticsLayer` carries a `nameText` and a `colorBlindText` (both TextMeshPro, so
             * MeshRenderers) beside the body, the hat, the skin and the visor. Moved along with the
             * rest they were photographed INTO the avatar, and every crewmate in the beam wore its
             * own floating name plate baked into its chest. Every piece of cosmetics Among Us has -
             * HatParent's front and back layer, SkinLayer.layer, VisorLayer.Image, the body sprite,
             * the hand hat - is a SpriteRenderer, so the filter costs nothing and excludes text by
             * construction rather than by name.
             *
             * The pet is excluded here as well and captured on its own pass: it is a separate
             * object that walks after its owner, so its pixels do not belong in the owner's frame.
             */
            // Typed as SpriteRenderer, not Renderer: the filter below only ever accepts sprite
            // renderers anyway, and the mask suspension further down needs the concrete type.
            var mine = new List<SpriteRenderer>();
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r == null || !r.enabled) continue;
                if (r.sprite == null) continue;
                if (!petOnly && petGo != null && r.transform.IsChildOf(petGo.transform)) continue;
                mine.Add(r);
            }
            if (mine.Count == 0) return false;

            foreach (var r in mine)
            {
                var go = r.gameObject;
                moved.Add((go, go.layer));
                go.layer = IsolationLayer;
            }

            /*
             * SUSPEND SPRITE MASKING FOR THE SHOT.
             *
             * The body, the visor and the skin render with `VisibleInsideMask`: Among Us shows them
             * only inside the player's sight mask (the same fact the Illusionist's clone has to work
             * around, see UnknownsCollection/IllusionistClone.cs). The isolation camera sees layer 30
             * and nothing else, so there is no mask in its view - and those three parts came out
             * completely empty while the hat, which usually sits at `None`, photographed fine. The
             * result was a hat and a suit floating with no crewmate inside them.
             *
             * Masking is therefore switched off for the duration of the render and restored in the
             * same `finally` that restores the layers, because a renderer left at `None` would stay
             * visible through walls for the rest of the round.
             */
            foreach (var r in mine)
            {
                masked.Add((r, r.maskInteraction));
                r.maskInteraction = SpriteMaskInteraction.None;
            }

            /*
             * THE FRAME IS FITTED TO WHAT IS BEING PHOTOGRAPHED, not fixed.
             *
             * It used to be a 1.5-unit square centred half a unit above the player, which works for
             * a bare crewmate and is a gamble on everything else: the tall hats (top hat, plant,
             * flower pot, the security-camera one) reach well past it and would have come out with
             * their tops cut off, and a pet in that frame would be a speck in the middle of a lot
             * of nothing. The union of the sprite bounds answers both at once, and it also puts the
             * frame's bottom edge on the character's FEET, which is where the renderer plants the
             * billboard.
             */
            var bounds = mine[0].bounds;
            for (int i = 1; i < mine.Count; i++) bounds.Encapsulate(mine[i].bounds);

            const float margin = 1.04f;
            float frameH = Mathf.Clamp(bounds.size.y * margin, 0.25f, 4f);
            float frameW = Mathf.Clamp(bounds.size.x * margin, 0.25f, 4f);
            var centre = new Vector3(bounds.center.x, bounds.center.y, 0f);
            int texH = TexHeight;
            int texW = Mathf.Clamp(Mathf.RoundToInt(texH * frameW / frameH), 8, 512);

            // Round up to a multiple of 32 so the render-target cache (keyed by exact pixel size)
            // settles on a handful of buckets instead of a fresh RenderTexture per crewmate's exact
            // silhouette. The camera's aspect is matched to the ROUNDED size below, so the extra
            // pixels widen the frame slightly rather than stretching the picture into it.
            texW = RoundUp32(texW);
            texH = RoundUp32(texH);
            frameW = frameH * texW / texH;

            var cam = GetCamera();
            cam.orthographicSize = frameH * 0.5f;
            cam.aspect = frameW / frameH;
            cam.transform.position = new Vector3(centre.x, centre.y, -20f);

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
            // A rendered texture starts at the bottom row, the renderer's billboards start at the
            // top, so the rows are flipped here rather than in the per-pixel sampling path.
            for (int y = 0; y < texH; y++)
            {
                int srcRow = (texH - 1 - y) * texW;
                int dstRow = y * texW;
                for (int x = 0; x < texW; x++)
                {
                    var c = src[srcRow + x];
                    int o = (dstRow + x) * 4;
                    rgba[o] = c.r;
                    rgba[o + 1] = c.g;
                    rgba[o + 2] = c.b;
                    rgba[o + 3] = c.a;
                }
            }

            // Trim the transparent margin so the sprite's own height is what gets scaled into the
            // scene. Without this every character would be drawn inside an invisible box and would
            // look far too small.
            if (!Trim(rgba, texW, texH, out int top, out int bottom)) return false;
            int cropH = bottom - top + 1;
            var cropped = new byte[texW * cropH * 4];
            Array.Copy(rgba, top * texW * 4, cropped, 0, cropped.Length);

            float worldHeight = frameH * cropH / texH;
            e.Sprite.Set(cropped, texW, cropH, worldHeight);
            e.CapturedAt = Time.time;
            e.ColorId = ColorIdOf(p);
            e.Cosmetics = CosmeticsKeyOf(p);
            return true;
        }
        catch (MissingMethodException ex)
        {
            // Il2Cpp binding that a future Among Us no longer has. Said once, loudly, because the
            // symptom otherwise is "everybody is a drawn crewmate again" with nothing in the log.
            NightfallPlugin.Logger?.LogWarning(
                $"[Nightfall] Avatar capture unavailable on this build: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            // Stamp the attempt even though it failed (AUDIT M-18) - the same line WorldRelay's
            // capture already carries. Without it the staleness gate below (Time.time - CapturedAt >
            // RefreshSeconds) stays true forever, so a permanently failing capture - a MissingMethod
            // after an Among Us update is the obvious way to get one - re-ran the FULL capture path
            // and wrote this warning every single frame, for every player, for the whole round.
            // With the stamp a broken capture retries once per RefreshSeconds and the log stays
            // readable enough to actually notice it.
            e.CapturedAt = Time.time;
            NightfallPlugin.Logger?.LogWarning($"[Nightfall] Avatar capture failed: {ex.Message}");
            return false;
        }
        finally
        {
            // Put every renderer back on its own layer, whatever happened above.
            foreach (var (go, layer) in moved)
            {
                try { if (go != null) go.layer = layer; } catch { }
            }
            // ... and back under its own mask rule. Skipping this would leave players visible
            // through walls, which is a far worse bug than the one the suspension fixes.
            foreach (var (r, mode) in masked)
            {
                try { if (r != null) r.maskInteraction = mode; } catch { }
            }
            if (activeSet) { try { RenderTexture.active = previous; } catch { } }
            // The camera, its RenderTexture and the readback Texture2D are all persistent/pooled
            // now (AUDIT-2026-09-03) - nothing to destroy per capture. They are freed in Clear().
        }
    }

    /// Finds the first and last row that contain anything visible.
    private static bool Trim(byte[] rgba, int w, int h, out int top, out int bottom)
    {
        top = -1;
        bottom = -1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                if (rgba[row + x * 4 + 3] < 24) continue;
                if (top < 0) top = y;
                bottom = y;
                break;
            }
        }
        return top >= 0 && bottom >= top;
    }
}
