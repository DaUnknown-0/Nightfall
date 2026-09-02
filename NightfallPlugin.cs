// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * Nightfall - a TRUE first-person view for Among Us.
 *
 * When the Werewolf of "Unknown's Collection" transforms, every living player drops out of the
 * top-down view and into a first-person perspective raycast from the map's own collision geometry:
 * real walls in real perspective, other players as figures in the room, a flashlight in hand. The
 * werewolf itself gets the other side of it - no torch, a red predator's sight, and its own claws
 * at the bottom of the screen.
 *
 * HOW IT IS PUT TOGETHER
 * ----------------------
 *   Core\            the renderer. No Unity anywhere in it, on purpose: the same files are compiled
 *                    into an offline tool that draws the identical picture into PNGs, which is how
 *                    the look is checked and corrected without launching the game. Feeding that
 *                    offline tool from a running game (the map survey, the map photograph, the
 *                    per-sprite dump) is NightfallSurveyTool now, a separate plugin - none of it is
 *                    something a player needs installed, so none of it ships in this DLL any more.
 *   SceneGeometry    builds the renderer's world from the live scene, once per round.
 *   NightfallView    owns the screen: one full-screen sprite on the world camera, under the HUD.
 *   NightfallControls turns the head with the mouse and rotates movement into view space.
 *   NightfallState   decides when the world flips, and gates it on the lobby handshake.
 *
 * The mod reads Unknown's Collection by reflection and never references it, so it loads happily
 * without it and never has to be rebuilt when UC is.
 */

global using Il2CppInterop.Runtime;
global using Il2CppInterop.Runtime.Attributes;
global using Il2CppInterop.Runtime.InteropTypes;
global using Il2CppInterop.Runtime.InteropTypes.Arrays;
global using Il2CppInterop.Runtime.Injection;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("Among Us.exe")]
public class NightfallPlugin : BasePlugin
{
    public const string PluginGuid = "com.tormod.nightfall";
    public const string PluginName = "Nightfall";
    public const string PluginVersion = "0.3.1.8";
    public static readonly System.Version Version = System.Version.Parse(PluginVersion);

    public static ManualLogSource Logger { get; private set; }

    // ---- feature ----
    public static ConfigEntry<bool> Enabled { get; private set; }
    public static ConfigEntry<bool> RequireEveryone { get; private set; }
    public static ConfigEntry<bool> RelativeMovement { get; private set; }

    // ---- look ----
    public static ConfigEntry<int> RenderWidth { get; private set; }
    public static ConfigEntry<float> FieldOfView { get; private set; }
    public static ConfigEntry<float> TorchRange { get; private set; }
    public static ConfigEntry<float> TurnSpeed { get; private set; }
    public static ConfigEntry<float> MouseSensitivity { get; private set; }

    // ---- keys ----
    public static ConfigEntry<bool> ShowKeyOnButton { get; private set; }
    public static ConfigEntry<bool> KeysAlwaysOn { get; private set; }

    public static Harmony Harmony { get; } = new Harmony(PluginGuid);

    public override void Load()
    {
        Logger = Log;

        NightfallOptions.Bind(Config);
        Enabled = Config.Bind("Nightfall", "Enabled", true,
            "Switch the first-person view on when Unknown's Collection's werewolf transforms.");
        RequireEveryone = Config.Bind("Nightfall", "RequireEveryone", true,
            "Only arm the view when every player in the lobby has Nightfall installed. Whoever is "
            + "missing it would otherwise keep the top-down overview during the hunt, which is a "
            + "real advantage. Turn off for solo testing.");
        RelativeMovement = Config.Bind("Nightfall", "RelativeMovement", true,
            "Move relative to where you are looking (W walks forwards). Off means Among Us' normal "
            + "world-axis movement, which is far less disorienting but also far less first person.");

        RenderWidth = Config.Bind("Look", "RenderWidth", 854,
            new ConfigDescription(
                "Internal horizontal resolution. The image is point-magnified to the screen, so "
                + "lower is chunkier and cheaper. Height follows at 16:9. Measured on Polus over "
                + "all 89 viewpoints with a full turn at each, worst viewpoint / average: "
                + "640x360 = 12,1 / 8,4 ms, 854x480 = 18,7 / 13,9 ms, 960x540 = 24,8 / 16,5 ms. "
                + "854 is the default because the step from 640 is the last one whose average "
                + "still leaves the game its own share of a sixty-hertz frame. Drop to 640 if the "
                + "machine is tight; 960 is for looking at the map rather than playing on it.",
                new AcceptableValueRange<int>(160, 1280)));
        FieldOfView = Config.Bind("Look", "FieldOfView", 75f,
            new ConfigDescription("Horizontal field of view in degrees.",
                new AcceptableValueRange<float>(50f, 110f)));
        TorchRange = Config.Bind("Look", "TorchRange", 13f,
            new ConfigDescription("How far the flashlight reaches, in world units.",
                new AcceptableValueRange<float>(4f, 30f)));
        TurnSpeed = Config.Bind("Look", "TurnSpeed", 9f,
            new ConfigDescription("How quickly the head follows the mouse.",
                new AcceptableValueRange<float>(2f, 30f)));

        ShowKeyOnButton = Config.Bind("Keys", "ShowKeyOnButton", true,
            "Print each ability's key in the top-right corner of its button. In the first-person "
            + "view the mouse turns the head instead of pointing, so the key is the only way to "
            + "fire an ability - and nothing in TOR, Unknown's Collection or Forgotten Fixes says "
            + "anywhere what it is.");
        KeysAlwaysOn = Config.Bind("Keys", "AlwaysOn", true,
            "Hand out keys and label the buttons all the time, not only while the first-person "
            + "view is up. On, because a key learned during the round is a key already known when "
            + "the lights go out; off if the labels are unwanted in the normal top-down game.");

        MouseSensitivity = Config.Bind("Look", "MouseSensitivity", 3.2f,
            new ConfigDescription("How far the view turns per unit of mouse movement.",
                new AcceptableValueRange<float>(0.5f, 12f)));

        var enabledEntry = Config.Bind("General", "Enabled", true,
            "Whether this mod is loaded at all. Kept separate from the feature-level `Nightfall.Enabled` "
            + "above so the Mod Manager's own enable/disable switch (which needs a restart either way) "
            + "does not fight with a toggle meant to be flipped mid-session.");

        // Register in the Mod Manager, same as this project family's other released mods (HostFix,
        // ChanceMod, UsefulTORStuff, UnknownsCollection) - via AppDomain, no compile-time reference,
        // so this works whether or not UsefulTORStuff (which hosts the Mod Manager UI) is installed.
        try
        {
            var modData = new Dictionary<string, object>
            {
                { "Guid", PluginGuid },
                { "Name", PluginName },
                { "Version", Version },
                { "RepositoryOwner", "DaUnknown-0" },
                { "RepositoryName", "Nightfall" },
                { "ButtonColor", new Color(0.68f, 0.36f, 0.95f) },
                { "Enabled", enabledEntry },
                { "RuntimeEnabled", enabledEntry.Value },
            };
            AppDomain.CurrentDomain.SetData($"ModManager.RegisteredMod.{PluginGuid}", modData);
            Logger.LogInfo($"[Nightfall] Registered in Mod Manager registry (runtime={enabledEntry.Value}).");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Nightfall] Failed to register in Mod Manager: {ex}");
        }

        if (!enabledEntry.Value)
        {
            Logger.LogInfo("[Nightfall] Disabled in config - skipping the rest of Load().");
            return;
        }

        try
        {
            Harmony.PatchAll();
            Logger.LogInfo($"[Nightfall] {PluginVersion} loaded. "
                           + "F9 forces the view on for testing.");
        }
        catch (Exception e)
        {
            Logger.LogError($"[Nightfall] Load failed: {e}");
        }

        // Self-updater: checks GitHub releases and offers an in-game update button.
        AddComponent<NightfallUpdater>();
    }
}

// Version display in the top-corner PingTracker readout, folded into the shared "Unknown's
// Collective" line alongside this project family's other mods (see UnknownsCollective.cs).
[HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
[HarmonyPriority(Priority.Low)]
internal static class NightfallVersionDisplayPatch
{
    // PERF: the line is built from a constant name and a constant version, so it was the same
    // string sixty times a second. Built once and held; nothing here can invalidate it.
    private static string cachedLine;

    public static void Postfix(PingTracker __instance)
    {
        if (__instance == null || __instance.text == null) return;
        string text = __instance.text.text;
        if (string.IsNullOrEmpty(text)) return;

        cachedLine ??= $"<color=#B18CFF>{NightfallPlugin.PluginName}</color> v{VersionDisplay.Format(NightfallPlugin.Version)}";
        UnknownsCollective.Contribute(NightfallPlugin.PluginGuid, cachedLine);
        text = UnknownsCollective.Render(__instance.text, text);

        // PERF: TextMeshPro rebuilds its mesh on EVERY assignment to .text, even when the string
        // is identical - the setter marks the text dirty without comparing. Six of our mods write
        // this same field one after another each frame (UC, UTS, Chance, HostFix, Nightfall,
        // ForceImpostor) and at most the first of them changes anything, because
        // UnknownsCollective.Render is idempotent within a frame. Comparing first turns six
        // rebuilds per frame into one, and into none on frames where the ping text did not move.
        if (!string.Equals(__instance.text.text, text, StringComparison.Ordinal))
            __instance.text.text = text;
    }
}

/// The one per-frame driver. HudManager.Update exists exactly while a map is loaded and not in the
/// menus, which is precisely the lifetime everything here needs, and it is the same driver the rest
/// of this mod family hangs its per-frame work on.
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class NightfallDriverPatch
{
    public static void Postfix()
    {
        try
        {
            if (ShipStatus.Instance == null) return;

            // ---- the view ----
            if (Input.GetKeyDown(KeyCode.F9))
            {
                NightfallState.ManualOverride = !NightfallState.ManualOverride;
                NightfallPlugin.Logger?.LogInfo(
                    $"[Nightfall] Manual override {(NightfallState.ManualOverride ? "ON" : "OFF")}.");
            }

            // Second chance at registering the host-synchronised 3D Mode option: the first is a
            // patch on MainMenuManager.Start, which a mod loaded out of order could still miss.
            NightfallOptions.TryRegister();

            NightfallState.PollMapChange();
            NightfallHandshake.Tick();
            NightfallState.Tick();

            // The ability keys. Deliberately outside the view's own lifetime: the point of the
            // labels is that the key is already known when the world goes dark, and a player who
            // only ever sees them during the hunt learns them at the worst possible moment.
            if ((NightfallPlugin.KeysAlwaysOn?.Value ?? true) || NightfallView.IsActive)
                NightfallKeys.Tick();
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] driver failed: {e}");
        }
    }
}
