# Water motion and lighting correction

**Goal:** Spray and visible wave wakes respond to actual fan-point velocity, remain continuous at flight speed, and use consistent water lighting in and out of direct light.

**Architecture:** Keep habitat-local particle simulation and cylindrical water coordinates. Inherit rigidbody point velocity at birth, sample wave emission by distance as well as time, and sweep GPU forcing between successive simulation positions. Advect newly generated rings with a damped inherited surface velocity. Use the same shadow reception and irradiance for near water, far water, foam and mist.

- [x] Reproduce missing spray inheritance using the actual drone moving at 24 m/s in native Unity Play mode.
- [x] Implement point-velocity inheritance, continuous wave emission and swept GPU inputs. Check moving and stationary cases and floating-origin stability.
- [x] Unify shadow reception and light response; inspect directly lit and unlit previews and check for shader errors.
- [x] Run native moving-effects, geometry and GPU checks in the temporary project. Preserve the open editor and existing assets.

Expected behavior: emitted spray carries the drone's surface-direction velocity; newly emitted waves inherit a damped water-flow velocity and form a trail. Existing water ripples continue propagating over the habitat rather than being rigidly attached to the drone. Distant shoreline depth bias remains active.

## Evidence

- Native moving-drone regression reproduced spray velocity −0.50 m/s while the rigidbody moved at 24 m/s; corrected version passes surface-velocity inheritance and continuous wake-spacing assertions for both fans.
- Wake sampler compares emission positions/times at 10 and 60 FPS, retains stationary emissions and resets across missed water/teleports. Full native geometry/coordinate/asset suite: 788 assertions passed.
- Moving GPU regression exposed an additional resource-access fault: the compute shader declared source/history textures writable even though they are read-only inputs and also participate in scroll/copy operations. Changing those to `Texture2D`/`Load` restored moving GPU output; only the destination remains `RWTexture2D`.
- Moving GPU readback after 92 dispatches: displacement 0.03427124 and foam 0.005496979, both finite and nonzero. Test uses synchronized AsyncGPUReadback. Temporary binding probes were removed.
- Independent review found the moving GPU failure, now covered and fixed. No findings deferred.
- Following the user's clarification, mist opacity is multiplied by 0.025 in shade and 0.35 in direct light, with shared light/shadow response for spray and foam. Captured lit/unlit GPU previews show faint shade mist.
- Validation uses Unity 2022.3.62f3 / GTX 1070 / OpenGL in the temporary project; player builds and other graphics APIs were not tested.
