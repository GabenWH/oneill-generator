# Original Unity VWater + actual-drone test

Choose ONE original vendor installation; do not import duplicate original VWater GUID/classes.

Fresh project/full source ZIP: copy its Assets into a NEW disposable Unity2022.3.62f3 3D Built-in project.
Already imported Grace-Unity-Original-VWater-Baseline-NOW.zip: use ONLY the DRONE ADD-ON ZIP (no duplicate vendor).

Then: Tools > Grace > VWater > Create ORIGINAL Flat Drone Baseline. Confirm/save or cancel. A NEW uniquely named scene is generated. Press Play, inspect water stationary, then Fans ON > Move > Raise. CAMERA orbit is optional; water does NOT rotate.

Original VWater shaders/compute/runtime/prefabs/textures unchanged. Actual Default smooth normal/foam assets included by the baseline/full vendor. The new bridge only submits sustained AddFlow.

Unity API compilation/FBX import/shader/scene/Play/GPU are UNRUN on author executor. Own530 math assertions, C#9 syntax-only checks, original hashes and Blender roundtrip checks passed; those are NOT a Unity pass. See docs/vwater-original-unity/README.md.

Current local real-ancestry commit: 34a7c6efc1c18feeca455b8ea40fce5ed264f3d5 based on d3a4599210865decf1769bbee7c75f940b420a03. Remote public push was blocked pending explicit approval of the expanded public vendor/drone payload. This archive is a private source handoff, not a claim a remote branch exists.
