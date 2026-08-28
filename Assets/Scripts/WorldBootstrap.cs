using UnityEngine;

public class WorldBootstrap : MonoBehaviour
{
    [Header("Cylinder")]
    public float radius = 4000f;
    public float length = 32000f;


    [Header("Custom Drone")]
    public GameObject dronePrefab;
    public float droneStartRadius = 1000f;
    [Header("Drone Start")]
    [Tooltip("How far inside the terrain surface the drone starts.")]
    // Terrain can protrude up to 50 m inward, so this safely starts in open air.
    [Min(50.1f)] public float droneSurfaceClearance = 75f;

    private void Awake()
    {
        BuildWorld();
    }

    private void BuildWorld()
    {
        // -----------------------------
        // WORLD
        // -----------------------------

        GameObject worldObject =
            new GameObject("ONeillWorld");

        ONeillWorld world =
            worldObject.AddComponent<ONeillWorld>();

        world.radius = radius;
        world.length = length;

        // -----------------------------
        // MATERIAL
        // -----------------------------

        Material land =
            new Material(
                Shader.Find("Standard")
            );

        land.name =
            "Procedural Land";

        land.color =
            new Color(
                0.20f,
                0.42f,
                0.16f
            );

        land.SetFloat(
            "_Glossiness",
            0.05f
        );

        // -----------------------------
        // CHUNKS
        // -----------------------------

        GameObject chunksObject =
            new GameObject(
                "Cylinder Chunks"
            );

        CylinderChunkManager manager =
            chunksObject.AddComponent<
                CylinderChunkManager
            >();

        manager.world =
            world;

        manager.landMaterial =
            land;

        // -----------------------------
        // DRONE
        // -----------------------------

        GameObject drone;
        if (dronePrefab != null){
            drone = Instantiate(
                dronePrefab,
                new Vector3(droneStartRadius,0f,0f),
                Quaternion.identity
            );
        }
        else {
            Debug.LogWarning(
                "No drone prefab assigned. Creating placeholder"
            );

            drone = 
                GameObject.CreatePrimitive(PrimitiveType.Sphere);

            Rigidbody rb =
                drone.AddComponent<Rigidbody>();
            rb.mass = 10f;
            rb.useGravity = false;

            drone.GetComponent<SphereCollider>().radius = 1f;

            drone.transform.position =
                new Vector3(droneStartRadius,0f,0f);

            DroneController controller =
                drone.AddComponent<
                    DroneController
            >();
        }

        // Start just inside the +X surface, looking across the habitat.
        drone.transform.position = new Vector3(
            radius - droneSurfaceClearance,
            0f,
            0f
        );

        // Forward points inward; Z is used as a stable horizon direction.
        drone.transform.rotation = Quaternion.LookRotation(
            Vector3.left,
            Vector3.forward
        );

        CylinderGravity gravity =
            drone.AddComponent<
                CylinderGravity
            >();

        gravity.world =
            world;

        // -----------------------------
        // CAMERA
        // -----------------------------
        if(drone.GetComponent<Camera>()==null){
        GameObject cameraObject =
            new GameObject(
                "Drone Camera"
            );

        cameraObject.transform.SetParent(
            drone.transform,
            false
        );

        cameraObject.transform.localPosition =
            Vector3.zero;

        cameraObject.transform.localRotation =
            Quaternion.identity;

        Camera camera =
            cameraObject.AddComponent<
                Camera
            >();

        camera.fieldOfView = 75f;

        camera.nearClipPlane = 0.1f;

        camera.farClipPlane = 50000f;

        // The gaps between terrain strips are the cylinder's windows.
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        cameraObject.AddComponent<
            AudioListener
        >();
        }
        // -----------------------------
        // SUN
        // -----------------------------

        GameObject sunObject =
            new GameObject("Sun");

        Light sun =
            sunObject.AddComponent<Light>();

        sun.type =
            LightType.Directional;

        sun.intensity = 1.1f;

        sun.shadows =
            LightShadows.Soft;

        sunObject.transform.rotation =
            Quaternion.Euler(
                35f,
                -30f,
                0f
            );

        // -----------------------------
        // AMBIENT LIGHT
        // -----------------------------

        RenderSettings.ambientMode =
            UnityEngine.Rendering
                .AmbientMode.Trilight;

        RenderSettings.ambientSkyColor =
            new Color(
                0.45f,
                0.55f,
                0.65f
            );

        RenderSettings.ambientEquatorColor =
            new Color(
                0.30f,
                0.35f,
                0.30f
            );

        RenderSettings.ambientGroundColor =
            new Color(
                0.12f,
                0.12f,
                0.12f
            );

        // -----------------------------
        // FLOATING ORIGIN
        // -----------------------------

        GameObject floatingObject =
            new GameObject(
                "Floating Origin"
            );

        FloatingOrigin floating =
            floatingObject.AddComponent<
                FloatingOrigin
            >();

        floating.player =
            drone.transform;

        // -----------------------------
        // INFO
        // -----------------------------

        Debug.Log(
            "O'NEILL CYLINDER READY\n" +
            "WASD: movement\n" +
            "Space/C: vertical\n" +
            "Mouse: pitch/yaw\n" +
            "Q/E: roll\n" +
            "Shift: boost\n" +
            "Escape: release mouse"
        );
    }
}
