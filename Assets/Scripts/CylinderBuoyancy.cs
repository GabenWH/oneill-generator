using UnityEngine;

/// <summary>Displaces the root box hull against the cylinder's curved, terrain-masked sea.</summary>
[RequireComponent(typeof(Rigidbody))]
public class CylinderBuoyancy : MonoBehaviour
{
    public ONeillWorld world;
    public CylinderWaterSimulation water;
    public BoxCollider hull;
    [Tooltip("Hull density divided by water density. Values below one float.")]
    [Min(.01f)] public float relativeDensity = .35f;
    [Tooltip("Linear velocity damping per immersed second.")]
    [Min(0f)] public float linearWaterDrag = 2f;
    [Tooltip("Angular velocity damping per immersed second.")]
    [Min(0f)] public float angularWaterDrag = 3f;

    public float submergedFraction => currentSubmergedFraction;

    private const int SamplesX = 2, SamplesY = 2, SamplesZ = 3;
    private const float SampleWeight = 1f / (SamplesX * SamplesY * SamplesZ);
    private Rigidbody body;
    private float currentSubmergedFraction;

    private void Awake() => ResolveHull();

    private void ResolveHull()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (hull == null) hull = GetComponent<BoxCollider>();
        if (world == null && water != null) world = water.world;
    }

    private void FixedUpdate() => ApplyWaterForces(Time.fixedDeltaTime);

    /// <summary>Applies distributed buoyancy and exponential water damping for one physics step.</summary>
    public void ApplyWaterForces(float deltaTime)
    {
        ResolveHull();
        if (body == null || body.isKinematic || deltaTime <= 0f) return;
        SampleForces(true, out _, out _);
        if (currentSubmergedFraction <= 0f) return;
        body.velocity *= Mathf.Exp(-Mathf.Max(0f, linearWaterDrag) * currentSubmergedFraction * deltaTime);
        body.angularVelocity *= Mathf.Exp(-Mathf.Max(0f, angularWaterDrag) * currentSubmergedFraction * deltaTime);
    }

    /// <summary>Reports buoyant acceleration and moment per unit mass without applying forces or drag.</summary>
    public void EvaluateForces(out Vector3 acceleration, out Vector3 torquePerMass)
        => SampleForces(false, out acceleration, out torquePerMass);

    private void SampleForces(bool apply, out Vector3 acceleration, out Vector3 torquePerMass)
    {
        ResolveHull();
        acceleration = torquePerMass = Vector3.zero;
        currentSubmergedFraction = 0f;
        if (body == null || hull == null || hull.attachedRigidbody != body || world == null ||
            water == null || water.chunks == null || world.radius <= 0f || world.WaterRadius <= 0f ||
            hull.size.x <= 0f || hull.size.y <= 0f || hull.size.z <= 0f) return;

        // Transform the actual collider, including its offset, orientation, and scale.
        Matrix4x4 hullToCylinder = world.transform.worldToLocalMatrix * hull.transform.localToWorldMatrix;
        Vector3 cellSize = new Vector3(hull.size.x / SamplesX, hull.size.y / SamplesY, hull.size.z / SamplesZ);
        Vector3 halfX = hullToCylinder.MultiplyVector(Vector3.right * cellSize.x * .5f);
        Vector3 halfY = hullToCylinder.MultiplyVector(Vector3.up * cellSize.y * .5f);
        Vector3 halfZ = hullToCylinder.MultiplyVector(Vector3.forward * cellSize.z * .5f);
        Vector3 centerOfMass = body.worldCenterOfMass;
        float density = Mathf.Max(.01f, relativeDensity);
        for (int z = 0; z < SamplesZ; z++)
            for (int y = 0; y < SamplesY; y++)
                for (int x = 0; x < SamplesX; x++)
                {
                    Vector3 hullPoint = hull.center + new Vector3(
                        (x + .5f) * cellSize.x - hull.size.x * .5f,
                        (y + .5f) * cellSize.y - hull.size.y * .5f,
                        (z + .5f) * cellSize.z - hull.size.z * .5f);
                    Vector3 local = hullToCylinder.MultiplyPoint3x4(hullPoint);
                    if (Mathf.Abs(local.z) >= world.length * .5f) continue;
                    float radius = new Vector2(local.x, local.y).magnitude;
                    if (radius < .0001f) continue;
                    float angle = Mathf.Repeat(Mathf.Atan2(local.y, local.x), Mathf.PI * 2f);
                    int sector = Mathf.FloorToInt(angle / (Mathf.PI * 2f) * water.chunks.angularChunks);
                    if (!water.chunks.IsLand(sector)) continue;
                    float terrainHeight = water.TerrainHeightAt(angle, local.z);
                    if (terrainHeight >= world.seaLevel) continue;

                    Vector3 radial = new Vector3(local.x / radius, local.y / radius, 0f);
                    float halfHeight = Mathf.Max(.0001f, Mathf.Abs(Vector3.Dot(radial, halfX)) +
                        Mathf.Abs(Vector3.Dot(radial, halfY)) + Mathf.Abs(Vector3.Dot(radial, halfZ)));
                    float seaRadius = water.SurfaceRadiusAtAngle(angle);
                    // The same polygon chord factor keeps shallow seabeds behind the faceted sea.
                    float groundRadius = (world.radius - terrainHeight) * seaRadius / world.WaterRadius;
                    float wetStart = Mathf.Max(radius - halfHeight, seaRadius);
                    float wetEnd = Mathf.Min(radius + halfHeight, groundRadius);
                    float fraction = Mathf.Clamp01((wetEnd - wetStart) / (2f * halfHeight));
                    if (fraction <= 0f) continue;

                    currentSubmergedFraction += fraction * SampleWeight;
                    Vector3 displacedCenter = local + radial * ((wetStart + wetEnd) * .5f - radius);
                    Vector3 point = world.transform.TransformPoint(displacedCenter);
                    Vector3 pointAcceleration = -world.GetCentrifugalAcceleration(point) *
                        (fraction * SampleWeight / density);
                    acceleration += pointAcceleration;
                    torquePerMass += Vector3.Cross(point - centerOfMass, pointAcceleration);
                    if (apply) body.AddForceAtPosition(pointAcceleration, point, ForceMode.Acceleration);
                }
        currentSubmergedFraction = Mathf.Clamp01(currentSubmergedFraction);
    }
}
