# Verification scope

The 50-wide Blender source exports to 71 evaluated mesh parts / 7,564 triangles. Export/reimport retained per-part triangle counts and world bounds within 3.58e-7 m; two real fan hubs, ten blades, unit/axis markers and parented fan-up markers were checked. Its source SHA-256 is recorded in the asset manifest. All 14 Standard materials and the packed vent texture are included. Both 50- and 200-wide updated source/FBX files match the original local model files.

Checked on the combined cylinder-and-water publication tree:

- All 31 runtime C# and 5 Editor C# source files compiled with Unity 2022.3.62f3 references. A deliberate temporary error proved that a newly added Editor file is checked; the probe was then removed and compilation passed again.
- Original 530 fan-input mathematical assertions passed.
- C# simulation clock/projection and visual/curve tests passed, including independent fan resets and 120-second float-clock alternation at 30/60/144 FPS.
- All 22 Python source/model tests passed.
- Ten complete shader stages compiled with Unity includes/OpenGL definitions; actual HLSL wave propagation, expiry, wisp shape, view-angle and LUT tests passed in software OpenGL.
- The actual compute kernel passed software checks for zero gain, gain/height response, directional wake, fade and finite safety bounds.
- Project asset GUIDs are unique; current test asset references resolve. Authored profile SHA-256: `e8dce55032e423d8b47468fb6be58134993a6045509833ad3ec7eb3fe85450cc`.
- Cylinder scripts/scenes/prefabs, ProjectSettings and Packages are unchanged. No local diagnostic logs, backup archives, Library files or Python caches are published.

Full-tree whitespace checking reports existing Unity/vendor serialized trailing whitespace. Those asset/profile/vendor bytes are preserved; the authored code/test/document diff check passes.

Native Unity FBX import, automatic model swap, Undo/Redo, rendered appearance, GTX 1070 performance and player builds remain unverified. Use the live Editor steps in README.md. Compiler and mathematical checks do not establish the native rendered result. Historical upstream example scenes may retain missing optional boat/particle references; the supplied large pond test does not instantiate them.

Local diagnostics/backups are not required to run the test and are excluded. Runtime inspection remains available through Tools > Grace > VWater > Inspect Active Downwash Test. Software probes generate fresh reports under ignored `Diagnostics/`.
