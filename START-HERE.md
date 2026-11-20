# O’Neill cylinder integration

Open `Assets/Scenes/SampleScene.unity` in Unity **2022.3.62f3**, Built-in rendering, and press Play. If this scene was already open while the files changed, stop Play and reopen it after imports finish.

The scene uses three continuous land bands alternating with three glass bands. Land, glass and water share `CylinderChunk`; terrain has a solid outer shell and exposed side walls, while adjacent glass tiles omit their internal boundary faces. Regeneration replaces the previous generated world.

`Assets/Prefabs/Drone50Updated.prefab` uses the updated 50-wide model from `BlenderWIP/fanbody-cargo-50-updated.blend`, imported at **3.29 × 1.85 × 4.36 metres** with two actual fan hubs and ten blades. Its cockpit faces flight-forward and has transparent custom canopy glass. The original drone prefab remains available.

Controls: WASD movement, Space/C vertical, mouse pitch/yaw, Q/E roll, Shift boost, Escape release mouse.

Custom VWater uses the existing solver, height response and downwash functions from the local `vwatertest` project. `CylinderWaterSimulation` maps them into a continuous unwrapped cylinder chart and emits fan ripples, foam, mist and droplets. The detail region is **320 m wide**, five times the former width, with dense geometry near the craft and expanding outer rings. A circular fade blends it into the coarse water. Distant water fades out expensive detail and uses a small depth bias at overlapping edges.

Spray inherits actual fan-point velocity, then slows under strong habitat-space air drag. Wave births and GPU forcing sweep between successive positions to keep wakes continuous at speed. The saved `vwatertest` profile is unchanged; the original height curves, particle sampling, lifetimes and emission rates drive both mist and droplets. Mist is faint in shade and gains visibility in direct sunlight. Foam has a **0.3-second half-life**; old waves no longer continuously regenerate it. Sources, MIT license and provenance for the water remain in this repository; the old flat-water handoff documentation under `docs/vwater-original-unity` is historical.

Land, craft, glass and water share cylinder sunlight occlusion. Transparent water, spray and canopy sample directional shadows at their world positions, and the canopy does not cast an opaque shadow. Broad housing shadows include the thick terrain walls and faceted outer shell independently of camera shadow distance; local terrain and craft shadows use Unity's shadow atlas. This prevents a camera-following dark circle where realtime wall shadows previously faded into a thinner housing approximation.

`CylinderBuoyancy` is attached to the craft at runtime. Twelve samples of the box hull produce partial immersion, restoring torque and water drag under radial gravity. With the fans off, the craft settles roughly one-third submerged. Tune `relativeDensity`, `linearWaterDrag` and `angularWaterDrag` on the runtime component; the default density ratio is 0.35. This approximates hull volume using its box collider.

The scene directly references the prepared assets. The explicit `Tools > O’Neill > Prepare Updated Drone and Materials` command rebuilds the prepared drone/material assets if needed; it does not edit an open scene.

Validation used a temporary project copy: native Unity C# compilation, chunk/neighbor/coordinate assertions, actual FBX import, moving and floating Play tests, custom shader rendering on GTX 1070/OpenGL, and GPU readback of fan-driven displacement and foam. Player builds and other graphics APIs were not tested. Details: `docs/superpowers/plans/2026-11-03-cylinder-integration.md` and `docs/superpowers/plans/2026-11-04-water-surface-physics.md`.
