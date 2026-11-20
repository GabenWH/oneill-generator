using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Batch Play/render smoke checks, intended for a temporary project copy.</summary>
[InitializeOnLoad]
public static class CylinderPlayChecks
{
    private const string Pending = "CylinderPlayChecks.Pending";
    private static int errors, startFrame, stage;
    private static double startTime;
    private static float stageTime;
    private static bool CheckMotion => SessionState.GetBool("CylinderPlayChecks.Motion", false);
    private static bool CheckAppearance => SessionState.GetBool("CylinderPlayChecks.Appearance", false);
    private static bool CheckBuoyancy => SessionState.GetBool("CylinderPlayChecks.Buoyancy", false);
    private static int initialMistCount, initialDropCount;

    static CylinderPlayChecks()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
        startTime = EditorApplication.timeSinceStartup;
    }

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    public static void RunMotion()
    {
        SessionState.SetBool("CylinderPlayChecks.Motion", true);
        Run();
    }

    public static void RunAppearance()
    {
        SessionState.SetBool("CylinderPlayChecks.Appearance", true);
        Time.captureDeltaTime = 1f/60f;
        Run();
    }

    public static void RunBuoyancy()
    {
        SessionState.SetBool("CylinderPlayChecks.Buoyancy",true);
        Time.captureDeltaTime=1f/60f;
        Run();
    }

    private static void OnLog(string condition, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.timeSinceStartup - startTime > 90) Finish(1, "Play startup timed out.");
            return;
        }
        try
        {
            if(CheckAppearance || CheckMotion || CheckBuoyancy)Time.captureDeltaTime=1f/60f;
            if (startFrame == 0) startFrame = Time.frameCount;
            if (Time.frameCount - startFrame < 30) return;
            if (stage == 1 && Time.time - stageTime < 1.5f) return;
            if (stage == 1 && CheckBuoyancy && Time.time-stageTime<8f) return;
            var world = UnityEngine.Object.FindObjectOfType<ONeillWorld>();
            var manager = UnityEngine.Object.FindObjectOfType<CylinderChunkManager>();
            var rig = UnityEngine.Object.FindObjectOfType<DroneRig>();
            var water = UnityEngine.Object.FindObjectOfType<CylinderWaterSimulation>();
            Require(world != null && manager != null && rig != null && water != null, "Scene must wire world, chunks, updated drone and custom water.");
            Require(manager.landStripes == 3, "Scene must use three land stripes.");
            Require(rig.fanHubs.Length == 2 && rig.fanBlades.Length == 10, "Actual updated fan rig is bound.");
            Require(manager.GetComponentsInChildren<LandChunk>().Length == 192, "Exactly 192 land chunks.");
            Require(manager.GetComponentsInChildren<CylinderWindowChunk>().Length == 192, "Exactly 192 glass chunks.");
            Require(manager.GetComponentsInChildren<CylinderWaterChunk>().Length == 192, "Water covers only the land bands.");
            foreach (var name in new[] { "Custom/LandShader", "Custom/CylinderStandard", "Custom/GlassShader", "Grace/VWater/Cylinder Surface", "Grace/VWater/Cylinder Mist" })
            {
                Shader shader = Shader.Find(name);
                Require(shader != null, "Missing shader: " + name);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,
                        name + ": " + message.message);
            }
            bool gpu = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            Camera camera = Camera.main;
            if (stage == 0)
            {
                if (gpu) Capture(camera, "/tmp/oneill-flight-start.png");
                // Place the actual fans over a submerged valley in the middle of a land band.
                float angle = 30f * Mathf.Deg2Rad, z = 0;
                for (; z < world.length * .4f; z += 25f)
                {
                    bool submerged = true;
                    for (int ahead = 0; ahead <= (CheckMotion ? 100 : 0); ahead += 10)
                        submerged &= world.SampleTerrainHeight(angle, z + ahead) < -40f;
                    if (submerged) break;
                }
                Require(z < world.length * .4f, "Fixture needs a submerged valley.");
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var rb = rig.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                rig.transform.position = world.transform.TransformPoint(radial * (world.WaterRadius -
                    (CheckMotion || CheckAppearance || CheckBuoyancy ? 1f : 2.5f)) + Vector3.forward * z);
                rig.transform.rotation = Quaternion.LookRotation(Vector3.forward, -radial);
                camera.transform.localPosition = new Vector3(0, 4, -8);
                camera.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -2.5f, 8f));
                if (CheckMotion)
                {
                    rig.GetComponent<DroneController>().enabled = false;
                    rig.GetComponent<CylinderGravity>().enabled = false;
                    rb.isKinematic = false;
                    rb.drag = 0f;
                    rb.angularVelocity = Vector3.zero;
                    rb.velocity = Vector3.forward * 24f;
                }
                if(CheckBuoyancy)
                {
                    rig.GetComponent<DroneController>().enabled=false;
                    world.simulateCoriolis=false;
                    rb.isKinematic=false;rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
                    rig.fanPower=0f;
                }
                foreach (Transform hub in rig.fanHubs)
                    Require(water.TryFanHit(hub, -rig.transform.up, out _, out _), "Both actual fans hit exposed water.");
                initialMistCount = ReadCount(water,"EmittedMistParticles");
                initialDropCount = ReadCount(water,"EmittedDroplets");
                stage = 1; startFrame = Time.frameCount; stageTime = Time.time;
                return;
            }
            if (CheckMotion) CheckMovingEffects(water, rig);
            if (CheckAppearance)
            {
                CaptureCloseShadows(camera, rig);
                CheckAppearanceConfiguration(water, rig);
                CylinderWaterGpuChecks.CheckFoamDecay(water.compute,(float)typeof(CylinderWaterSimulation).GetField("foamHalfLife").GetValue(water));
                CheckPatchBlend(camera,water,rig);
            }
            if (CheckMotion)
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var inputs = (Vector4[])typeof(CylinderWaterSimulation).GetField("skimVelocities", flags).GetValue(water);
                var positions = (Vector4[])typeof(CylinderWaterSimulation).GetField("flowPositions", flags).GetValue(water);
                var amplitudes = (float[])typeof(CylinderWaterSimulation).GetField("flowAmplitudes", flags).GetValue(water);
                Debug.Log("CYLINDER_INPUT center=" + water.UniversePosition + " position=" + positions[0] +
                    " velocityFoam=" + inputs[0].ToString("F5") + " pressure=" + amplitudes[0] + " steps=" + water.TotalDispatches);
            }
            if(CheckBuoyancy)
            {
                var buoyancy=rig.GetComponent<CylinderBuoyancy>();
                Require(buoyancy!=null && buoyancy.submergedFraction>.15f && buoyancy.submergedFraction<.65f,
                    "The craft settles partly submerged with its fans off.");
                Require(rig.GetComponent<Rigidbody>().velocity.magnitude<1f,"Floating hull settles without continued sinking or bouncing.");
                if(gpu)Capture(camera,"/tmp/oneill-floating-craft.png");
                Require(errors==0,"Buoyancy Play produced errors.");
                Finish(0,"CYLINDER_BUOYANCY_PLAY_PASS fraction="+buoyancy.submergedFraction+" speed="+rig.GetComponent<Rigidbody>().velocity.magnitude);
                return;
            }
            if (gpu)
            {
                Require(water.DynamicsAvailable && water.TotalDispatches > 0, "Custom GPU solver must dispatch.");
                var buffer = (RenderTexture)typeof(CylinderWaterSimulation).GetField("waterBuffer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(water);
                CheckGpuField(buffer);
                Capture(camera, "/tmp/oneill-drone-water.png");
                if (CheckMotion) CaptureLighting(camera, rig);
            }
            Require(errors == 0, "Play/render produced " + errors + " errors.");
            Finish(0, "CYLINDER_PLAY_PASS gpu=" + gpu + " dispatches=" + water.TotalDispatches);
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1, exception.Message); }
    }

    private static int ReadCount(CylinderWaterSimulation water, string name)
        => (int)(typeof(CylinderWaterSimulation).GetProperty(name)?.GetValue(water) ?? 0);

    private static void CheckAppearanceConfiguration(CylinderWaterSimulation water, DroneRig rig)
    {
        Require(Mathf.Approximately(water.universeSize, 320f), "Detailed water area must be 5x wider: 320 m, previously 64 m.");
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var systems=(ParticleSystem[])typeof(CylinderWaterSimulation).GetField("mist",flags).GetValue(water);
        Require((float)typeof(CylinderWaterSimulation).GetField("airDrag").GetValue(water)>=4f,
            "Mist has significant air drag.");
        var detail=(Mesh)typeof(CylinderWaterSimulation).GetField("detailMesh",flags).GetValue(water);
        Require(detail.bounds.size.magnitude < 1000f, "Detail bounds follow the local water patch, not the entire habitat.");
        Require(detail.vertexCount < 50000, "Five-times larger water keeps dense geometry near the drone.");
        double rate=0;
        foreach (var hub in rig.fanHubs)
        {
            Require(water.TryFanHit(hub,-rig.transform.up,out _,out float height),"Curve fixture stays above water.");
            rate+=water.responseProfile.Evaluate(height).MistAmount;
        }
        double duration=Time.time-stageTime;
        double mistExpected=rate*60*duration,dropExpected=rate*180*duration;
        int mist=ReadCount(water,"EmittedMistParticles")-initialMistCount;
        int drops=ReadCount(water,"EmittedDroplets")-initialDropCount;
        Debug.Log("CYLINDER_CURVE_RATES expectedMist="+mistExpected+" actualMist="+mist+" expectedDrops="+dropExpected+" actualDrops="+drops+" duration="+duration);
        Require(Math.Abs(mist-mistExpected)<mistExpected*.12+8,"Mist emission follows vwatertest height curves and 60/s rate.");
        Require(Math.Abs(drops-dropExpected)<dropExpected*.12+8,"Droplet emission follows vwatertest height curves and 180/s rate.");
        Debug.Log("CYLINDER_APPEARANCE_PASS mist="+mist+" droplets="+drops+" width="+water.universeSize);
    }

    private static void CaptureCloseShadows(Camera camera, DroneRig rig)
    {
        Vector3 position=camera.transform.localPosition;Quaternion rotation=camera.transform.localRotation;
        float distance=QualitySettings.shadowDistance;
        Light sun=null;Quaternion sunRotation=Quaternion.identity;
        foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())
            if(light.type==LightType.Directional){sun=light;sunRotation=sun.transform.rotation;break;}
        try
        {
            if(sun!=null)sun.transform.rotation=Quaternion.LookRotation(-rig.transform.up);
            camera.transform.localPosition=new Vector3(0,.7f,.4f);
            camera.transform.localRotation=Quaternion.Euler(10,0,0);
            foreach(float limit in new[]{75f,150f,300f})
            {
                QualitySettings.shadowDistance=limit;
                Capture(camera,"/tmp/oneill-close-shadow-"+limit+".png");
            }
        }
        finally { QualitySettings.shadowDistance=distance;camera.transform.localPosition=position;camera.transform.localRotation=rotation;if(sun!=null)sun.transform.rotation=sunRotation; }
    }

    private static void CheckPatchBlend(Camera camera,CylinderWaterSimulation water,DroneRig rig)
    {
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var detail=(GameObject)typeof(CylinderWaterSimulation).GetField("detailObject",flags).GetValue(water);
        var detailMaterial=(Material)typeof(CylinderWaterSimulation).GetField("detailMaterial",flags).GetValue(water);
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        Light sun=null;Quaternion sunRotation=Quaternion.identity;
        foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())if(light.type==LightType.Directional){sun=light;sunRotation=sun.transform.rotation;break;}
        try
        {
            float angle=water.UniversePosition.x/water.world.WaterRadius;
            Vector3 radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            camera.transform.position=water.world.transform.TransformPoint(radial*(water.world.WaterRadius-240f)+Vector3.forward*water.UniversePosition.y);
            camera.transform.rotation=Quaternion.LookRotation(water.world.transform.TransformDirection(radial),water.world.transform.forward);
            if(sun!=null)sun.transform.rotation=Quaternion.LookRotation(water.world.transform.TransformDirection(radial));
            foreach(var material in new[]{water.surfaceMaterial,detailMaterial})
            {material.SetInt("_DownwashWaveCount",0);material.SetFloat("_DynamicsEnabled",0);}
            water.surfaceMaterial.SetFloat("_DetailPatchEnabled",0);detail.SetActive(false);
            var coarse=Capture(camera,"/tmp/oneill-patch-coarse.png");
            water.surfaceMaterial.SetFloat("_DetailPatchEnabled",1);detail.SetActive(true);
            var blended=Capture(camera,"/tmp/oneill-patch-blended.png");
            long difference=0;int large=0;
            for(int i=0;i<coarse.Length;i++)
            {
                int error=Mathf.Max(Mathf.Abs(coarse[i].r-blended[i].r),Mathf.Max(Mathf.Abs(coarse[i].g-blended[i].g),Mathf.Abs(coarse[i].b-blended[i].b)));
                difference+=error;if(error>12)large++;
            }
            float mean=difference/(255f*coarse.Length),outliers=large/(float)coarse.Length;
            Debug.Log("CYLINDER_PATCH_BLEND mean="+mean+" outliers="+outliers);
            Require(mean<.015f && outliers<.025f,"Inactive detail patch must match surrounding water without a replacement square.");
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);detail.SetActive(true);water.surfaceMaterial.SetFloat("_DetailPatchEnabled",1);
            if(sun!=null)sun.transform.rotation=sunRotation;
        }
    }

    private static void CheckMovingEffects(CylinderWaterSimulation water, DroneRig rig)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var systems = (ParticleSystem[])typeof(CylinderWaterSimulation).GetField("mist", flags).GetValue(water);
        foreach (var system in systems)
        {
            Require(system.GetComponent<ParticleSystemRenderer>().receiveShadows,
                "Spray receives shadows so unlit mist stays faint.");
            var particles = new ParticleSystem.Particle[system.main.maxParticles];
            int count = system.GetParticles(particles);
            Require(count > 0, "Moving fans emit spray.");
            float forwardVelocity = 0f,oldVelocity=0;int young=0,old=0;
            for (int i = 0; i < count; i++)
            {
                float age=particles[i].startLifetime-particles[i].remainingLifetime;
                if(age<.03f){forwardVelocity+=particles[i].velocity.z;young++;}
                if(age>.3f){oldVelocity+=Mathf.Abs(particles[i].velocity.z);old++;}
            }
            Require(young>0,"Moving fans continuously emit new mist.");
            forwardVelocity /= young;
            Require(forwardVelocity > 18f && forwardVelocity < 30f,
                "Spray must inherit the 24 m/s drone velocity; got " + forwardVelocity);
            Debug.Log("CYLINDER_SPRAY_DRAG young="+forwardVelocity+" old="+(old>0?oldVelocity/old:0));
            Require(old>0 && oldVelocity/old<forwardVelocity*.65f,"Air drag slows older mist significantly.");
        }
        var motions = (System.Collections.Generic.List<Vector4>)typeof(CylinderWaterSimulation)
            .GetField("waveMotions", flags).GetValue(water);
        var waves = (System.Collections.Generic.List<Vector4>)typeof(CylinderWaterSimulation)
            .GetField("waves", flags).GetValue(water);
        for (int fan = 0; fan < 2; fan++)
        {
            int count = 0; float previous = float.NaN;
            for (int i = 0; i < motions.Count; i++)
            {
                if ((int)motions[i].w != fan) continue;
                Require(motions[i].y > 8f, "Visible waves inherit surface velocity.");
                if (!float.IsNaN(previous)) Require(waves[i].y - previous < 4f, "Fast movement has no gaps between wake emissions.");
                previous = waves[i].y; count++;
            }
            Require(count >= 8, "Moving fan must emit a continuous wave trail.");
        }
        foreach (var chunk in water.chunks.GetComponentsInChildren<CylinderWaterChunk>())
            Require(chunk.GetComponent<Renderer>().receiveShadows, "Coarse water receives the same shadows as the detail patch.");
        Debug.Log("CYLINDER_MOTION_PASS velocity=" + rig.GetComponent<Rigidbody>().velocity + " waves=" + waves.Count);
    }

    private static void CaptureLighting(Camera camera, DroneRig rig)
    {
        foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
        {
            if (light.type != LightType.Directional) continue;
            Quaternion previous = light.transform.rotation;
            try
            {
                light.transform.rotation = Quaternion.LookRotation(-rig.transform.up);
                Capture(camera, "/tmp/oneill-water-lit.png");
                light.transform.rotation = Quaternion.LookRotation(rig.transform.up);
                Capture(camera, "/tmp/oneill-water-unlit.png");
            }
            finally { light.transform.rotation = previous; }
            break;
        }
    }

    private static void CheckGpuField(RenderTexture buffer)
    {
        // Explicitly synchronize compute writes before reading the field.
        var readback = AsyncGPUReadback.Request(buffer, 0, TextureFormat.RGBAFloat);
        readback.WaitForCompletion();
        Require(!readback.hasError, "Compute field GPU readback succeeds.");
        float displacement = 0, foam = 0;
        foreach (Color sample in readback.GetData<Color>())
        {
            Require(!float.IsNaN(sample.r) && !float.IsInfinity(sample.r), "GPU wave field stays finite.");
            displacement = Mathf.Max(displacement, Mathf.Abs(sample.r));
            foam = Mathf.Max(foam, sample.g);
        }
        Debug.Log("CYLINDER_GPU_FIELD displacement=" + displacement + " foam=" + foam);
        Require(displacement > .00001f && foam > .00001f, "Actual fans must produce nonzero GPU displacement and foam.");
    }

    private static Color32[] Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Finish(int code, string message)
    {
        SessionState.SetBool(Pending, false);
        SessionState.SetBool("CylinderPlayChecks.Motion", false);
        SessionState.SetBool("CylinderPlayChecks.Appearance", false);
        SessionState.SetBool("CylinderPlayChecks.Buoyancy", false);
        Time.captureDeltaTime = 0;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        Debug.Log(message);
        EditorApplication.Exit(code);
    }
}
