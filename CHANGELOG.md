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

**The material catalogue no longer runs a 32-bit Among Us out of memory.** The first build that
ever ran on Mira crashed the client: an `OutOfMemoryException` fired immediately after
`Model built in 2255 ms`, and the log stops mid-write a few seconds later. Measured cause, per map:

| Map | Materials | Pixels retained | Total allocated to build them |
|---|---|---|---|
| Polus | 32 | 10,7 MB | 47,0 MB |
| Skeld | 48 | 16,0 MB | 67,8 MB |
| Mira HQ | 215 | 72,3 MB | **304,0 MB** |

Two fixes, and both help every map:

- **One drawing buffer, reused.** A `Canvas2D` is four floats per device pixel, a megabyte at 256
  square, and it is scratch: `ToRgba` copies the result out and nothing keeps the canvas. Handing
  each material its own therefore threw a megabyte at the allocator per material. There is now one
  buffer per resolution, reset between materials.
- **Resolution follows `Unit`, not habit.** Sharpness is texels per WORLD unit: the same 256-pixel
  tile is 177 texels per unit on a 1,45-unit wall panel and 1280 on a 0,2-unit gem. The wall is
  what the playtest called soft; the gem was paying wall prices for nothing. Below `Unit` 0,72 a
  material is drawn at 128 instead of 256, which is the same density the largest surfaces have.

After: Mira retains 46,4 MB and allocates 62,7 MB to build (Polus 7,2 / 12,7; Skeld 12,7 / 17,8) -
79 % less allocation on Mira. Across the offline renderer's 67 Mira viewpoints the pictures differ
by a mean absolute error of 0,00 to 0,02 of 255, largest single-pixel difference 3, and the frame
cost is unchanged within run-to-run noise. The world build now logs what its catalogue holds, so
the next such case is a reading rather than an inference.

## 0.2.0

First public release. A true first-person view for Among Us (a raycaster, no Unity in the
rendering core), triggered by Unknown's Collection's Werewolf transformation. Polus fully
hand-built (17 areas); the Skeld is in progress.
