# Water surface, shadows, particles and craft physics

**Goal:** Enlarge local water effects by five times without a visible patch; remove incorrect camera-relative shadow halos; restore vwatertest curve behavior; shorten foam; add strong spray drag and craft buoyancy.

**Implementation:** 320 m simulation region with 2048 buffers, a dense 64 m core and expanding low-cost mesh rings. Shared triangulated terrain texture and circular complementary coverage replace the hard square. Transparent receivers sample the directional shadow atlas in world space. Analytic cylinder/window/cap transmission gives the broad housing consistent sunlight occlusion regardless of shadow draw distance; opaque terrain and craft use the same direct-light treatment. Transparent panes remain transmissive.

**Particles:** Preserve the exact saved vwatertest profile and original 60/s mist + 180/s droplet rates multiplied by authored height curves and normalized thrust/gain. Preserve original sampling, lifetimes, view fade and noise. Substep emission budgets across slow frames. Apply exponential habitat-space air resistance at rate 5/s and radial gravity to droplets; compensate launch lift for the added drag so height settings remain meaningful. Cache data refreshes when terrain/resolution changes. Foam halves in 0.3 s and old waves no longer recreate it.

**Buoyancy:** Twelve box-hull volume samples, relative density 0.35, radial centrifugal gravity, restoring torque, water/terrain/window masks, exponential submerged water drag. Bootstrap attaches it to the actual craft.

## Checks

- Native missing-buoyancy and old 64 m region regressions failed before fixes.
- Saved profile hash matches vwatertest exactly (e8dce55032e423d8b47468fb6be58134993a6045509833ad3ec7eb3fe85450cc).
- Curve integration at source height: expected mist1439.55/droplets4318.65; actual1438/4318.
- Foam GPU fixture with existing waves retains about0.097 after one second, matching the configured half-life.
- Inactive patch vs surrounding water image comparison: mean RGB error0.0017; large-error pixels0.81%, without a hard replacement square.
- Moving craft24m/s: new spray about22.1m/s after birth aging; older mist about0.84m/s; continuous waves and GPU displacement/foam pass.
- Native geometry, terrain-cache regeneration, shadow and buoyancy suite passes 37,656 assertions, including 56 buoyancy assertions. The separate source-math harness passes 530 assertions.
- Fans-off floating Play check settles at submerged fraction 0.3361 and speed 0.0723 m/s after eight simulated seconds.
- Native checks run in temporary Unity2022.3.62f3 project copy, GTX1070/OpenGL. The source project/editor can stay closed. Metrics SQLite disk-I/O warning persists even after closing Unity; tests continue.

## Remaining validation

- [x] Final native geometry, cache regeneration, shadow and buoyancy suite.
- [x] Craft settles afloat under radial gravity with fans off.
- [x] Final shader/render check with updated canopy shadow settings: all five shaders compile; source emission counts, foam decay, patch blend and GPU displacement/foam checks pass. Close views captured at 75/150/300 m shadow distances. Unity exits successfully.
- [x] Source assets byte-match the validation copy; no missing metadata, duplicate GUIDs or unresolved asset references. `git diff --check` passes. Limits recorded below.

**Limits:** Housing shadows include exposed terrain walls, outer polygon faces and land end walls. Interior terrain relief and craft detail use the normal finite-range shadow atlas; glass transmission uses the ideal cylinder/cap incidence. Buoyancy approximates displaced volume using the existing box hull. Player builds and other graphics APIs are not validated.

Final logs: `/tmp/oneill-all-physics-final.log`, `/tmp/oneill-drag-motion-complete.log`, `/tmp/oneill-floating-final.log`, `/tmp/oneill-appearance-final.log`. Images: `/tmp/oneill-floating-craft.png`, `/tmp/oneill-close-shadow-{75,150,300}.png`, `/tmp/oneill-patch-{coarse,blended}.png`.

## Remaining terrain circle: Land_19_4

The reported camera position is `(368.080658,-105.516068,-650.618042)` and rotation is `(0.183074355,-0.793751895,0.249821991,0.523479521)`. The accumulated floating-origin offset was not supplied; the render fixture keeps this exact camera transform and places the named chunk 100 m ahead of it.

The original housing approximation tested a zero-thickness radius-4000 cylinder. Rays through the glass could then hit the adjoining terrain wall, which extends out to radius4300. Unity's real shadow atlas saw that distant wall; the housing approximation did not. The mismatch revealed a dark circle at the camera's shadow-distance limit. Bias, cascade count/projection and Standard material substitutions did not remove it. Physical mesh ray tests identified the wall on Land_0_2 about6.5 km from the receiving terrain.

The correction tests angular walls against cached terrain grid heights, the actual faceted outer shell, and opaque land end walls. It is shared by land, craft, water, spray and glass. Native terrain shadow casting/reception remain enabled. The height cache rebuilds with terrain amplitude, length or resolution changes; sun rotation and origin translation update the published globals.

- The missing-wall native regression fails before the correction (`/tmp/oneill-thick-wall-red.log`).
- The GPU regression fails when the wall correction is omitted (`/tmp/oneill-terrain-gpu-red.log`).
- Review caught outer-face self-occlusion against a smooth enclosing cylinder; an on-mesh outward-ray regression failed (`/tmp/oneill-outer-face-red.log`). Outer intersections now refine onto polygon planes. A separate 10,000-ray comparison against all288 exact polygon planes found no exit-distance error.
- Final native suite passes 37,659 assertions plus the shadow and 56-assertion buoyancy checks (`/tmp/oneill-shadow-native-final.log`).
- Final terrain GPU regression passes for light yaw -30/-26/-34 degrees at 75/150/300 m shadow distances; mean image difference stays below0.00339, and no more than0.00245% of pixels differ by more than12/255. The origin-shift comparison isolates housing shading from Unity's cascade texel snapping and has mean error0.00000104 (`/tmp/oneill-shadow-gpu-final.log`).
- The integrated Play check passes all five shaders, source curve rates, foam decay, patch blending, and GPU displacement/foam (`/tmp/oneill-shadow-water-final.log`). Final screenshots include `/tmp/oneill-terrain-shadow--30-150.png` and the matching other light/distance cases. Validation uses Unity2022.3.62f3/OpenGL in a separate project copy.
- Focused code review found no remaining significant issues after the exterior-face correction. Source/validation asset bytes match, metadata GUID checks and `git diff --check` pass. The user's open project was not used for batch runs.
