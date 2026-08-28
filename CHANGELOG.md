# Changelog — Nightfall

## 0.3.1.3 (Testversion)

**Mira HQ is a described world.** Its seventeen areas (Launchpad, Carpet Hall, Wood Hallway,
MedBay, Communications, Locker Room, Decontamination, Reactor, Laboratory, Sky Carpet Hall,
Storage, Cafeteria, Balcony, SkyBridge, Office, Admin and the Greenhouse) are exported from the
prototype into `Core\Areas\MiraAreas.g.cs`, and their 215 materials are ported into
`Core\Areas\AreaSurfacesMira.cs`. The first-person view now works on Polus, Mira HQ and the
Skeld; Airship and Fungle still fall back to the collider path.

Along the way, three things the prototype was drawing wrong came out and are fixed on both sides:
nine `fill()` calls in the SkyBridge's materials used a signature that does not exist, so the
parapet's MIRA band, its blue stripe, the bridge roof's light strips and the Door Log's red rings
were never painted at all; a `line()` in the Cafeteria's glass door was missing its colour, which
also silently reset the brace to hairline width; and a window in Sky Carpet Hall carried its glass
tint on the opening, where nothing reads it, instead of on the wall.

Signs are drawn for the first time: STORAGE, DECONTAMINATION, REACTOR and LABORATORY, through a
5x7 stencil alphabet, because the renderer's canvas has no text.

## 0.2.0

First public release. A true first-person view for Among Us (a raycaster, no Unity in the
rendering core), triggered by Unknown's Collection's Werewolf transformation. Polus fully
hand-built (17 areas); the Skeld is in progress.
