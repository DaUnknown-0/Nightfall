// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightfallKeys - a key for every ability, and the key printed on the button.
 *
 * WHY THIS EXISTS
 * ---------------
 * With the mouse captured, the cursor turns the head; it does not point at anything. A HUD button
 * is still drawn (the HUD is on its own cameras and Nightfall never touches it) and is still
 * clickable if the player holds ALT to hand the cursor back - but holding ALT also stops them
 * looking, which in the middle of a werewolf hunt is the same as not being able to use the ability
 * at all. So in this view a key is not a convenience: an ability without one is an ability the
 * player has lost.
 *
 * The Other Roles already has the whole mechanism. `CustomButton` carries a public `KeyCode?
 * hotkey` and fires the button from it every frame, and every button of TOR, Unknown's Collection
 * and Forgotten Fixes goes through that one class. Nothing here needs to be invented - three things
 * need to be finished:
 *
 *   1. SOME BUTTONS HAVE NO KEY AT ALL. TOR's Shifter, garlic and bomb-defuse buttons pass `null`;
 *      Unknown's Collection's four Copycat buttons pass `KeyCode.None`. All of them are mouse-only
 *      by design, which was fine in a top-down game.
 *   2. KEYS ARE HANDED OUT PER MOD, NOT PER PLAYER. TOR's convention (Q kill, F ability, G second,
 *      H third) rests on the assumption that a player has ONE role, and that assumption is gone:
 *      a role plus a modifier plus a cross-role counterplay button plus a Forgotten-Fixes extra can
 *      all be on screen at once. Two of them on F is a bug that only shows up in the one round
 *      where both are dealt - exactly the failure mode the option-ID registry exists to prevent
 *      (see ..\ID-Registry.md).
 *   3. THE KEY IS NOWHERE ON THE BUTTON. TOR writes the ability's NAME into the label and the
 *      cooldown into the timer, and the key into neither. Today the player learns it from a wiki.
 *
 * HOW IT IS DONE WITHOUT TOUCHING TOR
 * -----------------------------------
 * `CustomButton.buttons` is a public static list and `hotkey` is a public field, so the whole job is
 * reachable from outside. Nightfall does not REFERENCE The Other Roles (it must load without it,
 * and without Unknown's Collection), so everything here goes through plain reflection against
 * whatever assemblies happen to be loaded. TOR is an ordinary managed plugin, so this is ordinary
 * reflection - no Il2Cpp marshalling anywhere except the button's own ActionButton, which is a
 * vanilla type Nightfall already knows.
 *
 * Buttons are IDENTIFIED by the static field that holds them ("Saboteur.searchButton"), found by
 * sweeping the loaded assemblies once. That is what makes a written-down registry possible at all:
 * `CustomButton.buttons` is otherwise just a list in creation order, and creation order is a
 * property of TOR's source file, not of the game.
 *
 * WHAT IS DELIBERATELY NOT DONE
 * -----------------------------
 * Buttons that already have a working, unique key keep it. Nightfall is not a rebinding mod, and a
 * player who knows that the Sheriff shoots with Q must not have to relearn it because a first-person
 * mod is installed. Only two things change: an empty key is filled, and a key that CLASHES with
 * another button the same player is holding at the same moment is moved. `originalHotkey` is never
 * written, so TOR's own binding to the Among Us kill/ability keys keeps working untouched.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Nightfall;

public static class NightfallKeys
{
    // ================================================================================
    // The registry
    // ================================================================================
    /*
     * KEYS THAT ARE ALREADY SPOKEN FOR, across Among Us, TOR, Unknown's Collection, Forgotten
     * Fixes and Nightfall itself. Collected before anything was handed out, because the whole
     * point is that the three mods assign keys without knowing about each other.
     *
     *   W A S D + arrows  movement (Among Us)
     *   E, Space          use / confirm (Among Us; also UC's Saboteur scan minigame)
     *   Q                 kill (Among Us binding; TOR Sheriff/Jackal/Sidekick/Vampire/Thief,
     *                     UC Hunter/Pelican, UTS LoverRevenger)
     *   R                 report (Among Us); TOR Hunter-arrow / PropHunt-reveal; UC Illusionist record
     *   Tab               map (Among Us); TOR options page cycle
     *   Escape, Enter     menus; UC scan abort; UTS dialogs
     *   F                 TOR "ability" - about twenty-five role buttons and eight UC ones
     *   G                 TOR Action2 - nine buttons; UC Poltergeist hex, Maniac pass, Poisoner
     *                     antidote; UTS BomberCancel, MedicReshield
     *   H                 TOR Action3 - Hacker vitals; UC Poltergeist hand, Saboteur self-limp;
     *                     UTS TrapperLimp
     *   I                 TOR PropHunt invisibility
     *   J                 TOR use-portal; UC Poltergeist hex-mode cycle
     *   K                 TOR event kick; UC Poltergeist manifest-template cycle
     *   L                 TOR force-end (developer)
     *   C                 UC Saboteur trap; UTS Trickster avatar mixup
     *   T                 UC Poltergeist manifest
     *   U                 Bypass ("No-End") mod's "End Round" host button
     *   LeftShift         TOR PropHunt unstuck; RightShift TOR lobby rejoin
     *   KeypadPlus        TOR spectator zoom
     *   1-7 / Keypad1-7   TOR options pages (lobby only)
     *   F1 F2             TOR settings / summary
     *   F8                NightfallSurveyTool (separate plugin, not Nightfall itself)
     *   F9                Nightfall force view
     *   LeftAlt RightAlt  Nightfall free-look release
     *
     * What is left, and therefore what may be handed out. Ordered by how comfortable the key is
     * under a hand that is already on WASD.
     */
    private static readonly KeyCode[] FreePool =
    {
        KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M, KeyCode.X, KeyCode.Y, KeyCode.Z,
        KeyCode.U, KeyCode.O, KeyCode.P,
        KeyCode.Comma, KeyCode.Period, KeyCode.Semicolon, KeyCode.Quote,
        KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Slash, KeyCode.Minus, KeyCode.Equals,
    };

    /*
     * THE FIXED PART OF THE REGISTRY. Everything in here is a button that either had no key at all
     * or shares one with something the same player can hold at the same time; the assignment is
     * written down rather than taken from the pool so it is the same key in every round and can be
     * documented. Everything NOT in here keeps whatever key its own mod gave it, and only moves if
     * the frame-by-frame check finds a real clash.
     *
     * The key is the declaring type and field name of the static that holds the button.
     */
    private static readonly Dictionary<string, KeyCode> Preferred = new()
    {
        // --- The Other Roles: the three buttons that were mouse-only ---
        // The Shifter is a MODIFIER, so it rides on top of an arbitrary role's own buttons.
        { "HudManagerStartPatch.shifterShiftButton", KeyCode.V },
        // Garlic and defuse belong to EVERY living player whenever the situation is on the board,
        // so they must never collide with any role at all.
        { "HudManagerStartPatch.garlicButton",       KeyCode.B },
        { "HudManagerStartPatch.defuseButton",       KeyCode.N },

        // --- Unknown's Collection: the crew counterplay button ---
        // The Saboteur's "search" button belongs to any non-impostor, so it lands on top of that
        // player's own role button - and it is on F, which the Scout's own button also uses.
        { "Saboteur.searchButton",                   KeyCode.M },

        // --- Forgotten Fixes: the one modifier-driven button ---
        // The Revenger is granted by the LOVER modifier, so it stacks with the player's real role -
        // and it is on Q, which a Lover who happens to be Sheriff, Jackal, Thief or Vampire has too.
        { "LoverRevenger.revengerButton",            KeyCode.X },
    };

    /// Keys that must never be handed out, whatever the pool says. Kept separate from the pool so
    /// the reasoning above stays checkable.
    private static readonly HashSet<KeyCode> Reserved = new()
    {
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.E, KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Escape,
        KeyCode.Tab, KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.F1, KeyCode.F2, KeyCode.F8, KeyCode.F9,
    };

    // ================================================================================
    // Reflection state, resolved once
    // ================================================================================
    private static bool resolved, unavailable;
    private static Type customButtonType;
    private static FieldInfo fButtons, fHotkey, fActionButton, fHasButton, fShowText, fButtonText;

    /// Button instance -> the name of the static field that holds it. Rebuilt whenever the button
    /// list is replaced, which HudManager.Start does once per round.
    private static readonly Dictionary<object, string> names = new();
    /// Button instance -> the key Nightfall decided on. Persistent, so a button whose HasButton
    /// flickers does not change key underneath the player's fingers.
    private static readonly Dictionary<object, KeyCode> assigned = new();
    private static readonly Dictionary<object, TMPro.TextMeshPro> labels = new();
    private static int lastButtonCount = -1;

    public static void Reset()
    {
        names.Clear();
        assigned.Clear();
        labels.Clear();
        lastButtonCount = -1;
    }

    // ================================================================================
    // Per frame
    // ================================================================================
    public static void Tick()
    {
        if (unavailable) return;
        try
        {
            if (!resolved && !Resolve()) return;

            var list = fButtons.GetValue(null) as IList;
            if (list == null || list.Count == 0) return;

            // HudManager.Start throws the old buttons away and builds new ones. The list shrinking
            // or growing is the cheapest reliable signal that the instances have been replaced.
            if (list.Count != lastButtonCount)
            {
                lastButtonCount = list.Count;
                names.Clear();
                MapNames(list);
                Prune();
            }

            Assign(list);
            if (NightfallPlugin.ShowKeyOnButton?.Value ?? true) Label(list);
        }
        catch (Exception e)
        {
            unavailable = true;
            NightfallPlugin.Logger?.LogWarning(
                $"[Nightfall] Hotkey layer switched off after an error: {e.Message}");
        }
    }

    /// Hands out keys. Two passes on purpose: everything that already owns a key claims it first,
    /// so a clash is always resolved in favour of the button that is not being moved.
    private static void Assign(IList list)
    {
        var used = new HashSet<KeyCode>();
        var needs = new List<object>();

        // Pass 1 - the buttons the player is actually holding this frame, in list order.
        foreach (var b in list)
        {
            if (b == null || !Active(b)) continue;

            // A written-down key wins over the mod's own, because it was chosen precisely to avoid
            // the clash the mod could not see.
            if (names.TryGetValue(b, out string id) && Preferred.TryGetValue(id, out var want))
            {
                assigned[b] = want;
                Write(b, want);
                used.Add(want);
                continue;
            }

            if (assigned.TryGetValue(b, out var already))
            {
                if (used.Add(already)) { Write(b, already); continue; }
                assigned.Remove(b);              // its key was taken by someone earlier: move it
            }

            var own = Read(b);
            if (own.HasValue && own.Value != KeyCode.None && used.Add(own.Value)) continue;
            needs.Add(b);
        }

        // Pass 2 - whoever is left gets the first free key nobody is holding this frame.
        foreach (var b in needs)
        {
            KeyCode pick = KeyCode.None;
            foreach (var k in FreePool)
            {
                if (Reserved.Contains(k) || used.Contains(k)) continue;
                pick = k;
                break;
            }
            if (pick == KeyCode.None) continue;      // out of keys: leave it mouse-only, say nothing
            used.Add(pick);
            assigned[b] = pick;
            Write(b, pick);
        }
    }

    /// Draws the key in the button's top-right corner.
    ///
    /// The label is a CLONE of the button's own cooldown text, not a fresh TextMeshPro: a bare
    /// AddComponent gets no font asset in Il2Cpp and renders nothing, while the cooldown text is
    /// guaranteed to carry the font Among Us actually ships. Only the mod's own buttons are
    /// labelled - Among Us' use, kill and report buttons keep their own bindings and its own look.
    private static void Label(IList list)
    {
        foreach (var b in list)
        {
            if (b == null) continue;
            bool active = Active(b);

            if (!labels.TryGetValue(b, out var tmp) || tmp == null)
            {
                if (!active) continue;                       // build it the first time it is needed
                tmp = MakeLabel(b);
                if (tmp == null) continue;
                labels[b] = tmp;
            }

            var go = tmp.gameObject;
            if (go == null) continue;
            var key = Read(b);
            bool show = active && key.HasValue && key.Value != KeyCode.None;
            if (go.activeSelf != show) go.SetActive(show);
            if (!show) continue;

            string text = Pretty(key.Value);
            if (tmp.text != text) tmp.text = text;
        }
    }

    private static TMPro.TextMeshPro MakeLabel(object button)
    {
        try
        {
            var ab = fActionButton.GetValue(button) as ActionButton;
            if (ab == null) return null;
            var src = ab.cooldownTimerText;
            if (src == null) return null;

            var tmp = UnityEngine.Object.Instantiate(src, ab.transform);
            tmp.gameObject.name = "NightfallKey";
            tmp.transform.localPosition = new Vector3(0.29f, 0.30f, -9f);
            tmp.transform.localScale = Vector3.one;
            tmp.fontSize = 1.9f;
            tmp.fontSizeMin = 1.0f;
            tmp.fontSizeMax = 2.4f;
            tmp.enableWordWrapping = false;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.94f, 0.62f, 1f);      // warm, so it reads over any icon
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.text = "";
            return tmp;
        }
        catch { return null; }
    }

    /// KeyCode names are not what a key looks like on a keyboard.
    private static string Pretty(KeyCode k) => k switch
    {
        KeyCode.Comma => ",",
        KeyCode.Period => ".",
        KeyCode.Semicolon => ";",
        KeyCode.Quote => "'",
        KeyCode.LeftBracket => "[",
        KeyCode.RightBracket => "]",
        KeyCode.Slash => "/",
        KeyCode.Minus => "-",
        KeyCode.Equals => "=",
        KeyCode.LeftShift => "SHF",
        KeyCode.RightShift => "SHF",
        KeyCode.KeypadPlus => "+",
        KeyCode.Space => "SPC",
        KeyCode.Mouse0 => "LMB",
        KeyCode.Mouse1 => "RMB",
        _ => k.ToString().Length <= 3 ? k.ToString() : k.ToString().ToUpperInvariant()[..3],
    };

    // ================================================================================
    // Reflection plumbing
    // ================================================================================
    private static bool Resolve()
    {
        resolved = true;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType("TheOtherRoles.Objects.CustomButton", false);
                if (t == null) continue;
                customButtonType = t;
                break;
            }
            catch { }
        }
        if (customButtonType == null)
        {
            // No TOR, no custom buttons, nothing to do. Not an error: Nightfall must load alone.
            unavailable = true;
            return false;
        }

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static;
        fButtons = customButtonType.GetField("buttons", Any);
        fHotkey = customButtonType.GetField("hotkey", Any);
        fActionButton = customButtonType.GetField("actionButton", Any);
        fHasButton = customButtonType.GetField("HasButton", Any);
        fShowText = customButtonType.GetField("showButtonText", Any);
        fButtonText = customButtonType.GetField("buttonText", Any);

        if (fButtons == null || fHotkey == null || fActionButton == null || fHasButton == null)
        {
            unavailable = true;
            NightfallPlugin.Logger?.LogWarning(
                "[Nightfall] The Other Roles' CustomButton has changed shape - the hotkey layer is "
                + "off. Abilities keep their own keys; buttons stay unlabelled.");
            return false;
        }

        NightfallPlugin.Logger?.LogInfo("[Nightfall] Hotkey layer attached to CustomButton.");
        return true;
    }

    /// Sweeps every loaded assembly for statics that hold a CustomButton, so each button can be
    /// named. Collections are walked too: Unknown's Collection keeps the Copycat's four learned
    /// abilities in a dictionary rather than in four fields.
    ///
    /// ONLY THE PLUGINS ARE SWEPT. `GetTypes()` over everything loaded would walk the Il2Cpp
    /// interop assemblies as well - tens of thousands of generated types, most of a second of
    /// reflection at the start of every round, for a set of fields that cannot possibly be in
    /// there. The filter is exact rather than a name guess: an assembly is swept if it IS The
    /// Other Roles or if it REFERENCES it, which is precisely the set of assemblies that could
    /// have constructed a CustomButton.
    private static void MapNames(IList list)
    {
        const BindingFlags St = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var torName = customButtonType.Assembly.GetName().Name;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            bool relevant = asm == customButtonType.Assembly;
            if (!relevant)
            {
                try
                {
                    foreach (var r in asm.GetReferencedAssemblies())
                        if (r.Name == torName) { relevant = true; break; }
                }
                catch { }
            }
            if (!relevant) continue;

            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                FieldInfo[] fields;
                try { fields = t.GetFields(St); } catch { continue; }
                foreach (var f in fields)
                {
                    try
                    {
                        if (f.FieldType == customButtonType)
                        {
                            var v = f.GetValue(null);
                            if (v != null) names[v] = $"{t.Name}.{f.Name}";
                        }
                        else if (typeof(IEnumerable).IsAssignableFrom(f.FieldType)
                                 && f.FieldType != typeof(string))
                        {
                            if (f.GetValue(null) is not IEnumerable seq) continue;
                            int i = 0;
                            foreach (var item in seq)
                            {
                                object v = item;
                                // A dictionary yields KeyValuePair; the button is its Value.
                                var vt = v?.GetType();
                                if (vt != null && vt.IsGenericType
                                    && vt.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                                    v = vt.GetProperty("Value")?.GetValue(item);
                                if (v != null && customButtonType.IsInstanceOfType(v)
                                    && !names.ContainsKey(v))
                                    names[v] = $"{t.Name}.{f.Name}#{i}";
                                i++;
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    /// Forgets buttons that are no longer in TOR's list, so the dictionaries do not grow for the
    /// whole session (HudManager.Start rebuilds every button of every round).
    private static void Prune()
    {
        var stale = new List<object>();
        foreach (var kv in labels)
            if (kv.Value == null) stale.Add(kv.Key);
        foreach (var k in stale) labels.Remove(k);

        if (assigned.Count > 400) assigned.Clear();
    }

    private static bool Active(object button)
    {
        try
        {
            if (fHasButton.GetValue(button) is not Func<bool> has) return false;
            return has();
        }
        catch { return false; }
    }

    private static KeyCode? Read(object button)
    {
        try { return (KeyCode?)fHotkey.GetValue(button); }
        catch { return null; }
    }

    private static void Write(object button, KeyCode key)
    {
        try
        {
            var cur = (KeyCode?)fHotkey.GetValue(button);
            if (cur.HasValue && cur.Value == key) return;
            // `hotkey` only. `originalHotkey` stays untouched, so TOR's own re-binding to the Among
            // Us kill/ability keys at the start of every round keeps doing its job - it simply runs
            // before this does, every frame, and loses the last word where it has to.
            fHotkey.SetValue(button, key);
        }
        catch { }
    }
}
