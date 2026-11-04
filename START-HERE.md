# O’Neill cylinder and drone downwash work

Both updated drone variants are in `BlenderWIP/`: `fanbody-cargo-50-updated.blend` and `fanbody-cargo-200-updated.blend`, with corresponding FBX exports.

The current flat-water downwash test is documented in [docs/vwater-downwash/README.md](docs/vwater-downwash/README.md). It includes height-response curves, angle-faded mist and droplets, per-fan circular waves alternating at 0.4 seconds per fan, and the updated 50-wide drone installer.

Open `Assets/GraceVWaterOriginalTestRuns/LargePond_20261103/VWater_Drone_Large_Pond.unity` in Unity 2022.3.62f3 Built-in. Refresh Assets, let import/compilation finish, allow the updated model swap in Edit mode, save the test scene, then Play at 1.9 m. Your authored profile is included. The original drone stays disabled as a backup after the swap.

The cylinder scene, scripts, ProjectSettings and Packages are unchanged. The water test uses a stationary world-Y plane; adapting it to the rotating cylinder remains separate work. Editor Play is the target; native Unity import/rendering/performance and player builds remain unverified.

Earlier original-baseline documents under `docs/vwater-original-unity/` are historical. The current test intentionally modifies VWater compute/input scheduling and adds visible waves/particles. Original MIT attribution is retained in `LICENSE.VWater.txt`.
