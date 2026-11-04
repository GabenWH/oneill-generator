using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vertigo2;

namespace Grace.VWaterOriginal.Editor
{
    /// <summary>Explicit read-only scene inspection. No import hooks or scene writes.</summary>
    public static class GraceDownwashDiagnostics
    {
        [MenuItem("Tools/Grace/VWater/Inspect Active Downwash Test")]
        public static void Inspect()
        {
            var scene = SceneManager.GetActiveScene();
            var report = new StringBuilder();
            report.AppendLine("VWater downwash inspection: " + DateTime.Now.ToString("O"));
            report.AppendLine("Active scene: " + scene.path + " | unsaved changes: " + scene.isDirty + " | Play: " + Application.isPlaying);
            report.AppendLine("Graphics: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            if (!Application.isPlaying)
                report.AppendLine("EDIT MODE: simulation is idle. Press Unity's Play triangle, select Game, then turn fans on. This inspection menu does not start the test.");
            var bridges = Resources.FindObjectsOfTypeAll<GraceOriginalVWaterFanBridge>()
                .Where(b => b.gameObject.scene == scene).ToArray();
            report.AppendLine("Fan bridges in active scene: " + bridges.Length);
            if (bridges.Length != 1)
            {
                Write(report.ToString());
                Debug.LogWarning("Open a saved VWater drone test scene. Inspection found " + bridges.Length + " bridges; no objects changed.");
                return;
            }
            var bridge = bridges[0];
            report.AppendLine("Bridge: " + AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(bridge)));
            report.AppendLine("Bindings valid: " + bridge.ValidateBindings(out string problem) + " " + problem);
            report.AppendLine("Enabled: " + bridge.isActiveAndEnabled + " | fans: " + bridge.fansEnabled + " | moving: " + bridge.moveDrone);
            report.AppendLine("Height: " + bridge.fanHeight + " | thrust: " + bridge.thrust + " | response gain: " + bridge.responseGain + " | pressure: " + bridge.sourceAmplitude + " | foam: " + bridge.skimFoam);
            report.AppendLine("Height curves enabled: " + bridge.useHeightResponse + " | profile: " + AssetDatabase.GetAssetPath(bridge.responseProfile));
            report.AppendLine("Airborne particles: " + bridge.AirborneParticleCount + " | geometric wave packets: " + bridge.ActiveWaveCount + " | mist enabled: " + bridge.sprayEnabled + " | waves enabled: " + bridge.wavesEnabled);
            var dynamics = bridge.originalDynamics;
            var water = bridge.water;
            if (dynamics == null || water == null) { Write(report.ToString()); return; }
            report.AppendLine("Manager: " + AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(dynamics)));
            report.AppendLine("Compute: " + AssetDatabase.GetAssetPath(dynamics.compute));
            report.AppendLine("Owner matches: " + (VWaterDynamicsManager.instance == dynamics) + " | simulation active: " + dynamics.SimulationActive);
            report.AppendLine("Camera: " + (Camera.main != null ? Camera.main.name + " " + Camera.main.transform.position.ToString("F3") : "MISSING"));
            report.AppendLine("Universe center: " + dynamics.universePos + " | size: " + dynamics.universeSize + " | resolution: " + dynamics.bufferSize);
            report.AppendLine("Accepted/submitted this frame: " + bridge.CallsAcceptedThisFrame + "/" + bridge.CallsSubmittedThisFrame);
            report.AppendLine("Dispatches total: " + dynamics.TotalDispatches + " | this frame: " + dynamics.DispatchesThisFrame + " | last sources: " + dynamics.LastSkimSourcesDispatched + " | last time: " + dynamics.LastDispatchTime.ToString("F3"));
            var meshRenderer = bridge.originalRenderer != null ? bridge.originalRenderer.GetComponent<Renderer>() : null;
            var material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            var buffer = VWaterDynamicsManager.waterBuffer;
            report.AppendLine("Dynamic keyword: " + (material != null && material.IsKeywordEnabled("DYNAMICS_ENABLED")) + " | material samples live buffer: " + (material != null && buffer != null && material.GetTexture("_WaveBuffer") == buffer));
            var samples = new Vector3[bridge.fanHubs.Length];
            var hits = new bool[samples.Length];
            Vector3 surface = water.transform.position; surface.y = water.Position;
            Bounds footprintBounds = water.bounds;
            if (!Application.isPlaying)
            {
                // Runtime bounds are initialized in Start. Inspect actual geometry
                // in Edit mode instead of falsely reporting zero-sized water/misses.
                var colliders = water.GetComponentsInChildren<Collider>()
                    .Where(c => c.enabled && c.gameObject.activeInHierarchy).ToArray();
                if (colliders.Length > 0)
                {
                    footprintBounds = colliders[0].bounds;
                    for (int i = 1; i < colliders.Length; i++) footprintBounds.Encapsulate(colliders[i].bounds);
                }
            }
            report.AppendLine("Water bounds (XZ): " + footprintBounds.size.x.ToString("F2") + " x " + footprintBounds.size.z.ToString("F2") + " m" + (!Application.isPlaying ? " | geometry preview only" : ""));
            for (int i = 0; i < samples.Length; i++)
            {
                if (bridge.fanHubs[i] == null || i >= bridge.fanUpMarkers.Length || bridge.fanUpMarkers[i] == null) continue;
                Vector3 hub = bridge.fanHubs[i].position;
                Vector3 jet = hub - bridge.fanUpMarkers[i].position;
                hits[i] = DownwashMath.TryDisc(V(hub), V(jet), V(surface), footprintBounds.size.x * .5, footprintBounds.size.z * .5, bridge.thrust, out DownwashMath.Disc disc);
                report.AppendLine("Fan " + i + " hub: " + hub.ToString("F3") + " | jet: " + jet.normalized.ToString("F3") + " | hits pond: " + hits[i]);
                if (!hits[i]) continue;
                samples[i] = new Vector3((float)disc.Point.X, (float)disc.Point.Y, (float)disc.Point.Z);
                double power=DownwashVisualMath.ProjectedPower(disc.Strength,disc.Height,bridge.responseGain);
                var response=bridge.EvaluateResponse((float)disc.Height);
                var inputs=bridge.EvaluateSurfaceInputs(disc,response,power);
                report.AppendLine("  Hit: " + samples[i].ToString("F3") + " | radius: " + disc.Radius.ToString("F3") + " | pressure after clamp: " + inputs.Pressure.ToString("F4") + " | foam after clamp: " + inputs.Foam.ToString("F4"));
                report.AppendLine("  Curve/settings outputs: mist="+response.MistAmount.ToString("F3")+", spray="+response.SprayHeight.ToString("F3")+", wave="+response.WaveHeight.ToString("F3")+" | projected thrust/gain="+power.ToString("F3"));
            }
            if (!Application.isPlaying || buffer == null || !buffer.IsCreated())
            {
                report.AppendLine("GPU readback unavailable: enter Play with the Game view visible, then run this menu again.");
                Write(report.ToString()); return;
            }
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                report.AppendLine("GPU readback unsupported by this graphics backend. CPU counters do not prove pixels.");
                Write(report.ToString()); return;
            }
            Vector2 universe = dynamics.universePos;
            float size = dynamics.universeSize;
            int width = buffer.width, height = buffer.height;
            report.AppendLine("Reading a snapshot of the live simulation texture; residual waves/foam can persist after fans stop.");
            Debug.Log(report.ToString(), bridge);
            AsyncGPUReadback.Request(buffer, 0, TextureFormat.RGBAFloat, request =>
            {
                if (request.hasError) report.AppendLine("GPU READBACK FAILED; no GPU success claimed.");
                else
                {
                    var pixels = request.GetData<Color>();
                    float minHeight = float.MaxValue, maxHeight = float.MinValue, maxFoam = 0, maxNormal = 0;
                    int invalid = 0;
                    foreach (Color pixel in pixels)
                    {
                        if (!Finite(pixel.r) || !Finite(pixel.g) || !Finite(pixel.b) || !Finite(pixel.a)) { invalid++; continue; }
                        minHeight = Mathf.Min(minHeight, pixel.r); maxHeight = Mathf.Max(maxHeight, pixel.r);
                        maxFoam = Mathf.Max(maxFoam, pixel.g); maxNormal = Mathf.Max(maxNormal, Mathf.Abs(pixel.b), Mathf.Abs(pixel.a));
                    }
                    report.AppendLine("GPU snapshot: height [" + minHeight.ToString("F5") + ", " + maxHeight.ToString("F5") + "] | peak foam " + maxFoam.ToString("F5") + " | peak normal component " + maxNormal.ToString("F5") + " | nonfinite pixels " + invalid);
                    for (int i = 0; i < samples.Length; i++)
                    {
                        if (!hits[i] || size <= 0) continue;
                        int x = Mathf.FloorToInt(((samples[i].x - universe.x) / size + .5f) * width);
                        int y = Mathf.FloorToInt(((samples[i].z - universe.y) / size + .5f) * height);
                        if (x < 0 || y < 0 || x >= width || y >= height) report.AppendLine("Fan " + i + " is outside live buffer.");
                        else report.AppendLine("Fan " + i + " GPU sample (height,foam,nx,nz): " + pixels[y * width + x].ToString("F5"));
                    }
                    if (!Application.isPlaying || buffer == null || VWaterDynamicsManager.waterBuffer != buffer)
                        report.AppendLine("Scene/Play state changed during readback; this snapshot belongs to the earlier buffer.");
                }
                Write(report.ToString());
            });
        }

        private static DownwashMath.V3 V(Vector3 p) { return new DownwashMath.V3(p.x, p.y, p.z); }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        private static void Write(string report)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Diagnostics/Runtime"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Downwash_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            File.WriteAllText(path, report);
            Debug.Log(report + "\nReport saved: " + path);
        }
    }
}
