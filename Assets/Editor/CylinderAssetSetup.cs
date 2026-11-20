using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit asset preparation; no automatic editing of open scenes.</summary>
public static class CylinderAssetSetup
{
    private const string ModelFolder = "Assets/Models/Drone50Updated";
    public const string DronePath = "Assets/Prefabs/Drone50Updated.prefab";

    [MenuItem("Tools/O'Neill/Prepare Updated Drone and Materials")]
    public static void PrepareAssets()
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/Drone50Updated.fbx");
        if (modelAsset == null) throw new InvalidOperationException("Updated drone FBX has not imported.");
        var root = new GameObject("Drone50Updated");
        try
        {
            var canopy = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DroneCanopy.mat");
            if (canopy == null)
            {
                canopy = new Material(Shader.Find("Custom/GlassShader")) { name = "DroneCanopy" };
                AssetDatabase.CreateAsset(canopy, "Assets/Materials/DroneCanopy.mat");
            }
            canopy.SetColor("_Tint", new Color(.3f, .55f, .6f, 1f));
            canopy.SetFloat("_BaseAlpha", .01f);
            canopy.SetFloat("_EdgeAlpha", .06f);
            EditorUtility.SetDirty(canopy);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            Transform[] transforms = model.GetComponentsInChildren<Transform>();
            Transform origin = transforms.Single(t => t.name == "GraceRig_Origin");
            Transform up = transforms.Single(t => t.name == "GraceRig_Y");
            Transform forward = transforms.Single(t => t.name == "GraceRig_Z");
            float unit = Vector3.Distance(origin.position, up.position);
            // The legacy Z marker points aft; this cockpit's nose is Blender +Y.
            model.transform.rotation = Quaternion.Inverse(Quaternion.LookRotation(
                origin.position - forward.position, up.position - origin.position));
            model.transform.localScale *= 1f / unit;
            model.transform.position -= origin.position;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    AssetDatabase.LoadAssetAtPath<Material>(ModelFolder + "/" + material.name + ".mat")
                    ?? throw new InvalidOperationException("Missing drone material: " + material.name)).ToArray();
                if (renderer.name.StartsWith("One wraparound visor", StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = canopy;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            var rig = root.AddComponent<DroneRig>();
            rig.fanHubs = transforms.Where(t => t.name.StartsWith("Fan hub", StringComparison.Ordinal)).OrderBy(t => t.position.x).ToArray();
            rig.fanUpMarkers = rig.fanHubs.Select(t => t.GetComponentsInChildren<Transform>().Single(
                marker => marker.name.StartsWith("GraceFanUp_", StringComparison.Ordinal))).ToArray();
            rig.fanBlades = transforms.Where(t => t.name.StartsWith("Low-poly fan blade", StringComparison.Ordinal)).ToArray();
            if (rig.fanHubs.Length != 2 || rig.fanBlades.Length != 10)
                throw new InvalidOperationException("Updated drone must have two hubs and ten blades.");
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.x < 3f || bounds.size.x > 7f || bounds.size.z < 3f || bounds.size.z > 6f)
                throw new InvalidOperationException("Unexpected drone size in metres: " + bounds.size);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 250f;
            rb.useGravity = false;
            root.AddComponent<DroneController>();
            var cameraObject = new GameObject("Drone Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, .70f, .40f);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 50000f;
            camera.depthTextureMode = DepthTextureMode.Depth;
            PrefabUtility.SaveAsPrefabAsset(root, DronePath);
            Debug.Log("Updated drone prepared at meter scale: " + bounds.size + "; 2 hubs, 10 blades.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }

        var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CylinderGlass.mat");
        glass.shader = Shader.Find("Custom/GlassShader");
        glass.SetColor("_Tint", new Color(.5f, .65f, .7f, 1f));
        glass.SetFloat("_BaseAlpha", .025f);
        glass.SetFloat("_EdgeAlpha", .35f);
        glass.shaderKeywords = new string[0];
        EditorUtility.SetDirty(glass);
        var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Water/CylinderWater.mat");
        if (water == null)
        {
            water = new Material(Shader.Find("Grace/VWater/Cylinder Surface"));
            AssetDatabase.CreateAsset(water, "Assets/Water/CylinderWater.mat");
        }
        water.SetTexture("_Normal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Water/Textures/water_NRM_smooth.png"));
        water.SetTexture("_FoamMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Water/Textures/foam.png"));
        EditorUtility.SetDirty(water);
        AssetDatabase.SaveAssets();
    }

    // Batch setup edits only this explicitly named scene, never the user's open scene.
    public static void PrepareScene()
    {
        PrepareAssets();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var bootstrap = UnityEngine.Object.FindObjectOfType<WorldBootstrap>();
        bootstrap.dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePath);
        bootstrap.waterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Water/CylinderWater.mat");
        bootstrap.waterCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Water/Shaders/VWaterCompute.compute");
        bootstrap.flowMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Water/Textures/flowNoise.tga");
        bootstrap.mistShader = Shader.Find("Grace/VWater/Cylinder Mist");
        bootstrap.downwashProfile = AssetDatabase.LoadAssetAtPath<Grace.VWaterOriginal.DownwashResponseProfile>("Assets/Water/DroneHeightResponse.asset");
        bootstrap.landStripes = 3;
        EditorSceneManager.SaveScene(scene);
        Debug.Log("CYLINDER_ASSETS_PREPARED");
        EditorApplication.Exit(0);
    }

    public static void PrepareAndCheck()
    {
        PrepareAssets();
        CylinderWorldChecks.Run();
    }

    public static void FixGlassAndCheck()
    {
        var root=PrefabUtility.LoadPrefabContents(DronePath);
        try
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.name=="Custom/GlassShader")
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(root,DronePath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        CylinderWorldChecks.Run();
    }
}
