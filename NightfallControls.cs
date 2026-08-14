// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * NightfallControls - turning the head, and walking where you look.
 *
 * THE HARD PART OF FIRST PERSON IS NOT THE PICTURE
 * ------------------------------------------------
 * Among Us evaluates movement in world axes: W is north, always, because in a top-down game north
 * is a direction the player can see. The moment the view is aimed along a heading instead, that
 * stops being true - press W while facing south and the character walks backwards out of the
 * screen. So the input has to be rotated into view space, and that rotation is what actually makes
 * this feel like a first-person game rather than a very close top-down one.
 *
 * The rotation is applied as a POSTFIX on PlayerPhysics.FixedUpdate, to the velocity the game has
 * just computed. Deliberately not a prefix and deliberately not on the input source: this mod
 * family has already lost a day to detouring a small Il2Cpp method (Minigame.Close(bool) took down
 * process-wide HTTP), and rotating a vector after the fact needs no control of the original at all.
 * Because the rotated velocity is what the network layer then replicates, everyone else sees the
 * player move exactly where the player meant to go.
 *
 * THE MOUSE
 * ---------
 * The first attempt aimed the view at the CURSOR POSITION, so that the cursor stayed free for
 * tasks and HUD buttons. It played badly and the reason is obvious in hindsight: the cursor runs
 * into the edge of the screen. With the player drawn at the centre, the reachable angles are
 * bounded by the window, the view feels stiff, and there is barely anywhere to look except roughly
 * forwards and roughly backwards.
 *
 * So the mouse is captured and the heading integrates the mouse DELTA, exactly like a first-person
 * shooter: unbounded, continuous, and as fast or slow as the player's own sensitivity setting. The
 * cursor is released automatically whenever the game needs it (a task minigame, a meeting, the
 * chat) and on demand while ALT is held, which covers clicking a HUD button mid-hunt. Nothing the
 * game can do becomes unreachable; it just needs one key.
 */

using System;
using HarmonyLib;
using Nightfall.Core;
using UnityEngine;

namespace Nightfall;

public static class NightfallControls
{
    /// Where the player is looking. Eased towards the mouse direction rather than snapped, so the
    /// world does not jitter with every pixel of mouse movement.
    public static float Heading { get; private set; }
    /// Where the torch points. Leads the head slightly, which is what makes turning feel like
    /// swinging a lamp instead of rotating a turret.
    public static float TorchDir { get; private set; }

    private static bool initialised;

    /// True while the game needs the mouse for its own purposes. The view keeps rendering, but the
    /// head stops following the cursor, so clicking a task console does not spin the player around.
    ///
    /// THE MAP BELONGS IN HERE, and the sabotage map most of all. It is drawn by the HUD cameras,
    /// so it sits on top of the first-person picture and looks perfectly usable - but with the
    /// cursor captured, moving the mouse towards a reactor turns the player instead of pointing at
    /// it, and the sabotage cannot be called at all. That is the same failure as an ability with no
    /// key, only worse: the button is right there and visibly refuses to be clicked.
    ///
    /// Both maps, not only the sabotage one. The normal map has clickable things of its own in this
    /// mod family (Forgotten Fixes' meeting ping and its language toggle), and "the map is open"
    /// is a state in which nobody is walking anywhere anyway.
    public static bool InputSuspended =>
        MeetingHud.Instance != null || ExileController.Instance != null || Minigame.Instance != null
        || MapIsOpen || InVent
        || (HudManager.Instance != null && HudManager.Instance.Chat != null
            && HudManager.Instance.Chat.IsOpenOrOpening);

    /// Narrower than InputSuspended: WALKING keeps rotating into the (frozen) heading in every one
    /// of the states above except a meeting, an exile, a minigame, the chat, or venting - none of
    /// which the player can walk during anyway. The map is deliberately NOT in here: with the map
    /// up the player can still walk around the ship, and falling back to Among Us' world-axis
    /// movement for as long as the map is open would be exactly the disorientation this mod exists
    /// to remove.
    ///
    /// !moveable IS THE REAL GATE, NOT InVent (kept below anyway, belt and braces - see the note
    /// on InVent under this one).
    ///
    /// Logged evidence (LogOutput.log, tag VentDiag) from a reported "the character walks itself
    /// towards the vent and can be steered by turning the camera": the moment the player presses
    /// the vent key in range, `moveable` drops to false and `PlayerPhysics.FixedUpdate` starts
    /// producing a velocity whose DIRECTION visibly sweeps across many frames while its magnitude
    /// stays at walking speed - the game's own steering-to-the-vent, which is real gameplay, not
    /// leftover WASD. `inVent` only flips true once that walk-in finishes, so gating on it alone
    /// (as this file did before) left the entire steered approach unrotated-by-nothing, i.e. still
    /// rotated every frame.
    ///
    /// The steering reads its OWN previous velocity to decide the next step - it is not
    /// recomputed from scratch each frame the way raw WASD input is. Rotating its output by the
    /// (mouse-controlled) heading before handing it back therefore feeds a corrupted direction
    /// into the NEXT frame's steering, which rotates it again, compounding for as long as the
    /// approach lasts - which is exactly "steerable by turning the camera" and exactly why it only
    /// ever stopped once the player happened to steer it onto the vent by hand.
    ///
    /// InVent stays alongside it for the same reason as the other four states: once actually
    /// inside a vent Among Us will not let the player walk either, and there is no harm in saying
    /// so twice.
    public static bool MovementSuspended =>
        MeetingHud.Instance != null || ExileController.Instance != null || Minigame.Instance != null
        || InVent || !Moveable
        || (HudManager.Instance != null && HudManager.Instance.Chat != null
            && HudManager.Instance.Chat.IsOpenOrOpening);

    /// True while the local player is free to walk under their own input. False during the vent
    /// steer-in (see MovementSuspended's own note) and during any other freeze the game or a role
    /// applies via PlayerControl.moveable (a trap, a stun, a lobby gate) - in every one of those
    /// cases there is either nothing real to rotate or the game is driving the character itself,
    /// and rotating either is wrong. Defaults to true (nothing suspended) if the player reference
    /// is not there to ask.
    private static bool Moveable
    {
        get
        {
            try { return PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.moveable; }
            catch { return true; }
        }
    }

    private static bool MapIsOpen
    {
        get
        {
            try
            {
                var m = MapBehaviour.Instance;
                return m != null && (m.IsOpen || m.isActiveAndEnabled);
            }
            catch { return false; }
        }
    }

    /// True while the local player is inside a vent. The vent-to-vent buttons (Left/Center/Right)
    /// are real-world colliders positioned for the game's own top-down camera, so the cursor has to
    /// come back for them to be usable at all - see NightfallView, which hands the real picture
    /// back for the same duration so what the cursor lands on matches what is on screen.
    private static bool InVent
    {
        get
        {
            try { return PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.inVent; }
            catch { return false; }
        }
    }

    public static void Reset()
    {
        initialised = false;
        Heading = 0f;
        TorchDir = 0f;
        // Never leave the cursor captured: a locked cursor that outlives the view would trap the
        // player in the menus with no way out.
        ReleaseCursor();
    }

    /// True while the player is holding the free-look key, which hands the cursor back so HUD
    /// buttons can be clicked without leaving the view.
    public static bool CursorReleaseHeld =>
        Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

    private static bool cursorCaptured;

    /// Called every frame while the view is up.
    public static void Tick()
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null) return;

        if (!initialised)
        {
            // Start facing the way the character is already turned, so activating the view never
            // spins the world.
            Heading = me.cosmetics != null && me.cosmetics.currentBodySprite != null
                      && me.cosmetics.currentBodySprite.BodySprite != null
                      && me.cosmetics.currentBodySprite.BodySprite.flipX
                ? NfMath.Pi : 0f;
            TorchDir = Heading;
            initialised = true;
        }

        if (InputSuspended || CursorReleaseHeld)
        {
            ReleaseCursor();
            return;
        }

        CaptureCursor();

        // Mouse delta, not mouse position. Unbounded by the edges of the screen, which is the whole
        // difference between "I can look around" and "I can look forwards and backwards".
        float dx = 0f;
        try { dx = Input.GetAxisRaw("Mouse X"); } catch { }

        float sens = (NightfallPlugin.MouseSensitivity?.Value ?? 3.2f) * 0.06f;
        // Moving the mouse right must turn the view right, and screen-right is a NEGATIVE change of
        // a counter-clockwise world angle.
        Heading = NfMath.WrapAngle(Heading - dx * sens);

        // The torch trails the head very slightly. That lag is what the held flashlight leans by on
        // screen, and it is the only reason a turn reads as swinging a lamp rather than as the
        // world rotating around a fixed object.
        float torchStep = Mathf.Clamp01(Time.deltaTime * 16f);
        TorchDir = NfMath.WrapAngle(TorchDir + NfMath.WrapAngle(Heading - TorchDir) * torchStep);
    }

    /// Hides and centres the cursor. Re-applied every frame because Among Us also writes these,
    /// and whoever writes last wins.
    private static void CaptureCursor()
    {
        try
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorCaptured = true;
        }
        catch { }
    }

    public static void ReleaseCursor()
    {
        if (!cursorCaptured) return;
        try
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        catch { }
        cursorCaptured = false;
    }

    // ================================================================================
    // Walking where you look
    // ================================================================================
}

/// Rotates the movement Among Us has just computed into the direction the player is looking.
///
/// WHY THIS PATCH IS BACK
/// ----------------------
/// It was withdrawn once. Relative movement was a postfix here, Among Us lost its internet access
/// shortly afterwards, and the suspicion fell on this method: the Il2Cpp linker deduplicates
/// identical method bodies, so detouring a very small one can silently detour something else
/// entirely, and this project has one recorded case of exactly that taking down process-wide HTTP.
///
/// The suspicion was checked instead of trusted. The recorded case was `Minigame.Close(bool)`, a
/// two-line method. `PlayerPhysics.FixedUpdate` is not that: it is a substantial method, and The
/// Other Roles - which most players of this mod run anyway - has carried a postfix on this exact
/// method in shipping releases for years. The evidence says the method is safe to patch and the
/// original diagnosis was wrong.
///
/// WHY THE VELOCITY AND NOT THE INPUT
/// ----------------------------------
/// The rotated velocity is what the network layer replicates, so everyone else sees the player walk
/// where the player meant to go. Rotating the input instead would need control of Among Us' own
/// input path, which is a far bigger surface for no gain.
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
internal static class NightfallRelativeMovePatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        try
        {
            if (!NightfallView.IsActive) return;
            if (!(NightfallPlugin.RelativeMovement?.Value ?? true)) return;
            if (__instance == null || !__instance.AmOwner) return;
            // A meeting, an exile, a minigame, the chat, venting or not being `moveable` (a trap, a
            // stun, the game's own steer-into-the-vent) all mean there is either nothing real to
            // rotate or the game is driving the character itself - see MovementSuspended's own,
            // longer note for the vent case in particular, which was a real reported bug and not a
            // hypothetical. The map is excluded on purpose: it does NOT stop walking, and Heading is
            // frozen (not reset) while it is open, so rotating by it keeps the player walking
            // exactly the way they were facing when they opened it.
            if (NightfallControls.MovementSuspended) return;

            var body = __instance.body;
            if (body == null) return;
            var v = body.velocity;
            if (v.sqrMagnitude < 1e-6f) return;

            // Among Us moves in world axes: W is +y, D is +x. In here W has to be "forwards along
            // the heading" and D "to the right of it", which is the heading turned a quarter clock-
            // wise: (sin h, -cos h).
            float h = NightfallControls.Heading;
            float c = Mathf.Cos(h), s = Mathf.Sin(h);
            body.velocity = new Vector2(v.y * c + v.x * s, v.y * s - v.x * c);
        }
        catch { }
    }
}
