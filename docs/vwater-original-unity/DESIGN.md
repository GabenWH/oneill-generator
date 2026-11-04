# Original Unity VWater baseline — no port

The latest user instruction explicitly selects original, unmodified VWater in
a new Unity test scene. This replaces the earlier adaptation/orbit scope.

Use Unity 2022.3.62f3 Built-in. Vendor pinned source, prefabs, materials, textures
and metadata byte-exact with MIT notice. Instantiate the upstream manager/water
prefabs into a NEW, uniquely saved scene after save/cancel; clone its default
material without retuning. Flat stationary world-Y pond only. Original shader,
compute and runtime scripts remain unchanged. Camera-only orbit is optional.

Own C# bridge submits sustained actual fan-ray footprints through upstream
AddFlow from LateUpdate. No replacement solver, private-buffer reset, physical
downwash/buoyancy or underwater claim. Preserve all previous projects/ZIPs and
unknown user scenes/settings. No integration/push.

Unity cannot import GLB natively: use an authored Blender conversion of the exact
approved 50-WIDE drone, preserve geometry/hierarchy and add non-rendered axis
markers. Validate FBX roundtrip numerically; Unity importer validation remains
rig-side. Builder validates markers and actual two-hub/ten-blade binding.

The complete upstream tree DOES contain original textures. Earlier text-only
extraction omitted binaries and incorrectly called them absent. This package
corrects that explicitly and contains actual Default.mat dependencies.

Unity Editor/C#/shader/Play/GPU stages cannot run on this executor. Own pure
math, source/provenance, dependency and Blender conversion checks are distinct.
