using Grace.VWaterOriginal;
using UnityEngine;

public class WorldBootstrap : MonoBehaviour
{
    [Header("Cylinder")]
    [Min(1f)] public float radius = 4000f;
    [Min(1f)] public float length = 32000f;
    [Min(1)] public int landStripes = 3;
    [Header("Custom Drone")]
    public GameObject dronePrefab;
    public float droneStartRadius = 1000f;
    [Tooltip("Degrees around the cylinder; 30 is the center of the first land stripe.")]
    public float droneStartAngle = 30f;
    [Min(2f)] public float droneSurfaceClearance = 75f;
    [Header("Cylinder Materials")]
    public Material landMaterial;
    public Material windowMaterial;
    public Color lowColor, midColor, highColor;
    [Header("Custom VWater")]
    public Material waterMaterial;
    public ComputeShader waterCompute;
    public Texture2D flowMap;
    public Shader mistShader;
    public DownwashResponseProfile downwashProfile;
    private Material runtimeWaterMaterial;

    private void Awake() { BuildWorld(); }

    private void BuildWorld()
    {
        var worldObject = new GameObject("ONeillWorld");
        var world = worldObject.AddComponent<ONeillWorld>();
        world.radius = radius;
        world.length = length;
        var chunksObject = new GameObject("Cylinder Chunks");
        chunksObject.transform.SetParent(world.transform, false);
        var manager = chunksObject.AddComponent<CylinderChunkManager>();
        manager.world = world;
        manager.landStripes = landStripes;
        manager.landMaterial = landMaterial;
        manager.glassMaterial = windowMaterial;
        if (waterMaterial != null)
        {
            runtimeWaterMaterial = new Material(waterMaterial) { name = "Cylinder water (runtime)" };
            manager.waterMaterial = runtimeWaterMaterial;
        }
        manager.GenerateWorld();

        GameObject drone;
        if (dronePrefab != null) drone = Instantiate(dronePrefab);
        else
        {
            Debug.LogWarning("No drone prefab assigned; creating a flight placeholder.");
            drone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drone.AddComponent<Rigidbody>().useGravity = false;
            drone.AddComponent<DroneController>();
        }
        float angle = droneStartAngle * Mathf.Deg2Rad;
        Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        float groundHeight = Mathf.Max(world.SampleTerrainHeight(angle, 0f), world.seaLevel);
        drone.transform.position = world.transform.TransformPoint(outward *
            (radius - groundHeight - droneSurfaceClearance));
        // Fly along the habitat; the model's +Y and fans point inward.
        drone.transform.rotation = Quaternion.LookRotation(Vector3.forward, -outward);
        var gravity = drone.GetComponent<CylinderGravity>();
        if (gravity == null) gravity = drone.AddComponent<CylinderGravity>();
        gravity.world = world;

        Camera camera = drone.GetComponentInChildren<Camera>();
        if (camera == null)
        {
            var cameraObject = new GameObject("Drone Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(drone.transform, false);
            camera = cameraObject.GetComponent<Camera>();
        }
        camera.tag = "MainCamera";
        camera.fieldOfView = 75f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 50000f;
        camera.depthTextureMode |= DepthTextureMode.Depth;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.backgroundColor = Color.black;

        var sunObject = new GameObject("Sun");
        var sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        var sunShadows = sunObject.AddComponent<CylinderSunShadows>();
        sunShadows.world = world;
        sunShadows.chunks = manager;
        sunShadows.sun = sun;
        sunShadows.glassMaterial = windowMaterial;
        sunShadows.Refresh();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.45f, .55f, .65f);
        RenderSettings.ambientEquatorColor = new Color(.30f, .35f, .30f);
        RenderSettings.ambientGroundColor = new Color(.12f, .12f, .12f);

        if (runtimeWaterMaterial != null)
        {
            var water = worldObject.AddComponent<CylinderWaterSimulation>();
            water.world = world;
            water.chunks = manager;
            water.follow = camera.transform;
            water.drone = drone.GetComponent<DroneRig>();
            water.surfaceMaterial = runtimeWaterMaterial;
            water.compute = waterCompute;
            water.flowMap = flowMap;
            water.mistShader = mistShader;
            water.responseProfile = downwashProfile;
            water.Initialize();
            var buoyancy = drone.GetComponent<CylinderBuoyancy>();
            if (buoyancy == null) buoyancy = drone.AddComponent<CylinderBuoyancy>();
            buoyancy.world = world;
            buoyancy.water = water;
            buoyancy.hull = drone.GetComponent<BoxCollider>();
        }
        var floating = new GameObject("Floating Origin").AddComponent<FloatingOrigin>();
        floating.player = drone.transform;
        Debug.Log("O'NEILL CYLINDER READY — 3 land/window pairs, solid terrain and custom VWater.\n" +
            "WASD: movement | Space/C: vertical | Mouse: pitch/yaw | Q/E: roll | Shift: boost | Escape: release mouse");
    }

    private void OnDestroy()
    {
        if (runtimeWaterMaterial == null) return;
        if (Application.isPlaying) Destroy(runtimeWaterMaterial);
        else DestroyImmediate(runtimeWaterMaterial);
    }
}
