using System;
using UnityEditor;
using UnityEngine;

/// <summary>Run with -batchmode -executeMethod CylinderWorldChecks.Run.</summary>
public static class CylinderWorldChecks
{
    private static int checks;

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        try
        {
            Require(typeof(CylinderChunk).IsAssignableFrom(typeof(CylinderWindowChunk)),
                "Glass must use the shared CylinderChunk hierarchy.");
            var root = new GameObject("Chunk checks");
            try
            {
                var world = root.AddComponent<ONeillWorld>();
                var landObject = new GameObject("Land");
                landObject.transform.SetParent(root.transform);
                var land = landObject.AddComponent<LandChunk>();
                land.Generate(world, 0, 0, 24, 16, 12, 12);
                Mesh mesh = landObject.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh.vertexCount > 169 * 2, "Terrain needs an outer surface and separate wall vertices.");
                Require(mesh.bounds.max.x > world.radius + 250f, "Terrain shell must extend behind the deepest valleys.");
                int[] triangles = mesh.triangles;
                Vector3[] vertices = mesh.vertices;
                bool inward = false, outward = false, wall = false;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    Require(normal.sqrMagnitude > .000001f, "No degenerate chunk triangles.");
                    Vector3 radial = new Vector3(a.x + b.x + c.x, a.y + b.y + c.y, 0f).normalized;
                    float facing = Vector3.Dot(normal.normalized, radial);
                    inward |= facing < -.5f;
                    outward |= facing > .5f;
                    wall |= Mathf.Abs(facing) < .1f;
                }
                Require(inward && outward && wall, "Terrain needs inward top, outward bottom and side walls.");
                CheckWalls(mesh, 169 * 2, world);
                CheckNeighbors(root, world);
                CheckWaterCoordinates(root, world);
                CheckUpdatedDrone();
                CheckGroundEffect();
                CheckWakeSampling();
                CylinderShadowChecks.Validate();
                CylinderBuoyancyChecks.Validate();
                CheckWaterClipmap();
                CheckTerrainCacheRegeneration();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("CYLINDER_CHECKS_PASS " + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckGroundEffect()
    {
        var method = typeof(DroneController).GetMethod("GroundEffectAcceleration");
        Require(method != null, "Drone must provide a ground-effect cushion.");
        float near = (float)method.Invoke(null, new object[] { 2f, 0f, 8f, 20f, 4f });
        float far = (float)method.Invoke(null, new object[] { 8f, 0f, 8f, 20f, 4f });
        float descending = (float)method.Invoke(null, new object[] { 2f, -3f, 8f, 20f, 4f });
        Require(near > 9.81f && far == 0f, "Cushion must lift near land and fade out at its range.");
        Require(descending > near, "Cushion must damp descent toward land.");
    }

    private static void CheckWalls(Mesh mesh, int surfaceVertices, ONeillWorld world)
    {
        Vector3[] vertices = mesh.vertices, normals = mesh.normals;
        for (int i = surfaceVertices; i < vertices.Length; i += 4)
        {
            Vector3 center = (vertices[i] + vertices[i + 1] + vertices[i + 2] + vertices[i + 3]) * .25f;
            Vector3 tangent = new Vector3(-center.y, center.x, 0).normalized;
            // This test chunk spans angle 0..15 degrees and begins at the -Z end.
            if (Mathf.Abs(center.y) < .01f)
                Require(Vector3.Dot(normals[i], -tangent) > .99f, "Angular start wall faces out of its volume.");
            else if (Mathf.Abs(center.z + world.length * .5f) < .01f)
                Require(Vector3.Dot(normals[i], Vector3.back) > .99f, "Length start wall faces -Z.");
            else if (Mathf.Abs(center.z + world.length * .5f - world.length / 16f) < .01f)
                Require(Vector3.Dot(normals[i], Vector3.forward) > .99f, "Length end wall faces +Z.");
            else Require(Vector3.Dot(normals[i], tangent) > .99f, "Angular end wall faces out of its volume.");
        }
    }

    private static void CheckNeighbors(GameObject root, ONeillWorld world)
    {
        var go = new GameObject("Manager"); go.transform.SetParent(root.transform);
        var manager = go.AddComponent<CylinderChunkManager>(); manager.world = world;
        Require(manager.landStripes == 3, "The default is exactly three land stripes.");
        int starts = 0;
        for (int a = 0; a < manager.angularChunks; a++)
            if (manager.IsLand(a) && !manager.IsLand(a - 1)) starts++;
        Require(starts == 3, "Exactly three continuous land stripes, including angular wrap.");
        Require(manager.IsLand(0) == manager.IsLand(manager.angularChunks), "Stripe classification wraps.");
        Require(manager.GetExposedEdges(5, 5) == ChunkEdges.None, "An interior glass tile has no internal walls.");
        Require(manager.GetExposedEdges(4, 5) == ChunkEdges.AngularStart, "Glass closes against terrain.");
        var glassObject = new GameObject("Interior glass"); glassObject.transform.SetParent(root.transform);
        var glass = glassObject.AddComponent<CylinderWindowChunk>();
        glass.Generate(world, 5, 5, 24, 16, 12, 12, manager.GetExposedEdges(5, 5));
        Require(glassObject.GetComponent<MeshFilter>().sharedMesh.triangles.Length == 12 * 12 * 12,
            "Adjacent glass tiles emit just the inner and outer surfaces.");
        var previous = glassObject.GetComponent<MeshFilter>().sharedMesh;
        glass.Generate(world, 5, 5, 24, 16, 12, 12, ChunkEdges.None);
        Require(previous == null, "Regeneration releases the old mesh.");
        var aObject = new GameObject("Wrap A"); aObject.transform.SetParent(root.transform);
        var bObject = new GameObject("Wrap B"); bObject.transform.SetParent(root.transform);
        aObject.AddComponent<LandChunk>().Generate(world, 23, 0, 24, 16, 12, 12, ChunkEdges.None);
        bObject.AddComponent<LandChunk>().Generate(world, 0, 0, 24, 16, 12, 12, ChunkEdges.None);
        Mesh aMesh = aObject.GetComponent<MeshFilter>().sharedMesh, bMesh = bObject.GetComponent<MeshFilter>().sharedMesh;
        for (int z = 0; z <= 12; z++)
        {
            Require(Vector3.Distance(aMesh.vertices[z * 13 + 12], bMesh.vertices[z * 13]) < .0001f,
                "Terrain closes exactly at the angular wrap.");
            Require(Vector3.Dot(aMesh.normals[z * 13 + 12], bMesh.normals[z * 13]) > .999f,
                "Terrain normals match at the angular wrap.");
        }
        manager.longitudinalChunks = 2; manager.angularResolution = manager.longitudinalResolution = 2;
        manager.GenerateWorld();
        int count = manager.GetComponentsInChildren<CylinderChunk>().Length;
        manager.GenerateWorld();
        Require(manager.GetComponentsInChildren<CylinderChunk>().Length == count, "Regeneration does not duplicate chunks.");
        Require(manager.GetComponentsInChildren<CylinderEndCap>().Length == 2, "Regeneration keeps exactly two end caps.");
    }

    private static void CheckWaterCoordinates(GameObject root, ONeillWorld world)
    {
        Require(Mathf.Abs(CylinderWaterSimulation.UnwrapAngle(179f * Mathf.Deg2Rad, -179f * Mathf.Deg2Rad)
            - 181f * Mathf.Deg2Rad) < .0001f, "Water chart crosses the signed angular seam continuously.");
        var waterObject = new GameObject("Water"); waterObject.transform.SetParent(root.transform);
        waterObject.AddComponent<CylinderWaterChunk>().Generate(world, 0, 0, 24, 16, 12, 12);
        Require(waterObject.GetComponent<MeshCollider>() == null, "The sea surface does not block the drone.");
        Require(waterObject.GetComponent<MeshFilter>().sharedMesh.vertexCount == 169, "Water has a single inward-facing surface.");
        var sim = root.AddComponent<CylinderWaterSimulation>(); sim.world = world;
        var manager = root.GetComponentInChildren<CylinderChunkManager>(); sim.chunks = manager;
        var hub = new GameObject("Test hub"); hub.transform.SetParent(world.transform, false);
        float angle = .25f;
        // Find a submerged point in a land band, so a vertical fan actually reaches water.
        float z = 0;
        for (; z < world.length * .45f; z += 50f)
            if (world.SampleTerrainHeight(angle, z) < world.seaLevel - 1f) break;
        Require(z < world.length * .45f, "Test fixture contains a water-filled valley.");
        Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        hub.transform.localPosition = radial * (sim.SurfaceRadiusAtAngle(angle) - 2f) + Vector3.forward * z;
        float angularStep=Mathf.PI*2/(manager.angularChunks*manager.angularResolution);
        float expectedHeight=2f*Mathf.Cos(angle-(Mathf.Floor(angle/angularStep)+.5f)*angularStep);
        Require(sim.TryFanHit(hub.transform, radial, out Vector3 hit, out float distance) && Mathf.Abs(distance - expectedHeight) < .01f,
            "Radial downwash intersects the cylindrical sea at meter scale.");
        world.transform.position = new Vector3(-3900f, 2000f, 700f);
        Require(sim.TryFanHit(hub.transform, radial, out Vector3 shiftedHit, out float shiftedDistance) &&
            Vector3.Distance(hit, shiftedHit) < .01f && Mathf.Abs(distance - shiftedDistance) < .01f,
            "Water inputs are stable across a floating-origin shift.");
        world.transform.position = Vector3.zero;
    }

    private static void CheckUpdatedDrone()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CylinderAssetSetup.DronePath);
        Require(prefab != null, "The updated drone prefab is prepared.");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            Require(instance.transform.localScale == Vector3.one, "Drone physics root uses meter scale.");
            Transform origin = null, legacyForward = null;
            Renderer visor = null;
            foreach (Transform transform in instance.GetComponentsInChildren<Transform>())
            {
                if (transform.name == "GraceRig_Origin") origin = transform;
                if (transform.name == "GraceRig_Z") legacyForward = transform;
                if (transform.name.StartsWith("One wraparound visor", StringComparison.Ordinal)) visor = transform.GetComponent<Renderer>();
            }
            Require(origin != null && legacyForward != null &&
                Vector3.Dot((origin.position - legacyForward.position).normalized, instance.transform.forward) > .999f,
                "The updated cockpit faces along flight +Z; its legacy rig Z marker points aft.");
            Require(visor != null && visor.sharedMaterial.shader.name == "Custom/GlassShader",
                "The actual cockpit visor uses transparent custom glass.");
            Require(visor.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off,
                "Transparent canopy does not inherit an opaque fallback shadow caster.");
            var rig = instance.GetComponent<DroneRig>();
            for (int i = 0; i < rig.fanHubs.Length; i++)
                Require(Vector3.Dot((rig.fanUpMarkers[i].position - rig.fanHubs[i].position).normalized,
                    instance.transform.up) > .999f, "Actual fan axes point along local up.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void CheckWakeSampling()
    {
        var fine = new System.Collections.Generic.List<Vector3>();
        var coarse = new System.Collections.Generic.List<Vector3>();
        var samples = new System.Collections.Generic.List<Vector3>();
        var trail = new CylinderWakeTrail();
        for (int i = 0; i <= 60; i++)
        {
            trail.Sample(new Vector2(0, i * 24f / 60), i / 60f, 1.92f, samples);
            fine.AddRange(samples);
        }
        trail.Reset();
        for (int i = 0; i <= 10; i++)
        {
            trail.Sample(new Vector2(0, i * 24f / 10), i / 10f, 1.92f, samples);
            coarse.AddRange(samples);
        }
        Require(fine.Count == coarse.Count && fine.Count >= 12, "Wake spacing follows distance independently of frame rate.");
        for (int i = 0; i < fine.Count; i++)
            Require(Vector3.Distance(fine[i], coarse[i]) < .001f, "Swept wave birth positions and times match at 10 and 60 FPS.");
        trail.Reset();
        trail.Sample(new Vector2(1000, 1000), 2f, 1f, samples);
        Require(samples.Count == 1 && samples[0].x == 1000f, "Resetting a footprint never bridges a teleport or missing-water interval.");
        trail.Sample(new Vector2(1000, 1000), 2.1f, 1f, samples);
        Require(samples.Count == 0, "Stationary fans do not emit a ring every frame.");
        int stationaryEmissions = 0;
        for (int i = 2; i <= 6; i++)
        {
            trail.Sample(new Vector2(1000, 1000), 2f + i * .1f, 1f, samples);
            stationaryEmissions += samples.Count;
        }
        Require(stationaryEmissions == 1, "Stationary fans retain timed ring emission.");
    }

    private static void CheckWaterClipmap()
    {
        Mesh mesh=CylinderWaterSurface.BuildDetailMesh(320f);
        try
        {
            Require(Mathf.Abs(mesh.bounds.size.x-320f)<.01f && Mathf.Abs(mesh.bounds.size.z-320f)<.01f,
                "Water clipmap covers the enlarged 320 metre region.");
            Require(mesh.vertexCount<50000,"Expanded water preserves nearby resolution without a full dense outer grid.");
            int[] triangles=mesh.triangles;Vector3[] vertices=mesh.vertices;
            for(int i=0;i<triangles.Length;i+=3)
                Require(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).y>0,
                    "Clipmap core and expansion rings have consistent winding.");
        }
        finally{UnityEngine.Object.DestroyImmediate(mesh);}
    }

    private static void CheckTerrainCacheRegeneration()
    {
        var root=new GameObject("Terrain cache checks");
        var material=new Material(Shader.Find("Grace/VWater/Cylinder Surface"));
        try
        {
            var world=root.AddComponent<ONeillWorld>();
            var manager=root.AddComponent<CylinderChunkManager>();manager.world=world;
            manager.angularChunks=6;manager.angularResolution=2;manager.longitudinalChunks=2;manager.longitudinalResolution=2;
            var water=root.AddComponent<CylinderWaterSimulation>();water.world=world;water.chunks=manager;
            water.follow=root.transform;water.surfaceMaterial=material;water.Initialize();
            world.terrainAmplitude=125f;manager.angularResolution=3;manager.GenerateWorld();
            float angle=Mathf.PI*2f/(manager.angularChunks*manager.angularResolution);
            Require(Mathf.Abs(water.TerrainHeightAt(angle,0)-world.SampleTerrainHeight(angle,0))<.002f,
                "Regeneration refreshes cached terrain samples when amplitude and grid resolution change.");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(material);}
    }
}
