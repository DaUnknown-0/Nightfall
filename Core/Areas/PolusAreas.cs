// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

namespace Nightfall.Core;

/// The hand-written half of the generated area table: which maps it covers.
///
/// Kept for compatibility with anything still built against this type directly. Scene3D,
/// NightfallState and the render tool no longer call this - they go through
/// MapAreaRegistry, which is where a map's matching and build steps are wired together now.
public static partial class PolusAreas
{
    public static bool AppliesTo(string mapKey) =>
        mapKey != null && mapKey.ToLowerInvariant().Contains("polus");
}
