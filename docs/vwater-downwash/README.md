# VWater drone downwash test

This is the current adapted flat-water test in Unity 2022.3.62f3 Built-in. It combines compute-driven water normals/foam with visible circular waves, mist and droplets driven by the actual two fan footprints. The cylinder scene and its project settings are unchanged. Rotating-cylinder water remains a separate adaptation.

## Run the current scene

1. Open this repository in Unity, stop Play, and use **Assets > Refresh**. Wait for import and compilation.
2. Open `Assets/GraceVWaterOriginalTestRuns/LargePond_20261103/VWater_Drone_Large_Pond.unity`.
3. In Edit mode the updated 50-wide drone installs automatically. Look for **[Updated drone] Installed** in the Console, then **save the scene**. Its original drone is retained disabled under the same placement. Undo reverses the swap; **Tools > Grace > VWater > Use Updated 50-Wide Drone** retries it. It does not save over unsaved scene changes or operate during Play.
4. Press Play, select Game and wait three seconds. Fans start on at **1.9 m**. Look for two expanding ring centres beneath the fans, white surface response and airborne spray. Use **Show downwash controls** to move the drone, adjust height and compare mist/waves/fans on and off. Right-drag/wheel controls the camera.
5. Existing rings stay where emitted as the drone moves. Fans emit every **0.4 seconds each**, alternating **0.2 seconds apart**. **Wave Interval** in the bridge Inspector adjusts the rate; speed, width and radius remain tunable.

The basin is 24 m across with an 18 m water plane. The supplied profile contains the user's authored curves; they were copied unchanged. Both updated 50- and 200-wide model sources and their FBX exports are in `BlenderWIP/`. This test uses the 50-wide variant, exported separately into `Assets/GraceVWaterOriginalTest/UpdatedDrone50/`. It retains 71 evaluated mesh parts / 7,564 triangles, both actual fan hubs and ten blades. Standard material-factor conversion is used; it is not a Blender pixel-parity claim.

## Author the response

Select the object containing `GraceOriginalVWaterFanBridge`; **Select curve controls** in Game selects it. Enable **Use height curves**. The embedded `DroneHeightResponse` profile exposes six independent curves: mist amount, spray-height setting, wave-height setting, pressure-input magnitude, surface foam and mist turbulence. X is actual fan height above water in metres. Edit coloured curves and tangents directly, or enter numeric values and choose **Add / update keys at this height**. Scrub height to inspect the result; allow old particles/rings to expire between comparisons. **Save profile (keeps edits after Play)** persists the asset. Duplicate the profile first for a separate variation.

At vertical thrust **0.9** and gain **3**, curve outputs are unscaled. There is no second height-amplitude falloff after the curves. The legacy manual settings mode remains available. The spray-height field is an artistic setting rather than a measured particle apex.

**Airborne visibility by viewing angle** is a separate opacity curve: X=0 degrees means side-on along the water; X=90 means directly above/below. Y multiplies mist/droplet opacity. It does not affect surface foam or waves. **Use view-angle fade** toggles comparison, and **Preview view angle** shows the multiplier. **Mist turbulence** separately controls particle noise as a function of height. Opacity/noise changes do not reduce simulated particle count; native performance needs profiling.

## Diagnose the live simulation

Use **Tools > Grace > VWater > Inspect Active Downwash Test** to inspect the active scene, fan positions, compute path, buffer binding and simulation state. Its report and asynchronous GPU readback are written to the Console and generated `Diagnostics/Runtime/`. After two active seconds, **[Downwash running]** reports particle/wave counts and accepted/submitted inputs. **[Downwash GPU]** reports buffer peak height/foam when async readback is supported.

Particles and geometric waves use fan footprints independently of compute acceptance. Visible spray or positive CPU input counts alone do not prove that the compute buffer is running. Compare dispatch/source counters, nonzero GPU samples, the material keyword/buffer binding and the rendered result. The original material also has background motion, so compare fans on/off.

## Available verification

Run from the repository root:

```sh
python3 -m unittest discover -s tests/skimming-core -p 'test_*.py' -v
python3 -m unittest discover -s tests/vwater-skim -p 'test_*.py' -v
bash tests/vwater-skim/run_clock_tests.sh
bash tests/vwater-skim/run_visual_tests.sh
python3 tests/vwater-skim/compile_unity_scripts.py
python3 tests/vwater-skim/gpu_probe.py
python3 tests/vwater-skim/visual_shader_probe.py
```

The C# scripts use this project's Unity compiler/Mono installation; set `UNITY_EDITOR_DIR` when needed. Open the project in Unity once to generate `Library/Bee/artifacts/*/Assembly-CSharp.rsp`. The compiler check enumerates all current Assets sources, including newly added Editor files. `UNITY_REFERENCE_PROJECT` can reuse a matching Unity project's generated references when checking a separate checkout.

Software shader probes use numpy, PyOpenGL, Pillow, Mesa EGL and glslangValidator. They compile the actual HLSL and execute it on private software buffers; outputs go under generated `Diagnostics/`. These are compiler/mathematical checks, not a Unity scene capture or native GTX 1070 validation. Export geometry and current checks are summarized in [VERIFICATION.md](VERIFICATION.md).

Native Unity FBX import, automatic model swap, Undo/Redo, Inspector interaction, rendered appearance, performance and player builds remain unverified here. Preserve normal domain/scene reload and one manager/renderer pair. The original runtime's Editor imports and possible shader stripping mean this is an **Editor Play test**, not a verified shipping build.

VWater's MIT notice is retained in `LICENSE.VWater.txt`. The earlier documents, root `MANIFEST.sha256` and `UPSTREAM-PROVENANCE.json` describe the historical original baseline. The current adaptation deliberately changes the compute/input timing and adds geometric waves and airborne particles. Do not import a second VWater copy with duplicate GUIDs/classes.
