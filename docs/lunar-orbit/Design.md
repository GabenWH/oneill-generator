# Separate lunar orbit merge kit

Requested outcome: the cylinder orbits the Moon, has sunlight and visible spin,
and the ground-effect filter accepts CylinderChunk. Grace will merge later.

The accessible remote revision is c00e98f953b8742296092747c62c71d0720a1357.
Its cylinder sources are older than the pasted ApplyGroundEffect and contain
centrifugal/Coriolis forces in a stationary rotating frame. No current full
source snapshot is available. Do not replace these older sources into her project.

Chosen implementation: additive components, independent of ONeillWorld APIs.
A station-centered rotating-frame sky keeps existing flight physics unchanged.
A separate inertial overview demonstrates the station's real translation and
spin, without moving gameplay colliders. Naively rotating the existing gameplay
world would double-count the frame and break its hard-coded Z assumptions.

Orbit and spin use physical units and double-precision time. Moon radius and
mu, altitude, inclination, phase, cylinder radius/gravity and playback speed
are configurable. 100 km is only an editable preview default, not a chosen
mission altitude or a safe-operating claim. Circular two-body motion only.
The Sun is a fixed distant direction, with a visible disc and lunar eclipse
attenuation. Optional equirectangular texture is a map on a smooth sphere,
not lunar terrain. No weapon implementation or targeting work is included.

Keep the literal ground filter patch separate from the optional water-aware
radius filter. Do not silently broaden the latter or window behavior.

Verification: execute patch application checks and production math via a small
C++ syntax adapter where available; ship Unity Editor integration checks.
No Unity Editor or C# compiler is installed here: native C# compilation,
shader compilation, scene playback and appearance remain unverified.
