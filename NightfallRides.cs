// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightfallRides - the three ways a player leaves the floor, read from the game itself.
 *
 * THE PROBLEM, IN ONE SENTENCE
 * The built world answers "how high is the ground here" with a step function over rectangles
 * (AreaBuilder.GroundAt), and that is right for every place a player WALKS - but the Airship and
 * the Fungle also carry players: across the Gap Room's pit on a moving platform, up and down
 * seven ladders, and along the Fungle's zipline. During a ride the player's position is over
 * something that is not where their feet are - the pit floor 1.8 units down, a ladder's wall
 * band, a jungle two levels below the cable - and the eye followed the ground, not the ride. That
 * is the "sometimes I glitch under the map" report, and the platform on top of it "does not
 * move" because nothing in the map data is a platform: area files describe places, and the
 * platform is a thing.
 *
 * WHAT THIS DOES
 * Once per world build it finds the game's own objects - MovingPlatformBehaviour, every Ladder,
 * the ZiplineBehaviour - and remembers the geometry they carry: the platform's two end positions
 * and disc radius, each ladder's bottom and top, the zipline's handle line and its two landings.
 * Every frame it then answers one question for the view: "is the local player on a ride, and if
 * so, what is the ground under them REALLY?"
 *
 *   platform  the player is within the disc of the platform's current position -> the deck the
 *             platform's own use positions stand on (the floors beside the pit), not the pit
 *   ladder    the player's position lies on the segment between a ladder's two ends -> the two
 *             decks blended by how far along they are, so the climb is a glide, not a jump
 *   zipline   the same along the cable, between the two landing grounds
 *
 * It is geometry, not state: it never asks the game "is this player climbing", because
 * PlayerPhysics does not say so cleanly and the coroutine that moves the player is private. A
 * player standing on a ladder's line is treated as on the ladder - and since a ladder's segment
 * is inside a wall band in the map data, no walking player is ever there.
 *
 * The platform is also SHOWN: Scene3D builds its disc at twenty-four positions along the ride
 * and this file tells it each frame which one to reveal (SetPlatformPosition).
 */

using System;
using System.Collections.Generic;
using Nightfall.Core;
using UnityEngine;

namespace Nightfall;

public static class NightfallRides
{
    private sealed class LadderRun
    {
        public Vector2 Bottom, Top;
        public string Name;
    }

    private static MovingPlatformBehaviour platform;
    private static Vector2 platformLeftUse, platformRightUse;
    private static float platformRadius;
    private static readonly List<LadderRun> ladders = new();
    private static ZiplineBehaviour zipline;
    private static Vector2 zipTop, zipBottom, zipLandTop, zipLandBottom;
    private static bool zipValid;

    // Tolerances, in world units: how far off a ladder's or the cable's line a player may be and
    // still count as on it. Ladders are narrow and the climb animation keeps the player exactly
    // on the line; the zipline swings a little.
    private const float LadderTolerance = 0.45f;
    private const float ZiplineTolerance = 0.7f;

    public static void Clear()
    {
        platform = null;
        ladders.Clear();
        zipline = null;
        zipValid = false;
    }

    /// Finds the rides in the current map. Called right before the world is built so the platform
    /// spec can go into the build; harmless on maps that have none of them.
    public static Scene3D.PlatformSpec Discover()
    {
        Clear();
        var spec = new Scene3D.PlatformSpec();
        try
        {
            var ship = ShipStatus.Instance;
            if (ship == null) return spec;

            // ---- platform ----
            try
            {
                platform = ship.GetComponentInChildren<MovingPlatformBehaviour>(true);
                if (platform != null)
                {
                    var l = platform.LeftPosition;
                    var r = platform.RightPosition;
                    platformLeftUse = new Vector2(platform.LeftUsePosition.x, platform.LeftUsePosition.y);
                    platformRightUse = new Vector2(platform.RightUsePosition.x, platform.RightUsePosition.y);
                    platformRadius = 0.75f;
                    try
                    {
                        var sr = platform.GetComponentInChildren<SpriteRenderer>();
                        if (sr != null && sr.bounds.extents.x > 0.2f)
                            platformRadius = Mathf.Clamp(sr.bounds.extents.x, 0.4f, 1.5f);
                    }
                    catch { }
                    spec = new Scene3D.PlatformSpec
                    {
                        Left = new NfVec2(l.x, l.y),
                        Right = new NfVec2(r.x, r.y),
                        Radius = platformRadius,
                        Valid = true,
                    };
                    NightfallPlugin.Logger?.LogInfo(
                        $"[Nightfall] moving platform found: left ({l.x:0.##}, {l.y:0.##}), right ({r.x:0.##}, {r.y:0.##}), radius {platformRadius:0.##}.");
                }
            }
            catch (Exception e) { NightfallPlugin.Logger?.LogWarning($"[Nightfall] platform lookup failed: {e.Message}"); platform = null; }

            // ---- ladders ----
            try
            {
                foreach (var ld in UnityEngine.Object.FindObjectsOfType<Ladder>())
                {
                    if (ld == null || ld.IsTop || ld.Destination == null) continue;
                    var b = ld.transform.position;
                    var t = ld.Destination.transform.position;
                    ladders.Add(new LadderRun
                    {
                        Bottom = new Vector2(b.x, b.y),
                        Top = new Vector2(t.x, t.y),
                        Name = ld.gameObject.name,
                    });
                }
                if (ladders.Count > 0)
                    NightfallPlugin.Logger?.LogInfo($"[Nightfall] {ladders.Count} ladder(s) found for the ride ground.");
            }
            catch (Exception e) { NightfallPlugin.Logger?.LogWarning($"[Nightfall] ladder lookup failed: {e.Message}"); }

            // ---- zipline ----
            try
            {
                zipline = ship.GetComponentInChildren<ZiplineBehaviour>(true);
                if (zipline != null && zipline.handleTop != null && zipline.handleBottom != null
                    && zipline.landingPositionTop != null && zipline.landingPositionBottom != null)
                {
                    zipTop = zipline.handleTop.position;
                    zipBottom = zipline.handleBottom.position;
                    zipLandTop = zipline.landingPositionTop.position;
                    zipLandBottom = zipline.landingPositionBottom.position;
                    zipValid = true;
                    NightfallPlugin.Logger?.LogInfo(
                        $"[Nightfall] zipline found: top ({zipTop.x:0.##}, {zipTop.y:0.##}) -> bottom ({zipBottom.x:0.##}, {zipBottom.y:0.##}).");
                }
            }
            catch (Exception e) { NightfallPlugin.Logger?.LogWarning($"[Nightfall] zipline lookup failed: {e.Message}"); zipValid = false; }
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogWarning($"[Nightfall] ride discovery failed: {e.Message}");
        }
        return spec;
    }

    /// Tells the scene where the platform is right now, so the matching slot is drawn.
    public static void SyncPlatform(Scene3D scene)
    {
        if (scene == null || platform == null) return;
        try
        {
            var p = platform.transform.position;
            scene.SetPlatformPosition(new NfVec2(p.x, p.y));
        }
        catch { platform = null; }
    }

    /// The ground under a player on a ride, or null when they are simply walking.
    public static float? GroundOverride(Vector2 pos, Scene3D scene)
    {
        if (scene == null) return null;

        // Platform: the disc under the player's feet is the ride, whatever the pit says.
        if (platform != null)
        {
            try
            {
                var pp = platform.transform.position;
                float dx = pos.x - pp.x, dy = pos.y - pp.y;
                float reach = platformRadius + 0.3f;
                if (dx * dx + dy * dy <= reach * reach)
                {
                    // The deck the platform serves is the one its use positions stand on.
                    float gl = scene.GroundAt(new NfVec2(platformLeftUse.x, platformLeftUse.y));
                    float gr = scene.GroundAt(new NfVec2(platformRightUse.x, platformRightUse.y));
                    return Mathf.Max(gl, gr);
                }
            }
            catch { platform = null; }
        }

        // Ladders: blend between the ground at either end along the climb.
        foreach (var ld in ladders)
        {
            if (!OnSegment(pos, ld.Bottom, ld.Top, LadderTolerance, out float t)) continue;
            var dir = (ld.Top - ld.Bottom).normalized;
            // Sample the decks a little PAST each end: the ends themselves sit in the wall band
            // the ladder climbs, where GroundAt would read the wrong side.
            float g0 = scene.GroundAt(ToNf(ld.Bottom - dir * 0.45f));
            float g1 = scene.GroundAt(ToNf(ld.Top + dir * 0.45f));
            return Mathf.Lerp(g0, g1, Smooth(t));
        }

        // Zipline: the same along the cable, between the two landings' grounds.
        if (zipValid && OnSegment(pos, zipTop, zipBottom, ZiplineTolerance, out float zt))
        {
            float g0 = scene.GroundAt(ToNf(zipLandTop));
            float g1 = scene.GroundAt(ToNf(zipLandBottom));
            return Mathf.Lerp(g0, g1, zt);
        }

        return null;
    }

    private static NfVec2 ToNf(Vector2 v) => new(v.x, v.y);

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    /// Is `p` within `tol` of the segment a-b, strictly between its ends? t is the fraction along.
    private static bool OnSegment(Vector2 p, Vector2 a, Vector2 b, float tol, out float t)
    {
        t = 0f;
        var ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-4f) return false;
        t = Vector2.Dot(p - a, ab) / len2;
        // Strictly inside: at the very ends the player is standing on the deck, and the deck rule
        // is the right one there.
        if (t <= 0.04f || t >= 0.96f) return false;
        var closest = a + ab * t;
        return (p - closest).sqrMagnitude <= tol * tol;
    }
}
