// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightfallDiag - autotest of the first-person view, ONLY with the environment variable
 * NIGHTFALL_DIAG=gpu (never on by default; see the project rule about diagnostics leaking into real
 * games). In a freeplay: switches the view on, waits for the GPU model, then stands the player in
 * up to ten rooms, looks two ways in each, logs the frame time and saves a screenshot per view to
 * BepInEx/nightfall-diag. Ends with the log line "[Nightfall/Diag] gpu done".
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nightfall;

internal static class NightfallDiag
{
    private static readonly bool On =
        string.Equals(Environment.GetEnvironmentVariable("NIGHTFALL_DIAG"), "gpu", StringComparison.OrdinalIgnoreCase);

    /// True only inside the autotest (environment variable set).
    public static bool Active => On;

    private enum Step { Wait, Activate, Shots, Done }

    private static Step step = Step.Wait;
    private static float t0 = -1f, stepAt;
    private static readonly List<(Vector2 pos, float heading, string name)> poses = new();
    private static int pose = -1;
    private static int frames;
    private static float frameTime;
    private static bool shot;

    private static string Folder => Path.Combine(BepInEx.Paths.BepInExRootPath, "nightfall-diag");

    public static void Tick()
    {
        if (!On || step == Step.Done) return;
        try
        {
            var me = PlayerControl.LocalPlayer;
            if (me == null || ShipStatus.Instance == null) return;
            float now = Time.realtimeSinceStartup;
            if (t0 < 0f) { t0 = now; Log("ship found, waiting"); }

            switch (step)
            {
                case Step.Wait:
                    if (now - t0 < 6f) return;
                    NightfallState.ManualOverride = true;
                    step = Step.Activate;
                    stepAt = now;
                    Log($"view requested; map '{SceneGeometry.CurrentMapKey}', package '{GpuWorld.PackKeyForCurrentMap()}'");
                    return;

                case Step.Activate:
                    if (NightfallView.IsActive && GpuWorld.Visible)
                    {
                        BuildPoses();
                        Directory.CreateDirectory(Folder);
                        step = Step.Shots;
                        stepAt = now;
                        Log($"GPU view up after {now - t0:F1} s; {poses.Count} views");
                        return;
                    }
                    if (now - stepAt > 45f)
                    {
                        Log($"GPU view did not come up (view active {NightfallView.IsActive}, model ready {GpuWorld.Ready})");
                        Finish();
                    }
                    return;

                case Step.Shots:
                    RunShots(me, now);
                    return;
            }
        }
        catch (Exception e)
        {
            Log("failed: " + e);
            Finish();
        }
    }

    private static void BuildPoses()
    {
        poses.Clear();
        var seen = new HashSet<string>();
        try
        {
            foreach (var room in ShipStatus.Instance.AllRooms)
            {
                if (room == null || room.roomArea == null) continue;
                var c = (Vector2)room.roomArea.bounds.center;
                string name = room.RoomId.ToString();
                if (!seen.Add(name) || GpuWorld.GroundAt(c.x, c.y) == null) continue;
                poses.Add((c, MathF.PI * 0.5f, name + "_N"));
                poses.Add((c, -MathF.PI * 0.5f, name + "_S"));
                if (poses.Count >= 20) break;
            }
        }
        catch { }
        if (poses.Count == 0)
        {
            var p = PlayerControl.LocalPlayer.GetTruePosition();
            poses.Add((p, MathF.PI * 0.5f, "spawn_N"));
            poses.Add((p, 0f, "spawn_E"));
        }
    }

    private static void RunShots(PlayerControl me, float now)
    {
        if (pose < 0 || (shot && now - stepAt > 0.6f))
        {
            if (pose >= 0 && frames > 0)
                Log($"view {poses[pose].name}: {frameTime / frames * 1000f:F1} ms per frame over {frames} frames");
            pose++;
            if (pose >= poses.Count) { Finish(); return; }
            var (pos, heading, _) = poses[pose];
            try { me.NetTransform.SnapTo(pos); } catch { me.transform.position = new Vector3(pos.x, pos.y, me.transform.position.z); }
            NightfallControls.DiagLook(heading);
            stepAt = now;
            frames = 0;
            frameTime = 0f;
            shot = false;
            return;
        }
        float age = now - stepAt;
        if (age > 1.0f && age < 2.0f) { frames++; frameTime += Time.unscaledDeltaTime; }
        if (!shot && age >= 2.0f)
        {
            string f = Path.Combine(Folder, $"{GpuWorld.LoadedKey}_{pose:00}_{poses[pose].name}.png");
            ScreenCapture.CaptureScreenshot(f);
            shot = true;
            stepAt = now;            // the 0.6 s after the shot let the file be written
        }
    }

    private static void Finish()
    {
        step = Step.Done;
        NightfallState.ManualOverride = false;
        Log("gpu done");
    }

    private static void Log(string s) => NightfallPlugin.Logger?.LogInfo("[Nightfall/Diag] " + s);
}
