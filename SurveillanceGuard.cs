// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.
//
// Keeps the first-person screen out of the security camera feeds.
//
// Report 2026-10-19 00:17 (Skeld, Security): one of the four feeds of the camera minigame was black
// except for a thin strip along its top edge. The feeds are rendered by cameras the minigame
// instantiates from its CameraPrefab (SurveillanceMinigame.Begin, one per viewport), and those
// cameras carry the prefab's culling mask, which - like the world camera's - includes layer 1. That
// is the layer NightfallView parks its full-screen sprite on: a quad one unit in front of the main
// camera, i.e. at the player's own position, sized to the screen. The feed that looks at the room
// the player stands in therefore photographs that sprite head-on, and the sprite is the overlay
// (black where the hand light does not reach), so the feed goes black wherever the quad covers it.
// The strip at the top was the part of the feed beyond the quad's upper edge.
//
// The fix strips layer 1 (and Nightfall's own model layer, for good measure) from every camera the
// open minigame owns. Done from the per-frame driver rather than a patch per minigame type: Polus,
// Airship and Fungle use different minigame classes with the same camera prefab arrangement, and a
// patch bound to a method a subclass does not declare throws at PatchAll.

using System;
using UnityEngine;

namespace Nightfall;

internal static class SurveillanceGuard
{
    private const int HiddenLayers = (1 << 1) | (1 << 31);

    private static IntPtr lastInstance = IntPtr.Zero;
    private static int frames;

    public static void Tick()
    {
        var mg = Minigame.Instance;
        if (mg == null)
        {
            lastInstance = IntPtr.Zero;
            return;
        }
        if (mg.TryCast<SurveillanceMinigame>() == null && mg.TryCast<PlanetSurveillanceMinigame>() == null
            && mg.TryCast<FungleSurveillanceMinigame>() == null)
            return;
        // The cameras are created inside Begin, after Minigame.Instance is set; look again for a
        // few frames so none created late is missed, then leave the minigame alone.
        if (mg.Pointer != lastInstance)
        {
            lastInstance = mg.Pointer;
            frames = 0;
        }
        if (frames > 30 || (frames > 3 && frames % 10 != 0)) { frames++; return; }
        frames++;
        var cams = mg.GetComponentsInChildren<Camera>(true);
        if (cams == null) return;
        foreach (var cam in cams)
        {
            if (cam == null) continue;
            int have = cam.cullingMask;
            int want = have & ~HiddenLayers;
            if (want == have) continue;
            cam.cullingMask = want;
            NightfallPlugin.Logger?.LogInfo(
                $"[Nightfall] Surveillance camera '{cam.name}' mask {have} -> {want} (first-person screen hidden from the feed).");
        }
    }
}
