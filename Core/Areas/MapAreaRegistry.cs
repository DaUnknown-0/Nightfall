// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

/// WHICH MAPS HAVE A HAND-BUILT WORLD, IN ONE PLACE.
///
/// Scene3D, NightfallState and the offline render tool all need the same two answers - "is this map
/// described" and "then build it" - and until now all three asked PolusAreas directly, which meant a
/// second described map would have to touch every one of them. This is the one seam: a caller matches
/// a map key against this registry instead of a hard-coded class name, and the registry currently
/// holds exactly one entry.
///
/// THE ENTRY IS HARD-CODED HERE RATHER THAN SELF-REGISTERED.
///
/// PolusAreas could register itself from a static constructor, but a static constructor only runs the
/// first time something touches the type - and nothing outside this file needs to touch PolusAreas
/// any more, now that Scene3D, NightfallState and the render tool go through the registry instead.
/// Relying on "PolusAreas registers itself" would mean it only happens if some unrelated code path
/// references the type first, and under IL2CPP/BepInEx's own loading order that is not a guarantee
/// worth building on. Three maps do not need a general self-registration mechanism; they need this
/// list to be right, so it is written by hand, here.
public static class MapAreaRegistry
{
    private sealed class Entry
    {
        public string KeyFragment = "";
        public Func<Area[]> Build = () => Array.Empty<Area>();

        /// Builds whatever stands outside the described rooms - Polus' dust plain and its Gorges
        /// pits, via AreaBuilder.BuildPlanet. Null for a map with nothing outside its own hull;
        /// Scene3D then simply skips that step.
        public Action<AreaBuilder, float, float, float, float> BuildExterior;
    }

    private static readonly List<Entry> entries = new()
    {
        new Entry
        {
            KeyFragment = "polus",
            Build = PolusAreas.Build,
            BuildExterior = (b, x0, y0, x1, y1) => b.BuildPlanet(x0, y0, x1, y1),
        },
        new Entry
        {
            KeyFragment = "mira",
            Build = MiraAreas.Build,
            // No BuildExterior either, for a different reason than the Skeld's: Mira HQ is a
            // station hanging in the sky, and its one open room - the Launchpad - brings its own
            // ground with it. `launchpad.js` builds the pad slab, its kerb and the lawn as floors
            // of the area itself, because they are DRAWN on the map and were measured; a
            // BuildPlanet plain underneath would be a second, invented ground competing with
            // them for the same height.
        },
        new Entry
        {
            KeyFragment = "airship",
            Build = AirshipAreas.Build,
            // The Airship brings its DECK with it - `hull.js` is the 22nd and LAST area, lays the
            // underbody 0.02 below every room and carries the flank and keel as `ribbon` fixtures,
            // which is also why it has to stay last (restage is last-wins, and anywhere the
            // underbody meets a room the room has to win). What it cannot bring is the part of the
            // ship the map never draws: the gas envelope over the deck, its fins, the nacelles and
            // the cloud sea underneath. That is AirshipExterior, and it is a BuildExterior rather
            // than a 23rd area precisely because it is not measured - see the file's head.
            BuildExterior = AirshipExterior.Build,
        },
        new Entry
        {
            KeyFragment = "fungle",
            Build = FungleAreas.Build,
            // Deliberately no BuildExterior YET, and this is the one entry where that is a
            // shortcut rather than a decision. The Fungle is an island: unlike the Skeld it has
            // real outdoor ground between its rooms, and unlike Polus that ground is not a flat
            // plain that BuildPlanet could lay down - it rises through four measured levels
            // (Jungle 0, Highland 4.5884, Ledge 6.4746, Kuppe 8.1244). Until that terrain is
            // described, the areas carry their own ground and everything between them is honestly
            // empty. See section 7 of _work/KONZEPT_AIRSHIP_FUNGLE.md.
        },
        new Entry
        {
            KeyFragment = "skeld",
            Build = SkeldAreas.Build,
            // No BuildExterior: Skeld is a sealed ship in open space, not a station standing on
            // walkable ground. A window with nothing built behind it already shows NightSky's baked
            // stars for free (Scene3D.Build calls NightSky.EnsureBuilt unconditionally, before either
            // map branch), so there is no plain to build and nothing to punch Gorges-style holes in.
        },
    };

    /// Adds a map to the registry. `keyFragment` is matched the same way Polus always has been -
    /// a case-insensitive substring of the map key. `buildExterior` may be left null for a map with
    /// no outside world to build.
    public static void Register(string keyFragment, Func<Area[]> build,
        Action<AreaBuilder, float, float, float, float> buildExterior = null)
    {
        entries.Add(new Entry
        {
            KeyFragment = keyFragment,
            Build = build,
            BuildExterior = buildExterior,
        });
    }

    private static Entry Find(string mapKey)
    {
        if (mapKey == null) return null;
        var lower = mapKey.ToLowerInvariant();
        foreach (var e in entries)
            if (lower.Contains(e.KeyFragment)) return e;
        return null;
    }

    /// True when `mapKey` has a hand-built world registered for it. Scene3D, NightfallState and the
    /// render tool all ask this instead of naming a map's area class directly.
    public static bool AppliesTo(string mapKey) => Find(mapKey) != null;

    /// The rooms for `mapKey`, or an empty array if none are registered.
    public static Area[] Build(string mapKey) => Find(mapKey)?.Build() ?? Array.Empty<Area>();

    /// Builds the registered exterior for `mapKey`, if it has one. A no-op otherwise, so a map with
    /// nothing outside its hull needs no special case at the call site.
    public static void BuildExterior(string mapKey, AreaBuilder b, float x0, float y0, float x1, float y1) =>
        Find(mapKey)?.BuildExterior?.Invoke(b, x0, y0, x1, y1);
}
