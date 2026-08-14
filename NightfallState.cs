// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightfallState - when the world flips, and who it flips for.
 *
 * READING UNKNOWN'S COLLECTION WITHOUT DEPENDING ON IT
 * ----------------------------------------------------
 * The trigger is the Werewolf's transformation, which lives in another mod. Nightfall reads it by
 * REFLECTION rather than by an assembly reference, for three reasons: the mod still loads and does
 * nothing sensible (instead of crashing) if Unknown's Collection is absent or older, it survives UC
 * being rebuilt without Nightfall being rebuilt, and it keeps the dependency one-directional - UC
 * knows nothing about Nightfall and never has to.
 *
 * The state is POLLED once per frame rather than hooked. `Werewolf.wolfForm` is a public static
 * that UC sets identically on every client, but it falls back to false through four separate paths
 * (voluntary revert, meeting or death, silver wound, round reset), and the method that does it is
 * private. One bool compare per frame catches all four; a hook would have to catch each one.
 *
 * WHO GETS IT
 * -----------
 * Every living player, the werewolf included - it gets the beast's own view instead of a torch.
 * Ghosts keep the top-down overview, because a ghost's whole remaining game is finishing tasks and
 * watching, and neither survives being put inside a corridor.
 *
 * FAIRNESS
 * --------
 * The effect is client-side and cannot be forced on anyone: a player without the mod simply keeps
 * the normal view, and with it a real advantage. So the feature gates on a handshake - every client
 * announces itself, and the view only arms when the whole lobby has answered - with a host switch
 * to turn the requirement off for testing. The host is warned in chat when someone is missing.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Hazel;
using Nightfall.Core;
using UnityEngine;

namespace Nightfall;

public static class NightfallState
{
    // ================================================================================
    // Reflected access to Unknown's Collection
    // ================================================================================
    private static bool ucProbed;
    private static FieldInfo fActive, fWolfForm, fWerewolf;
    private static Type ucWerewolf;

    public static bool UcPresent => ucWerewolf != null;

    private static void ProbeUc()
    {
        if (ucProbed) return;
        ucProbed = true;
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.Equals("UnknownsCollection", StringComparison.OrdinalIgnoreCase))
                    continue;
                ucWerewolf = asm.GetType("UnknownsCollection.Werewolf");
                break;
            }
            if (ucWerewolf == null)
            {
                NightfallPlugin.Logger?.LogWarning(
                    "[Nightfall] Unknown's Collection not found - the werewolf trigger is inert. "
                    + "The manual key still works.");
                return;
            }
            const BindingFlags f = BindingFlags.Public | BindingFlags.Static;
            fActive = ucWerewolf.GetField("active", f);
            fWolfForm = ucWerewolf.GetField("wolfForm", f);
            fWerewolf = ucWerewolf.GetField("werewolf", f);

            NightfallPlugin.Logger?.LogInfo(
                $"[Nightfall] Unknown's Collection found. Werewolf hooks: "
                + $"active={fActive != null}, wolfForm={fWolfForm != null}, werewolf={fWerewolf != null}");
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] UC probe failed: {e}");
        }
    }

    /// True while UC's werewolf is transformed. Everything about the trigger reduces to this.
    public static bool WolfFormActive()
    {
        ProbeUc();
        try
        {
            if (fActive == null || fWolfForm == null) return false;
            return (bool)fActive.GetValue(null) && (bool)fWolfForm.GetValue(null);
        }
        catch { return false; }
    }

    public static PlayerControl TheWerewolf()
    {
        ProbeUc();
        try { return fWerewolf?.GetValue(null) as PlayerControl; }
        catch { return null; }
    }

    public static bool LocalIsWerewolf()
    {
        var w = TheWerewolf();
        var me = PlayerControl.LocalPlayer;
        return w != null && me != null && w.PlayerId == me.PlayerId;
    }

    // ================================================================================
    // Arming
    // ================================================================================
    /// Manual override, toggled with the debug key. Lets the view be looked at without staging a
    /// whole werewolf endgame, which is the difference between testing this feature ten times an
    /// hour and twice a night.
    public static bool ManualOverride;

    private static bool wasOn;
    private static float transitionStart = -1f;

    /// How long the world takes to tip over. The view fades in from black rather than cutting, so
    /// the change reads as something happening to the player rather than a rendering glitch.
    private const float TransitionSeconds = 0.9f;

    /// Set the moment the round is decided and cleared when a new map is built. See the note in
    /// ShouldBeOn.
    private static bool roundOver;

    /*
     * WHICH MAPS THIS FEATURE EXISTS ON.
     *
     * Only Polus has a described world (`PolusAreas`, 17 areas measured by hand). The other four
     * still go through the old collider path, and that path was never good enough to play in: a
     * collider is not a wall, it runs into every door niche and out again, it has no window, sill
     * or lintel, and the props are photographs of a top-down drawing stood on end. It renders, and
     * that is exactly the problem - "it renders" reads to a player as "this is what the mod is",
     * and they judge Polus by Skeld.
     *
     * So an undescribed map switches the whole feature off rather than showing its worse half. The
     * check is on the BUILT MODEL rather than on ShipStatus, because that is the same question
     * Scene3D.Build asks itself when it chooses between the two paths - one answer, one place.
     */
    private static string lastLoggedMap = "";

    public static bool MapIsDescribed()
    {
        try
        {
            var model = SceneGeometry.Current;
            if (model == null) return false;
            bool ok = Core.Scene3D.UseAreas && Core.MapAreaRegistry.AppliesTo(model.MapKey);
            if (!ok && lastLoggedMap != model.MapKey)
            {
                lastLoggedMap = model.MapKey;
                NightfallPlugin.Logger?.LogInfo(
                    $"[Nightfall] '{model.MapKey}' has no described world - the first-person view "
                    + "stays off on this map. Only Polus is built; see MapAreaRegistry.AppliesTo.");
            }
            return ok;
        }
        catch { return false; }
    }

    public static bool ShouldBeOn()
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null) return false;
        if (me.Data.Disconnected) return false;
        if (me.Data.IsDead) return false;                       // ghosts keep the overview
        if (ShipStatus.Instance == null) return false;

        // Meetings, the vote and the exile screen. The head must not follow a cursor that is
        // voting, and the discussion is not a place anybody needs a corridor.
        if (MeetingHud.Instance != null || ExileController.Instance != null) return false;

        /*
         * THE ROUND IS OVER. Between the winning condition firing and the scene actually changing
         * there is a window of a second or two in which ShipStatus still exists, nobody is dead
         * yet as far as PlayerControl is concerned, and the game is drawing its end screen - so
         * every guard above still says yes and the first-person view would keep rendering
         * underneath the results. Two independent signals, because they arrive at different
         * moments: the flag from the OnGameEnd patch, and the client's own game state, which
         * leaves `Started` the instant the round is decided.
         */
        if (roundOver) return false;
        try
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.GameState != InnerNet.InnerNetClient.GameStates.Started)
                return false;
        }
        catch { }

        // Before the manual override on purpose: on a map with no described world there is nothing
        // worth forcing on, not even for a test.
        if (!MapIsDescribed()) return false;

        if (ManualOverride) return true;
        if (NightfallPlugin.Enabled != null && !NightfallPlugin.Enabled.Value) return false;

        var mode = NightfallOptions.Current;
        if (mode == NightfallOptions.Mode.Never) return false;
        if (!NightfallHandshake.EveryoneHasMod()) return false;

        /*
         * WHERE "ALWAYS" STARTS, and why it is not "at round start".
         *
         * The world model is built in PollMapChange the moment ShipStatus.Instance appears, which is
         * the map loading - well before roles are dealt - so the geometry is never the limit. The
         * limits are the two moments in which the player is not playing:
         *
         *   THE LOBBY has no ShipStatus at all (it has LobbyBehaviour), so the guard above already
         *   keeps the view off there. That is right: the lobby is a room with no collision geometry
         *   and no map, and a first-person view of it would be a first-person view of nothing.
         *
         *   THE INTRO CUTSCENE is a full-screen HUD overlay, so it would draw over the picture and
         *   the picture would be paid for anyway. It also runs while the player cannot move, which
         *   is the worst possible first impression of a view whose whole point is walking. So the
         *   view waits for it.
         *
         * Meetings and exiles are handled above, unchanged, and for the same reason as before: the
         * head must not follow a cursor that is voting.
         */
        if (mode == NightfallOptions.Mode.Always)
        {
            try { if (IntroCutscene.Instance != null) return false; } catch { }
            return true;
        }

        return WolfFormActive();
    }

    public static void Tick()
    {
        bool want = ShouldBeOn();

        if (want && !wasOn)
        {
            transitionStart = Time.time;
            NightfallControls.Reset();
            NightfallView.Activate();
        }
        else if (!want && wasOn)
        {
            NightfallView.Deactivate();
            transitionStart = -1f;
        }
        wasOn = want;

        if (!NightfallView.IsActive) return;

        NightfallControls.Tick();
        BuildView();
        NightfallView.Tick();
    }

    /// Assembles the parameters for this frame's picture.
    private static void BuildView()
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null) return;

        var v = ViewParams.Default;
        var pos = me.GetTruePosition();
        v.Position = new NfVec2(pos.x, pos.y);
        v.Heading = NightfallControls.Heading;
        v.FlashlightDir = NightfallControls.TorchDir;
        v.Time = Time.time;

        v.Fov = (NightfallPlugin.FieldOfView?.Value ?? 75f) * NfMath.Pi / 180f;
        // LocalIsWerewolf() alone is the ROLE, not the HUNT: in "Always" mode the first-person view
        // is on the whole round, including for a werewolf who has not transformed yet, and without
        // the wolfForm check that player would see the beast's claws and night vision the entire
        // game instead of the crew's own torch. The screen has to match the form, not the role.
        v.PredatorVision = LocalIsWerewolf() && WolfFormActive();
        // The beast's distance is blood, not night: with the vanilla blue-grey fog the red
        // predator tint faded into a dirty violet at range and the picture fell apart into two
        // colour worlds. Costs nothing - the fog colour is a per-frame constant.
        if (v.PredatorVision) v.FogColor = new NfColor(0.10f, 0.030f, 0.035f);

        // HOW FAR YOU CAN SEE IS THE GAME'S DECISION, NOT OURS.
        //
        // Among Us computes a light radius per player every frame, and during the wolf darkness
        // Unknown's Collection shrinks it hard. Choosing our own view distance ignored that, and
        // testing showed the consequence: the first-person view let a player see well beyond what
        // the game allowed. In a blackout that is not atmosphere, it is an advantage. So both the
        // torch and the fog are derived from the radius the game itself grants this player right
        // now, and the config setting only scales that instead of replacing it.
        float radius = 5f;
        float maxRadius = 5f;
        try
        {
            var ship = ShipStatus.Instance;
            if (ship != null && me.Data != null)
            {
                radius = ship.CalculateLightRadius(me.Data);
                maxRadius = ship.MaxLightRadius;
            }
        }
        catch { }
        radius = Mathf.Clamp(radius, 0.6f, 12f);

        float scale = Mathf.Clamp(NightfallPlugin.TorchRange?.Value ?? 13f, 4f, 30f) / 13f;
        v.FlashlightRange = radius * 1.55f * scale;
        // Past a little over twice the radius everything is fog, so the model can never be used to
        // read a room the game has already taken away.
        v.ViewDistance = Mathf.Clamp(radius * 2.6f * scale, 4f, 40f);

        // EVERY ROLE WITH BOOSTED VISION GETS A VISIBLY STRONGER LAMP, NOT JUST A LONGER ONE.
        //
        // CalculateLightRadius already comes back bigger for an Impostor, a Jackal with the vision
        // option, a Sidekick, a Spy, a Jester, a Thief or a Lighter mid-ability - TOR patches that
        // one method for every one of them, and Nightfall never has to know which role it is
        // talking to. FlashlightRange above already grows with that radius, but a longer range
        // alone reads as "a little further", not as "a stronger torch" - brightness is what the
        // falloff curve below actually renders as strength. So the same ratio that already
        // lengthens the beam also brightens it, floored at 1 (nobody's torch gets dimmer than the
        // baseline) and capped so a heavily inflated vision option cannot turn it into a floodlight.
        float visionBoost = maxRadius > 0.01f ? radius / maxRadius : 1f;
        v.FlashlightPower = Mathf.Clamp(visionBoost, 1f, 2.2f);

        // The beam gutters for the first moment after the change, as if the torch were being
        // fumbled out of a pocket, then settles.
        if (transitionStart > 0f)
        {
            float t = NfMath.Clamp01((Time.time - transitionStart) / TransitionSeconds);
            v.FlashlightPower *= t * t;
            v.Ambient *= t;
        }

        NightfallView.View = v;
    }

    // ================================================================================
    // Round lifecycle
    // ================================================================================
    /// Rebuilds the world when a new map appears.
    ///
    /// NOT a Harmony patch on ShipStatus.Begin any more. That is a small Il2Cpp method, and this
    /// project has already lost a day to the fact that the Il2Cpp linker deduplicates identical
    /// method bodies: a detour on a tiny method silently patches every other method with the same
    /// machine code, and the recorded case took out the whole process's HTTP stack. The rule from
    /// that incident is to poll state instead of detouring, which is exactly what this does, from
    /// the per-frame driver the mod already owns.
    private static int lastShipId;

    public static void PollMapChange()
    {
        try
        {
            var ship = ShipStatus.Instance;
            int id = ship != null ? ship.GetInstanceID() : 0;
            if (id == lastShipId) return;
            lastShipId = id;

            SceneGeometry.Clear();
            NightfallView.Reset();
            ManualOverride = false;
            wasOn = false;
            roundOver = false;          // a new map is a new round
            if (ship == null) return;

            if (!SceneGeometry.Build()) return;

            /*
             * A MAP THAT HAS BEEN BUILT BY HAND NEEDS NO PHOTOGRAPH OR SPRITE HARVEST AT ALL, and an
             * undescribed map needs them even less, since the view cannot come up on one in the
             * first place (see MapIsDescribed). Both used to answer "what does this surface look
             * like" by photographing the running game - tens of megabytes held beside a running copy
             * of Among Us - but that data now lives on disk instead, produced ahead of time by the
             * separate NightfallSurveyTool plugin, so there is nothing left for the live mod to
             * capture at all.
             */
            var model = SceneGeometry.Current;
            if (model == null) return;
            if (!(Core.Scene3D.UseAreas && Core.MapAreaRegistry.AppliesTo(model.MapKey)))
                NightfallPlugin.Logger?.LogInfo(
                    $"[Nightfall] '{model.MapKey}' is not a described map - the first-person view "
                    + "stays off here (see MapIsDescribed).");
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] Map change handling failed: {e}");
        }
    }

    /// The same belt-and-braces rule the rest of this mod family adopted after state leaked between
    /// lobbies: clear on join, not only on round end.
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    internal static class JoinPatch
    {
        public static void Postfix()
        {
            SceneGeometry.Clear();
            NightfallView.Reset();
            NightfallControls.Reset();
            ManualOverride = false;
            wasOn = false;
            roundOver = false;
            NightfallHandshake.Reset();
        }
    }

    /// The round is decided. Reset is not enough on its own: the driver keeps running while the end
    /// screen is up, so the view has to be told to stay down as well - hence the flag, which lives
    /// until the next map is built.
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    internal static class GameEndPatch
    {
        public static void Postfix()
        {
            roundOver = true;
            NightfallView.Reset();
            ManualOverride = false;
            wasOn = false;
        }
    }
}

/// <summary>
/// Who in this lobby can actually see the first-person view.
///
/// Modelled on Unknown's Collection's own version handshake: every client announces itself once
/// per round, the host counts the answers, and the feature only arms when nobody is missing. The
/// difference from a cosmetic mod is that here it is a fairness question - a player without the
/// mod keeps the top-down overview during the hunt, which is a real advantage.
/// </summary>
public static class NightfallHandshake
{
    /// Own channel. Free per the project's ID registry (211-229, 231-239, 241-243 unused; TOR
    /// stays below 200, Unknown's Collection owns 230, Useful TOR Stuff owns 240).
    public const byte CallId = 231;

    private static readonly HashSet<byte> respondents = new();
    private static float lastAnnounce;
    private static bool warned;

    public static void Reset()
    {
        respondents.Clear();
        lastAnnounce = 0f;
        warned = false;
    }

    public static void Announce()
    {
        try
        {
            var me = PlayerControl.LocalPlayer;
            if (me == null || AmongUsClient.Instance == null) return;
            var w = AmongUsClient.Instance.StartRpcImmediately(
                me.NetId, CallId, SendOption.Reliable, -1);
            w.Write(me.PlayerId);
            AmongUsClient.Instance.FinishRpcImmediately(w);
            respondents.Add(me.PlayerId);
        }
        catch (Exception e)
        {
            NightfallPlugin.Logger?.LogError($"[Nightfall] Announce failed: {e}");
        }
    }

    public static void Receive(byte playerId) => respondents.Add(playerId);

    public static bool EveryoneHasMod()
    {
        if (NightfallPlugin.RequireEveryone != null && !NightfallPlugin.RequireEveryone.Value) return true;
        try
        {
            int alive = 0;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                alive++;
                if (!respondents.Contains(p.PlayerId)) return false;
            }
            return alive > 0;
        }
        catch { return false; }
    }

    /// Re-announces periodically (cheap, idempotent) so a late joiner is picked up, and tells the
    /// host once who is missing.
    public static void Tick()
    {
        try
        {
            if (PlayerControl.LocalPlayer == null || ShipStatus.Instance == null) return;
            if (Time.time - lastAnnounce > 5f)
            {
                lastAnnounce = Time.time;
                Announce();
            }

            if (warned || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (NightfallPlugin.RequireEveryone == null || !NightfallPlugin.RequireEveryone.Value) return;
            if (Time.time - lastAnnounce > 1f) return;      // only just after an announce round

            var missing = new List<string>();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                if (!respondents.Contains(p.PlayerId)) missing.Add(p.Data.PlayerName);
            }
            if (missing.Count == 0 || respondents.Count <= 1) return;

            warned = true;
            NightfallPlugin.Logger?.LogWarning(
                $"[Nightfall] Not everyone has the mod - first person stays off. Missing: "
                + string.Join(", ", missing));
        }
        catch { }
    }
}

/// Receives the handshake. A separate patch class because it hooks the game's RPC funnel rather
/// than anything of ours.
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class NightfallRpcPatch
{
    public static void Postfix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        try
        {
            if (callId != NightfallHandshake.CallId) return;
            NightfallHandshake.Receive(reader.ReadByte());
        }
        catch { }
    }
}
