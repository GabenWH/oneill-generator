# O'Neill lunar orbit merge kit

**Separate feature branch. Main and all existing project files are unchanged.**

The current full cylinder source is not available here. The latest accessible
remote is `GabenWH/oneill-generator` branch
`codex/vwater-downwash-drone-updates`, commit
`c00e98f953b8742296092747c62c71d0720a1357` (2026-11-04).
Its cylinder code still comes from the older `d3a4599` baseline and lacks the
pasted ground-effect function and `CylinderWaterChunk`. This package does NOT
replace your current `DroneController`, `ONeillWorld`, `WorldBootstrap`, scenes,
water code or physics.

## Try the separate overview

1. Check out this feature branch. Its `Assets/GraceLunarOrbit` folder is already
   installed. For a manual merge, copy only that folder and its meta; do not
   overwrite a folder of the same name without comparing first. The additive
   patch in `docs/lunar-orbit/Patches` is an alternative, not an extra step.
2. Open `Assets/GraceLunarOrbit/Scenes/LunarOrbitOverview.unity` in Unity 2022.3
   Built-in rendering and press Play. The saved scene creates its reference
   objects at runtime; it does not need your cylinder assets or a setup menu.
3. Right-drag to orbit the camera, scroll to zoom, Space to pause, R to reset.
   The orange stripe makes spin easy to see. The 1x/10x/60x controls accelerate
   both orbit AND spin together. The 10 km / 100 km buttons offer illustrative
   altitudes; neither is a terrain-clearance guarantee.

This is a **smooth gray fallback Moon and a simplified station marker**, not
photographic lunar terrain or your current station mesh. No NASA asset bytes
are bundled. One overview scene unit is one kilometre. The station marker is
4 km in radius and 32 km long by default, with a **configurable 100 km centre
altitude** chosen only to make a useful preview. Altitude is not decided for
your project. Never treat a very low centre altitude as safe clearance for a
large station, lunar relief or orbital perturbations.

The real-time circular-orbit period at the 100 km default is about 117.8 minutes;
at the illustrative 10 km option it is about 109.2 minutes.
The positive-Z 1g spin at 4 km radius has a period of about 126.9 seconds.
The overview uses a circular two-body orbit and fixed inertial spin axis,
not a full lunar gravity field, N-body model or ephemeris. Runtime changes to
station radius/length require restarting the overview to rebuild the marker.

## Add orbit and sunlight to the actual habitat later

Your accessible cylinder code already simulates flight in a **co-rotating
frame**, with centrifugal and Coriolis accelerations. Do NOT simply rotate
its colliders/root as well: that double-counts rotation and breaks its
world-Z assumptions. `LunarOrbitSky` keeps the gameplay world stationary and
transforms the Moon, sunlight and stars into that frame. The inertial overview
shows the equivalent moving/spinning station from outside.

Add `GraceLunarOrbit.LunarOrbitSky` to a separate object and assign:

- **Habitat Origin:** your actual `ONeillWorld` transform, at the cylinder centre.
- **Habitat Camera:** the active drone/player camera.
- **Sunlight:** the existing directional Sun created by your bootstrap. This
  avoids a duplicate light. If omitted, the component creates its own and warns.
- **Sky Shader:** `Shaders/LunarOrbitSky.shader`. Keep the serialized reference
  so a player build includes the shader.
- **Habitat Radius Metres / Surface Gravity:** match your current physics.
- **Altitude Metres**, inclination, phase, Sun direction and playback speed:
  choose your scene settings. Keep speed 1 for real-time behavior.
- **Moon Albedo:** optionally assign the NASA equirectangular map below.

If your bootstrap creates the world at runtime, merge the small setup example
in `docs/lunar-orbit/BootstrapExample.txt` after it creates the world, camera
and Sun. It is an example against the inspected baseline, not a replacement
bootstrap. This avoids assigning nonexistent scene references by hand.

The optional **Enable Moon Fill Light** creates a dim, shadowless directional
fill from the Moon. Start with **Moon Fill Intensity = 0.04** and compare off/on;
its phase/angular-size fade is an artistic approximation, NOT calculated lunar
irradiance or global illumination. It is off by default in gameplay and on in
the separate overview. It adds a light/rendering cost and can reach places that
a real occluded Moon could not; tune it in your actual scene.

The component only owns the selected camera's sky clear mode, the global
skybox and selected Sun's orientation/intensity while enabled. It restores
those on disable. It does not move the world or player, add forces, change
ambient settings, or modify the water simulation. Camera-relative sky geometry
avoids far-clip limits and rebasing jumps. Only one sky owner is supported.
Other cameras, third-party sky controllers and simultaneous scenes need explicit
integration. Existing ambient light may keep an eclipse from looking completely
dark; this component deliberately leaves your ambient settings alone. The optional fill is separate from those ambient settings; it is not a
photometric reflected-moonlight simulation.

Sunlight direction changes with spin; Moon occultation dims the directional
light. The Sun is modeled as a fixed distant direction with a 0.533-degree disc.
The orbital period is not the time spent in sunlight or shadow: eclipse duration
depends on orbital plane, phase, altitude and Sun direction. Window lighting
also changes with the much faster cylinder spin.
No seasons, Earth, terrain shadows, lunar gravity gradient, real lunar axis
orientation or solar-distance changes are modeled. The Moon rotation is an
approximate 27.32-day sidereal rotation about model Z.

## Ground-effect change

`docs/lunar-orbit/Patches/01-cylinder-chunk-filter.patch` is the exact requested one-line swap:
`GetComponent<LandChunk>()` becomes `GetComponent<CylinderChunk>()`.
It targets `Assets/Scripts/DroneController.cs`; verify your actual file path.
Run `git apply --check` before applying, or use the included full-function
`docs/lunar-orbit/Patches/ApplyGroundEffect.requested.txt` to merge by hand.

**The one-line change alone may still exclude water.** Your pasted function
then skips every hit whose radial distance is `>= world.WaterRadius`. Water
colliders on that radius are skipped too. To retain the submerged-LAND rule
while allowing water, the independent
`02-OPTIONAL-land-only-radius-exclusion.patch` limits that check to LandChunk.
Apply it only after patch 01, and only after verifying your water simulation
is not already supplying the same ground-effect force. It is not applied by
the add-on or included in the literal requested patch.

`CylinderWaterChunk : CylinderChunk` passes the new type test. A collider on a
child without that component does not: the requested patch deliberately keeps
`GetComponent`, not a broader parent search. The inspected old window class
inherits `MonoBehaviour`, so it does not pass. If your newer window class
inherits `CylinderChunk`, it WILL pass; decide whether to exclude it explicitly.
No unseen window/water behavior has been guessed or changed.

## Moon model / texture

A Unity sphere with a Moon map is enough for an orbital backdrop. For a higher
quality smooth model, NASA offers a separate official GLB; importing GLB into
Unity normally requires a glTF importer, so this package does not add one.

- NASA Moon 3D model and credit information: https://svs.gsfc.nasa.gov/14959/
- Small GLB: https://svs.gsfc.nasa.gov/vis/a010000/a014900/a014959/moon_small.glb
- NASA CGI Moon Kit and texture context: https://svs.gsfc.nasa.gov/4720/
- 4K albedo TIFF: https://svs.gsfc.nasa.gov/vis/a000000/a004700/a004720/lroc_color_poles_4k.tif

Download the map yourself, drag it into Unity as a **Default 2D texture**, use
sRGB color and Repeat wrap, then assign it to **Moon Albedo** on either component.
The sky uses longitude/latitude coordinates with north at the top. Use its
longitude-offset field if you want a particular hemisphere facing the camera.
The overview's standard sphere UVs are a simple visual reference; do not use
this preview to locate a lunar landing site or surface route.

Suggested credit: **NASA's Scientific Visualization Studio; lunar imagery from
NASA/GSFC/Arizona State University.** Review the asset page's credit and NASA's
media-use guidelines for your intended release:
https://www.nasa.gov/nasa-brand-center/images-and-media/
NASA imagery is generally public domain unless separately credited, but logos,
endorsement and third-party items have separate restrictions. This package
contains no NASA texture/model data and does not assign it a new license.

## Future planet loading

This pass is only a distant-body/orbit foundation, not a KSP-style terrain
streamer. `docs/lunar-orbit/TerrainStreamingExtension.md` names the public pose
boundary and outlines later LOD, collision and reference-frame work. No streamed
terrain, landing or vessel-frame switching is implemented.

## Checks and known limits

Run `python3 -m unittest discover -s tests/lunar-orbit -v` from the repository root.
Requires Python and g++. The math test compiles a small C++ syntax adaptation
of the **production C# math bodies**; it checks 1,020 numerical assertions and
six invalid-input cases. Patch tests actually apply both patches to the pasted
fixture and confirm a wrong source is rejected. Asset checks verify GUID and
scene-reference integrity.

**Unity Editor and a native C# compiler were unavailable here.** This is NOT
proof of C#/ShaderLab compilation, scene import, visual quality, Play behavior
or player-build compatibility. No Unity screenshot or performance result is
claimed. Independent source review found and fixed lifecycle, finite-input and
shader-inclusion problems.

After import, run **Tools → Grace → Lunar Orbit → Run math and frame checks**.
Then test the overview and your separately integrated habitat:

- At 1x the station marker rotates about Z while its centre follows the Moon.
- Inside the habitat, stationary terrain stays fixed; Moon and stars drift with
  inverse spin. Flight should remain unchanged.
- Force a floating-origin shift and check the Moon/Sun do not jump.
- Enter lunar shadow: sunlight fades. Exit: it returns.
- Disable/re-enable the sky, including twice in one frame: restore the old sky,
  camera clear mode and Sun; no duplicate/orphan light.
- Check a player build and Console for both shaders, missing references and errors.
- Test ground effect over dry land, water, submerged terrain and windows separately.
