# Harbor Beach / 1976
### A small, first-person waterfront prototype for Unity 2022 (Built-in Render Pipeline)

A brick-and-timber market square leads to a tram stop, a timber promenade and a sloping beach. Across the water and behind the allotments, farmland rises up the inside of the cylinder. The local ground is in meters; the distant curvature is compressed and scenic, not the main project's full-size cylinder.

## Open it

1. Check out this branch and open the project in **Unity 2022.3.62f3**. Wait for script and FBX import. For a scratch project on Unity 2022.3 Built-in, copy `Assets/BeachTownPrototype` and `Assets/BeachTownPrototype.meta` into the new project.
2. Pick **Tools → O’Neill → Create Beach Town Prototype** (the actual editor label uses a straight apostrophe). Resolve any save prompt for previously open scenes. The tool creates and opens a new `HarborBeach.unity` scene under `Assets/BeachTownPrototype`, without overwriting files. The town is generated when the scene runs, so the edit view only has the prototype root.
3. Press **Play**, then click in Game view. **WASD / arrows** walk, **mouse** looks, **Shift** jogs, **E** boards/exits at a stop, **R** returns to the square, **Esc** releases the mouse. The blank square brackets in the HUD tick off destinations as you reach them.

Start by walking through the market square toward the rail. The raised Harbor Beach platform is on the north side of the track, just east of the pedestrian crossing. Approach while the HUD reads **Docked at Harbor Beach** and press **E**. The tram dwells eight seconds per stop and takes 19 seconds between stops. Exit at Field Overlook and head north to the overlook deck. You can also walk there directly. The beach is beyond the promenade, down the sand slope; there is no swimming. Step out of bounds and the prototype sends you back to the square.

## Scope

- Walkable exterior layout with blocky timber cottages, painted storefronts, a small market, allotment rows and a beach. No building interiors, residents, traffic or day–night simulation.
- The original **T01 Harbor Local** exterior FBX ships with this package. If Unity provides usable render bounds, the prototype fits the model to its nominal 11.29 m length and aligns the bottom to the rails; if the model is missing or has unexpected proportions it uses a geometric substitute. Boarding uses an offset ride-along camera, not an interior. The tram is a scripted shuttle, not a working wheel/door/dynamics implementation.
- This is an isolated Play Mode scene generator, not a standalone player build or a production asset validation. Existing scenes, project settings, packages and simulation code are unchanged. Either Unity input backend can be used; an existing project with customized legacy axes should retain the standard `Mouse X` / `Mouse Y` axes for mouse look.

## Verification

- The prototype package passed six static checks (scene safety hooks, input and objective wiring, rail constants, valid FBX header, and no project-setting overrides).
- Blender 4.3.2 reimported the original FBX as 4 meshes, 4,496 triangles, with world extents **11.294 × 2.652 × 3.980 m** in Blender.
- Unity was unavailable here, so Unity compilation, visuals, import orientation/materials, and Play Mode behavior have **not been tested**. First Unity smoke test: generate the scene; look around and walk to all three spots; board when docked, ride and exit; verify the tram uses the FBX rather than its fallback and its wheels sit on both rails; then verify **R** and **Esc**.
