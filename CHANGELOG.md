# Changelog — Nightfall

## Unreleased

### Performance (Audit 2026-09-01)
- **Tastenlabels** (`NightfallKeys.cs`): `Pretty(KeyCode)` lief für jeden aktiven Button jeden
  Frame in den Default-Zweig mit bis zu drei `Enum.ToString()`-Aufrufen (Reflection plus neuer
  String). Ergebnis pro Taste jetzt gecacht.
- **Collective-HUD-Zeile** (`UnknownsCollective.cs`, in allen fünf Mods gleich): Block gecacht,
  neu nur bei geänderter Mitgliedszeile, Anzahl, Auf-/Zuklappen oder Lobby-/Runden-Wechsel.

## 0.3.1.7 (Testversion)

**Abbau am Rundenende jetzt im Prefix, und zwar zuerst.** Am 30.08. starb der Client beim
Rundenende (END-Knopf): Access Violation im .NET-Laufzeitsystem, mit System.Text.Json und dem
JIT im Stack, auf dem Main-Thread, bei 971 MB privat in einem 32-Bit-Prozess. Serialisiert hat
der Tracker-Export in SEINEM OnGameEnd-Postfix, und zwei Postfixe aus zwei Mods haben keine
definierte Reihenfolge: er lief also mit dieser Welt im Rücken, 40 996 Dreiecke, 39 MB Meshes
und 65 MB Texturen, die nach dem Rundenende niemand mehr braucht. Das letzte Rundenende, das
erfolgreich exportiert hat, lag bei 895 MB mit einer Welt von einem Drittel der Größe. Der
Abbau ist deshalb ein Prefix mit `Priority.First`: der läuft vor dem Original und damit vor
jedem Postfix, wer auch immer ihn geschrieben hat. Er gibt void zurück, überspringt also
niemals OnGameEnd selbst.

## 0.3.1.6 (Testversion)

**Hoch und runter schauen.** Der Renderer kannte nur eine Blickrichtung in der Ebene, der
Horizont lag fest in der Bildmitte. `ViewParams.Pitch` verschiebt ihn jetzt um tan(Pitch)
(`Raster3D.HorizonY`), statt die Kamera zu drehen: jede Spalte dieses Rasterisierers IST ein
Azimut, eine echte Drehung müsste das aufgeben. Geometrisch ein Schub in Bildschirm-y, also
bleiben senkrechte Kanten senkrecht. Die Maus steuert es mit, nach oben weiter als nach unten
(55 gegen 30 Grad): oben hängt die Gashülle über dem Deck, unten der eigene Fußboden. Das
Render-Werkzeug kennt dafür `--pitch <grad>`.

## 0.3.1.5 (Testversion)

**Die Airship sieht von außen aus wie ein Luftschiff.** Bisher endete das Schiff mod-seitig an
seiner Silhouette: Deck, Räume, sonst Sternenhimmel. Neu ist `Core\Areas\AirshipExterior.cs`
(als `BuildExterior` der Karte registriert): die Gashülle als Luftschiffprofil (stumpfer Bug,
lang auslaufendes Heck, 28 × 20 Felder), vier Leitwerksflossen, vier Motorgondeln, die
Halteseile vom Schanzkleid hinauf und ein Wolkenbank aus 64 Ballen 24 bis 34 Einheiten unter
dem Deck. Bewusst KEIN Bereich, sondern ein eigener Bauschritt: die Karte zeichnet das alles
nirgends, also darf es nicht wie eine Messung aussehen. Dazu drei neue Materialien
(Ballonhaut, Gondel, Wolke).

**Neu im Bausatz, aus dem Prototyp nachgezogen:**

- `ribbon`: Bahnen zwischen Polylinien. Damit kommen die **Rumpfflanke** (das helle Band
  südlich der Räume ist die umgeklappte Bordwand, kein Boden) und die neue **Bordwand des
  Kiels** in der Mod an; beide waren im Export bisher als „PORT: kein Gegenstück" weggelassen.
  Die Windung wird je Feld so gewählt, dass die Normale vom Schiff wegzeigt, denn dieser Renderer
  beleuchtet über die Normale, und eine verkehrt herum gebaute Haut wird schwarz.
- `Window` an einer **Diagonalwand** (Brüstung – Glas – Sturz). Der Bug des Cockpits ist eine
  Kette von sieben Sehnen, und die Zeichnung zeigt darüber Glas: ohne das Fensterband stand
  dort eine geschlossene Wand, mit der ersten Fassung gar keine.
- `NoHousing` an der Lampe (ein Lagerfeuer leuchtet selbst; das Gehäuse stand als schwebender
  Deckel darüber) und ein auf 0,55 × 0,45 **gedeckelter Konsolenschirm** (auf einem großen
  Pult war die gekippte Platte eine Klinge, die aus dem Möbel ragte).

**Karten neu exportiert** (Prototyp-Runden 4 und 5, an Original-Karte und Kollidern geprüft):
Cockpit verglast, Engine-Ostende als Kran + Kohle statt eines erfundenen Turbinenpaars und mit
der bis dahin fehlenden Türöffnung zur Main Hall, Records als runde Holzplattform, Medicals
NW-Fase, Löcher im Security-Balkon, Cargo-Käfige als Gitter mit Kisten dahinter, der Tresor
wieder erreichbar; Fungle: Lagerfeuer, Vorplatz ohne Void-Graben, Laborfenster, Dropship-Dach.
Zwei nachgereichte Materialien (`aAirEngineCrane`, `aAirEngineCoal`, `funLabHazard`).

**The Airship and the Fungle are described worlds.** Both are exported from the prototype the
same way Mira HQ was: the Airship's 22 areas (474 floors, 425 walls, 228 ceilings, 794 fixtures)
into `Core\Areas\AirshipAreas.g.cs` with 223 materials in `AreaSurfacesAirship.cs`, the Fungle's
16 areas (423 floors, 181 walls, 864 fixtures, plus the measured terrain as its own area) into
`FungleAreas.g.cs` with 236 materials in `AreaSurfacesFungle.cs`. Every map in the game now has
a built world; the collider fallback is no longer reached anywhere. First rounds on both: 32 895
and 33 736 triangles, built in about a second.

Two things the two maps needed from the kit: a `ladder` fixture (two rails and rungs at the
measured step, the seven ladders climb their wall bands as stair runs), and a per-material
`Detail` override in the catalogue so the 154 Airship and 140 Fungle materials in the mid band
render at 128 pixels per unit instead of 256. Without that the two catalogues came to 58,7 and
65,2 MB, both above Mira's 46,4 MB, which is the largest this 32-bit client is known to carry.

**Rides: the platform moves, ladders and the zipline are glides, holes are not falls.** The
built world answers "how high is the ground here" per rectangle, which is right for every place a
player walks and wrong for the three ways the Airship and the Fungle carry them. New
`NightfallRides` reads the game's own `MovingPlatformBehaviour`, every `Ladder` and the
`ZiplineBehaviour` once per world build and decides the ground under the local player from the
ride: on the platform's disc it is the deck the platform serves, not the pit 1,8 units down
(that drop was "I glitch under the map"); on a ladder's line it blends the two decks along the
climb; on the cable it blends the two landings. The platform is also drawn now: its disc is
built at 24 positions along the ride, all hidden, and each frame the one nearest the real
platform is shown, the same sentinel the doors use. And where the description simply has no deck
under a point - a gap between two described areas on the Fungle's highlands, metres above the
planet fallback - a drop of more than half a unit onto nothing is no longer believed; the last
real ground is held until a deck turns up. Onto a deck or a pit, however low, the drop is taken.

**A slab never roofs a pit.** The hull's underbody at -0,02 ran under every room as one closed
surface, which over the Gap Room's pit (-1,795) put it ABOVE the pit floor: from the deck the pit
read as a red floor level with the tiles, with the drums poking through it; from the pit's own
ledge, reached by ladder, it was a red ceiling a hand's breadth overhead. Both were reported. A
non-pit floor below deck 0 is now cut around every pit registered beneath it, in the kit and in
the prototype's build.js alike; room floors at 0 or above are untouched, because Polus's lava
bridges are floors over a pit on purpose.

**The meeting room's shaft is closed above the ship.** Its two cheeks ended at the room's floor
height, so from inside the room one looked over them, out across the Gap Room's roof, at sky and
clouds. They now reach the room's ceiling, a hood closes the shaft's south end above the Gap
Room's ceiling, and the shaft has a ceiling of its own.

**The Fungle's sea is a night sea.** The atlas measures the water around the island as #f3bb54,
the evening light on it seen from above; at eye height that read as a desert of sand to the
horizon. Deep blue-green now, the sun only as sparse warm glints on the crests; the sunset band
in the fog colours the distance warm by itself. The terrain was re-exported from the prototype's
new generator (848 rectangles, seven materials instead of two, all seven translated into the
catalogue), and `funTerrainSand` finally is the beach its name says, not the sea under a false
one. Lamps on a post (`post: true`) are built; the hull flank ribbon is left out of the export
until the kit can build it.

**Measured, not assumed, this time.** The first-person build now logs what it costs the process,
not just the catalogue: `Model built in 1895 ms: 22534 triangles; process +38 MB (874 MB private
now)` on Mira, and `World released; 895 MB` at round end. That number is what a 32-bit crash is
about, and the material catalogue's own figure never was it.

**`SortingLayer.layers` is not called any more.** It threw on every single world build this
machine has ever logged, nine out of nine, eight times as an OutOfMemoryException and once as an
arithmetic overflow, the last time at 874 MB in a process with 4 GB to address. That was never
memory: the struct carries a string, the Il2Cpp interop path mis-sizes the array and asks for an
impossible allocation. The culling mask has been what keeps the vanilla world off the screen
since the second playtest, so the lookup had nothing left to do, and a call that fails inside the
interop layer on the exact frame the view comes up is a suspect for the crash that followed one
of those throws on 2026-08-28, not a defence. The old note in `AreaSurfaces.cs` that read the
exception as the catalogue exhausting the address space had it backwards.

The version line in the ping tracker is built once instead of every frame, and only written to
the text component when it actually changed (TextMeshPro rebuilds its mesh on every assignment).

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
