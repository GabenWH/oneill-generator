# Lunar Orbit Add-on Implementation Plan

Goal: ship separate mergeable changes without replacing newer cylinder sources.
Architecture: pure orbit math, Unity presentation driver, sky shader, inertial
overview, and isolated ground patches.
Tech stack: Unity 2022.3 Built-in, C#, ShaderLab/HLSL, Python offline checks.
Spec: Design.md.

1. Write checks for orbital period, radius, spin period, angle wrap, eclipse and
invalid configuration; run missing-feature failures before implementation.
2. Implement production math, habitat-frame sky/light driver and inertial demo.
3. Add a saved demo scene and optional Moon texture hookup.
4. Package exact ground filter patch and independent optional radius correction.
5. Run offline suite, request review, document Unity verification limits and
merge/use steps. No GitHub push or overwrite of existing project sources.

Review focus: rotating-frame sign, floating-origin independence, visible-light
and eclipse consistency, component lifecycle restoration, shader availability
in player builds, wrong/stale patch target and unverified window inheritance.
