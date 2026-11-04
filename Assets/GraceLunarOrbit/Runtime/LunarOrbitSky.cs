using System;
using UnityEngine;

namespace GraceLunarOrbit
{
    [DisallowMultipleComponent]
    public sealed class LunarOrbitSky : MonoBehaviour
    {
        [Header("Existing habitat (never moved by this component)")]
        public Transform habitatOrigin;
        public Camera habitatCamera;
        [Tooltip("Assign the existing bootstrap Sun to avoid a second directional light.")]
        public Light sunlight;
        [Tooltip("Assign Grace/Lunar Orbit Sky. A serialized reference keeps it in player builds.")]
        public Shader skyShader;
        public Texture2D moonAlbedo;

        [Header("Circular lunar orbit (SI units)")]
        [Min(1f)] public float moonRadiusMetres = 1737400f;
        public double moonMu = 4904869500000.0;
        [Min(0f)] public float altitudeMetres = 100000f;
        public float inclinationDegrees = 20f;
        public float ascendingNodeDegrees;
        public float initialOrbitDegrees;

        [Header("Station spin: must match existing rotating-frame physics")]
        [Min(1f)] public float habitatRadiusMetres = 4000f;
        [Min(0f)] public float surfaceGravity = 9.81f;
        public float initialSpinDegrees;
        [Tooltip("Fixed inertial axis. Keep zero for the default Z-axis habitat.")]
        public Vector3 inertialAxisEuler;

        [Header("Sun and lunar map")]
        public Vector3 directionToSunInertial = new Vector3(1f, 0.2f, 0.1f);
        [Range(0.1f, 2f)] public float sunAngularDiameterDegrees = 0.533f;
        public float moonMapLongitudeOffsetDegrees;
        [Min(1f)] public float moonRotationPeriodSeconds = 2360591.5f;

        [Header("Optional artistic reflected Moon fill (not photometric)")]
        public bool enableMoonFillLight;
        [Range(0f, 0.2f)] public float moonFillIntensity = 0.04f;
        public Color moonFillColor = new Color(0.82f, 0.85f, 0.9f);

        [Header("Clock")]
        [Min(0f)] public float timeMultiplier = 1f;
        public bool paused;
        public double simulationSeconds;

        public Vector3 StationPositionKm { get; private set; }
        public Quaternion HabitatToInertial { get; private set; }
        public float SunVisibility { get; private set; } = 1f;
        public double OrbitPeriodSeconds => LunarOrbitMath.OrbitalPeriod(moonMu,
            LunarOrbitMath.OrbitRadius(moonRadiusMetres, altitudeMetres));
        public double SpinPeriodSeconds => surfaceGravity > 0f
            ? 2.0 * Math.PI / LunarOrbitMath.SpinRate(surfaceGravity, habitatRadiusMetres)
            : double.PositiveInfinity;

        private Material ownedSky;
        private Material previousSky;
        private CameraClearFlags previousClearFlags;
        private float previousIntensity;
        private Quaternion previousLightRotation;
        private Light previousRenderSun;
        private Light ownedMoonFill;
        private bool initialized;
        private bool ownsLight;
        private bool started;
        private static LunarOrbitSky activeSky;

        private void Start() { started = true; Initialize(); }

        private void Initialize()
        {
            if (habitatOrigin == null || habitatCamera == null || skyShader == null)
            {
                Debug.LogError("LunarOrbitSky needs a habitat origin, camera and sky shader. Nothing was changed.", this);
                enabled = false;
                return;
            }
            if (activeSky != null && activeSky != this)
            {
                Debug.LogError("Only one LunarOrbitSky can own the global sky at a time.", this);
                enabled = false;
                return;
            }
            try { EvaluatePose(); }
            catch (ArgumentOutOfRangeException error)
            {
                Debug.LogError("Invalid lunar orbit settings: " + error.Message, this);
                enabled = false;
                return;
            }
            activeSky = this;
            previousSky = RenderSettings.skybox;
            previousRenderSun = RenderSettings.sun;
            previousClearFlags = habitatCamera.clearFlags;
            ownedSky = new Material(skyShader) { name = "Lunar Orbit Sky (runtime)" };
            RenderSettings.skybox = ownedSky;
            habitatCamera.clearFlags = CameraClearFlags.Skybox;
            if (sunlight == null)
            {
                GameObject go = new GameObject("Lunar Orbit Sun");
                go.transform.SetParent(transform, false);
                sunlight = go.AddComponent<Light>();
                sunlight.type = LightType.Directional;
                sunlight.intensity = 1.1f;
                sunlight.shadows = LightShadows.Soft;
                ownsLight = true;
                Debug.LogWarning("Created a Sun. Disable any other directional sun, or assign it instead.", this);
            }
            previousIntensity = sunlight.intensity;
            previousLightRotation = sunlight.transform.rotation;
            RenderSettings.sun = sunlight;
            initialized = true;
            ApplyPresentation();
        }

        private void Update()
        {
            if (!initialized) return;
            if (!paused) simulationSeconds += Time.deltaTime * Math.Max(0.0, timeMultiplier);
            try { EvaluatePose(); }
            catch (ArgumentOutOfRangeException error)
            {
                Debug.LogError("Invalid lunar orbit settings: " + error.Message, this);
                enabled = false;
                return;
            }
        }

        // FloatingOrigin currently runs in LateUpdate. Rendering from the camera
        // callback observes the final rebased camera/origin, without an order race.
        private void OnEnable()
        {
            Camera.onPreCull += BeforeCamera;
            if (started) Initialize();
        }
        private void BeforeCamera(Camera camera)
        {
            if (initialized && camera == habitatCamera) ApplyPresentation();
        }

        public void EvaluatePose()
        {
            ValidateSettings();
            double r = LunarOrbitMath.OrbitRadius(moonRadiusMetres, altitudeMetres);
            double orbit = LunarOrbitMath.Angle(initialOrbitDegrees * Math.PI / 180.0,
                LunarOrbitMath.AngularSpeed(moonMu, r), simulationSeconds);
            Quaternion plane = Quaternion.AngleAxis(ascendingNodeDegrees, Vector3.forward) *
                Quaternion.AngleAxis(inclinationDegrees, Vector3.right);
            StationPositionKm = plane * new Vector3(
                (float)(LunarOrbitMath.OrbitX(r, orbit) / 1000.0),
                (float)(LunarOrbitMath.OrbitY(r, orbit) / 1000.0), 0f);
            double spin = LunarOrbitMath.Angle(initialSpinDegrees * Math.PI / 180.0,
                LunarOrbitMath.SpinRate(surfaceGravity, habitatRadiusMetres), simulationSeconds);
            HabitatToInertial = Quaternion.Euler(inertialAxisEuler) *
                Quaternion.AngleAxis((float)(spin * 180.0 / Math.PI), Vector3.forward);
        }

        private static void RequireFinite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException("Orbit settings must be finite");
        }

        private void ValidateSettings()
        {
            RequireFinite(simulationSeconds); RequireFinite(timeMultiplier);
            RequireFinite(inclinationDegrees); RequireFinite(ascendingNodeDegrees);
            RequireFinite(initialOrbitDegrees); RequireFinite(initialSpinDegrees);
            RequireFinite(inertialAxisEuler.x); RequireFinite(inertialAxisEuler.y); RequireFinite(inertialAxisEuler.z);
            RequireFinite(directionToSunInertial.x); RequireFinite(directionToSunInertial.y); RequireFinite(directionToSunInertial.z);
            RequireFinite(sunAngularDiameterDegrees); RequireFinite(moonMapLongitudeOffsetDegrees);
            RequireFinite(moonRotationPeriodSeconds); RequireFinite(moonFillIntensity);
            if (sunAngularDiameterDegrees <= 0f || sunAngularDiameterDegrees >= 180f || moonRotationPeriodSeconds <= 0f)
                throw new ArgumentOutOfRangeException("Sun diameter / Moon rotation period");
            if (sunlight != null && sunlight.type != LightType.Directional)
                throw new ArgumentOutOfRangeException("Assign a directional Sun light");
        }

        private void ApplyPresentation()
        {
            if (habitatOrigin == null || habitatCamera == null || sunlight == null) return;
            Quaternion inertialToHabitat = Quaternion.Inverse(HabitatToInertial);
            Quaternion habitatToWorld = habitatOrigin.rotation;
            Vector3 cameraOffsetKm = habitatOrigin.InverseTransformDirection(
                habitatCamera.transform.position - habitatOrigin.position) / 1000f;
            Vector3 moonFromCamera = inertialToHabitat * -StationPositionKm - cameraOffsetKm;
            Vector3 sunInertial = directionToSunInertial.sqrMagnitude > 0.000001f
                ? directionToSunInertial.normalized : Vector3.right;
            Vector3 sunLocal = inertialToHabitat * sunInertial;
            Vector3 sunWorld = habitatToWorld * sunLocal;
            float distance = moonFromCamera.magnitude;
            double moonAngle = Math.Asin(Math.Min(1.0, moonRadiusMetres / (1000.0 * Math.Max(0.001, distance))));
            double separation = Vector3.Angle(moonFromCamera, sunLocal) * Math.PI / 180.0;
            SunVisibility = (float)LunarOrbitMath.SunVisibility(separation, moonAngle,
                Math.Max(0.001, sunAngularDiameterDegrees) * Math.PI / 360.0);
            sunlight.intensity = previousIntensity * SunVisibility;
            sunlight.transform.rotation = Quaternion.LookRotation(-sunWorld,
                Mathf.Abs(Vector3.Dot(sunWorld, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);
            if (enableMoonFillLight && ownedMoonFill == null)
            {
                GameObject fill = new GameObject("Moon fill (artistic approximation)");
                fill.transform.SetParent(transform, false);
                ownedMoonFill = fill.AddComponent<Light>();
                ownedMoonFill.type = LightType.Directional;
                ownedMoonFill.shadows = LightShadows.None;
                ownedMoonFill.cullingMask = sunlight.cullingMask;
            }
            if (ownedMoonFill != null)
            {
                ownedMoonFill.enabled = enableMoonFillLight;
                ownedMoonFill.color = moonFillColor;
                ownedMoonFill.intensity = Mathf.Clamp(moonFillIntensity,0f,0.2f) *
                    (float)LunarOrbitMath.MoonFillWeight(moonAngle,Vector3.Dot(-moonFromCamera.normalized,sunLocal));
                Vector3 incoming = habitatToWorld * -moonFromCamera.normalized;
                ownedMoonFill.transform.rotation = Quaternion.LookRotation(incoming,
                    Mathf.Abs(Vector3.Dot(incoming,Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);
            }
            ownedSky.SetVector("_MoonCenterKm", habitatToWorld * moonFromCamera);
            ownedSky.SetFloat("_MoonRadiusKm", moonRadiusMetres / 1000f);
            ownedSky.SetVector("_SunDirection", sunWorld);
            ownedSky.SetFloat("_SunCosRadius", Mathf.Cos(sunAngularDiameterDegrees * Mathf.Deg2Rad * 0.5f));
            ownedSky.SetFloat("_HasMoonMap", moonAlbedo != null ? 1f : 0f);
            if (moonAlbedo != null) ownedSky.SetTexture("_MoonMap", moonAlbedo);
            double lunarAngle = LunarOrbitMath.Angle(0.0, 2.0 * Math.PI /
                Math.Max(1.0, moonRotationPeriodSeconds), simulationSeconds);
            Quaternion moonToInertial = Quaternion.AngleAxis((float)(lunarAngle * 180.0 / Math.PI) +
                moonMapLongitudeOffsetDegrees, Vector3.forward);
            Quaternion worldToMoon = Quaternion.Inverse(moonToInertial) * HabitatToInertial *
                Quaternion.Inverse(habitatToWorld);
            ownedSky.SetMatrix("_WorldToMoon", Matrix4x4.Rotate(worldToMoon));
            ownedSky.SetMatrix("_WorldToInertial", Matrix4x4.Rotate(HabitatToInertial *
                Quaternion.Inverse(habitatToWorld)));
        }

        private void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            if (!initialized) return;
            if (RenderSettings.skybox == ownedSky) RenderSettings.skybox = previousSky;
            if (RenderSettings.sun == sunlight) RenderSettings.sun = previousRenderSun;
            if (habitatCamera != null && habitatCamera.clearFlags == CameraClearFlags.Skybox)
                habitatCamera.clearFlags = previousClearFlags;
            if (sunlight != null)
            {
                if (ownsLight) { Destroy(sunlight.gameObject); sunlight = null; }
                else { sunlight.intensity = previousIntensity; sunlight.transform.rotation = previousLightRotation; }
            }
            if (ownedMoonFill != null) { Destroy(ownedMoonFill.gameObject); ownedMoonFill = null; }
            if (ownedSky != null) Destroy(ownedSky);
            if (activeSky == this) activeSky = null;
            initialized = false;
            ownsLight = false;
        }
    }
}
