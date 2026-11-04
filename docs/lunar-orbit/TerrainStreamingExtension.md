# Foundation for a later planet-loading system

This kit does **not** implement a KSP-style streamed planet. The Moon is a
smooth reference sphere in the overview and an analytical sky sphere in the
habitat view. There are no terrain tiles, terrain LOD, local surface colliders,
landing support, vessel reference-frame transitions or loading scheduler.

## Available boundary

`LunarOrbitSky.EvaluatePose()` evaluates the clock/configuration without moving
any gameplay objects. Its public outputs are:

- `StationPositionKm`: the station centre in Moon-centred inertial coordinates.
- `HabitatToInertial`: attitude mapping habitat axes into that inertial frame.
- `simulationSeconds`: shared simulation time.
- `moonRadiusMetres`, orbital parameters and spin settings: explicit units.

These are adequate for a small distant-body presentation. The Vector3 output
is a render-boundary value, NOT a high-precision persistent planetary position.
A future terrain service should retain double-precision body/vessel positions
and convert to camera-relative floats only after subtracting nearby origins.
Do not silently use the rendered sphere's scaled position as authoritative
surface-navigation or physics data.

## Proposed separate layer

1. A body catalog holds radius, rotation, gravity parameters, texture/height-map
   provenance, coordinate convention and terrain data references.
2. A camera-relative body presenter uses a distant sphere at orbital range.
3. A terrain provider requests bounded quadtree/cube-sphere patches around an
   actual nearby observer. Rendering LOD and collision LOD have separate limits.
   Load asynchronously, cap memory/work per frame and retain the old tile until
   its replacement is ready. Use deterministic boundaries to prevent cracks.
4. A frame service owns the authoritative double position and transforms
   velocities as well as positions when switching Moon-inertial, Moon-fixed,
   habitat-rotating and local physics frames.
5. Only after local terrain is ready should the renderer replace the distant
   Moon representation, and only after matching colliders/gravity are ready
   should a craft enter a surface-flight state. A loading failure must not
   remove the existing terrain or drop a craft through a gap.

The current existing `CylinderGravity` remains a rotating-habitat model; it is
not appropriate lunar flight gravity outside the habitat. That transition must
be designed and tested before adding descent or landing. Use real elevation
source data and documented coordinates for terrain; an albedo image is not a
height map. None of the proposed layers above is claimed as implemented.
