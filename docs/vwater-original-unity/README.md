# ORIGINAL VWater Unity test — no port

## Three steps on the real rig
1. Open this branch in a **separate checkout / disposable Unity 2022.3.62f3
   Built-in project**. Do not merge into the current cylinder scene or import
   both this vendor copy and another VWater installation (duplicate GUID/classes).
   Let Unity finish importing/compiling. No ProjectSettings were changed here.
2. Select **Tools → Grace → VWater → Create ORIGINAL Flat Drone Baseline**.
   Confirm and answer Unity's save/cancel prompt. It creates only a NEW scene
   under `Assets/GraceVWaterOriginalTestRuns/Run_*/`; original scenes are not
   overwritten. Original material defaults/textures are cloned without retuning.
3. Press **Play**. Inspect stationary original water first, then click **Fans ON
   → Move drone → Raise fans**. Optional orbit/drag/wheel moves the **camera**,
   never the water. Read Console; send the first error/screenshot if it fails.

For the immediate untouched upstream sample instead, open
`Assets/GraceVWaterOriginalVendor/VWater/Examples/VWater Example.unity` and Play.
The previously delivered baseline ZIP uses its original path
`Assets/VWater/Examples/VWater Example.unity`. Those are the SAME original scene
bytes, not two installations to import together. The add-on-only ZIP is for
people who already imported that original baseline; it excludes the vendor copy.

## Exactly what is original
Pinned zulubo/VWater **fb156e302f213a507956de0db1a442c17e22d6b5**, MIT copyright
2024 Zach Tsiakalis-Brown. Full original asset tree, source, prefabs, materials,
textures and import metadata are byte/Git-blob verified. No VWater shader,
include, compute, runtime source or original prefab/material was rewritten.
The builder verifies original hashes before using the installed GUIDs; differing
assets cause an explicit stop, not an overwrite. Reconcile such differences.

It uses the real **water_NRM_smooth.png**, **foam.png** and **flowNoise.tga**.
**Correction:** earlier text-only snapshots omitted binaries and incorrectly
said the upstream repository lacked normal/foam textures. The complete pinned
repository contains them. Earlier adapter/Godot ZIPs remain separate preserved
artifacts; their asset-availability claim is superseded by this correction.

Own code only creates the new scene, converts the approved drone to FBX, moves
it and submits two sustained fan footprints through original **LateUpdate →
AddFlow**. There is no substitute solver, private-buffer reset, new shader,
vertex displacement, rotating-water/cylinder adaptation or CFD. Input falloff is
an illustrative adapter, not a validated aerodynamic model. Calls/events and
`DYNAMICS_ENABLED` are wiring diagnostics, **not evidence of visible GPU ripples**.

## Drone import
Unity does not natively import GLB. The exact approved **50-WIDE** source
(`73813c1a...4de7`) was converted using installed Blender 4.3.2, without rendering.
Roundtrip checked **36 mesh objects / 3514 triangles**, bounds error at most
**3.58e-7 m**; actual two hubs/ten blades retained. Six non-rendered markers let
the builder check actual Unity import axes/units/hub positions. No proxy fallback.
Unity Standard material-factor conversion is **not GLTF pixel parity**; original
double-sided/material-extension behavior is not fully preserved. No drone mesh
reshaping or water shader workaround is used to conceal that difference.

## Honest verification / limits
Actually run: exact upstream hash checks; own **530 fan-input math assertions**;
authored C#9 syntax parsing; Blender import/export/reimport geometry checks;
dependency/metadata/source safety checks and independent static review.
**Unity API/type compilation, native FBX import, shader compilation, Editor scene
creation, Play, GPU ripples and pixels are UNRUN here.** Unity is unavailable on
this executor. Source/syntax/math checks are not Unity compilation or Play proof.

Original behavior is deliberately retained: world-XZ/Y assumptions, camera-
following field, source-copy/reset timing, unchecked GPU borders, uncleared and
unreleased static RTs, original GrabPass/depth/lighting quirks. No undisclosed
fixes. Use normal domain/scene reload and one original manager pair. Original
runtime files contain `UnityEditor.EditorTools` imports and runtime shader
features can be stripped: **Editor Play is not a player-build validation**.
Underlying upstream examples also retain some absent optional boat/particle
references; the new baseline does not instantiate boats/particles/underwater.
The original example uses Unity UGUI package UI; original manifest declares it.

Original project was authored in Unity 2022.3.25f1, **Linear** color space. This
package does not silently change the target project's settings; compare original
colors in a disposable Linear project. No original cylinder scripts/scenes,
paused boundary draft, previous ZIPs, main/current branch or user local files
were modified. Water rotation remains a separate unsupported adaptation task.
