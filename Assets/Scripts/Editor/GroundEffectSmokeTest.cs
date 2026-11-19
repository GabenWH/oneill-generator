using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class GroundEffectSmokeTest
{
    [MenuItem("Tools/O'Neill/Test Drone Ground Effect")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running the ground-effect test.");

        Scene previous = SceneManager.GetActiveScene();
        Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scratch);
        try
        {
            var world = new GameObject("Test cylinder").AddComponent<ONeillWorld>();
            world.transform.position = new Vector3(20f, -40f, 100000f);
            world.radius = 1000f;
            world.length = 240f;

            var land = Add<LandChunk>(world, "Dry ground");
            land.Generate(world, 0, 0, 8, 1, 12, 4);
            var glass = Add<CylinderWindowChunk>(world, "Glass");
            glass.Generate(world, 1, 0, 8, 1, 12, 4);
            var wetLand = Add<LandChunk>(world, "Submerged ground");
            wetLand.Generate(world, 2, 0, 8, 1, 12, 4);
            var water = Add<CylinderWaterChunk>(world, "Water");
            water.surfaceRadius = 700f;
            water.Generate(world, 2, 0, 8, 1, 12, 4);

            Physics.SyncTransforms();
            float dry = CheckSurface(world, land, 0);
            float window = CheckSurface(world, glass, 1);
            float wet = CheckSurface(world, water, 2);
            Need(Mathf.Abs(dry - window) < .04f && Mathf.Abs(dry - wet) < .04f,
                "Equal clearances must produce equal lift.");

            Vector3 empty = world.transform.position + Direction(3) * 700f;
            Need(!GroundEffectSurface.TryMeasure(world, empty, 12f, new RaycastHit[16], out _, out _),
                "Empty space must not acquire a surface.");
            Need(GroundEffectSurface.Acceleration(12f, 0f, 12f, 12f, 1f) == 0f,
                "The edge of the effect range must not add lift.");
            Debug.Log($"Ground-effect test passed: land {dry:F2}, glass {window:F2}, water {wet:F2} m/s² at 3 m; empty space has no hit.");
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            foreach (GameObject root in scratch.GetRootGameObjects())
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh != null) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
            EditorSceneManager.CloseScene(scratch, true);
        }
    }

    private static T Add<T>(ONeillWorld world, string name) where T : Component
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(world.transform, false);
        return obj.AddComponent<T>();
    }

    private static Vector3 Direction(int strip)
    {
        // Deliberately avoid vertices/diagonals and let each mesh define its actual radius.
        float a = (strip + .43f) * Mathf.PI / 4f;
        return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
    }

    private static float CheckSurface(ONeillWorld world, Component expected, int strip)
    {
        Vector3 down = Direction(strip);
        var target = expected.GetComponent<MeshCollider>();
        Need(target.Raycast(new Ray(world.transform.position + Vector3.forward * 7f, down),
            out RaycastHit known, 1400f), $"No inward-facing collider on {expected.name}.");
        Vector3 origin = known.point - down * 3f;
        // Force the overflow fallback, even if an unrelated collider is reported first.
        Need(GroundEffectSurface.TryMeasure(world, origin, 12f, new RaycastHit[1],
            out _, out RaycastHit actual), $"No ground-effect hit on {expected.name}.");
        Need(actual.collider == target && Mathf.Abs(actual.distance - 3f) < .02f,
            $"Wrong collider or clearance for {expected.name}.");
        float lift = GroundEffectSurface.Acceleration(actual.distance, 0f, 12f, 12f, 1f);
        Need(lift > 0f, $"No lift on {expected.name}.");
        return lift;
    }

    private static void Need(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
