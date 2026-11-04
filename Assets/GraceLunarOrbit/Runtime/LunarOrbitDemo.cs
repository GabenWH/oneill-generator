using System;
using UnityEngine;

namespace GraceLunarOrbit
{
    // Separate visualization only: 1 scene unit = 1 km. Never attach this to a
    // gameplay scene; it creates its own reference Moon, station and camera.
    public sealed class LunarOrbitDemo : MonoBehaviour
    {
        public Texture2D moonAlbedo;
        public Shader referenceSurfaceShader;
        [Min(5000f)] public float altitudeMetres = 100000f;
        [Min(1f)] public float stationRadiusMetres = 4000f;
        [Min(1f)] public float stationLengthMetres = 32000f;
        [Min(0f)] public float surfaceGravity = 9.81f;
        [Min(0f)] public float timeMultiplier = 1f;
        public float inclinationDegrees = 20f;
        public Vector3 directionToSun = new Vector3(1f, 0.2f, 0.1f);
        public bool enableMoonFill = true;
        [Range(0f,0.2f)] public float moonFillIntensity = 0.04f;
        public bool paused;
        public double simulationSeconds;
        private Transform station;
        private Transform moon;
        private Transform sunDisc;
        private Camera view;
        private Light sun;
        private Light moonFill;
        private readonly System.Collections.Generic.List<Material> materials =
            new System.Collections.Generic.List<Material>();
        private float yaw = 160f;
        private float pitch = 20f;
        private float cameraDistance = 65f;
        private float lightVisibility = 1f;
        private Renderer[] stationRenderers;
        private MaterialPropertyBlock eclipse;

        private Material Surface(string name, Color color, Texture texture = null, bool unlit = false)
        {
            Material material = new Material(referenceSurfaceShader);
            material.name = name;
            material.color = unlit ? Color.black : color;
            material.SetColor("_EmissionColor", unlit ? color : Color.black);
            if (texture != null) material.mainTexture = texture;

            materials.Add(material);
            return material;
        }

        private GameObject Primitive(string name, PrimitiveType shape, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private void Start()
        {
            if (referenceSurfaceShader == null)
            {
                Debug.LogError("Assign Grace/Lunar Reference Surface before running the overview.", this);
                enabled = false;
                return;
            }
            Material lunar = Surface("Smooth Moon reference", Color.white, moonAlbedo);
            if (moonAlbedo == null) lunar.color = new Color(0.38f, 0.38f, 0.37f);
            moon = Primitive("Moon (smooth reference, no terrain)", PrimitiveType.Sphere, transform, lunar).transform;
            moon.gameObject.layer = 2; // Separate reference Moon from the station-only fill.
            moon.localScale = Vector3.one * (float)(2.0 * LunarOrbitMath.MoonRadius / 1000.0);
            station = new GameObject("Station visual only").transform;
            station.SetParent(transform, false);
            Material frame = Surface("Ivory frame", new Color(0.8f, 0.76f, 0.59f));
            Material land = Surface("Green habitat bands", new Color(0.25f, 0.5f, 0.2f));
            Material marker = Surface("Orange spin marker", new Color(1f, 0.29f, 0.05f), null, true);
            float radius = stationRadiusMetres / 1000f;
            float length = stationLengthMetres / 1000f;
            // Open cage with bands: a simplified scale marker, not the current station asset.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12f;
                Transform beam = Primitive("Axial beam " + i, PrimitiveType.Cube, station,
                    i == 0 ? marker : (i % 2 == 0 ? land : frame)).transform;
                beam.localPosition = new Vector3(Mathf.Cos(angle)*radius, Mathf.Sin(angle)*radius, 0f);
                beam.localRotation = Quaternion.AngleAxis(i*30f, Vector3.forward);
                beam.localScale = new Vector3(0.18f, i % 2 == 0 ? 1.1f : 0.18f, length);
            }
            for (int end = -1; end <= 1; end += 2)
                for (int i = 0; i < 48; i++)
                {
                    float angle = i * Mathf.PI * 2f / 48f;
                    Transform rim = Primitive("End rim", PrimitiveType.Cube, station, frame).transform;
                    rim.localPosition = new Vector3(Mathf.Cos(angle)*radius, Mathf.Sin(angle)*radius, end*length/2f);
                    rim.localRotation = Quaternion.AngleAxis(i*7.5f, Vector3.forward);
                    rim.localScale = new Vector3(0.18f, radius*2f*Mathf.Sin(Mathf.PI/48f)+0.03f,0.18f);
                }
            GameObject cameraObject = new GameObject("Overview Camera");
            cameraObject.transform.SetParent(transform, false);
            view = cameraObject.AddComponent<Camera>();
            view.nearClipPlane = 0.05f;
            view.farClipPlane = 30000f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            sun = new GameObject("Sunlight").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sunDisc = Primitive("Sun disc (angular-size proxy)", PrimitiveType.Sphere, transform,
                Surface("Sun", new Color(1f, 0.96f, 0.84f), null, true)).transform;
            sunDisc.gameObject.layer = 2;
            moonFill = new GameObject("Artistic Moon fill").AddComponent<Light>();
            moonFill.transform.SetParent(transform,false);
            moonFill.type = LightType.Directional;
            moonFill.shadows = LightShadows.None;
            moonFill.cullingMask = 1; // Only layer 0 station, not layer 2 Moon/Sun proxies.
            moonFill.color = new Color(0.82f,0.85f,0.9f);
            sunDisc.localScale = Vector3.one * (2f * 20000f * Mathf.Tan(0.533f * Mathf.Deg2Rad * 0.5f));
            stationRenderers = station.GetComponentsInChildren<Renderer>();
            eclipse = new MaterialPropertyBlock();
            UpdatePose();
        }

        private void Update()
        {
            if (station == null) return;
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.R)) simulationSeconds = 0.0;
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 3f;
                pitch = Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*3f,-80f,80f);
            }
            cameraDistance = Mathf.Clamp(cameraDistance-Input.mouseScrollDelta.y*5f,25f,2000f);
            if (!paused) simulationSeconds += Time.deltaTime * Math.Max(0.0,timeMultiplier);
            UpdatePose();
        }

        private void UpdatePose()
        {
            double radius = LunarOrbitMath.OrbitRadius(LunarOrbitMath.MoonRadius, Math.Max(0f,altitudeMetres));
            double angle = LunarOrbitMath.Angle(0.0,LunarOrbitMath.AngularSpeed(LunarOrbitMath.MoonMu,radius),simulationSeconds);
            station.localPosition = Quaternion.AngleAxis(inclinationDegrees,Vector3.right) * new Vector3(
                (float)(LunarOrbitMath.OrbitX(radius,angle)/1000.0),
                (float)(LunarOrbitMath.OrbitY(radius,angle)/1000.0),0f);
            double spin = LunarOrbitMath.Angle(0.0,LunarOrbitMath.SpinRate(Math.Max(0f,surfaceGravity),
                Math.Max(1f,stationRadiusMetres)),simulationSeconds);
            station.localRotation = Quaternion.AngleAxis((float)(spin*180.0/Math.PI),Vector3.forward);
            moon.localRotation = Quaternion.AngleAxis((float)(simulationSeconds/2360591.5*360.0),Vector3.forward) *
                Quaternion.Euler(90f,0f,0f);
            view.transform.localPosition = station.localPosition + Quaternion.Euler(pitch,yaw,0f)*Vector3.back*cameraDistance;
            view.transform.LookAt(station.position,transform.up);
            Vector3 direction = directionToSun.sqrMagnitude > 0.000001f ? directionToSun.normalized : Vector3.right;
            sun.transform.rotation = Quaternion.LookRotation(transform.TransformDirection(-direction),
                Mathf.Abs(Vector3.Dot(direction,Vector3.up)) > 0.99f ? transform.right : transform.up);
            double moonAngle = Math.Asin(LunarOrbitMath.MoonRadius/radius);
            lightVisibility = (float)LunarOrbitMath.SunVisibility(Vector3.Angle(-station.localPosition,direction)*Math.PI/180.0,
                moonAngle,0.533*Math.PI/360.0);
            moonFill.enabled = enableMoonFill;
            moonFill.intensity = Mathf.Clamp(moonFillIntensity,0f,0.2f) * (float)LunarOrbitMath.MoonFillWeight(
                moonAngle,Vector3.Dot(station.localPosition.normalized,direction));
            Vector3 incomingFill = transform.TransformDirection(station.localPosition.normalized);
            moonFill.transform.rotation = Quaternion.LookRotation(incomingFill,
                Mathf.Abs(Vector3.Dot(incomingFill,Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);
            // The Moon stays sunlit even when the station enters lunar shadow.
            // Only the station renderers receive the eclipse factor via property blocks.
            foreach (Renderer renderer in stationRenderers)
            {
                eclipse.SetColor("_Color",renderer.sharedMaterial.color * Mathf.Lerp(0.025f,1f,lightVisibility));
                renderer.SetPropertyBlock(eclipse);
            }
            sunDisc.localPosition = view.transform.localPosition + direction*20000f;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(15,15,470,280),GUI.skin.box);
            GUILayout.Label("LUNAR ORBIT / INERTIAL OVERVIEW");
            GUILayout.Label("Smooth Moon + simplified station marker. 1 unit = 1 km.");
            GUILayout.Label($"Altitude {altitudeMetres/1000f:F1} km | Time {simulationSeconds:F0} s | Sun {lightVisibility:P0}");
            GUILayout.Label("Right-drag: look around | Scroll: zoom | Space: pause | R: reset");
            GUILayout.Label("Playback multiplies BOTH orbit and spin. 1x is physical speed.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x")) timeMultiplier=1f;
            if (GUILayout.Button("10x")) timeMultiplier=10f;
            if (GUILayout.Button("60x")) timeMultiplier=60f;
            if (GUILayout.Button(paused ? "Resume" : "Pause")) paused=!paused;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("10 km illustrative orbit")) altitudeMetres=10000f;
            if (GUILayout.Button("100 km preview orbit")) altitudeMetres=100000f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button(enableMoonFill ? "Artistic Moon fill: ON" : "Artistic Moon fill: OFF")) enableMoonFill=!enableMoonFill;
            GUILayout.Label("Centre altitude only. No terrain/station-clearance validation.");
            GUILayout.EndArea();
        }
        private void OnDestroy() { foreach (Material material in materials) if (material != null) Destroy(material); }
    }
}
