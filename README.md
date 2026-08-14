# Nightfall

A real first-person view for Among Us.

The moment the Werewolf from **Unknown's Collection** transforms, every living player leaves the
top-down view and stands in the middle of the map: perspective walls, computed from the real
collision geometry, fellow players as figures in the room, a torch in hand. The Werewolf gets the
opposite deal: no light, red predator vision, and his own claws in frame.

Nightfall is a standalone BepInEx plugin. It modifies neither The Other Roles nor Unknown's
Collection, it only reads their state via reflection. If UC is missing, Nightfall still loads and
stays quiet.

This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is
not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein
are property of Innersloth LLC. © Innersloth LLC.

## Installation

1. [The Other Roles](https://github.com/TheOtherRolesAU/TheOtherRoles) must be installed.
   [Unknown's Collection](https://github.com/DaUnknown-0/UnknownsCollection) is optional, but it is
   what supplies the Werewolf whose transformation triggers Nightfall.
2. Download the latest `Nightfall.dll` from the
   [Releases page](https://github.com/DaUnknown-0/Nightfall/releases/latest).
3. Drop it into `<Among Us>/BepInEx/plugins/`.
4. Start the game.

The built-in updater then checks this repo's GitHub releases on the main menu on its own.

---

## Layout

| Part | Job |
|---|---|
| `Core\` | The renderer. Contains **no** Unity. |
| `Core\Areas\` | The hand-built world (Polus). Also contains no Unity. |
| `SceneGeometry.cs` | Builds the world model once per round from the live scene. |
| `Core\NightSky.cs` | The night sky, **baked once** into a panorama and only looked up afterwards. |
| `Core\HandLight.cs` | Everything that goes on top in screen coordinates: torch in hand, claws, predator vision, vignette. Shared by both renderers. |
| `NightfallView.cs` | Owns the screen: one fullscreen sprite on the world camera, below the HUD. |
| `NightfallControls.cs` | Turns the head with the mouse, converts movement into view-relative motion. |
| `NightfallState.cs` | Decides when the world flips, handshake included. |
| `NightfallOptions.cs` | The 3D mode as a host-synced TOR option (Always / Werewolf only / Never). |
| `WorldRelay.cs` | Brings back into frame everything the roles of the three mods place into the world. |
| `NightfallKeys.cs` | Gives every ability a key and writes it onto the button. |
| `..\Assets\NightfallRenderTool` | Offline renderer: **compiles the very same Core files** and draws the very same image as a PNG. |
| `..\NightfallSurveyTool\` | **Its own plugin**, not part of Nightfall itself. Surveys a loaded map (collision geometry, photo from above, individual sprites) and writes the result to `Nightfall\<map>.json` and friends: the data source for the offline renderer. Install it only to survey NEW maps, never ship it as part of the mod players get (see its own README there). |

The shared core is the heart of quality assurance: what gets checked as an image outside the game
is, line for line, what runs inside it.

## Why a raycaster and not real 3D

Among Us is a 2D game without any wall geometry at all. A raycaster needs exactly one capability
from its host: "put this byte array on the screen". That one is proven three times over in this mod
family. Real 3D would need runtime meshes, a shader that survives Il2Cpp stripping, and an asset
pipeline: three unknowns instead of none. On top of that, the raycaster brings its own depth buffer
(one distance per image column), which turns occlusion of player figures into a single comparison,
and the torch falls out as a function of angle and distance along the way.

**Measured:** 320x180, 1267 wall segments on Polus, roughly 3.8 ms per frame. All rays run in pure
C# against a grid-indexed segment model, and **not a single call** crosses the Il2Cpp boundary per
frame.

## What Among Us ships itself

Surveying Polus showed that the map already knows its own materials:

```
PolusShip(Clone)/<room>/Walls          the real room walls
PolusShip(Clone)/<room>/Sounds/Metal   TRIGGER: footsteps sound metallic here
                   /Sounds/Snow        ... Snow, Tile, Carpet, Wood, Plastic
PolusShip(Clone)/<room>                TRIGGER: the room itself
```

The footstep sound zones are a complete floor material map of the station, placed there by the
developers. Nightfall reads them straight into its floor grid: the lab floor is tiled because Among
Us says it is tiled. The room triggers answer "inside or outside", meaning ceiling or Polus night
sky.

The game hands over the height tiers for free as well, through the physics layers:

| Layer | Contents | In frame |
|---|---|---|
| 9 `Ship` | real walls | full wall height |
| 10 `Shadow` | sight blockers | full wall height |
| 11 `Objects` | tall obstacles | tall |
| 12 `ShortObjects` | tables, consoles, crates | waist-high, you see over them |

## Controls

| Key | Effect |
|---|---|
| **F9** | Force first-person view (test, bypasses the handshake, but **not** the four locks: ghost, meeting, round end, map without area data) |

F8/Ctrl+F8 (export map geometry, or additionally every object individually) no longer belong to
Nightfall itself: that is `NightfallSurveyTool` now, its own plugin (see the layout table above and
its own README). Install it only to survey a new map; for players it stays out.

The mouse turns the view, WASD walks relative to it. With a task, meeting, chat **or the map open
(the sabotage map too)** the view freezes and the cursor is released, so that a click on a console
or on a reactor does not yank the player around. On top of that, **Alt** releases the cursor at any
time for as long as the key is held.

## Settings

`BepInEx\config\com.tormod.nightfall.cfg`:

| Key | Default | Meaning |
|---|---|---|
| `Nightfall / Enabled` | `true` | first-person view at all |
| `Nightfall / Mode` | `WerewolfOnly` | **Only the fallback** when TOR is missing. With TOR the host option "Nightfall: 3D Mode" in the General tab decides (see below) |
| `Keys / ShowKeyOnButton` | `true` | print the key in the top right corner of every ability button |
| `Keys / AlwaysOn` | `true` | assign and label keys outside the view as well |
| `Nightfall / RequireEveryone` | `true` | only if everyone in the lobby has the mod (fairness) |
| `Nightfall / RelativeMovement` | `true` | W walks forward instead of north |
| `Look / RenderWidth` | `854` | internal resolution, height follows in 16:9 (see "How high the resolution may go") |
| `Look / FieldOfView` | `75` | field of view in degrees |
| `Look / TorchRange` | `13` | torch range in world units |
| `Look / TurnSpeed` | `9` | how fast the head follows the mouse |

The `Survey / *` settings (AutoRun, SaveAtlas, IncludeConsoles, DumpSprites) now live in
`BepInEx\config\com.tormod.nightfallsurveytool.cfg`, the own config file of
`NightfallSurveyTool`.

## Offline renderer

```powershell
cd Assets\NightfallRenderTool
dotnet run -c Release -- "<Among Us>\Nightfall\polusship.json" ..\..\tmp\shots --scale 3
```

Writes one image per viewpoint, plus the sprite contact sheets and a measurement of frame cost
across **all** viewpoints with a full turn at each one. On Polus the viewpoints are the `look` lists
of the area files: the spots where their author stood himself while checking. That is far better
than searching for them: hunting for "the spot with the most open space in front of it" reliably
walked out of the building in a hand-built world, because most of the open space near a Polus hut is
the planet next door.

**`--at x,y[,angle]` puts the camera exactly there** (angle in degrees, 0 = east; without an angle
the tool looks for the most open viewing direction), `--name` names the file. That is the one
extension the playtest forced: a finding arrives as a screenshot from a spot that is on no shot list
("the wall left of the beds", "the gap above the porch"), and without this camera a fix cannot be
held against the image that reported it.

`--ground x0,y0,x1,y1[,step]` samples `Scene3D.GroundAt` along a line and prints every change: the
mod's eye height is smoothed `GroundAt`, so this printout decides whether a height error sits in the
data or in the smoothing (the image alone cannot, because the tool adds `GroundAt` unsmoothed).
`--colliders` builds the world from colliders and map photo again (for comparison),
`--fullbright` floods the scene (separates "wall in the wrong place" from "wall unlit"),
`--probe` prints what the individual rays hit, `--compare` puts the view next to the map cutout with
the field of view drawn in, `--guessprops` turns the old guesswork back on.

`--propsheet` writes **every object in the scene** at original size onto a sheet of graph paper.
That is the single most useful check there is: whether an object was cut out correctly cannot be
judged in a dark corridor where it is forty pixels tall and half in shadow.

## How the map gets its look, on the maps without area data

**Since the hand-built world this only applies to Skeld, Mira, Airship and Fungle.** On Polus it is
exactly the other way round: nothing photographed, everything drawn; the section above describes
that. Both paths sit side by side in the code, and the RenderTool switches back to this one with
`--colliders`.

None of the look was drawn by hand. It all comes out of the copy of the game that is running right
now:

| In frame | Source |
|---|---|
| Floor | the map photograph, sampled per pixel at the world position. The lab tiles, the yellow grating of the ramp, the warning stripes in Storage are the real ones. |
| Walls | the drawn wall band of that same photograph, read out column by column and stood upright: the colors come from the original, only the vertical articulation (base, panel, trim) is added. |
| Ceiling | invented, because a top-down view contains no ceiling. Dark panels in a heavily desaturated derivative of the room color. |
| Props | `SpriteHarvest`: every object cut out of the scene individually and stood up as a vertical billboard of its own artwork. Polus: 148 objects from 208 sprites. |
| Painted-in furnishings | `BakedProps`: what is not an object at all but was painted into the room artwork (lockers, sinks, shelves). Polus: 33 additional objects. |

**Why billboards and not boxes.** Among Us has never drawn the back of anything. A box would need
five faces, the artwork supplies one, so four would have to be invented: which is exactly what the
guessed props did, and exactly why they looked like they came from a different game. A billboard
shows the drawing the game itself shows, from every direction.

**How the painted-in furnishings are found.** The obvious route would be the collider: everything
solid has one. Measured, it is worthless here. Polus has 50 colliders on the object layers; 33
belong to an already harvested sprite, and the remaining 17 are, without exception, the shadow
collider of a door. **The painted-in furnishings have no collider at all**: in game you walk right
through the lockers. So the image is the only witness left, but the question is asked the other way
round than the old PropFinder asked it:

1. The map says where a player can **stand**: inside the room, clear of every wall collider. Sampled
   as a grid that is a few hundred points per room which are floor by definition.
2. Their colors give the palette of the floor: four to five flat fills, because that is how Among Us
   paints, accent tiles and grout lines included.
3. A flood fill runs from those points over everything that matches the palette and stops at
   everything that is not floor.
4. The wall bands are erased beforehand, so the ring of walls does not connect the entire furnishing
   into a single blob: exactly the failure that produced the room-sized crate.
5. Whatever is left has to be **outlined**: Among Us draws every *thing* with a thick dark stroke,
   every *surface* not. Without this test, snow drifts and tile borders stood in the air as slabs;
   with it, 33 objects remain out of 92 candidates.

**The floor gets the furniture subtracted again** (`FloorRepair`): the photograph was taken from
above and contains every table, every rock, every snowman. If those also stood in the scene as
objects, each one would be in frame twice: once upright and once as its own outline flat on the
floor. The covered pixels are therefore replaced by the median of the ring around them (no blur: the
edge between purple dust and snow would otherwise fray into gray).

**Props are no longer guessed.** The former `PropFinder` searched the map image for blobs that stand
out from the floor and called them objects. On patterned floors almost everything stands out: in the
lab that produced a single room-sized crate with a screen on top, on the ramp a wall of gray crates,
and on every console with a collider stood a second one. The path is off
(`Scene3D.GuessPropsFromArtwork`, `--guessprops` in the tool). An empty room is a smaller lie than a
wrong one.

## The hand-built world (Polus)

As of this version, Nightfall guesses nothing on Polus any more. The geometry comes from
`Assets\NightfallWeb\src\areas\*.js`: seventeen areas, every number read off by hand against the
printed grid over the map photo and verified in the first-person prototype. That is the most
accurate description of Polus that exists in this project.

| Part | Job |
|---|---|
| `..\Assets\NightfallWeb\export_areas.mjs` | runs the area modules and writes `Core\Areas\PolusAreas.g.cs` |
| `Core\Areas\PolusAreas.g.cs` | **generated**: 17 areas, 172 floors, 126 walls (38 openings), 57 ceilings, 1132 pieces of furnishing |
| `Core\Areas\AreaData.cs` | the area format as C# types |
| `Core\Areas\AreaSurfaces.cs` | the material catalog (port of `surfaces.js`), 31 drawn surfaces |
| `Core\Areas\AreaKit.cs` | the construction kit (port of `kit.js` + `build.js`) and the planet with its holes |

The path of the data in one line:

```powershell
cd Assets\NightfallWeb
node export_areas.mjs          # src\areas\*.js  ->  Core\Areas\PolusAreas.g.cs
```

The export *runs the modules* instead of reading their source text: several area files compute their
contents (the rock table, the beaten paths, the octagon of Specimens). It knows every permitted key
and **throws** when an area file gains a new one: a piece of furnishing that loses its height on the
way would otherwise go unnoticed until someone stands next to it. The area files themselves stay
unchanged; the prototype is the version you can walk through, and it stays the original.

**Why this replaces the old route.** A collider is not a wall: it runs into every door niche and out
again, wraps around crates, ends in the middle of nowhere and follows a chain-link fence in
Electrical. Windows, bases, door frames and lintels are missing from it entirely, because the game
never needs them as collision. The area data is the opposite: a wall is stated as a *footprint*
(from the edge of one floor to the edge of the next), with its openings, its materials per compass
direction and the furniture in front of it.

**What is deliberately different from the prototype:**

- **No point lights.** The prototype lights its rooms with about ninety of them. Nightfall is a
  blackout with a torch in it, so a ceiling lamp is *glowing geometry*: it glows, it does not
  illuminate. That also happens to be the only version that costs nothing per frame.
- **Round things are coarser.** A barrel has ten sides instead of sixteen, a rock three domes
  instead of five, out of a 6x4 instead of a 7x5 sphere. Under torchlight the silhouette survives,
  not the segment count.
- **Glass is opaque dark blue.** The rasterizer cannot blend, every triangle writes depth. A window
  at night is very nearly exactly that anyway.
- **A glowing panel is a surface, not a solid.** There are about 250 of them (every screen, every
  ceiling tube, every light slit), and exactly one of their six sides is ever seen: a lamp from
  below, a wall screen from the front. Twelve triangles became two. Nothing turns invisible in the
  process, because the rasterizer draws both sides of a triangle.
- **Three small things are simplified:** a pipe is a box instead of a lying cylinder (at seven
  centimeters of radius the same image for a sixth of the cost), the ramp a slanted surface instead
  of a tilted box, and the communications dish a squashed sphere on a mast instead of an open cone
  with a feed arm.
- **No image plate in the floor any more.** The floor is drawn, not photographed, and with that the
  reason for `FloorRepair` disappears too: the furniture now stands in the room as solids instead of
  additionally lying flat in the photo.

### Measured

The RenderTool now measures **every viewpoint of the shot list with a full turn**, not just the
first one. The first one was the spawn: the ramp of the dropship with the whole station in front of
it, the most expensive spot on Polus and not one where anyone spends a round. That is why both the
worst *and* the median viewpoint are quoted.

**This table is the state before the playtest** and stands here because it answers the one question
that mattered back then: what the hand-built world costs against the old one. The numbers valid
today are under "How high the resolution may go".

| | Triangles | Memory | Build time | 320x180 | 640x360 |
|---|---|---|---|---|---|
| old (`--colliders`, 18 viewpoints) | 3,428 | 24.6 MB | 291 ms | 2.88 / 1.76 ms | 8.96 / 5.93 ms |
| **hand-built world** (89 viewpoints) | **30,308** | **7.6 MB** | **187 ms** | **7.17 / 3.80 ms** | **11.25 / 8.44 ms** |

So ten times the geometry costs a good factor of two in compute time on average, and **a third of
the memory**: the photograph of the map (2976x2330 on Polus, roughly 28 MB) and the sprite harvest
are never created at all for a hand-built map, and `FloorRepair` used to make a second copy of it on
top. The viewpoints are not the same either: the 89 come from the `look` lists of the area files and
sit in the middle of the rooms, the old 18 from a search for open space.

Four things made ten times the geometry affordable:

1. **View cone prefilter per cell** (`Scene3D.Query`). A square around the player is four times the
   area of the cone; three quarters of everything sat behind the camera.
2. **The same test once more per triangle**, with the radius as a safety margin: fourteen arithmetic
   steps save a full transform and sixteen band comparisons.
3. **Cells from near to far.** Standing in front of a building, its interior gets drawn and is then
   covered by the roof: a pixel that loses the depth test has already paid for texture, light cone
   and fog by that point. Near first means the roof is there first. (8.99 to 7.06 ms.)
4. **Spans instead of a bounding box in the rasterizer** (`Raster3D.DrawClipped`). The barycentric
   weights are linear in x, so the pixel run of a row that is actually hit can be *computed* instead
   of testing every pixel of the enclosing rectangle and throwing most of them away. That waste is
   not a constant: a head-on wall fills half its rectangle, a floor slab seen edge-on is a sliver in
   a screen-wide rectangle, and floor is most of a first-person image. (640x360: 16.2 to 12.2 ms;
   affects both worlds.)

## The first playtest, and what the image is afterwards

Up to this point Nightfall had only ever been checked in the RenderTool. The first pass in the
running game produced 27 screenshots, and they fall into two kinds: **the world is wrong** (a hole,
a missing ceiling, a box in the wrong place) and **the image is bad** (stripes, moiré, hard edges, a
light bar instead of a lamp). The second kind was the larger one, and it largely had a single cause.

### 1. Texture filtering: why everything was striped

Bilinear filtering answers the question "which texel lies under this pixel". It does **not** answer
the question "how many texels lie under this pixel", and that is exactly the problem: a corrugated
wall seen lengthwise, a floor slab running to the horizon or the layered rock of the canyon put
twenty texels under one pixel. Picking two of them is a lottery that comes out differently for the
neighboring pixel: the result is stripes, and they crawl as you walk. Never in the middle of a wall
you are standing in front of; always where a surface runs away from you.

The answer is a **mip pyramid** (`Surface3D`): every level is the previous one averaged two by two,
so there is always a level whose texels are pixel-sized. The rasterizer picks it from the screen
derivative of the UVs, and that is nearly free here: the barycentric weights are linear in x *and*
in y, so `du/dx = (duz/dx − u · diz/dx) · z` with a constant numerator: four multiplications per
pixel, no additional division. The logarithm for the level comes out of the exponent of the floating
point number (`NfMath.FastLog2`), not out of the library.

Two different downscales, because alpha means two things here: on a cut-out object it is opacity, so
the average has to be **premultiplied** (otherwise every table gets a dark fringe); everywhere else
it is the tint mask and a plain average is correct.

**Measured:** 640x360 from 10.3 to 12.4 ms (worst viewpoint), memory from 7.6 to 8.4 MB. That fixes
the findings on images 7, 12, 13 and 18 as well as the striped embankment in image 1.

### 2. Texture resolution: 128 stays the yardstick, 256 gets drawn

Every surface in the catalog is described inside a fixed 128 square, and every number in it is tuned
against the map artwork: a grout line is two units wide, a rib repeats every eleven. Simply enlarging
the canvas would halve all of those numbers relatively: thinner grout, finer grain, a map that looks
different overall.

`Canvas2D` therefore now has **two sizes**: `W`/`H` stay the coordinate system of the design,
`Detail` says how many real pixels one design unit becomes. All shapes run through a single place
anyway (`FillSdf`), so this is one conversion in one spot; the antialiasing follows along, because a
device pixel is now `1/Detail` design units wide. The catalog is unchanged, the texture is merely
drawn more precisely.

`Detail = 2`, so 256 textures. Costs almost nothing at runtime (the pyramid makes sure a distant
wall still reads a pixel-sized level), but it costs memory: roughly 10 MB instead of 2.5.

### 3. How high the resolution may go, with numbers

Measured across **all 89 viewpoints with a full turn at each**, after all the changes of this pass
(mips, 256 textures, round light cone, torch in hand, vignette, baked sky):

| | worst viewpoint | median | pixels |
|---|---|---|---|
| 480x270 | 9.3 ms | 6.0 ms | 0.13 M |
| **640x360** (before) | 12.9 ms | 9.3 ms | 0.23 M |
| **854x480** (now) | **19.3 ms** | **14.8 ms** | 0.41 M |
| 960x540 | 23.2 ms | 18.2 ms | 0.52 M |

For comparison, the same measurement **before** this pass: 640x360 = 10.3 / 8.0 ms. So everything
above (mip pyramid, doubled texture resolution, round cone, hand torch, vignette, baked sky) costs
about a quarter more compute time at the same resolution taken together. Model: 30,454 triangles,
19.2 MB (previously 30,308 and 7.6 MB; the growth is the mip pyramids and the 256 textures).

The choice is **854x480**, and the limit it hangs on is the 16.7 ms of a 60 Hz frame: 854 sits below
it with 14.8 median, 960 above it with 18.2. That is the whole difference: one step makes sixty
frames, the next one does not. The worst viewpoint is above the line for both, but that is the ramp
of the dropship with the whole station in front of it: the spawn, where nobody spends a round, and
the most expensive spot on the map.

If you do not like the number, change one line of config: 640x360 is the safe step (9.3 median, so
there is still headroom for the game on top), 960x540 the one to look at. The range now goes up to
1280.

And resolution is not the only thing that means image quality: of the four points of this pass,
filtering costs 18 %, texture resolution nothing, and the light cone less than what it replaces.
Taken together they are more visible than the jump from 640 to 854 on its own.

What paid for that: `PrepareExtents` now runs across all cores (it is the part of a frame whose cost
does *not* fall with resolution: a straight line through the pixel counts left about 4.4 ms of
baseline), and the vignette computes in r² instead of a square root per pixel, likewise across all
cores.

### 4. The torch was a curtain, not a cone

On almost every one of the 27 images there is a **hard vertical bar of light** running from the
ceiling across the wall down to the floor, equally wide everywhere. The reason is in one line of
code: the cone was computed from the **azimuth alone**, meaning the compass direction from the eye
to the point, and an angle without elevation does not describe a cone but an infinite vertical
wedge.

The full angle needs the height difference with it. In radians that would be one `acos` per pixel,
and it does not need to be: the **cosine** of the angle *is* the dot product of the ray axis with
the unit vector to the point, and the thresholds of the cone can be kept as cosines instead of
angles. That makes the round cone **cheaper than the wedge it replaces**: the old one paid an
`atan2` per pixel, the new one two multiplications and an addition.

Three things came along with it:

- **The cone has a corona.** The falloff reached out to 2.3 times the core angle; that was tuned
  against the wedge, which lit a floor-length stripe anyway. A round cone hits a fraction of that,
  so the falloff now reaches out to 3.2 times. That is what makes a room readable instead of a
  keyhole.
- **Two colors instead of one number.** A light bulb is warm and the Polus night is not. The light
  value is now split into "what the torch contributes" and "what the rest contributes" and each gets
  its own color: a warm patch with cold blue-gray around it. Costs three multiplications and is the
  most recognizable thing about a lamp in the dark.
- **The near field no longer burns out flat.** The old curve multiplied by 2.3 and fell off as
  1/(1+d²/(R²/4)): within two meters it was therefore above the ceiling of 1.45 everywhere at once,
  and a round cone turns that into a **flat white disc** without any detail. Half the amplitude at
  twice the curve radius keeps the brightness almost unchanged between six and thirteen meters (0.86
  against 0.92 at eight) and leaves the near field below the ceiling.

### 5. The torch was not there at all, either

The raycaster drew a torch in the bottom right corner and, for the beast, its two front paws; the
triangle renderer that replaced it drew neither: the entire first playtest was played empty-handed,
and a first-person view with nothing in frame reads as a camera, not as a person. That is not
decoration: **the torch tilts toward where the beam points**, and that tilt is the only thing that
tells the player the mouse is steering the light and not the head. Vignette and predator tint were
missing just the same.

All four now live in `Core\HandLight.cs` and belong to both renderers: a hand torch drawn twice is
one that drifts apart.

### 6. The starry sky

A star was a **grid cell**: a rectangle with hard edges whose size depended on how many pixels that
cell happened to cover. Different sizes, angular, with aliasing on every edge, and on top of that
each one blinked on its own with a sine over time, which turns a field of hard squares into noise
rather than a sky.

A formula has to be cheap, because it runs across up to half a screen; that exact budget is what
limited the old sky to a hash. **Baking cancels the budget:** `NightSky` builds a 2048x320 panorama
once per session, and a pixel costs one bilinear lookup after that: less than the hash cost. In
exchange the sky can afford:

- **round, soft stars** at subtexel positions, with a steep brightness law (m⁴), so nearly all of
  them faint and a handful bright, in white, blue-white and amber;
- the **Milky Way** as a sine wave across the panorama (a great circle seen from the inside is
  exactly that), with dust lanes and a higher star density inside it;
- an **aurora** in curtains close above the horizon;
- **extinction at the horizon**: real skies lose their stars in the last few degrees, and that is
  the cheapest thing that turns a star field into a sky: it gives the horizon a place.

It **does not move**, and that is the point. The stars stand still in the world, the head pans past
them, nothing pulses. A sky that does something is a sky you look at instead of listening for
footsteps. It is baked in `Scene3D.Build`, meaning where the round pays once anyway, and not in the
first frame after the transformation.

The column is looked up via the **real** azimuth in the process, not via a linear ramp across the
field of view: the linear version is off by several degrees at the edge of a 107° image, and you see
that in the stars drifting against the buildings as you turn.

### 7. The world itself, finding by finding

Everything here lives in the area files under `..\Assets\NightfallWeb\src\areas\`, was regenerated
afterwards with `node export_areas.mjs` and reproduced in the RenderTool at the exact spot where the
screenshot was taken (that is what the tool's `--at x,y,angle` is for now: reproducing a finding
needs a camera, not a room, and the shot list only knows rooms).

| Finding | Cause | What was changed |
|---|---|---|
| **Dropship floor full of stripes and dots** (image 7) | Not just filtering: the **belly of the hull** (`dropship.js`) reached down the full `DECK - GROUND`, so its top face sat **exactly at the height of the cargo floor** above it. Two quads at the same height fight over every pixel. | Belly 0.04 lower. Plus a sweep across the **entire** generated world that checks every furnishing top edge against every floor: no second case. |
| **Corridor decon to specimens with no ceiling** (image 14) | The prototype reads the map correctly (snow on the floor, walls as an elevation in its own floor plan), it is just that this tube is 1.6 wide between 2.2 tall walls. The sky is a slit narrower than the walls are tall. | Ceiling over it. The **lower** tube stays open: it is 1.7 tall, considerably wider, was walked in the same pass and drew no complaint: the difference is the ratio, not the principle. |
| **Storage: boxes missing** (image 8) | Two crates north of the west door were missing from `storage.js`, because they sit outside the porch rectangle: of all things the ones you have right in front of you when looking through the open double door. | Both remeasured from the atlas and added. |
| **Weapons oriented wrong** (image 21) | The map draws the fire console as an **L**: narrow leg against the west wall, wide counter at its **north end**, and screen and joystick on top of it. It was built the other way round, which put monitor and joystick a meter too far south. | Both legs swapped, fixtures moved to the north end. |
| **Weapons exit too short** (image 22, hitbox) | The porch ended at −20.95. That is the **inner** edge of the door prop `Weapons/Walls/BottomDoor`; its outer edge sits at −20.24. Comms' identically built porch gets it right. 0.71 units too short, so you were walking where there was no drawn door any more. | Floor, both side walls, end wall, ceiling and door leaf moved to −20.24. |
| **Red bars floating, red stripe in the sky** (images 2, 9) | Two errors on the same staircase (`outside.js`, stabilizer). First, the handrail sat 0.05 **outside** the tread: there is no floor built there, and at the top step a good meter of air gapes. Second, every step is 0.131 taller than the previous one, but the rail run is only 0.075 tall: between each pair a 0.056 gap remained. That was not a railing but a row of floating T-pieces. | Run moved inward, outer edge flush with the tread edge; every piece reaches one step height downward and overlaps the one below. |
| **White surface with nothing attached in the dropship** (image 6) | The geometry is correct (recomputed: bottom edge exactly at deck height, top edge at the ceiling joint). The **material** was wrong: the file says "the corrugated cladding", what was built was `ceilingPanel`, almost white and without any structure beyond two grooves. Burned out by the torch cone, that is a blank slab. | `corrugated`, the corrugated sheet from the catalog; its gray-green sits between the two hull colors of the ship. |
| **Office left exit wrong and too thin** (image 20) | **Not solved.** Three candidates with a red base and a passage were measured against atlas *and* collider (corridor passage to the tiled room, Office's southwest door, Admin's west door): all three agree with at least one of the two sources. Without the game coordinates of the screenshot there is no telling which spot is meant. | Nothing. Guessing would be worse here than leaving it open. |
| **"What is that purple stuff doing there?"** (image 27, O2 = LifeSupport) | The two bright "plates" in the machine bay, built as 0.30 to 0.34 tall blocks. The atlas has **nothing** underneath them there: the floor is a continuous `#6b6680`, the only real structure being the dashed walkway bands. The prototype had noted exactly this suspicion ever since it was built; in game it was the most conspicuous object in the room. | Blocks out, the two measured bands laid in flush with the floor. |
| **Hole in the wall by the lab** (image 12) | **Not a bug.** It is the deliberately roofless balcony bay with the telescope, seen from inside over a partition wall, cross-checked with `--fullbright`: there really is sky there, not missing geometry. That it reads as wall damage comes down to the bay being unlit. | A faint lamp on the terrace, so that railing and telescope make the bay readable as a balcony. The map does not draw one: this is an addition and is written down as such in the file. |
| **The rock floats over the edge** (image 1) | **Not solved.** The stripes on the embankment were filtering and are gone; the floating is not. The open item "21 rocks stand on their full sprite box" was recomputed, and the result is a counter-finding: the two already trimmed by hand kept 72 % and 33 % of their northward extent respectively. **There is no common ratio**, and therefore no general rule either: the procedure stands (measure the edge in the atlas, cut `y1` back, set the height explicitly). The most likely candidate is `outsideprops.js` line 105, `[32.16, -19.17, 33.21, -18.52]`, but it does not overlap any hole in the plane, so the floating could also be perspective. | Nothing. Without the measurement these would be guessed numbers on a rock that may not even be the one meant. |
| **Green field in Admin** (image 19, not reported) | Checked: that is the green map plate of the Admin table seen from very close, not Nightfall. | Nothing. |

### 8. The hitbox findings are a category of their own

Two findings ("you can walk through the right-hand beds", "Weapons exit too short") concern not the
image but the collision. That comes with a statement that precedes any measurement:

**Nightfall does not touch the physics.** The only intervention in movement is a postfix on
`PlayerPhysics.FixedUpdate` that **rotates** the velocity vector (`NightfallControls`), so that W
walks in the viewing direction. Collision, colliders and doors stay entirely with the game. So the
mod cannot change where you can walk through: it can only draw an image that matches it or does not.

Two different cases follow from that:

- **You walk through something that is drawn.** That is normal in Among Us and not a malfunction of
  the mod. The prototype measured it while the world was being built: Polus has 50 colliders on the
  object layers, 33 belong to a harvested sprite, the remaining 17 are without exception the shadow
  collider of a door: **the painted-in furnishings have no collider at all**. In the original game
  you walk through the lockers, the beds and the workbenches, you just do not see it from above. In
  first person you see it immediately. Changing that would mean adding colliders to the game, that
  is, changing the game: a decision, not a bug fix, and it belongs to the user.
- **You bump into something that is not drawn, or the other way round.** That is a real bug, but it
  sits in the area data: the drawn opening does not line up with the gap in the Ship collider. That
  one is lookup-able (`world.json`), so the finding is measurable and the correction belongs in the
  area file.

## The second playtest (images 28 to 34)

Seven screenshots, five findings. Each one is reproduced in the RenderTool at the reported spot; the
evidence lives under `..\tmp\nf28`, `..\tmp\nfbridge` and `..\tmp\nfcrowd`.

### 1. Office's left exit has been found: it was the west door of the tiled room, 0.79 too far north

The open item from the first pass needed a screenshot with a known position; image 28 (looking
straight at the door) plus image 29 (minimap: purple crewmate between Weapons and Admin) supply it:
the player stands at roughly (15.0 / −22.0) and faces east. `--at 15.0,-22.0,0` shows the west wall
of the small tiled room there with the door **an eighth of a screen too far left**: none of the
three candidates from back then, but the door in `corridor.js`, whose opening had been measured from
the **parked door leaf** instead of from the hole.

The first version took the prop rectangle `Admin/LeftDoor` (y −21.63 to −20.88) as the opening. But
the survey ran with `isOpen: true`, so that rectangle is the **leaf parked in the wall** north of
the hole (the door transform sits at −21.723, hence south of the entire rectangle: that is the
giveaway). Where the hole really is, the Ship collider states literally, because a collider is
"where a crewmate stops": `Admin/Walls` runs along the west flank down to y = −21.519, crosses the
wall, and a second polygon comes back up from −23.791 to −22.486. In between there is no wall:
**opening y −22.49 to −21.52** (0.97 wide instead of 0.75). The atlas confirms both edges to the
pixel. Changed in `corridor.js`: opening span, the grating platform in front of it moved along, and
the red base of the west facade split into two pieces: run through, it would have bricked up the
lower 0.41 of the door opening in dark red.

### 2. The lava bridge "glitches": three end-cap boxes fought the planet plane over the same pixels

The prototype finding that was never closed (missing end faces of the channel) is **not** the cause:
all end faces exist and arrive in the export. The cause is the rule that `outside.js` itself states
five lines further up: "two tops at exactly the same height fight for the pixels". Of the seven end
boxes of the canyon, exactly three sit **outside** their hole (x 37.94 east end of the lake, x 30.90
west tip, x 40.17 west end of the channel), the planet plane runs over them there, and with
`h: 1.12` starting at `y0: −1.40` their top edge lands at **−0.28, exactly planet height**. 0.75 u²
of that lies along the west flank of the walkway, 0.14 on the east flank: precisely the two strips
you look down at from the bridge. The depth test rejects ties (`z >= depth`), so drawing order
decides per pixel and changes its mind with every step: that is the flickering. All three set to
`h: 1.10`, following the pattern of the box that already got it right.

A coplanarity sweep across all 927 horizontal top faces of the world found four more cases of the
same class, all fixed: the two grating mats at the foot of the stabilizer stairs lay at planet
height across their full area (now +0.02), the topmost step exactly at platform height and reaching
under the slab (now −0.005), and the shaft floor under Electrical's outdoor vent at −0.28 (now 0.40
instead of 0.38 tall). The remaining pairs from the sweep are same-colored flush attachments (window
sills, bases) and invisible.

### 3. Vanilla sprites in frame (images 33/34): the cover-up hung on a lookup that throws in Il2Cpp

Players, telescope, rock and doors sat **above** the first-person view. The fullscreen sprite was
supposed to sit on the topmost sorting layer, but the line "Screen on sorting layer" is missing from
the log, so `SortingLayer.layers` threw in Il2Cpp, the `catch` swallowed it, and the sprite stayed
on "Default": everything on a later layer won, no matter how large the order. The new cover-up no
longer fights against layers, it takes the work away from the camera instead:
**`HideWorld()` narrows the culling mask of the world camera down to layer 1** (only the Nightfall
screen), set fresh every frame, restored on deactivation. The HUD is untouched, because it is drawn
by its own cameras (depth 99/100): the reason a sprite on the world camera was the right vehicle
from the start. The layer lookup stays as a second line of defense and now speaks up loudly when it
fails.

### 4. Players are only visible when the torch is on them

The rule behind images 33/34, and it is a question of its own alongside brightness: walls may be
dark-but-readable, a **person** outside the cone has to be gone, otherwise the blackout is a radar.
`Raster3D.SeenFactor` judges billboards against a **separate, narrow cone** (core plus half, 22°
full, zero at 33°, explicitly not the 70° corona: measured against that, every crewmate in frame was
"in the light" and the first attempt changed nothing). Plus a range limit (full out to 0.7 of twice
the torch range, then off) and an arm's-length exception: never invisible below one meter, but at
most half: whoever walks through someone sees a figure, not an identity. The edges are soft (alpha
blend instead of a threshold), a billboard only writes depth from half opacity upwards, otherwise a
semi-transparent figure punches holes into the wall behind it. Measurement is taken at chest height,
not at the feet. `PredatorVision` is untouched: the beast has no torch, its night vision sees
farther: that is the asymmetry that makes the transformation playable. New test shot `beamrule` in
the RenderTool: five identical crewmates at the same distance at 0°, 18°, 30°, 45°, 70° off-axis:
two clear, one half, two gone.

### 5. The drawn crewmate figure is new, following Among Us' own drawing rules

The old figure was a capsule with a gradient. Four things were wrong, all four against the way Among
Us paints itself: the proportions (a crewmate is squat, two thirds as wide as it is tall), the
gradient (the game fills with **exactly two** flat colors, player color and `ShadowColors` shadow,
with a hard, curved boundary at three quarters height), the visor (a wide rhombus with a dark
outline, glass and one hard highlight, not a circle) and the outlines (every part outlined, the
backpack against the body too; the legs deliberately **not**: the game draws body and legs as one
closed silhouette with a notch between the feet). A texel in the fill is now the palette entry
**exactly** (colorMask 1), only a faint shoulder sheen thins it at the top. Two traps found along
the way: the part assignment must not be a nearest-distance comparison (a normalized distance is
relative to the part's own radius, which is why the backpack always lost against the body when seen
from behind and vanished in the one view where it is the whole picture: now priority pack > legs >
body), and the outline width needs a band **in the field of the respective part**, otherwise the
body has three texels of stroke and the leg half of one.

### 6. Image 30, "character does not go up": the camera forgot its height every frame

The user refined the finding later: "I still do not walk up stairs, camera stays at the same
height." Suspicion fell on the data first, and the data is innocent, verified in every layer: the
stabilizer stairs stand as ten separate floors each with rising deck height in `outside.js` (there
is no `walkable` flag in the prototype at all, stairs **are** floors), the export delivers both
staircases completely into `PolusAreas.g.cs` (deck −0.26 to 0.895), and `GroundAt` reads exactly
that list. The RenderTool proves it: rendered halfway up the stairs, the camera visibly stands above
the planet, the platform at knee height (`..\tmp\nfstairs`).

The bug sat in the game's frame driver, meaning exactly in the piece the shared core does **not**
cover. `BuildView` builds `View` fresh every frame from `ViewParams.Default` (EyeHeight 0.62), and
`NightfallView.Tick` smoothed the eye height **against that just-reset field**: the camera rose by
about a sixth of the difference per frame and started over at 0.62 in the next frame. On the stairs,
about 0.10 of the 0.55 of climb arrived permanently, no matter how long you stood at the top: "the
camera stays at the same height" is the exact description of the code. The smoothing state now lives
in a static field (`eyeSmooth`) that survives the frame and goes back to NaN on `Activate`. Why the
tool never showed it: it adds `GroundAt` straight onto the eye height, without smoothing; the bug
lived in the ~200 lines between core and game.

### 7. Hats, skins, visors, pets: the photograph now delivers what it promises

Checking against the interop assembly (CosmeticsLayer, HatParent, SkinLayer, VisorLayer,
PetBehaviour) turned up three bugs in `AvatarCapture`:

- **The name text was photographed along with everything else.** `nameText` and `colorBlindText`
  (TextMeshPro MeshRenderers) hang under `cosmetics` as well; the all-renderers filter took them
  with it, and every crewmate wore its name tag baked into its chest. Now only **SpriteRenderer**s
  are isolated: every cosmetic part of the game is one, text by construction is not.
- **The fixed 1.5 frame decapitated tall hats** and turned the figure into a postage stamp in empty
  space. The frame is now built from the union of the sprite bounds (4 % margin, clamped to 0.25 to
  4.0): top hat and flower pot fit, `WorldHeight` is correct automatically.
- **The pet is not a body part.** `PetBehaviour` trails its owner with its own physics up to a meter
  behind; baked into the player photo it would either be gone or glued to the hip. It is now
  photographed separately (`ForPet`) and drawn as **its own billboard at its real position**: in the
  dark a pet is a second movement you can mistake for a player, and that is exactly what it is
  supposed to be.
- Plus: the freshness key reads `CurrentOutfit` instead of `DefaultOutfit` (otherwise a Morphling
  kept its old photo until the timer ran out), and a `MissingMethodException` while photographing is
  logged instead of swallowed: the symptom would otherwise be "everyone is a drawn crewmate again"
  without a single line in the log.

### Measured after this pass, and why the resolution stays where it is

The same measurement as always, all viewpoints with a full turn, after all the changes of this pass
(visibility cone, new figure, larger plane, new index bounds):

| | worst viewpoint | median |
|---|---|---|
| **854x480** (current) | 21.8 ms (upper tube) | **14.3 ms** |
| 960x540 | 24.3 ms | 17.6 ms |

The median is slightly **better** than before the pass (14.8) despite the larger world, because the
correct index bounds no longer funnel the outer ring through wrong cells. The worst viewpoint got
more expensive (19.3 to 21.8) and is now the upper tube: from there you now see real ground all the
way to the fog where there used to be empty space, and that is the honest price of the edge. **More
resolution is not on the table:** 960x540 sits at 17.6 median, above the 16.7 ms budget of a 60 Hz
frame, exactly the limit the choice hung on last time as well. The 2.4 ms of median headroom at 854
are not a reserve but the cushion for the game, the upload and the expensive viewpoints: the
antialiasing from the open list stays the next sensible step, because it gets by without more
pixels.

### 8. The outside: the world stopped at the edge, but not because something was missing

The suspicion was that rocks and trench from `outsideprops.js`/`outside.js` do not reach the mod. A
full inventory (prototype run through Node and counted against `PolusAreas.g.cs`) refutes that: all
45 floors, 179 + 124 pieces of furnishing, 24 rocks, 10 snowmen, all snow fields, terminals and
beaten paths arrive 1:1; not one `kind` and not one material falls through. What actually stopped
was the **planet plane**: it was built at collider hull plus 14, only about eight units beyond the
drawn map edge, while the fog does not start until 18.9 (0.45 of the view distance 42). From the
edge of every outdoor area you therefore looked over a sharp ground edge onto stars **below** the
horizon. Now plus 48: the edge lies behind the full fog from every walkable point. The old worry
"every triangle costs a bucket pass" no longer applies since the distance cap and the cell frustum
in `Query`: cells beyond the view distance are never visited in the first place; the larger plane
costs a good 900 triangles of memory and nothing per frame. Fittingly, `BuildIndex` now measures its
bounds against the **triangles instead of the colliders**: before that the whole outer ring of the
plane was clamped into the border cells, whose centers then lied to the frustum and distance test.

**The floating rock from image 34 is settled, and it is not one of the 24.** The image shows smooth,
drawn outlines, painted patches of snow and a black sprite shadow: that is Among Us' own `rock1`
artwork, in the same image as the purple vanilla player complete with hat, so it is the same finding
as image 33: a vanilla sprite **above** the Nightfall image, fixed by the culling mask (finding 3).
A Nightfall rock is faceted and stands with its equator on the ground (`Blob` centered on `deck`,
the lower half is stuck in the planet): by construction it cannot float. The old image 1 finding
(twenty-one rocks on their full sprite box) stays what it was: one measurement per rock, without a
general ratio.

### 9. Image 31, "there is still a crate missing here": both crate clusters at the dropship sat too far north

The finding came through the collision rather than through the view: a collider without an object,
west of the dropship ramp, and image 32 (vanilla top-down view) shows what should be standing there:
crates and barrels. The collider is called `Outside/RocksNBoxes/boxcluster` and measures y −8.02 to
−6.00; the built crates (three guessed 0.6 ones) ended at y −7.35 though: the **southern half of the
collider was empty**, and it is exactly from the south (from the direction of Electrical) that you
walk toward it. The atlas also says the crates are about 0.86 wide, not 0.6. The twin `boxclust2`
east of the ramp has the same error one size up (collider to −8.35, crates to −7.45) plus a blue
barrel that the map does not draw there at all.

Both clusters are now remeasured from the atlas (standing line = bottom edge of the drawn front
face, the folding is artwork): three crates each, filling the collider all the way to the south
edge; on the west cluster plus the dark barrel at the back and the blue one with the red lightning
bolt at the front, on the east cluster snow caps and no barrel. Evidence `..\tmp\nfbox`, and image
31 clears itself up along the way: the "white cabinet" in it is the engine nacelle of the dropship
with its four fans, standing behind the now-filled cluster.

## The third playtest

### 1. The Weapons corridor was now too long: the second measurement was as wrong as the first

Reported as "the hitbox ends earlier than the drawn wall", and that is the overcorrection of the
image 22 fix: with the door **closed** the game stops the walker at the door box (south edge
−21.171), but the drawn door sat at −20.42 to −20.24, so 0.75 further out. The third measurement
went to the collider instead of the prop, and the rule is simple now: **the end wall of a porch is
the game's door box, both edges.** `Weapons/Walls/BottomDoor` measures y −21.171 to −20.463; the
inner edges of the side walls in the Ship collider end at −20.4607 and −20.4641, exactly at the
outer edge of the box. Cross-check on Comms: door box −19.629 to −18.921, built end wall −19.62,
identical.

What was wrong about the second measurement: it carried over Comms' pattern "end wall at the outer
edge of the door prop". At Comms that is only true **by coincidence**: Among Us folds the wall face
and cap northward over the standing line, and Comms' exit faces south, so the south edge of the
sprite coincides with the collider line (−19.63 against −19.629). Weapons' exit faces north: there
the north edge of the sprite is the drawn cap, not a standing line. A rule derived from the artwork
does not mirror along with it; one derived from the collider does. Changed in `weapons.js` (end wall
= door box, side walls, floor and ceiling back to −20.46), evidence `..\tmp\nfweap`.

### 2. The screen arrows now stand in the world

"They should not be on the screen either, but on the 3d map if anywhere." Task, sabotage and tracker
arrows are all the same machine: vanilla `ArrowBehaviour` on layer UI, merely wrapped by TOR's
`Arrow` class: a 2D sticker on the lens, the same class of bug as the show-through sprites from
images 33/34, only drawn by a different camera (the HUD cameras, which is why the culling mask of
the world camera did not catch them).

The solution has two halves:

- **Off the lens:** as long as the view is on, all `ArrowBehaviour` objects are parked on layer 2
  ("Ignore Raycast"), which no Among Us camera has in its mask, and put back on deactivation.
  Deliberately the layer and not the renderer's `enabled` flag: the game and TOR set that flag
  themselves every frame, and that is a fight you lose every other frame. The layer stays untouched,
  so the flag stays readable too: it still says which arrow is meant.
- **Into the world:** every visible arrow becomes a glowing target pin in space (`MarkerSprite`), in
  the color of the arrow (yellow = task, red = sabotage, tracker keeps its own). The pin floats at
  most 2.4 units ahead in the direction of the target and moves onto the target itself as you get
  closer: walking toward the pin is walking toward the task. What is behind you is behind you: a
  sign in the world is found by looking around, and that is the grammar of the whole mod. The pins
  **glow** (`Billboard.Glow`) and are exempt from the visibility cone: a direction hint is game
  information the player is entitled to, not a person who has to disappear in the dark. Walls still
  occlude it (the depth test stays), and no pin is drawn closer than 0.7: nothing sticks to your
  face.

Nothing is lost for the feel of it: the direction stays, only its carrier moves from the lens into
space. Checked in the new test shot `pinrule` (`..\tmp\nfpins`): three pins at −24°, 0° and 26°, all
readable, while the crewmate behind them still obeys the beam rule.

As a by-catch, **every billboard now stands on its real ground**: `Billboard.Base` carries the
standing height (`GroundAt`), players, pets and bodies included: before that, feet stood at
reference height 0 everywhere, so on the stairs and on the dropship deck they were in the floor.

### 3. Vents and the other world interactions from first person

The vent positions of the hand-built world match those of the game (the three outdoor vents from
`outsideprops.js` against the `vents` list of the survey: deviation ≤ 0.01), so there was nothing to
find there. The inventory of the interactions, each checked against its carrier channel:

| Interaction | Channel | From eye level |
|---|---|---|
| Consoles, tasks | USE button (aims at the nearest usable by itself), minigames are UI overlays | **works**; the pins now show the direction there |
| Vent in/out | USE / vent button in the HUD | **works** |
| Vent to vent | click on the world arrows (`Vent.ClickLeft/Center/Right`, via `ButtonBehavior` colliders) | **was unusable, fixed.** `Vent.TryMoveToVent` is `private` and is only reached from the three `ClickX()` methods: there are no direction keys, only the world arrows (verified by decompiling `Assembly-CSharp.dll`, not assumed). The arrows are positioned for the REAL top-down camera, not for Nightfall's image, and with the cursor captured no click was possible anyway. `NightfallControls.InputSuspended` now releases the cursor while `inVent`, and `NightfallView.Tick` gives the real image back for the same duration (world camera mask + arrows restored), so that cursor position and image match up again: a state in which nobody can see or attack the player anyway |
| Doors | collision + `SyncDoors`; door console via USE | **works** |
| Emergency button, report | usable + HUD button | **works** |
| Sabotage map | `MapBehaviour`, UI overlay | **was half broken, fixed.** It deliberately sits above the image and therefore looked usable, but was not: with the cursor captured, moving the mouse toward the reactor turned the player instead of pointing at it, and the sabotage could not be triggered at all. The same class of bug as an ability without a key, only more unpleasant, because the button is visibly there and visibly refuses. `MapBehaviour` now belongs to `InputSuspended`, so the view freezes and the cursor is free as long as a map is open: both maps, because in this mod family the normal one has click targets of its own (Forgotten Fixes' meeting ping and the language switcher). The MOVEMENT ROTATION was affected too and no longer is: `NightfallRelativeMovePatch` checks the narrower `MovementSuspended` (without the map), so with the sabotage map open you keep walking in the (frozen) viewing direction instead of falling back to Among Us' world axes |
| Bodies | own billboards, report via HUD | **works** |
| Ladders, moving platform | do not exist on Polus | open for the other maps |
| White outline of the nearest usable | world-side shader, hidden along with the vanilla world | missing; the USE button itself still lights up, that stays the feedback |

### 4. The Office table was a bistro table under a three meter hitbox

Reported as "the table in Office (the one with the button) is way too small (for the hitbox)", and
both are true at once: the collider `Office/caftable` measures x 17.71 to 21.40 (3.7 long), and the
area file describes exactly that rectangle, but the port of kit.js' round table built a **circle
with the shorter semi-radius** instead of the ellipse the prototype gets from non-uniformly scaling
a unit cylinder. What was left was a little plate 0.78 across in the middle of a hitbox full of
conference table. `Cyl` can now do `radiusY` (elliptical cylinder), and the round table uses both
semi-axes; that incidentally repairs the four other `round` tables of the map (Boiler Room, Comms,
lab), which are only near-circular by accident. Evidence `..\tmp\nftable` (looking from the west
end: the table fills the room lengthwise, the emergency button sits on it).

### 5. The west canyon at the seismic stabilizer (image 35)

"At the reactor there is a chasm with a collider that still has to be built downward; a big rock to
the left, to the lower left the rock goes on to about decontamination, from there it opens up
again." The game confirms it in its own words: the stair collider of the left stabilizer is called,
literally, **`bridgeLeft`**: the staircase is a bridge, and together with `Dropship/Walls` it fences
in the entire plateau edge. What was built there was flat planet, so every one of those fences was a
wall in the middle of nothing.

Built to the colliders, not to the artwork, wherever the two contradict each other:

- **The pits do not start until x 2.70.** The fence begins at (2.59/−8.03); west of that there is
  only the `OuterBoundary` rectangle out to the map edge: the pocket behind the painted mountain is
  **walkable** in the vanilla game (a hidden dead end), and a pit under a point you can stand on
  pulls the camera into it (`GroundAt`: pit wins). It stays solid ground.
  **→ SUPERSEDED.** "Walkable" was an assumption and not a measurement, and it is wrong:
  Electrical's north wall and `bridgeLeft`'s west end close the pocket. See "The boundary at both
  stabilizers, measured instead of assumed".
- The staircase keeps a **ridge of rock** underneath it (the bank slab pattern of the lava canyon:
  the planet plane has no side faces, so a cut-out strip below it would be a floating band).
- Pit floors set back 0.30 from every visible edge, banks as 2.15 thick dust slabs, platform slab
  and stair strip cut out of the holes (otherwise the eye would fall into the pit while on the
  stairs: the same trap the canyon already set once during construction).
- **Canyon back wall** as a row of rocks along the north edge, two more close the horizon above the
  walkable west pocket (north of y 2.39, out of any reach).
- **The mountain in the west** and the flank running down the west edge to y ≈ −20.6 (decon width),
  all eastern edges at x ≤ 0.10: the walkable strip next to Electrical (x 0 to 1.2) stays clear.

Holes in the plane in `AreaKit.Gorges` (the C# side owns the planet), everything else in
`outside.js`. Evidence `..\tmp\nfravine`: from the foot of the stairs the ground next to the bridge
visibly falls away, the canyon wall stands on the horizon; to the west the mountain next to
Electrical's wall.

### 6. Predator vision, as good as the budget allows

"Improve the Werewolf's vision as much as you can", with the unchanged proviso that performance
counts. All four changes replace equally expensive computation, none of them adds work:

- **Farther and brighter:** the night vision falloff runs with curve radius 3.0 instead of 2.2 and a
  floor of 0.17 instead of 0.14: the readable field grows by about a quarter, and the end of a
  corridor is a silhouette instead of emptiness. (Both renderers, identical curve.)
- **Shadow lift before the tint:** a parabola (`lum·(1.55−0.55·lum)`, no `pow`) lifts the mid
  shadows, where the old ramp pressed everything into the same near-black.
- **Prey runs warm:** a living player is lifted to full brightness in predator vision, so the red
  tint maps him to the hot end of the ramp: a heat signature against the cold room. One `max`
  operation per figure.
- **Blood-red fog:** distance is blood for the beast, not night; with the blue-gray vanilla fog the
  red image fell apart into two color worlds. One constant per frame.

Evidence `..\tmp\nfwolf\polusship_predator.png`: the room reads all the way to the end, both
crewmates stand out as warm signatures, the paws stay in frame.

### Measured after the third pass

The same measurement as always, all 91 viewpoints with a full turn, after all the changes (canyon,
rocks, crates, elliptical table, pins, base on every billboard, predator vision):

| | worst viewpoint | median |
|---|---|---|
| **854x480** | 18.8 ms (specimens_konsolen) | **14.1 ms** |

Compared to the state before it (21.8 / 14.3) nothing got more expensive: the new geometry lies
almost entirely behind the distance cap and the cell frustum, and the pins are a handful of
billboards. The old worst viewpoint (upper tube) no longer is; Specimens' console view now leads
with 18.8, below the 21.8 of back then.

## The fourth playtest

### 1. "I teleport down onto the lava when I walk across the bridge": the hole was under the door, not under the bridge

The suspicion went in two directions (bridge deck is not floor, or the smoothing slips into the
wrong plane), and both were measured instead of guessed. That is what the RenderTool's new
**`--ground x0,y0,x1,y1[,step]`** is for: it samples `Scene3D.GroundAt` (exactly the function the
mod builds its eye height from) along a line and prints every change. That closes half the tool's
blind spot: whether a height error sits in `GroundAt` or in the ~200 lines of smoothing between core
and game is now a measurement and no longer a guess.

The result clears the bridge completely: the deck (x 38.26 to 39.85) sits at 0, none of the 17 pit
surfaces overlaps it, and a sweep of all 164 non-pit floors against all pits found exactly one
overlap on the whole map (the platform slab of the left stabilizer juts 0.045 into the canyon pit,
unreachable behind the `bridgeLeft` fence). The only height error on the bridge route was the
**door opening of the airlock** (y −11.72 to −11.30): the room floors end at the wall faces, there
is no floor at all within the footprint of the wall itself, so `GroundAt` fell through to the planet
in the door opening: 0.185 lower. Walking through, the camera abruptly dipped by nearly a third of
the eye height and came back up, right in front of the bridge with the glowing river in view: that
is the reported "teleport down onto the lava". The smoothing is innocent (0.185 is far below the 0.9
teleport threshold; it even damped the drop).

The same floor gap sat in **eight doors** (Office east door, Admin west door, corridor tiled room,
airlock, among others): only the one with lava glowing next to it got reported. The fix is therefore
not a single correction but a pass: `AreaBuilder.SealThresholds` runs **after** all areas are built
(whether a threshold is missing depends on the decks of both neighboring rooms, and one of the two
may not exist yet while the wall is being built) and lays a threshold under every door or passage
opening with a hole: top edge 0.007 below the lower of the two neighboring floors (never coplanar
with an adjoining floor), registered as deck and drawn as a box. Doors onto open planet (Comms
porch, lab west door, Storage west door, the dropship hatch that never opens) get **no** threshold:
the step is real there, the floor really does end. Evidence: `--ground 39.05,-10,39.05,-19` shows a
0.007 dent instead of the 0.185 hole; both walking directions are flat.

### 2. Image 36: the vent on the minimap is missing in the world, and it was not the only one

The earlier finding "vent positions match the game (Δ ≤ 0.01)" was true for exactly the three vents
that were measured. Counting the game against the hand-built world (`vents` list of the survey, 12
entries, against all area files) gives: nine built (six in room files: Admin, Electrical,
Office/corridor, Storage, LifeSupp, Bathroom/laboratory; three in `outsideprops.js`), **three were
missing entirely**:

| Vent | Position (world.json) | Location |
|---|---|---|
| `SubBathroomVent` | (30.907 / −11.86) | open snow south of the lab, **the one from image 36** |
| `CommsVent` | (12.304 / −18.898) | on the planet at the southeast stilt of the Comms hut |
| `ElecFenceVent` | (6.90 / −14.41) | at the chain-link fence of Electrical |

All three are open-air vents and now stand in the vent block of `outsideprops.js`: the standard
grating 0.71 x 0.28 (the size the map draws every open-air vent at), centered on the world.json
position (Δ ≤ 0.005). Evidence `..\tmp\nfvents\` (all three rendered from eye level; at the Comms
vent the hut stands next to it, at the fence vent the fence).

### 3. Ceilings over the decon corridors to Specimens, all the pieces, not just the one

"Ceiling over the corridor decon → specimens" had already been reported as done once, and for the
upper tube that was true. Walking the complete route Admin → Specimens → lab airlock piece by piece
turned up three gaps, all closed:

| Section | State | Change |
|---|---|---|
| Lower decon chamber (Admin southeast corner) | ceiling present | none |
| Grating platform in front of Admin's decon door | open | covered along with the new stub ceiling of the lower tube (starting at x 25.63, exactly at Admin's ceiling edge: same height, an overlap would be a coplanar pixel fight) |
| **Lower tube** (`righttube.js`, stub + wide + narrow leg) | **deliberately open** (decision of the second playtest: "the ratio, not the principle") | four adjoining ceiling panels, walls back to the measured 2.10 (the 1.70 were the lowering "so you look over them at the planet", which buys nothing once there is a roof), lamps hung under the ceiling; snow stays (both ends are open doors, same reasoning as in decon2) |
| Upper decon chamber + upper tube (`decon2.js`) | ceilings present | none |
| **Northeast mouth of Specimens** (x 38.26 to 39.85, y −19.9 to −18.1) | **open**: the comment "beyond it is the tube, and the tube has no roof" had been out of date since the decon2 ceiling, and the wedges between stair roof and tube ceiling stayed sky, right above the threshold gratings | stepped ceiling strips over the mouth floors, ending exactly at −18.10 where decon2 takes over (adjoining, never overlapping) |

Evidence `..\tmp\nftube\`: the narrow leg as a corridor with windows, the bend, the view north from
the pod mouth across the bridge and the reverse view from the tube into the pod: continuous ceiling
everywhere.

### 4. Image 37 (Admin): the shelf and the brown body, both measured against atlas AND collider

The measurement reverses the first suspicion ("it juts too far into the room"): **the shelf was too
shallow, not too deep**, exactly as the user put it literally ("does not reach out far enough").
The rule is the one from the crate clusters of the second playtest: the standing line of a piece of
furniture is the **bottom edge of its drawn front face**, the folding above it is artwork. And the
game confirms it here both times in the collider:

| Object | drawn front reaches to | collider (Admin/Walls polygon) | as built | now |
|---|---|---|---|---|
| Bookshelf (middle of the south half) | y −24.87 | (22.65/−24.84) (21.58/−24.85) (21.57/−23.77) | body to −24.35, books to −24.42 | body to −24.85, book front on top of it |
| North element (next to the door) | y −22.30 | (21.65/−22.26) (22.09/−22.45) | body to −21.55, bottle front to −21.78 | body to −22.26, front to −22.49 |
| Southwest element (the "brown body") | - (no folded front) | x 20.47 to 20.93, y −25.68 to −23.79 | [20.50..20.91, −25.62..−23.79] | **unchanged: the measurement confirms it** |

So you bumped into the invisible remaining half of the hitbox half a meter in front of the books:
the same class of finding as the Office table in the third pass. The "very large brown body" behind
it is real: the map paints the brown mass, the game collides it as a wall; it looks alien because
from the southwest you only see its featureless west and top faces (the bottle front faces south).
Pulling the fronts of both shelves forward softens exactly that. The red crates on the floor moved
along with the fronts. Evidence `..\tmp\nfadmin\` (viewpoint of image 37, before/after).

### 5. The southern boundary: a row of rocks along the OuterBoundary

The south ended as a bare plane in the fog. What is really there: the `OuterBoundary` edge runs dead
straight along y = −28.03 from x 0.03 to (28.39/−27.97) and then rises in a diagonal to
(44.30/−23.33); west of x ≈ 28 the map paints a rock edge breaking downward (to y −31.4), east of
that the plane simply runs out. A drop-off downward was considered and rejected: from the inside, at
night, a cliff edge reads as exactly the "the world just stops" this is supposed to cure (the lesson
from the old planet edge of the second playtest). So what is built is the pattern of the west flank:
20 staggered rock masses just **outside** the boundary, along the straight section with north edges
at −28.2/−28.3, along the diagonal each rock 0.25 south of the southernmost fence point over its x
span, around the corner all the way to the east edge (x 44.3). Verified by computation: distance to
the fence everywhere 0.17 to 0.32, no overlap with existing geometry (only the intended staggering
among themselves). **Not yet shot from the inside in the RenderTool:** the export has run, the
visual check from the southern viewpoints (schneemaenner-sued, pfad-sued) is still pending.
**→ DONE**, finding mixed: see "The southern rocks: looked at, finding mixed".

### 6. Boundary at the stabilizers: NOT IMPLEMENTED, analysis available

**→ DONE, but not according to this plan.** The analysis below rests on two unverified assumptions
about walkability, and both have since been measured and are **wrong**. What was actually built and
what backs it up is under "The boundary at both stabilizers, measured instead of assumed". The text
below stays as evidence of how far a plausible guess can carry before somebody measures it.

Aborted at the user's request in the middle of the analysis. State of the findings, so that the next
person does not measure it again:

- **On the left** the one real gap is the **east side**: the pit does not start until x 5.50 **and**
  y −5.20, but the vanilla fence (`Dropship/Walls`) runs at y ≈ −7.25 to −7.48 (polyline
  (5.41/−7.32) (7.05/−7.25) (8.70/−7.48) (10.11/−7.27) (10.58/−7.09) (11.01/−6.46) (12.62/−7.18)
  (14.24/−7.95)): two full units of shelf between the path and the abyss. Plan: pit south edge
  segment by segment onto the fence (bank slab 0.30 + pit, the file's pattern), leave out the crate
  cluster area x ≥ 12.30 (the built crates reach up to y −6.0 into the bend of the fence). The
  Gorges holes in `AreaKit.cs` have to grow along exactly.
- **Also found:** the platform slab on the left overlaps the pit [2.70/−5.00/3.85/−3.30] by 0.045
  (x 3.805 to 3.85): the one deck-over-pit overlap on the entire map (sweep under finding 1). Cut
  the pit to x1 = 3.80.
- **On the right there is no canyon at all**, and beware: the painted canyon around the right
  stabilizer is **largely walkable** in vanilla (hidden pockets!). North of y ≈ −1.0 everything
  between the dropship fence corner (19.04/−1.00) and the OuterBoundary north edge is open, and from
  there you get to the platform ring unfenced and down west of the stairs: the only fences there are
  `bridgeRight` itself, `Dropship/Walls` in the south/west and two small `Science/Walls` pieces
  (24.82 to 26.03 / −6.97 to −6.64). **So pits are largely forbidden there** (pit under a walkable
  point = camera drop, finding class 1). The enclosing effect on the right has to come mostly from
  **rocks on solid ground**, not from holes; at most narrow pits right along the bridgeRight fence
  flanks. The same walkability check is still pending for the eastern extension on the left as well
  (the western access at x ≈ 2.7, y −6.5 looked unfenced in the collider dump: clarify before
  building!).

## The boundary at both stabilizers, measured instead of assumed

The open item from the fourth playtest ("the boundary should come closer to the path, on both
sides") hung on an assumption the predecessor had noted and never checked, and checking it
overturns it: **the pocket behind the painted mountain is not walkable, and neither is the painted
canyon around the right stabilizer.**

### The tool: a flood fill over the real collider dump

`..\tmp\reach.mjs` loads `polusship.json`, stamps every non-trigger collider of the layers Default
(the `OuterBoundary`), Ship, Objects and ShortObjects into a 5 cm grid, dilated by the **real player
radius 0.1564**, and floods from the spawn. `..\tmp\rim.mjs` then prints, per x column, the
**northernmost y position a crewmate can stand at**. That is the question every pit hangs on: a pit
under a reachable point pulls the camera into it (`GroundAt`: pit wins), and that is exactly the
error the area files must never produce.

The sensitivity has been checked: with the layer sets {0,9,11,12}, {0,9,12} and {0,9} the result
comes out exactly the same. So the statement does not hang on the question of whether a player walks
through `ShortObjects`.

**Result:**

| Location | Predecessor's guess | Measurement |
|---|---|---|
| West pocket on the left (x 0.1 to 2.5) | "walkable in the vanilla game, hidden dead end" | **not reachable**: Electrical's north wall (y ≈ −8.0) and `bridgeLeft`'s west end (2.59/−8.03) close it |
| Strip next to Electrical (x 0 to 1.2) | "walkable, stays clear" | **not reachable**: the same barrier |
| Canyon floor on the left | (implicitly not reachable) | confirmed: only the stair corridor (x 4.25 to 5.05) and the platform are reachable |
| Canyon on the right | "**largely walkable** in vanilla (hidden pockets!), pits largely forbidden there" | **not reachable**: between x 19.5 and 28.3, north of the fence only the stair corridor (x 23.65 to 24.60) and its platform are walkable |

With that the whole reservation falls away, and the edge may go where the map paints it.

### Left: the pit edge moves up to the fence

East of the stairs there were **two full units of shelf** between the `Dropship/Walls` fence
(y ≈ −7.3) and the pit (from −5.5 on). Every south edge is now a measured rim value; the bank starts
on that line and is 0.30 deep, so the drop-off lies about a quarter unit in front of the last
reachable collider center (the rim is a *transform* position, the collider center sits 0.25 further
south: that is the entire safety margin, and it is deliberate).

| x span | measured rim | Bank | Pit from |
|---|---|---|---|
| 5.15 to 5.50 (tongue east of the stair railing) | −7.30 | −7.30 to −7.00 | −7.00 |
| 5.50 to 7.90 | −7.20 | −7.20 to −6.90 | −6.90 |
| 7.90 to 10.30 | −7.15 | −7.15 to −6.85 | −6.85 |
| 10.30 to 10.75 | −6.90 | −6.90 to −6.60 | −6.60 |
| **10.75 to 11.55** | −6.50 | −6.05 to −5.75 | −5.75 |
| 11.55 to 12.10 | −6.65 | −6.65 to −6.35 | −6.35 |
| 12.10 to 13.90 (crate cluster) | — | unchanged −5.50 to −5.20 | −5.20 |

The exception in the middle is a real find: at x 11.05 to 11.42 stand the valve box of the dropship
and its little barrel (`dropship.js`), y −6.45 to −6.05. They are behind the fence and drawn in
properly, but a box hanging over an abyss is worse than a ledge: the edge makes a northward detour
there, and the box stands on a shelf.

Plus the two small items the predecessor had already named: the **tongue x 5.15 to 5.50** (a strip
of solid ground 4 units long right next to the stairs) is now pit (the platform slab reaches to
x 5.05 measured to within 0.05, so 0.10 of clearance remains), and the **platform slab / pit
overlap** has been cut back from x1 3.85 to **3.80**.

### Right: the canyon that did not exist yet

Built to the same pattern, from the same measurement:

| x span | Rim | Bank | Pit from |
|---|---|---|---|
| 19.50 to 21.30 (north of the east crates) | −7.35 | −6.05 to −5.75 | −5.75 |
| 21.30 to 21.90 | −6.75 | −6.75 to −6.45 | −6.45 |
| 21.90 to 23.60 | −6.45 | −6.45 to −6.15 | −6.15 |
| 23.60 to 24.75 | stairs, −2.65 | **ridge** −7.20 to −2.35 | −2.35 |
| 24.75 to 26.20 | −6.90 | −6.90 to −6.60 | −6.60 |
| 26.20 to 27.40 | −6.55 | −6.55 to −6.25 | −6.25 |
| 27.40 to 28.30 | −7.00 | −7.00 to −6.70 | −6.70 |

Three decisions in there:

- **The staircase keeps its ridge**, for the reason the left one has one: the planet plane has no
  side faces, so a strip left standing between two holes would be a floating band with invisible cut
  edges.
- **The west end stops at x 19.50**, because the hull of the dropship reaches to 19.44, and the edge
  runs **north** of the `boxclust2` crates (which end at y −6.32) instead of underneath them.
- **The east end stops at x 28.30 and is closed with rocks on solid ground**, not with more holes:
  from x 28.5 on the plane north of the lab begins, and in the hand-built world the lab reaches to
  y −5.00 in places. A rock costs nothing when it stands in the wrong spot.
- Plus four rock masses as a canyon back wall along the north edge (y 1.2 to 3.2) and two on the
  east flank.

### Verified, three ways

**1. No hole under walkable floor.** `..\tmp\pitcheck.mjs` reads the **generated**
`PolusAreas.g.cs` (that is, what the mod ships), samples every `Pit` surface in 8 cm steps and asks
the flood fill for every point. Result: **0 reachable points in all 28 canyon pits.** The first run
still found 3: the east edge of the pit reached 0.10 into the stair corridor, whose floor starts at
x 23.65; edge pulled back to 23.60, then clean.

The lava canyon reports 74 reachable points in the process, and that is **not** a bug: Among Us
paints the lava without any collider, in vanilla you walk right over it, and `outside.js` explicitly
says you are meant to go down here rather than across. The checker tells the two kinds apart by the
deck (−2.30 canyon, −1.25 lava).

**2. The heights are correct, measured with `--ground`.**

```
left   x=9,00   : planet −0,185 up to y −6,85, then −1,518   (edge as planned)
left   x=11,10  : planet −0,185 up to y −5,75, then −1,518   (the niche around the valve box)
right  x=22,50  : planet −0,185 up to y −6,15, then −1,518
right  x=24,17  : −0,185 → −0,172 → … → 0,594 (the nine steps and the platform), from −2,35 on −1,518
```

The last line is the important one: the staircase carries the camera from the plane up onto the
platform, and the abyss does not start until **north** of it.

**3. Looked at.** `..\tmp\nfstab\`: from the right staircase the suppressor stands between red
railings, the ground falls away left and right and the rock wall closes the horizon; from the
platform, the same to the east and west; from the left fence at (9/−7.55) looking north, the canyon
floor lies in front of the canyon wall instead of the old plane.

### The southern rocks: looked at, finding mixed

The visual check had been pending since the fourth playtest. `..\tmp\nfsued\`, three viewpoints:

| Viewpoint | Finding |
|---|---|
| `pfad-sued` (9.8/−20.7 facing west) | **good.** The row stands as a staggered ridge on the horizon, snow caps readable, no gap in between |
| `sued-diagonale` (34.0/−25.5 facing WNW) | **good.** The diagonal reads as a rock spine; between two masses there is a narrow sliver of sky that passes for a cleft |
| `schneemaenner-sued` (18.2/−26.4 facing south) | **coarse.** Here it is only 1.8 units to the rock edge, and from that close the rock kit falls apart into large flat facets: bright caps fill the top of the frame, the silhouette reads as a slab rather than a wall |

The third finding is **not fixed and deliberately not guessed at**. It is not the placement (the row
stands exactly where the `OuterBoundary` runs and closes the horizon completely) but the kit: a
`rock` is three blobs from a 6x4 sphere, and that is a resolution meant for "a rock on the horizon"
and not for "a wall two meters in front of your face". The candidates would be more domes for this
row (costing triangles exactly where most of them are in frame anyway) or moving the rocks 0.4
further south (which brings back the "the world just stops" they were meant to cure). Both are a
trade-off and not a bug fix.

## The roles of the three mods in first person

Up to this point Nightfall was a world. What was missing was the game inside it: The Other Roles,
Unknown's Collection and TOR - Forgotten Fixes bring over sixty roles between them, and **not a
single one of them had ever been checked against this view**. This section first records what was
found, and then what followed from it.

### The state of things, read out of the code

Three observations, each from one line of code, and each one decides an entire class of abilities:

**1. The culling mask hides not only vanilla but everything.** `NightfallView.HideWorld` narrows the
mask of the world camera down to `1 << 1`, exactly the one layer the fullscreen sprite sits on. That
was aimed at the show-through vanilla sprites (images 33/34) and hits **every world-side
SpriteRenderer any mod creates** along the way: a Saboteur trap, a Collector relic, a ghost hand, a
Tesla indicator, a portal disc, a Ninja trace. They are not broken and not switched off, the camera
simply does not get to see them any more. That is the one cause behind almost every finding of this
pass.

**2. The HUD is untouched, and that is exactly why it is a problem.** The HUD cameras (depth 99/100)
keep drawing, so ability buttons, task list, chat and meetings are visible. But they are not
clickable: `NightfallControls` locks the cursor (`CursorLockMode.Locked`), and a locked cursor turns
the view instead of pointing at a button. There is an emergency exit, **holding ALT releases the
cursor** (`CursorReleaseHeld`), but it is an emergency exit and not a control scheme: whoever holds
ALT can no longer turn, and whoever stands in the dark in front of a Werewolf has no time for
aim-with-the-mouse. **Without a hotkey an ability is practically impossible to trigger in this
view.**

**3. TOR already has the key channel, it is just half-assigned and labelled nowhere.**
`CustomButton` (TOR, `Objects\CustomButton.cs`) carries a public `KeyCode? hotkey`, checks it every
frame in `Update` (`if (hotkey.HasValue && Input.GetKeyDown(hotkey.Value)) onClickEvent()`), and the
list of all buttons sits there as `public static List<CustomButton> buttons`. Everything Nightfall
needs is therefore **reachable from outside, without changing a single line of TOR**: enumerate
buttons, overwrite `hotkey`, grab `actionButton` for the label. UC builds its buttons through the
same class, so the same applies there.

What TOR does **not** have: a display of the key. `actionButtonLabelText` carries the ability name
(and only if `showButtonText`), `cooldownTimerText` the timer. Today the player learns their key
from the wiki, not from the screen.

### The key situation as it stands today

TOR's convention is a one-role assumption: `Q` = kill (tied to the Among Us kill binding), `F` =
ability (tied to the Among Us ability binding), `G` = second ability (`Action2Keycode`), `H` = third
(`Action3Keycode`). As long as a player has exactly one role, nothing collides, because `Sheriff.F`
and `Medic.F` never exist at the same time.

That assumption no longer holds. A player today can carry all at once: a role, a modifier with its
own button (Shifter), a role with up to four buttons (Saboteur, Poltergeist), a counterplay button
belonging to someone else (Saboteur's SEARCH belongs to every non-Impostor, Poisoner's antidote to
the Medic) and a UTS add-on. Two buttons on the same key do not stand out until both meet in one
round: the same class of bug as the option IDs, for which this project already keeps a registry
(`..\ID-Registry.md`).

### The world relay: one machine instead of thirty (`WorldRelay.cs`)

The obvious route would have been to teach Nightfall every ability individually: read UC's trap
list, read TOR's portal list, redraw every relic. That is thirty pieces of reflection against three
mods that keep being written, and it is wrong on the day somebody adds a role.

The survey says there is a much simpler way. **Every** world object in this mod family is a bare
`new GameObject(...)` **at the root level of the scene**, with one or more `SpriteRenderer`s under
it: UC sends absolutely everything through `UCFx.NewFxRoot` (layer 11), TOR writes
`new GameObject("Trap")`, `"Portal"`, `"Garlic"`, `"Bomb"`, `"JackInTheBox"`,
`"NinjaTrace"`, `"Silhouette"`, `"FootprintHolder"` without any parent at all. Screen UI is exactly
the opposite: it hangs under `HudManager`, so under **one** root that you skip.

So the relay does not know what a trap is. Four times a second it walks the root objects of the
scene, skips the four that Nightfall draws itself (ship, players, pets, bodies) plus the HUD and the
cameras, and turns everything else into billboards. A new role shows up in first person on the day
it shows up in the game, without a line here.

Two properties fall out for free in the process, and both count:

- **The relay inherits the visibility rules of the roles.** A trap only the Saboteur may see is
  already `SetActive(false)` for everyone else; Shade's body markers are only created for the Shade
  in the first place. The relay reads the live renderers, so by construction it cannot show anything
  the game has hidden. The same argument `NightfallView.IsVisible` makes for invisible players, and
  for the same reason.
- **The sorting depth is the height.** A sticker on the floor is drawn at `z = y/1000` (so roughly
  zero), an effect that is supposed to sit *above* everything, an aura, a hex halo, a ring of sparks
  around the chest, at `z = -1.2` so that it wins the 2D sorting. The depth the mod sorts by is
  therefore incidentally and reliably also its height above the floor. Everything with `z < -0.5`
  floats at 0.45, everything else stands on the ground.

What gets lost along the way: a billboard stands upright, a trap lies flat. Drawn as an upright
board it is the same compromise as the props of the photographed maps ("billboards instead of
boxes") and just as right here: a sticker flat on the floor is nearly invisible from eye level, and
what you must not step on has to be visible across the room. The photograph is refreshed three times
a second, the **position every frame**: whatever moves, moves correctly, only its image is a third
of a second old.

### Task 1: ability by ability

State before this pass, and what became of it. "World sprite" means: was invisible due to the
culling mask, is back in frame now via `WorldRelay`.

#### Unknown's Collection

| Role | Ability | State before | Changed |
|---|---|---|---|
| Saboteur | stun trap (`SaboteurTrap.cs`) | **invisible** (world sprite, layer 11) | Relay. Stands as a board on the floor, in the color of the trap |
| Saboteur | sabotage task marker (ring at the marked terminal) | **invisible** (world sprite) | Relay |
| Saboteur | kill FX (sparks → bloom) | **invisible** (world sprite) | Relay |
| Saboteur | SEARCH minigame (`SaboteurScanUI`) | worked (HUD under HudManager) | key **M** (see task 2), controls E/Space/Enter unchanged |
| Saboteur | self-limp | worked (button only) | the key is now printed on the button |
| Collector | relics on the map (`CollectorRelics.cs`) | **invisible**, and with it the entire role | Relay. Crystal, halo and three sparks arrive as one image |
| Collector | relic sense of the Impostors | **invisible** (it is the alpha of the relics themselves) | Relay: the alpha moves into `Fade`, the fading stays |
| Collector | collect burst, arrival burst, victory aura | **invisible** | Relay |
| Poltergeist | manifestation (`PoltergeistManifest`) | button ok; the created likeness is a player | player billboard already covers it; button **T** labelled |
| Poltergeist | door haunt, hex burst, poof, reveal | **invisible** (world sprites) | Relay |
| Poltergeist | ghost hand (pulsing ring at the reactor) | **invisible** | Relay, and this was the worst case: the hand is a *channel* the player has to hold, and without feedback he holds it into the void |
| Poltergeist | hex halo on the victim, own aura | **invisible** | Relay |
| Poltergeist | hex vignette (blind/night vision) | worked (`hud.FullScreen` clone, HUD camera) | nothing |
| Poltergeist | toggle hex mode (**J**), manifest template (**K**) | worked (raw key input) | nothing; both keys are noted in the registry |
| Manipulator | faked admin/vitals display | worked | nothing. Both are device UI and are only open while you stand in front of them, and then Nightfall freezes the view anyway (`InputSuspended`) |
| Manipulator | glitch sparks around itself | **invisible** | Relay |
| Tesla | ring of sparks on the charged victim (`TeslaParticles`) | **invisible** | Relay. The ring is the only warning that two poles are converging |
| Tesla | discharge + chain lightning between the victims | **invisible** | Relay |
| Tesla | charge display, own status, meeting UI | worked (HUD) | nothing |
| Illusionist | clone (`IllusionistClone.cs`) | **invisible**, a role whose entire purpose is a deception | Relay. **Special case:** the clone is the only world object of either mod that sets *no* layer (layer 0 instead of 11). The relay does not filter by layer but by root, so it does not fall through anyway |
| Illusionist | materialize/dissolve poof | **invisible** | Relay |
| Illusionist | REC indicator | worked (HUD) | nothing |
| Shade | body markers (Shade only) | **invisible** | Relay; it inherits the "Shade only" rule along with it |
| Maniac | explosion, handover wisp | **invisible** | Relay |
| Maniac | bomb name tag | worked (name text of the carrier) | nothing. **But:** there are no name tags in first person, Nightfall draws players as a photo without `nameText` (deliberately, since the second playtest). So the carrier can only be identified from the HUD text. **Open, see below.** |
| Werewolf | blood ring + paw print under the body | **invisible** | Relay |
| Werewolf | transformation flare, silver death flipbook | **invisible** | Relay |
| Werewolf | wolf form (body + full-body hat) | worked (player photo, `AvatarCapture`) | nothing |
| Werewolf | victory screen at the end of the round | worked (end screen, the view is already off by then) | nothing |
| Hunter | silver shot | worked (button **Q**) | label |
| Pelican | swallow, belly display, hunt countdown | worked (HUD) | label |
| Poisoner | antidote ring on the target | **invisible** | Relay |
| Poisoner | antidote button (belongs to the **Medic**) | worked (**G**) | label; noted in the registry as a foreign button |
| Siphoner | range ring around itself | **invisible**, and that is the ring you read the range off | Relay |
| Copycat | four learned abilities | **not triggerable**: all four on `KeyCode.None`, so mouse-only buttons | get keys from the free pool (see task 2) |
| Copycat | morph shimmer, shot trail, shield aura | **invisible** | Relay |
| Follower | shift wave | **invisible** | Relay |
| Scout | phase shift (transparency) | worked | nothing: `IsVisible` reads the alpha of the body, so the Scout disappears here too |
| Scout | poof on toggling | **invisible** | Relay |
| Beacon | vignette, status badge | worked (canvas/HUD) | nothing |
| Bug | glitch overlay | worked (canvas, sortingOrder 999) | nothing |
| Silencer | name tag marking | worked in the meeting | nothing. Outside the meeting there are no names in first person (see Maniac) |
| Witness | red pulsing name of the killer | ditto | ditto |
| Auditor | panel + victim hint | worked (HUD) | nothing |
| all | kill cutscenes (`UCKillOverlay`, `UCKillOverlayTOR`, 20 kinds) | worked | nothing. They hang under `HudManager` on its layer and are drawn by the HUD cameras, so the culling mask of the world camera does not reach them. That was the one worry that turned out to be unfounded on reading up |

#### The Other Roles

| Role | Ability | State before | Changed |
|---|---|---|---|
| Trapper | trap (`Objects\Trap.cs`, layer 11) | **invisible** | Relay |
| Bomber | bomb (`Objects\Bomb.cs`, layer 11) | **invisible**, and it has to be defused, by anyone | Relay |
| Vampire / all | garlic (`Objects\Garlic.cs`, layer 11) | **invisible** | Relay |
| Trickster | jack-in-the-box (`Objects\JackInTheBox.cs`, layer 11) | **invisible**, and a fake vent nobody sees is not a lie | Relay |
| Portalmaker | portal + foreground animation (layer 11) | **invisible** | Relay |
| Ninja | trace (`Objects\NinjaTrace.cs`) | **invisible** | Relay |
| Detective | footprints (`Objects\Footprint.cs`) | **invisible** | Relay (the `FootprintHolder` is a root object as well) |
| Ninja / Vampire | blood trail (`Objects\Bloodytrail.cs`) | **invisible** | Relay |
| Seer | soul silhouette (`Objects\Silhouette.cs`) | **invisible** | Relay |
| Tracker, Snitch, Vulture, Bounty Hunter | arrows to the target | already solved (third playtest): as glowing pins in the world | nothing |
| Security Guard | cameras, sealed vents | **partly open**: a placed camera hangs under the ship, not at root level, and therefore falls through the relay. The camera screen itself is a minigame and works | **open**, see below |
| Engineer, Medic, Sheriff, Jackal, … | everything that is just a button | worked | label; keys unchanged |
| Guesser | guessing in the meeting | worked | nothing: it is a mouse UI **in the meeting**, and there Nightfall releases the cursor anyway (`InputSuspended`) |
| Shifter (modifier) | swap roles | **not triggerable** (`hotkey: null`) | key **V** |
| anyone | place garlic | **not triggerable** (`hotkey: null`) | key **B** |
| anyone | defuse bomb | **not triggerable** (`hotkey: null`), and that is the worst of the three: there is a time limit | key **N** |

#### TOR - Forgotten Fixes (UTS)

| Feature | State before | Changed |
|---|---|---|
| BomberCancel, MedicReshield, TrapperLimp, TricksterAvatarSabotage | worked (buttons **G/G/H/C**) | label |
| LoverRevenger | worked, but on **Q**, and the Revenger is granted by the Lover *modifier*, so it can sit on a Sheriff, Jackal, Thief or Vampire, all of which have Q themselves | fixed key **X** |
| MeetingMapPing, MapLanguageToggle, LawyerLoverTracker, SnitchLogic | worked (all minimap, so HUD) | nothing |
| InvertVision, SpyExtras flash | worked (`hud.FullScreen`) | nothing |
| LobbyPasswordGate, ModManagerUI, SettingsShare, WebConfig | menu/lobby, outside the view | nothing |

### Task 2: the key bindings across all three mods

First the complete list, then the distribution: the order is the entire point, because today the
three mods hand out their keys independently of each other.

**Taken, across everything:**

| Key | Who |
|---|---|
| W A S D, arrows | movement (Among Us) |
| E, space | use/confirm (Among Us); UC's Saboteur scan |
| Q | kill (Among Us binding); TOR Sheriff/Jackal/Sidekick/Vampire/Thief; UC Hunter/Pelican; UTS LoverRevenger |
| R | report (Among Us); TOR Hunter arrows / PropHunt reveal; UC Illusionist recording |
| Tab | map (Among Us); TOR option pages |
| F | TOR "ability": around 25 role buttons, plus 8 UC buttons |
| G | TOR Action2: 9 buttons; UC Poltergeist hex, Maniac handover, Poisoner antidote; UTS BomberCancel, MedicReshield |
| H | TOR Action3 (Hacker vitals); UC Poltergeist hand, Saboteur limp; UTS TrapperLimp |
| I | TOR PropHunt invisibility |
| J | TOR use portal; UC Poltergeist hex mode |
| K | TOR event kick; UC manifest template |
| L | TOR end round (developer) |
| C | UC Saboteur trap; UTS Trickster mixup |
| T | UC Poltergeist manifestation |
| LeftShift / RightShift | TOR PropHunt unstuck / return to lobby |
| Keypad+ | TOR spectator zoom |
| 1-7, keypad 1-7 | TOR option pages (lobby only) |
| F1 F2 | TOR settings / summary |
| F8 | NightfallSurveyTool survey (its own plugin, not Nightfall itself) |
| F9 | Nightfall force view |
| Alt | Nightfall release cursor |
| Esc, Enter | menus; UC scan abort; UTS dialogs |

**Free and therefore assignable** (in this order, by reachability with one hand on WASD):
`V B N M X Y Z U O P`, then `, . ; ' [ ] / - =`.

**Fixed assignments** (`NightfallKeys.Preferred`, named after the static field that holds the
button: the only stable identifier, because `CustomButton.buttons` is otherwise just a list in
creation order):

| Button | Key | Why this one |
|---|---|---|
| `HudManagerStartPatch.shifterShiftButton` | **V** | Shifter is a *modifier*, so it sits on top of an arbitrary role |
| `HudManagerStartPatch.garlicButton` | **B** | belongs to **every** living player as soon as garlic is in the game |
| `HudManagerStartPatch.defuseButton` | **N** | belongs to every living player while a bomb is armed |
| `Saboteur.searchButton` | **M** | belongs to every non-Impostor, and it sat on **F**, which the Scout needs for its own role |
| `LoverRevenger.revengerButton` | **X** | granted by the Lover modifier, sat on **Q** like the role underneath it |

Everything else keeps the key of its own mod. Nightfall is not a rebinding mod: someone who knows
that the Sheriff shoots with Q should not have to relearn it because a first-person view is
installed. Exactly two things change: **an empty key gets filled**, and **a key that collides with
another button of the same player at the same moment gets moved**, out of the free pool, permanently
for the round, so that it does not wander under the player's fingers. The Copycat buttons get their
four keys that way (they live in a dictionary instead of in four fields, so they cannot be addressed
by name).

`originalHotkey` is **never** written. TOR's own binding to the Among Us keys for kill and ability
(`ReloadHotkeys`, runs once per round from `resetVariables`) therefore stays fully functional: it
simply runs before Nightfall, every frame, and loses the last word exactly where it has to lose it.

**The label** sits in the top right corner of the button and is a *clone of the button's cooldown
text*, not a fresh `TextMeshPro`: a bare `AddComponent<TextMeshPro>()` gets no font asset in Il2Cpp
and draws nothing, while the cooldown text is guaranteed to carry the font Among Us ships. It is on
**always** by default, not only during the view (`Keys / AlwaysOn`): a key you learned during the
round is one you already know when the lights go out.

**No intervention in TOR was necessary.** `CustomButton.buttons` is `public static`, `hotkey` is
`public`, `actionButton` is `public`: the whole job is reachable from outside. Nightfall still does
not reference TOR (it has to load without TOR and without UC), it goes through ordinary reflection
against the loaded assemblies; TOR is a normal managed plugin, so this is ordinary reflection
without any Il2Cpp marshalling question.

### Where the view stays off on principle

Four locks stand **before** everything else, before the debug key F9 and before the 3D mode as well.
They do not answer "does the player want this" but "is this even a situation in which a corridor
makes sense".

| Lock | Why |
|---|---|
| **Ghost** | Existing decision. The entire remaining game of a ghost is tasks and watching, and neither survives being put into a corridor |
| **Meeting, voting, exile** | The head must not follow the cursor that is currently voting. `MeetingHud` and `ExileController` |
| **End of round** | **New.** Between the win condition triggering and the actual scene change there are one to two seconds in which `ShipStatus` still exists, nobody is dead as far as `PlayerControl` is concerned, and the game is already drawing its end screen. Every old condition kept saying yes in that window, and the first-person view kept running underneath the evaluation. Two independent signals, because they arrive at different moments: a flag from the `OnGameEnd` patch, and `AmongUsClient.GameState`, which leaves the `Started` state the instant the round is decided |
| **Maps without a description** | **New.** Only Polus has a hand-built world (`PolusAreas`, 17 areas surveyed by hand). Skeld, Mira, Airship and Fungle used to run through the old collider path, and that was never good enough to play on: a collider is not a wall, it runs into every door niche and out again, windows, bases, door frames and lintels are missing from it entirely, and the props are upright photographs of a top-down view. It *renders*, and that is exactly the problem, because "it renders" reads to a player as "this is what the mod is", and he judges Polus by Skeld |

The map lock deliberately stands **before** `ManualOverride`: on a map without a described world
there is nothing worth forcing, not even for testing. The check asks the same thing as `Scene3D.Build`
does when it chooses between the two paths: one answer in one place. One line per map goes into the
log while it does, so that "nothing happens" does not happen silently.

**A side effect that saves more than the lock itself:** on a map without a description, the **map
photograph and the sprite harvest** are now skipped as well. Both existed only to feed an image that
is never drawn there: the photograph is the map at 52 pixels per unit (double-digit megabytes lying
next to a running Among Us), the harvest is one camera render per drawn object, which briefly
freezes the game. Skeld, Mira, Airship and Fungle therefore cost Nightfall one geometry pass and
nothing else. The collider path itself stays in the code and reachable in the RenderTool
(`--colliders`); it is one line away from being wanted again as soon as a second map is described.

### The 3D mode: Always / Werewolf only / Never

Up to this point there was exactly one trigger: the transformation. The new switch turns that into
three possibilities, and it is the **first host-synced setting** of the mod (the item had been on
the open list for a long time).

| Value | Meaning |
|---|---|
| **Werewolf only** (default) | the previous behavior: the view starts when the UC Werewolf transforms and ends when it transforms back |
| **Always** | first person for the whole round, regardless of the Werewolf |
| **Never** | off |

The default is deliberately **Werewolf only**: an existing round plays exactly as it did before, and
nobody finds themselves in a corridor unasked after an update.

**Why a TOR option and not a BepInEx config.** Everything Nightfall configures so far is a matter of
taste on one machine: torch range, turn speed, resolution. The mode is not. It decides whether a
player spends the round in a corridor or looks down at the map from above, and two players who
answer that differently are not playing the same game. That is exactly the argument behind the lobby
handshake, and it has the same answer: the host decides, and the value travels along. TOR's
`CustomOption.ShareOptionSelections()` distributes every `(id, selection)` pair of its list; whoever
registers there is host-synced for free.

**The option ID is 1700**, out of a block **1700-1719** taken for Nightfall. IDs have to be unique
across plugins in this mod family, because a duplicate does not crash loudly
but silently overwrites the selections of the other mod (`..\ID-Registry.md`: ChanceMod 11xx,
UsefulTORStuff 12xx-13xx, Unknown's Collection 14xx-16xx up to 1699, and 1700 is the first free
value above that).

**When "Always" takes effect.** Not "at round start", but at the two places where the player is
really playing:

- The **world model** is never the limit: `PollMapChange` builds it as soon as `ShipStatus.Instance`
  shows up, so when the map loads and long before roles are handed out.
- The **lobby** has no `ShipStatus` at all (it has `LobbyBehaviour`), so it already falls through the
  existing condition. That is as it should be: the lobby has no collision geometry and no map, and a
  first-person view of it would be a first-person view of nothing.
- The **intro cutscene** is a fullscreen HUD overlay, so it would sit on top of an image that gets
  computed anyway, and it runs while you cannot move, which is the worst conceivable first
  impression of a view whose whole point is walking. "Always" waits for it to finish.
- **Meetings and exile** stay excluded as before, for the old reason: the head must not follow the
  cursor that is currently voting.

**Ghosts keep the top-down view**, in all three modes. The decision was already in `ShouldBeOn()`
(`me.Data.IsDead → false`) and is untouched: the entire remaining game of a ghost is tasks and
watching, and neither survives being put into a corridor.

**What happens when the host has the mod and a fellow player does not.** Two gates stand side by
side, and the new one does not replace the old one:

1. `RequireEveryone` (default **on**) demands that **everyone** in the lobby has answered the
   handshake. If that is not the case, the view stays off for everyone, no matter what the mode
   says. The missing player would otherwise keep the top-down view, and in a blackout that is not a
   cosmetic advantage but a gameplay one. The host gets a log warning with the names once.
2. The mode takes effect **inside** that gate. Because "everyone has the mod" includes the host, the
   host always has the option whenever it can do anything at all, so the value is always the host's.
   That closes exactly the hole that earned UTS its `UTSGate` class: an option the host does not own
   is never sent, and the client quietly keeps computing with its own stored value.
3. The one place where the two can diverge is the explicit emergency exit `RequireEveryone = false`
   (solo test). Then a client computes with its local value, which is what the switch is for, and
   what its own description says.

Without The Other Roles the mode falls back to a local config entry `Nightfall / Mode`. Practically
nothing is lost: without TOR there is no Unknown's Collection either, so no Werewolf to trigger on,
and what is left is the debug key and "Always".

### What stayed open

- **There are no names above heads in first person**, and three abilities depend on them: Maniac's
  bomb tag on the carrier, Silencer's silence marking and Witness' red pulsing killer name all write
  into `cosmetics.nameText`, which `AvatarCapture` has explicitly **not** photographed since the
  second playtest (otherwise every crewmate wears its name tag in its chest). In the meeting all
  three work; outside in the dark they are missing. That is not a malfunction but a decision that
  belongs to the user: putting name tags back into first person is exactly the radar question that
  makes players outside the light cone disappear in the first place.
- **Security Guard's cameras** hang under the ship instead of at root level and therefore fall
  through the relay filter. The filter *could* search the ship for foreign objects, but then it
  would have to tell Nightfall-drawn map props from attached ones, and there is no reliable test for
  that. Deliberately left open instead of guessed at.
- **Soft edges are lost.** `IBillboardSource.Sample` knows no partial alpha, it cuts off hard at
  alpha 24. A soft glow therefore becomes a disc with an edge. For traps, relics and clones that is
  right, for spark clouds it is coarser than the original.
- **Vanilla buttons stay unlabelled** (use, kill, report, sabotage, vent). They carry the bindings
  from Among Us' own settings, and reading those back out of Rewired is a second mechanism for a
  button every player knows anyway.

### Measured after this pass

The same measurement as always, all 91 viewpoints with a full turn, after the new canyon on the
right, the pulled-in edge on the left, and the southern and eastern rocks:

| | worst viewpoint | median | triangles | model |
|---|---|---|---|---|
| **854x480** | **16.64 ms** (boiler_durchgang) | **13.69 ms** | 38,228 | 20.8 MB, 433 ms build time |

For comparison, the state before it: 18.8 / 14.1 ms at 30,454 triangles. The world grew by a quarter
and **got faster**: the old worst viewpoint (specimens_konsolen) no longer leads, and for the first
time even the worst viewpoint is below the 16.7 ms of a 60 Hz frame. The reason is the same as last
time: the new geometry lies almost entirely behind the distance cap and the cell frustum, and the
canyon *removes* plane triangles from the cells the planet used to lie in.

**What this number does not include:** the billboards of the world relay. The RenderTool knows no
mods, so its measurement contains not a single trap and not one relic. The relay is capped (at most
64 forwarded roots, at most 220 billboards in frame) and shares the capture budget of one photograph
per 0.12 s with `AvatarCapture`; the actual cost in game is **not measured yet** and belongs in the
next playtest.

## State

Done: survey, map photograph, world model, triangle rasterizer, floor and walls from the real
artwork, 8-direction figures for crew and beast, torch, predator vision, night sky, screen hookup,
controls, trigger, handshake. **854x480 at roughly 15 ms per frame on average on Polus** (see "How
high the resolution may go").

**The first pass in the game has been played** and triggered the whole section before it: mip
pyramid, doubled texture resolution, round light cone with a warm color, the hand torch back in
frame, a baked starry sky and eight corrections to the world.

**The second pass has been played too** (images 28 to 34, its own section): vanilla world hidden via
the culling mask, players only visible in the light cone, Office's west door in its real place, the
lava bridge flicker traced back to three coplanar end boxes, the planet plane extended out behind
the fog, the drawn crewmate figure rebuilt according to the game's own drawing rules, and the avatar
photograph now holds hats, skins, visors and pets (pets as their own billboards at their real
position).

**This pass is about the roles, the keys and the mode** (own sections above): everything the three
mods place into the world comes back into frame through a single relay instead of thirty special
cases; every ability has a key and carries it in the top right corner of its button, with a binding
list kept across all three mods plus vanilla and five fixed assignments for the buttons that had
none or shared one; the 3D mode is a host-synced TOR option (1700). Plus the boundary at **both**
stabilizers, built from a measured walkability grid instead of from two guesses that the measurement
refuted both of, at **16.64 / 13.69 ms** across all 91 viewpoints, so for the first time below the
16.7 ms of a 60 Hz frame in the worst case as well.

**The third pass has been dealt with** (own section above): the stairs really do carry the camera
now (the smoothing state survived no frame, the one bug the shared core could not see by
construction), the Weapons porch ends at the game's door box, the screen arrows stand in the world
as glowing pins (vent neighbors and interaction inventory included), the Office table is the ellipse
across its entire hitbox, the west canyon with its mountain and west-edge rocks is built true to the
colliders (image 35), the two
crate clusters at the dropship fill their colliders (image 31), and predator vision is brighter,
reaches farther and reads prey as a heat signature, all at 14.1 ms median, measured.

New and done: the hand-built world for Polus (see above), floors at deck height with the planet
underneath and the lava canyon as a real pit, 15 of the 16 sliding doors coupled to the game's
doors, eye height follows the floor (softly, because `GroundAt` is a step function).

Found and fixed while building it in:

- **Polus' doors are triggers.** A door never becomes geometry, so its only trace in the segment
  model was the shadow strip next to it, at Comms almost a full unit away from the door. Three of the
  sixteen built doors did not find their counterpart because of that. Now `MapModel` keeps its own
  list of door positions (`DoorAnchors`), triggers included and the two consoles of an airlock door
  excluded by name, because those stand half a meter off to the side.
- **The area file draws a porch door at the outer end, the game puts its collider into the wall.**
  Both are correct (rule 5 of the prototype), they are just up to a unit apart, so the matching looks
  for proximity rather than overlap and hands out every game door only once.
- **The measurement in the RenderTool measured a single viewpoint**, and the most expensive one on
  the map at that.

Open:

- **Third playtest.** The second one has been played (images 28 to 34, own section above); all seven
  findings are dealt with and reproduced in the RenderTool. New things to check in game: the culling
  mask cover-up (does it really hide everything, and does the world come back on deactivation), the
  visibility cone while walking, the pet billboards and the photographed hats.
- ~~Office's left exit~~: **found and fixed** (second playtest, finding 1): it was the west door of
  the tiled room, whose opening had been measured from the parked door leaf instead of from the hole
  in the collider.
- **The twenty-one rocks on their full sprite box.** The "floating rock" from image 34 turned out to
  be a vanilla sprite above the image (finding 8) and says nothing about them. The search for a
  general ratio remains a failure (72 % against 33 % on the two already trimmed ones), so it stays
  one measurement at a time.
- **Antialiasing.** The filtering is in place, the triangle edges are not: a column edge against the
  night sky is still a staircase. That is the next visible step and the only one that gets by
  without more pixels.
- **The other four maps** are now **switched off** instead of running through the collider path: the
  path renders, but not well enough to be judged by afterwards (own section "Where the view stays
  off on principle"). The code stays, the RenderTool still reaches it via `--colliders`. As soon as
  one of the four is described, exactly one line changes: `PolusAreas.AppliesTo`.
- **The rooms are dark**, and with the round cone darker than before: the old wedge lit a
  floor-length stripe, a cone lights a fraction of that. Two counter-measures were taken: ambient
  from 0.055 to 0.075 and the corona of the cone from 2.3 to 3.2 core angles. Whether that is enough
  is for the next pass to say; the cheap way out (more ambient under a ceiling) is still on the
  table.
- **From very close up the torch still burns out**, but no longer across an area. Halving the
  amplitude at twice the curve radius turned the white disc into a white patch with a soft edge; a
  bright wall two meters in front of your face now keeps its grout lines. Within a meter it is still
  white, and that is as it should be.
- ~~Host-synced settings via TOR options~~: **done** for the 3D mode (`NightfallOptions`, option
  1700, General tab). The look settings stay local, and rightly so: resolution and mouse sensitivity
  are the machine's business, not the lobby's.
- Sound, vents as enterable objects.
- **The world relay has not been measured in game yet.** The root scan runs four times a second, the
  photographs share a budget of one per 0.12 s with `AvatarCapture`, capped at 64 roots and 220
  billboards. All of that is estimated and none of it measured; a round with Saboteur, Collector and
  Poltergeist at the same time is the test that counts.
- **The three name tag abilities** (Maniac's bomb carrier, Silencer's silence marking, Witness' red
  killer name) are missing outside the meeting, because there are no names above heads in first
  person. The user's decision, see "What stayed open".
- **Security Guard's cameras** hang under the ship instead of at root level and fall through the
  relay filter.
- **The southern rocks from very close up** (`schneemaenner-sued`): the kit is too coarse there. Two
  candidates, both a trade-off, see the section of its own.

### Deliberately left uncertain

- **The height factor 0.66** (`AreaBuilder.V`) is a decision of the prototype, not a measurement.
  Polus' rooms are flat (Comms measures 3.2 by 2.7) and with full wall height you stand in a shaft.
- **Ceilings are invented.** A top-down view contains none.
- **Windows are opaque.** The rasterizer does not blend.
- **The dropship hatch stays closed.** It has no door underneath it in the game, and that is as it
  should be; it goes into the log anyway, because the other reason for such a message would be a
  wrongly built door.
- All open items at the end of `..\Assets\NightfallWeb\README.md` still apply, because the data is
  the same: the three approximated slopes, the heights of the rocks, Electrical's transformer yard.

## License

GPL-3.0-or-later, see `LICENSE`.
