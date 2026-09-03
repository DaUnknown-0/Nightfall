// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightfallOptions - the one setting that has to be the same for everybody.
 *
 * WHY THIS IS NOT A BEPINEX CONFIG ENTRY
 * --------------------------------------
 * Everything Nightfall has settled so far is a matter of taste on one machine: how far the torch
 * reaches, how fast the head turns, how many pixels the picture is. The 3D MODE is not. It decides
 * whether a player spends the round inside a corridor or looking down on the map from above, and
 * two players who answer that differently are not playing the same game - the one with the overview
 * can read a room the other has to walk into. That is the same fairness argument the lobby handshake
 * already makes, and it has the same answer: the host decides, and the value travels.
 *
 * The Other Roles already has that machinery. `CustomOption.ShareOptionSelections()` walks
 * `CustomOption.options` on the host and broadcasts every (id, selection) pair; every client writes
 * the value onto its own option with the matching id. An option registered into that list is
 * host-synchronised for free.
 *
 * ... AND THE CATCH THAT COMES WITH IT
 * ------------------------------------
 * The sync is host-driven, so an option the HOST does not have is never sent, and a client keeps
 * whatever its own config last stored (this mod family has a whole class about it: UsefulTORStuff's
 * UTSGate). Here that hole is already closed from the other side: Nightfall only arms at all when
 * every player in the lobby answered the handshake, and "every player" includes the host - so
 * whenever the mode can have any effect, the host has the option and the value is the host's.
 *
 * The one case where the two can disagree is the deliberate escape hatch: `RequireEveryone = false`
 * for solo testing. Then a client may act on its own stored value, which is exactly what that switch
 * is for and why it says so in its own description.
 *
 * THE ID
 * ------
 * 1700, out of a block 1700-1719 taken for Nightfall. CustomOption ids have to be unique across
 * every plugin in this family, because a duplicate does not clash loudly - it silently writes the
 * other mod's selection (see ..\ID-Registry.md, which records the blocks: ChanceMod 11xx,
 * UsefulTORStuff 12xx-13xx, Unknown's Collection 14xx-16xx up to 1699).
 *
 * WITHOUT THE OTHER ROLES
 * -----------------------
 * Nightfall must load and behave when TOR is absent, so the option falls back to a local config
 * entry with the same three values. Nothing is lost in practice: without TOR there is no Unknown's
 * Collection either, so there is no werewolf to trigger on, and the only remaining users of the
 * mode are the debug key and "Always".
 */

using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace Nightfall;

public static class NightfallOptions
{
    public enum Mode
    {
        /// Today's behaviour: the view begins when Unknown's Collection's werewolf transforms and
        /// ends when it reverts. The default, so an existing lobby plays exactly as it did.
        WerewolfOnly = 0,
        /// First person for the whole round, werewolf or not.
        Always = 1,
        /// Off. Neither the werewolf nor anything else brings the view up.
        Never = 2,
    }

    /// The option's id. Block 1700-1719 belongs to Nightfall - see the header.
    public const int OptionId = 1700;

    private static readonly string[] Labels = { "Werewolf Only", "Always", "Never" };

    /// The local fallback, and the value a client uses when The Other Roles is not installed.
    public static ConfigEntry<Mode> LocalMode { get; private set; }

    private static object option;              // TheOtherRoles.CustomOption instance
    private static FieldInfo fSelection;
    private static bool tried;
    /// How many times TryRegister has been called while still waiting for TOR. See the cap in
    /// TryRegister for why this exists (AUDIT-2026-09-03).
    private static int attempts;

    public static void Bind(ConfigFile config)
    {
        LocalMode = config.Bind("Nightfall", "Mode", Mode.WerewolfOnly,
            "When the first-person view applies. Werewolf Only (the default) is the original "
            + "behaviour: it starts when Unknown's Collection's werewolf transforms and stops when "
            + "it reverts. Always puts the whole round in first person. Never switches the feature "
            + "off. With The Other Roles installed this is a HOST setting (TOR Settings tab) and "
            + "this entry is only the fallback for a lobby without it.");
    }

    /// The mode in force right now. The host's value when TOR is there, the local one otherwise.
    public static Mode Current
    {
        get
        {
            try
            {
                if (option != null && fSelection != null)
                {
                    int sel = (int)fSelection.GetValue(option);
                    if (sel >= 0 && sel < Labels.Length) return (Mode)sel;
                }
            }
            catch { }
            return LocalMode?.Value ?? Mode.WerewolfOnly;
        }
    }

    // ================================================================================
    // Registration
    // ================================================================================
    /*
     * WHEN. The option has to be in TOR's list before the lobby's settings screen is built and
     * before the host shares its selections, and Nightfall very probably loads BEFORE TOR (BepInEx
     * orders by plugin GUID, and "com.tormod.nightfall" sorts ahead of "me.eisbison.theotherroles"),
     * so Load() is too early to find the type. MainMenuManager.Start runs once every plugin is up
     * and long before any lobby, which is the right moment; the per-frame driver retries as a
     * belt-and-braces second chance, because registering twice is prevented by `tried` and
     * registering late still shows up (TOR rebuilds its settings screen every time it is opened).
     */
    public static void TryRegister()
    {
        if (tried) return;

        // AUDIT-2026-09-03: without TOR, `tried` is never set below and this per-frame driver scans
        // every loaded assembly for "TheOtherRoles.CustomOption" for the rest of the session. TOR,
        // if it is going to load at all, is up well before the first HudManager.Update - so past a
        // generous number of retries it clearly is not coming, and this gives up for good.
        if (++attempts > 120) { tried = true; return; }

        Type customOption = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                customOption = asm.GetType("TheOtherRoles.CustomOption", false);
                if (customOption != null) break;
            }
            catch { }
        }
        if (customOption == null) return;      // TOR not loaded (yet). Try again next frame.

        tried = true;
        try
        {
            var enumType = customOption.GetNestedType("CustomOptionType");
            if (enumType == null) throw new MissingMemberException("CustomOptionType");

            // The string[] overload. Picked by signature rather than by parameter count, because
            // TOR has three Create overloads that differ only in the fourth parameter.
            MethodInfo create = null;
            foreach (var m in customOption.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "Create") continue;
                var p = m.GetParameters();
                if (p.Length >= 4 && p[3].ParameterType == typeof(string[])) { create = m; break; }
            }
            if (create == null) throw new MissingMethodException("CustomOption.Create(string[])");

            var ps = create.GetParameters();
            var args = new object[ps.Length];
            args[0] = OptionId;
            args[1] = Enum.Parse(enumType, "General");
            args[2] = "Nightfall: 3D Mode";
            // The string[] overload always defaults to index 0, so the order IS the default:
            // "Werewolf Only" first, which is what every existing lobby already plays.
            args[3] = Labels;
            for (int i = 4; i < ps.Length; i++)
                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue
                                                : (ps[i].ParameterType.IsValueType
                                                    ? Activator.CreateInstance(ps[i].ParameterType)
                                                    : null);
            // isHeader, so the entry gets its own line in the General tab instead of hiding under
            // whatever option happens to precede it.
            for (int i = 4; i < ps.Length; i++)
                if (ps[i].Name == "isHeader") args[i] = true;

            option = create.Invoke(null, args);
            fSelection = customOption.GetField("selection",
                BindingFlags.Public | BindingFlags.Instance);

            if (option == null || fSelection == null)
                throw new MissingMemberException("CustomOption.selection");

            MoveToEndOfOptionList(customOption);

            NightfallPlugin.Logger?.LogInfo(
                $"[Nightfall] 3D Mode registered as TOR option {OptionId} (host-synchronised).");
        }
        catch (Exception e)
        {
            option = null;
            fSelection = null;
            NightfallPlugin.Logger?.LogWarning(
                $"[Nightfall] Could not register the 3D Mode option with The Other Roles "
                + $"({e.Message}). Falling back to the local config entry, which is NOT shared "
                + "with the lobby.");
        }
    }

    /*
     * PUT THIS OPTION LAST, ON PURPOSE (AUDIT-2026-08-23, M-19).
     *
     * The host shares every option through TOR's ShareOptionSelections, which walks
     * CustomOption.options IN ORDER and packs up to 200 of them into one RPC
     * (CustomOptions.cs:153-170). The receiving end resolves each id with
     *     CustomOption.options.First(o => o.id == optionId)
     * (RPC.cs:208) - and .First() THROWS when nothing matches. That whole loop sits inside a single
     * try/catch, so on a client that does not have Nightfall, hitting option 1700 does not merely
     * skip it: it aborts the batch and silently discards every option that came AFTER it. The lobby
     * then plays with the host's settings for the first half of the list and the client's own
     * defaults for the rest, with nothing but one line in the TOR log to show for it.
     *
     * Being last makes that harmless - the only option lost to the throw is this one, which such a
     * client has no use for anyway. Registration order alone cannot guarantee it: several mods
     * register from MainMenuManager.Start and the order between assemblies is undefined. So instead
     * of hoping, the entry is moved to the end of the list explicitly, the same way UTS reorders its
     * own options. TOR's own .First() stays untouched; it is noted in the known-issues list.
     */
    private static void MoveToEndOfOptionList(Type customOption)
    {
        try
        {
            var fOptions = customOption.GetField("options", BindingFlags.Public | BindingFlags.Static);
            if (fOptions?.GetValue(null) is not System.Collections.IList list) return;
            int idx = list.IndexOf(option);
            if (idx < 0 || idx == list.Count - 1) return;   // absent, or already last
            list.RemoveAt(idx);
            list.Add(option);
        }
        catch (Exception e)
        {
            // Not fatal: the option still works, it just sits wherever it was registered, which is
            // the behaviour this mod shipped with.
            NightfallPlugin.Logger?.LogWarning(
                $"[Nightfall] Could not move the 3D Mode option to the end of TOR's option list "
                + $"({e.Message}). A client without Nightfall may lose the options that follow it "
                + "in the host's settings broadcast.");
        }
    }
}

/// The first moment at which every plugin is guaranteed to be loaded. See TryRegister.
[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
internal static class NightfallOptionRegisterPatch
{
    public static void Postfix()
    {
        try { NightfallOptions.TryRegister(); } catch { }
    }
}
