using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>GPU regression for the camera-following shadow circle on Land_19_4.</summary>
public static class CylinderTerrainShadowChecks
{
    private static readonly Vector3 ReportedPosition = new Vector3(368.080658f, -105.516068f, -650.618042f);
    private static readonly Quaternion ReportedRotation = new Quaternion(.183074355f, -.793751895f, .249821991f, .523479521f);
    private static readonly string[] Shaders = { "Custom/LandShader", "Custom/CylinderStandard", "Custom/GlassShader",
        "Grace/VWater/Cylinder Surface", "Grace/VWater/Cylinder Mist" };

    public static void Run()
    {
        try { RenderCases(); Debug.Log("CYLINDER_TERRAIN_SHADOW_PASS"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void RenderCases()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Terrain shadow reproduction");
        var world = root.AddComponent<ONeillWorld>();
        var chunks = root.AddComponent<CylinderChunkManager>();
        chunks.world = world;
        chunks.landMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/LandMaterial.mat");
        chunks.glassMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CylinderGlass.mat");
        chunks.GenerateWorld();
        Transform tile = root.transform.Find("Generated Cylinder/Land_19_4");
        Require(tile != null, "The reported terrain chunk must exist.");
        foreach (var land in root.GetComponentsInChildren<LandChunk>())
            Require(land.GetComponent<MeshRenderer>().receiveShadows &&
                land.GetComponent<MeshRenderer>().shadowCastingMode == ShadowCastingMode.On,
                "Terrain must retain normal local shadow casting and reception.");
        Vector3 target = tile.GetComponent<MeshFilter>().sharedMesh.vertices[6 * 13 + 6];
        // The accumulated floating-origin offset was not in the report. Keep
        // the exact camera transform and put the named tile 100 m ahead of it.
        root.transform.position = ReportedPosition + ReportedRotation * Vector3.forward * 100f - target;
        var camera = new GameObject("Reported terrain camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetPositionAndRotation(ReportedPosition, ReportedRotation);
        camera.fieldOfView = 75f; camera.nearClipPlane = .1f; camera.farClipPlane = 50000;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.depthTextureMode = DepthTextureMode.Depth;
        var sun = new GameObject("Diagnostic sun", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(35, -30, 0);
        var provider = sun.gameObject.AddComponent<CylinderSunShadows>();
        provider.world = world; provider.chunks = chunks; provider.sun = sun; provider.glassMaterial = chunks.glassMaterial;
        // Runtime components do not receive their Play callbacks in this Edit-mode harness.
        Lifecycle(provider, "OnEnable");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.45f, .55f, .65f);
        RenderSettings.ambientEquatorColor = new Color(.30f, .35f, .30f);
        RenderSettings.ambientGroundColor = new Color(.12f, .12f, .12f);
        try
        {
            foreach (float yaw in new[] { -30f, -26f, -34f })
            {
                sun.transform.rotation = Quaternion.Euler(35, yaw, 0);
                sun.shadows = LightShadows.None; provider.Refresh();
                var stable = Capture(camera, "housing-" + yaw);
                foreach (float distance in new[] { 75f, 150f, 300f })
                {
                    sun.shadows = LightShadows.Soft; QualitySettings.shadowDistance = distance; provider.Refresh();
                    Compare("yaw=" + yaw + " distance=" + distance, stable, Capture(camera, "shadow-" + yaw + "-" + distance));
                }
            }
            // Isolate our housing coordinates from Unity's world-anchored
            // cascade texel snapping, which can change low-level PCF shading.
            sun.shadows = LightShadows.None; provider.Refresh();
            var beforeShift = Capture(camera, "before-origin-shift");
            Vector3 shift = new Vector3(9000, -2500, 1000);
            root.transform.position += shift; camera.transform.position += shift;
            provider.Refresh();
            Compare("floating origin", beforeShift, Capture(camera, "after-origin-shift"));
            CheckShaders();
        }
        finally
        {
            Lifecycle(provider, "OnDisable");
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(sun.gameObject);
        }
    }

    private static void Lifecycle(CylinderSunShadows provider, string method)
        => typeof(CylinderSunShadows).GetMethod(method, System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance).Invoke(provider, null);

    private static void CheckShaders()
    {
        foreach (string name in Shaders)
        {
            Shader shader = Shader.Find(name);
            Require(shader != null && shader.isSupported, "Missing or unsupported shader: " + name);
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,
                    name + ": " + message.message);
        }
    }

    private static Color32[] Capture(Camera camera, string label)
    {
        var target = new RenderTexture(960, 640, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(960, 640, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        try
        {
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); pixels.Apply();
            CheckShaders();
            File.WriteAllBytes("/tmp/oneill-terrain-" + label + ".png", pixels.EncodeToPNG());
            return pixels.GetPixels32();
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Compare(string label, Color32[] a, Color32[] b)
    {
        long error = 0; int changed = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int delta = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
            error += delta; if (delta > 12) changed++;
        }
        float mean = error / (255f * a.Length), outliers = changed / (float)a.Length;
        Debug.Log("TERRAIN_SHADOW_COMPARE " + label + " mean=" + mean + " outliers=" + outliers);
        Require(mean < .005f && outliers < .01f,
            "The terrain housing shadow must not reveal a camera-centred circle at the realtime shadow limit: " + label);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
