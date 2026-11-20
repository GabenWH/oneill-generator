# Cylinder integration implementation plan

**Goal:** Complete the shared chunk hierarchy, remove internal glass faces, give terrain thickness, use three land stripes, replace the drone with the updated model at meter scale, and integrate the existing custom water and shaders.

**Architecture:** `CylinderChunk` owns tessellation, winding, mesh lifetime and optional boundary walls. Land, glass and water subclasses supply surface radii and attributes. The manager owns stripe selection, neighbor visibility and regeneration. Custom VWater compute and downwash functions from the local `vwatertest` project run in unwrapped cylinder coordinates; rendering maps their normals back onto the curved surface.

**Tech stack:** Unity 2022.3.62f3, Built-in rendering, C#, ShaderLab/HLSL.

## Constraints and decisions

- Work in the existing `chunk-refactor` checkout and leave changes reviewable without committing or pushing.
- Exactly three continuous land bands alternating with three glass bands; keep 24 angular chunks by default.
- Keep terrain height continuous at the angular wrap; outer terrain radius must exceed all terrain and glass radii.
- Cull boundary walls only between matching chunk types; keep the cylinder ends closed.
- Preserve existing drone controls, camera, gravity and floating origin, using the updated 50-wide FBX and original meter-scale rig markers.
- Reuse the custom GPU solver, clock, downwash wave functions and authored height response. Add a cylinder adapter rather than rotating a flat pond.
- Validate actual C# compilation against installed Unity libraries; distinguish batch/Play/GPU checks from static verification.

## Tasks

- [x] Add and run failing mesh/stripe regressions in an Editor check entry point.
- [x] Finish `CylinderChunk`, derive land/glass/water chunks, implement neighbor-aware walls, correct winding, continuous terrain and mesh disposal. Verify geometry and regeneration.
- [x] Import updated drone resources, build a prefab with correct axes, unit scale, collider, camera and fan bindings; guard optional legacy thrusters.
- [x] Import custom VWater compute/textures/profile/functions, implement cylinder-coordinate simulation and downwash rendering, and wire explicit materials in the scene.
- [x] Compile runtime and Editor code; run available geometry, import, shader and scene checks. Review the complete change and record validation limits.

## Review focus

Angular wrap and longitudinal neighbors; matching land/water seam subdivisions; triangle winding on all boundaries; regeneration without duplicate objects or mesh leaks; floating-origin-stable water coordinates; disabled GPU support; source assets and material references preserved; drone starts clear of terrain and faces along the surface with radial up.

## Validation record

- Ran Unity 2022.3.62f3 in `/tmp/oneill-chunk-validation`, preserving the already-open project/editor.
- Initial geometry regression failed on the missing glass inheritance, then passed 765 assertions covering shell geometry, walls, stripe count, glass neighbor faces, wrap seams/normals, mesh disposal, regeneration and water coordinates across floating-origin shifts.
- Fresh independent review found reversed drone forward and an opaque canopy. A new regression reproduced the wrong forward axis; corrected model orientation and assigned custom transparent canopy glass. Actual imported bounds: 3.29 × 1.85 × 4.36 m, two hubs, ten blades.
- Native Play/render checks on NVIDIA GTX 1070 / OpenGL verified 192 land + 192 glass + 192 water chunks, all four custom shaders, both actual fan intersections, solver dispatch and GPU readback. Sustained inputs produced finite nonzero displacement and foam; captured cockpit and water previews in `/tmp`.
- Far water clips against the shared terrain height grid and fades screen-depth shoreline foam at long distances to avoid habitat-scale depth quantization artifacts.
- Existing 530 fan-input math assertions passed using the installed .NET 10 runtime with `--roll-forward Major`; their project targets .NET 8.
- Unity metrics SQLite cache emitted nonfatal disk I/O/locking messages; asset import, C# compilation, Play, shader compilation and GPU checks completed independently.
- Player builds and other graphics APIs are not validated.

Final checks: 771 native Unity geometry/asset assertions passed after the camera fixes. Asset GUID scan found no duplicates or unresolved local references. Sustained GPU check dispatched 81 steps and read back displacement 0.001142502 / foam 0.0001424551 (nonzero, finite). Independent review findings are fixed; no review findings deferred.
