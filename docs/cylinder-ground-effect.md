# Cylinder drone ground effect

In the native Unity flight scene, the existing drone now measures clearance by
casting radially outward. The nearest physical land, glass-window or water
chunk gets the same tunable lift curve. Terrain behind a water collider does
not win the query, and there's no global radius cutoff that would reject dry
terrain or glass. No collider in range means no force. This is a gameplay lift
model, not a water downwash or buoyancy simulation.

## Check it in Unity 2022.3.62f3

1. After import, run **Tools → O'Neill → Test Drone Ground Effect** in Edit
   mode. It generates land, glass, and a water-over-land fixture in an additive
   scratch scene, checks the real colliders at 3 m clearance and verifies no
   hit in empty space. It closes that scene without writing an asset.
2. Open `Assets/Scenes/SampleScene.unity`. On **Bootstrap**, enable **Show
   Ground Effect** and enter Play. Approach a side window or land; the HUD
   reports the nearest eligible collider, clearance, and acceleration.
3. To add an optional physics-only water patch, turn on **Include Ground
   Effect Test Water** on **Bootstrap** before Play. Assign an obviously tinted
   material to **Ground Effect Test Water Material** or it uses the glass
   material. The patch is just off the +X meridian at positive Y and Z,
   252 m inward from nominal cylinder radius (about X=3748, Y=46, Z=62
   at its center with the default world). Approach from the inner side.
   It has a solid test collider and no waves, spray, or VWater rendering.

The end caps have no collider in this version; the glass case tests the
collidable side windows. The opt-in water fixture does not replace the
existing cylinder terrain or import the separate VWater package.
